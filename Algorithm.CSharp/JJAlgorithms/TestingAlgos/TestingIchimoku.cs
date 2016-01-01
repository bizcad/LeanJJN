using Newtonsoft.Json;
using QuantConnect.Data;
using QuantConnect.Indicators;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Basic template algorithm simply initializes the date range and cash
    /// </summary>
    public class TestingIchimoku : QCAlgorithm
    {
        private bool headingwritten = false;
        StringBuilder ichimokuLog = new StringBuilder();

        private string[] ticketsArray = { "WMT" };

        private IchimokuKinkoHyo ichi = new IchimokuKinkoHyo("Ichi1");
        private IchimokuKinkoHyo ichi5 = new IchimokuKinkoHyo("Ichi5");
        private IchimokuKinkoHyo ichi10 = new IchimokuKinkoHyo("Ichi10");
        private IchimokuKinkoHyo ichi30 = new IchimokuKinkoHyo("Ichi30");
        private IchimokuKinkoHyo ichi60 = new IchimokuKinkoHyo("Ichi60");

        public override void Initialize()
        {
            SetStartDate(2015, 10, 10);  //Set Start Date
            SetEndDate(2015, 10, 15);    //Set End Date
            SetCash(100000);             //Set Strategy Cash

            foreach (var security in ticketsArray)
            {
                AddSecurity(SecurityType.Equity, security, Resolution.Minute);

                RegisterIndicator(security, ichi, Resolution.Minute);
                RegisterIndicator(security, ichi5, TimeSpan.FromMinutes(5));
                RegisterIndicator(security, ichi10, TimeSpan.FromMinutes(10));
                RegisterIndicator(security, ichi30, TimeSpan.FromMinutes(30));
                RegisterIndicator(security, ichi60, TimeSpan.FromMinutes(60));

                SetWarmup(390 * 5);
            }
        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">Slice object keyed by symbol containing the stock data</param>
        public override void OnData(Slice data)
        {
            if (!IsWarmingUp)
            {
                #region Logging stuff

                if (!headingwritten)
                {
                    ichimokuLog.Append("Time,Close");
                    ichimokuLog.Append(",t1");
                    ichimokuLog.Append(",k1");
                    ichimokuLog.Append(",sa1");
                    ichimokuLog.Append(",sb1");
                    ichimokuLog.Append(",t5");
                    ichimokuLog.Append(",k5");
                    ichimokuLog.Append(",sa5");
                    ichimokuLog.Append(",sb5");
                    ichimokuLog.Append(",t10");
                    ichimokuLog.Append(",k10");
                    ichimokuLog.Append(",sa10");
                    ichimokuLog.Append(",sb10");
                    ichimokuLog.Append(",t30");
                    ichimokuLog.Append(",k30");
                    ichimokuLog.Append(",sa30");
                    ichimokuLog.Append(",sb30");
                    ichimokuLog.Append(",t60");
                    ichimokuLog.Append(",k60");
                    ichimokuLog.Append(",sa60");
                    ichimokuLog.Append(",sb60");
                    headingwritten = true;
                }
                string logmsg =
                    string.Format(
                        "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11}," +
                        "{12},{13},{14},{15},{16},{17},{18},{19},{20},{21}",
                        Time,
                        Securities["WMT"].Price,
                        ichi.Tenkan.Current.Value,
                        ichi.Kijun.Current.Value,
                        ichi.SenkouA.Current.Value,
                        ichi.SenkouB.Current.Value,
                        ichi5.Tenkan.Current.Value,
                        ichi5.Kijun.Current.Value,
                        ichi5.SenkouA.Current.Value,
                        ichi5.SenkouB.Current.Value,
                        ichi10.Tenkan.Current.Value,
                        ichi10.Kijun.Current.Value,
                        ichi10.SenkouA.Current.Value,
                        ichi10.SenkouB.Current.Value,
                        ichi30.Tenkan.Current.Value,
                        ichi30.Kijun.Current.Value,
                        ichi30.SenkouA.Current.Value,
                        ichi30.SenkouB.Current.Value,
                        ichi60.Tenkan.Current.Value,
                        ichi60.Kijun.Current.Value,
                        ichi60.SenkouA.Current.Value,
                        ichi60.SenkouB.Current.Value
                        );
                ichimokuLog.AppendLine(logmsg);

                #endregion Logging stuff
            }
        }

        public override void OnEndOfAlgorithm()
        {
            File.WriteAllText("IchimokuTesting.csv", ichimokuLog.ToString());
        }
    }
}