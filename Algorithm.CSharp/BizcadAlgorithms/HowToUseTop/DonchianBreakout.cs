using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;

namespace QuantConnect.Algorithm.CSharp.BizcadAlgorithms.HowToUseTop
{
    public class DonchianBreakout : QCAlgorithm
    {
        //Variables
        //int quantity = 0;
        decimal close = 0;
        string symbol = "SPY";
        RollingWindow<decimal> _top = new RollingWindow<decimal>(2);
        RollingWindow<decimal> _bottom = new RollingWindow<decimal>(2);
        decimal top = 0;
        decimal bottom = 0;

        Maximum max;
        Minimum min;

        private Symbol _symbol;

        public override void Initialize()
        {
            SetCash(25000);
            SetStartDate(2010, 1, 1);
            SetEndDate(DateTime.Now.Date.AddDays(-1));
            int period = 5;

            // You want to refer to symbols as a Symbol type.
            var sec = AddSecurity(SecurityType.Equity, symbol, Resolution.Daily);
            _symbol = sec.Symbol;

            max = MAX(symbol, period, Resolution.Daily);
            min = MIN(symbol, period, Resolution.Daily);
        }

        //private DateTime previous;

        public void OnData(TradeBars data)
        {
            if (!max.IsReady)
                return;
            // data.Time is not obsolete you may want to use algorithm.Time instead
            //  or if you want the tme 
            //if (previous.Date == data.Time.Date)
            //    return;

            // The trade bar will give you the Close
            //close = Securities[symbol].Close;
            close = data[_symbol].Close;

            int quantity = Convert.ToInt32(Portfolio.Cash / close);
            int holdings = Portfolio[symbol].Quantity;

            // by declaring top as a var here, you are hiding the global variable
            //var top = max; //NEED TO FIGURE OUT HOW TO LAG THIS
            //var bottom = min; //NEED TO FIGURE OUT HOW TO LAG THIS

            // since you want a decimal value you need to pull out the Current.Value
            //  Current is the latest IndicatorDataPoint
            top = max.Current.Value;
            bottom = min.Current.Value;

            Console.WriteLine("close " + close);
            Console.WriteLine("top " + top);
            Console.WriteLine("bottom " + bottom);
            Console.WriteLine("quantity " + quantity);

            // Since max.Current is already an IDP, you can just use it.
            //_top.Add(new IndicatorDataPoint(Time, top));
            _top.Add(max.Current);
            if (!_top.IsReady) return;
            var historicMax = _top[1];
            Console.WriteLine("max lagged " + historicMax);

            _bottom.Add(min.Current);
            if (!_bottom.IsReady) return;
            var historicMin = _bottom[1];
            Console.WriteLine("min lagged " + historicMin);

            if (Time == new DateTime(2010,6,8))
                System.Diagnostics.Debug.WriteLine("");

            if (close > historicMax && holdings < 1)
            {
                Order(symbol, quantity);
                Debug("Long");
            }

            if (close < historicMin && holdings > 0)
            {
                var ticket = Order(symbol, -quantity);
                Debug("Short");
            }
            Plot("High", historicMax);
            Plot("Low", historicMin);
            Plot("Close", close);
        }
    }
}