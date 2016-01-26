using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Algorithm.CSharp.Benchmarks;
using QuantConnect.Configuration;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.CSharp
{
    public class TwoBarReversalAlgorithm : QCAlgorithm
    {
        private DateTime startTime = DateTime.Now;
        private DateTime _startDate = new DateTime(2015, 10, 27);
        private DateTime _endDate = new DateTime(2015, 11, 27);
        private const decimal PortfolioAmount = 25000;
        /*********** Control Panel **********/
        private bool shouldSellOutAtEod = true;         // Should the algo sell out at 15 min before EoD.
        private int maxOperationQuantity = 500;         // Maximum shares per operation.
        private decimal RngFac = 0.35m;                 // Percentage of the bar range used to estimate limit prices.
        private decimal minBarSize = 0.19m;
        private decimal barDifferenceTolerance = .05m;
        /************************************/

        private string[] _symbolarray = { "WYNN" };
        private List<string> _symbols = new List<string>();

        public TradeBarConsolidator Consolidator15Minute = new TradeBarConsolidator(TimeSpan.FromMinutes(15));
        public List<TwoBarReversalStrategy> Strategies = new List<TwoBarReversalStrategy>();
        private int _barcount = 0;

        private ILogHandler mylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("CustomFileLogHandler");
        private string ondataheader =
            @"Symbol, Time,BarCount,Volume,,Time, Open,High,Low,Close,Target,Stop, Attempts, , , ,Entry,Exit, IsActive,Unrealized, , Owned, TradeNet, Portfolio";

        private List<OrderTransaction> _transactions;
        private OrderTransactionProcessor _orderTransactionProcessor = new OrderTransactionProcessor();
        private List<TradeBar> list15 = new List<TradeBar>(); 

        public override void Initialize()
        {
            var mbs = Config.Get("size");
            minBarSize = Convert.ToDecimal(mbs);
            var tolerance = Config.Get("tolerance");
            barDifferenceTolerance = Convert.ToDecimal(tolerance);

            //Initialize dates
            var sd = Config.Get("start-date");
            var ed = Config.Get("end-date");

            _startDate = new DateTime(Convert.ToInt32(sd.Substring(0, 4)), Convert.ToInt32(sd.Substring(4, 2)), Convert.ToInt32(sd.Substring(6, 2)));
            _endDate = new DateTime(Convert.ToInt32(ed.Substring(0, 4)), Convert.ToInt32(ed.Substring(4, 2)), Convert.ToInt32(ed.Substring(6, 2)));

            _symbolarray = Config.Get("symbols").Split(',');

            SetStartDate(_startDate);       // Set Start Date
            SetEndDate(_endDate);           // Set End Date
            SetCash(PortfolioAmount);       // Set Strategy Cash

            foreach (string t in _symbolarray)
            {
                _symbols.Add(t);
            }

            foreach (string symbol in _symbols)
            {
                AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);
            }
            foreach (Symbol symbol in Portfolio.Keys)
            {
                Consolidator15Minute.DataConsolidated += On15Minute;
                SubscriptionManager.AddConsolidator(symbol, Consolidator15Minute);
                Strategies.Add(new TwoBarReversalStrategy(symbol, this, barDifferenceTolerance, minBarSize));
            }

            #region logging
            var algoname = GetType().Name;
            mylog.Debug(algoname);
            mylog.Debug(ondataheader);
            _orderTransactionProcessor = new OrderTransactionProcessor();
            _transactions = new List<OrderTransaction>();

            #endregion
        }

        private void On15Minute(object sender, TradeBar tradeBar)
        {
            //list15.Add(tradeBar);
            var strategy = Strategies.FirstOrDefault(s => s.GetSymbol() == tradeBar.Symbol);
            if (strategy != null)
            {
                strategy.Position = GetPosition(tradeBar.Symbol);
                strategy.CurrentTradeBar = tradeBar;
                strategy.CheckSignal();
            }
        }
        #region "one minute events"
        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">TradeBars IDictionary object with your stock data</param>
        public void OnData(TradeBars data)
        {
            _barcount++;
            foreach (KeyValuePair<Symbol, TradeBar> kvp in data)
            {
                OnDataForSymbol(kvp);
            }
        }

        private void OnDataForSymbol(KeyValuePair<Symbol, TradeBar> data)
        {
            if (_barcount > 14035)
                System.Diagnostics.Debug.WriteLine("here");
            var strategy = Strategies.FirstOrDefault(s => s.GetSymbol() == data.Key);
            if (strategy != null)
            {
                strategy.Position = GetPosition(data.Value.Symbol);
                strategy.CurrentTradeBar = data.Value;
                strategy.CheckSignal();

                SellOutAtEndOfDay(data, ref strategy.ActualSignal);

                if (strategy.ActualSignal != OrderSignal.doNothing && strategy.IsActive)
                {
                    HandlePartiallyFilled(data, strategy);
                    // set now because MarketOrder fills can happen before ExecuteStrategy returns.
                    strategy.Status = OrderStatus.New;
                    strategy.IsActive = false;
                    ExecuteStrategy(data.Key, strategy, data);
                }
                #region logging

                #region "biglog"

                //Log(data.Key.Value);
                string logmsg =
                    string.Format(
                        "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20}" +
                        ",{21},{22},{23},{24},{25},{26},{27},{28},{29},{30},{31},{32}",
                        data.Key.Value,
                        this.Time,
                        _barcount,
                        data.Value.Volume,
                        "",
                        Time.ToShortTimeString(),
                        data.Value.Open,
                        data.Value.High,
                        data.Value.Low,
                        data.Value.Close,
                        strategy.TargetPrice,
                        strategy.StopPrice,
                        strategy.TradeAttempts,
                        "",
                        "",
                        "",
                        strategy.Entryprice,
                        strategy.Exitprice,
                        strategy.IsActive,
                        Portfolio.TotalUnrealisedProfit,
                        "",
                        Portfolio[data.Key].Quantity,
                        Portfolio[data.Key].LastTradeProfit,
                        Portfolio.TotalPortfolioValue,
                        strategy.TwoBar.IsReady ? strategy.TwoBar.BarsWindow[0].Open.ToString(CultureInfo.InvariantCulture) : "",
                        strategy.TwoBar.IsReady ? strategy.TwoBar.BarsWindow[0].High.ToString(CultureInfo.InvariantCulture) : "",
                        strategy.TwoBar.IsReady ? strategy.TwoBar.BarsWindow[0].Low.ToString(CultureInfo.InvariantCulture) : "",
                        strategy.TwoBar.IsReady ? strategy.TwoBar.BarsWindow[0].Close.ToString(CultureInfo.InvariantCulture) : "",
                        strategy.TwoBar.IsReady ? strategy.TwoBar.BarsWindow[1].Open.ToString(CultureInfo.InvariantCulture) : "",
                        strategy.TwoBar.IsReady ? strategy.TwoBar.BarsWindow[1].High.ToString(CultureInfo.InvariantCulture) : "",
                        strategy.TwoBar.IsReady ? strategy.TwoBar.BarsWindow[1].Low.ToString(CultureInfo.InvariantCulture) : "",
                        strategy.TwoBar.IsReady ? strategy.TwoBar.BarsWindow[1].Close.ToString(CultureInfo.InvariantCulture) : "",
                        ""
                        );
                //mylog.Debug(logmsg);
                #endregion

                #endregion

            }
        }

        /// <summary>
        /// Handle order events
        /// </summary>
        /// <param name="orderEvent">the order event</param>
        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            base.OnOrderEvent(orderEvent);
            ProcessOrderEvent(orderEvent);
        }
        /// <summary>
        /// Handles the On end of algorithm 
        /// </summary>
        public override void OnEndOfAlgorithm()
        {
            StringBuilder sb = new StringBuilder();
            //sb.Append(" Symbols: ");
            foreach (var s in _symbols)
            {

                sb.Append(s);
                sb.Append(",");
            }
            string symbolsstring = sb.ToString();
            symbolsstring = symbolsstring.Substring(0, symbolsstring.LastIndexOf(",", System.StringComparison.Ordinal));
            string debugstring =
                string.Format(
                    "\nAlgorithm Name: {0}\n Symbol: {1}\n Ending Portfolio Value: {2} \n Start Time: {3} \t {4} \n End Time: {5} \t {6}",
                    this.GetType().Name, symbolsstring, Portfolio.TotalPortfolioValue, startTime, _startDate,
                    DateTime.Now, _endDate);
            Log(debugstring);
            Log(string.Format("MinBarSize: {0}", minBarSize));
            Log(string.Format("BarDifferenceTolerance: {0}", barDifferenceTolerance));

            #region logging

            ////NotifyUser();

            ////            string filepath = @"I:\MyQuantConnect\Logs\" + symbol + "dailyreturns" + sd + ".csv";
            //string filepath = @"I:\MyQuantConnect\Logs\" + symbol + "dailyreturns.csv";
            //using (
            //    StreamWriter sw = new StreamWriter(filepath))
            //{
            //    sw.Write(minuteHeader.ToString());
            //    sw.Write(minuteReturns.ToString());
            //    sw.Flush();
            //    sw.Close();
            //}
            //SendTradeBar15ToFile(symbolsstring + "tradebars.csv");
            //SendTradesToFile(symbolsstring + "trades.csv", _orderTransactionProcessor.Trades);
            //SendTransactionsToFile(symbolsstring + "transactions.csv");

            #endregion
        }



        #endregion

        #region "Methods"
        /// <summary>
        /// Local processing of the order event.  It only logs the transaction and orderEvent
        /// </summary>
        /// <param name="orderEvent">OrderEvent - the order event</param>
        private void ProcessOrderEvent(OrderEvent orderEvent)
        {
            int orderId;
            var strategy = Strategies.FirstOrDefault(s => s.GetSymbol() == orderEvent.Symbol);
            if (strategy != null)
            {
                orderId = orderEvent.OrderId;

                strategy.Status = orderEvent.Status;
            }

            switch (orderEvent.Status)
            {
                case OrderStatus.New:
                case OrderStatus.None:
                case OrderStatus.Submitted:
                case OrderStatus.Invalid:
                    break;
                case OrderStatus.PartiallyFilled:
                    if (strategy != null)
                    {
                        //nEntryPrice = Portfolio[orderEvent.Symbol].HoldStock ? Portfolio[orderEvent.Symbol].AveragePrice : 0;
                        strategy.TradeAttempts++;
                        //Log(string.Format("Trade Attempts: {0} OrderId {1}", currentSignalInfo.TradeAttempts, orderEvent.OrderId));
                    }

                    break;
                case OrderStatus.Canceled:
                    if (strategy != null)
                    {
                        //Log(string.Format("Order {0} cancelled.", orderEvent.OrderId));
                        strategy.IsActive = true;
                        strategy.TradeAttempts = 0;
                        strategy.ActualSignal = OrderSignal.doNothing;
                    }
                    break;
                case OrderStatus.Filled:

                    if (strategy != null)
                    {
                        strategy.Entryprice = Portfolio[orderEvent.Symbol].HoldStock ? Portfolio[orderEvent.Symbol].AveragePrice : 0;
                        strategy.Exitprice = Portfolio[orderEvent.Symbol].HoldStock ? 0 : orderEvent.FillPrice;
                        strategy.IsActive = true;
                        //if (currentSignalInfo.TradeAttempts > 0)
                        //    Log(string.Format("Order Filled OrderId {0} on attempt {1}", orderEvent.OrderId, currentSignalInfo.TradeAttempts));
                        strategy.TradeAttempts = 0;
                        strategy.ActualSignal = OrderSignal.doNothing;


                    }
                    
                    IEnumerable<OrderTicket> tickets = Transactions.GetOrderTickets(t => t.OrderId == orderEvent.OrderId);
                    if (tickets != null)
                    {
                        foreach (var t in from ticket in tickets let transactionFactory = new OrderTransactionFactory((QCAlgorithm)this) select transactionFactory.Create(orderEvent, ticket, false))
                        {
                            _transactions.Add(t);
                            _orderTransactionProcessor.ProcessTransaction(t);
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Executes the ITrend strategy orders.
        /// </summary>
        /// <param name="symbol">The symbol to be traded.</param>
        /// <param name="strategy">The actual arder to be execute.</param>
        /// <param name="data">The actual TradeBar data.</param>
        private void ExecuteStrategy(Symbol symbol, BaseStrategy strategy, KeyValuePair<Symbol, TradeBar> data)
        {
            decimal limitPrice = 0m;
            int shares = Convert.ToInt32(PositionShares(symbol, strategy));
            if (shares == 0)
            {
                return;
            }
            ILimitPriceCalculator priceCalculator = new InstantTrendLimitPriceCalculator();
            OrderTicket ticket;

            if (shares == 0)
                strategy.ActualSignal = OrderSignal.doNothing;

            switch (strategy.ActualSignal)
            {
                case OrderSignal.goLongLimit:
                    // Define the limit price.
                    limitPrice = priceCalculator.Calculate(data.Value, strategy.ActualSignal, RngFac);
                    //ticket = MarketOrder(symbol, shares, false, strategy.Id.ToString(CultureInfo.InvariantCulture));
                    ticket = LimitOrder(symbol, shares, limitPrice);

                    break;

                case OrderSignal.goShortLimit:
                    limitPrice = priceCalculator.Calculate(data.Value, strategy.ActualSignal, RngFac);
                    //ticket = MarketOrder(symbol, shares, false);
                    ticket = LimitOrder(symbol, shares, limitPrice);

                    break;

                case OrderSignal.goLong:
                case OrderSignal.goShort:
                case OrderSignal.closeLong:
                case OrderSignal.closeShort:
                case OrderSignal.revertToLong:
                case OrderSignal.revertToShort:
                    ticket = MarketOrder(symbol, shares);
                    //_ticketsQueue.Add(ticket);
                    break;

                default: break;
            }
        }
        private decimal GetBetSize(Symbol symbol, BaseStrategy strategy)
        {
            decimal currentPrice = Portfolio[symbol].Price;
            decimal betsize = Portfolio[symbol].Invested
                ? Math.Abs(Portfolio[symbol].Quantity)
                : Math.Abs((15m / 26m) * Portfolio.Cash / currentPrice);
            if (betsize <= 10)
            {
                betsize = 0;
                strategy.ActualSignal = OrderSignal.doNothing;
            }
            return betsize;

        }
        /// <summary>
        /// Estimate number of shares, given a kind of operation.
        /// </summary>
        /// <param name="symbol">The symbol to operate.</param>
        /// <param name="order">The kind of order.</param>
        /// <returns>The signed number of shares given the operation.</returns>
        public decimal PositionShares(Symbol symbol, BaseStrategy strategy)
        {
            decimal quantity = 0;
            int operationQuantity;
            decimal targetSize = GetBetSize(symbol, strategy);
            if (targetSize <= 10)
            {
                targetSize = 0;
                strategy.ActualSignal = OrderSignal.doNothing;
            }

            switch (strategy.ActualSignal)
            {
                case OrderSignal.goLongLimit:
                case OrderSignal.goLong:
                    //operationQuantity = CalculateOrderQuantity(symbol, targetSize);     // let the algo decide on order quantity
                    //operationQuantity = (int)targetSize;
                    quantity = Math.Min(maxOperationQuantity, targetSize);
                    break;

                case OrderSignal.goShortLimit:
                case OrderSignal.goShort:
                    //operationQuantity = CalculateOrderQuantity(symbol, targetSize);     // let the algo decide on order quantity
                    operationQuantity = (int)targetSize;
                    quantity = -Math.Min(maxOperationQuantity, targetSize);
                    break;

                case OrderSignal.closeLong:
                case OrderSignal.closeShort:
                    if (Portfolio[symbol].Quantity != 0)
                        quantity = -Portfolio[symbol].Quantity;
                    break;

                case OrderSignal.revertToLong:
                case OrderSignal.revertToShort:
                    if (Portfolio[symbol].Quantity != 0)
                        quantity = -2 * Portfolio[symbol].Quantity;
                    break;

                default:
                    quantity = 0;
                    break;
            }

            return quantity;
        }

        #endregion
        #region "Logging Methods"
        private void SendTradesToFile(string filename, IList<MatchedTrade> tradelist)
        {
            string filepath = @"I:\MyQuantConnect\Logs\" + filename;
            //string filepath = AssemblyLocator.ExecutingDirectory() + filename;
            if (File.Exists(filepath)) File.Delete(filepath);
            var liststring = CsvSerializer.Serialize<MatchedTrade>(",", tradelist);
            using (StreamWriter fs = new StreamWriter(filepath, true))
            {
                foreach (var s in liststring)
                    fs.WriteLine(s);
                fs.Flush();
                fs.Close();
            }
        }

        private void SendTransactionsToFile(string filename)
        {
            string filepath = @"I:\MyQuantConnect\Logs\" + filename;
            //string filepath = AssemblyLocator.ExecutingDirectory() + "transactions.csv";
            //if (File.Exists(filepath)) File.Delete(filepath);
            var liststring = CsvSerializer.Serialize<OrderTransaction>(",", _transactions, true);
            using (StreamWriter fs = new StreamWriter(filepath, true))
            {
                foreach (var s in liststring)
                {
                    if (!s.Contains("Symbol"))
                        fs.WriteLine(s);
                }
                fs.Flush();
                fs.Close();
            }
        }
        private void SendTradeBar15ToFile(string filename)
        {
            string filepath = @"I:\MyQuantConnect\Logs\" + filename;
            var liststring = CsvSerializer.Serialize<TradeBar>(",", list15, true);
            using (StreamWriter fs = new StreamWriter(filepath, true))
            {
                foreach (var s in liststring)
                {
                    //if (!s.Contains("Symbol"))
                        fs.WriteLine(s);
                }
                fs.Flush();
                fs.Close();
            }
        }
        private void SendOrderEventsToFile()
        {
            //string filepath = AssemblyLocator.ExecutingDirectory() + "orderEvents.csv";
            //if (File.Exists(filepath)) File.Delete(filepath);
            //var liststring = CsvSerializer.Serialize<OrderEvent>(",", _orderEvents, true);
            //using (StreamWriter fs = new StreamWriter(filepath, true))
            //{
            //    foreach (var s in liststring)
            //        fs.WriteLine(s);
            //    fs.Flush();
            //    fs.Close();
            //}
        }
        #endregion


        #region Helpers
        /// <summary>
        /// Sells out all positions at 15:50, and calculates the profits for the day
        ///  emails the transactions for the day to me
        /// </summary>
        /// <param name="data">TradeBars - the data</param>
        /// <param name="signal">the current OrderSignal</param>
        /// <returns>false if end of day, true during the day </returns>
        private void SellOutAtEndOfDay(KeyValuePair<Symbol, TradeBar> data, ref OrderSignal signal)
        {

            if (shouldSellOutAtEod)
            {
                //signal = OrderSignal.doNothing;      // Just in case a signal slipped through in the last minute.
                #region logging
                if (Time.Hour == 16)
                {
                    signal = OrderSignal.doNothing;
                    #region logging

                    //SendTransactionsToFile(data.Key + "transactions.csv");
                    #endregion

                    //NotifyUser();
                }
                #endregion

                if (Time.Hour == 15 && Time.Minute > 45)
                {
                    signal = OrderSignal.doNothing;
                    if (Portfolio[data.Key].IsLong)
                    {
                        signal = OrderSignal.closeShort;
                    }
                    if (Portfolio[data.Key].IsShort)
                    {
                        signal = OrderSignal.closeLong;
                    }
                }
            }
        }
        private void HandlePartiallyFilled(KeyValuePair<Symbol, TradeBar> data, TwoBarReversalStrategy strategy)
        {
            IEnumerable<OrderTicket> livetickets =
                Transactions.GetOrderTickets(
                    t => t.Symbol == data.Key && (t.Status == OrderStatus.Submitted || t.Status == OrderStatus.PartiallyFilled));

            if (livetickets != null)
            {
                foreach (OrderTicket liveticket in livetickets)
                {
                    if (strategy.TradeAttempts++ > 3)
                    {
                        liveticket.Cancel();
                        //Log(string.Format("Order {0} cancellation sent. Trade attempts > 3.", liveticket.OrderId));

                    }
                }
            }
        }
        private StockState GetPosition(Symbol symbol)
        {
            int position = Portfolio[symbol].Quantity;
            if (position > 0) return StockState.longPosition;
            if (position < 0) return StockState.shortPosition;
            return StockState.noInvested;
        }
        #endregion
    }
}
