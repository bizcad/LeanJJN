using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using QuantConnect.Indicators;

namespace QuantConnect.Algorithm.CSharp
{
    public class TradeBarRecord
    {
        [JsonProperty(PropertyName = "date")]
        DateTime _time;

        [JsonProperty(PropertyName="Symbol")]
        string _symbol;

        [JsonProperty(PropertyName="ClosePrice")]
        decimal _closePrice;

        [JsonProperty(PropertyName = "Decycle")]
        decimal _decycle;

        [JsonProperty(PropertyName = "SmoothedSeries")]
        decimal _smoothedSeries;

        [JsonProperty(PropertyName = "DecycleInverseFisher")]
        decimal _decycleInverseFisher;

        [JsonProperty(PropertyName = "PSAR")]
        decimal _PSAR;

        [JsonProperty(PropertyName = "Flag")]
        string _flag;

        [JsonProperty(PropertyName = "ActualOrder")]
        string _actualOrder;

        public TradeBarRecord(DateTime ObsTime, string Symbol, decimal ClosePrice, decimal Decycle, decimal DecycleInverseFisher,
            decimal SmoothedSeries, decimal PSAR, string Flag, string ActualOrder)
        {
            _time = ObsTime;
            _symbol = Symbol;
            _closePrice = ClosePrice;
            _decycle = Decycle;
            _decycleInverseFisher = DecycleInverseFisher;
            _smoothedSeries = SmoothedSeries;
            _PSAR = PSAR;
            _flag = Flag;
            _actualOrder = ActualOrder;
        }
    }
}
