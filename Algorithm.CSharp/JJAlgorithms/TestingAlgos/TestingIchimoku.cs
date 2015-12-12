using QuantConnect.Data;
using QuantConnect.Data.Consolidators;
using QuantConnect.Indicators;
using System;
using System.Collections.Generic;
using System.Text;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Basic template algorithm simply initializes the date range and cash
    /// </summary>
    public class TestingIchimoku : QCAlgorithm
    {
        private string[] ticketsArray = { "AAPL", "BABA" };

        IchimokuKinkoHyo ichi = new IchimokuKinkoHyo("Ichi1");
        IchimokuKinkoHyo ichi5 = new IchimokuKinkoHyo("Ichi5");
        IchimokuKinkoHyo ichi10 = new IchimokuKinkoHyo("Ichi10");
        IchimokuKinkoHyo ichi30 = new IchimokuKinkoHyo("Ichi30");
        IchimokuKinkoHyo ichi60 = new IchimokuKinkoHyo("Ichi60");
        int counter = 0;

        public override void Initialize()
        {
            SetStartDate(2015, 6, 01);  //Set Start Date
            SetEndDate(2015, 6, 5);    //Set End Date
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
            if (IsWarmingUp)
            {
                if (ichi.IsReady && counter == 0)
                {
                    Log("Warming up");
                    Log("Ichimoku1 ready!");
                    counter++;
                }
                if (ichi5.IsReady && counter == 1)
                {
                    Log("Ichimoku5 ready!");
                    counter++;
                }
                if (ichi10.IsReady && counter == 2)
                {
                    Log("Ichimoku10 ready!");
                    counter++;
                }
                if (ichi30.IsReady && counter == 3)
                {
                    Log("Ichimoku30 ready!");
                    counter++;
                }
                if (ichi60.IsReady && counter == 4)
                {
                    Log("Ichimoku60 ready!");
                    counter++;
                }
            }
            else
            {
                if (counter == 5)
                {
                    Log("Warm up ended");
                    counter++;
                }
            }


        }

        public override void OnEndOfDay(string symbol)
        {
        }
    }
}