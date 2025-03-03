using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Utilities.Schedules;

namespace RunRequest.Models
{
    public class ScheduledJob
    {
        public DateTime? ScheduledStartTime { get; set; }
        public int ScheduleId { get; set; }
        public String JobStatusReason { get; set; }
        public String FinalDisposition { get; set; }
        public String JobOutcome { get; set; }
        public String ScheduleStatus { get; set; }
        public DateTime QueuedTime { get; set; }
        public DateTime ActualStartTime { get; set; }
        public String RunStatus { get; set; }
        public String RequestedBy { get; set; }
        public String Environment { get; set; }
        public String JobId { get; set; }
        public String HasNotes { get; set; }
        public String Owner { get; set; }
        public String Parameters { get; set; }
    }

    public class RunJob
    {
        public String JobIndex { get; set; }
        public DateTime? LogDateTime { get; set; }
        public String Owner { get; set; }
    }

    public class LogRecord
    {
        public String JobIndex { get; set; }
        public DateTime LogDateTime { get; set; }
        public String LogCategory { get; set; }
        public String LoggedByUser { get; set; }
        public String LogContent { get; set; }
        public String UID { get; set; }
        public Boolean Remediated { get; set; }
        public String RemediationNote { get; set; }
        public DateTime? ScheduledStartTime { get; set; }
        public String Owner { get; set; }
    }

    public class JobNotes
    {
        public int NoteIndex { get; set; }
        public string JobIndex { get; set; }
        public DateTime JobRunDate { get; set; }
        public DateTime NoteDateTime { get; set; }
        public string AdminUser { get; set; }
        public string NoteText { get; set; }
    }
}