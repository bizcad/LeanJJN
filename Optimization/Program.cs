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
        /// <param name="algorithmName">A parameter for the Lean Engine.</param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public decimal Run(string algorithmName, string startDate, string endDate, string symbols)
        {

            Config.Set("start-date", startDate);
            Config.Set("end-date", endDate);
            Config.Set("symbols", symbols);

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

            // Set up a list of algorithms to run
            List<string> algos = new List<string>();
            //algos.Add("InstantTrendAlgorithmOriginal");
            //algos.Add("InstantaneousTrendAlgorithmQC");
            //algos.Add("InstantaneousTrendAlgorithm");
            //algos.Add("MultiSignalAlgorithm");
            //algos.Add("MultiSignalAlgorithmQC");
            //algos.Add("ITrendAlgorithm");
            //algos.Add("ITrendAlgorithmNickVariation");
            algos.Add("MultiSignalAlgorithm");
            //algos.Add("MultiSignalAlgorithmTicketQueue2");

            var daysToRun = GenerateDaysToRun();

            string startDate = "20150519";
            string endDate = "20151106";

            List<string> symbList = new List<string>();
            using (var sr = new StreamReader(@"H:\GoogleFinanceData\NasdaqTop63.csv"))
            {
                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();
                    if (!line.Contains("Symbol") && line.Length > 0)
                    {
                        var x = line.Split(',');
                        symbList.Add(x[0]);
                    }

                }
            }

            RunAlgorithm(algos, daysToRun, symbList);
        }

        private static Dictionary<string, DateRange> GenerateDaysToRun()
        {
            Dictionary<string, DateRange> daysToRun = new Dictionary<string, DateRange>();
            List<DayOfWeek> days = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };

            DateTime sDate = new DateTime(2016, 1, 4);
            DateTime eDate = new DateTime(2016, 1, 8);

            daysToRun.Add("All", new DateRange(dateToString(sDate), dateToString(eDate)));

            // Do each month
            // move sDate to first of next month
            var som = sDate;
            som = new DateTime(som.Year, som.Month + 1, 1);

            var ed = new DateTime(eDate.Year, eDate.Month, 1);
            while (som < ed)
            {
                var eom = new DateTime(som.Year, som.Month + 1, 1);
                eom = eom.AddDays(-1);  //last day of month
                while (!days.Contains(eom.DayOfWeek))
                {
                    eom = eom.AddDays(-1);
                }
                daysToRun.Add("m" + dateToString(som), new DateRange(dateToString(som), dateToString(eom)));
                som = new DateTime(som.Year, som.Month + 1, 1);
                while (!days.Contains(som.DayOfWeek))
                {
                    som = som.AddDays(1);
                }
            }

            som = new DateTime(sDate.Year, sDate.Month + 1, 1);
            while (som.DayOfWeek != DayOfWeek.Monday)
                som = som.AddDays(-1);
            while (som < eDate)
            {
                var eow = som.AddDays(4);
                while (eow.DayOfWeek != DayOfWeek.Friday)
                {
                    eow = eow.AddDays(-1);
                }
                daysToRun.Add("w" + dateToString(som), new DateRange(dateToString(som), dateToString(eow)));

                som = som.AddDays(8);
                while (som.DayOfWeek != DayOfWeek.Monday)
                    som = som.AddDays(-1);
            }


            return daysToRun;
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

        private static double RunAlgorithm(List<string> algos, Dictionary<string, DateRange> daysDictionary, List<string> symbolsList)
        {

            var sum_sharpe = 0.0;
            foreach (string s in algos)
            {
                foreach (string key in daysDictionary.Keys)
                {
                    foreach (string sym in symbolsList)
                    {
                        var val = s;
                        var startDate = daysDictionary[key].startDate;
                        var endDate = daysDictionary[key].endDate;
                        AppDomain ad = null;
                        RunClass rc = CreateRunClassInAppDomain(ref ad);
                        Console.Write("Running algorithm {0} for: {1} to {2}", val, startDate, endDate);
                        try
                        {
                            sum_sharpe += (double)rc.Run(val, startDate, endDate, sym);
                        }
                        catch (Exception e)
                        {
                            Log.Error(e.Message + e.StackTrace);
                        }

                        AppDomain.Unload(ad);
                        // After the Lean Engine has run and is deallocated,
                        // rename my custom mylog.csv file to include the algorithm name.
                        //  mylog.csv is written in the algorithm.  Replace with your custom logs.
                        try
                        {
                            string f = AssemblyLocator.ExecutingDirectory();
                            string sourcefile = f + @"mylog.csv";
                            if (File.Exists(sourcefile))
                            {
                                string destfile = f + string.Format(@"mylog{0}.csv", sym);
                                if (File.Exists(destfile))
                                    File.Delete(destfile);
                                File.Move(sourcefile, destfile);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
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

