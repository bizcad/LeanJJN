using System;
using System.IO;
using System.Text;
using QuantConnect.Algorithm;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;

namespace QuantConnect.Algorithm.CSharp
{
    /*
    *   John Ehlers' MAMA and FAMA 
    *	(programmed by Jean-Paul van Brakel)
    */
    public class FamaMamaAlgorithm : QCAlgorithm
    {
        public string _ticker = "AAPL"; 				// which stock ticker
        public static int _consolidated_minutes = 10;	// number of minutes
        public static double MAMA_FastLimit = 0.10;		// fast parameter
        public static double MAMA_SlowLimit = 0.001;	// slow parameter
        private TradeBarConsolidator consolidator;
        private readonly RollingWindow<double> Prices = new RollingWindow<double>(9);
        private readonly RollingWindow<double> Smooths = new RollingWindow<double>(9);
        private readonly RollingWindow<double> Periods = new RollingWindow<double>(9);
        private readonly RollingWindow<double> Detrenders = new RollingWindow<double>(9);
        private readonly RollingWindow<double> Q1s = new RollingWindow<double>(9);
        private readonly RollingWindow<double> I1s = new RollingWindow<double>(9);
        private readonly RollingWindow<double> Q2s = new RollingWindow<double>(9);
        private readonly RollingWindow<double> I2s = new RollingWindow<double>(9);
        private readonly RollingWindow<double> Res = new RollingWindow<double>(9);
        private readonly RollingWindow<double> Ims = new RollingWindow<double>(9);
        private readonly RollingWindow<double> SmoothPeriods = new RollingWindow<double>(9);
        private readonly RollingWindow<double> Phases = new RollingWindow<double>(9);
        private readonly RollingWindow<double> MAMAs = new RollingWindow<double>(9);
        private readonly RollingWindow<double> FAMAs = new RollingWindow<double>(9);
        private Chart plotter;
        decimal _oldprice = 100000;
        decimal _price;
        int _old_dir = 0;
        int _mama_dir = 0;
        int _trend_dir = 0;
        private DateTime startTime;

        public override void Initialize()
        {
            //Start and End Date range for the backtest:
            startTime = DateTime.Now;
            SetStartDate(2016, 1, 02);
            SetEndDate(2016, 1, 07);
            SetCash(26000);
            AddSecurity(SecurityType.Equity, _ticker, Resolution.Minute);
            consolidator = new TradeBarConsolidator(TimeSpan.FromMinutes(_consolidated_minutes));
            consolidator.DataConsolidated += ConsolidatedHandler;
            SubscriptionManager.AddConsolidator(_ticker, consolidator);

            //plotter = new Chart("MAMA", ChartType.Overlay);
            //plotter.AddSeries(new Series("Price", SeriesType.Line));
            //plotter.AddSeries(new Series("MAMA", SeriesType.Line));
            //plotter.AddSeries(new Series("FAMA", SeriesType.Line));
            //AddChart(plotter);

            //Warm up the variables
            for (int i = 0; i < 7; i++)
            {
                Periods.Add(0.0);
                Smooths.Add(0.0);
                Detrenders.Add(0.0);
                Q1s.Add(0.0);
                I1s.Add(0.0);
                Q2s.Add(0.0);
                I2s.Add(0.0);
                Res.Add(0.0);
                Ims.Add(0.0);
                SmoothPeriods.Add(0.0);
                Phases.Add(0.0);
                MAMAs.Add(0.0);
                FAMAs.Add(0.0);
            }
        }

        public void OnData(TradeBars data)
        {
            // ignore this for now
        }

