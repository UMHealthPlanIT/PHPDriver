using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utilities;
using Cronos;
using Utilities.Schedules;


namespace PHPController
{
    public class Scheduling
    {
        /// <summary>
        /// Populate the daily tracking table with the jobs that are scheduled to run today.
        /// </summary>
        /// <param name="procLog">Logger object that lets us determine environment (TEST or PROD) and allows us to log with the calling class.</param>
        /// <param name="ownerGroup">Group that owns this particular server and process (Sparrow or PHP).</param>
        public static void PopulateDailySchedule(Logger procLog, string ownerGroup)
        {
            List<JobSchedule> results = GetDaysSchedule(DateTime.Today, procLog);

            procLog.WriteToLog("Found " + results.Count() + " items with next schedule run date of today");

            foreach (JobSchedule itm in results)
            {
                bool addThisRecord = false;
                if (ownerGroup.ToUpper() == "ALL")
                {
                    addThisRecord = true;
                }
                else if (itm.Owner == ownerGroup)
                {
                    addThisRecord = true;
                }

                if (addThisRecord)
                {
                    DailyScheduleRecord programs = new DailyScheduleRecord();
                    programs.JobId = itm.JobId;
                    programs.ScheduleId = itm.ScheduleId;
                    programs.ScheduledStartTime = itm.NextRun;
                    programs.RequestedBy = "Schedule";
                    programs.Environment = (procLog.TestMode ? "T" : "P");
                    programs.Owner = itm.Owner;
                    programs.Parameters = itm.JobId;
                    if (itm.Parameters != null && itm.Parameters.Length > 0)
                    {
                        programs.Parameters += "," + itm.Parameters;
                    }

                    if (itm.OnHold)
                    {
                        programs.ScheduleStatus = "On Hold";
                    }
                    else
                    {
                        programs.ScheduleStatus = "Active";
                    }

                    try
                    {
                        programs.InsertRecord(procLog.TestMode, ownerGroup);
                    }
                    catch (Exception ex)
                    {
                        procLog.WriteToLog("Error inserting record for " + programs.JobId + ". " + ex.ToString(), UniversalLogger.LogCategory.ERROR);
                    }
                }
            }
        }

        /// <summary>
        /// Retrieve list of records of jobs scheduled to run today.
        /// </summary>
        /// <param name="scheduleDate">Date for the schedule that is to be retrieved.</param>
        /// <param name="procLog"></param>
        /// <returns></returns>
        private static List<JobSchedule> GetDaysSchedule(DateTime scheduleDate, Logger procLog)
        {
            Data.AppNames db = (procLog.TestMode ? Data.AppNames.ExampleTest : Data.AppNames.ExampleProd);

            string scheduleDateString = @"SELECT 
                                           [ID] as ScheduleId
                                          ,[JobId]
                                          ,[Owner]
                                          ,[StartDate]
                                          ,[EndDate]
                                          ,[OnHold]
                                          ,[Parameters]
                                          ,[Cron_Minute]
                                          ,[Cron_Hour]
                                          ,[Cron_Day_Month]
                                          ,[Cron_Month]
                                          ,[Cron_Day_Week]
FROM[PHPConfg].[dbo].[Controller_Schedule_C] WHERE '" + scheduleDate.ToString("yyyy-MM-dd") + "' BETWEEN StartDate AND EndDate AND Cron_Day_Month <> 'OTHER'";

            List<JobSchedule> fullTable = ExtractFactory.ConnectAndQuery<JobSchedule>(db, scheduleDateString).ToList();

            List<JobSchedule> todaysSchedule = new List<JobSchedule>();


            foreach (JobSchedule jobSched in fullTable)
            {
                try
                {
                    if (jobSched.NextRun.Date == scheduleDate.Date)
                    {
                        todaysSchedule.Add(jobSched);
                    }
                }
                catch (Exception ex)
                {
                    procLog.WriteToLog("The schedule for the following job had Cron errors: " + jobSched.JobId + "  " + ex.ToString(), UniversalLogger.LogCategory.WARNING);
                }
            }

            return todaysSchedule;
        }
        
    }

    /// <summary>
    /// Class that corresponds to the records in the Daily Tracking table for managing jobs running on a given day
    /// </summary>
    public class JobSchedule
    {
        public DateTime ScheduleDate
        {
            get
            {
                return DateTime.Today;
            }
            set
            {

            }
        }
        public int ID { get; set; }
        public string JobId { get; set; }
        public int ScheduleId { get; set; }
        public string Owner { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool OnHold { get; set; }
        public string Parameters { get; set; }
        public string Cron_Minute { get; set; }
        public string Cron_Hour { get; set; }
        public string Cron_Day_Month { get; set; }
        public string Cron_Month { get; set; } = "*";
        public string Cron_Day_Week { get; set; }
        public DateTime NextRun
        {
            get
            {
                DateTime next = new DateTime();
                double daysSinceStart = (DateTime.Today - StartDate).TotalDays;

                if (this.Cron_Day_Month.Substring(0, 1) != "#") //Is this an 'every n weeks' schedule?
                {
                    string crn = this.Cron_Minute + " " + this.Cron_Hour + " " + this.Cron_Day_Month + " " + this.Cron_Month + " " + this.Cron_Day_Week;
                    CronExpression expression = CronExpression.Parse(crn);
                    DateTimeOffset startOfDay = new DateTimeOffset(ScheduleDate);
                    next = Convert.ToDateTime(expression.GetNextOccurrence(startOfDay, TimeZoneInfo.Local)?.DateTime);
                }
                else
                {
                    int intervalDays = Convert.ToInt16(this.Cron_Day_Month.Substring(1, 1)) * 7;

                    if (StartDate == DateTime.Today || daysSinceStart % intervalDays == 0)
                    {
                        next = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, Convert.ToInt16(this.Cron_Hour), Convert.ToInt16(this.Cron_Minute), 0);
                    }
                    else
                    {
                        double numberOfRunsSinceStart = daysSinceStart / intervalDays;
                        double numberOfDaysFromStartToLast = Math.Truncate(numberOfRunsSinceStart) * intervalDays;
                        DateTime lastRunTest = StartDate.AddDays(numberOfDaysFromStartToLast);
                        DateTime nextRunDate = lastRunTest.AddDays(intervalDays);

                        next = new DateTime(nextRunDate.Year, nextRunDate.Month, nextRunDate.Day, Convert.ToInt16(this.Cron_Hour), Convert.ToInt16(this.Cron_Minute), 0);
                    }
                }

                return next;
            }
            set
            {

            }
        }
    }
}
