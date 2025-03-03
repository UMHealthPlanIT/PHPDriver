using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace RunRequest.Models
{
    public class JobSchedule_C
    {
        public int ID { get; set; }
        public string JobId { get; set; }
        public string Owner { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool OnHold { get; set; }
        public string Parameters { get; set; }
        public string Cron_Minute { get; set; }
        public string Cron_Hour { get; set; }
        public string Cron_Day_Month { get; set; }
        public string Cron_Month { get; set; }
        public string Cron_Day_Week { get; set; }
        public string Cron_Expression { get; set; }
        public string HumanExpression { get; set; }
        public DateTimeOffset? NextRun { get; set; }
        public string RowClass {get; set;}

    }
}