﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace QuantConnect.Algorithm.CSharp
{
    public class TradeBarRecord
    {
        [JsonProperty(PropertyName = "ActualOrder")]
        private List<string> _actualOrder;

        [JsonProperty(PropertyName = "ClosePrice")]
        private List<decimal> _closePrice;

        [JsonProperty(PropertyName = "Decycle")]
        private List<decimal> _decycle;

        [JsonProperty(PropertyName = "DecycleInverseFisher")]
        private List<decimal> _decycleInverseFisher;

        [JsonProperty(PropertyName = "Flag")]
        private List<string> _flag;

        [JsonProperty(PropertyName = "PSAR")]
        private List<decimal> _PSAR;

        [JsonProperty(PropertyName = "SmoothedSeries")]
        private List<decimal> _smoothedSeries;

        [JsonProperty(PropertyName = "Symbol")]
        private List<string> _symbol;

        [JsonProperty(PropertyName = "date")]
        private List<DateTime> _time;

        public TradeBarRecord()
        {
            _time = new List<DateTime>();
            _symbol = new List<string>();
            _closePrice = new List<decimal>();
            _decycle = new List<decimal>();
            _decycleInverseFisher = new List<decimal>();
            _smoothedSeries = new List<decimal>();
            _PSAR = new List<decimal>();
            _flag = new List<string>();
            _actualOrder = new List<string>();
        }

        public void Add(DateTime ObsTime, string Symbol, decimal ClosePrice, decimal Decycle, decimal DecycleInverseFisher,
            decimal SmoothedSeries, decimal PSAR, string Flag, string ActualOrder)
        {
            _time.Add(ObsTime);
            _symbol.Add(Symbol);
            _closePrice.Add(ClosePrice);
            _decycle.Add(Decycle);
            _decycleInverseFisher.Add(DecycleInverseFisher);
            _smoothedSeries.Add(SmoothedSeries);
            _PSAR.Add(PSAR);
            _flag.Add(Flag);
            _actualOrder.Add(ActualOrder);
        }
    }
}