        public void ConsolidatedHandler(object sender, TradeBar data)
        {
            Prices.Add((double)(data.High + data.Low) / 2);
            _price = data.Close;
            if (!Prices.IsReady) return;

            // MAMA and FAMA
            // *********************************************************************************************************
            double Smooth = (double)((4 * Prices[0] + 3 * Prices[1] + 2 * Prices[2] + Prices[3]) / 10);
            Smooths.Add(Smooth);
            double Detrender = (.0962 * Smooths[0] + .5769 * Smooths[2] - .5769 * Smooths[4] - .0962 * Smooths[6]) * (.075 * Periods[1] + .54);
            Detrenders.Add(Detrender);

            // Compute InPhase and Quadrature components
            Q1s.Add((.0962 * Detrenders[0] + .5769 * Detrenders[2] - .5769 * Detrenders[4] - .0962 * Detrenders[6]) * (.075 * Periods[1] + .54));
            I1s.Add(Detrenders[3]);

            // Advance the phase of I1 and Q1 by 90 degrees
            double jI = (.0962 * I1s[0] + .5769 * I1s[2] - .5769 * I1s[4] - .0962 * I1s[6]) * (.075 * Periods[1] + .54);
            double jQ = (.0962 * Q1s[0] + .5769 * Q1s[2] - .5769 * Q1s[4] - .0962 * Q1s[6]) * (.075 * Periods[1] + .54);

            // Phasor addition for 3 bar averaging
            double I2 = I1s[0] - jQ;
            double Q2 = Q1s[0] + jI;

            // Smooth the I and Q components before applying the discriminator
            I2s.Add(.2 * I2 + .8 * I2s[0]);
            Q2s.Add(.2 * Q2 + .8 * Q2s[0]);

            // Homodyne Discriminator
            double Re = I2s[0] * I2s[1] + Q2s[0] * Q2s[1];
            double Im = I2s[0] * Q2s[1] - Q2s[0] * I2s[1];
            Res.Add(.2 * Re + .8 * Res[0]);
            Ims.Add(.2 * Im + .8 * Ims[0]);
            double Period = 0;
            if (Im != 0 && Re != 0)
                Period = (2 * Math.PI) / Math.Atan(Im / Re);
            if (Period > 1.5 * Periods[0])
                Period = 1.5 * Periods[0];
            if (Period < .67 * Periods[0])
                Period = .67 * Periods[0];
            if (Period < 6)
                Period = 6;
            if (Period > 50)
                Period = 50;
            Periods.Add(.2 * Period + .8 * Periods[0]);
            SmoothPeriods.Add(33 * Periods[0] + .67 * SmoothPeriods[0]);

            if (I1s[0] != 0)
                Phases.Add(Math.Atan(Q1s[0] / I1s[0]));
            double DeltaPhase = Phases[1] - Phases[0];
            if (DeltaPhase < 1)
                DeltaPhase = 1;
            double alpha = MAMA_FastLimit / DeltaPhase;
            if (alpha < MAMA_SlowLimit)
                alpha = MAMA_SlowLimit;
            MAMAs.Add(alpha * Prices[0] + (1 - alpha) * MAMAs[0]);
            FAMAs.Add(.5 * alpha * MAMAs[0] + (1 - .5 * alpha) * FAMAs[0]);

            if (MAMAs[0] > FAMAs[0])
            {
                _trend_dir = 1;
            }
            else if (MAMAs[0] < FAMAs[0])
            {
                _trend_dir = -1;
            }

            if (MAMAs[0] > MAMAs[1])
            {
                _mama_dir = 1;
            }
            else if (MAMAs[0] < MAMAs[1])
            {
                _mama_dir = -1;
            }
            // *********************************************************************************************************

            // Update chart
            if (Math.Abs(FAMAs[0] - Prices[0]) < 5)
            {
                Plot("MAMA", "price", Prices[0]);
                Plot("MAMA", "MAMA", MAMAs[0]);
                Plot("MAMA", "FAMA", FAMAs[0]);
            }

            // Order logic / (simple) risk management
            decimal pps = ((_price - _oldprice) / _oldprice) * 100;
            if (pps <= -2.5M || _trend_dir != _old_dir)
            { 	// if direction is wrong
                // End position
                Liquidate(_ticker);
            }

            if (!Portfolio.HoldStock)
            {
                int quantity = (int)Math.Floor(Portfolio.Cash / data.Close);
                if (_trend_dir != _old_dir)
                {
                    if (quantity > 0)
                        Order(_ticker, _trend_dir * quantity);
                    _oldprice = _price;
                    _old_dir = _trend_dir;
                }
            }
        }

        /// <summary>
        /// Handles the On end of algorithm 
        /// </summary>
        public override void OnEndOfAlgorithm()
        {
            StringBuilder sb = new StringBuilder();
            //sb.Append(" Symbols: ");
            //foreach (var s in Symbols)
            //{

            //    sb.Append(s.ToString());
            //    sb.Append(",");
            //}
            string symbolsstring = _ticker;
            //symbolsstring = symbolsstring.Substring(0, symbolsstring.LastIndexOf(",", System.StringComparison.Ordinal));
            string debugstring =
                string.Format(
                    "\nAlgorithm Name: {0}\n Symbol: {1}\n Ending Portfolio Value: {2} \n lossThreshhold = {3}\n Start Time: {4}\n End Time: {5}",
                    this.GetType().Name, symbolsstring, Portfolio.TotalPortfolioValue, 0, startTime,
                    DateTime.Now);
            Logging.Log.Trace(debugstring);

            #region logging

            

            //            string filepath = @"I:\MyQuantConnect\Logs\" + symbol + "dailyreturns" + sd + ".csv";
            

            #endregion
        }    
    }
}