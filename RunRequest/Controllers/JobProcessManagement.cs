using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using RunRequest.Models;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.IO;
using System.Configuration;
using Utilities;
using System.Web.Configuration;
using System.Web.Script.Serialization;
using System.Data;
using System.Data.SqlClient;
using Utilities.Schedules;
using System.DirectoryServices.AccountManagement;

namespace RunRequest.Controllers
{
    public class JobProcessManagement
    {

        public static List<DriverProcess> GetDriverProcesses()
        {
            List<DriverProcess> driverWinProcs = new List<DriverProcess>();
            bool testMode = (ConfigurationManager.AppSettings["testMode"] == "true");
            Data.AppNames db;
            if (!testMode)
            {
                db = Data.AppNames.ExampleProd;
            }
            else
            {
                db = Data.AppNames.ExampleTest;
            }

            List<string> servers = ExtractFactory.ConnectAndQuery<string>(db, "SELECT ServerName FROM [dbo].[Controller_ServerList_C]").ToList();            

            ParallelLoopResult result = Parallel.ForEach(servers, srv =>
            {
                try
                {
                    List<DriverProcess> foundProcs = GetProcsFromServer(srv);
                    driverWinProcs.AddRange(foundProcs);
                }
                catch (Exception ex)
                {

                }
            });

            return driverWinProcs;
        }

        private static string GetJobOwner(string pJobId)
        {
            List<string> jobOwner = null;
            string jobOwnerQuery = $@"SELECT TOP 1 Owner FROM (select top 1 owner from PHPArchv.dbo.CONTROLLER_PHP_JobDailySchedule_A WHERE JobId = '{pJobId}' and owner is not null
														       ORDER BY ScheduledStartTime DESC) as own";
            jobOwner = ExtractFactory.ConnectAndQuery<string>(PhpArchive, jobOwnerQuery).ToList();
            return jobOwner.Count > 0? jobOwner[0] : "";
        }

        public static String KillDriverProcess(string winProcess, string jobID, string userID, string server, string owner)
        {
            PrincipalContext ctx = new PrincipalContext(ContextType.Domain, "");
            GroupPrincipal phpit = GroupPrincipal.FindByIdentity(ctx, "");
            UserPrincipal user = UserPrincipal.FindByIdentity(ctx, userID);

            if(user.IsMemberOf(phpit) && owner != "PHP")
            {
                Task.Run(
                    () => Services.LoggingService.WriteToLog("WEB0004", DateTime.Now, userID, "PHP User " + userID + " has attempted to kill a Sparrow job " + jobID + ". That is not allowed.", "ERROR")
                    );
                throw new Exception("User Not Authorized to Kill Job");
            }

            string uri;
            uri = "http://" + server + "/api/DriverProcesses/" + winProcess;
            
            string rText;
            WebRequest request = WebRequest.Create(uri);
            request.UseDefaultCredentials = true;
            request.Method = "DELETE";
            request.ContentType = "application/json; charset=utf-8";
            HttpWebResponse response;
            try
            {
                response = (HttpWebResponse)request.GetResponse();
            }
            catch(Exception ex)
            {
                return ex.ToString();
            }
            
            using (StreamReader sr = new StreamReader(response.GetResponseStream()))
            {
                rText = sr.ReadToEnd();
            }

            if(jobID == null)
            {
                jobID = "that was manually started.";
            }


            Task.Run(
                () => Services.LoggingService.WriteToLog("WEB0004", DateTime.Now, userID, "User " + userID + " has attempted to kill a job " + jobID, "INFO")
                );

            Task.Run(
                () => Services.LoggingService.WriteToLog(jobID, DateTime.Now, userID, "User " + userID + " has attempted to kill this job", "INFO")
            );

            return rText;
        }
        
        public static List<ScheduledJob> GetJobSchedule(string scheduleDate = "")
        {
            if(scheduleDate == "")
            {
                scheduleDate = DateTime.Today.ToString("yyyy-MM-dd");
            }
            List<string> owners = ExtractFactory.ConnectAndQuery<string>(PhpConfig, "SELECT Owner FROM [dbo].[Controller_ServerList_C]").ToList();
            string finalScheduledJobQuery = "";
            foreach (string owner in owners)
            {
                finalScheduledJobQuery += ScheduledJobsQuery(scheduleDate, owner) + " UNION ALL ";
            }
            finalScheduledJobQuery += ScheduledJobsQuery(scheduleDate, "ALL") + " ORDER BY ScheduledStartTime DESC";
            List<ScheduledJob> jobList = ExtractFactory.ConnectAndQuery<ScheduledJob>(PhpArchive, finalScheduledJobQuery).ToList(); 
            return jobList;
        }

