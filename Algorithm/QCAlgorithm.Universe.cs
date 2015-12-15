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
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Securities;
using SecurityTypeMarket = System.Tuple<QuantConnect.SecurityType, string>;

namespace QuantConnect.Algorithm
{
    public partial class QCAlgorithm
    {
        // user defined universes by security type
        private readonly Dictionary<SecurityTypeMarket, UserDefinedUniverse> _userDefinedUniverses;

        /// <summary>
        /// Gets the current universe selector, or null if no selection is to be performed
        /// </summary>
        public UniverseManager Universes
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the subscription settings to be used when adding securities via universe selection
        /// </summary>
        public SubscriptionSettings UniverseSettings
        {
            get;
            private set;
        }

        /// <summary>
        /// Adds the universe to the algorithm
        /// </summary>
        /// <param name="universe">The universe to be added</param>
        public void AddUniverse(Universe universe)
        {
            Universes.Add(universe.Configuration.Symbol, universe);
        }

        /// <summary>
        /// Creates a new universe and adds it to the algorithm. This will use the default universe settings
        /// specified via the <see cref="UniverseSettings"/> property. This universe will use the defaults
        /// of SecurityType.Equity, Resolution.Daily, Market.USA, and UniverseSettings
        /// </summary>
        /// <typeparam name="T">The data type</typeparam>
        /// <param name="name">A unique name for this universe</param>
        /// <param name="selector">Function delegate that performs selection on the universe data</param>
        public void AddUniverse<T>(string name, Func<IEnumerable<T>, IEnumerable<Symbol>> selector)
        {
            AddUniverse(SecurityType.Equity, name, Resolution.Daily, Market.USA, UniverseSettings, selector);
        }

        /// <summary>
        /// Creates a new universe and adds it to the algorithm. This will use the default universe settings
        /// specified via the <see cref="UniverseSettings"/> property. This universe will use the defaults
        /// of SecurityType.Equity, Resolution.Daily, Market.USA, and UniverseSettings
        /// </summary>
        /// <typeparam name="T">The data type</typeparam>
        /// <param name="name">A unique name for this universe</param>
        /// <param name="selector">Function delegate that performs selection on the universe data</param>
        public void AddUniverse<T>(string name, Func<IEnumerable<T>, IEnumerable<string>> selector)
        {
            AddUniverse(SecurityType.Equity, name, Resolution.Daily, Market.USA, UniverseSettings, selector);
        }

        /// <summary>
        /// Creates a new universe and adds it to the algorithm. This will use the default universe settings
        /// specified via the <see cref="UniverseSettings"/> property. This universe will use the defaults
        /// of SecurityType.Equity, Resolution.Daily, and Market.USA
        /// </summary>
        /// <typeparam name="T">The data type</typeparam>
        /// <param name="name">A unique name for this universe</param>
        /// <param name="subscriptionSettings">The settings used for securities added by this universe</param>
        /// <param name="selector">Function delegate that performs selection on the universe data</param>
        public void AddUniverse<T>(string name, SubscriptionSettings subscriptionSettings, Func<IEnumerable<T>, IEnumerable<Symbol>> selector)
        {
            AddUniverse(SecurityType.Equity, name, Resolution.Daily, Market.USA, subscriptionSettings, selector);
        }

        /// <summary>
        /// Creates a new universe and adds it to the algorithm. This will use the default universe settings
        /// specified via the <see cref="UniverseSettings"/> property. This universe will use the defaults
        /// of SecurityType.Equity, Resolution.Daily, and Market.USA
        /// </summary>
        /// <typeparam name="T">The data type</typeparam>
        /// <param name="name">A unique name for this universe</param>
        /// <param name="subscriptionSettings">The settings used for securities added by this universe</param>
        /// <param name="selector">Function delegate that performs selection on the universe data</param>
        public void AddUniverse<T>(string name, SubscriptionSettings subscriptionSettings, Func<IEnumerable<T>, IEnumerable<string>> selector)
        {
            AddUniverse(SecurityType.Equity, name, Resolution.Daily, Market.USA, subscriptionSettings, selector);
        }

