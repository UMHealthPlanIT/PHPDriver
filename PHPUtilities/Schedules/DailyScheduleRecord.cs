using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Utilities.Schedules
{
    public class DailyScheduleRecord
    {
        public int Id { get; set; }
        public string JobId { get; set; }
        public int ScheduleId { get; set; }
        public DateTime? ScheduledStartTime { get; set; }
        public string JobStatusReason { get; set; } = "";
        public string FinalDisposition { get; set; }
        public string JobOutcome { get; set; }
        public DateTime? ActualEndTime { get; set; }
        public string ScheduleStatus { get; set; }
        public DateTime? ActualStartTime { get; set; }
        public DateTime? QueuedTime { get; set; }
        public string RunStatus { get; set; }
        public string RequestedBy { get; set; }
        public string Owner { get; set; }
        public string Environment { get; set; }
        public string Parameters { get; set; }
        public bool IsHeartBeatJob { get; set; } = false;

        public void UpdateRecord(bool testMode, string ownerGroup)
        {
            Logger querylogger = new Logger("DailyScheduleRecord", testMode);
            querylogger.WriteToLog(UpdateQuery(ownerGroup));
            DataWork.RunSqlCommand(UpdateQuery(ownerGroup), GetArchiveDatabase(testMode));
        }

        public void RecordStartedJob(bool testMode, string ownerGroup)
        {
            Logger querylogger = new Logger("DailyScheduleRecord", testMode);
            querylogger.WriteToLog(StartJobQuery(ownerGroup));
            DataWork.RunSqlCommand(StartJobQuery(ownerGroup), GetArchiveDatabase(testMode));
        }

        public void InsertRecord(bool testMode, string ownerGroup)
        {
            Logger querylogger = new Logger("DailyScheduleRecord", testMode);
            querylogger.WriteToLog(WriteNewRecordQuery(ownerGroup));
            DataWork.RunSqlCommand(WriteNewRecordQuery(ownerGroup), GetArchiveDatabase(testMode));
        }

        private string UpdateQuery(string ownerGroup)
        {
            DateTime? nullCheckDate = new DateTime();
            string actualEndTime = "null";
            string queuedTime = "null";
            string jobStatusReason;
            try
            {
                jobStatusReason = this.JobStatusReason.Replace("'", "''");
            }
            catch(NullReferenceException ex)
            {
                jobStatusReason = "";
            } 
            string groupTable = "";
            if (ownerGroup != "ALL")
            {
                groupTable = ownerGroup + "_";            
            }

            if (this.ActualEndTime != nullCheckDate)
            {
                actualEndTime = $"'{this.ActualEndTime}'";
            }

            if (this.QueuedTime != nullCheckDate)
            {
                queuedTime = $"'{this.QueuedTime}'";
            }

            return $@"UPDATE dbo.CONTROLLER_{groupTable}JobDailySchedule_A
                        SET 
                            [JobStatusReason] = '{jobStatusReason}'
                            ,[FinalDisposition] = '{this.FinalDisposition}'
                            ,[JobOutcome] = '{this.JobOutcome}'
                            ,[ActualEndTime] = {actualEndTime}
                            ,[ScheduleStatus] = '{this.ScheduleStatus}'
                            ,[RunStatus] = '{this.RunStatus}'
                        WHERE Id = {this.Id}";
        }

        private string WriteNewRecordQuery(string ownerGroup)
        {
            string groupTable = "";
            if (ownerGroup != "ALL")
            {
                groupTable = ownerGroup + "_";
            }
            return $@"INSERT INTO dbo.CONTROLLER_{groupTable}JobDailySchedule_A 
                                  ([JobId]
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
                                  ,[Parameters])
                              VALUES('{this.JobId}', '{this.ScheduleId}', '{this.ScheduledStartTime}', null, null, null, null, '{this.ScheduleStatus}', null, null, null, '{this.RequestedBy}', '{ownerGroup}', '{this.Environment}', '{this.Parameters}')";
        }

        private string StartJobQuery(string ownerGroup)
        {
            DateTime? nullCheckDate = new DateTime();
            string actualStartTime = "null";
            string queuedTime = "null";
            string groupTable = "";
            if (ownerGroup != "ALL")
            {
                groupTable = ownerGroup + "_";
            }

            if (this.QueuedTime != nullCheckDate)
            {
                queuedTime = $"'{this.QueuedTime}'";
            }
            if (this.ActualStartTime != nullCheckDate)
            {
                actualStartTime = $"'{this.ActualStartTime}'";
            }
            return $@"UPDATE dbo.CONTROLLER_{groupTable}JobDailySchedule_A
                        SET                             
                            [ActualStartTime] = {actualStartTime}
                            ,[QueuedTime] = {queuedTime}
                            ,[RunStatus] = '{this.RunStatus}'
                            ,[ActualEndTime] = NULL
                        WHERE Id = {this.Id}";
        }

        private Data.AppNames GetArchiveDatabase(bool testMode)
        {
            Data.AppNames database;
            if (testMode)
            {
                database = Data.AppNames.ExampleTest;
            }
            else
            {
                database = Data.AppNames.ExampleProd;
            }
            return database;
        }
    }
}
