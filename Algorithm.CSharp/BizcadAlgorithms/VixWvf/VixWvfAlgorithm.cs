﻿using System;
using System.Collections.Generic;
using System.Text;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Logging;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.CSharp.BizcadAlgorithm.VixWvf
{
    public class VixWvfAlgorithm : QCAlgorithm
    {

        #region "Variables"

        private DateTime startTime = DateTime.Now;
        private DateTime _startDate = new DateTime(2015, 10, 27);
        private DateTime _endDate = new DateTime(2015, 11, 27);
        private decimal _portfolioAmount = 25000;

        /* +-------------------------------------------------+
         * |Algorithm Control Panel                          |
         * +-------------------------------------------------+*/
        private static int Period = 22; // Instantaneous Trend period.
        private static decimal Tolerance = 0.0001m; // Trigger - Trend crossing tolerance.
        private static decimal RevertPCT = 1.0015m; // Percentage tolerance before revert position.
        private static decimal maxLeverage = 1m; // Maximum Leverage.
        private decimal leverageBuffer = 0.25m; // Percentage of Leverage left unused.
        private int maxOperationQuantity = 500; // Maximum shares per operation.
        private decimal RngFac = 0.35m; // Percentage of the bar range used to estimate limit prices.
        private bool noOvernight = true; // Close all positions before market close.
        /* +-------------------------------------------------+*/

        private string[] symbolarray = { "SPY", "VIX"};
        private List<string> Symbols = new List<string>();
        WilliamsVixFixIndicator wvfh = new WilliamsVixFixIndicator(Period);
        WilliamsVixFixIndicatorReverse wvfl = new WilliamsVixFixIndicatorReverse(Period);
        InverseFisherTransform ifwvfh = new InverseFisherTransform(22);
        InverseFisherTransform ifwvfl = new InverseFisherTransform(22);
        IchimokuKinkoHyo ichi = new IchimokuKinkoHyo("Ichi1");
        IchimokuKinkoHyo ichi5 = new IchimokuKinkoHyo("Ichi5");
        IchimokuKinkoHyo ichi10 = new IchimokuKinkoHyo("Ichi10");
        IchimokuKinkoHyo ichi30 = new IchimokuKinkoHyo("Ichi30");
        IchimokuKinkoHyo ichi60 = new IchimokuKinkoHyo("Ichi60");
        InstantaneousTrend iTrend = new InstantaneousTrend(22);
        private TradeBarConsolidator Consolidator5Minute = new TradeBarConsolidator(TimeSpan.FromMinutes(5));
        private TradeBarConsolidator Consolidator10Minute = new TradeBarConsolidator(TimeSpan.FromMinutes(10));
        private TradeBarConsolidator Consolidator30Minute = new TradeBarConsolidator(TimeSpan.FromMinutes(30));
        private TradeBarConsolidator Consolidator60Minute = new TradeBarConsolidator(TimeSpan.FromMinutes(60));

        private List<string> IchimokuLog = new List<string>(); 

        private int barcount = 0;
        private ILogHandler mylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("CustomFileLogHandler");
        private TradeBar vix = new TradeBar();

        private bool headingwritten = false;

        #endregion

        public override void Initialize()
        {
            SetStartDate(_startDate); //Set Start Date
            SetEndDate(_endDate); //Set End Date
            SetCash(_portfolioAmount); //Set Strategy Cash

            foreach (string t in symbolarray)
            {
                Symbols.Add(t);
            }

            foreach (string symbol in Symbols)
            {
                AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);
            }
            //foreach (Symbol s in Portfolio.Keys)
            //{
            //    var priceIdentity = Identity(s, selector: Field.Close);
            //}

            // Add the event handler for OnDataConsolidated so this algorithm gets an event
            Consolidator5Minute.DataConsolidated += On5Minute;
            Consolidator10Minute.DataConsolidated += On10Minute;
            Consolidator30Minute.DataConsolidated += On30Minute;
            Consolidator60Minute.DataConsolidated += On60Minute;

            // Add the Consolidator to the SubscriptionManager so OnDataConsolidated fires the above
            //    events each time the consolidator consolidates.  This event is our hook into the 
            //    Consolidator and we get a consolidated TradeBar as the TradeBar e.
            SubscriptionManager.AddConsolidator(Portfolio.Securities[symbolarray[0]].Symbol, Consolidator5Minute);
            SubscriptionManager.AddConsolidator(Portfolio.Securities[symbolarray[0]].Symbol, Consolidator10Minute);
            SubscriptionManager.AddConsolidator(Portfolio.Securities[symbolarray[0]].Symbol, Consolidator30Minute);
            SubscriptionManager.AddConsolidator(Portfolio.Securities[symbolarray[0]].Symbol, Consolidator60Minute);

            // Register the Ichimoku indicators, which also adds the Consolidator base class OnDataConsolidated
            RegisterIndicator(Portfolio.Securities[symbolarray[0]].Symbol, ichi, new TimeSpan(0, 1, 0));
            RegisterIndicator(Portfolio.Securities[symbolarray[0]].Symbol, ichi5, new TimeSpan(0, 5, 0));
            RegisterIndicator(Portfolio.Securities[symbolarray[0]].Symbol, ichi10, new TimeSpan(0, 10, 0));
            RegisterIndicator(Portfolio.Securities[symbolarray[0]].Symbol, ichi30, new TimeSpan(0, 30, 0));
            RegisterIndicator(Portfolio.Securities[symbolarray[0]].Symbol, ichi60, new TimeSpan(0, 60, 0));

            // consolidate the history?
            var history = History(Portfolio.Securities[symbolarray[0]].Symbol, 3601);
            foreach (TradeBar bar in history)
            {
                ichi.Update(bar);
                Consolidator5Minute.Update(bar);
                Consolidator10Minute.Update(bar);
                Consolidator30Minute.Update(bar);
                Consolidator60Minute.Update(bar);

                //ichi5.Update(bar);
                //ichi10.Update(bar);
                //ichi30.Update(bar);
                //ichi60.Update(bar);
            }

        }

        private void On5Minute(object sender, TradeBar e)
        {
        }
 
        private void On10Minute(object sender, TradeBar e)
        {
        }

        private void On30Minute(object sender, TradeBar e)
        {
        }
        private void On60Minute(object sender, TradeBar e)
        {
        }
        #region "one minute events"
        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">TradeBars IDictionary object with your stock data</param>
        public void OnData(TradeBars data)
        {
            barcount++;
            string msg = "here";
            if (barcount == 100)
               System.Diagnostics.Debug.WriteLine(msg);
            foreach (KeyValuePair<Symbol, TradeBar> kvp in data)
            {
                OnDataForSymbol(kvp);
            }
        }

        private void OnDataForSymbol(KeyValuePair<Symbol, TradeBar> data)
        {
            if (data.Key == "VIX")
            {
                vix = data.Value;
            }
            if (data.Key.Value == symbolarray[0])
            {
                wvfh.Update(data.Value);
                wvfl.Update(data.Value);
                ifwvfh.Update(wvfh.Current);
                ifwvfl.Update(wvfl.Current);
                ichi.Update(data.Value);
                iTrend.Update(new IndicatorDataPoint(data.Value.EndTime, data.Value.Close));

                #region "biglog"

                if (!headingwritten)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("Barcount, Symbol,EndTime,Volume,Open,High,Low,Close");
                    sb.Append(",EndTime");
                    sb.Append(",vix");
                    sb.Append(",iTrend");
                    sb.Append(",wvfh");
                    sb.Append(",fwvfh");
                    sb.Append(",wvfl");
                    sb.Append(",fwvfl");
                    sb.Append(",t1");
                    sb.Append(",k1");
                    sb.Append(",sa1");
                    sb.Append(",sb1");
                    sb.Append(",t5");
                    sb.Append(",k5");
                    sb.Append(",sa5");
                    sb.Append(",sb5");
                    sb.Append(",t10");
                    sb.Append(",k10");
                    sb.Append(",sa10");
                    sb.Append(",sb10");
                    sb.Append(",t30");
                    sb.Append(",k30");
                    sb.Append(",sa30");
                    sb.Append(",sb30");
                    sb.Append(",t60");
                    sb.Append(",k60");
                    sb.Append(",sa60");
                    sb.Append(",sb60");
                    IchimokuLog.Add(sb.ToString());
                    headingwritten = true;
                }
                string logmsg =
                    string.Format(
                        "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19}" 
                        + ",{20},{21},{22},{23},{24},{25},{26},{27},{28},{29} "
                        + ",{30},{31},{32},{33},{34},{35}"
                        ,
                        barcount,
                        data.Key,
                        data.Value.EndTime,
                        data.Value.Volume,
                        data.Value.Open,
                        data.Value.High,
                        data.Value.Low,
                        data.Value.Close,
                        data.Value.EndTime.ToShortTimeString(),
                        vix.Close,
                        iTrend.Current.Value,
                        wvfh.Current.Value,
                        ifwvfh.Current.Value * -1,
                        wvfl.Current.Value,
                        ifwvfl.Current.Value * -1,
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
                        ichi60.SenkouB.Current.Value,
                        ""
                        );
                IchimokuLog.Add(logmsg);


                #endregion

            }
        }
        #endregion
    }
}
