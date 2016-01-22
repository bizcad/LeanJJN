using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Statistics;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// Finds a Two Bar Reversal pattern
    /// </summary>
    public class TwoBarReversalIndicator : TradeBarIndicator
    {
        /// <summary>
        /// The Rolling window of the two bars to compare
        /// </summary>
        public RollingWindow<TradeBar> BarsWindow;

        /// <summary>
        /// True for using the body for the comparison; false for high/low
        /// </summary>
        public bool UseBody = true;

        /// <summary>
        /// The min size for a bar to be considered a candidate for a reversal bar.
        /// </summary>
        public decimal MinimumBarSize = 0.3m;

        /// <summary>
        /// The max difference between two bodies or high/lows to be considered the same.
        /// </summary>
        public decimal BarDifferenceTolerance = 0.05m;

        /// <summary>
        /// Indicator to find a trade bar 2 bar reversal pattern.  This pattern is when the body
        /// or high and low of two bars in a row are the same size except one is a bullish bar
        /// and the other is a bearish bar.
        /// </summary>
        /// <param name="name">string - an optional name for the indicator</param>
        /// <param name="useBody">true to use the body, false to use the high/low</param>
        public TwoBarReversalIndicator(string name, bool useBody = true)
            : base(name)
        {
            UseBody = useBody;
            BarsWindow = new RollingWindow<TradeBar>(2);
        }

        /// <summary>
        /// The named constuctor
        /// </summary>
        /// <param name="useBody">true to use the body, false to use the high/low</param>
        public TwoBarReversalIndicator(bool useBody)
            : this("TBR" + useBody, useBody)
        {
        }


        public override bool IsReady
        {
            get { return BarsWindow.IsReady; }
        }

        /// <summary>
        /// Computes whether we have found a two bar pattern
        /// </summary>
        /// <param name="input">TradeBar - the current bar</param>
        /// <returns></returns>
        protected override decimal ComputeNextValue(TradeBar input)
        {
            BarsWindow.Add(input);
            if (!IsReady) return 0;
            if (Math.Abs(BarsWindow[0].Open - BarsWindow[0].Close) > MinimumBarSize)
            {

                if (UseBody)
                {
                    if (Math.Abs(BarsWindow[0].Open - BarsWindow[1].Close) < BarDifferenceTolerance &&
                        Math.Abs(BarsWindow[0].Close - BarsWindow[1].Open) < BarDifferenceTolerance)
                    {
                        // 1 for up bar, -1 for down bar
                        return BarsWindow[0].Open < BarsWindow[0].Close ? 1m : -1m;
                    }
                }
                else
                {
                    if (Math.Abs(BarsWindow[0].High - BarsWindow[1].Low) < BarDifferenceTolerance &&
                        Math.Abs(BarsWindow[0].Low - BarsWindow[1].High) < BarDifferenceTolerance)
                    {
                        // 1 for up bar, -1 for down bar
                        return BarsWindow[0].Low < BarsWindow[0].High ? 1m : -1m;
                    }                    
                }
                
            }
            return 0;

        }
    }
}
