using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// 
    /// </summary>
    public class MarketMeanessIndex : WindowIndicator<IndicatorDataPoint>
    {
        private readonly int _period;
        private RollingWindow<double> medianData;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="period"></param>
        public MarketMeanessIndex(string name, int period)
            : base(name, period)
        {

            medianData = new RollingWindow<double>(period);
            _period = period;
        }
        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="period">int - the number of periods in the indicator warmup</param>
        public MarketMeanessIndex(int period)
            : this("MMI" + period, period)
        {
        }

        private double Median()
        {
            int k;
            double median;

            int obs = medianData.Count;
            bool even = obs % 2 == 0;

            double[] array = medianData.OrderBy(x => x).ToArray();
            median = MathNet.Numerics.Statistics.ArrayStatistics.MedianInplace(array);
            
            return median;
        }

        protected override decimal ComputeNextValue(IReadOnlyWindow<IndicatorDataPoint> window, IndicatorDataPoint input)
        {
            medianData.Add((double)input.Value);
            var m = Median();
            if (!medianData.IsReady)
                return (decimal)m;
            int i, nh = 0, nl = 0;
            for (i = 1; i < medianData.Count; i++)
            {

                try
                {
                    if (medianData[i] > m && medianData[i] > medianData[i - 1])
                        nl++;
                    else if (medianData[i] < m && medianData[i] < medianData[i - 1])
                        nh++;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            return 100m * (nl + nh) / (medianData.Count - 1);

        }
    }
}
