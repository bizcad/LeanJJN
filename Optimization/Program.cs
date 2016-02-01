/*
 * run Lean multiple times with different inputs
 * 
 * COMMENT OUT THE Console.Read(); line in \Queues\JobQueue.cs
 * so that it will not wait for a key stroke between runs
 * 
 *  public void AcknowledgeJob(AlgorithmNodePacket job)
        {
            // Make the console window pause so we can read log output before exiting and killing the application completely
            Log.Trace("Engine.Main(): Analysis Complete. Press any key to continue.");
            //Console.Read();
        }

 */
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Reflection;
using QuantConnect;
using QuantConnect.Algorithm.CSharp;
using QuantConnect.Lean.Engine;
using QuantConnect.Lean.Engine.Results;
using QuantConnect.Logging;
using QuantConnect.Packets;
using QuantConnect.Configuration;
using QuantConnect.Interfaces;
using QuantConnect.Queues;
using QuantConnect.Messaging;
using QuantConnect.Api;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Lean.Engine.Setup;
using QuantConnect.Lean.Engine.RealTime;
using QuantConnect.Lean.Engine.TransactionHandlers;
using QuantConnect.Lean.Engine.HistoricalData;
using QuantConnect.Util;

namespace Optimization
{

    public class RunClass : MarshalByRefObject
    {
        private const string _collapseMessage = "Unhandled exception breaking past controls and causing collapse of algorithm node. This is likely a memory leak of an external dependency or the underlying OS terminating the LEAN engine.";

        private Api _api;
        private Messaging _notify;
        private JobQueue _jobQueue;
        private IResultHandler _resultshandler;

        private FileSystemDataFeed _dataFeed;
        private ConsoleSetupHandler _setup;
        private BacktestingRealTimeHandler _realTime;
        private ITransactionHandler _transactions;
        private IHistoryProvider _historyProvider;

        private readonly Engine _engine;

        public RunClass()
        {

        }

