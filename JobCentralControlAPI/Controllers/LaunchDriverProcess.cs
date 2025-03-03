using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using System.Collections.Specialized;
using Utilities;

namespace JobCentralControlAPI.Controllers
{
    public class LaunchDriverProcess
    {
        public static string LaunchProcess(string source, string jobID, string testMode, string requestor, bool sourceReady, JObject args = null, bool rerun = false)
        {
            string ownerGroup = Environment.GetEnvironmentVariable("OwnerGroup");
            requestor = requestor.Replace('~', '\\');
            string argString = "";
            bool isTestMode = testMode == "Test" ? true : false;
            string driver = Environment.GetEnvironmentVariable("DriverPath", EnvironmentVariableTarget.Machine);

            try
            {
                argString = CreateCommandlineArgs(jobID, testMode, requestor, args);
            }
            catch (Exception)
            {

            }

            string recordedArgs = null;
            if (source != "RUNREQUEST")
            {
                recordedArgs = jobID;
                Dictionary<string, string> usefulArgs = new Dictionary<string, string>();
                if (args != null)
                {
                    usefulArgs = args.ToObject<Dictionary<string, string>>();
                }

                if (usefulArgs.Count > 0)
                {
                    foreach (KeyValuePair<string, string> arg in usefulArgs)
                    {
                        if (arg.Value.Trim() != "")
                        {
                            recordedArgs += "," + arg.Value;
                        }
                    }
                }
            }

            //if it isn't a rerun, add a record to the Job Schedule table so we can track it
            if (rerun == false && sourceReady)
            {
                string insertQuery = string.Format("insert into CONTROLLER_" + ownerGroup + "_JobDailySchedule_A (JobId, ScheduleId, ScheduledStartTime, RequestedBy, ScheduleStatus, Environment, RunStatus, Owner, ActualStartTime, [Parameters]) select '{0}', '0', '{1}', '{2}', 'Active', '{3}', 'S', '{5}', '{1}', '{4}'", jobID, DateTime.Now, requestor, isTestMode ? "T" : "P", recordedArgs, ownerGroup);

                DataWork.RunSqlCommand(insertQuery, isTestMode ? Data.AppNames.ExampleTest : Data.AppNames.ExampleProd);
            }
            else if (rerun == false && !sourceReady && source != "RUNREQUEST")
            {
                string insertQuery = string.Format("insert into CONTROLLER_" + ownerGroup + "_JobDailySchedule_A (JobId, ScheduleId, ScheduledStartTime, RequestedBy, ScheduleStatus, Environment, RunStatus, Owner, ActualStartTime, [Parameters]) select '{0}', '0', '{1}', '{2}', 'Active', '{3}', 'S', '{5}', '{1}', '{4}'", jobID, DateTime.Now, requestor, isTestMode ? "T" : "P", recordedArgs, ownerGroup);

                DataWork.RunSqlCommand(insertQuery, isTestMode ? Data.AppNames.ExampleTest : Data.AppNames.ExampleProd);
                return "One of the data sources associated with this job is inaccessible. The job has been queued.";
            }
            else if (!sourceReady)
            {
                return "One of the data sources associated with this job is inaccessible.";
            }

            if (source == "RUNREQUEST")
            {
                if (argString == "" || (!argString.ToUpper().Contains("MR_DATA") && !argString.ToUpper().Contains("MS_DATA")))
                {
                    argString = "";
                    string strArgs = args.ToString();
                    strArgs = strArgs.Replace("\n", "");
                    strArgs = strArgs.Replace("\r", "");
                    strArgs = strArgs.Replace("\"", "~");
                    strArgs = strArgs.Replace(" ", "`");
                    argString = jobID + " " + strArgs + " -r " + requestor;
                    if (isTestMode)
                    {
                        argString = argString + " -t";
                    }
                }
                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo(driver, argString);
                    startInfo.UseShellExecute = false;
                    try
                    {
                        Process process = Process.Start(startInfo);
                        while (!process.HasExited)
                        {
                            System.Threading.Thread.Sleep(50);
                        }
                        Utilities.Data.AppNames dataSource;
                        if (isTestMode)
                        {
                            dataSource = Utilities.Data.AppNames.ExampleTest;
                        }
                        else
                        {
                            dataSource = Utilities.Data.AppNames.ExampleProd;
                        }
                        string GUID = "";
                        try
                        {
                            GUID = Utilities.ExtractFactory.ConnectAndQuery<string>(dataSource, string.Format(@"SELECT TOP(1) UID FROM [PHPConfg].[ULOGGER].[LoggerRecord] a with (nolock) WHERE JobIndex = '{0}' AND LoggedByUser = '{1}' AND LogCategory = 'AUDIT' AND LogDateTime > DATEADD(MINUTE,-5,GETDATE())
	                            AND EXISTS (SELECT TOP(1) UID FROM [PHPConfg].[ULOGGER].[LoggerRecord] b with (nolock) WHERE a.UID = b.UID AND LogCategory = 'MILESTONE' and LogContent = 'Program Complete') ORDER BY LogDateTime DESC"
                                , jobID, requestor)).First();
                        }
                        catch (Exception Ex)
                        {
                            return LogErrorAndReturn(jobID, isTestMode, "", Ex.Message + " - " + Ex.StackTrace, "Run failed to complete.");
                        }

                        if (GUID == "")
                        {
                            return "Failed to find completion record.";
                        }

                        string file = "";
                        try
                        {
                            file = Utilities.ExtractFactory.ConnectAndQuery<string>(dataSource, string.Format(@"SELECT TOP(1) LogContent FROM [PHPConfg].[ULOGGER].[LoggerRecord] with (nolock) WHERE UID = '{0}' AND LogCategory = 'AUDIT'", GUID)).First();
                        }
                        catch (System.IO.FileNotFoundException Ex)
                        {
                            return LogErrorAndReturn(jobID, isTestMode, GUID, Ex.Message + " - " + Ex.StackTrace, "Failed to find output file.");
                        }

                        if (file == "")
                        {
                            return "Failed to find file.";
                        }

                        return file;
                    }
                    catch (Exception E)
                    {
                        return LogErrorAndReturn(jobID, isTestMode, "", E.Message + " - " + E.StackTrace, "Failed to launch job.");
                    }
                }
                catch (Exception Ex)
                {
                    return LogErrorAndReturn(jobID, isTestMode, "", Ex.Message + " - " + Ex.StackTrace, "Failed to launch job.");
                }
            }
            else
            {
                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo(driver, argString);
                    startInfo.UseShellExecute = false;
                    try
                    {
                        Process process = Process.Start(startInfo);
                        return "Job started with proc ID: " + process.Id;
                    }
                    catch (Exception E)
                    {
                        return LogErrorAndReturn(jobID, isTestMode, "", E.Message + " - " + E.StackTrace, "Failed to launch job.");
                    }
                }
                catch (Exception Ex)
                {
                    return LogErrorAndReturn(jobID, isTestMode, "", Ex.Message + " - " + Ex.StackTrace, "Failed to launch job.");
                }
            }
        }

        private static string LogErrorAndReturn(string jobID, bool testMode, string guid, string message, string userMessage)
        {
            Logger logger = new Logger(new Logger.LaunchRequest(jobID, testMode, null, uid: guid != null ? guid : ""));
            logger.WriteToLog(message, UniversalLogger.LogCategory.ERROR);

            return userMessage;
        }

        public static string CreateCommandlineArgs(string jobID, string testMode, string requestor, JObject args)
        {
            string argString = jobID;
            Dictionary<string, string> argsDict = new Dictionary<string, string>();
            if (args != null)
            {
                argsDict = args.ToObject<Dictionary<string, string>>();
            }
            else
            {
                argsDict.Add("","");
            }

            foreach (KeyValuePair<string, string> argument in argsDict)
            {
                if (argument.Value == "Mr_Data" || argument.Value == "Ms_Data")
                {
                    argString = argument.Value + " " + argString;
                }
                else
                {
                    argString += " " + argument.Value;
                }
            }
            if (testMode == "Test")
            {
                argString = argString + " -t"; ;
            }
            argString += " -r " + requestor;

            return argString;
        }
    }
}