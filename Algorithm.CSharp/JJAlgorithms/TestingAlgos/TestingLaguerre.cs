/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); 
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using Newtonsoft.Json;
using QuantConnect.Data;
using QuantConnect.Orders;
using QuantConnect.Packets;
using System.Collections.Generic;
using System.IO;
using System.Text;
using QuantConnect.Indicators;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Basic template algorithm simply initializes the date range and cash
    /// </summary>
    public class TestingLaguerre : QCAlgorithm
    {

        StringBuilder logging = new StringBuilder();
        LaguerreIndicator Laguerre;
        string ticker = "AAPL";

        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        public override void Initialize()
        {
            SetStartDate(2015, 06, 01);  //Set Start Date
            SetEndDate(2015, 06, 10);    //Set End Date
            SetCash(100000);             //Set Strategy Cash
            // Find more symbols here: http://quantconnect.com/data
            AddSecurity(SecurityType.Equity, ticker, Resolution.Minute);
            Laguerre = new LaguerreIndicator(0.8m);
            RegisterIndicator(ticker, Laguerre, Resolution.Minute, Field.Close);
            logging.AppendLine("Time,Close,Laguerre,FIR,LaguerreRSI");
        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">Slice object keyed by symbol containing the stock data</param>
        public override void OnData(Slice data)
        {
            string logMsng = string.Format("{0},{1},{2},{3},{4}",
                                           Time,
                                           Securities[ticker].Price,
                                           Laguerre.Laguerre[0].Value.SmartRounding(),
                                           Laguerre.FIR[0].Value.SmartRounding(),
                                           Laguerre.LaguerreRSI[0].Value.SmartRounding());
            logging.AppendLine(logMsng);
        }

        public override void OnEndOfAlgorithm()
        {
            File.WriteAllText("TestingLaguerre.csv", logging.ToString());
        }
    }
}