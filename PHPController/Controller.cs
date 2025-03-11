using Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;

namespace PHPController
{
    class Controller
    {
        public static Logger ControllerLog;
        public System.Timers.Timer controlTimer;
        public static List<String> QueuedJobs;
        public bool isTestMode = true;
        public bool WorkerIsRunning = false;
        public static string ownerGroup;

        /// <summary>
        /// This controller manages launching and tracking jobs based on a schedule.
        /// </summary>
        /// <param name="procLog">Logger object used for determining current environment and logging on behalf of the calling class.</param>
        public Controller(Logger procLog, bool isTestModeInbound)
        {
            ControllerLog = procLog;
            string env = "TEST";
            
            //Set environment type to either TEST or PROD based on the "DrdiverEnvironment" system environment variable on the server. If not found, we assume the environment is TEST.
            try
            {
                env = Environment.GetEnvironmentVariable("DriverEnvironment");
            }
            catch(Exception ex)
            {
                env = "TEST";
            }
            ControllerLog.WriteToLog("Driver Environment: " + env);

            if (env == "PROD")
            {
                isTestMode = false;
            }

            //Set the Owner of this server based on system environment variable, which will determine which jobs will be run (Sparrow vs PHP). If not found, this will run all jobs.
            try
            {
                ownerGroup = Environment.GetEnvironmentVariable("OwnerGroup");
            }
            catch (Exception ex)
            {
                ownerGroup = "ALL";
            }
            if (ownerGroup == null)
            {
                ownerGroup = "ALL";
            }

            //If the day's schedule isn't already populated, populate it.
            if (!RunSchedulePopulated())
            {
                ControllerLog.WriteToLog("No Schedule found. Loading today schedule.");
                LoadTodaysRunSchedule();               
            }

            QueuedJobs = new List<String>();

            //If the server is in TEST, launch Worker immediately, otherwise Controller will wait until the quarter hour mark to launch Worker.
            if (isTestModeInbound)
            {
                ControllerLog.WriteToLog("In test mode. Launching Worker immediately.");
                LaunchWorker();
            }

            //Get datetime, and set timer for the next quarter hour. Then start event listener for when we hit the quarter hour mark.
            DateTime now = DateTime.Now;
            DateTime interval = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0).AddMinutes(now.Minute % 15 == 0 ? 15 : 15 - now.Minute % 15);
            TimeSpan span = interval - now;
            controlTimer = new System.Timers.Timer((int)(span.TotalSeconds * 1000)); 
            controlTimer.Elapsed += HandleTimer;
            controlTimer.Start();
            ControllerLog.WriteToLog("Timer Initialized");

        }

        /// <summary>
        /// The method fired off by the timer that populates the daily schedule if it hasn't already been done and then launches Worker
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HandleTimer(object sender, ElapsedEventArgs e)
        {
            ControllerLog.WriteToLog("Timer Complete");
            DateTime now = DateTime.Now;
            DateTime interval = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0).AddMinutes(now.Minute % 15 == 0 ? 15 : 15 - now.Minute % 15);
            TimeSpan span = interval - now;
            controlTimer.Interval = ((double)(span.TotalSeconds * 1000) + 1000);

            //If today's schedule hasn't been populated, do so.
            if (!RunSchedulePopulated())
            {
                LoadTodaysRunSchedule();
            }

            LaunchWorker();
        }

        /// <summary>
        /// Launch an instance of the Worker class that will manage application launching and status
        /// </summary>
        private void LaunchWorker()
        {
            try
            {
                ControllerLog.WriteToLog("Trying to start Worker");

                if (!WorkerIsRunning)
                {
                    WorkerIsRunning = true;
                    new Worker(isTestMode, ownerGroup);
                    WorkerIsRunning = false;
                    ControllerLog.WriteToLog("Worker launch complete");
                }
                else
                {
                    ControllerLog.WriteToLog("An instance of Worker is still running. No new Worker instances will be created.", UniversalLogger.LogCategory.ERROR);
                }
            }
            catch (Exception exc)
            {
                if (!isTestMode)
                {
                    string query = @"SELECT Number FROM [dbo].[PhpDeveloperOnCall_C]
                                    WHERE (GETDATE() NOT BETWEEN InactiveStart AND InactiveStop 
										OR InactiveStart IS NULL)
                                    AND Active = 1";

                    List<string> numbers = ExtractFactory.ConnectAndQuery<string>(ControllerLog, ControllerLog.LoggerExampleDb, query).ToList();

                    List<SMS.TwilioResponse> resp = SMS.SendShsSms(ControllerLog, "Worker exited in error, See Logs", numbers, SMS.SHSNumbers._0);
                    foreach (SMS.TwilioResponse r in resp)
                    {
                        ControllerLog.WriteToLog($"Paged {r.to}, at {r.date_created}, last known status: {r.status}. Error code: {r.error_code ?? "null"}, error message: {r.error_message ?? "null"}");
                    }
                }
                //Page the dev services on-call to let them know Worker died.
                ControllerLog.WriteToLog("Worker exited in error: " + exc.ToString(), UniversalLogger.LogCategory.ERROR);
                WorkerIsRunning = false;
            }
        }

        /// <summary>
        /// Populates the core JobDailySchedule table that controls which jobs are to run today, and which jobs are 'Pending Programs'
        /// </summary>
        private void LoadTodaysRunSchedule()
        {
            Scheduling.PopulateDailySchedule(ControllerLog, ownerGroup);
            ControllerLog.WriteToLog("Today's schedule has been loaded.");
        }

        /// <summary>
        /// Checks to see if there are any jobs in today's run schedule
        /// </summary>
        /// <returns>If there are no jobs in today's run schedule</returns>
        private bool RunSchedulePopulated()
        {
            bool runScheduleExists = false;

            try
            {
                //Populate the appropriate daily tracking table based on ownerGroup
                string groupTable = "";
                if (ownerGroup != "ALL")
                {
                    groupTable = ownerGroup + "_";
                }
                string ifLoadedQuery = @"select count(*) from [CONTROLLER_" + groupTable + "JobDailySchedule_A] where RequestedBy = 'LsfUserSchedule' and ScheduledStartTime > '" + DateTime.Today.ToString("yyyy-MM-dd") + "'";
                
                int NumberOfRecsFound = ExtractFactory.ConnectAndQuery<int>(ControllerLog.LoggerPhpArchive, ifLoadedQuery).First();
                ControllerLog.WriteToLog("Number of JobDailySchedule records found: " + NumberOfRecsFound + " in " + ControllerLog.LoggerPhpArchive.ToString() + Environment.NewLine + ifLoadedQuery);
                runScheduleExists = (NumberOfRecsFound != 0);
            }
            catch (Exception exc)
            {
                ControllerLog.WriteToLog("Had a problem checking to make sure today's schedule was populated - it probably did, so let's just run a Worker" + Environment.NewLine + exc.ToString(), UniversalLogger.LogCategory.ERROR);
                runScheduleExists = true;
            }

            return runScheduleExists;
        }        
    }
}
