using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data.Linq;
using System.Diagnostics;
using System.Configuration;

namespace Utilities
{
    /// <summary>
    /// Creates a text log for the given processed in the current executable's location/ProcessLogs folder. Inherit this class to be able to call WriteToLog directly.
    /// </summary>
    public class Logger
    {
        public object LoggerRequestObject;
        public String requestedBy;
        public String ProcessId;
        public String LoggerWorkDir;
        public String LoggerOutputYearDir;
        public String LoggerFtpFromDir;
        public bool TestMode;
        public String TestFTP;
        public String logLocation;
        public String LoggerStagingDir;
        public Boolean LoggerLogOnly;
        public String LoggerPhpDeptFolders;
        public Data.AppNames LoggerPhpConfig;
        public Data.AppNames LoggerPhpArchive;
        public Data.AppNames LoggerExampleDb;
        public String UniqueID;
        /// <summary>
        /// Flips between the remote databases RPT and SIT based on the environmental configuration of PhpDriver
        /// </summary>
        public Data.AppNames LoggerFacetsRemote;


        /// <summary>
        /// Directory that the current executable is sitting in
        /// </summary>
        public String WorkingDirectory;
        public String OwnerGroup;

        public Logger(LaunchRequest requestedLaunch)
        {
            requestedBy = requestedLaunch.requestedBy;
            LoadEnvironment(requestedLaunch.programCode, requestedLaunch.overrideRunMode, false, requestedLaunch.webLaunch);
            this.UniqueID = requestedLaunch.uniqueID;
        }

        /// <summary>
        /// Creates the text log in the aforementioned location, and notes the creation of the log in the text file.
        /// </summary>
        /// <param name="ProcId">The 7 character identifier of the process being run</param>
        /// <param name="setTestMode">Manually sets Test Mode to true, pointing outputs to V: and the test sharepoint site</param>
        /// <param name="logOnly">Allows you to create a log without also creating a working directory for the program/log</param>
        /// <param name="SSISUsername">Used to control SSIS environment</param>
        [Obsolete("This constructor is deprecated, please use the LaunchRequest Constructor instead")]
        public Logger(String ProcId, Boolean setTestMode = false, Boolean logOnly = false, String uniqueID = "")
        {            
            LoadEnvironment(ProcId, setTestMode, logOnly);
            this.UniqueID = uniqueID;
        }

        private void LoadEnvironment(string ProcId, bool setTestMode, bool logOnly, Boolean webRequest = false)
        {
            ProcessId = ProcId;
            WorkingDirectory = Navigation.GetProgramDirectory();

            TestFTP = "TestFTP";

            string env = Environment.GetEnvironmentVariable("DriverEnvironment");


            if (setTestMode || env != "PROD")
            {
                TestMode = true;
            }
            else
            {
                TestMode = false;
            }

            if (TestMode)
            {
                LoggerExampleDb = Data.AppNames.ExampleTest;
            }
            else
            {
                LoggerExampleDb = Data.AppNames.ExampleProd;
            }

            LoggerLogOnly = logOnly;
            LoggerWorkDir = FindWorkDir(ProcId);
            LoggerOutputYearDir = LoggerWorkDir + @"\Output\" + DateTime.Now.Year + @"\";
            LoggerFtpFromDir = LoggerWorkDir + @"\FromFTP\" + DateTime.Now.Year + @"\";
            LoggerStagingDir = LoggerWorkDir + @"\FromFTP\Staging\";


            try
            {
                OwnerGroup = Environment.GetEnvironmentVariable("OwnerGroup");
            }
            catch (Exception ex)
            {
                OwnerGroup = "ALL";
            }
            if (OwnerGroup == null)
            {
                OwnerGroup = "ALL";
            }
        }

        /// <summary>
        /// Writes the given string out to the log, prepended with the date and time
        /// </summary>
        /// <param name="logText">Text to write to log</param>
        public void WriteToLog(String logText, UniversalLogger.LogCategory category = UniversalLogger.LogCategory.INFO)
        {
            try
            {
                UniversalLogger.WriteToLog(this, logText, category: category);
            }
            catch
            {
                //We're handling these errors in UniversalLogger
            }                       
        }

        /// <summary>
        /// This method takes a file/directory that points to a server that doesn't have automatic test/prod flipping built in, and handles test/prod flipping
        /// </summary>
        /// <param name="path">The path for the file or directory</param>
        /// <param name="testingDir">The folder to point to in test mode. This should just be a name or nested name "Testing" or "Testing\\A" etc.. This folder will reside under LoggerWorkDir and be created in this call if it doesn't already exist.</param>
        /// <returns></returns>
        public string networkTestFlip(string path, string testingDir)
        {
            if (TestMode)
            {
                FileSystem.ReportYearDir(LoggerWorkDir + testingDir);
                if (!string.IsNullOrEmpty(Path.GetExtension(path)))
                {
                    return LoggerWorkDir + testingDir + "\\" + Path.GetFileName(path);
                }
                else
                {
                    return LoggerWorkDir + testingDir + "\\";
                }
            }
            else
            {
                return path;
            }
        }

        /// <summary>
        /// On error portion of the Php Interface for implementing Classes, use this if you want 'default' OnError behavior
        /// </summary>
        /// <param name="exc">The exception to handle</param>
        public void OnError(Exception exc)
        {
            this.WriteToLog(exc.Message + Environment.NewLine + exc.StackTrace, UniversalLogger.LogCategory.ERROR);
            if(exc.InnerException != null)
            {
                WriteToLog(exc.InnerException + Environment.NewLine + exc.InnerException.StackTrace, UniversalLogger.LogCategory.ERROR);
            }
            Console.WriteLine(exc.Message + Environment.NewLine + exc.StackTrace);
        }


