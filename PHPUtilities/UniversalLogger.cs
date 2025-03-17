using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Utilities
{
    public static class UniversalLogger
    {
        private static readonly string ProdUrl = "https://..org/api/DataStation/api/";
        private static readonly string TestUrl = "https://..org/api/DataStation/api/";
        //private static readonly string TestUrl = "http://http://localhost:56494/";
        private static bool EmergencyLoggingActivated = false;
        private static string EmergencyLogLocation = Path.GetTempFileName();
        private static FileStream EmergencyLog = File.Open(EmergencyLogLocation, FileMode.Append);
        private static Stopwatch stopwatch = new Stopwatch();
        private static long logFlushTimeout = 0;
        private static byte[] tmp;

        public enum LogCategory
        {
            AUDIT,
            ERROR,
            INFO,
            MILESTONE,
            WARNING
        }

        public enum LoggerMode
        {
            DEFAULT,    // Inherits calling proc logger mode.
            TEST,       // Forces logger to log to test database.
            PROD        // Forces logger to log to prod database.
        }

        /// <summary>
        /// A basic method to allow users to write content to the Universal Logger database. In it's most basic form, all you need to provide is the Logger (program),
        /// the Job Index and some content to log and the method will call the API to log to the database.
        /// </summary>
        /// <param name="procLog">A class that extends the Logger class (all jobs do this, so pass itself)</param>
        /// <param name="jobIndex">The Job Index that identifies the program calling the logger.</param>
        /// <param name="logContent">The content you want to log.</param>
        /// <param name="loggedByUser">By default, this will use the currently logged in user. Can override with another user.</param>
        /// <param name="category">Default is an informational log type. Can specify other log types with LogCategory enum.</param>
        /// <param name="logMode">Default uses Logger.TestMode - can override with TEST or PROD mode to force the use of test/prod logging database.</param>
        /// <param name="isSensitive">All logs are sent to the IT Security logging infrastructure as well. Only set this to true if the log contains sensitive (patient) information.</param>
        public static void WriteToLog(Logger procLog, String logContent, String loggedByUser = "", LogCategory category = LogCategory.INFO,
                                        LoggerMode logMode = LoggerMode.DEFAULT, bool isSensitive = false)
        {
            // Get the user calling the logging framework.
            if (String.IsNullOrWhiteSpace(loggedByUser))
            {
                loggedByUser = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            }

            // Set Test/Prod mode.
            if (logMode == LoggerMode.DEFAULT)
            {
                logMode = procLog.TestMode ? LoggerMode.TEST : LoggerMode.PROD;
            }

            // Build Log object.
            LogEntryModel logEntry = new LogEntryModel()
            {
                JobIndex = procLog.ProcessId.Substring(0, Math.Min(50, procLog.ProcessId.Length)),
                LogDateTime = DateTime.Now,
                LoggedByUser = loggedByUser,
                LogContent = logContent,
                LogCategory = category.ToString(),
                UID = procLog.UniqueID
            };

            if (logMode == LoggerMode.TEST && !procLog.ProcessId.StartsWith("WEB"))
            {
                Console.WriteLine($"---{logEntry.LogDateTime} {logEntry.LogCategory}: {logEntry.LogContent}"); //Should we censor on isSensitive? I imagine output just goes to stdout, which will be lost on reboot at the latest. -James
            }

            // Serialize the payload
            String payload = JsonConvert.SerializeObject(logEntry);

            // Call the API
            String endpointUrl = (logMode == LoggerMode.TEST) ? TestUrl : ProdUrl;
            endpointUrl += "ULogger/WriteToLog";

            if (isSensitive)
            {
                endpointUrl += "?sensitive=true";
            }

            String result = "";
            int tryNumber = 1;
            do
            {
                try
                {
                    if (EmergencyLoggingActivated)
                    {
                        tmp = new UTF8Encoding(true).GetBytes(isSensitive ? "!" : "" + payload + Environment.NewLine);
                        EmergencyLog.Write(tmp, 0, tmp.Length);
                        if (stopwatch.ElapsedMilliseconds - logFlushTimeout > 100)
                        {
                            logFlushTimeout = stopwatch.ElapsedMilliseconds;
                            EmergencyLog.Flush(); //Outout file seems to never be written when driver exits - I assumed GC would dispose it and flush appropriately but it seems not. Trying this?
                        }
                        tryNumber = 0;
                    }
                    else
                    {
                        result = CallApi<String>(endpointUrl, "POST", procLog, payload);
                        tryNumber = 0;
                    }
                }
                catch (Exception ex)
                {
                    if (tryNumber < 4)
                    {
                        tryNumber++;
                        continue;
                    }
                    else
                    {
                        try
                        {
                            using (System.IO.StreamWriter str = File.AppendText(procLog.logLocation))
                            {
                                str.WriteLine(DateTime.Now.ToString() + " " + logContent);
                            }
                        }
                        catch
                        {
                            Random random = new Random();
                            System.Threading.Thread.Sleep(2000 + random.Next(0, 1000)); //since the above error happens if to threads try to write to the same log at the same time
                                                                                        //, let's try breaking them apart by waiting between 2 and 3 seconds
                            try
                            {
                                using (System.IO.StreamWriter str = File.AppendText(procLog.logLocation))
                                {
                                    str.WriteLine(DateTime.Now.ToString() + " " + logContent);
                                }
                            }
                            catch
                            {
                                //If this failes too, you've got much bigger fish to fry

                                //You were correct, mystery past dev. -James
                                EmergencyLoggingActivated = true;
                                stopwatch.Start();

                                if (!procLog.TestMode)
                                {
                                    string query = @"SELECT Number FROM [dbo].[PhpDeveloperOnCall_C]
                                    WHERE (GETDATE() NOT BETWEEN InactiveStart AND InactiveStop 
										OR InactiveStart IS NULL)
                                    AND Active = 1";

                                    List<string> numbers = ExtractFactory.ConnectAndQuery<string>(procLog, Data.AppNames.ExampleProd, query).ToList();

                                    //todo: send page
                                }
                            }

                        }
                    }
                }
            } while (tryNumber > 0 && tryNumber < 4);


            // Watch for errors. Logger should only return a result when something goes wrong.
            if (!String.IsNullOrWhiteSpace(result))
            {
                throw new ULoggerException(result);
            }
        }

        /// <summary>
        /// A simplified version of the WriteToLog method that simply logs a successful run of a program as a milestone log. This allows us to have a unified approach to
        /// tracking successful job runs.
        /// </summary>
        /// <param name="procLog">A class that extends the Logger class (all jobs do this, so pass itself)</param>
        /// <param name="loggedByUser">By default, this will use the currently logged in user. Can override with another user.</param>
        /// <param name="runDate">By default this will use today's date. However, the option to override exists for use with recovery runs. If a recovery run executes, you should 
        ///                       use the override for this so we can track it.</param>
        public static void WriteToLogProgramComplete(Logger procLog, String loggedByUser = "", DateTime runDate = new DateTime())
        {
            runDate = (runDate == new DateTime()) ? DateTime.Today : runDate;

            WriteToLog(procLog, "Program Complete", loggedByUser: loggedByUser, category: LogCategory.MILESTONE);
        }
        /// <summary>
        /// A simplified version of the WriteToLog method that simply logs a successful run of a program as a milestone log. This allows us to have a unified approach to
        /// tracking job runs starts.
        /// </summary>
        /// <param name="procLog">A class that extends the Logger class (all jobs do this, so pass itself)</param>
        /// <param name="loggedByUser">By default, this will use the currently logged in user. Can override with another user.</param>
        /// <param name="runDate">By default this will use today's date. However, the option to override exists for use with recovery runs. If a recovery run executes, you should 
        ///                       use the override for this so we can track it.</param>
        public static void WriteToLogProgramStart(Logger procLog, String loggedByUser = "", DateTime runDate = new DateTime())
        {
            runDate = (runDate == new DateTime()) ? DateTime.Today : runDate;

            WriteToLog(procLog, "Program Started", loggedByUser: loggedByUser, category: LogCategory.MILESTONE);
        }

        /// <summary>
        /// This method queries the Universal Logger database to check whether a previous execution of the program completed successfully.
        /// </summary>
        /// <param name="procLog">A class that extends the Logger class (all jobs do this, so pass itself)</param>
        /// <param name="jobIndex">The Job Index that identifies the program calling the logger.</param>
        /// <param name="runDate">By default this will look for a successful run marked with yesterday's date. Can override to look at other dates as well.</param>
        /// <param name="successContentOverride">By default, this method will look for LogContent containing the default success message 'yyyy-MM-dd Completed Success', but
        ///                                     can override to look for other LogContent messages.</param>
        /// <returns></returns>
        public static bool PreviousRunWasSuccessful(Logger procLog, String jobIndex, DateTime runDate = new DateTime(), String successContentOverride = "")
        {
            String getLastLogEntry = @"SELECT * FROM ULOGGER.LoggerRecord with (nolock) WHERE JobIndex = '{0}' AND LogCategory = 'MILESTONE' AND LogContent like '%{1}%'";
            jobIndex = jobIndex.Substring(0, Math.Min(50, jobIndex.Length)).ToUpper();

            if (!String.IsNullOrWhiteSpace(successContentOverride))
            {
                getLastLogEntry = String.Format(getLastLogEntry, jobIndex, successContentOverride);
            }
            else
            {
                if (runDate == new DateTime())
                {
                    runDate = DateTime.Today.AddDays(-1);
                }

                getLastLogEntry = String.Format(getLastLogEntry, jobIndex, runDate.ToString("yyyy-MM-dd") + " Completed Success");
            }

            return ExtractFactory.ConnectAndQuery(procLog.LoggerPhpConfig, getLastLogEntry).Rows.Count > 0;
        }

        // Base method to call out to the logger api, shouldn't be used directly by jobs - but rather through logger helper methods.
        private static T CallApi<T>(String endPoint, String method, Logger procLog, String payload = "", Dictionary<String, String> headers = null)
        {
            WebRequest http = WebRequest.Create(endPoint);

            http.UseDefaultCredentials = true;
            http.Method = method;
            http.ContentType = "application/json";

            if (headers != null)
            {
                foreach (String headerName in headers.Keys)
                {
                    http.Headers.Add(headerName, headers[headerName]);
                }
            }

            if (payload != "")
            {
                string nonASCII = @"[^\u0020-\u007E]";
                if (Regex.IsMatch(payload, nonASCII))
                {
                    //This is part of the two-pronged attack on stopping failed log messages
                    procLog.WriteToLog("We just stripped non-ASCII characters from the log message. They did not fall between u0020 and u007E");
                    payload = Regex.Replace(payload, nonASCII, string.Empty);
                }
                byte[] binary = System.Text.ASCIIEncoding.Default.GetBytes(payload);
                http.ContentLength = binary.Length;

                using (System.IO.Stream requestStream = http.GetRequestStream())
                {
                    requestStream.Write(binary, 0, binary.Length);
                }
            }

            using (Stream ms = http.GetResponse().GetResponseStream())
            {
                StreamReader reader = new StreamReader(ms, System.Text.Encoding.UTF8);

                String responseString = reader.ReadToEnd();

                return JsonConvert.DeserializeObject<T>(responseString);
            }
        }

        public static void WriteTextLog(string logContent, Logger logger, string fileName = "ExecutionLog")
        {
            // Build Log object.
            LogEntryModel log = new LogEntryModel()
            {
                JobIndex = logger.ProcessId.Substring(0, Math.Min(50, logger.ProcessId.Length)),
                LogDateTime = DateTime.Now,
                LoggedByUser = System.Security.Principal.WindowsIdentity.GetCurrent().Name,
                LogContent = logContent,
                LogCategory = LogCategory.INFO.ToString(),
                UID = logger.UniqueID
            };

            string logPath = "";

            if (logger.TestMode)
            {
                logPath = @"\\JobsEnvironmentTest\JobOutput\";
            }
            else
            {
                logPath = @"\\JobsEnvironmentProd\JobOutput\";
            }

            logPath += log.JobIndex + @"\ExecutionLogs\";
            logger.LoggerReportYearDir(logPath);

            fileName = logPath + fileName + "_" + DateTime.Today.ToString("yyyyMMdd") + ".csv";

            string record = ExtractFactory.ObjectToText(log, ",", endWithDelimiter: false);

            using (StreamWriter wr = File.AppendText(fileName))
            {
                record.Replace("\r\n", string.Empty)
                    .Replace("\n", string.Empty)
                    .Replace("\r", string.Empty);
                wr.WriteLine(record);
            }
        }

        // Custom logger exception to track logger-specific issues.
        public class ULoggerException : Exception
        {
            public ULoggerException() { }

            public ULoggerException(String content) : base(content) { }
        }

        // This model mimics the internal model that the API uses. Make sure these stay in sync.
        private class LogEntryModel
        {
            [Required(AllowEmptyStrings = false)]
            [MaxLength(50)]
            public String JobIndex { get; set; }

            [Required]
            public DateTime LogDateTime { get; set; }

            [Required(AllowEmptyStrings = false)]
            [MaxLength(20)]
            public String LogCategory { get; set; }

            [Required(AllowEmptyStrings = false)]
            [MaxLength(30)]
            public String LoggedByUser { get; set; }

            [Required(AllowEmptyStrings = false)]
            public String LogContent { get; set; }

            public string UID { get; set; }

            public bool Remediated { get; set; }

            [MaxLength(250)]
            public String RemediationNote { get; set; }
        }
    }
}
