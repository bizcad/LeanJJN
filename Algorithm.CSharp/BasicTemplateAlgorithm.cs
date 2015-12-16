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

using System;
using System.Text;
using Newtonsoft.Json;
using QuantConnect.Data;
using QuantConnect.Orders;
using QuantConnect.Packets;
using System.Collections.Generic;
using System.IO;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Basic template algorithm simply initializes the date range and cash
    /// </summary>
    public class BasicTemplateAlgorithm : QCAlgorithm
    {
        private decimal lossThreshhold = -55m;
        private DateTime startTime;
        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        public override void Initialize()
        {
            SetStartDate(2015, 10, 07);  //Set Start Date
            SetEndDate(2015, 10, 11);    //Set End Date
            SetCash(100000);             //Set Strategy Cash
            // Find more symbols here: http://quantconnect.com/data
            AddSecurity(SecurityType.Equity, "SPY", Resolution.Minute);

            startTime = DateTime.Now;
        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">Slice object keyed by symbol containing the stock data</param>
        public override void OnData(Slice data)
        {
            if (!Portfolio.Invested)
            {
                SetHoldings("SPY", 1);
                Debug("Purchased Stock");
            }
        }

        public override void OnEndOfAlgorithm()
        {
            //var resultJsonString = File.ReadAllText(@"C:\Users\JJ\Desktop\Algorithmic Trading\JSONTest.json");
            //dynamic deserializedResults = JsonConvert.DeserializeObject(resultJsonString);
            //var charts = JsonConvert.DeserializeObject<Dictionary<string, Chart>>(deserializedResults["oResultData"]["results"]["Charts"].ToString());

            //var orders = JsonConvert.DeserializeObject<Dictionary<int, Order>>(deserializedResults["oResultData"]["results"]["Orders"].ToString());
            StringBuilder sb = new StringBuilder();
            //sb.Append(" Symbols: ");
            foreach (var s in Portfolio.Keys)
            {
                sb.Append(s.Value);
                sb.Append(",");
            }
            string symbolsstring = sb.ToString();
            symbolsstring = symbolsstring.Substring(0, symbolsstring.LastIndexOf(",", System.StringComparison.Ordinal));
            string debugstring =
                string.Format(
                    "\nAlgorithm Name: {0}\n Symbol: {1}\n Ending Portfolio Value: {2} \n lossThreshhold = {3}\n Start Time: {4}\n End Time: {5}",
                    this.GetType().Name, symbolsstring, Portfolio.TotalPortfolioValue, lossThreshhold, startTime,DateTime.Now);
            Logging.Log.Trace(debugstring);

        }
    }
}