        /// <summary>
        /// Runs a Lean Engine
        /// </summary>
        /// <param name="algorithmName">A parameter for the Lean Engine which is injected into Config.</param>
        /// <param name="startDate">An algorithm start date which is injected into Config</param>
        /// <param name="endDate">An algorithm end date which is injected into Config</param>
        /// <param name="symbols">A csv list of symbols injected into Config</param>
        /// <param name="size">A decimal value injected into Config</param>
        /// <param name="tolerance">A decimal value injected into Config</param>
        /// <returns></returns>
        public decimal Run(string algorithmName, string startDate, string endDate, string symbols, decimal size, decimal tolerance)
        {
            // Inject the parameters into Config before running the algorithm
            Config.Set("start-date", startDate);
            Config.Set("end-date", endDate);
            Config.Set("symbols", symbols);
            Config.Set("size", size.ToString(CultureInfo.InvariantCulture));
            Config.Set("tolerance", tolerance.ToString(CultureInfo.InvariantCulture));

            // Launch a Lean instance and run algorithmName
            LaunchLean(algorithmName);

            if (_resultshandler != null)
            {
                /************  Comment one of the two following lines to select which ResultHandler to use ***********/
                //var dsktophandler = (OptimizationResultHandler)_resultshandler;
                var dsktophandler = (ConsoleResultHandler)_resultshandler;

                // Return the Sharpe Ratio from Statistics to gauge the performance of the run
                //  Of course it could be any statistic.
                var sharpe_ratio = 0.0m;
                string ratio = "0";
                if (dsktophandler.FinalStatistics.Count > 0)
                {
                    ratio = dsktophandler.FinalStatistics["Sharpe Ratio"];
                    Decimal.TryParse(ratio, out sharpe_ratio);
                }
                return sharpe_ratio;
            }
            return -1.0m;
        }
        /// <summary>
        /// Instantiate a Lean instance and run it.
        /// </summary>
        /// <param name="algorithm"></param>
        private static void LaunchLean(string algorithm)
        {
            // Set the algorithm in Config.  Here is where you can customize Config settings
            Config.Set("algorithm-type-name", algorithm);

            Log.LogHandler =
                Composer.Instance.GetExportedValueByTypeName<ILogHandler>(Config.Get("log-handler", "CompositeLogHandler"));

            //Initialize:
            string mode = "RELEASE";
            var liveMode = Config.GetBool("live-mode");
            Log.DebuggingEnabled = Config.GetBool("debug-mode");

#if DEBUG
            mode = "DEBUG";
#endif

            //Name thread for the profiler:
            var name = Thread.CurrentThread.Name;
            if (name == null)
                Thread.CurrentThread.Name = "Algorithm Analysis Thread";
            Log.Trace("Engine.Main(): LEAN ALGORITHMIC TRADING ENGINE v" + Constants.Version + " Mode: " + mode);
            Log.Trace("Engine.Main(): Started " + DateTime.Now.ToShortTimeString());
            Log.Trace("Engine.Main(): Memory " + OS.ApplicationMemoryUsed + "Mb-App  " + +OS.TotalPhysicalMemoryUsed +
                      "Mb-Used  " + OS.TotalPhysicalMemory + "Mb-Total");

            //Import external libraries specific to physical server location (cloud/local)
            LeanEngineSystemHandlers leanEngineSystemHandlers;
            try
            {
                leanEngineSystemHandlers = LeanEngineSystemHandlers.FromConfiguration(Composer.Instance);
            }
            catch (CompositionException compositionException)
            {
                Log.Error("Engine.Main(): Failed to load library: " + compositionException);
                throw;
            }

            //Setup packeting, queue and controls system: These don't do much locally.
            leanEngineSystemHandlers.Initialize();

            //-> Pull job from QuantConnect job queue, or, pull local build:
            string assemblyPath;
            var job = leanEngineSystemHandlers.JobQueue.NextJob(out assemblyPath);

            if (job == null)
            {
                throw new Exception("Engine.Main(): Job was null.");
            }

            LeanEngineAlgorithmHandlers leanEngineAlgorithmHandlers;
            try
            {
                leanEngineAlgorithmHandlers = LeanEngineAlgorithmHandlers.FromConfiguration(Composer.Instance);
            }
            catch (CompositionException compositionException)
            {
                Log.Error("Engine.Main(): Failed to load library: " + compositionException);
                throw;
            }

            // log the job endpoints
            Log.Trace("JOB HANDLERS: ");
            Log.Trace("         DataFeed:     " + leanEngineAlgorithmHandlers.DataFeed.GetType().FullName);
            Log.Trace("         Setup:        " + leanEngineAlgorithmHandlers.Setup.GetType().FullName);
            Log.Trace("         RealTime:     " + leanEngineAlgorithmHandlers.RealTime.GetType().FullName);
            Log.Trace("         Results:      " + leanEngineAlgorithmHandlers.Results.GetType().FullName);
            Log.Trace("         Transactions: " + leanEngineAlgorithmHandlers.Transactions.GetType().FullName);
            Log.Trace("         History:      " + leanEngineAlgorithmHandlers.HistoryProvider.GetType().FullName);
            Log.Trace("         Commands:     " + leanEngineAlgorithmHandlers.CommandQueue.GetType().FullName);
            if (job is LiveNodePacket) Log.Trace("         Brokerage:    " + ((LiveNodePacket)job).Brokerage);

            // if the job version doesn't match this instance version then we can't process it
            // we also don't want to reprocess redelivered jobs
            if (job.Version != Constants.Version || job.Redelivered)
            {
                Log.Error("Engine.Run(): Job Version: " + job.Version + "  Deployed Version: " + Constants.Version +
                          " Redelivered: " + job.Redelivered);
                //Tiny chance there was an uncontrolled collapse of a server, resulting in an old user task circulating.
                //In this event kill the old algorithm and leave a message so the user can later review.
                leanEngineSystemHandlers.Api.SetAlgorithmStatus(job.AlgorithmId, AlgorithmStatus.RuntimeError, _collapseMessage);
                leanEngineSystemHandlers.Notify.SetChannel(job.Channel);
                leanEngineSystemHandlers.Notify.RuntimeError(job.AlgorithmId, _collapseMessage);
                leanEngineSystemHandlers.JobQueue.AcknowledgeJob(job);
                return;
            }

            try
            {
                /*
                 * This is the guts of Optimization.  It runs a LeanEngine with the various parameters you set earlier
                 */
                var engine = new QuantConnect.Lean.Engine.Engine(leanEngineSystemHandlers, leanEngineAlgorithmHandlers, liveMode);
                engine.Run(job, assemblyPath);
            }
            finally
            {
                //Delete the message from the job queue:
                leanEngineSystemHandlers.JobQueue.AcknowledgeJob(job);
                Log.Trace("Engine.Main(): Packet removed from queue: " + job.AlgorithmId);

                // clean up resources
                leanEngineSystemHandlers.Dispose();
                leanEngineAlgorithmHandlers.Dispose();
                Log.LogHandler.Dispose();
            }
        }
        /* Depricated */
        /// <summary>
        /// Launches a Lean Engine using a parameter
        /// </summary>
        /// <param name="val">The paramater to use when launching lean. </param>
        /// <param name="uslessparam"></param>
        private void LaunchLean(string val, string uslessparam)
        {

            //    Config.Set("environment", "backtesting");
            //    string algorithm = algorithm_name;

            //    // Set the algorithm in Config.  Here is where you can customize Config settings
            //    Config.Set("algorithm-type-name", algorithm);

            //    _jobQueue = new JobQueue();
            //    _notify = new Messaging();
            //    _api = new Api();

            //    /************  Comment one of the two following lines to select which ResultHandler to use ***********/
            //    //_resultshandler = new OptimizationResultHandler();
            //    _resultshandler = new ConsoleResultHandler();

            //    _dataFeed = new FileSystemDataFeed();
            //    _setup = new ConsoleSetupHandler();
            //    _realTime = new BacktestingRealTimeHandler();
            //    _historyProvider = new SubscriptionDataReaderHistoryProvider();
            //    _transactions = new BacktestingTransactionHandler();

            //    // Set the Log.LogHandler to only write to the log.txt file.
            //    //  This setting avoids writing Log messages to the console.
            //    Log.LogHandler = (ILogHandler)new FileLogHandler();
            //    Log.DebuggingEnabled = false;                           // Set this property to true for lots of messages
            //    Log.DebuggingLevel = 1;                                 // A reminder that the default level for Log.Debug message is 1

            //    var systemHandlers = new LeanEngineSystemHandlers(_jobQueue, _api, _notify);
            //    systemHandlers.Initialize();

            //    var algorithmHandlers = new LeanEngineAlgorithmHandlers(_resultshandler, _setup, _dataFeed, _transactions, _realTime, _historyProvider);
            //    string algorithmPath;

            //    AlgorithmNodePacket job = systemHandlers.JobQueue.NextJob(out algorithmPath);
            //    try
            //    {
            //        var _engine = new Engine(systemHandlers, algorithmHandlers, Config.GetBool("live-mode"));
            //        _engine.Run(job, algorithmPath);
            //    }
            //    finally
            //    {
            //        /* The JobQueue.AcknowledgeJob only asks for any key to close the window. 
            //         * We do not want that behavior, so we comment out this line so that multiple Leans will run
            //         * 
            //         * The alternative is to comment out Console.Read(); the line in JobQueue class.
            //         */
            //        //systemHandlers.JobQueue.AcknowledgeJob(job);
            //        Log.Trace("Engine.Main(): Packet removed from queue: " + job.AlgorithmId);

            //        // clean up resources
            //        systemHandlers.Dispose();
            //        algorithmHandlers.Dispose();
            //        Log.LogHandler.Dispose();
            //    }

        }

    }
    class MainClass
    {
        private static int runnumber = 0;
        private static AppDomainSetup _ads;
        private static string _callingDomainName;
        private static string _exeAssembly;
        public static void Main(string[] args)
        {
            //Initialize:
            string mode = "RELEASE";
            var liveMode = Config.GetBool("live-mode");


#if DEBUG
            mode = "DEBUG";
#endif

            Config.Set("live-mode", "false");
            Config.Set("messaging-handler", "QuantConnect.Messaging.Messaging");
            Config.Set("job-queue-handler", "QuantConnect.Queues.JobQueue");
            Config.Set("api-handler", "QuantConnect.Api.Api");

            /************  Comment one of the two following lines to select which ResultHandler to use ***********/
            //Config.Set("result-handler", "QuantConnect.Lean.Engine.Results.OptimizationResultHandler");
            Config.Set("result-handler", "QuantConnect.Lean.Engine.Results.ConsoleResultHandler");

            // Set up an AppDomain
            _ads = SetupAppDomain();

            /* 
             * Set up a list of algorithms to run
             *  Each algorithm will be run in a separate Lean instance
             */
            List<string> algos = new List<string>();
            //algos.Add("InstantTrendAlgorithmOriginal");
            //algos.Add("InstantaneousTrendAlgorithmQC");
            //algos.Add("InstantaneousTrendAlgorithm");
            //algos.Add("MultiSignalAlgorithm");
            //algos.Add("MultiSignalAlgorithmQC");
            //algos.Add("ITrendAlgorithm");
            //algos.Add("ITrendAlgorithmNickVariation");
            //algos.Add("MultiSignalAlgorithm");
            //algos.Add("MeanReversionAlgorithm");
            //algos.Add("MultisymbolAlgorithm");
            //algos.Add("TwoBarReversalAlgorithm");
            //algos.Add("FileLoggingSampleAlgorithm");
            algos.Add("DonchianBreakout");

            /*
             * Generate the days to run the algorithm for
             */
            var daysToRun = GenerateDaysToRun();

            List<string> symbList = new List<string>();

            /*
             * Read a list of symbols from a file.  The algo will be run on each symbol
             */
            //using (var sr = new StreamReader(@"H:\GoogleFinanceData\NasdaqTop63.csv"))
            //{
            //    while (!sr.EndOfStream)
            //    {
            //        string line = sr.ReadLine();
            //        if (!line.Contains("Symbol") && line.Length > 0)
            //        {
            //            var x = line.Split(',');
            //            symbList.Add(x[0]);
            //        }

            //    }
            //}

            // Just add 1 symbol by hand for now
            symbList.Add("SPY");

            /*
             * Uncomment the two for loops to push a size and tolerance value into the algorithm
             */
            decimal size = .19m;
            decimal tolerance = .11m;
            //for (size = .1m; size < .4m; size += .01m)
            //    for (tolerance = .1m; tolerance < .2m; tolerance += .01m)
                    RunAlgorithm(algos, daysToRun, symbList, size, tolerance);
        }