        /// <summary>
        /// Creates a new universe and adds it to the algorithm. This will use the default universe settings
        /// specified via the <see cref="UniverseSettings"/> property. This universe will use the defaults
        /// of SecurityType.Equity, Market.USA and UniverseSettings
        /// </summary>
        /// <typeparam name="T">The data type</typeparam>
        /// <param name="name">A unique name for this universe</param>
        /// <param name="resolution">The epected resolution of the universe data</param>
        /// <param name="selector">Function delegate that performs selection on the universe data</param>
        public void AddUniverse<T>(string name, Resolution resolution, Func<IEnumerable<T>, IEnumerable<Symbol>> selector)
        {
            AddUniverse(SecurityType.Equity, name, resolution, Market.USA, UniverseSettings, selector);
        }

        /// <summary>
        /// Creates a new universe and adds it to the algorithm. This will use the default universe settings
        /// specified via the <see cref="UniverseSettings"/> property. This universe will use the defaults
        /// of SecurityType.Equity, Market.USA and UniverseSettings
        /// </summary>
        /// <typeparam name="T">The data type</typeparam>
        /// <param name="name">A unique name for this universe</param>
        /// <param name="resolution">The epected resolution of the universe data</param>
        /// <param name="selector">Function delegate that performs selection on the universe data</param>
        public void AddUniverse<T>(string name, Resolution resolution, Func<IEnumerable<T>, IEnumerable<string>> selector)
        {
            AddUniverse(SecurityType.Equity, name, resolution, Market.USA, UniverseSettings, selector);
        }

        /// <summary>
        /// Creates a new universe and adds it to the algorithm. This will use the default universe settings
        /// specified via the <see cref="UniverseSettings"/> property. This universe will use the defaults
        /// of SecurityType.Equity, and Market.USA
        /// </summary>
        /// <typeparam name="T">The data type</typeparam>
        /// <param name="name">A unique name for this universe</param>
        /// <param name="resolution">The epected resolution of the universe data</param>
        /// <param name="subscriptionSettings">The settings used for securities added by this universe</param>
        /// <param name="selector">Function delegate that performs selection on the universe data</param>
        public void AddUniverse<T>(string name, Resolution resolution, SubscriptionSettings subscriptionSettings, Func<IEnumerable<T>, IEnumerable<Symbol>> selector)
        {
            AddUniverse(SecurityType.Equity, name, resolution, Market.USA, subscriptionSettings, selector);
        }

        /// <summary>
        /// Creates a new universe and adds it to the algorithm. This will use the default universe settings
        /// specified via the <see cref="UniverseSettings"/> property. This universe will use the defaults
        /// of SecurityType.Equity, and Market.USA
        /// </summary>
        /// <typeparam name="T">The data type</typeparam>
        /// <param name="name">A unique name for this universe</param>
        /// <param name="resolution">The epected resolution of the universe data</param>
        /// <param name="subscriptionSettings">The settings used for securities added by this universe</param>
        /// <param name="selector">Function delegate that performs selection on the universe data</param>
        public void AddUniverse<T>(string name, Resolution resolution, SubscriptionSettings subscriptionSettings, Func<IEnumerable<T>, IEnumerable<string>> selector)
        {
            AddUniverse(SecurityType.Equity, name, resolution, Market.USA, subscriptionSettings, selector);
        }

        /// <summary>
        /// Creates a new universe and adds it to the algorithm. This will use the default universe settings
        /// specified via the <see cref="UniverseSettings"/> property.
        /// </summary>
        /// <typeparam name="T">The data type</typeparam>
        /// <param name="securityType">The security type the universe produces</param>
        /// <param name="name">A unique name for this universe</param>
        /// <param name="resolution">The epected resolution of the universe data</param>
        /// <param name="market">The market for selected symbols</param>
        /// <param name="selector">Function delegate that performs selection on the universe data</param>
        public void AddUniverse<T>(SecurityType securityType, string name, Resolution resolution, string market, Func<IEnumerable<T>, IEnumerable<Symbol>> selector)
        {
            AddUniverse(securityType, name, resolution, market, UniverseSettings, selector);
        }

