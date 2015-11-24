using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using QuantConnect;
using QuantConnect.Indicators;

using Newtonsoft.Json;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// This algorithm uses two strategies:
    ///    - Using the MSA strategy, flags possibles mean reversions. 
    ///    - Waits for the LW signal, if the flag and the signal have both the same direction, sends the order.
    ///    - Once in the market, the position is followed by an trailing stop order.
    /// </summary>
    public class LittleWingPlusMSA : QCAlgorithm
    {
        #region Algorithm Control Panel
        /*===========| Algorithm Global Variables |===========*/
        private DateTime _startDate = new DateTime(2015, 07, 01);
        private DateTime _endDate = new DateTime(2015, 07, 05);
        private decimal _portfolioInitialCash = 26000 * 4;
        /// <summary>
        /// The symbols to be used by the strategy.
        /// </summary>
        private static string[] Symbols = { "AMZN", "BP", "JNJ", "JPM" };
        /// <summary>
        /// The maximum leverage.
        /// </summary>
        private const decimal maxLeverage = 1m;
        /// <summary>
        /// Percentage of portfolio used when estimating the positions.
        /// </summary>
        private decimal maxExposure = 0.60m;
        /// <summary>
        /// The maximum portfolio risk per position.
        /// </summary>
        private decimal maxPortfolioRiskPerPosition = 0.1m;
        /// <summary>
        /// The global stop loss percent used in the StopLossOrders.
        /// </summary>
        private const decimal GlobalStopLossPercent = 0.001m;
        /// <summary>
        /// The percent profit to start using the PSAR trailing stop.
        /// </summary>
        private const decimal PercentProfitStartPsarTrailingStop = 0.0003m;
        /// <summary>
        /// The minimum shares per transaction.
        /// </summary>
        private int minSharesPerTransaction = 10;
        /// <summary>
        /// Close all positions before market close.
        /// </summary>
        private bool noOvernight = false;

        /*===========| LW Control Panel |===========*/
        private const int DecyclePeriod = 10;
        private const int InvFisherPeriod = 270;
        private const decimal Threshold = 0.9m;
        private const decimal Tolerance = 0.001m;
        private bool resetAtEndOfDayLW = false;

        /*===========| MSA Control Panel |===========*/
        private const int SmoothedSeriesPeriod = 15;
        private const int PreviousDaysN = 20;
        private const int RunsPerDay = 5;
        private const decimal MinimumRunThreshold = 0.005m;
        private bool resetAtEndOfDayMSA = false;

        #endregion

        #region Fields and Properties
        // Flags the first minutes after market open.
        private bool isMarketJustOpen;

        // Flags the last minutes before market close.
        private bool isMarketAboutToClose;

        private Dictionary<string, OrderSignal> OrderFlags = new Dictionary<string, OrderSignal>();

        // Dictionary used to store the portfolio share-size for each symbol.
        private Dictionary<string, decimal> ShareSize = new Dictionary<string, decimal>();

        private bool isNormalOperativeTime
        {
            get
            {
                return !(isMarketJustOpen ||
                         isMarketAboutToClose);
            }
        }

        /*===========| LW Handlers |===========*/
        // Dictionary used to store the DIFStrategy object for each symbol.
        private Dictionary<string, DIFStrategy> Triggers = new Dictionary<string, DIFStrategy>();

        /*===========| MSA Handlers |===========*/
        // Dictionary used to store the MSAStrategy object for each symbol.
        private Dictionary<string, MSAStrategy> Flaggers = new Dictionary<string, MSAStrategy>();

        /*===========| Trailing Orders Handlers |===========*/

        /*===========| Logging Stuff |===========*/
        List<TradeBarRecord> TradeBarSeries = new List<TradeBarRecord>();

        #endregion

        #region QC Overridden methods
        public override void Initialize()
        {
            SetStartDate(_startDate);
            SetEndDate(_endDate);
            SetCash(_portfolioInitialCash);

            foreach (var symbol in Symbols)
            {
                AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);
                // Initialize the algorithm with an equally weighted portfolio.
                ShareSize.Add(symbol, (maxLeverage * maxExposure) / Symbols.Count());

                //Initialize the Flag dictionary.
                OrderFlags.Add(symbol, OrderSignal.doNothing);

                // Price identity to be injected in the DIFStrategy objects.
                var priceIdentity = Identity(symbol);
                // Smoothed price series to be injected in the MSAStrategy objects
                var smoothedSeries = new Decycle(SmoothedSeriesPeriod).Of(priceIdentity);

                // Populate the Flaggers dictionary with MSAStrategy objects. 
                Flaggers.Add(symbol, new MSAStrategy(smoothedSeries, PreviousDaysN, RunsPerDay, MinimumRunThreshold));

                // Populate the Triggers dictionary with LWStrategy objects.
                Triggers.Add(symbol, new DIFStrategy(priceIdentity, DecyclePeriod, InvFisherPeriod, Threshold, Tolerance, 0));

                // Strategy warm-up.
                var history = History(symbol, (PreviousDaysN + 1) * 390);
                foreach (var bar in history)
                {
                    priceIdentity.Update(bar.EndTime, bar.Close);
                }
            }

            #region Schedules

            // Set flags correctly at market open
            Schedule.Event("MarketOpen")
                .EveryDay()
                .At(9, 29)
                .Run(() =>
                {
                    isMarketJustOpen = true;
                    isMarketAboutToClose = false;
                    Log(string.Format("========== {0} Market Open ==========", Time.DayOfWeek));
                });

            Schedule.Event("MarketOpenSpan")
                .EveryDay()
                .At(9, 30)
                .Run(() => isMarketJustOpen = false);

            Schedule.Event("MarketAboutToClose")
                .EveryDay()
                .At(15, 50)
                .Run(() => isMarketAboutToClose = true);

            Schedule.Event("MarketClose")
                .EveryDay()
                .At(15, 59)
                .Run(() => CloseDay());

            #endregion Schedules
        }
             

        public override void OnData(Data.Slice data)
        {
            if (isNormalOperativeTime)
            {
                foreach (var symbol in Symbols)
                {
                    OrderSignal actualOrder = OrderSignal.doNothing;

                    if (!data.ContainsKey(symbol)) continue;

                    if (Portfolio[symbol].Invested)
                    {
                        actualOrder = ScanForEntry(symbol);
                    }
                    else
                    {
                        actualOrder = ScanForExit(symbol);
                    }
                    ExecuteOrder(symbol, actualOrder);

                    TradeBarSeries.Add(new TradeBarRecord(Time,
                                                          symbol,
                                                          Securities[symbol].Close,
                                                          Triggers[symbol].DecycleTrend,
                                                          Triggers[symbol].InverseFisher,
                                                          Flaggers[symbol].SmoothedSeries,
                                                          Enum.GetName(typeof(OrderSignal), Flaggers[symbol].ActualSignal),
                                                          Enum.GetName(typeof(OrderSignal), actualOrder)));
                }
            }
        }



        public override void OnEndOfAlgorithm()
        {
            var tradeBars = JsonConvert.SerializeObject(TradeBarSeries);
            File.WriteAllText("LW_MSA_series.json", tradeBars);

            var closedTrades = JsonConvert.SerializeObject(TradeBuilder.ClosedTrades);
            File.WriteAllText("LW_MSA_closedTrades.json", closedTrades);
            
            var transactions = JsonConvert.SerializeObject(Transactions.GetOrders(o => true));
            File.WriteAllText("LW_MSA_transactions.json", transactions);
        }
        # endregion

        # region Algorithm methods

        private OrderSignal ScanForExit(string symbol)
        {
            return Triggers[symbol].ActualSignal;
        }

        private OrderSignal ScanForEntry(string symbol)
        {
            return Flaggers[symbol].ActualSignal;
        }

        private void ExecuteOrder(string symbol, OrderSignal actualOrder)
        {
            return;
        }

        private void CloseDay()
        {
            return;
        }
        #endregion
    }
}
