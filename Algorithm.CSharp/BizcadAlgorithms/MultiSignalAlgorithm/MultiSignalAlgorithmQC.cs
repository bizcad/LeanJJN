using System;
using System.Collections.Generic;
using System.Globalization;

using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.CSharp
{
    public class MultiSignalAlgorithmQC : QCAlgorithm
    {

        #region "Variables"
        DateTime startTime = DateTime.Now;
        //private DateTime _startDate = new DateTime(2015, 8, 10);
        //private DateTime _endDate = new DateTime(2015, 8, 14);
        private DateTime _startDate = new DateTime(2015, 12, 14);
        private DateTime _endDate = new DateTime(2015, 12, 14);
        private decimal _portfolioAmount = 26000;
        private decimal _transactionSize = 15000;
        //+----------------------------------------------------------------------------------------+
        //  Algorithm Control Panel                         
        // +---------------------------------------------------------------------------------------+
        private decimal maxOperationQuantity = 500;         // Maximum shares per operation.

        private decimal RngFac = 0.35m;                 // Percentage of the bar range used to estimate limit prices.

        private decimal lossThreshhold = -55;           // When unrealized losses fall below, revert position
        // +---------------------------------------------------------------------------------------+

        private List<Symbol> Symbols;
        //        private Symbol symbol;

        private int barcount = 0;

        #region lists

        List<SignalInfo> signalInfos = new List<SignalInfo>();
        #endregion
        #region "logging P&L"
        // *****************  P & L ************/
        //private ILogHandler mylog;
        //        private string ondataheader =
        //            @"Time,BarCount,Volume, Open,High,Low,Close,EndTime,Period,DataType,IsFillForward,Time,Symbol,Price,,,Time,Price,Trend, Trigger, orderSignal, Comment,, EntryPrice, Exit Price,Unrealized,Order Id, Owned, TradeNet, Portfolio";

        private DateTime tradingDate;
        //private decimal totalProfit = 0;
        //        private decimal tradeprofit = 0m;
        //        private decimal tradefees = 0m;
        private decimal tradenet = 0m;        // *****************  P & L ************/
        #endregion
        private List<OrderTransaction> _transactions;
        //        private OrderTransactionProcessor _orderTransactionProcessor = new OrderTransactionProcessor();
        //private int _tradecount = 0;

        #endregion

        private bool shouldSellOutAtEod = true;
        private int orderId = 0;
        private decimal nEntryPrice = 0;
        private string comment;

        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        /// <seealso cref="QCAlgorithm.SetStartDate(System.DateTime)"/>
        /// <seealso cref="QCAlgorithm.SetEndDate(System.DateTime)"/>
        /// <seealso cref="QCAlgorithm.SetCash(decimal)"/>
        public override void Initialize()
        {
            //Initialize dates
            _startDate = new DateTime(2016, 1, 11);
            _endDate = new DateTime(2016, 1, 14);
            SetStartDate(_startDate);
            SetEndDate(_endDate);
            SetCash(_portfolioAmount);



            string symbolstring = "WYNN";

            #region logging
            //mylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("CustomFileLogHandler");
            //var algoname = this.GetType().Name;
            //mylog.Debug(algoname);
            //mylog.Debug(ondataheader);
            #endregion

            Symbols = new List<Symbol>();
            //Add as many securities as you like. All the data will be passed into the event handler:
            int id = 0;

            AddSecurity(SecurityType.Equity, symbolstring, Resolution.Minute);
            var keys = Securities.Keys;
            foreach (Symbol s in Securities.Keys)
            {
                Symbols.Add(s);
                signalInfos.Add(new SignalInfo()
                {
                    Id = id++,
                    Name = s.Value,
                    Symbol = s,
                    SignalType = typeof(Sig9),
                    Value = OrderSignal.doNothing,
                    IsActive = true,
                    Status = OrderStatus.None,
                    SignalJson = string.Empty,
                    InternalState = string.Empty,
                    Comment = string.Empty,
                    nTrig = 0,
                    Price = new RollingWindow<IndicatorDataPoint>(14),
                    trend = new InstantaneousTrend(s.Value, 7, .24m)
                });
            }

            //            _orderTransactionProcessor = new OrderTransactionProcessor();
            _transactions = new List<OrderTransaction>();

            // for use with Tradier. Default is IB.
            //var security = Securities[symbol];
            //security.TransactionModel = new ConstantFeeTransactionModel(1.0m);

        }
        #region "one minute events"
        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">TradeBars IDictionary object with your stock data</param>
        public void OnData(TradeBars data)
        {
            foreach (KeyValuePair<Symbol, TradeBar> kvp in data)
            {
                //Log(kvp.Key);
                OnDataForSymbol(kvp);
            }
        }
        private void OnDataForSymbol(KeyValuePair<Symbol, TradeBar> data)
        {
            #region logging

            comment = string.Empty;
            tradingDate = this.Time;

            #endregion

            barcount++;


            var time = this.Time;
            //string tradeBarMessage = JsonConvert.SerializeObject(data);

            //EmailTradeBarMessage(tradeBarMessage);

            #region "Guts of the Algorithm"
            try
            {
                List<SignalInfo> minuteSignalInfos = new List<SignalInfo>(signalInfos.Where(s => s.Name == data.Key.Value));
                if (minuteSignalInfos.Any())
                {
                    foreach (var signalInfo in minuteSignalInfos)
                    {
                        signalInfo.Price.Add(idp(time, (data.Value.Close + data.Value.Open) / 2));
                        // Update the indicators
                        signalInfo.trend.Update(idp(time, signalInfo.Price[0].Value));
                    }
                    // Get the OrderSignal from the Sig9
                    GetOrderSignals(data, minuteSignalInfos);
                    foreach (var currentSignalInfo in minuteSignalInfos)
                    {
                        // If EOD, set signal to sell/buy out.
                        OrderSignal signal = currentSignalInfo.Value;
                        SellOutAtEndOfDay(data, ref signal);
                        currentSignalInfo.Value = signal;
                        if (currentSignalInfo.Status == OrderStatus.Submitted)
                        {
                            HandleSubmitted(data, currentSignalInfo);
                        }
                        if (currentSignalInfo.Status == OrderStatus.PartiallyFilled)
                        {
                            HandlePartiallyFilled(data, currentSignalInfo);
                        }

                        if (currentSignalInfo.Value != OrderSignal.doNothing && currentSignalInfo.IsActive)
                        {
                            // set now because MarketOrder fills can happen before ExecuteStrategy returns.
                            currentSignalInfo.Status = OrderStatus.New;
                            currentSignalInfo.IsActive = false;
                            ExecuteStrategy(currentSignalInfo.Symbol, currentSignalInfo, data);
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                string msg = string.Format("Error in OnDataForSymbol: {0} \n StackTrace {1}", ex.Message, ex.StackTrace);
                Notify.Email("nicholasstein@cox.net",
                "Exception",
                msg,
                null);
                Log(msg);
                Quit(msg);
            }

            #endregion
            var sharesOwned = Portfolio[data.Key].Quantity;
            #region "biglog"

            string logmsg =
                string.Format(
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11}",
                    data.Value.Symbol.Value,
                    time,
                    barcount,
                    data.Value.Volume,
                    Math.Round(data.Value.Open, 4),
                    Math.Round(data.Value.High, 4),
                    Math.Round(data.Value.Low, 4),
                    Math.Round(data.Value.Close, 4),
                    data.Value.EndTime.ToShortTimeString(),
                    Math.Round(signalInfos[0].trend.Current.Value, 4),
                    Math.Round(signalInfos[0].nTrig, 4),
                    signalInfos[0].Value
                    );

            
            //tradeprofit = 0;
            //tradefees = 0;
            tradenet = 0;
            #endregion

            #region "logging"

            
            EmailTradeBarMessage(logmsg);
            PostMessage(typeof (Message), logmsg);
            //mylog.Debug(logmsg);
            #endregion
            //tradeprofit = 0;
            //tradefees = 0;
            //tradenet = 0;
            // At the end of day, reset the trend and trendHistory
            if (time.Hour == 16)
            {
                barcount = 0;
            }
            System.Threading.Thread.Sleep(1000);
        }

        private void EmailTradeBarMessage(string logmsg)
        {
            Log(logmsg);
            Message m = new Message {Id = barcount, MessageType = "logmsg", Contents = logmsg};
            string json = JsonConvert.SerializeObject(m);
            Notify.Email("quantconnect@bizcad.com", string.Format("TradeBars For: {0} {1}", Time.ToShortDateString(), Time.ToLongTimeString()), json, json);
            
        }

        private void PostMessage(Type type, string logmsg)
        {
            Message message = new Message
            {
                Id = 0,
                MessageType = "Message",
                Contents = logmsg
            };
            string address = @"http://bizcadsignalrchat.azurewebsites.net/Messages/Create";
            //string address = @""http://localhost:64527/Messages/Create/";

            Notify.Web(address, message);
        }

        private void HandlePartiallyFilled(KeyValuePair<Symbol, TradeBar> data, SignalInfo currentSignalInfo)
        {
            IEnumerable<OrderTicket> livetickets =
                Transactions.GetOrderTickets(
                    t => t.Symbol == data.Key && t.Status == OrderStatus.Submitted);

            if (livetickets != null)
            {
                foreach (OrderTicket liveticket in livetickets)
                {
                    if (liveticket.Quantity > 0) // long
                    {
                        AlterLongLimit(data, liveticket, currentSignalInfo);
                    }
                    else // short
                    {
                        AlterShortLimit(data, liveticket, currentSignalInfo);
                    }
                }
            }
        }

        private void HandleSubmitted(KeyValuePair<Symbol, TradeBar> data, SignalInfo currentSignalInfo)
        {
            IEnumerable<OrderTicket> livetickets =
                Transactions.GetOrderTickets(t => t.Symbol == data.Key && t.Status == OrderStatus.Submitted);

            if (livetickets != null)
            {
                foreach (OrderTicket liveticket in livetickets)
                {
                    if (liveticket.Quantity > 0) // long
                    {
                        AlterLongLimit(data, liveticket, currentSignalInfo);
                    }
                    else // short
                    {
                        AlterShortLimit(data, liveticket, currentSignalInfo);
                    }
                }
            }
        }

        private void AlterShortLimit(KeyValuePair<Symbol, TradeBar> data, OrderTicket liveticket, SignalInfo currentSignalInfo)
        {
            //Log(string.Format("Trade Attempts: {0} OrderId {1}", currentSignalInfo.TradeAttempts, liveticket.OrderId));
            if (currentSignalInfo.TradeAttempts++ > 3)
            {
                liveticket.Cancel();
                Log(string.Format("Order {0} cancellation sent. Trade attempts > 3.", liveticket.OrderId));

            }
        }

        private void AlterLongLimit(KeyValuePair<Symbol, TradeBar> data, OrderTicket liveticket, SignalInfo currentSignalInfo)
        {
            //var limit = liveticket.Get(OrderField.LimitPrice);
            //decimal newLimit = limit;
            //Log(string.Format("Trade Attempts: {0} OrderId {1}", currentSignalInfo.TradeAttempts, liveticket.OrderId));
            if (currentSignalInfo.TradeAttempts++ > 3)
            {
                liveticket.Cancel();
                Log(string.Format("Order {0} cancellation sent. Trade attempts > 3.", liveticket.OrderId));

            }
        }

        #endregion

        /// <summary>
        /// Run the strategy associated with this algorithm
        /// </summary>
        /// <param name="data">TradeBars - the data received by the OnData event</param>
        public void GetOrderSignals(KeyValuePair<Symbol, TradeBar> data, List<SignalInfo> signalInfos)
        {
            #region "GetOrderSignals Execution"

            foreach (SignalInfo info in signalInfos)
            {
                info.Value = OrderSignal.doNothing;

                Type t = info.SignalType;
                var sig = Activator.CreateInstance(t) as ISigSerializable;
                if (sig != null)
                {
                    sig.symbol = data.Key;
                    sig.Deserialize(info.SignalJson);
                    sig.Barcount = barcount; // for debugging
                    switch (info.Status)
                    {
                        case OrderStatus.None:
                        case OrderStatus.New:
                        case OrderStatus.Submitted:
                            sig.orderFilled = false;
                            break;
                        case OrderStatus.Filled:
                            info.TradeAttempts = 0;
                            sig.orderFilled = true;
                            break;
                        case OrderStatus.PartiallyFilled:
                            sig.orderFilled = true;
                            break;
                        case OrderStatus.Canceled:
                        case OrderStatus.Invalid:
                            info.TradeAttempts = 0;
                            sig.orderFilled = false;
                            break;
                    }
                    var sec = Portfolio[data.Key];
                    Dictionary<string, string> paramlist = new Dictionary<string, string>
                            {
                                {"symbol", data.Key.Value},
                                {"Barcount", barcount.ToString(CultureInfo.InvariantCulture)},
                                {"nEntryPrice", nEntryPrice.ToString(CultureInfo.InvariantCulture)},
                                {"IsLong", sec.IsLong.ToString()},
                                {"IsShort", sec.IsShort.ToString()},
                                {"trend", info.trend.Current.Value.ToString(CultureInfo.InvariantCulture)},
                                {"lossThreshhold", lossThreshhold.ToString(CultureInfo.InvariantCulture)},
                                {"UnrealizedProfit", sec.UnrealizedProfit.ToString(CultureInfo.InvariantCulture)}
                            };

                    if (barcount == 144)
                    {
                        System.Diagnostics.Debug.WriteLine("bar 144");
                    }
                    info.Value = sig.CheckSignal(data, paramlist, out comment);
                    info.nTrig = sig.nTrig;
                    info.Comment = comment;

                    if (Time.Hour == 16)
                    {
                        sig.Reset();
                    }
                    info.SignalJson = sig.Serialize();
                    info.InternalState = sig.GetInternalStateFields().ToString();
                }
            }

            #endregion  // execution
        }

        #region "Event Processiong"
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
        /// Local processing of the order event.  It only logs the transaction and orderEvent
        /// </summary>
        /// <param name="orderEvent">OrderEvent - the order event</param>
        private void ProcessOrderEvent(OrderEvent orderEvent)
        {
            //add to the list of order events which is saved to a file when running locally 
            //  I will use this file to test Stefano Raggi's code
            if (orderEvent.OrderId == 82)
                System.Diagnostics.Debug.WriteLine("ORder 82");
            var currentSignalInfo = signalInfos.FirstOrDefault(s => s.Symbol == orderEvent.Symbol);
            orderId = orderEvent.OrderId;

            if (currentSignalInfo != null) currentSignalInfo.Status = orderEvent.Status;
            IEnumerable<OrderTicket> tickets = Transactions.GetOrderTickets(t => t.OrderId == orderId);

            switch (orderEvent.Status)
            {
                case OrderStatus.New:
                case OrderStatus.None:
                case OrderStatus.Submitted:
                    break;
                case OrderStatus.Invalid:
                    Log(string.Format("Order {0} invalidated attempt {1}. {2} shares at {3}, {4}",
                        orderEvent.OrderId,
                        currentSignalInfo.TradeAttempts,
                        orderEvent.FillQuantity,
                        orderEvent.FillPrice,
                        orderEvent.Message));
                    break;
                case OrderStatus.PartiallyFilled:
                    if (currentSignalInfo != null)
                    {

                        nEntryPrice = Portfolio[orderEvent.Symbol].HoldStock ? Portfolio[orderEvent.Symbol].AveragePrice : 0;
                        currentSignalInfo.TradeAttempts++;
                        Log(string.Format("Order {0} Partial Fill confirmed on attempt {1}. {2} shares at {3}",
                            orderEvent.OrderId,
                            currentSignalInfo.TradeAttempts,
                            orderEvent.FillQuantity,
                            orderEvent.FillPrice));
                    }

                    break;
                case OrderStatus.Canceled:
                    if (currentSignalInfo != null)
                    {
                        Log(string.Format("Order {0} cancellation confirmed.", orderEvent.OrderId));
                        currentSignalInfo.IsActive = true;
                        currentSignalInfo.TradeAttempts = 0;
                        Log(string.Format("Order {0} Holdings for {1}: {2} shares at {3}",
                            orderEvent.OrderId,
                            Portfolio[orderEvent.Symbol].Symbol.Value,
                            Portfolio[orderEvent.Symbol].Quantity,
                            Portfolio[orderEvent.Symbol].AveragePrice));
                    }
                    break;
                case OrderStatus.Filled:

                    if (currentSignalInfo != null)
                    {
                        currentSignalInfo.IsActive = true;
                        Log(string.Format("Order {0} Fill {1} confirmed on attempt {2}. {3} shares at {4}",
                            orderEvent.OrderId,
                            currentSignalInfo.Value,
                            currentSignalInfo.TradeAttempts,
                            orderEvent.FillQuantity,
                            orderEvent.FillPrice));
                        currentSignalInfo.TradeAttempts = 0;
                    }
                    nEntryPrice = Portfolio[orderEvent.Symbol].HoldStock ? Portfolio[orderEvent.Symbol].AveragePrice : 0;

                    if (tickets != null)
                    {
                        foreach (OrderTicket ticket in tickets)
                        {
                            #region "save the ticket as a OrderTransacton"
                            OrderTransactionFactory transactionFactory = new OrderTransactionFactory((QCAlgorithm)this);
                            OrderTransaction t = transactionFactory.Create(orderEvent, ticket, false);
                            _transactions.Add(t);
                            #endregion
                        }
                        Log(string.Format("Order {0} Holdings for {1}: {2} shares at {3}",
                            orderEvent.OrderId,
                            Portfolio[orderEvent.Symbol].Symbol.Value,
                            Portfolio[orderEvent.Symbol].Quantity,
                            Portfolio[orderEvent.Symbol].AveragePrice));
                    }
                    break;
            }
        }


        #endregion
        //private decimal CalculateTradeProfit(Symbol symbol)
        //{
        //    return _orderTransactionProcessor.CalculateLastTradePandL(symbol);
        //}
        #region "Methods"
        /// <summary>
        /// Executes the ITrend strategy orders.
        /// </summary>
        /// <param name="symbol">The symbol to be traded.</param>
        /// <param name="signalInfo">The actual arder to be execute.</param>
        /// <param name="data">The actual TradeBar data.</param>
        private void ExecuteStrategy(Symbol symbol, SignalInfo signalInfo, KeyValuePair<Symbol, TradeBar> data)
        {
            return;
            int shares = Convert.ToInt32(PositionShares(symbol, signalInfo));
            if (shares != 0)
            {
                ILimitPriceCalculator priceCalculator = new InstantTrendLimitPriceCalculator();
                OrderTicket ticket = null;
                decimal limitPrice = 0;


                switch (signalInfo.Value)
                {
                    case OrderSignal.goLongLimit:
                        // Define the limit price.
                        limitPrice = priceCalculator.Calculate(data.Value, signalInfo, RngFac);
                        ticket = LimitOrder(symbol, shares, limitPrice, signalInfo.Id.ToString(CultureInfo.InvariantCulture));
                        break;

                    case OrderSignal.goShortLimit:
                        limitPrice = priceCalculator.Calculate(data.Value, signalInfo, RngFac);
                        ticket = LimitOrder(symbol, shares, limitPrice, signalInfo.Id.ToString(CultureInfo.InvariantCulture));
                        break;

                    case OrderSignal.goLong:
                    case OrderSignal.goShort:
                    case OrderSignal.closeLong:
                    case OrderSignal.closeShort:
                    case OrderSignal.revertToLong:
                    case OrderSignal.revertToShort:
                        ticket = MarketOrder(symbol, shares, false, signalInfo.Id.ToString(CultureInfo.InvariantCulture));
                        limitPrice = ticket.AverageFillPrice;
                        break;

                    default:
                        break;
                }
                if (ticket != null)
                {
                    if (ticket.OrderId == 82)
                        System.Diagnostics.Debug.WriteLine("Send order 82");
                    string msg = string.Format("Order {0} Sent {1}, {2}, {3} at {4} bar {5}",
                        ticket.OrderId,
                        signalInfo.Value,
                        signalInfo.Name,
                        shares,
                        limitPrice,
                        barcount);
                    Log(msg);
                }
            }
        }

        private decimal GetBetSize(Symbol symbol, SignalInfo signalInfo)
        {
            // *********************************
            //  ToDo: Kelly Goes here in a custom bet sizer
            //  This implementation uses the same as the original algo
            //    and just refactors it out to a class.
            // *********************************
            IBetSizer allocator = new InstantTrendBetSizer(this);
            return allocator.BetSize(symbol, signalInfo.Price[0].Value, _transactionSize, signalInfo);
        }

        /// <summary>
        /// Estimate number of shares, given a kind of operation.
        /// </summary>
        /// <param name="symbol">The symbol to operate.</param>
        /// <param name="signalInfo">The signalInfo.</param>
        /// <returns>The signed number of shares given the operation.</returns>
        public decimal PositionShares(Symbol symbol, SignalInfo signalInfo)
        {
            decimal quantity = 0;
            //            int operationQuantity;
            decimal targetSize = GetBetSize(symbol, signalInfo);

            switch (signalInfo.Value)
            {
                case OrderSignal.goLongLimit:
                case OrderSignal.goLong:
                    quantity = Math.Min(maxOperationQuantity, targetSize);
                    break;

                case OrderSignal.goShortLimit:
                case OrderSignal.goShort:
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
            }

            return quantity;
        }

        /// <summary>
        /// Sells out all positions at 15:50, and calculates the profits for the day
        ///  emails the transactions for the day to me
        /// </summary>
        /// <param name="data">TradeBars - the data</param>
        /// <param name="signalInfosMinute"></param>
        /// <param name="signal">the current OrderSignal</param>
        /// <returns>false if end of day, true during the day </returns>
        private void SellOutAtEndOfDay(KeyValuePair<Symbol, TradeBar> data, ref OrderSignal signal)
        {
            if (shouldSellOutAtEod)
            {
                #region logging
                if (Time.Hour == 16)
                {
                    NotifyUser();
                    signal = OrderSignal.doNothing;
                }
                #endregion

                if (Time.Hour == 15 && Time.Minute > 45)
                {
                    signal = OrderSignal.doNothing;
                    if (Portfolio[data.Key].IsLong)
                    {
                        signal = OrderSignal.goShort;
                    }
                    if (Portfolio[data.Key].IsShort)
                    {
                        signal = OrderSignal.goLong;
                    }
                }
            }
        }

        private void NotifyUser()
        {
            #region logging

            if (this.Time.Hour == 16)
            {
                var transactionsAsCsv = CsvSerializer.Serialize<OrderTransaction>(",", _transactions, true);
                StringBuilder sb = new StringBuilder();
                var transcount = _transactions.Count();
                foreach (string s in transactionsAsCsv)
                    sb.AppendLine(s);
                string attachment = sb.ToString();

                Notify.Email("nicholasstein@cox.net",
                    string.Format("Transactions For: {0}", Time.ToLongDateString()),
                    string.Format("Todays Date: {0} \nNumber of Transactions: {1}", Time.ToLongDateString(), _transactions.Count()),
                    attachment);

                _transactions = new List<OrderTransaction>();
            }

            #endregion
        }
        /// <summary>
        /// Handles the On end of algorithm 
        /// </summary>
        public override void OnEndOfAlgorithm()
        {
            StringBuilder sb = new StringBuilder();
            //sb.Append(" Symbols: ");
            foreach (var s in Symbols)
            {

                sb.Append(s.Value);
                sb.Append(",");
            }
            string symbolsstring = sb.ToString();
            symbolsstring = symbolsstring.Substring(0, symbolsstring.LastIndexOf(",", System.StringComparison.Ordinal));
            string debugstring =
                string.Format(
                    "\nAlgorithm Name: {0}\n Symbol: {1}\n Ending Portfolio Value: {2} \n lossThreshhold = {3}\n Start Time: {4}\n End Time: {5}",
                    this.GetType().Name, symbolsstring, Portfolio.TotalPortfolioValue, lossThreshhold, startTime,
                    DateTime.Now);
            Log(debugstring);
            #region logging

            NotifyUser();
            #endregion
        }

        #endregion

        #region "Logging Methods"

        #endregion


        /// <summary>
        /// Convenience function which creates an IndicatorDataPoint
        /// </summary>
        /// <param name="time">DateTime - the bar time for the IndicatorDataPoint</param>
        /// <param name="value">decimal - the value for the IndicatorDataPoint</param>
        /// <returns>a new IndicatorDataPoint</returns>
        /// <remarks>I use this function to shorten the a Add call from 
        /// new IndicatorDataPoint(this.Time, value)
        /// Less typing.</remarks>
        private IndicatorDataPoint idp(DateTime time, decimal value)
        {
            return new IndicatorDataPoint(time, value);
        }

    }

}