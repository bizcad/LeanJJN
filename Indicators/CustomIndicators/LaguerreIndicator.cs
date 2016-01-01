using System.Collections.Generic;

namespace QuantConnect.Indicators
{
    public class LaguerreIndicator : WindowIndicator<IndicatorDataPoint>
    {
        private decimal _gamma;                       // the factor constant
        private decimal _tolerance;
        private List<RollingWindow<decimal>> SeriesL = new List<RollingWindow<decimal>>(4);
        private decimal[] L = new decimal[4];

        public RollingWindow<IndicatorDataPoint> FIR { get; private set; }
        public RollingWindow<IndicatorDataPoint> Laguerre { get; private set; }
        public RollingWindow<IndicatorDataPoint> LaguerreRSI { get; private set; }

        public LaguerreIndicator(string name, decimal gamma, decimal Tolerance = 0.001m)
            : base(name, 4)
        {
            _gamma = gamma;
            _tolerance = Tolerance;

            FIR = new RollingWindow<IndicatorDataPoint>(4);
            Laguerre = new RollingWindow<IndicatorDataPoint>(2);
            LaguerreRSI = new RollingWindow<IndicatorDataPoint>(2);

            for (int i = 0; i < 4; i++)
            {
                SeriesL.Add(new RollingWindow<decimal>(2));
            }
        }

        public LaguerreIndicator(decimal gamma)
            : this("Laguerre" + gamma, gamma)
        { }

        public override void Reset()
        {
            base.Reset();
            for (int i = 0; i < 4; i++)
            {
                SeriesL[i].Reset();
            }
            FIR.Reset();
            Laguerre.Reset();
            LaguerreRSI.Reset();
        }

        protected override decimal ComputeNextValue(IReadOnlyWindow<IndicatorDataPoint> window, IndicatorDataPoint input)
        {
            if (!this.IsReady)
            {
                for (int i = 0; i < 4; i++)
                {
                    SeriesL[i].Add(input.Value);
                }
                FIR.Add(input);
                Laguerre.Add(input);
                LaguerreRSI.Add(new IndicatorDataPoint(input.Time, 0m));
            }
            else
            {
                EstimateFIR(window);
                EstimateSeriesL(input);
                EstimateLaguerre(input);
            }
            return Laguerre[0].Value;
        }

        private void EstimateFIR(IReadOnlyWindow<IndicatorDataPoint> window)
        {
            decimal actualFIR = (window[0] + 2m * window[1] + 2m * window[2] + window[3]) / 6m;
            FIR.Add(new IndicatorDataPoint(window[0].Time, actualFIR));
        }

        private void EstimateSeriesL(IndicatorDataPoint input)
        {
            // Estimate L0
            decimal actualL0 = (1m - _gamma) * input.Value + _gamma * SeriesL[0][0];
            SeriesL[0].Add(actualL0);
            L[0] = actualL0;
            // Estimate L1 to L3
            for (int i = 1; i < 4; i++)
            {
                decimal actualLx = -_gamma * SeriesL[i - 1][0] + SeriesL[i - 1][1] + _gamma * SeriesL[i][0];
                SeriesL[i].Add(actualLx);
                L[i] = actualLx;
            }
        }

        private void EstimateLaguerre(IndicatorDataPoint input)
        {
            decimal CU = 0m;
            decimal CD = 0m;

            if (L[0] >= L[1]) CU = L[0] - L[1]; else CD = L[1] - L[0];
            if (L[1] >= L[2]) CU = CU + L[1] - L[2]; else CD = CD + L[2] - L[1];
            if (L[2] >= L[3]) CU = CU + L[2] - L[3]; else CD = CD + L[3] - L[2];

            decimal actualLaguerreRSI = (CU + CD != 0) ? CU / (CU + CD) : 0.5m;
            LaguerreRSI.Add(new IndicatorDataPoint(input.Time, actualLaguerreRSI));

            decimal actualLaguerre = (L[0] + 2m * L[1] + 2m * L[2] + L[3]) / 6m;
            Laguerre.Add(new IndicatorDataPoint(input.Time, actualLaguerre));
        }
    }
}