        /// <summary>
        /// Adds date ranges to the list of days to run the algorithm(s)
        /// Each date range in the list will be run by the algorithm(s)
        /// </summary>
        /// <returns></returns>
        private static Dictionary<string, DateRange> GenerateDaysToRun()
        {
            Dictionary<string, DateRange> daysToRun = new Dictionary<string, DateRange>();
            List<DayOfWeek> days = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };

            // Determine the start date and end date of the date range to add to the list of dates to run
            DateTime sDate = new DateTime(2010, 1, 1);
            DateTime eDate = new DateTime(2016, 1, 31);

            // All adds the whole date range to the list of dates to run the algorithm will run 
            daysToRun.Add("All", new DateRange(dateToString(sDate), dateToString(eDate)));

            // Add 1 month intervals to the list of dates to run
            //AddMonthsToList(sDate, eDate, days, daysToRun);

            // Add 1 week interval to the list of dates to run
            //AddWeeksToList(sDate, eDate, daysToRun);

            // Add a day interval to the list of dates to run.  
            // Calling this function will run the algorithm ever day between sDate and eDate
            //AddDaysToList(sDate, eDate, days, daysToRun);

            return daysToRun;
        }

        private static void AddDaysToList(DateTime sDate, DateTime eDate, List<DayOfWeek> days, Dictionary<string, DateRange> daysToRun)
        {
            
            var day = sDate;
            while (day <= eDate)
            {
                if (days.Contains(day.DayOfWeek))
                {
                    // Holidays.Dates are found in Lean\Common\Global as a list
                    if(!USHoliday.Dates.Contains(day))
                        daysToRun.Add("d" + dateToString(day), new DateRange(dateToString(day), dateToString(day)));
                }
                day = day.AddDays(1);
            }

        }

