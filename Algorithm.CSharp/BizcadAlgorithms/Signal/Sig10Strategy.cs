using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using QuantConnect.Indicators;
using QuantConnect.Orders;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.CSharp
{
    public class Sig10Strategy : BaseStrategy
    {
        #region "fields"

        private bool bReverseTrade = false;
        public decimal RevPct = 1.0015m; 
        private decimal _lossThreshhold = -50;
        private decimal _tolerance = -.001m;
        private int _period;     // used to size the length of the trendArray
        private Indicator _price;
        public InstantaneousTrend Trend;
        public Momentum TrendMomentum;
        public RollingWindow<decimal> MomentumWindow;
        public bool IsActive = true;
        public int TradeAttempts = 0;
        public OrderStatus Status = OrderStatus.None;
        public string comment = string.Empty;
        private SecurityHolding _holding;


        #endregion
        #region "Properties"
        /// <summary>
        /// The symbol being processed
        /// </summary>
        public Symbol symbol { get; set; }

        public int Id { get; set; }
        /// <summary>
        /// The entry price from the last trade, set from the outside
        /// </summary>
        public decimal nEntryPrice { get; set; }

        /// <summary>
        /// The crossover carried from one instance to the next via serialization
        /// set to 1 when (nTrig Greater Than the current trend)
        /// set to -1 when (nTrig less than the current trend)
        /// It needs to be public because it is Serialized
        /// </summary>
        public int xOver { get; set; }

        /// <summary>
        /// The trigger use in the decision process
        /// Used internally only, not serialized or set from the outside
        /// </summary>
        public decimal nTrig { get; set; }
        /// <summary>
        /// True if the the order was filled in the last trade.  Mostly used after Limit orders
        /// It needs to be public because it is set from the outside by checking the ticket in the Transactions collection
        /// </summary>
        public Boolean orderFilled { get; set; }
        /// <summary>
        /// A flag to disable the trading.  True means make the trade.  This is left over from the 
        /// InstantTrendStrategy where the trade was being made in the strategy.  
        /// </summary>
        //public Boolean maketrade { get; set; }
        /// <summary>
        /// The array used to keep track of the last n trend inputs
        /// It works like a RollingWindow by pushing down the [0] to [1] etc. before updating the [0]
        /// </summary>
        public decimal[] trendArray { get; set; }
        /// <summary>
        /// The bar count from the algorithm
        /// This is set each time through to the barcount in the algorithm
        /// </summary>
        public int Barcount { get; set; }
        /// <summary>
        /// The state of the portfolio.  This is pushed in each time it is run from the Portfolio
        /// it is not Serialized.
        /// </summary>
        public bool IsShort { get; set; }
        /// <summary>
        /// The state of the portfolio.  This is pushed in each time it is run from the Portfolio
        /// </summary>
        public bool IsLong { get; set; }
        /// <summary>
        /// Internal state variables.  This POCO is used to report the internal state of the Signal.
        /// </summary>
        public SigC sigC { get; set; }

        public decimal UnrealizedProfit { get; set; }

        private bool BarcountLT4 { get; set; }
        private bool NTrigLTEP { get; set; }
        private bool NTrigGTEP { get; set; }
        private bool NTrigGTTA0 { get; set; }
        private bool NTrigLTTA0 { get; set; }
        private bool ReverseTrade { get; set; }
        private bool xOverIsPositive { get; set; }
        private bool xOverisNegative { get; set; }
        private bool OrderFilled { get; set; }

        #endregion

        public Sig10Strategy(SecurityHolding sym, Indicator priceIdentity, int trendPeriod, decimal lossThreshhold, decimal tolerance, decimal revertPct)
        {

            trendArray = new decimal[trendPeriod + 1];       // initialized to 0.  Add a period for Deserialize to make IsReady true

            symbol = sym.Symbol;
            _holding = sym;
            _price = priceIdentity;
            _period = trendPeriod;
            _lossThreshhold = lossThreshhold;
            _tolerance = tolerance;
            RevPct = revertPct;
            Trend = new InstantaneousTrend(sym.Symbol.Value, 7, .24m).Of(priceIdentity);
            TrendMomentum = new Momentum(2).Of(Trend);
            MomentumWindow = new RollingWindow<decimal>(2);
            ActualSignal = OrderSignal.doNothing;

            Trend.Updated += (object sender, IndicatorDataPoint updated) =>
            {
                Barcount++;
                UpdateTrendArray(Trend.Current.Value);
                nTrig = Trend.Current.Value;
                if (Trend.IsReady)
                {
                    TrendMomentum.Update(Trend.Current);
                }
                if (TrendMomentum.IsReady) 
                    MomentumWindow.Add(TrendMomentum.Current.Value);
                if (MomentumWindow.IsReady) 
                    CheckSignal();
            };
        }

        public override void CheckSignal()
        {
            ActualSignal = OrderSignal.doNothing;
            if (Barcount < 4)
            {
                BarcountLT4 = true;
                comment = "Barcount < 4";
                nTrig = Trend.Current.Value;
                ActualSignal = OrderSignal.doNothing;
            }
            else
            {
                nTrig = 2m * trendArray[0] - trendArray[2];
                
                IsShort = _holding.IsShort;
                IsLong = _holding.IsLong;

                #region "Selection Logic Reversals"

                ActualSignal = CheckLossThreshhold(ref comment, ActualSignal);
                if (Barcount>17)
                    Debug.WriteLine("Bar count > 17");

                if (nTrig < (Math.Abs(nEntryPrice) / RevPct))
                {
                    NTrigLTEP = true;
                    if (IsLong)
                    {
                        ActualSignal = OrderSignal.revertToShort;
                        bReverseTrade = true;
                        ReverseTrade = true;
                        comment =
                            string.Format("nTrig {0} < (nEntryPrice {1} * RevPct {2}) {3} IsLong {4} )",
                                Math.Round(nTrig, 4),
                                nEntryPrice,
                                RevPct,
                                NTrigLTEP,
                                IsLong);
                    }
                    else
                    {
                        NTrigLTEP = false;
                    }
                }
                else
                {
                    if (nTrig > (Math.Abs(nEntryPrice) * RevPct))
                    {
                        NTrigGTEP = true;
                        if (IsShort)
                        {
                            ActualSignal = OrderSignal.revertToLong;
                            bReverseTrade = true;
                            ReverseTrade = true;
                            comment =
                                string.Format("nTrig {0} > (nEntryPrice {1} * RevPct {2}) {3} IsLong {4} )",
                                    Math.Round(nTrig, 4),
                                    nEntryPrice,
                                    RevPct,
                                    NTrigLTEP,
                                    IsLong);
                        }
                        else
                        {
                            NTrigGTEP = false;
                        }
                    }
                }

                #endregion
                #region "selection logic buy/sell"

                ActualSignal = CheckLossThreshhold(ref comment, ActualSignal);

                if (!bReverseTrade)
                {

                    if (nTrig > trendArray[0])
                    {
                        NTrigGTTA0 = true;
                        if (xOver == -1)
                        {
                            #region "If Not Long"
                            if (!IsLong)
                            {

                                if (!orderFilled)
                                {
                                    ActualSignal = OrderSignal.goLong;
                                    comment =
                                        string.Format(
                                            "nTrig {0} > trend {1} xOver {2} !IsLong {3} !orderFilled {4}",
                                            Math.Round(nTrig, 4),
                                            Math.Round(trendArray[0], 4),
                                            xOver,
                                            !IsLong,
                                            !orderFilled);
                                }
                                else
                                {
                                    ActualSignal = OrderSignal.goLongLimit;
                                    comment =
                                        string.Format(
                                            "nTrig {0} > trend {1} xOver {2} !IsLong {3} !orderFilled {4}",
                                            Math.Round(nTrig, 4),
                                            Math.Round(trendArray[0], 4),
                                            xOver,
                                            !IsLong,
                                            !orderFilled);

                                }
                            }
                            #endregion
                        }

                        if (comment.Length == 0)
                            comment = "Trigger over trend - setting xOver to 1";
                        xOver = 1;
                        xOverisNegative = xOver < 0;
                        xOverIsPositive = xOver > 0;
                    }
                    else
                    {
                        if (nTrig < trendArray[0])
                        {
                            NTrigLTTA0 = true;
                            if (xOver == 1)
                            {
                                #region "If Not Short"
                                if (!IsShort)
                                {
                                    if (!orderFilled)
                                    {
                                        ActualSignal = OrderSignal.goShort;
                                        comment =
                                            string.Format(
                                                "nTrig {0} < trend {1} xOver {2} !isShort {3} orderFilled {4}",
                                                Math.Round(nTrig, 4),
                                                Math.Round(trendArray[0], 4),
                                                xOver,
                                                !IsShort,
                                                !orderFilled);

                                    }
                                    else
                                    {
                                        ActualSignal = OrderSignal.goShortLimit;
                                        comment =
                                            string.Format(
                                                "nTrig {0} < trend {1} xOver {2} !isShort {3} orderFilled {4}",
                                                Math.Round(nTrig, 4),
                                                Math.Round(trendArray[0], 4),
                                                xOver,
                                                !IsShort,
                                                !orderFilled);

                                    }
                                }
                                #endregion
                            }
                            if (comment.Length == 0)
                                comment = "Trigger under trend - setting xOver to -1";
                            xOver = -1;
                            xOverisNegative = xOver < 0;
                            xOverIsPositive = xOver > 0;
                        }



                    }
                }

                #endregion

            }

        }
        private void UpdateTrendArray(decimal trendCurrent)
        {
            for (int i = trendArray.Length - 2; i >= 0; i--)
            {
                trendArray[i + 1] = trendArray[i];
            }
            trendArray[0] = trendCurrent;
        }

        private OrderSignal CheckLossThreshhold(ref string comment, OrderSignal retval)
        {
            //if (Barcount >= 376)
            //    System.Diagnostics.Debug.WriteLine("Barcount: " + Barcount);
            if (UnrealizedProfit < _lossThreshhold)
            {
                if (IsLong)
                {
                    retval = OrderSignal.goShortLimit;

                }
                if (IsShort)
                {
                    retval = OrderSignal.goLongLimit;

                }
                comment = string.Format("Unrealized loss exceeded {0}", _lossThreshhold);
                bReverseTrade = true;
            }
            return retval;
        }

        
        /// <summary>
        /// Convenience function which creates an IndicatorDataPoint
        /// </summary>
        /// <param name="time">DateTime - the bar time for the IndicatorDataPoint</param>
        /// <param name="value">decimal - the value for the IndicatorDataPoint</param>
        /// <returns>a new IndicatorDataPoint</returns>
        /// <remarks>I use this function to shorten the a Add call from 
        /// new IndicatorDataPoint(this.Time, value)
        /// Less typing.</remarks>
        private IndicatorDataPoint idp(DateTime time, decimal value)
        {
            return new IndicatorDataPoint(time, value);
        }
    }
}
