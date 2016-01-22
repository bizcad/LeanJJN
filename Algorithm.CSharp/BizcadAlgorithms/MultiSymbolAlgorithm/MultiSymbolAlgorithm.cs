using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using QuantConnect.Algorithm;
using QuantConnect.Algorithm.CSharp;
using QuantConnect.Configuration;
using QuantConnect.Data.Market;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Securities.Equity;
using QuantConnect.Util;

namespace QuantConnect
{
    class MultisymbolAlgorithm : QCAlgorithm
    {

        #region "Variables"

        private DateTime startTime = DateTime.Now;
        private DateTime _startDate = new DateTime(2016, 1, 11);
        private DateTime _endDate = new DateTime(2016, 1, 12);
        private decimal _portfolioAmount = 26000;

        /* +-------------------------------------------------+
         * |Algorithm Control Panel                          |
         * +-------------------------------------------------+*/
        private static int SMAPeriod = 4;            // Instantaneous Trend period.
        private static decimal Tolerance = 0.0001m;      // Trigger - Trend crossing tolerance.
        private static decimal RevertPCT = 1.0015m;     // Percentage tolerance before revert position.
        private static decimal maxLeverage = 1m;        // Maximum Leverage.
        private decimal leverageBuffer = 0.25m;         // Percentage of Leverage left unused.
        private int maxOperationQuantity = 500;         // Maximum shares per operation.
        private decimal RngFac = 0.35m;                 // Percentage of the bar range used to estimate limit prices.
        private bool noOvernight = true;                // Close all positions before market close.
        private decimal lossThreshhold = -55m;          // Absolute loss to get out of trade
        /* +-------------------------------------------------+*/

//        readonly string[] symbolarray = new string[] { "AAPL", "NFLX", "AMZN", "SPY" };
        readonly string[] symbolarray = new string[] { "WYNN" };
        readonly List<string> Symbols = new List<string>();

        // Dictionary used to store the RSIStrategy object for each symbol.
        private Dictionary<string, Sig10Strategy> Strategy = new Dictionary<string, Sig10Strategy>();

        // Dictionary used to store the portfolio sharesize for each symbol.
        private Dictionary<string, decimal> ShareSize = new Dictionary<string, decimal>();

        private EquityExchange theMarket = new EquityExchange();

        private int barcount;

        private ILogHandler mylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("CustomFileLogHandler");
        private string ondataheader =
            @"Symbol, Time,BarCount,Volume, Open,High,Low,Close,,Time,Trend, nTrig, Trend Mo, Comment,,EntryPrice, MomentumWindow" +
            ", TradeAttempts,Status, ActualSignal,, IsActive,Unrealized, Owned, LastProfit, Portfolio";

        #endregion

        public override void Initialize()
        {
            //Initialize dates
            var sd = Config.Get("start-date");
            var ed = Config.Get("end-date");

            _startDate = new DateTime(Convert.ToInt32(sd.Substring(0, 4)), Convert.ToInt32(sd.Substring(4, 2)), Convert.ToInt32(sd.Substring(6, 2)));
            _endDate = new DateTime(Convert.ToInt32(ed.Substring(0, 4)), Convert.ToInt32(ed.Substring(4, 2)), Convert.ToInt32(ed.Substring(6, 2)));


            SetStartDate(_startDate);       //Set Start Date
            SetEndDate(_endDate);           //Set End Date
            SetCash(_portfolioAmount);      //Set Strategy Cash

            foreach (string t in symbolarray)
            {
                Symbols.Add(t);
            }

            foreach (string symbol in Symbols)
            {

                AddSecurity(SecurityType.Equity, symbol);


                //Strategy.Add(symbol, new MultiSymbolStrategy(priceIdentity, SMAPeriod, Tolerance, RevertPCT));


            }
            foreach (Symbol s in Portfolio.Keys)
            {
                var priceIdentity = Identity(s, selector: Field.Close);
                Strategy.Add(s.Value, new Sig10Strategy(Portfolio[s], priceIdentity, SMAPeriod, lossThreshhold, Tolerance, RevertPCT));
                // Equally weighted portfolio.
                ShareSize.Add(s, (maxLeverage * (1 - leverageBuffer)) / Symbols.Count());
            }
            #region logging
            var algoname = this.GetType().Name;
            mylog.Debug(algoname);
            StringBuilder sb = new StringBuilder();
            foreach (var s in Symbols)
                sb.Append(s + ",");
            mylog.Debug(ondataheader);
            #endregion

        }
        public void OnData(TradeBars data)
        {

            barcount++;
            foreach (KeyValuePair<Symbol, TradeBar> kvp in data)
            {
                OnDataForSymbol(kvp);
            }
        }
        private void OnDataForSymbol(KeyValuePair<Symbol, TradeBar> data)
        {
            bool isMarketAboutToClose = !theMarket.DateTimeIsOpen(Time.AddMinutes(10));
            if (isMarketAboutToClose)
                System.Diagnostics.Debug.WriteLine("here");
            // Operate only if the market is open
            if (theMarket.DateTimeIsOpen(Time))
            {
                // First check if there are some limit orders not filled yet.
                if (Transactions.LastOrderId > 0)
                {
                    CheckLimitOrderStatus(data);
                }
                // Check if the market is about to close and noOvernight is true.
                OrderSignal actualOrder = OrderSignal.doNothing;
                if (noOvernight && isMarketAboutToClose)
                {
                    actualOrder = ClosePositions(data.Key);
                }
                else
                {
                    // Now check if there is some signal and execute the strategy.
                    actualOrder = Strategy[data.Key].ActualSignal;
                }
                // Only execute an order if the strategy is unlocked
                if (actualOrder != OrderSignal.doNothing && Strategy[data.Key].IsActive)
                {
                    // set now because MarketOrder fills can happen before ExecuteStrategy returns.
                    Strategy[data.Key].Status = OrderStatus.New;
                    Strategy[data.Key].IsActive = false;
                    ExecuteStrategy(data.Key, actualOrder);
                }
            }
            #region "biglog"

            string logmsg =
                string.Format(
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20}" +
                    ",{21},{22},{23},{24},{25},{26},{27},{28}",
                    data.Key.Value,
                    Time,
                    barcount,
                    data.Value.Volume,
                    data.Value.Open,
                    data.Value.High,
                    data.Value.Low,
                    data.Value.Close,
                    "",
                    Time.ToShortTimeString(),
                    Strategy[data.Key].Trend.Current.Value,
                    Strategy[data.Key].nTrig,
                    Strategy[data.Key].TrendMomentum,
                    Strategy[data.Key].comment,
                    "",
                    Strategy[data.Key].nEntryPrice,
                    Strategy[data.Key].MomentumWindow.IsReady ? Strategy[data.Key].MomentumWindow[0] : 0,
                    Strategy[data.Key].TradeAttempts,
                    Strategy[data.Key].Status,
                    Strategy[data.Key].ActualSignal,
                    "",
                    Strategy[data.Key].IsActive,
                    Portfolio[data.Key].UnrealizedProfit,
                    Portfolio[data.Key].Quantity,
                    Portfolio[data.Key].LastTradeProfit,
                    Portfolio.TotalPortfolioValue,
                    "",
                    "",
                    ""
                    );
            mylog.Debug(logmsg);



