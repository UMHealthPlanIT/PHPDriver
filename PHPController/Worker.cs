using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Data;
using System.Data.Linq;
using Utilities;
using System.Configuration;
using System.Management;
using System.IO;
using Utilities.Integrations;
using Utilities.Schedules;

namespace PHPController
{
    /// <summary>
    /// Launches and tracks the results of jobs
    /// </summary>
    class Worker
    {
        Logger procLog;
        string OwnerGroup;
        Dictionary<string, string> DataSourceReadinessList = null;

        public Worker(bool isTestMode, string ownerGroup)
        {
            procLog = new Logger("Worker", setTestMode: isTestMode, logOnly: true, uniqueID: Guid.NewGuid().ToString());
            OwnerGroup = ownerGroup;
            UniversalLogger.WriteToLogProgramStart(procLog);
            ManageJobs();
            UniversalLogger.WriteToLogProgramComplete(procLog);
        }



        public bool ManageJobs()
        {
            TimeSpan timeSpan = (DateTime.Now.Subtract(DateTime.MinValue));
            DateTime interval = DateTime.MinValue.Add(new TimeSpan(0, (((int)timeSpan.TotalMinutes) / 15) * 15, 0));

            DataSourceReadinessList = GetDataSourceStatus();

            List<DailyScheduleRecord> pendingJobs = ExtractFactory.ConnectAndQuery<DailyScheduleRecord>(procLog.LoggerPhpArchive, GetPendingJobsQuery()).ToList();

            pendingJobs.AddRange(GetHeartbeatJobs(interval));

            if (pendingJobs.Count() == 0)
            {
                procLog.WriteToLog("There are no pending programs or any in error.");
                UniversalLogger.WriteToLogProgramComplete(procLog);
                return false;
            }

            List<string> relevantDataSources = new List<string>(DataSourceReadinessList.Keys);


            //If the master switch is set to off, nothing is getting run this heartbeat
            if (DataSourceReadinessList["Master"] == "Yes")
            {
                ProcessBackendErrors();

                List<JobLogRecord> completedJobs = GetCompletedJobs();

                //Loop through each job found that was either started, or should have been started
                foreach (DailyScheduleRecord pendProg in pendingJobs)
                {
                    JobIndex jobIndexData = new JobIndex(pendProg.JobId); //Some jobs are scheduled with appended letters, but those won't get us to the job index values we're after. So we're chopping it down to the first 7 characters or less.
                    try
                    {
                        procLog.WriteToLog(String.Format("Extracted program code {0} from the JobDailySchedule table", jobIndexData.JobId));

                        JobRecordMatch matchRecords = new JobRecordMatch(completedJobs, jobIndexData, pendProg.ActualStartTime);

                        JobLogRecord currentJob = null;

                        bool previouslyErrored = (pendProg.JobOutcome != "MILESTONE" && !string.IsNullOrEmpty(pendProg.JobOutcome));
                        bool weFoundACompletionRecord = false;

                        //Match the current job to the found errored completion record
                        if ((matchRecords.ErroredRecord != null && matchRecords.CompletedJob.UID == matchRecords.ErroredRecord.UID) && (matchRecords.CompletedJob.LogDateTime > pendProg.ScheduledStartTime))
                        {
                            currentJob = matchRecords.ErroredRecord;
                            weFoundACompletionRecord = true;
                        }
                        //Match the current job to the found successful completion record
                        else if (matchRecords.CompletedJob != null && matchRecords.CompletedJob.LogDateTime > pendProg.ActualStartTime)
                        {
                            currentJob = matchRecords.CompletedJob;
                            weFoundACompletionRecord = true;
                        }

                        //We did not find a completion record, attempt to launch the job
                        if (!weFoundACompletionRecord)
                        {
                            if (jobIndexData != null)
                            {
                                LaunchJob(pendProg, previouslyErrored, jobIndexData);
                            }
                            else
                            {
                                procLog.WriteToLog(pendProg.JobId + " does not appear to have a record in the Job Index.  Job will not launch.", UniversalLogger.LogCategory.ERROR);
                            }
                        }

                        //We've matched a completion record to the current job with a status that has not previously been recorded
                        if (weFoundACompletionRecord && pendProg.JobOutcome != currentJob.LogCategory)
                        {
                            pendProg.RunStatus = "F";
                            pendProg.JobOutcome = currentJob.LogCategory;
                            pendProg.FinalDisposition = currentJob.LogCategory;
                            pendProg.JobStatusReason = currentJob.LogContent.Truncate(500);
                            pendProg.ActualEndTime = currentJob.LogDateTime;
                        }
                        //No completion milestones were found
                        else
                        {
                            procLog.WriteToLog(jobIndexData.JobId + " must still be running or still in error. In other words, we haven't found a completion milestone or error record yet, but it isn't late or hasn't been fixed");
                        }

                    }
                    catch (Exception ex)
                    {
                        procLog.WriteToLog("Encountered an error when checking status of, or trying to run job " + jobIndexData.JobId + ":" + ex.ToString(), UniversalLogger.LogCategory.ERROR);
                    }

                    //Record status to the Daily Tracking Table
                    try
                    {
                        pendProg.UpdateRecord(procLog.TestMode, OwnerGroup);
                    }
                    catch (Exception ex)
                    {
                        procLog.WriteToLog("Record failed to update for " + jobIndexData.JobId + " :" + ex.ToString(), UniversalLogger.LogCategory.ERROR);
                    }
                }
            }
            else
            {
                procLog.WriteToLog("Master switch is set to off. No new jobs will start.", UniversalLogger.LogCategory.WARNING);
                SendAlerts.Send(procLog.ProcessId, 0, "Master Switch is Set to Off", "The Master Switch prevented this heartbeat from launching jobs. Check the switch if this is in error.", procLog);
            }
            return true;
        }

