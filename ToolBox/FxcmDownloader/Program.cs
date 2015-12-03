﻿/*
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
using System.Globalization;
using System.Linq;
using org.apache.log4j;
using QuantConnect.Configuration;
using QuantConnect.Data.Market;
using QuantConnect.Logging;

namespace QuantConnect.ToolBox.FxcmDownloader
{
    class Program
    {
        /// <summary>
        /// Primary entry point to the program
        /// </summary>
        private static void Main(string[] args)
        {
            if (args.Length != 4)
            {
                Console.WriteLine("Usage: FxcmDownloader SYMBOLS RESOLUTION FROMDATE TODATE");
                Console.WriteLine("SYMBOLS = eg EURUSD,USDJPY");
                Console.WriteLine("RESOLUTION = Second/Minute/Hour/Daily/All");
                Console.WriteLine("FROMDATE = yyyymmdd");
                Console.WriteLine("TODATE = yyyymmdd");
                Environment.Exit(1);
            }

            try
            {
                Logger.getRootLogger().setLevel(Level.ERROR);
                BasicConfigurator.configure(new FileAppender(new SimpleLayout(), "FxcmDownloader.log", append: false));

                // Load settings from command line
                var tickers = args[0].Split(',');
                var allResolutions = args[1].ToLower() == "all";
                var resolution = allResolutions ? Resolution.Second : (Resolution)Enum.Parse(typeof(Resolution), args[1]);
                var startDate = DateTime.ParseExact(args[2], "yyyyMMdd", CultureInfo.InvariantCulture);
                var endDate = DateTime.ParseExact(args[3], "yyyyMMdd", CultureInfo.InvariantCulture);

                // Load settings from config.json
                var dataDirectory = Config.Get("data-directory", "../../../Data");
                var server = Config.Get("fxcm-server", "http://www.fxcorporate.com/Hosts.jsp");
                var terminal = Config.Get("fxcm-terminal", "Demo");
                var userName = Config.Get("fxcm-user-name", "username");
                var password = Config.Get("fxcm-password", "password");

                // Download the data
                const string market = "fxcm";
                var downloader = new FxcmDataDownloader(server, terminal, userName, password);

                foreach (var ticker in tickers)
                {
                    var securityType = downloader.GetSecurityType(ticker);
                    var symbol = Symbol.Create(ticker, securityType, Market.FXCM);

                    if (!downloader.HasSymbol(ticker))
                        throw new ArgumentException("The symbol " + ticker + " is not available.");

                    var data = downloader.Get(symbol, securityType, resolution, startDate, endDate);

                    if (allResolutions)
                    {
                        var bars = data.Cast<TradeBar>().ToList();

                        // Save the data (second resolution)
                        var writer = new LeanDataWriter(securityType, resolution, symbol, dataDirectory, market);
                        writer.Write(bars);

                        // Save the data (other resolutions)
                        foreach (var res in new[] {Resolution.Minute, Resolution.Hour, Resolution.Daily})
                        {
                            var resData = FxcmDataDownloader.AggregateBars(symbol, bars, res.ToTimeSpan());

                            writer = new LeanDataWriter(securityType, res, symbol, dataDirectory, market);
                            writer.Write(resData);
                        }
                    }
                    else
                    {
                        // Save the data (single resolution)
                        var writer = new LeanDataWriter(securityType, resolution, symbol, dataDirectory, market);
                        writer.Write(data);
                    }
                }
            }
            catch (Exception err)
            {
                Log.Error(err);
            }
        }
    }
}
