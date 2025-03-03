using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;

namespace RunRequest.Models
{
    public class JobSummaryDetails
    {
        [Display(Name = "Job Code")]
        public string jobCode { get; set; }
        [Display(Name = "Title")]
        public string title { get; set; }
        public string tool { get; set; }
        public string toolIcon { get; set; }
        public List<Departments> Department { get; set; }
        public string Format { get; set; }
        public string JobType { get; set; }
        public String UserCanRun { get; set; }
        public JobCurrentStatus RunStatus { get; set; } = JobCurrentStatus.None;
        public Boolean consumesInboundFiles { get; set; }
        public string description { get; set; }
        public List<JobParameter> Parameters { get; set; }
        public List<string> dataSource { get; set; }
        public string devStatus { get; set; }
        public bool Attachments { get; set; }
        public string RunType { get; set; }
    }

    public class Departments
    {
        public string DepartmentProperName { get; set; }
        public string DepartmentNoSpaces { get; set; }
    }

    public enum JobCurrentStatus
    {
        Queued, Started, Finished, Errored, OnHold, None, DataSourceNotReady
    }
}