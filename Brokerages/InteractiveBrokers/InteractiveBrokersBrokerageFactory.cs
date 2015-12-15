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
using System.Collections.Generic;
using Krs.Ats.IBNet;
using QuantConnect.Configuration;
using QuantConnect.Interfaces;
using QuantConnect.Packets;
using QuantConnect.Securities;
using QuantConnect.Util;

namespace QuantConnect.Brokerages.InteractiveBrokers
{
    /// <summary>
    /// Factory type for the <see cref="InteractiveBrokersBrokerage"/>
    /// </summary>
    public class InteractiveBrokersBrokerageFactory : BrokerageFactory
    {
        /// <summary>
        /// The default markets for the fxcm brokerage
        /// </summary>
        public static readonly IReadOnlyDictionary<SecurityType, string> DefaultMarketMap = new Dictionary<SecurityType, string>
        {
            {SecurityType.Base, Market.USA},
            {SecurityType.Equity, Market.USA},
            {SecurityType.Option, Market.USA},
            {SecurityType.Forex, Market.FXCM},
            {SecurityType.Cfd, Market.FXCM}
        }.ToReadOnlyDictionary();

        /// <summary>
        /// Initializes a new instance of the InteractiveBrokersBrokerageFactory class
        /// </summary>
        public InteractiveBrokersBrokerageFactory()
            : base(typeof(InteractiveBrokersBrokerage))
        {
        }

        /// <summary>
        /// Gets the brokerage data required to run the IB brokerage from configuration
        /// </summary>
        /// <remarks>
        /// The implementation of this property will create the brokerage data dictionary required for
        /// running live jobs. See <see cref="IJobQueueHandler.NextJob"/>
        /// </remarks>
        public override Dictionary<string, string> BrokerageData
        {
            get
            {
                var data = new Dictionary<string, string>();
                data.Add("ib-account", Config.Get("ib-account"));
                data.Add("ib-user-name", Config.Get("ib-user-name"));
                data.Add("ib-password", Config.Get("ib-password"));
                data.Add("ib-agent-description", Config.Get("ib-agent-description"));
                return data;
            }
        }

        /// <summary>
        /// Gets a new instance of the <see cref="InteractiveBrokersBrokerageModel"/>
        /// </summary>
        public override IBrokerageModel BrokerageModel
        {
            get { return new InteractiveBrokersBrokerageModel(); }
        }

        /// <summary>
        /// Gets a map of the default markets to be used for each security type
        /// </summary>
        public override IReadOnlyDictionary<SecurityType, string> DefaultMarkets
        {
            get { return DefaultMarketMap; }
        }

        /// <summary>
        /// Creates a new IBrokerage instance and set ups the environment for the brokerage
        /// </summary>
        /// <param name="job">The job packet to create the brokerage for</param>
        /// <param name="algorithm">The algorithm instance</param>
        /// <returns>A new brokerage instance</returns>
        public override IBrokerage CreateBrokerage(LiveNodePacket job, IAlgorithm algorithm)
        {
            var errors = new List<string>();

            // read values from the brokerage datas
            var useTws = Config.GetBool("ib-use-tws");
            var port = Config.GetInt("ib-port", 4001);
            var host = Config.Get("ib-host", "127.0.0.1");
            var twsDirectory = Config.Get("ib-tws-dir", "C:\\Jts");
            var ibControllerDirectory = Config.Get("ib-controller-dir", "C:\\IBController");

            var account = Read<string>(job.BrokerageData, "ib-account", errors);
            var userID = Read<string>(job.BrokerageData, "ib-user-name", errors);
            var password = Read<string>(job.BrokerageData, "ib-password", errors);
            var agentDescription = Read<AgentDescription>(job.BrokerageData, "ib-agent-description", errors);

            if (errors.Count != 0)
            {
                // if we had errors then we can't create the instance
                throw new Exception(string.Join(Environment.NewLine, errors));
            }
            
            // launch the IB gateway
            InteractiveBrokersGatewayRunner.Start(ibControllerDirectory, twsDirectory, userID, password, useTws);

            var ib = new InteractiveBrokersBrokerage(algorithm.Transactions, account, host, port, agentDescription);
            Composer.Instance.AddPart<IDataQueueHandler>(ib);
            return ib;
        }

        /// <summary>
        /// Creates a new IBrokerage instance and set ups the environment for the brokerage
        /// </summary>
        /// <param name="brokerageData">Brokerage data containing necessary parameters for initialization</param>
        /// <param name="orderProvider">IOrderMapping instance to maintain IB -> QC order id mapping</param>
        /// <returns>A new brokerage instance</returns>
        public IBrokerage CreateBrokerage(Dictionary<string, string> brokerageData, IOrderProvider orderProvider)
        {
            var errors = new List<string>();

            // read values from the brokerage datas
            var useTws = Config.GetBool("ib-use-tws");
            var port = Config.GetInt("ib-port", 4001);
            var host = Config.Get("ib-host", "127.0.0.1");
            var twsDirectory = Config.Get("ib-tws-dir", "C:\\Jts");
            var ibControllerDirectory = Config.Get("ib-controller-dir", "C:\\IBController");

            var account = Read<string>(brokerageData, "ib-account", errors);
            var userID = Read<string>(brokerageData, "ib-user-name", errors);
            var password = Read<string>(brokerageData, "ib-password", errors);
            var agentDescription = Read<AgentDescription>(brokerageData, "ib-agent-description", errors);

            if (errors.Count != 0)
            {
                // if we had errors then we can't create the instance
                throw new Exception(string.Join(Environment.NewLine, errors));
            }

            // launch the IB gateway
            InteractiveBrokersGatewayRunner.Start(ibControllerDirectory, twsDirectory, userID, password, useTws);

            return new InteractiveBrokersBrokerage(orderProvider, account, host, port, agentDescription);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// Stops the InteractiveBrokersGatewayRunner
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public override void Dispose()
        {
            InteractiveBrokersGatewayRunner.Stop();
        }
    }
}