        /// <summary>
        /// Compare the number of currently running jobs to the max set in app.config
        /// </summary>
        /// <param name="procLog">The Logger insace for Worker so that logs can all be grouped for the same Worker instance</param>
        /// <returns></returns>
        private bool UnderMaxConcurrentJobLimit()
        {
            int currentProcsRunning = GetNumberOfCurrentDriverProcs();
            int maxProcsAllowed = Convert.ToInt16(ConfigurationManager.AppSettings["ConcurrentProcesses"]);
            procLog.WriteToLog("Maximum process count: " + maxProcsAllowed.ToString() + ". Current number of processes found: " + currentProcsRunning.ToString());

            return (currentProcsRunning < maxProcsAllowed);
        }

        /// <summary>
        /// Checks to ensure the job is ready to run (under max job count on the server, data sources used by this job are available, and job is not on hold), then launches the job.
        /// </summary>
        /// <param name="pendingProgram">Job to be launched</param>
        /// <param name="previouslyErrored">If this job was previously run and ended in error</param>
        /// <param name="currentJobIndexData">Job data from the Job Index record</param>
        /// <returns></returns>
        private Boolean LaunchJob(DailyScheduleRecord pendingProgram, Boolean previouslyErrored, JobIndex currentJobIndexData)
        {
            bool jobLaunched = false;
            bool jobOnHold = false;
            bool jobIsRunnable;
            try
            {
                jobIsRunnable = (currentJobIndexData.Tool.Contains(".Net") || currentJobIndexData.Tool.ToUpper().Contains("MRDATA") || pendingProgram.Parameters.ToUpper().Contains("MS_DATA"));
            }
            catch (NullReferenceException ex)
            {
                procLog.WriteToLog("Job " + pendingProgram.Id + " does not have a record in the Job Index table. The job can not be launched.", UniversalLogger.LogCategory.ERROR);
                return false;
            }

            bool jobNotLaunchedPreviously = (pendingProgram.ActualStartTime == null && !previouslyErrored && pendingProgram.QueuedTime == null);
            bool dataSourcesReady = DataSourceManagement.JobSourcesAreReady(currentJobIndexData, procLog); ;
            List<string> unavailableDataSources = new List<string>();

            //If we have too many jobs already running we will not launch another
            if (!UnderMaxConcurrentJobLimit())
            {
                procLog.WriteToLog(String.Format("Maximum concurrent job count has been reached. {0} will be launched on the next Controller heartbeat.", currentJobIndexData.JobId));
            }

            //If this is a heartbeat job, there will be no schedule to check
            if (!pendingProgram.IsHeartBeatJob)
            {
                //We won't check the schedule if this run was requested ad-hoc by user
                if (pendingProgram.ScheduleId != 0)
                {
                    //Does the job have any schedule records to check?
                    if (currentJobIndexData.Schedules.Count > 0)
                    {
                        //If the current schedule record for this job set to On Hold, do not run
                        if (currentJobIndexData.Schedules.Where(x => x.ID == pendingProgram.ScheduleId).FirstOrDefault().OnHold)
                        {
                            procLog.WriteToLog($"The job's current schedule is set to On Hold. {currentJobIndexData.JobId} will not be launched.");
                            jobOnHold = true;
                        }
                    }
                }
            }


            //If the job in the Job Index set to On Hold, do not run
            if (currentJobIndexData.OnHold)
            {
                procLog.WriteToLog($"The job set to On Hold in the Job Index. {currentJobIndexData.JobId} will not be launched.");
                jobOnHold = true;
            }

            //Check on hold status in the job schedule record against what we have in our daily tracking table
            if (jobOnHold && pendingProgram.RequestedBy.ToUpper() == "LSFUSERSCHEDULE" && pendingProgram.ScheduleStatus != "On Hold")
            {
                pendingProgram.ScheduleStatus = "On Hold";
                procLog.WriteToLog($"Marking daily tracker record as On Hold for {currentJobIndexData.JobId}");
            }
            else if (!jobOnHold && pendingProgram.ScheduleStatus == "On Hold")
            {
                pendingProgram.ScheduleStatus = "Active";
                procLog.WriteToLog($"Job is no longer marked as On Hold, changing daily tracker record to Active for {currentJobIndexData.JobId}");
            }

            //Check if the job is a runnable job type, hasn't already been started, and isn't on hold
            if (jobIsRunnable && jobNotLaunchedPreviously && (pendingProgram.RequestedBy.ToUpper() != "LSFUSERSCHEDULE" || !jobOnHold))
            {
                if (dataSourcesReady)
                {
                    NewJob.QueueProgram(pendingProgram, procLog, OwnerGroup);
                    procLog.WriteToLog("We just queued: " + pendingProgram.JobId + ". Which is supposed to start at: " + pendingProgram.ScheduledStartTime + " and is in schedule status: " + pendingProgram.ScheduleStatus);
                    jobLaunched = true;
                }
                //One or more data sources for this job are not available
                else
                {
                    procLog.WriteToLog("Datasource " + String.Join(",", unavailableDataSources) + " readiness switch is set to No. " + pendingProgram.JobId + " did not start.");

                    if (DateTime.Now > pendingProgram.ScheduledStartTime && pendingProgram.JobStatusReason == null)
                    {
                        pendingProgram.JobStatusReason = "Waiting To Start: " + String.Join(", ", unavailableDataSources);
                    }
                }
            }

            //Write updates to the daily tracking table record
            try
            {
                pendingProgram.UpdateRecord(procLog.TestMode, OwnerGroup);
            }
            catch (Exception ex)
            {
                procLog.WriteToLog("Record failed to update for " + pendingProgram.JobId + " :" + ex.ToString(), UniversalLogger.LogCategory.ERROR);
            }

            return jobLaunched;
        }

