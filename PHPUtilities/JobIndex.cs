using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Linq.Mapping;
using Utilities.Schedules;

namespace Utilities
{
    public class JobIndex : JobIndexBase
    {
        private JobIndexBase Job {get;set;}
        public JobIndex(string jobId)
        {
            if (jobId.Length > 7 && jobId.Substring(7, 1) == "_") //Ms. Data jobs
            {
                Job = GetJobData(jobId);
            }
            else
            {
                Job = GetJobData(new string(jobId.Take(7).ToArray()));
            }

            this.JobIdOriginal = jobId;
            if (Job != null)
            {
                this.JobId = Job.JobId;
                this.Title = Job.Title;
                this.BusinessValueDescription = Job.BusinessValueDescription;
                this.BusinessOwner = Job.BusinessOwner;
                this.Department = Job.Department;
                this.JobCoordinator = Job.JobCoordinator;
                this.Tool = Job.Tool;
                this.ProjectAssociation = Job.ProjectAssociation;
                this.TechnicalNotes = Job.TechnicalNotes;
                this.Status = Job.Status;
                this.RunType = Job.RunType;
                this.Frequency = Job.Frequency;
                this.OutboundData = Job.OutboundData;
                this.VendorRecipient = Job.VendorRecipient;
                this.SecurityPrivacyApproval = Job.SecurityPrivacyApproval;
                this.TransportMethod = Job.TransportMethod;
                this.ConsumesUploadedFile = Job.ConsumesUploadedFile;
                this.EpicIDNumber = Job.EpicIDNumber;
                this.PeerReviewer = Job.PeerReviewer;
                this.PeerReviewedDate = Job.PeerReviewedDate;
                this.ResponsibleTeam = Job.ResponsibleTeam;
                this.StandardPackage = Job.StandardPackage;
                this.RecoveryType = Job.RecoveryType;
                this.RecoveryDetails = Job.RecoveryDetails;
                this.MissionCriticalJob = Job.MissionCriticalJob;
                this.LastModifiedBy = Job.LastModifiedBy;
                this.LastModifiedDate = Job.LastModifiedDate;
                this.OwningGroup = Job.OwningGroup;
                this.PageOnError = Job.PageOnError;
                this.OnHold = Job.OnHold;
                this.Attachment = Job.Attachment;
                try
                {
                    this.DataSourceList = Job.DataSources.Split(',').ToList();
                }
                catch (NullReferenceException ex) //Only a single value or a null found, so split will fail
                {
                    this.DataSourceList = new List<string>();
                    this.DataSourceList.Add(Job.DataSources);
                }
            }
            else
            {
                this.JobId = jobId;
            }
        }

        private JobIndexBase GetJobData(string jobId)
        {
            JobIndexBase jd = ExtractFactory.ConnectAndQuery<JobIndexBase>(Data.AppNames.ExampleProd, GetJobInfoQuery(jobId)).FirstOrDefault();
            return jd;
        }

        public String JobIdOriginal { get; set; }
      
        public List<string> DataSourceList { get; set; }

        private List<Schedule> _Schedule { get; set; }
        public List<Schedule> Schedules
        {
            get 
            {
                List<Schedule> scheduleRecords = new List<Schedule>();
                if (_Schedule == null) //Lazy loading, but only once
                {
                    string env = Environment.GetEnvironmentVariable("DriverEnvironment");
                    Data.AppNames db = Data.AppNames.ExampleTest; //The schedule needs to come from the appropriate corresponding environment.
                    if (env == "PROD")
                    {
                        db = Data.AppNames.ExampleProd;
                    }

                    scheduleRecords = ExtractFactory.ConnectAndQuery<Schedule>(db, GetJobScheduleQuery(this.JobId)).ToList();

                    _Schedule = scheduleRecords;
                    return scheduleRecords;
                }
                else
                {
                    return _Schedule;
                }
            }
            set
            {

            }
        }
            
        private string GetJobInfoQuery(string jobId)
        {
            return $@"SELECT
                         *
                    FROM [PHPConfg].[CONTROLLER].[JobIndex_C]
                    WHERE JobId = '{jobId}'";
        }

        private string GetJobScheduleQuery(string jobId)
        {
            return $@"SELECT 
                         [ID]
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
                          FROM [PHPConfg].[dbo].[Controller_Schedule_C]
                          WHERE JobId like '{jobId}%'";
        }

        

