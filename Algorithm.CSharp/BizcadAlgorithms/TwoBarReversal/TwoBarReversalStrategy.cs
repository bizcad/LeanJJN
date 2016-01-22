using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Data;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp
{
    public class TwoBarReversalStrategy : BaseStrategy
    {
        public TwoBarReversalAlgorithm Algorithm;
        public Symbol symbol;
        public decimal IntraBarTolerance { get; set; }
        public decimal BarSizeTolerance { get; set; }
        //        public TradeBarConsolidator Consolidator15Minute = new TradeBarConsolidator(TimeSpan.FromMinutes(15));
        public TwoBarReversalIndicator TwoBar;
        public TradeBar CurrentTradeBar;
        public bool IsActive = true;
        public int TradeAttempts = 0;
        public OrderStatus Status = OrderStatus.None;
        public decimal StopPrice = 0;
        public decimal TargetPrice = 0;
        public decimal Entryprice = 0;
        public decimal Exitprice = 0;

        public Symbol GetSymbol()
        {
            return symbol;
        }
        
        public TwoBarReversalStrategy(Symbol sym, TwoBarReversalAlgorithm algorithm, decimal barDifferenceTolerance, decimal minBarSize)
        {
            TwoBar = new TwoBarReversalIndicator("tbr" + sym.Value)
            {
                BarDifferenceTolerance = barDifferenceTolerance,
                MinimumBarSize = minBarSize
            };
            Algorithm = algorithm;
            Algorithm.RegisterIndicator(sym, TwoBar, new TimeSpan(0, 15, 0));
            symbol = sym;
            ActualSignal = OrderSignal.doNothing;
        }

            
        /// <summary>
        /// The 1 minute signal.  Check if we have reached a target or stop price in the last bar.
        /// </summary>
        public override void CheckSignal()
        {
            //TwoBar.Update(tradeBar);
            if (TwoBar.IsReady)
            {
                ActualSignal = OrderSignal.doNothing;
                switch (Position)
                {
                    case StockState.noInvested:
                        if (TwoBar.Current.Value == 1m)
                        {
                            ActualSignal = OrderSignal.goLongLimit;
                            StopPrice = CurrentTradeBar.Low - 0.05m;
                            TargetPrice = CurrentTradeBar.High + (Math.Abs(CurrentTradeBar.Close - CurrentTradeBar.Open) * 1.5m);
                        }
                        if (TwoBar.Current.Value == -1m)
                        {
                            ActualSignal = OrderSignal.goShortLimit;
                            StopPrice = CurrentTradeBar.High + 0.05m;
                            TargetPrice = CurrentTradeBar.High + (Math.Abs(CurrentTradeBar.Open - CurrentTradeBar.Close) * 1.5m);
                        }
                        break;

                    case StockState.longPosition:
                        if (TwoBar.BarsWindow[0].Close > TargetPrice)
                            ActualSignal = OrderSignal.closeLong;
                        if (TwoBar.BarsWindow[0].Close < StopPrice)
                            ActualSignal = OrderSignal.closeLong;

                        break;

                    case StockState.shortPosition:
                        if (TwoBar.BarsWindow[0].Close < TargetPrice)
                            ActualSignal = OrderSignal.closeShort;
                        if (TwoBar.BarsWindow[0].Close > StopPrice)
                            ActualSignal = OrderSignal.closeShort;

                        break;


                }
            }
        }

    }
}