        private static void AddWeeksToList(DateTime sDate, DateTime eDate, Dictionary<string, DateRange> daysToRun)
        {
            #region "Do each week"

            var startOfWeek = sDate;

            while (startOfWeek < eDate)
            {
                var endOfWeek = startOfWeek;
                while (endOfWeek.DayOfWeek != DayOfWeek.Friday)
                {
                    endOfWeek = endOfWeek.AddDays(1);
                }
                if (USHoliday.Dates.Contains(endOfWeek))
                    endOfWeek = endOfWeek.AddDays(-1);
                if (endOfWeek > eDate)
                    endOfWeek = eDate;
                daysToRun.Add("w" + dateToString(startOfWeek) + "-" + dateToString(endOfWeek),
                    new DateRange(dateToString(startOfWeek), dateToString(endOfWeek)));

                startOfWeek = endOfWeek;
                while (startOfWeek.DayOfWeek != DayOfWeek.Monday)
                    startOfWeek = startOfWeek.AddDays(1);
                if (USHoliday.Dates.Contains(startOfWeek))
                {
                    startOfWeek = startOfWeek.AddDays(1);
                }
            }

            #endregion
        }

        private static void AddMonthsToList(DateTime sDate, DateTime eDate, List<DayOfWeek> days, Dictionary<string, DateRange> daysToRun)
        {
            #region "Do each month"

            // move sDate to first of next month
            var startOfMonth = sDate;


            var ed = eDate;
            while (startOfMonth < ed)
            {
                DateTime endOfMonth;
                if (startOfMonth.Month == 12)
                {
                    endOfMonth = new DateTime(startOfMonth.Year, startOfMonth.Month, 31);
                }
                else
                {
                    endOfMonth = new DateTime(startOfMonth.Year, startOfMonth.Month + 1, 1);
                    endOfMonth = endOfMonth.AddDays(-1); //last day of month
                }
                if (endOfMonth > ed)
                    endOfMonth = ed;
                if (USHoliday.Dates.Contains(endOfMonth))
                    endOfMonth = endOfMonth.AddDays(-1);

                while (!days.Contains(endOfMonth.DayOfWeek))
                {
                    endOfMonth = endOfMonth.AddDays(-1);
                }
                daysToRun.Add("m" + dateToString(startOfMonth) + "-" + dateToString(endOfMonth),
                    new DateRange(dateToString(startOfMonth), dateToString(endOfMonth)));
                startOfMonth = endOfMonth;
                while (startOfMonth.Day != 1)
                    startOfMonth = startOfMonth.AddDays(1);
                while (!days.Contains(startOfMonth.DayOfWeek))
                {
                    startOfMonth = startOfMonth.AddDays(1);
                }
                if (USHoliday.Dates.Contains(startOfMonth))
                    startOfMonth = startOfMonth.AddDays(1);

            }

            #endregion


        }