        public class Schedule
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
            public string Cron_Month { get; set; } = "*";
            public string Cron_Day_Week { get; set; }
            
        }
    }

    public class JobIndexInfo
    {
        public static List<JobIndexBase> GetRunnableJobs(bool testMode)
        {
            Data.AppNames database = Data.AppNames.ExampleProd; //Only need to reference the job index in prod

            List<JobIndexBase> runnableJobs = ExtractFactory.ConnectAndQuery<JobIndexBase>(database, GetRunnableJobsQuery(testMode)).ToList();

            return runnableJobs;
        }

        public static List<JobIndexBase> GetJobsByServiceLine(string serviceLine, bool testMode)
        {
            Data.AppNames database = Data.AppNames.ExampleProd; //Only need to reference the job index in prod

            List<JobIndexBase> runnableJobs = ExtractFactory.ConnectAndQuery<JobIndexBase>(database, GetJobsByServiceLineQuery(serviceLine, testMode)).ToList();

            return runnableJobs;
        }

        public static List<JobIndexBase> GetJobsByTool(String tool, bool testMode = false)
        {
            Data.AppNames database = Data.AppNames.ExampleProd; //Only need to reference the job index in prod

            List<JobIndexBase> runnableJobs = ExtractFactory.ConnectAndQuery<JobIndexBase>(database, GetJobsByToolQuery(tool, testMode)).ToList();

            return runnableJobs;
        }


        private static string GetRunnableJobsQuery(bool isTest)
        {
            string jobStatus = "'In Service'";
            if (isTest)
            {
                jobStatus = "'In Service', 'In Development'";
            }

            return $@"SELECT *
                      FROM [PHPConfg].[CONTROLLER].[JobIndex_C]
                      WHERE Status IN ({jobStatus})
                      AND Tool NOT IN ('Crystal Reports','Tableau', 'SSIS')";
        }

        private static string GetJobsByServiceLineQuery(string serviceLine, bool isTest)
        {
            string jobStatus = "'In Service'";
            if (isTest)
            {
                jobStatus = "'In Service', 'In Development'";
            }
            
            return $@"SELECT *
                      FROM [PHPConfg].[CONTROLLER].[JobIndex_C]
                      WHERE Status IN ({jobStatus})                      
                      AND Tool NOT IN ('Crystal Reports','Tableau', 'SSIS')
                      AND [Owning Group] = '{serviceLine}'";
        }

        private static string GetJobsByToolQuery(string tool, bool isTest)
        {
            string jobStatus = "'In Service'";
            if (isTest)
            {
                jobStatus = "'In Service', 'In Development'";
            }

            return $@"SELECT *
                      FROM [PHPConfg].[CONTROLLER].[JobIndex_C]
                      WHERE Status IN ({jobStatus}) 
                      AND Tool = '{tool}'";
        }
    }

    [Table(Name = "JobIndex_C")]
    public class JobIndexBase
    {
        [Column(Name = "JobId")]
        public string JobId { get; set; }

        [Column(Name = "Title")]
        public string Title { get; set; }

        [Column(Name = "Business Value Description")]
        public string BusinessValueDescription { get; set; }

        [Column(Name = "Business Owner")]
        public virtual string BusinessOwner { get; set; }

        [Column(Name = "Department")]
        public string Department { get; set; }

        [Column(Name = "Job Coordinator")]
        public virtual string JobCoordinator { get; set; }

        [Column(Name = "Tool")]
        public string Tool { get; set; }

        [Column(Name = "Project Association")]
        public string ProjectAssociation { get; set; }

        [Column(Name = "Technical Notes")]
        public string TechnicalNotes { get; set; }

        [Column(Name = "Status")]
        public string Status { get; set; }

        [Column(Name = "Run Type")]
        public string RunType { get; set; }

        [Column(Name = "Frequency")]
        public string Frequency { get; set; }

        [Column(Name = "Outbound Data")]
        public bool OutboundData { get; set; }

        [Column(Name = "Vendor Recipient")]
        public string VendorRecipient { get; set; }

        [Column(Name = "Security Privacy Approval")]
        public string SecurityPrivacyApproval { get; set; }

        [Column(Name = "Transport Method")]
        public string TransportMethod { get; set; }

        [Column(Name = "Consumes Uploaded File")]
        public bool ConsumesUploadedFile { get; set; }

        [Column(Name = "Epic ID Number")]
        public string EpicIDNumber { get; set; }

        [Column(Name = "Peer Reviewer")]
        public string PeerReviewer { get; set; }

        [Column(Name = "Peer Reviewed Date")]
        public DateTime? PeerReviewedDate { get; set; }

        [Column(Name = "Responsible Team")]
        public string ResponsibleTeam { get; set; }

        [Column(Name = "Standard Package")]
        public bool StandardPackage { get; set; }

        [Column(Name = "Recovery Type")]
        public string RecoveryType { get; set; }

        [Column(Name = "Recovery Details")]
        public string RecoveryDetails { get; set; }

        [Column(Name = "Mission Critical Job")]
        public bool MissionCriticalJob { get; set; }

        [Column(Name = "Last Modified By")]
        public string LastModifiedBy { get; set; }

        [Column(Name = "Last Modified Date")]
        public DateTime? LastModifiedDate { get; set; }

        [Column(Name = "Owning Group")]
        public string OwningGroup { get; set; }

        [Column(Name = "Data Sources")]
        public string DataSources { get; set; }

        [Column(Name = "Page On Error")]
        public bool PageOnError { get; set; }

        [Column(Name = "On Hold")]
        public bool OnHold { get; set; }

        [Column(Name = "Attachment")]
        public bool Attachment { get; set; }

    }

    public class JobIndexRunRequest : JobIndexBase
    {
        public JobCurrentStatus RunStatus { get; set; }
        public List<string> DataSourceList { get; set; }
        public Boolean RunnableByThisUser { get; set; }
        public string recoveryInfo { get; set; }
    }

    public enum JobCurrentStatus
    {
        Queued, Started, Finished, Errored, None
    }

    public class JobIndexTableauDashBoard : JobIndex
    {
        public JobIndexTableauDashBoard(string jobId) : base(jobId)
        {
            person jobCoord = new person();
            person busOwner = new person();

            try
            {
                if (this.JobCoordinator == "Not Available" || this.JobCoordinator == null || this.JobCoordinator == "")
                {
                    jobCoord.firstName = "NA";
                    jobCoord.lastName = "NA";
                }
                else
                {
                    int jobCoordEmailSplitIndex = this.JobCoordinator.IndexOf("@");
                    if (jobCoordEmailSplitIndex > 0)
                    {
                        string fullName = this.JobCoordinator.Substring(0, jobCoordEmailSplitIndex).Trim();
                        jobCoord.firstName = fullName.Substring(0, fullName.IndexOf(".")).Trim();
                        jobCoord.lastName = fullName.Substring(fullName.IndexOf(".") + 1, fullName.Length - (fullName.IndexOf(".") + 1));
                    }
                    else
                    {
                        int jobCoordSplitIndex = this.JobCoordinator.IndexOf(",");
                        jobCoord.firstName = this.JobCoordinator.Substring(jobCoordSplitIndex + 1, this.JobCoordinator.Length - jobCoordSplitIndex - 1).Trim();
                        jobCoord.lastName = this.JobCoordinator.Substring(0, jobCoordSplitIndex);
                    }
                }
            }
            catch (Exception ex)
            {
                jobCoord.firstName = "NA";
                jobCoord.lastName = "NA";
            }

            try
            {
                if (this.BusinessOwner == "Not Available" || this.BusinessOwner == null || this.BusinessOwner == "")
                {
                    busOwner.firstName = "NA";
                    busOwner.lastName = "NA";
                }
                else
                {
                    int busOwnerEmailSplitIndex = this.BusinessOwner.IndexOf("@");
                    if (busOwnerEmailSplitIndex > 0)
                    {
                        string fullName = this.BusinessOwner.Substring(0, busOwnerEmailSplitIndex).Trim();
                        busOwner.firstName = fullName.Substring(0, fullName.IndexOf(".")).Trim();
                        busOwner.lastName = fullName.Substring(fullName.IndexOf(".") + 1, fullName.Length - (fullName.IndexOf(".") + 1));
                    }
                    else
                    {
                        int busOwnerSplitIndex = this.BusinessOwner.IndexOf(",");
                        busOwner.firstName = this.BusinessOwner.Substring(busOwnerSplitIndex + 1, this.BusinessOwner.Length - busOwnerSplitIndex - 1).Trim();
                        busOwner.lastName = this.BusinessOwner.Substring(0, busOwnerSplitIndex);
                    }
                }
            }
            catch (Exception ex)
            {
                busOwner.firstName = "NA";
                busOwner.lastName = "NA";
            }
            

            this.JobCoordinatorPerson = jobCoord;
            this.BusinessOwnerPerson = busOwner;
        }

        public person JobCoordinatorPerson { get; set; }
        public person BusinessOwnerPerson { get; set; }

        public class person
        {
            public string firstName { get; set; }
            public string lastName { get; set; }
            public string userName { get; set; }
            public string email { get; set; }
        }
    }


}