        public static List<LogRecord> GetLogsFromDatabase(String jobIndex, DateTime logDay)
        {
            List<LogRecord> logBlock = null;
            logBlock = ExtractFactory.ConnectAndQuery<LogRecord>(PhpConfig, LogByJobQuery(jobIndex, logDay.ToString("yyyy-MM-dd HH:mm:ss.fff"))).ToList();
            return logBlock;
        }

        public static void ResolveErroredJob(string jobIndex, DateTime date, string owner)
        {
            DataWork.RunSqlCommand(UpdateErrorRecordQuery(jobIndex, date.ToString("yyyy-MM-dd HH:mm:ss.fff"), owner), PhpArchive);
        }

        public static bool ReRunJob(String jobIndex, string user, string owner)
        {
            bool jobLaunched = false;
            String rawArgs = ExtractFactory.ConnectAndQuery<String>(PhpArchive, GetLastJobParamters(jobIndex, owner)).ToList().FirstOrDefault();
            List<String> splitArgs = rawArgs.Split(',').ToList();
            string jobId = splitArgs[0];
            splitArgs.RemoveAt(0);
            Dictionary<string, string> preJsonArgs = new Dictionary<string, string>();
            int argNum = 1;
            foreach (string argValue in splitArgs)
            {
                preJsonArgs.Add("arg" + argNum.ToString(), argValue);

                argNum++;
            }
            string parmsJson = new JavaScriptSerializer().Serialize(preJsonArgs);
            bool testMode = true;


            if(ConfigurationManager.AppSettings["testMode"] == "false")
            {
                testMode = false;
            }

            string returnValue = Services.SharedServices.CallDriverApi("JobConsole", jobId, testMode, user, parmsJson, owner, true);
            if (returnValue.Contains("Job started"))
            {
                jobLaunched = true;
            }
            return jobLaunched;
        }

        public static List<ScheduledJob> GetErroredJobs(int daysLookup)
        {
            List<ScheduledJob> erroredList = null;
            erroredList = ExtractFactory.ConnectAndQuery<ScheduledJob>(PhpConfig, GetErroredJobsQuery(daysLookup)).ToList();
            return erroredList;
        }

        public static List<RunJob> GetAllJobs(string date)
        {
            List<RunJob> jobList = null;
            jobList = ExtractFactory.ConnectAndQuery<RunJob>(PhpConfig, GetAllJobsRunQuery(date)).ToList();
            return jobList;
        }