        /// <summary>
        /// Creates a new universe and adds it to the algorithm. This will use the default universe settings
        /// specified via the <see cref="UniverseSettings"/> property.
        /// </summary>
        /// <typeparam name="T">The data type</typeparam>
        /// <param name="securityType">The security type the universe produces</param>
        /// <param name="name">A unique name for this universe</param>
        /// <param name="resolution">The epected resolution of the universe data</param>
        /// <param name="market">The market for selected symbols</param>
        /// <param name="selector">Function delegate that performs selection on the universe data</param>
        public void AddUniverse<T>(SecurityType securityType, string name, Resolution resolution, string market, Func<IEnumerable<T>, IEnumerable<string>> selector)
        {
            AddUniverse(securityType, name, resolution, market, UniverseSettings, selector);
        }

        /// <summary>
        /// Creates a new universe and adds it to the algorithm
        /// </summary>
        /// <typeparam name="T">The data type</typeparam>
        /// <param name="securityType">The security type the universe produces</param>
        /// <param name="name">A unique name for this universe</param>
        /// <param name="resolution">The epected resolution of the universe data</param>
        /// <param name="market">The market for selected symbols</param>
        /// <param name="subscriptionSettings">The subscription settings to use for newly created subscriptions</param>
        /// <param name="selector">Function delegate that performs selection on the universe data</param>
        public void AddUniverse<T>(SecurityType securityType, string name, Resolution resolution, string market, SubscriptionSettings subscriptionSettings, Func<IEnumerable<T>, IEnumerable<Symbol>> selector)
        {
            var marketHoursDbEntry = _marketHoursDatabase.GetEntry(market, name, securityType);
            var dataTimeZone = marketHoursDbEntry.DataTimeZone;
            var exchangeTimeZone = marketHoursDbEntry.ExchangeHours.TimeZone;
            var symbol = QuantConnect.Symbol.Create(name, securityType, market);
            var config = new SubscriptionDataConfig(typeof(T), symbol, resolution, dataTimeZone, exchangeTimeZone, false, false, true, true);
            AddUniverse(new FuncUniverse(config, subscriptionSettings, d => selector(d.OfType<T>())));
        }

        /// <summary>
        /// Creates a new universe and adds it to the algorithm
        /// </summary>
        /// <typeparam name="T">The data type</typeparam>
        /// <param name="securityType">The security type the universe produces</param>
        /// <param name="name">A unique name for this universe</param>
        /// <param name="resolution">The epected resolution of the universe data</param>
        /// <param name="market">The market for selected symbols</param>
        /// <param name="subscriptionSettings">The subscription settings to use for newly created subscriptions</param>
        /// <param name="selector">Function delegate that performs selection on the universe data</param>
        public void AddUniverse<T>(SecurityType securityType, string name, Resolution resolution, string market, SubscriptionSettings subscriptionSettings, Func<IEnumerable<T>, IEnumerable<string>> selector)
        {
            var marketHoursDbEntry = _marketHoursDatabase.GetEntry(market, name, securityType);
            var dataTimeZone = marketHoursDbEntry.DataTimeZone;
            var exchangeTimeZone = marketHoursDbEntry.ExchangeHours.TimeZone;
            var symbol = QuantConnect.Symbol.Create(name, securityType, market);
            var config = new SubscriptionDataConfig(typeof(T), symbol, resolution, dataTimeZone, exchangeTimeZone, false, false, true, true);
            AddUniverse(new FuncUniverse(config, subscriptionSettings, d => selector(d.OfType<T>()).Select(x => QuantConnect.Symbol.Create(x, securityType, market))));
        }

        /// <summary>
        /// Creates a new univese and adds it to the algorithm. This is for coarse fundamntal US Equity data and
        /// will be executed on day changes in the NewYork time zone (<see cref="TimeZones.NewYork"/>
        /// </summary>
        /// <param name="selector">Defines an initial coarse selection</param>
        public void AddUniverse(Func<IEnumerable<CoarseFundamental>, IEnumerable<Symbol>> selector)
        {
            var symbol = CoarseFundamental.CreateUniverseSymbol(Market.USA);
            var config = new SubscriptionDataConfig(typeof(CoarseFundamental), symbol, Resolution.Daily, TimeZones.NewYork, TimeZones.NewYork, false, false, true);
            AddUniverse(new FuncUniverse(config, UniverseSettings, selectionData => selector(selectionData.OfType<CoarseFundamental>())));
        }

