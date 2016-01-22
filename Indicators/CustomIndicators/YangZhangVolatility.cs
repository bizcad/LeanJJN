using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Data.Market;

namespace QuantConnect.Indicators
{
    /// <summary>

    /// Yang Zhang Volatility
    /// </summary>

    public class YangZhang : TradeBarIndicator
    {


        private int _period;

        /// <summary>
        /// Components of the YZ Volatitily
        /// </summary>


        public IndicatorBase<TradeBar> OcCp { get; private set; }
        public IndicatorBase<TradeBar> CcOc { get; private set; }
        public IndicatorBase<TradeBar> OHL { get; private set; }


        public IndicatorBase<IndicatorDataPoint> MuOpen { get; private set; }
        public IndicatorBase<IndicatorDataPoint> MuClose { get; private set; }


        public IndicatorBase<IndicatorDataPoint> CloseVol { get; private set; }
        public IndicatorBase<IndicatorDataPoint> OpenVol { get; private set; }
        public IndicatorBase<IndicatorDataPoint> RSVol { get; private set; }


        public override bool IsReady
        {
            get { return Samples > _period; }
        }

        public YangZhang(string name, int period)
            : base(name)
        {
            _period = period;
            MuClose = new SimpleMovingAverage("MuC", period);
            MuOpen = new SimpleMovingAverage("MuO", period);
            CloseVol = new Sum("CV", period);
            OpenVol = new Sum("OV", period);
            RSVol = new SimpleMovingAverage("OV", period);

            TradeBar previous = null;

            OcCp = new FunctionalIndicator<TradeBar>(name + "_e", currentBar =>
            {
                var nextValue = ComputeOcCp(previous, currentBar);
                previous = currentBar;
                return nextValue;
            }   // in our IsReady function we just need at least two sample
            , trueRangeIndicator => trueRangeIndicator.Samples >= _period
            );

            CcOc = new FunctionalIndicator<TradeBar>(name + "_", currentBar => ComputeCcOc(currentBar)
            , trueRangeIndicator => trueRangeIndicator.Samples >= _period);

            OHL = new FunctionalIndicator<TradeBar>(name + "_", currentBar => ComputeOHL(currentBar)
            , trueRangeIndicator => trueRangeIndicator.Samples >= _period);

        }


        public YangZhang(int period)
            : this("YangZhang" + period, period)
        {
        }


        public static decimal ComputeOcCp(TradeBar previous, TradeBar current)
        {

            if (previous == null)
            {
                return 0m;
            }

            return (decimal)Math.Log((double)(current.Open / previous.Close));
        }

        public static decimal ComputeCcOc(TradeBar current)
        {
            return (decimal)Math.Log((double)(current.Close / current.Open));
        }

        public static decimal ComputeOHL(TradeBar current)
        {
            var temp1 = Math.Log((double)(current.High / current.Close)) * Math.Log((double)(current.High / current.Open));
            var temp2 = Math.Log((double)(current.Low / current.Close)) * Math.Log((double)(current.Low / current.Open));
            return (decimal)(temp1 + temp2);
        }

        protected override decimal ComputeNextValue(TradeBar input)
        {

            decimal N = (decimal)_period;

            CcOc.Update(input);
            OcCp.Update(input);
            OHL.Update(input);

            MuOpen.Update(input.Time, OcCp.Current.Value);
            MuClose.Update(input.Time, CcOc.Current.Value);

            var delta_sq = Math.Pow((double)(OcCp.Current.Value - MuOpen.Current.Value), 2);
            OpenVol.Update(input.Time, (decimal)delta_sq);
            decimal SigmaOpen = OpenVol.Current.Value * (1 / (N - 1));

            var delta_sq2 = Math.Pow((double)(CcOc.Current.Value - MuClose.Current.Value), 2);
            CloseVol.Update(input.Time, (decimal)delta_sq2);
            decimal SigmaClose = CloseVol.Current.Value * (1 / (N - 1));

            RSVol.Update(input.Time, OHL.Current.Value);
            decimal SigmaRS = RSVol.Current.Value * (1 / (N - 1));

            decimal sum = (SigmaOpen + (decimal).16433333 * SigmaClose + (decimal).83566667 * SigmaRS);

            decimal res = (decimal)(Math.Sqrt((double)sum) * Math.Sqrt((double)252));

            return res;



        }


        public override void Reset()
        {

            MuClose.Reset();
            MuOpen.Reset();
            OHL.Reset();
            OcCp.Reset();
            CcOc.Reset();
            OpenVol.Reset();
            CloseVol.Reset();
            RSVol.Reset();
            base.Reset();
        }
    }
}