        public void Finish()
        {
            Console.WriteLine("Program complete");
        }

        public String FindWorkDir(String ProcessId)
        {
            string workDir = "";
            if (TestMode)
            {
                workDir = @"\\locTest\" + ProcessId + @"\";
            }
            else
            {
                workDir = @"\\locProd\" + ProcessId + @"\";
            }

            if (!LoggerLogOnly && ProcessId != "Controller")
            {
                this.LoggerReportYearDir(workDir);
            }

            return workDir;
        }

        public DateTime GetLastRunDate()
        {            
            List<DateTime?> LastRunDates = ExtractFactory.ConnectAndQuery<DateTime?>(this.LoggerExampleDb, LastRunDateQuery(this.ProcessId, OwnerGroup)).ToList();

            if (LastRunDates[0] == null)
            {
                this.WriteToLog("Did not find a run history for this record, using today instead");
                return DateTime.Today;
            }
            else
            {
                return Convert.ToDateTime(LastRunDates[0]);
            }

        }

        /// <summary>
        /// Provides default request object handling for web reports, which is used by the Run Request front-end to generate the dynamic parameters
        /// </summary>
        /// <returns></returns>
        public object GetRequestParametersObject()
        {
            return LoggerRequestObject;
        }


        /// <summary>
        /// Creates the given directory if it doesn't exist with error handling for network connectivity issues
        /// </summary>
        /// <param name="Create">Path to the directory to create</param>
        public void LoggerReportYearDir(String Create)
        {
            try
            {
                FileSystem.ReportYearDir(Create, this);
            }
            catch (IOException IOExc)
            {
                FileSystem.TryRecoverFromNetwork(this, IOExc, Create);
            }
        }

        public String LoggerFirstOfLastMonth(String format)
        {
            DateTime now = DateTime.Now;
            DateTime thisMonth = new DateTime(now.Year, now.Month, 1);
            return thisMonth.AddMonths(-1).ToString(format);
        }

        public String LoggerEndOfLastMonth(String format)
        {
            DateTime now = DateTime.Now;
            DateTime thisMonth = new DateTime(now.Year, now.Month, 1);
            return thisMonth.AddDays(-1).ToString(format);
        }

        public String LoggerFirstOfLastMonth()
        {
            return this.LoggerFirstOfLastMonth("yyyy-MM-dd");
        }

        public String LoggerEndOfLastMonth()
        {
            return this.LoggerEndOfLastMonth("yyyy-MM-dd");
        }

        public class LaunchRequest
        {
            private string _programCode;
            private Boolean _overrideRunMode;
            private string[] _providedArgs;
            public string requestedBy { get; }
            public string uniqueID { get; set; }
            private Boolean _webLaunch;

            public LaunchRequest(String jobId, Boolean overrideTestMode, string[] arguments, string uid = "")
            {
                _programCode = jobId;
                _overrideRunMode = overrideTestMode;
                _providedArgs = arguments;
                requestedBy = null;
                _webLaunch = false;
                uniqueID = uid;
            }

            public LaunchRequest(String jobId, Boolean overrideTestMode, string[] arguments, string requester, string uid = "")
            {
                _programCode = jobId;
                _overrideRunMode = overrideTestMode;
                _providedArgs = arguments;
                requestedBy = requester;
                _webLaunch = false;
                uniqueID = uid;
            }

            public LaunchRequest(String jobId, Boolean overrideTestMode, string[] arguments, string requester, Boolean launchedFromWeb, string uid = "")
            {
                _programCode = jobId;
                _overrideRunMode = overrideTestMode;
                _providedArgs = arguments;
                requestedBy = requester;
                _webLaunch = launchedFromWeb;
                uniqueID = uid;
            }

            public string programCode
            {
                get
                {
                    return _programCode;
                }
            }

            public Boolean webLaunch
            {
                get
                {
                    return _webLaunch;
                }
            }
            public Boolean overrideRunMode
            {
                get
                {
                    return _overrideRunMode;
                }
            }

            public string[] providedArgs
            {
                get
                {
                    return _providedArgs;
                }
            }

        }

        private string LastRunDateQuery(string jobIndex, string ownerGroup)
        {
            string groupTable = "";
            if (ownerGroup != "ALL")
            {
                groupTable = ownerGroup + "_";
            }
            return string.Format(@"SELECT MAX(LastRunDate)
                                    FROM (
                                           SELECT CAST(MAX(LogDateTime) AS Date) AS LastRunDate 
                                           FROM [ULOGGER].[LoggerRecord] with (nolock)
                                           WHERE JobIndex LIKE '{0}%' 
                                           AND LogCategory = 'MILESTONE' 
                                           AND LogContent = 'Program Complete'
                                           UNION
                                           SELECT CAST(MAX(ScheduledStartTime) AS Date) AS LastRunDate 
                                           FROM PHPArchv.dbo.[CONTROLLER_{1}JobDailySchedule_A]
                                           WHERE JobId LIKE '{0}%' 
                                           AND (ActualEndTime < CONVERT(date, GETDATE(), 112) OR ScheduleStatus = 'On Hold')
                                           AND CONVERT(date, ActualEndTime, 112) != '1900-01-01'
                                           AND Environment = 'P'
                                    ) AS RunDate", jobIndex, groupTable);

        }

    }
}