        /// <summary>
        /// Creates a new universe and adds it to the algorithm. This can be used to return a list of string
        /// symbols retrieved from anywhere and will loads those symbols under the US Equity market.
        /// </summary>
        /// <param name="name">A unique name for this universe</param>
        /// <param name="selector">Function delegate that accepts a DateTime and returns a collection of string symbols</param>
        public void AddUniverse(string name, Func<DateTime, IEnumerable<string>> selector)
        {
            AddUniverse(SecurityType.Equity, name, Resolution.Daily, Market.USA, UniverseSettings, selector);
        }

        /// <summary>
        /// Creates a new universe and adds it to the algorithm. This can be used to return a list of string
        /// symbols retrieved from anywhere and will loads those symbols under the US Equity market.
        /// </summary>
        /// <param name="name">A unique name for this universe</param>
        /// <param name="resolution">The resolution this universe should be triggered on</param>
        /// <param name="selector">Function delegate that accepts a DateTime and returns a collection of string symbols</param>
        public void AddUniverse(string name, Resolution resolution, Func<DateTime, IEnumerable<string>> selector)
        {
            AddUniverse(SecurityType.Equity, name, resolution, Market.USA, UniverseSettings, selector);
        }

        /// <summary>
        /// Creates a new user defined universe that will fire on the requested resolution during market hours.
        /// </summary>
        /// <param name="securityType">The security type of the universe</param>
        /// <param name="name">A unique name for this universe</param>
        /// <param name="resolution">The resolution this universe should be triggered on</param>
        /// <param name="market">The market of the universe</param>
        /// <param name="subscriptionSettings">The subscription settings used for securities added from this universe</param>
        /// <param name="selector">Function delegate that accepts a DateTime and returns a collection of string symbols</param>
        public void AddUniverse(SecurityType securityType, string name, Resolution resolution, string market, SubscriptionSettings subscriptionSettings, Func<DateTime, IEnumerable<string>> selector)
        {
            var marketHoursDbEntry = _marketHoursDatabase.GetEntry(market, name, securityType);
            var dataTimeZone = marketHoursDbEntry.DataTimeZone;
            var exchangeTimeZone = marketHoursDbEntry.ExchangeHours.TimeZone;
            var symbol = QuantConnect.Symbol.Create(name, securityType, market);
            var config = new SubscriptionDataConfig(typeof(CoarseFundamental), symbol, resolution, dataTimeZone, exchangeTimeZone, false, false, true);
            AddUniverse(new UserDefinedUniverse(config, subscriptionSettings, resolution.ToTimeSpan(), selector));
        }

        /// <summary>
        /// Adds the security to the user defined universe for the specified 
        /// </summary>
        private void AddToUserDefinedUniverse(Security security)
        {
            // add this security to the user defined universe
            UserDefinedUniverse universe;
            var key = new SecurityTypeMarket(security.Type, security.SubscriptionDataConfig.Market);
            if (_userDefinedUniverses.TryGetValue(key, out universe))
            {
                universe.Add(security.Symbol);
            }
            else
            {
                // create a new universe, these subscription settings don't currently get used
                // since universe selection proper is never invoked on this type of universe
                var securityConfig = security.SubscriptionDataConfig;
                var universeSymbol = UserDefinedUniverse.CreateSymbol(securityConfig.SecurityType, securityConfig.Market);
                var uconfig = new SubscriptionDataConfig(securityConfig, symbol: universeSymbol, isInternalFeed: true, fillForward: false);
                universe = new UserDefinedUniverse(uconfig,
                    new SubscriptionSettings(security.Resolution, security.Leverage, security.IsFillDataForward, security.IsExtendedMarketHours),
                    QuantConnect.Time.OneDay,
                    new List<Symbol> { security.Symbol }
                    );
                _userDefinedUniverses[key] = universe;

                Universes.Add(universe.Configuration.Symbol, universe);
            }
        }
    }
}