        private static String ScheduledJobsQuery(string scheduleDate, string owner)
        {
            string groupTable = "";
            if (owner != "ALL")
            {
                groupTable = owner + "_";
            }

            return string.Format(@"SELECT
                                   [JobId]
                                  ,[ScheduledStartTime]
                                  ,[JobStatusReason]
                                  ,[FinalDisposition]
                                  ,[JobOutcome]
                                  ,COALESCE([ScheduleStatus], 'Active') as [ScheduleStatus]
                                  ,[RunStatus]
                                  ,[RequestedBy]
                                  ,[Environment]
								  ,CASE
									WHEN EXISTS (select * from [PHPConfg].[dbo].[JobConsoleJobNotes_A] where JobIndex = DDP.JobId and CONVERT(varchar(10), JobRunDate, 120) = CONVERT(varchar(10), DDP.ScheduledStartTime, 120))
										THEN 'Yes'
									ELSE 'No'
								   END AS HasNotes
                                  ,UPPER(Owner) AS Owner
                              FROM [PHPArchv].[dbo].[CONTROLLER_{1}JobDailySchedule_A] as DDP
                              WHERE CONVERT(varchar(10), ScheduledStartTime, 120) = '{0}' and
							        JobId not in (select * from PHPConfg.dbo.ShowBackendErrors_C)", scheduleDate, groupTable);
        }

        private static String LogByJobQuery(String jobIndex, String scheduledStartDate)
        {
            string convertedDate = Convert.ToDateTime(scheduledStartDate).ToString("yyyy-MM-dd");
            return String.Format(@"SELECT *, CONVERT(datetime, '{1}') as ScheduledStartTime
                                   FROM [PHPConfg].[ULOGGER].[LoggerRecord] with (nolock)
                                   WHERE CONVERT(varchar(10), LogDateTime, 120) = CONVERT(varchar(10), '{2}', 120) and
                                    JobIndex = '{0}' AND
                                   ((UID in(select UID 
	                                        from [PHPConfg].[ULOGGER].[LoggerRecord] with (nolock)
                                            where CONVERT(varchar(10), LogDateTime, 120) = CONVERT(varchar(10), '{2}', 120) and JobIndex = '{0}'
                                            group by UID) and UID <> '') or JobIndex = '{0}')
                                   ORDER BY LogDateTime, UID ASC", jobIndex, scheduledStartDate, convertedDate);
        }

        private static String UpdateErrorRecordQuery(string jobIndex, string date, string owner)
        {
            string groupTable = "";
            if (owner != "ALL")
            {
                groupTable = owner + "_";
            }

            return string.Format(@"UPDATE
                                  [PHPArchv].[dbo].[CONTROLLER_{2}JobDailySchedule_A]
                                  SET FinalDisposition = 'MILESTONE',
                                      JobStatusReason = 'Manually Resolved'
                                  WHERE JobId like '{0}%' AND ScheduledStartTime = '{1}'", jobIndex, date, groupTable);
        }

        private static String GetErroredJobsQuery(int daysLookup)
        {
            
            return String.Format(@"SELECT
                                   [JobId]
                                  ,[ScheduledStartTime]
                                  ,[JobStatusReason]
                                  ,[FinalDisposition]
                                  ,[JobOutcome]
                                  ,COALESCE([ScheduleStatus], 'Active')
                                  ,[RunStatus]
                                  ,[RequestedBy]
                                  ,[Environment]
								  ,CASE
									WHEN EXISTS (select * from [PHPConfg].[dbo].[JobConsoleJobNotes_A] where JobIndex = DDP.JobId and CONVERT(varchar(10), JobRunDate, 120) = CONVERT(varchar(10), DDP.ScheduledStartTime, 120))
										THEN 'Yes'
									ELSE 'No'
								   END AS HasNotes
								  ,UPPER((SELECT TOP 1 CJDS.Owner FROM PHPArchv.dbo.CONTROLLER_SPARROW_JobDailySchedule_A AS CJDS WHERE CJDS.JobId = DDP.JobId ORDER BY CJDS.ScheduledStartTime DESC)) AS Owner
                                  FROM [PHPArchv].[dbo].[CONTROLLER_SPARROW_JobDailySchedule_A] as DDP
                                  WHERE FinalDisposition = 'ERROR' AND ScheduledStartTime >= DATEADD(dd, -{0}, GETDATE())
								  UNION ALL
                                  SELECT
                                   [JobId]
                                  ,[ScheduledStartTime]
                                  ,[JobStatusReason]
                                  ,[FinalDisposition]
                                  ,[JobOutcome]
                                  ,COALESCE([ScheduleStatus], 'Active')
                                  ,[RunStatus]
                                  ,[RequestedBy]
                                  ,[Environment]
								  ,CASE
									WHEN EXISTS (select * from [PHPConfg].[dbo].[JobConsoleJobNotes_A] where JobIndex = DDP.JobId and CONVERT(varchar(10), JobRunDate, 120) = CONVERT(varchar(10), DDP.ScheduledStartTime, 120))
										THEN 'Yes'
									ELSE 'No'
								   END AS HasNotes
								  ,UPPER((SELECT TOP 1 CJDS.Owner FROM PHPArchv.dbo.CONTROLLER_PHP_JobDailySchedule_A AS CJDS WHERE CJDS.JobId = DDP.JobId ORDER BY CJDS.ScheduledStartTime DESC)) AS Owner
                                  FROM [PHPArchv].[dbo].[CONTROLLER_PHP_JobDailySchedule_A] as DDP
                                  WHERE FinalDisposition = 'ERROR' AND ScheduledStartTime >= DATEADD(dd, -{0}, GETDATE())
								  ORDER BY ScheduledStartTime DESC", daysLookup);
        }

        private static String GetAllJobsRunQuery(string date)
        {
            return string.Format(@"SELECT DISTINCT [JobIndex]
				                                  ,CONVERT(datetime, '{0}') as [LogDateTime]
                                                  ,UPPER((SELECT TOP 1 CJDS.Owner FROM [PHPConfg].dbo.Controller_Schedule_C AS CJDS WHERE CJDS.JobId = JobIndex)) AS Owner
                                  FROM [PHPConfg].[ULOGGER].[LoggerRecord] with (nolock)
                                  WHERE CAST(FLOOR(CAST(LogDateTime as float)) as datetime) = CONVERT(datetime, '{0}')
                                  GROUP BY JobIndex
                                  order by JobIndex", date);
        }

        private static String GetLastJobParamters(string jobIndex, string owner)
        {
            string groupTable = "";
            if (owner != "ALL")
            {
                groupTable = owner + "_";
            }

            return string.Format(@"SELECT [Parameters]
                                    FROM [PHPArchv].[dbo].[CONTROLLER_{1}JobDailySchedule_A]
                                    WHERE JobId = '{0}'
                                    AND ScheduledStartTime = (select max(ScheduledStartTime) from [PHPArchv].[dbo].[CONTROLLER_{1}JobDailySchedule_A] where JobId = '{0}' and RunStatus is not null)", jobIndex, groupTable);
        }

        public static List<JobNotes> GetNotes(string jobIndex, DateTime jobRunDate)
        {
            List<JobNotes> notes = ExtractFactory.ConnectAndQuery<JobNotes>(PhpConfig, GetJobsNotesQuery(jobIndex, jobRunDate.ToString("yyyy-MM-dd"))).ToList();
            if(notes.Count < 1)
            {
                JobNotes emptyNote = new JobNotes();
                emptyNote.JobIndex = jobIndex;
                emptyNote.JobRunDate = jobRunDate;
                emptyNote.NoteText = "EMPTY";
                notes.Add(emptyNote);
            }
            return notes;
        }

        public static void PostJobNote(JobNotes newNote)
        {
            newNote.NoteText = newNote.NoteText.Replace("'", "''");
            DataWork.RunSqlCommand(PostJobNoteQuery(newNote), PhpConfig);
        }

        private static string GetJobsNotesQuery(string jobIndex, string jobRunDate)
        {
            return string.Format(@"SELECT 
                                   [NoteIndex]
                                  ,[JobIndex]
                                  ,[JobRunDate]
                                  ,[NoteDateTime]
                                  ,[AdminUser]
                                  ,[NoteText]
                              FROM [PHPConfg].[dbo].[JobConsoleJobNotes_A]
                              WHERE JobIndex = '{0}' AND JobRunDate = '{1}'
                              ORDER BY NoteIndex DESC", jobIndex, jobRunDate);
        }

        private static string PostJobNoteQuery(JobNotes newNote)
        {
            return string.Format(@"INSERT INTO [PHPConfg].[dbo].[JobConsoleJobNotes_A] ([JobIndex],[JobRunDate],[NoteDateTime],[AdminUser],[NoteText])
                                   VALUES('{0}', '{1}', GETDATE(), '{2}', '{3}')", newNote.JobIndex, newNote.JobRunDate.ToString("yyyy-MM-dd"), newNote.AdminUser, newNote.NoteText);
        }

        public static bool RunAdhoc(String jobIndex, String user, String parms, String launchServerName)
        {
            bool jobLaunched = false;
            
            List<String> splitArgs = JsonConvert.DeserializeObject<List<String>>(parms);

            Dictionary<string, string> preJsonArgs = new Dictionary<string, string>();
            int argNum = 1;
            foreach (string argValue in splitArgs)
            {
                preJsonArgs.Add("arg" + argNum.ToString(), argValue);

                argNum++;
            }
            string parmsJson = new JavaScriptSerializer().Serialize(preJsonArgs);
            bool testMode = true;
            if (ConfigurationManager.AppSettings["testMode"] == "false")
            {
                testMode = false;
            }

            string returnValue = Services.SharedServices.CallDriverApi("JobConsole", jobIndex, testMode, user, parmsJson, launchServerName, false);
            if(returnValue.Contains("Job started"))
            {
                jobLaunched = true;
            }
            return jobLaunched;
        }

        public static Data.AppNames PhpArchive
        {
            get
            {
                Data.AppNames archiveServer;
                if (ConfigurationManager.AppSettings["testMode"] != "true")
                {
                    archiveServer = Data.AppNames.ExampleTest;
                }
                else
                {
                    archiveServer = Data.AppNames.ExampleProd;
                }

                return archiveServer;
            }
            set
            {

            }
        }

        public static Data.AppNames PhpConfig
        {
            get
            {
                Data.AppNames configServer;
                if (ConfigurationManager.AppSettings["testMode"] != "true")
                {
                    configServer = Data.AppNames.ExampleTest;
                }
                else
                {
                    configServer = Data.AppNames.ExampleProd;
                }

                return configServer;
            }
            set
            {

            }
        }

        private static List<DriverProcess> GetProcsFromServer(string server)
        {
            List<DriverProcess> procs = new List<DriverProcess>();
            string serverUrl = "http://" + server + "/api/DriverProcesses";
            WebClient cl = new WebClient();
            cl.UseDefaultCredentials = true;
            string returnData = cl.DownloadString(serverUrl);

            JArray reObject = JArray.Parse(returnData);
            foreach (JObject addy in reObject)
            {
                bool mrData = addy.SelectToken("args[2]").ToString() == "Mr_Data";
                string argToUse = mrData ? "3" : "2";
                string owner = GetJobOwner(addy.SelectToken($"args[{argToUse}]").ToString());
                addy.Merge(JObject.Parse(@"{ ""Owner"": """ + owner + @"""}"));
                DriverProcess proc = addy.ToObject<DriverProcess>();
                proc.server = server;
                procs.Add(proc);
            }

            return procs;
        }
    }
}