        private static string dateToString(DateTime d)
        {
            string year;
            string month;
            string day;

            year = d.Year.ToString(CultureInfo.InvariantCulture);
            month = d.Month.ToString(CultureInfo.InvariantCulture);
            if (d.Month < 10)
                month = "0" + month;

            day = d.Day.ToString(CultureInfo.InvariantCulture);
            if (d.Day < 10)
                day = "0" + day;
            string strdate = year + month + day;
            return strdate;
        }

        static AppDomainSetup SetupAppDomain()
        {
            _callingDomainName = Thread.GetDomain().FriendlyName;
            //Console.WriteLine(callingDomainName);

            // Get and display the full name of the EXE assembly.
            _exeAssembly = Assembly.GetEntryAssembly().FullName;
            //Console.WriteLine(exeAssembly);

            // Construct and initialize settings for a second AppDomain.
            AppDomainSetup ads = new AppDomainSetup();
            ads.ApplicationBase = AppDomain.CurrentDomain.BaseDirectory;

            ads.DisallowBindingRedirects = false;
            ads.DisallowCodeDownload = true;
            ads.ConfigurationFile =
                AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
            return ads;
        }

        static RunClass CreateRunClassInAppDomain(ref AppDomain ad)
        {
            // Create the second AppDomain.
            var name = Guid.NewGuid().ToString("x");
            ad = AppDomain.CreateDomain(name, null, _ads);

            // Create an instance of MarshalbyRefType in the second AppDomain. 
            // A proxy to the object is returned.
            RunClass rc = (RunClass)ad.CreateInstanceAndUnwrap(_exeAssembly, typeof(RunClass).FullName);
            return rc;
        }

