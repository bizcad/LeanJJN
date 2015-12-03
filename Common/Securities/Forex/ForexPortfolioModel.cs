﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); 
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using QuantConnect.Logging;
using QuantConnect.Orders;

namespace QuantConnect.Securities.Forex
{
    /// <summary>
    /// The equity portfolio model implementation is the same as the default
    /// </summary>
    public class ForexPortfolioModel : SecurityPortfolioModel
    {
        /// <summary>
        /// Performs application of an OrderEvent to the portfolio
        /// </summary>
        /// <param name="portfolio">The algorithm's portfolio</param>
        /// <param name="security">The fill's security</param>
        /// <param name="fill">The order event fill object to be applied</param>
        public override void ProcessFill(SecurityPortfolioManager portfolio, Security security, OrderEvent fill)
        {
            // split the symbol into base and quote currencies

            string baseCurrency;
            string quoteCurrency;
            Forex.DecomposeCurrencyPair(security.Symbol.Value, out baseCurrency, out quoteCurrency);

            // e.g. EUR GBP

            // EUR
            var baseCash = portfolio.CashBook[baseCurrency];
            // GBP
            var quoteCash = portfolio.CashBook[quoteCurrency];

            //Get the required information from the vehicle this order will affect
            var closedPosition = false;
            var isLong = security.Holdings.IsLong;
            var isShort = security.Holdings.IsShort;

            //Make local decimals to avoid any rounding errors from int multiplication
            var averageHoldingsPrice = security.Holdings.AveragePrice;
            var quantityHoldings = (decimal)security.Holdings.Quantity;
            var absoluteHoldingsQuantity = security.Holdings.AbsoluteQuantity;

            var lastTradeProfit = 0m;

            try
            {
                //Update the Vehicle approximate total sales volume.
                var saleValueInQuoteCurrency = fill.FillPrice * Convert.ToDecimal(fill.AbsoluteFillQuantity);
                var saleValue = saleValueInQuoteCurrency*quoteCash.ConversionRate;
                security.Holdings.AddNewSale(saleValue);

                //Get the Fee for this Order - Update the Portfolio Cash Balance: Remove Transaction Fees.
                var order = new MarketOrder(security.Symbol, fill.FillQuantity, security.LocalTime.ConvertToUtc(security.Exchange.TimeZone), type: security.Type){Price = fill.FillPrice, Status = OrderStatus.Filled};
                var feeThisOrder = Math.Abs(security.TransactionModel.GetOrderFee(security, order));
                security.Holdings.AddNewFee(feeThisOrder);
                portfolio.CashBook[CashBook.AccountCurrency].AddAmount(-feeThisOrder);

                // Apply the funds using the current settlement model
                security.SettlementModel.ApplyFunds(portfolio, security, fill.UtcTime, baseCurrency, fill.FillQuantity);
                security.SettlementModel.ApplyFunds(portfolio, security, fill.UtcTime, quoteCurrency, -fill.FillQuantity * fill.FillPrice);

                //Calculate & Update the Last Trade Profit;
                if (isLong && fill.Direction == OrderDirection.Sell)
                {
                    //Closing up a long position
                    if (quantityHoldings >= fill.AbsoluteFillQuantity)
                    {
                        //Closing up towards Zero -- this is in the quote currency
                        lastTradeProfit = (fill.FillPrice - averageHoldingsPrice) * fill.AbsoluteFillQuantity;
                    }
                    else
                    {
                        //Closing up to Neg/Short Position (selling more than we have) - Only calc profit on the stock we have to sell.
                        lastTradeProfit = (fill.FillPrice - averageHoldingsPrice) * quantityHoldings;
                    }
                    closedPosition = true;
                }
                else if (isShort && fill.Direction == OrderDirection.Buy)
                {
                    //Closing up a short position.
                    if (absoluteHoldingsQuantity >= fill.FillQuantity)
                    {
                        //Reducing the stock we have, and enough stock on hand to process order.
                        lastTradeProfit = (averageHoldingsPrice - fill.FillPrice) * fill.AbsoluteFillQuantity;
                    }
                    else
                    {
                        //Increasing stock holdings, short to positive through zero, but only calc profit on stock we Buy.
                        lastTradeProfit = (averageHoldingsPrice - fill.FillPrice) * absoluteHoldingsQuantity;
                    }
                    closedPosition = true;
                }


                if (closedPosition)
                {
                    // convert the compute profit into the account currency
                    lastTradeProfit *= quoteCash.ConversionRate;

                    //Update Vehicle Profit Tracking:
                    security.Holdings.AddNewProfit(lastTradeProfit);
                    security.Holdings.SetLastTradeProfit(lastTradeProfit);
                    portfolio.AddTransactionRecord(security.LocalTime.ConvertToUtc(security.Exchange.TimeZone), lastTradeProfit - 2 * feeThisOrder);
                }


                //UPDATE HOLDINGS QUANTITY, AVG PRICE:
                //Currently NO holdings. The order is ALL our holdings.
                if (quantityHoldings == 0)
                {
                    //First transaction just subtract order from cash and set our holdings:
                    averageHoldingsPrice = fill.FillPrice;
                    quantityHoldings = fill.FillQuantity;
                }
                else if (isLong)
                {
                    //If we're currently LONG on the stock.
                    switch (fill.Direction)
                    {
                        case OrderDirection.Buy:
                            //Update the Holding Average Price: Total Value / Total Quantity:
                            averageHoldingsPrice = ((averageHoldingsPrice * quantityHoldings) + (fill.FillQuantity * fill.FillPrice)) / (quantityHoldings + fill.FillQuantity);
                            //Add the new quantity:
                            quantityHoldings += fill.FillQuantity;
                            break;

                        case OrderDirection.Sell:
                            quantityHoldings += fill.FillQuantity; //+ a short = a subtraction
                            if (quantityHoldings < 0)
                            {
                                //If we've now passed through zero from selling stock: new avg price:
                                averageHoldingsPrice = fill.FillPrice;
                            }
                            else if (quantityHoldings == 0)
                            {
                                averageHoldingsPrice = 0;
                            }
                            break;
                    }
                }
                else if (isShort)
                {
                    //We're currently SHORTING the stock: What is the new position now?
                    switch (fill.Direction)
                    {
                        case OrderDirection.Buy:
                            //Buying when we're shorting moves to close position:
                            quantityHoldings += fill.FillQuantity;
                            if (quantityHoldings > 0)
                            {
                                //If we were short but passed through zero, new average price is what we paid. The short position was closed.
                                averageHoldingsPrice = fill.FillPrice;
                            }
                            else if (quantityHoldings == 0)
                            {
                                averageHoldingsPrice = 0;
                            }
                            break;

                        case OrderDirection.Sell:
                            //We are increasing a Short position:
                            //E.g.  -100 @ $5, adding -100 @ $10: Avg: $7.5
                            //      dAvg = (-500 + -1000) / -200 = 7.5
                            averageHoldingsPrice = ((averageHoldingsPrice * quantityHoldings) + (fill.FillQuantity * fill.FillPrice)) / (quantityHoldings + fill.FillQuantity);
                            quantityHoldings += fill.FillQuantity;
                            break;
                    }
                }
            }
            catch (Exception err)
            {
                Log.Error(err);
            }

            //Set the results back to the vehicle.
            security.Holdings.SetHoldings(averageHoldingsPrice, Convert.ToInt32(quantityHoldings));
        }
    }
}
