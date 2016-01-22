using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Algorithm.CSharp;
using QuantConnect.Configuration;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.Examples
{
    /// <summary>
    /// A aelatively simple mean revesion algorithm using an EMA and SMA
    /// and trading when the difference between the two cross over the standard deviation
    /// 
    /// </summary>
    public class MeanReversionAlgorithm : QCAlgorithm
    {
        private DateTime startTime = DateTime.Now;
        private DateTime _startDate = new DateTime(2015, 10, 27);
        private DateTime _endDate = new DateTime(2015, 11, 27);
        private const decimal PortfolioAmount = 25000;
        /*********** Control Panel **********/
        private bool shouldSellOutAtEod = true;         // Should the algo sell out at 15 min before EoD.
        private int maxOperationQuantity = 500;         // Maximum shares per operation.
        private decimal RngFac = 0.35m;                 // Percentage of the bar range used to estimate limit prices.
        private decimal size = 0.19m;
        private decimal tolerance = .05m;
        /************************************/

        private string[] _symbolarray = { "TLT" };
        private List<string> _symbols = new List<string>();


        public List<TripleMovingAverageStrategy> Strategies = new List<TripleMovingAverageStrategy>();
        private int barcount = 0;

        private ILogHandler mylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("CustomFileLogHandler");
        private string ondataheader =
            @"Symbol, Time,BarCount,Volume,,Time, Open,High,Low,Close,Target,Stop, Attempts, , , ,Entry,Exit, IsActive,Unrealized, , Owned, TradeNet, Portfolio";

        private List<OrderTransaction> _transactions;
        private OrderTransactionProcessor _orderTransactionProcessor = new OrderTransactionProcessor();



        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        /// <seealso cref="QCAlgorithm.SetStartDate(System.DateTime)"/>
        /// <seealso cref="QCAlgorithm.SetEndDate(System.DateTime)"/>
        /// <seealso cref="QCAlgorithm.SetCash(decimal)"/>
        public override void Initialize()
        {
            //mylog.Debug(transheader);
            mylog.Debug("Mean Reversion Algorithm");
            mylog.Debug(ondataheader);
            //dailylog.Debug("Mean Reversion Algorithm");
            //dailylog.Debug(dailyheader);

            //Initialize dates
            var sd = Config.Get("start-date");
            var ed = Config.Get("end-date");

            _startDate = new DateTime(Convert.ToInt32(sd.Substring(0, 4)), Convert.ToInt32(sd.Substring(4, 2)), Convert.ToInt32(sd.Substring(6, 2)));
            _endDate = new DateTime(Convert.ToInt32(ed.Substring(0, 4)), Convert.ToInt32(ed.Substring(4, 2)), Convert.ToInt32(ed.Substring(6, 2)));

            _symbolarray = Config.Get("symbols").Split(',');


            //Initialize dates
            SetStartDate(_startDate);
            SetEndDate(_endDate);
            SetCash(22000);

            //Add as many securities as you like. All the data will be passed into the event handler:
            foreach (string t in _symbolarray)
            {
                _symbols.Add(t);
            }
            foreach (var s in _symbols)
            {
                AddSecurity(SecurityType.Equity, s, Resolution.Minute);
            }
            foreach (Symbol symbol in Portfolio.Keys)
            {
                var priceIdentity = Identity(symbol, selector: Field.Close);
                Strategies.Add(new TripleMovingAverageStrategy(symbol, priceIdentity, this, size, tolerance));
            }

        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">TradeBars IDictionary object with your stock data</param>
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
                        barcount,
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
                        "",
                        "",
                        "",
                        "",
                        "",
                        "",
                        "",
                        "",
                        //strategy.TwoBar.IsReady ? strategy.TwoBar.BarsWindow[1].Close.ToString(CultureInfo.InvariantCulture) : "",
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
            Log(string.Format("MinBarSize: {0}", size));
            Log(string.Format("BarDifferenceTolerance: {0}", tolerance));

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

        /// <summary>
        /// Factory function which creates an IndicatorDataPoint
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

        private StockState GetPosition(Symbol symbol)
        {
            int position = Portfolio[symbol].Quantity;
            if (position > 0) return StockState.longPosition;
            if (position < 0) return StockState.shortPosition;
            return StockState.noInvested;
        }
        #region "Methods"
        private void SellOutAtEndOfDay(KeyValuePair<Symbol, TradeBar> data, ref OrderSignal signal)
        {

            if (shouldSellOutAtEod)
            {
                //signal = OrderSignal.doNothing;      // Just in case a signal slipped through in the last minute.
                #region logging
                if (Time.Hour == 16)
                {
                    #region logging
                    signal = OrderSignal.doNothing;
 
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
        /// <summary>
        /// Handles the case where an order is partially filled.
        /// </summary>
        /// <param name="data">The current data</param>
        /// <param name="strategy"></param>
        private void HandlePartiallyFilled(KeyValuePair<Symbol, TradeBar> data, BaseStrategy strategy)
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
                    //nEntryPrice = Portfolio[orderEvent.Symbol].HoldStock ? Portfolio[orderEvent.Symbol].AveragePrice : 0;
                    IEnumerable<OrderTicket> tickets = Transactions.GetOrderTickets(t => t.OrderId == orderEvent.OrderId);
                    if (tickets != null)
                    {
                        foreach (OrderTicket ticket in tickets)
                        {
                            #region "save the ticket as a OrderTransacton"
                            OrderTransactionFactory transactionFactory = new OrderTransactionFactory((QCAlgorithm)this);
                            OrderTransaction t = transactionFactory.Create(orderEvent, ticket, false);
                            _transactions.Add(t);
                            _orderTransactionProcessor.ProcessTransaction(t);
                            #endregion

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

    }
}
