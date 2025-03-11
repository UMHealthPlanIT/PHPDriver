using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Utilities;
using Utilities.Integrations;
using Utilities.Schedules;

namespace RunRequest.Models
{
    public class JobHistory
    {
        public JobSummaryDetails JobDetails { get; set; }
        public string JobCode { get; set; }
        public PagedList.IPagedList<DailyScheduleRecord> History { get; set; }
    }
}