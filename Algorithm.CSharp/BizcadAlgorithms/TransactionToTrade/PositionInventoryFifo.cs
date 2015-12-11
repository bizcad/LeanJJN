﻿using System.Collections.Concurrent;
using System.Linq;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp
{
    public class PositionInventoryFifo : IPositionInventory
    {
        public ConcurrentQueue<OrderTransaction> Buys { get; set; }
        public ConcurrentQueue<OrderTransaction> Sells { get; set; }
        public const string Buy = "Buy";
        public const string Sell = "Sell";
        public Symbol Symbol { get; set; }

        public PositionInventoryFifo()
        {
            this.Buys = new ConcurrentQueue<OrderTransaction>();
            this.Sells = new ConcurrentQueue<OrderTransaction>();
        }

        public void Add(OrderTransaction transaction)
        {
            Symbol = transaction.Symbol;
            if (transaction.Direction == OrderDirection.Buy)
            {
                Buys.Enqueue(transaction);
            }
            if (transaction.Direction == OrderDirection.Sell)
            {
                Sells.Enqueue(transaction);
            }
        }

        public OrderTransaction Remove(string queueName)
        {
            OrderTransaction transaction = null;
            if (queueName.Contains(Buy))
                Buys.TryDequeue(out transaction);
            if (queueName.Contains(Sell))
                Sells.TryDequeue(out transaction);
            return transaction;
        }
        public OrderTransaction RemoveBuy()
        {
            OrderTransaction transaction = null;
            Buys.TryDequeue(out transaction);
            return transaction;
        }
        public OrderTransaction RemoveSell()
        {
            OrderTransaction transaction = null;
            Sells.TryDequeue(out transaction);
            return transaction;
        }

        public int BuysCount()
        {
            return Buy.Count();
        }

        public int SellsCount()
        {
            return Sells.Count;
        }

        public Symbol GetSymbol()
        {
            return Symbol;
        }

        public int GetBuysQuantity(Symbol symbol)
        {
            if (BuysCount() > 0)
            {
                return Buys.Sum(b => b.Quantity);
            }
            return 0;
        }
        public int GetSellsQuantity(Symbol symbol)
        {
            if (SellsCount() > 0)
            {
                return Sells.Sum(b => b.Quantity);
            }
            return 0;
        }
    }
}
