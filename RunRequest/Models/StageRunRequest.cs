using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Utilities;
using Utilities.Schedules;
using System.DirectoryServices.AccountManagement;

namespace RunRequest.Models
{
    public class StageRunRequest
    {
        /// <summary>
        /// Loads job launch request to JobDailySchedule table to be launched at the next Worker heartbeat
        /// </summary>
        /// <param name="jobId">Job Identifier from the Job Index</param>
        /// <param name="accessingUser"></param>
        /// <param name="testMode"></param>
        /// <param name="owner"></param>
        public static void LoadRunRequestToController(String jobId, String accessingUser, Boolean testMode, string owner = "Sparrow")
        {
            string[] jobAndParms = jobId.Split(' ');
            string jobIndex = jobAndParms[0];
            if (jobAndParms[0] == "Mr_Data")
            {
                jobIndex = jobAndParms[1];
            }
            else if (jobAndParms[0] == "Ms_Data")
            {
                jobIndex = jobAndParms[1];
            }

            DailyScheduleRecord program = new DailyScheduleRecord();

            program.JobId = jobIndex;
            program.ScheduledStartTime = DateTime.Now;
            program.RequestedBy = accessingUser;
            program.ScheduleStatus = "Active";
            program.Environment = testMode ? "T" : "P";
            program.Parameters = String.Join(",", jobAndParms);

            try
            {
                program.InsertRecord(testMode, owner);
                Services.LoggingService.WriteToLog("WEB0004", DateTime.Now, accessingUser, "Loaded Request to Run Job " + jobId + " to JobDailySchedule Table", "INFO");
            }
            catch (Exception ex)
            {
                Services.LoggingService.WriteToLog("WEB0004", DateTime.Now, accessingUser, "Error Loading Job " + jobId + " to JobDailySchedule table" + Environment.NewLine + ex.ToString(), "ERROR");
            }
        }
    }
}