            #endregion


        }
        /// <summary>
        /// If the limit order aren't filled, then cancels the order and send a market order.
        /// </summary>
        private void CheckLimitOrderStatus(KeyValuePair<Symbol, TradeBar> data)
        {
            // GetOrderTickets should return only 1 ticket, but since it returns an Enumerable 
            //   the code needs to iterate it.
            foreach (var liveticket in Transactions.GetOrderTickets(
                t => t.Symbol == data.Key && t.Status == OrderStatus.Submitted))
            {
                CheckNumberOfTradeAttempts(data, liveticket);
            }
        }
        private void CheckNumberOfTradeAttempts(KeyValuePair<Symbol, TradeBar> data, OrderTicket liveticket)
        {
            //Log(string.Format("Trade Attempts: {0} OrderId {1}", currentSignalInfo.TradeAttempts, liveticket.OrderId));
            if (Strategy[data.Key].TradeAttempts++ > 3)
            {
                liveticket.Cancel();
                //Log(string.Format("Order {0} cancellation sent. Trade attempts > 3.", liveticket.OrderId));
            }
        }
        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            string symbol = orderEvent.Symbol.Value;
            Strategy[symbol].Status = orderEvent.Status;
            switch (orderEvent.Status)
            {
                case OrderStatus.New:
                case OrderStatus.None:
                case OrderStatus.Submitted:
                    Strategy[orderEvent.Symbol.Value].orderFilled = false;
                    break;
                case OrderStatus.Invalid:
                    Strategy[symbol].TradeAttempts = 0;
                    Strategy[symbol].orderFilled = false;
                    break;
                case OrderStatus.PartiallyFilled:
                    if (Strategy[symbol] != null)
                    {
                        Strategy[symbol].orderFilled = true;
                        // Do not unlock the strategy
                        Strategy[symbol].nEntryPrice= Portfolio[orderEvent.Symbol].HoldStock ? Portfolio[orderEvent.Symbol].AveragePrice : 0;
                        Strategy[symbol].orderFilled = true;
                        Strategy[symbol].TradeAttempts++;
                        //Log(string.Format("Trade Attempts: {0} OrderId {1}", currentSignalInfo.TradeAttempts, orderEvent.OrderId));
                    }

                    break;
                case OrderStatus.Canceled:
                    if (Strategy[symbol] != null)
                    {
                        //Log(string.Format("Order {0} cancelled.", orderEvent.OrderId));
                        Strategy[symbol].IsActive = true;       // Unlock the strategy for the next bar 
                        Strategy[symbol].TradeAttempts = 0;     // Reset the number of trade attempts.
                        Strategy[symbol].orderFilled = false;
                    }
                    break;
                case OrderStatus.Filled:
                    if (Strategy[symbol] != null)
                    {
                        //Log(string.Format("Order Filled OrderId {0} on attempt {1}", orderEvent.OrderId, currentSignalInfo.TradeAttempts));
                        Strategy[symbol].IsActive = true;
                        Strategy[symbol].TradeAttempts = 0;
                        Strategy[symbol].nEntryPrice = Portfolio[orderEvent.Symbol].HoldStock ? Portfolio[orderEvent.Symbol].AveragePrice : 0;
                        Strategy[symbol].orderFilled = true;

                    }
                    #region "Report Transaction"
                    #endregion
                    break;
            }
        }


        private OrderSignal ClosePositions(string symbol)
        {
            OrderSignal actualOrder;
            if (Strategy[symbol].Position == StockState.longPosition) actualOrder = OrderSignal.closeLong;
            else if (Strategy[symbol].Position == StockState.shortPosition) actualOrder = OrderSignal.closeShort;
            else actualOrder = OrderSignal.doNothing;
            return actualOrder;
        }
        /// <summary>
        /// Executes the ITrend strategy orders.
        /// </summary>
        /// <param name="symbol">The symbol to be traded.</param>
        /// <param name="actualOrder">The actual order to be execute.</param>
        private void ExecuteStrategy(string symbol, OrderSignal actualOrder)
        {
            // Define the operation size.  PositionShares can sometimes return 0
            //   if your position gets overextended.  
            // If that happens the code avoids an invalid order, by just returning.
            int shares = PositionShares(symbol, actualOrder);
            if (shares == 0)
            {
                Strategy[symbol].IsActive = true;
                return;
            }

            switch (actualOrder)
            {
                case OrderSignal.goLong:
                case OrderSignal.goShort:
                case OrderSignal.goLongLimit:
                case OrderSignal.goShortLimit:
                    //Log("===> Entry to Market");
                    decimal limitPrice;
                    var barPrices = Securities[symbol];

                    // Define the limit price.
                    if (actualOrder == OrderSignal.goLong ||
                        actualOrder == OrderSignal.goLongLimit)
                    {
                        limitPrice = Math.Max(barPrices.Low,
                                    (barPrices.Close - (barPrices.High - barPrices.Low) * RngFac));
                    }
                    else
                    {
                        limitPrice = Math.Min(barPrices.High,
                                    (barPrices.Close + (barPrices.High - barPrices.Low) * RngFac));
                    }
                    // Send the order.
                    LimitOrder(symbol, shares, limitPrice);
                    break;

                case OrderSignal.closeLong:
                case OrderSignal.closeShort:
                    //Log("<=== Closing Position");
                    // Send the order.
                    var ticket = MarketOrder(symbol, shares);
                    break;

                case OrderSignal.revertToLong:
                case OrderSignal.revertToShort:
                    //Log("<===> Reverting Position");
                    // Send the order.
                    MarketOrder(symbol, shares);
                    break;

                default: break;
            }
        }
        /// <summary>
        /// Estimate number of shares, given a kind of operation.
        /// </summary>
        /// <param name="symbol">The symbol to operate.</param>
        /// <param name="order">The kind of order.</param>
        /// <returns>The signed number of shares given the operation.</returns>
        public int PositionShares(string symbol, OrderSignal order)
        {
            int quantity;
            int operationQuantity;

            switch (order)
            {
                case OrderSignal.goLong:
                case OrderSignal.goLongLimit:
                    operationQuantity = CalculateOrderQuantity(symbol, ShareSize[symbol]);
                    quantity = Math.Min(maxOperationQuantity, operationQuantity);
                    break;

                case OrderSignal.goShort:
                case OrderSignal.goShortLimit:
                    operationQuantity = CalculateOrderQuantity(symbol, -ShareSize[symbol]);
                    quantity = Math.Max(-maxOperationQuantity, operationQuantity);
                    break;

                case OrderSignal.closeLong:
                case OrderSignal.closeShort:
                    quantity = -Portfolio[symbol].Quantity;
                    break;

                case OrderSignal.revertToLong:
                case OrderSignal.revertToShort:
                    quantity = -2 * Portfolio[symbol].Quantity;
                    break;

                default:
                    quantity = 0;
                    break;
            }
            return quantity;
        }
        /// <summary>
        /// Handles the On end of algorithm 
        /// </summary>
        public override void OnEndOfAlgorithm()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var s in Symbols)
            {
                sb.Append(s);
                sb.Append(",");
            }
            string symbolsstring = sb.ToString();
            symbolsstring = symbolsstring.Substring(0, symbolsstring.LastIndexOf(",", System.StringComparison.Ordinal));
            string debugstring =
                string.Format(
                    "\nAlgorithm Name: {0}\n Symbols: {1}\n Ending Portfolio Value: {2}\n Start Time: {3} \t {4} \n End Time: {5} \t {6}",
                    this.GetType().Name, symbolsstring, Portfolio.TotalPortfolioValue, startTime, _startDate,
                    DateTime.Now, _endDate);
            Logging.Log.Trace(debugstring);
        }


    }
}