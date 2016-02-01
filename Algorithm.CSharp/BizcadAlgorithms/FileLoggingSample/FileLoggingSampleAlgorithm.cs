/*
 * Notice this algorithm references QuantConnect.Logging which is unfortunately not legal in back test
 * So it is useful only on your local machine which runs a clone of Lean.
 * 
 * To get it to compile on QuantConnect, I have commented out all references to the logging functions
 * Copy this algorithm into your QuantConnect.Algorithm.CSharp folder in your QuantConnect.Lean project
 *  and then uncomment the using, the declaration, the initialization and the call to mylog on OnData.
 *  
 * You also need to add a class called CustomFileLogHandler.cs in your QuantConnect.Logging project in
 *  your QuantConnect.Lean solution.  That file is also a part of this Project, but it is all commented
 *  out because QC does not allow you to reference System.IO.  Add the file to the Logging project and
 *  uncomment the file's contents.
 *  
 */
using System;
using System.Collections.Generic;
using QuantConnect.Algorithm;
using QuantConnect.Data.Market;
// Uncomment this using statement
using QuantConnect.Logging;
using QuantConnect.Util;

namespace QuantConnect
{
    /// <summary>
    /// Sample for adding custom logging to your Local copy of Lean 
    /// </summary>
    public class FileLoggingSampleAlgorithm : QCAlgorithm
    {
        #region "Custom Logging"
        // Uncomment this declaration
        private ILogHandler mylog;
        #endregion
        /// <summary>
        /// Usual Initialize summary
        /// </summary>
        public override void Initialize()
        {
            //Initialize dates
            SetStartDate(new DateTime(2016, 1, 11));
            SetEndDate(new DateTime(2016, 1, 14));
            SetCash(50000);

            AddSecurity(SecurityType.Equity, "SPY", Resolution.Minute);

            // Uncomment this instantition
            mylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("CustomFileLogHandler");

        }
        /// <summary>
        /// Usuall OnData summary
        /// </summary>
        /// <param name="data">Trade bars for the event handler</param>
        public void OnData(TradeBars data)
        {
            foreach (KeyValuePair<Symbol, TradeBar> kvp in data)
            {
                // Add your indicator values to the logmsg
                string logmsg = string.Format(
                    "{0},{1},{2},{3},{4},{5},{6}",
                    kvp.Value.EndTime.ToShortTimeString(),
                    kvp.Value.Symbol.Value,
                    Math.Round(kvp.Value.Open, 4),
                    Math.Round(kvp.Value.High, 4),
                    Math.Round(kvp.Value.Low, 4),
                    Math.Round(kvp.Value.Close, 4),
                    kvp.Value.Volume
                    );
                // Uncomment this call
                mylog.Debug(logmsg);
            }
        }
    }
}
