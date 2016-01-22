using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Algorithm.Examples;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp
{
    public class TripleMovingAverageStrategy : BaseStrategy
    {
        public MeanReversionAlgorithm Algorithm;
        public Symbol symbol;
        public TradeBar CurrentTradeBar;
        public bool IsActive = true;
        public int TradeAttempts = 0;
        public OrderStatus Status = OrderStatus.None;
        public decimal StopPrice = 0;
        public decimal TargetPrice = 0;
        public decimal Entryprice = 0;
        public decimal Exitprice = 0;

        private Indicator _price;
        
        private InstantaneousTrend trend;
        private ExponentialMovingAverage ema10;
        private SimpleMovingAverage sma10;
        public Symbol GetSymbol()
        {
            return symbol;
        }        

        public TripleMovingAverageStrategy(Symbol sym, Indicator priceIdentity, MeanReversionAlgorithm algorithm, decimal minBarSize, decimal barDifferenceTolerance)
        {
            Algorithm = algorithm;
            symbol = sym;

            trend = new InstantaneousTrend(10).Of(_price);
            ema10 = new ExponentialMovingAverage(10).Of(_price);
            sma10 = new SimpleMovingAverage(10).Of(_price);

            Position = StockState.noInvested;
            EntryPrice = null;
            ActualSignal = OrderSignal.doNothing;
        }

        public override void CheckSignal()
        {
            if (ema10.Current.Value > sma10.Current.Value && trend.Current.Value > ema10.Current.Value
                && ((_price.Current.Value > trend.Current.Value) && !Algorithm.Portfolio[symbol].IsLong))
            {
                ActualSignal = OrderSignal.goLong;
            }
            if (trend.Current.Value < sma10.Current.Value && trend.Current.Value < ema10.Current.Value
                && ((_price.Current.Value < trend.Current.Value) && !Algorithm.Portfolio[symbol].IsShort))
            {
                ActualSignal = OrderSignal.goShort;
            }
            if (Algorithm.Portfolio[symbol].IsLong && trend.Current.Value < ema10.Current.Value)
            {
                ActualSignal = OrderSignal.closeLong;
                
            }
            if (Algorithm.Portfolio[symbol].IsShort && trend.Current.Value > ema10.Current.Value)
            {
                ActualSignal = OrderSignal.closeShort;
            }
        }
    }
}