        /// <summary>
        /// Retrieves the number of currently running insances of Driver.exe on the server
        /// </summary>
        /// <returns></returns>
        private int GetNumberOfCurrentDriverProcs()
        {
            procLog.WriteToLog("Evaluating total number of instances of Driver currently running");
            string wmiQuery = string.Format("select * from Win32_Process where Name='{0}'", "Driver.exe");
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(wmiQuery);
            ManagementObjectCollection retObjectCollection = searcher.Get();
            int currentProcsRunning = retObjectCollection.Count;

            return currentProcsRunning;
        }

        /// <summary>
        /// Retrieves list of jobs that have completed today 
        /// </summary>
        /// <returns></returns>
        private List<JobLogRecord> GetCompletedJobs()
        {
            List<JobLogRecord> records = ExtractFactory.ConnectAndQuery<JobLogRecord>(procLog.LoggerPhpConfig, GetCompletedJobsTodayQuery(), tries: 1).ToList();
            return records;
        }

        /// <summary>
        /// Looks for any new errors in Controller, Worker, ULOGGER, etc and adds them to the JobDailySchedule table so they show in the job console
        /// </summary>
        private void ProcessBackendErrors()
        {
            string groupTable = "";
            if (OwnerGroup != "ALL")
            {
                groupTable = OwnerGroup + "_";
            }
            //Yeah we're taking the max of a text field. If we don't, then we'll sometimes end up with multiple entries in the JobDailySchedule table with the same timestamp because it truncates the milliseconds
            string query = string.Format(@"INSERT INTO PHPArchv.dbo.CONTROLLER_{0}JobDailySchedule_A (JobId, ScheduledStartTime, FinalDisposition, JobOutcome, JobStatusReason, ScheduleId, RunStatus, [Owner])
                                            SELECT L.JobIndex, 
                                            dateadd(ms, -datepart(ms, LogDateTime), LogDateTime),                                              
                                            LogCategory,    
                                            LogCategory, 
                                            LEFT(max(LogContent),500),
                                            0,
                                            'F',
                                            '{1}'
                                            FROM PHPConfg.ULOGGER.LoggerRecord as L with (nolock)
                                            left join PHPArchv.dbo.CONTROLLER_{0}JobDailySchedule_A as IT on L.JobIndex = IT.JobId and dateadd(ms, -datepart(ms, L.LogDateTime), L.LogDateTime) = IT.ScheduledStartTime
                                            inner join (select * from PHPConfg.dbo.ShowBackendErrors_C) as err on L.JobIndex like '%'+err.JobIndex+'%'
                                            WHERE 
                                            LogDateTime >= convert(date, getdate()) and 
                                            LogCategory = 'ERROR' and
                                            L.JobIndex NOT LIKE 'WEB%' and
                                            IT.Id is null
                                            GROUP BY L.JobIndex, dateadd(ms, -datepart(ms, LogDateTime), LogDateTime), dateadd(ms, -datepart(ms, LogDateTime), LogDateTime), LogCategory", groupTable, OwnerGroup);

            procLog.WriteToLog("Only adding non-job errors to the Sparrow table to avoid duplicates", UniversalLogger.LogCategory.WARNING);

        }

        /// <summary>
        /// Retrieve data source list and their status
        /// </summary>
        /// <returns>Dictionary with Data Source name as key, and 'Yes' or 'No' as value</returns>
        private Dictionary<string, string> GetDataSourceStatus()
        {
            Dictionary<string, string> dsData = new Dictionary<string, string>();
            try
            {
                procLog.WriteToLog("Checking Data Source Status");
                dsData = DataSourceManagement.SourceStatus(procLog.TestMode);
            }
            catch (Exception exc)
            {
                procLog.WriteToLog(exc.ToString());
                procLog.WriteToLog("Unable to setup data source readiness list.", UniversalLogger.LogCategory.WARNING);
            }

            return dsData;
        }

        /// <summary>
        /// Retrieves a list of jobs that need to run based on heartbeat frequency (every heartbeat, every other heartbeat, once an hour, etc.)
        /// </summary>
        /// <param name="interval">Current DateTime to set as scheduled start time</param>
        /// <returns></returns>
        private List<DailyScheduleRecord> GetHeartbeatJobs(DateTime interval)
        {
            List<DailyScheduleRecord> hbJobs = new List<DailyScheduleRecord>();
            try
            {
                procLog.WriteToLog("Getting Heartbeat jobs");
                List<String> heartbeatPrograms = ExtractFactory.ConnectAndQuery<String>(procLog.LoggerPhpConfig, GetHeartbeatJobsQuery(interval)).ToList();
                foreach (string hbp in heartbeatPrograms)
                {
                    DailyScheduleRecord heartbeatJob = new DailyScheduleRecord();
                    heartbeatJob.JobId = hbp.Trim();
                    heartbeatJob.RequestedBy = "Schedule";
                    heartbeatJob.ScheduledStartTime = interval;
                    heartbeatJob.ScheduleStatus = "Active";
                    heartbeatJob.ScheduleId = 0;
                    heartbeatJob.Parameters = hbp.Trim();
                    heartbeatJob.IsHeartBeatJob = true;
                    hbJobs.Add(heartbeatJob);
                }
            }
            catch (Exception e)
            {
                procLog.WriteToLog("There was an error while trying to get the Heartbeat Jobs: " + e.Message, UniversalLogger.LogCategory.ERROR);
                procLog.WriteToLog(e.StackTrace, UniversalLogger.LogCategory.WARNING);
            }
            return hbJobs;
        }

        /// <summary>
        /// Returns SQL query that will pull all jobs with a completion record dated for today from the ULogger table.
        /// </summary>
        /// <returns></returns>
        private string GetCompletedJobsTodayQuery()
        {
            return string.Format(@"SELECT UID
                                    into #temp
                                    FROM [PHPConfg].[ULOGGER].[LoggerRecord] with (nolock)
                                    WHERE 
                                    CONVERT(varchar(10), LogDateTime, 120) = CONVERT(varchar(10), GETDATE(), 120)
                                    AND LogCategory = 'MILESTONE' 
                                    AND LogContent = 'Program Started'


                                    SELECT *
                                    FROM [PHPConfg].[ULOGGER].[LoggerRecord] with (nolock)
                                    WHERE UID IN
                                    (SELECT UID from #temp)
                                    and ((LogCategory = 'MILESTONE' and LogContent = 'Program Complete') OR LogCategory = 'ERROR')
                                    AND CONVERT(varchar(10), LogDateTime, 120) = CONVERT(varchar(10), GETDATE(), 120)
                                    AND JobIndex NOT IN ('CONTROLLER', 'DRIVER', 'WORKER')
                                    ORDER BY LogDateTime DESC

                                    drop table #temp");
        }

        /// <summary>
        /// Returns SQL query that will pull all pending jobs (jobs that should have already been started) from the appropriate daily tracking table by owner
        /// </summary>
        /// <returns></returns>
        private string GetPendingJobsQuery()
        {
            DateTime dateTest = DateTime.Now;
            string groupTable = "";
            if (OwnerGroup != "ALL")
            {
                groupTable = OwnerGroup + "_";
            }
            return string.Format(@"SELECT 
	                               [Id]
                                  ,[JobId]
                                  ,[ScheduleId]
                                  ,[ScheduledStartTime]
                                  ,[JobStatusReason]
                                  ,[FinalDisposition]
                                  ,[JobOutcome]
                                  ,[ActualEndTime]
                                  ,[ScheduleStatus]
                                  ,[ActualStartTime]
                                  ,[QueuedTime]
                                  ,[RunStatus]
                                  ,[RequestedBy]
                                  ,[Owner]
                                  ,[Environment]
                                  ,[Parameters]
	                              FROM [PHPArchv].[dbo].[CONTROLLER_{2}JobDailySchedule_A]
	                              WHERE (([JobOutcome] = '' OR [JobOutcome] IS NULL OR [JobOutcome] = 'ERROR')
	                                                            AND ScheduledStartTime BETWEEN '{0}' and '{1}')
                                                                and JobId not in (select * from PHPConfg.dbo.ShowBackendErrors_C) 
                                  AND (RunStatus = 'S' OR RunStatus = '' OR RunStatus IS NULL)", dateTest.ToString("yyyy-MM-dd"), dateTest.ToString(), groupTable);
        }

        /// <summary>
        /// Retrieve list of jobs that that need to launch on this heartbeat by owner
        /// </summary>
        /// <param name="interval">Current datetime</param>
        /// <returns></returns>
        private string GetHeartbeatJobsQuery(DateTime interval)
        {
            string twentyFour = interval.ToString("HH:mm:ss");
            string minToCheck = interval.Minute.ToString();
            string whereStatment = "";
            if (minToCheck == "15" || minToCheck == "45")
            {
                whereStatment = "RunFrequency in ('4')";
            }
            else if (minToCheck == "30")
            {
                whereStatment = "RunFrequency in ('4','2')";
            }
            else
            {
                whereStatment = "RunFrequency in ('4','1','2')";
            }


            return string.Format($@"SELECT [JobId]
                              FROM [PHPConfg].[dbo].[WORKER_HeartbeatJobs_C]
                                WHERE {whereStatment}
                                AND (StartTime <= '{twentyFour}' AND EndTime >= '{twentyFour}')
                                AND OnHold != 1
                                AND Owner = '{OwnerGroup}'");


        }

        /// <summary>
        /// Class corresponds to log record from the ULogger table
        /// </summary>
        private class JobLogRecord
        {
            public String JobIndex { get; set; }
            public DateTime LogDateTime { get; set; }
            public String LogCategory { get; set; }
            public String LoggedByUser { get; set; }
            public String LogContent { get; set; }
            public String UID { get; set; }
            public bool Remediated { get; set; }
            public String RemediationNote { get; set; }
        }

        /// <summary>
        /// Class to contain and sort the records found so they can be matched to a given job
        /// </summary>
        private class JobRecordMatch
        {
            public JobRecordMatch(List<JobLogRecord> completedJobs, JobIndex jobIndexData, DateTime? jobActualStartTime)
            {
                JobLogsFound = completedJobs.Where(i => i.JobIndex.ToUpper() == jobIndexData.JobIdOriginal.ToUpper() && i.LogDateTime > jobActualStartTime).ToList();
                CompletedJob = completedJobs.Where(i => i.JobIndex.ToUpper() == jobIndexData.JobIdOriginal.ToUpper()).FirstOrDefault();
                ErroredRecord = JobLogsFound.Find(i => i.LogCategory == "ERROR" && i.LogDateTime > jobActualStartTime);
            }

            /// <summary>
            /// All logs found for a given job since the given job start time
            /// </summary>
            public List<JobLogRecord> JobLogsFound { get; set; }
            /// <summary>
            /// Completion log found for the given job within the given timeframe
            /// </summary>
            public JobLogRecord CompletedJob { get; set; }
            /// <summary>
            /// Error log found for the given job within the given timeframe
            /// </summary>
            public JobLogRecord ErroredRecord { get; set; }
        }
    }


}
