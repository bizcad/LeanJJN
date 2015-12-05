using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using QuantConnect;
using QuantConnect.Data;
using QuantConnect.Orders;
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
        private DateTime _endDate = new DateTime(2015, 09, 30);
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
        private bool noOvernight = true;

        /*===========| LW Control Panel |===========*/
        private const int DecyclePeriod = 15;
        private const int InvFisherPeriod = 290;
        private const decimal Threshold = 0.9m;
        private const decimal Tolerance = 0.001m;
        private bool resetAtEndOfDayLW = false;

        /*===========| MSA Control Panel |===========*/
        private const int SmoothedSeriesPeriod = 30;
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
        // Dictionary used to store the PSAR indicator for each symbol.
        private Dictionary<string, ParabolicStopAndReverse> PSARDict = new Dictionary<string, ParabolicStopAndReverse>();

        // This flag is used to indicate we've switched from a global, non changing
        // stop loss to a dynamic trailing stop using the PSAR.
        private Dictionary<string, bool> EnablePsarTrailingStop = new Dictionary<string, bool>();

        // This is the ticket from our stop loss order (exit)
        private Dictionary<string, OrderTicket> StopLossTickets = new Dictionary<string, OrderTicket>();

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

                // Populate the PSARDict
                PSARDict.Add(symbol, new ParabolicStopAndReverse(afStart: 0.01m, afIncrement: 0.001m, afMax: 0.2m));
                RegisterIndicator(symbol, PSARDict[symbol], Resolution.Minute);

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
                .At(9, 40)
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

        public override void OnData(Slice data)
        {

            foreach (var symbol in Symbols)
            {
                // Check if there is new data for the symbol.
                if (!data.ContainsKey(symbol)) continue;
                OrderSignal actualOrder = OrderSignal.doNothing;
                if (isNormalOperativeTime)
                {
                    UpdateOrderFlag(symbol);

                    if (!Portfolio[symbol].Invested)
                    {
                        actualOrder = ScanForEntry(symbol);
                    }
                    else
                    {
                        actualOrder = ScanForExit(symbol);
                    }
                }
                else if (isMarketAboutToClose && noOvernight)
                {
                    actualOrder = ClosePosition(symbol);
                }
                ExecuteOrder(symbol, actualOrder);

                TradeBarSeries.Add(new TradeBarRecord(Time,
                                                      symbol,
                                                      Securities[symbol].Close,
                                                      Triggers[symbol].DecycleTrend,
                                                      Triggers[symbol].InverseFisher,
                                                      Flaggers[symbol].SmoothedSeries,
                                                      PSARDict[symbol],
                                                      Enum.GetName(typeof(OrderSignal), Flaggers[symbol].ActualSignal),
                                                      Enum.GetName(typeof(OrderSignal), actualOrder)));
            }

        }



        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            string symbol = orderEvent.Symbol;
            int position = Portfolio[symbol].Quantity;
            var actualOrder = Transactions.GetOrderById(orderEvent.OrderId);

            switch (orderEvent.Status)
            {
                case OrderStatus.New:
                    Log("New order sent: " + actualOrder.ToString());
                    break;

                case OrderStatus.Submitted:
                    Log("Order Submitted: " + actualOrder.ToString());
                    break;

                case OrderStatus.PartiallyFilled:
                case OrderStatus.Filled:
                    Log("Order Filled: " + actualOrder.ToString());
                    break;

                case OrderStatus.Canceled:
                    Log("Order Canceled: " + actualOrder.ToString());
                    break;

                case OrderStatus.None:
                case OrderStatus.Invalid:
                default:
                    Log("WTF!!!!!");
                    break;
            }

            if (position > 0) Triggers[symbol].Position = StockState.longPosition;
            else if (position < 0) Triggers[symbol].Position = StockState.shortPosition;
            else Triggers[symbol].Position = StockState.noInvested;
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

        private void UpdateOrderFlag(string symbol)
        {
            // Update the flag only if there is some new signal.
            if (Flaggers[symbol].ActualSignal != OrderSignal.doNothing)
            {
                OrderFlags[symbol] = Flaggers[symbol].ActualSignal;
            }
        }

        /// <summary>
        /// Scans for entry opportunities.
        /// </summary>
        /// <param name="symbol">The symbol.</param>
        /// <returns>The actual order signal</returns>
        private OrderSignal ScanForEntry(string symbol)
        {
            // First approach, just check if the Trigger has the same signal as the Flag. 
            return (Triggers[symbol].ActualSignal == OrderFlags[symbol]) ? Triggers[symbol].ActualSignal : OrderSignal.doNothing;
        }

        private OrderSignal ScanForExit(string symbol)
        {
            return Triggers[symbol].ActualSignal;
        }

        /// <summary>
        /// Estimate the shares to operate in the next transaction given the stock weight and the kind of order.
        /// </summary>
        /// <param name="symbol">The symbol.</param>
        /// <param name="actualOrder">The actual order.</param>
        /// <returns></returns>
        private int? PositionShares(string symbol, OrderSignal actualOrder)
        {
            int? positionQuantity = null;
            int quantity = 0;
            decimal price = Securities[symbol].Price;

            // Handle negative portfolio weights.
            if (ShareSize[symbol] < 0)
            {
                if (actualOrder == OrderSignal.goLong) actualOrder = OrderSignal.goShort;
                else if (actualOrder == OrderSignal.goShort) actualOrder = OrderSignal.goLong;
            }

            switch (actualOrder)
            {
                case OrderSignal.goShort:
                case OrderSignal.goLong:
                    // In the first part the estimations are in ABSOLUTE VALUE!

                    // Estimated the desired quantity to achieve target-percent holdings.
                    quantity = Math.Abs(CalculateOrderQuantity(symbol, ShareSize[symbol]));
                    // Estimate the max allowed position in dollars and compare it with the desired one.
                    decimal maxOperationDollars = Portfolio.TotalPortfolioValue * maxPortfolioRiskPerPosition;
                    decimal operationDollars = quantity * price;
                    // If the desired is bigger than the max allowed operation, then estimate a new bounded quantity.
                    if (maxOperationDollars < operationDollars) quantity = (int)(maxOperationDollars / price);

                    if (actualOrder == OrderSignal.goLong)
                    {
                        // Check the margin availability.
                        quantity = (int)Math.Min(quantity, Portfolio.MarginRemaining / price);
                    }
                    else
                    {
                        // In case of short sales, the margin should be a 150% of the operation.
                        quantity = (int)Math.Min(quantity, Portfolio.MarginRemaining / (1.5m * price));
                        // Now adjust the sing correctly.
                        quantity *= -1;
                    }
                    break;

                case OrderSignal.closeShort:
                case OrderSignal.closeLong:
                    quantity = -Portfolio[symbol].Quantity;
                    break;

                default:
                    break;
            }

            // Only assign a value to the positionQuantity if is bigger than a threshold. If not, then it'll return null.
            if (Math.Abs(quantity) > minSharesPerTransaction)
            {
                positionQuantity = quantity;
            }

            return positionQuantity;
        }

        /// <summary>
        /// Executes the strategy.
        /// </summary>
        /// <param name="symbol">The symbol.</param>
        /// <param name="actualOrder">The actual order.</param>
        private void ExecuteOrder(string symbol, OrderSignal actualOrder)
        {
            int? shares = PositionShares(symbol, actualOrder);

            switch (actualOrder)
            {
                case OrderSignal.goLong:
                case OrderSignal.goShort:
                    // If the returned shares is null then is the same than doNothing.
                    if (shares.HasValue)
                    {
                        Log("===> Market entry order sent " + symbol);
                        MarketOrder(symbol, shares.Value);
                    }
                    break;

                case OrderSignal.closeLong:
                case OrderSignal.closeShort:
                    Log("<=== Closing Position " + symbol);
                    MarketOrder(symbol, shares.Value);
                    break;

                default: break;
            }
        }

        /// <summary>
        /// Closes all positions.
        /// </summary>
        /// <param name="symbol">The symbol.</param>
        /// <returns></returns>
        private OrderSignal ClosePosition(string symbol)
        {
            OrderSignal actualOrder;

            if (Portfolio[symbol].IsLong)
            {
                actualOrder = OrderSignal.closeLong;
                Log("<=== Closing EOD Long position " + symbol);
            }
            else if (Portfolio[symbol].IsShort)
            {
                actualOrder = OrderSignal.closeShort;
                Log("===> Closing EOD Short position " + symbol);
            }
            else actualOrder = OrderSignal.doNothing;
            return actualOrder;
        }

        private void CloseDay()
        {
            foreach (var symbol in Symbols)
            {
                OrderFlags[symbol] = OrderSignal.doNothing;
            }
        }
        #endregion
    }
}
