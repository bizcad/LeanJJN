using QuantConnect.Indicators;

namespace QuantConnect.Algorithm.CSharp
{
    public class LaguerreStrategy : BaseStrategy
    {
        private Indicator _price;
        private decimal _tolerance;
        public LaguerreIndicator Laguerre { get; private set; }
        public bool IsReady { get { return Laguerre.IsReady; } }

        public LaguerreStrategy(Indicator price, decimal gamma = 0.8m, decimal tolerance = 0.001m)
        {
            _price = price;
            _tolerance = tolerance;
            Laguerre = new LaguerreIndicator(gamma).Of(price);
            Laguerre.Updated += (object sender, IndicatorDataPoint updated) =>
                {
                    if (IsReady) CheckSignal();
                };
            ActualSignal = OrderSignal.doNothing;
            Position = StockState.noInvested;
        }

        public void Reset()
        {
            ActualSignal = OrderSignal.doNothing;
            Position = StockState.noInvested;
            Laguerre.Reset();
        }

        public override void CheckSignal()
        {
            var actualSignal = OrderSignal.doNothing;

            bool LaguerreCrossOverFIR = Laguerre.Laguerre[1].Value < Laguerre.FIR[1].Value
                                     && Laguerre.Laguerre[0].Value > Laguerre.FIR[0].Value;

            bool LaguerreCrossUnderFIR = Laguerre.Laguerre[1].Value > Laguerre.FIR[1].Value
                                      && Laguerre.Laguerre[0].Value < Laguerre.FIR[0].Value;

            switch (Position)
            {
                case StockState.shortPosition:
                    if (LaguerreCrossUnderFIR) actualSignal = OrderSignal.closeShort;
                    break;

                case StockState.longPosition:
                    if (LaguerreCrossOverFIR) actualSignal = OrderSignal.closeLong;
                    break;

                case StockState.noInvested:
                    if (LaguerreCrossOverFIR) actualSignal = OrderSignal.goShort;
                    if (LaguerreCrossUnderFIR) actualSignal = OrderSignal.goLong;
                    break;

                default:
                    break;
            }

            ActualSignal = actualSignal;
        }
    }
}