        /// <summary>
        /// Run the alist of algorithms for each date range, for each symbol, for each size and tolerance variation
        /// You can potentially run Lean many times because of the 4 separate loops
        /// </summary>
        /// <param name="algos">A list of algorithms to run</param>
        /// <param name="daysDictionary">A list of date ranges to run</param>
        /// <param name="symbolsList">A list of symbols to run</param>
        /// <param name="size">A size variable that can be read in your algorithm</param>
        /// <param name="tolerance">A tolerance variable that can be read in your algorithm</param>
        /// <returns>the sharp ratio from STATISTICS</returns>
        private static double RunAlgorithm(List<string> algos, Dictionary<string, DateRange> daysDictionary, List<string> symbolsList, decimal size, decimal tolerance)
        {

            var sum_sharpe = 0.0;
            foreach (string algoname in algos)
            {
                foreach (string key in daysDictionary.Keys)
                {
                    foreach (string sym in symbolsList)
                    {
                        var val = algoname;
                        var startDate = daysDictionary[key].startDate;
                        var endDate = daysDictionary[key].endDate;
                        AppDomain ad = null;
                        RunClass rc = CreateRunClassInAppDomain(ref ad);
                        Console.Write("Running algorithm {0} for: {1} to {2}", val, startDate, endDate);
                        try
                        {
                            sum_sharpe += (double)rc.Run(val, startDate, endDate, sym, size, tolerance);
                        }
                        catch (Exception e)
                        {
                            Log.Error(e.Message + e.StackTrace);
                        }

                        AppDomain.Unload(ad);
                        /* After the Lean Engine has run and is deallocated,
                           rename my custom mylog.csv file to include the algorithm name.
                          mylog.csv is written in the algorithm.  Replace with your custom logs.
                         * For example you could save a separate log.txt for each run of the Lean engine
                         */
                        //try
                        //{
                        //    string f = AssemblyLocator.ExecutingDirectory();
                        //    string sourcefile = f + @"mylog.csv";
                        //    if (File.Exists(sourcefile))
                        //    {
                        //        string destfile = f + string.Format(@"mylog{0}{1}.csv",algoname, sym);
                        //        if (File.Exists(destfile))
                        //            File.Delete(destfile);
                        //        File.Move(sourcefile, destfile);
                        //    }
                        //}
                        //catch (Exception e)
                        //{
                        //    Console.WriteLine(e);
                        //}
                        runnumber++;

                    }

                }
            }

            return sum_sharpe;
        }
    }

    class DateRange
    {
        public DateRange(string s, string e)
        {
            startDate = s;
            endDate = e;
        }
        public string startDate;
        public string endDate;
    }

}

