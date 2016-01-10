using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Indicators.CustomIndicators
{
    /// <summary>
    /// Calculates a historical volatility for time series.
    /// </summary>
    /// <remarks>https://en.wikipedia.org/wiki/Volatility_(finance)
    /// Returns are not compounded and do not include dividends or splits. 
    /// On Resolution of ;lt 1 day this assumption will not present a problem.
    /// On Resolution of ;gt q day, the caller must make the adjustment, as would 
    ///     normally be the case in back test where TradeBars are adjusted.
    /// For live trading, this assumption could be problematic as the standard deviation would
    ///     have to be recalculated.  The algorithm author should call Reset() to
    ///     start a new StandardDeviation, which would return 0 for the first two periods.
    /// </remarks>
    public class HistoricalLogReturnsVolatility : WindowIndicator<IndicatorDataPoint>
    {
        private int _period;
        private string _name;
        private StandardDeviation _std;
        private readonly double _annualizationMulitplier;

        /// <summary>
        /// Creates a new HistoricalVolatility indicator with the specified period
        /// </summary>
        /// <param name="period">The period over which to perform to computation</param>
        /// <param name="resolution">The resolution of the period. eg. Resolution.Daily</param>
        public HistoricalLogReturnsVolatility(int period, Resolution resolution)
            : base("HVOL" + period, period)
        {
        }

        /// <summary>
        /// Creates a new RateOfChangePercent indicator with the specified period
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        /// <param name="period">The period over which to perform to computation</param>
        /// <param name="resolution">The Resolution of the period eb Resolution.Daily</param>        
        public HistoricalLogReturnsVolatility(string name, int period, Resolution resolution)
            : base(name, period)
        {
            _period = period;
            _name = name;
            _std = new StandardDeviation(period);
            switch (resolution)
            {
                case Resolution.Tick:
                case Resolution.Second:
                    _annualizationMulitplier = Math.Sqrt(60 * 60 * 24 * 252L);  // seconds, minutes, hours, trading days
                    break;
                case Resolution.Minute:
                    _annualizationMulitplier = Math.Sqrt(60 * 24 * 252L);  // minutes, hours, trading days
                    break;
                case Resolution.Hour:
                    _annualizationMulitplier = Math.Sqrt(24 * 252L);  // seconds, minutes, hours, trading days
                    break;
                case Resolution.Daily:
                    _annualizationMulitplier = Math.Sqrt(252L);
                    break;
            }
        }
        /// <summary>
        /// Creates a new Historical Volatility with a custom annualizer value
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        /// <param name="period">The period over which to perform to computation</param>
        /// <param name="customAnnualizerValue">The value to be multiplied against the Standard Deviation of log values</param>
        /// <remarks>
        /// This indicator usually uses the standard QuantConnect Resolutions, but if you are using a 
        ///     Consolidator the standard Resolutions may not apply.  This constructor allows you to set your
        ///     own annualiztionMultiplier.  For example, you may think that the market is open 262 days instead of 252.
        /// </remarks>
        public HistoricalLogReturnsVolatility(string name, int period, Double customAnnualizerValue)
            : base(name, period)
        {
            _period = period;
            _name = name;
            _std = new StandardDeviation(period);
            _annualizationMulitplier = customAnnualizerValue;
        }

        /// <summary>
        /// Also resets the StandardDeviation.  This should be used when there is a dividend or split.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            _std = new StandardDeviation(_period);
        }

        /// <summary>
        /// Computes the next value of this indicator from the given state
        /// </summary>
        /// <param name="input">The input given to the indicator</param>
        /// <param name="window">The window for the input history</param>
        /// <returns>A new value for this indicator</returns>
        protected override decimal ComputeNextValue(IReadOnlyWindow<IndicatorDataPoint> window, IndicatorDataPoint input)
        {
            var ret = Math.Log((double)window[0].Value /(double) window[1].Value - 1L);
            _std.Update(new IndicatorDataPoint(input.EndTime, (decimal)ret));
            Current = new IndicatorDataPoint(input.EndTime, System.Convert.ToDecimal((double)_std.Current.Value * _annualizationMulitplier));
            return Current.Value;
        }
    }
}
