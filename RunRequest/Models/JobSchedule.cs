using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Utilities;
using Utilities.Integrations;

namespace RunRequest.Models
{
    public class JobSchedule
    {
        public string JobId { get; set; }
        public DateTimeOffset? NextRun { get; set; }
        public string NoSchedule { get; set; }
        public PagedList.IPagedList<JobSchedule_C> jobSchedules { get; set; }
        public string RunType { get; set; }
        public string Tool { get; set; }
    }
}