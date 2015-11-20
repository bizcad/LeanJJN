﻿using System;

using QuantConnect.Algorithm.CSharp;
using QuantConnect.Indicators;
using QuantConnect.Orders;

namespace QuantConnect
{
    public class NoStrategy : BaseStrategy
    {
        private string _symbol;
        private Indicator _trend;
        private Indicator _price;
        public OrderTicket limitEntry;
        public OrderTicket limitExit;

        public Indicator Price
        {
            get { return _price; }
        }

        public Indicator Trend
        {
            get { return _trend; }
        }

        public NoStrategy(string Symbol, Indicator Price, Indicator Trend)
        {
            _symbol = Symbol;
            _price = Price;
            _trend = Trend;

            _trend.Updated += (object sender, IndicatorDataPoint updated) =>
                {
                    if (!_trend.IsReady)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("From TREND EVENT HANDLER, Trend is NOT ready");
                        Console.ResetColor();
                    }
                    CheckSignal();
                };
        }


        public override void CheckSignal()
        {
        }

        public void Reset()
        {
            _trend.Reset();
        }
    }
}