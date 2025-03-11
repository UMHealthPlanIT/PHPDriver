using Cronos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PagedList;
using Utilities;
using Utilities.Schedules;
using RunRequest.Models;
using RunRequest.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Web.Configuration;
using System.Web.Mvc;
using System.Web.UI;

namespace RunRequest.Controllers
{
    public class HomeController : Controller
    {
        public static readonly bool testMode = Convert.ToBoolean(WebConfigurationManager.AppSettings["testMode"]);

        private static List<DailyScheduleRecord> mostRecentRunRecordDictionary;
        
        /// <summary>
        /// Index for Run Request
        /// </summary>
        /// <param name="overrideServiceLine">Use this to switch between the default and PHP left-hand filter controls</param>
        /// <param name="jobIndex">Providing this value will render that single job in the page, and activat the launch dialog</param>
        /// <param name="ForIframe">Providing this in addition to the jobIndex argument will render the launch dialog in a single frame - for use in an iFrame</param>
        /// <returns>Main Home view</returns>
        public ActionResult Index(string overrideServiceLine = "", string jobIndex = "", bool ForIframe = false)
        {
            
            string databasePointer = testMode ? "PhpConfgTest" : "PhpConfgProd";
            
            string serviceLine;

            if (overrideServiceLine == "")
            {
                serviceLine = GetServiceLineFromUser(User);
            }
            else
            {
                serviceLine = overrideServiceLine;
                System.Web.HttpContext.Current.Session["serviceLine"] = serviceLine; //this serviceLine session variable is also being set in GetServiceLineFromUser
            }

            ViewBag.ServiceLine = serviceLine;
            ViewBag.TableauTitlePage = false;
            List<Models.JobSummaryDetails> jobDetails;

            ViewBag.JobDirect = false;
            if (jobIndex != "")
            {
                jobDetails = JobDetailsManager.GetJobDetails(User, jobIndex);
                ViewBag.JobDirect = true;
            }
            else
            {
                jobDetails = JobDetailsManager.GetJobDetails(User);
                jobDetails = jobDetails.OrderBy(x => x.title).ToList();// .Sort();
            }
            
            
            if (jobDetails.Count(x => x.UserCanRun == "JobsICanRun") == 0)
            {
                ViewBag.UserCanRunJobs = false;
            }
            else
            {
                ViewBag.UserCanRunJobs = true;
            }

            if (serviceLine == "PHP")
            {
                PopulatePHPLeftNav(jobDetails);
            }

            Session["dataSourceReadinessList"] = new Tuple<Dictionary<string, string>, DateTime>(Utilities.DataSourceManagement.SourceStatus(testMode), DateTime.Now);

            if (jobIndex != "" && ForIframe)
            {
                return PartialView("iFrameLaunchDialog", jobDetails[0]);
            }
            else
            {
                string WebIndex = "WEB0004";
                string logContent = string.Format("User {0} accessed RunRequest in {2} Mode. {1} Service Line", this.User.Identity.Name, serviceLine, testMode ? "Test" : "Prod");
                LoggingService.WriteToLog(WebIndex, DateTime.Now, UserPrincipal.Current.UserPrincipalName, logContent);

                return View(jobDetails);
            }
        }
        
        private void PopulatePHPLeftNav(List<Models.JobSummaryDetails> jobDetails)
        {

            List<Models.Departments> deduplicatedDepartments = new List<Models.Departments>();
            List<List<Models.Departments>> availableDepts = jobDetails.Select(x => x.Department).ToList();

            foreach (List<Models.Departments> listofDepts in availableDepts)
            {
                foreach (Models.Departments dep in listofDepts)
                {
                    if (!deduplicatedDepartments.Exists(x => x.DepartmentProperName == dep.DepartmentProperName) && dep.DepartmentProperName.StartsWith("PHP"))
                    {
                        if (dep.DepartmentProperName != "PHP")
                        {
                            deduplicatedDepartments.Add(dep);
                        }

                    }
                }
            }

            ViewBag.FirstFilter = "Departments";
            ViewBag.DistinctDepartments = deduplicatedDepartments.OrderBy(x => x.DepartmentProperName);
        }
       
        private static string GetServiceLineFromUser(IPrincipal accessingUser)
        {
            PrincipalContext ctx = new PrincipalContext(ContextType.Domain, "");
            GroupPrincipal developers = GroupPrincipal.FindByIdentity(ctx, "");


            UserPrincipal user = UserPrincipal.FindByIdentity(ctx, accessingUser.Identity.Name);

            string serviceLine;
            if (user.IsMemberOf(developers))
            {
                serviceLine = "DEV";
            }
            else
            {
                serviceLine = "SHS";
            }
            System.Web.HttpContext.Current.Session["serviceLine"] = serviceLine;
            return serviceLine;
        }

        private static List<Models.JobSummaryDetails> GetRunnableJobsIndex(String serviceLine, String userAccount, String jobOverride = "")
        {

            List<JobIndexRunRequest> sampleSet;
            List<JobIndexRunRequest> jobList = new List<JobIndexRunRequest>();

            string jobOverrideFilter = (jobOverride == "" ? "" : "&jobOverride=" + jobOverride);

            //Get all jobs for PHP
            if (serviceLine == "PHP")
            {
                sampleSet = SharedServices.callDataApi<List<JobIndexRunRequest>>("SharePoint/GetServiceLineJobsApi?serviceLine=PHP&callingUser=" + userAccount + jobOverrideFilter).ToList();
                System.Diagnostics.Debug.WriteLine("SharePoint/GetServiceLineJobsApi?serviceLine=PHP&callingUser=" + userAccount + jobOverrideFilter);
                List<JobIndexRunRequest> subSampleSet = SharedServices.callDataApi<List<JobIndexRunRequest>>("SharePoint/GetRunnableJobsForUser?callingUser=" + userAccount + jobOverrideFilter).ToList();
                System.Diagnostics.Debug.WriteLine("SharePoint/GetRunnableJobsForUser?callingUser=" + userAccount + jobOverrideFilter);
                subSampleSet = subSampleSet.Where(x => x.RunnableByThisUser).ToList();
                sampleSet.AddRange(subSampleSet.Where(x => !sampleSet.Any(y => y.JobId == x.JobId)));
            }
            //Get all jobs that are not HR or PHP
            else
            {
                sampleSet = SharedServices.callDataApi<List<JobIndexRunRequest>>("SharePoint/GetRunnableJobsForUser?callingUser=" + userAccount + jobOverrideFilter).ToList();
                System.Diagnostics.Debug.WriteLine("SharePoint/GetRunnableJobsForUser?callingUser=" + userAccount + jobOverrideFilter);

                if (serviceLine != "DEV")
                {
                    sampleSet = sampleSet.Where(x => x.RunnableByThisUser).ToList();
                }
            }

            List<Models.JobSummaryDetails> abbreviatedSummary = new List<Models.JobSummaryDetails>();

            foreach (JobIndexRunRequest rawJobsData in sampleSet)
            {
                Models.JobSummaryDetails jobSummary = new Models.JobSummaryDetails();
                jobSummary.jobCode = rawJobsData.JobId;
                if (rawJobsData.Title == null)
                {
                    jobSummary.title = "";
                }
                else
                {
                    jobSummary.title = (rawJobsData.Title.Length > 60 ? rawJobsData.Title.Truncate(60) + "..." : rawJobsData.Title);
                }
                jobSummary.tool = rawJobsData.Tool;
                jobSummary.Format = GetFormat(rawJobsData);

                List<Models.Departments> departmentsWithoutSpaces = new List<Models.Departments>();                
                Models.Departments depart = new Models.Departments();
                depart.DepartmentProperName = rawJobsData.Department;
                depart.DepartmentNoSpaces = rawJobsData.Department.Replace(" ", "");
                departmentsWithoutSpaces.Add(depart);

                jobSummary.JobType = GetJobType(rawJobsData);
                jobSummary.UserCanRun = (rawJobsData.RunnableByThisUser ? "JobsICanRun" : "UnauthorizedToRun");
                jobSummary.consumesInboundFiles = rawJobsData.ConsumesUploadedFile;
                jobSummary.description = rawJobsData.BusinessValueDescription;
                jobSummary.dataSource = rawJobsData.DataSourceList;
                jobSummary.devStatus = rawJobsData.Status;

                jobSummary.Attachments = rawJobsData.Attachment;
                
                jobSummary.RunType = rawJobsData.RunType;


 
                jobSummary.Department = departmentsWithoutSpaces;

                if (jobSummary.tool == "Tableau")
                {
                    jobSummary.toolIcon = "fa-tachometer";

                }
                else if (rawJobsData.OutboundData)
                {
                    jobSummary.toolIcon = "fa-cogs";
                }
                else if (jobSummary.tool == "Crystal Reports")
                {
                    jobSummary.toolIcon = "fa-bar-chart-o";
                }
                else if (jobSummary.tool == ".Net-MrData")
                {
                    jobSummary.toolIcon = "fa-user-md";
                }
                else if (jobSummary.tool == ".Net-MsData")
                {
                    jobSummary.toolIcon = "fa-arrows-h";
                }
                else if (jobSummary.tool == ".Net-WebReport" || jobSummary.tool == "SAS Web Report" || jobSummary.tool == ".Net-MrData-WebReport")
                {
                    jobSummary.toolIcon = "fa-cloud-download";
                }
                else if (jobSummary.tool == ".Net-IT Tool")
                {
                    jobSummary.toolIcon = "fa-wrench";
                }
                else
                {
                    jobSummary.toolIcon = "fa-file-text";
                }

                abbreviatedSummary.Add(jobSummary);
            }
            return abbreviatedSummary.ToList();
        }
        
        private static string GetJobType(JobIndexRunRequest rawJobsData)
        {
            if (rawJobsData.Tool == ".Net-MrData" || rawJobsData.Tool == ".Net-MsData" || rawJobsData.Tool == ".Net" || rawJobsData.Tool == ".Net-IT Tool")
            {
                return "DotNet";
            }
            else if (rawJobsData.Tool == ".Net-WebReport" || rawJobsData.Tool == ".Net-MrData-WebReport")
            {
                return "WebReport";
            }
            else
            {
                return rawJobsData.Tool.Replace(" ", "").Trim();
            }
        }
        
        private static string GetFormat(JobIndexRunRequest rawJobsData)
        {
            if (rawJobsData.OutboundData)
            {
                return "OutboundData";
            }
            else if (rawJobsData.Tool.ToUpper().Contains("TABLEAU"))
            {
                return "Tableau";
            }
            else
            {
                return "Excel";
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="jobId"></param>
        /// <param name="dtl"></param>
        /// <returns></returns>
        [HttpGet]
        public bool GetJobDataSourceStatus(string jobId, Models.JobSummaryDetails dtl = null)
        {
            if ((Session["dataSourceReadinessList"] == null) || (((Tuple<Dictionary<string, string>, DateTime>)Session["dataSourceReadinessList"]).Item2 <= DateTime.Now.AddSeconds(-30)))
            {
                Session["dataSourceReadinessList"] = new Tuple<Dictionary<string, string>, DateTime>(Utilities.DataSourceManagement.SourceStatus(testMode), DateTime.Now);
            }

            string readyStatus;

            if (((Tuple<Dictionary<string, string>, DateTime>)Session["dataSourceReadinessList"]).Item1.TryGetValue("Master", out readyStatus))
            {
                if (readyStatus.ToUpper() == "NO")
                {
                    return false;
                }
            }
            try
            {
                List<string> dataSourcesForJob;
                if(dtl.dataSource == null)
                {
                    List<Models.JobSummaryDetails> jobSumDtls = JobDetailsManager.GetJobDetails(User);
                    dataSourcesForJob = jobSumDtls.Where(x => x.jobCode == jobId).First().dataSource;

                }
                else
                {
                    dataSourcesForJob = dtl.dataSource;
                }
               
                foreach (String DS in dataSourcesForJob)
                {
                    if (((Tuple<Dictionary<string, string>, DateTime>)Session["dataSourceReadinessList"]).Item1.TryGetValue(DS, out readyStatus))
                    {
                        if (readyStatus.ToUpper() == "NO")
                        {
                            return false;
                        }
                    }

                }
            } catch(Exception e)
            {
                Services.LoggingService.WriteToLog("WEB0004", DateTime.Now, User.Identity.Name, "Job ID is " + jobId + Environment.NewLine + e.ToString(), "ERROR");
            }

            return true;
        }

        /// <summary>
        /// Pulls a job's current run status from the JobDailySchedule table
        /// </summary>
        /// <param name="jobId">Job to Check the Status Of</param>
        /// <param name="fromIndex">Pass true when calling from the index page (i.e. when we've just pulled this data already) versus later via the timer or on-click of a particular job</param>
        /// <param name="dtl">Job Index Details - used to skip sharepoint calls (I think)</param>
        /// <returns></returns>
        [HttpGet]        
        public Models.JobCurrentStatus GetJobsCurrentRunStatus(String jobId, bool fromIndex, Models.JobSummaryDetails dtl = null)
        {
            Models.JobCurrentStatus currentJobStatus = Models.JobCurrentStatus.None;

            try
            {
                DailyScheduleRecord mostRecentRunRecord = null;

                Data.AppNames db;
                if (!testMode)
                {
                    db = Data.AppNames.ExampleProd;
                }
                else
                {
                    db = Data.AppNames.ExampleTest;
                }

                mostRecentRunRecord = ExtractFactory.ConnectAndQuery<DailyScheduleRecord>(db, "SELECT * FROM [PHPConfg].[dbo].[WEB0004_MostRecentRunsFromToday_V]").Where(b => b.JobId.Contains(jobId) && b.ScheduledStartTime >= DateTime.Today).OrderByDescending(x => x.ScheduledStartTime).FirstOrDefault();


                if (!GetJobDataSourceStatus(jobId, dtl))
                {
                    currentJobStatus = Models.JobCurrentStatus.DataSourceNotReady;
                }
                else
                if (mostRecentRunRecord == null)
                {
                    currentJobStatus = Models.JobCurrentStatus.None;
                }
                else
                {
                    currentJobStatus = TranslateStatus(mostRecentRunRecord);
                }
            } catch(Exception exc)
            {
                Services.LoggingService.WriteToLog("WEB0004", DateTime.Now, User.Identity.Name, "Job ID is " + jobId + Environment.NewLine + exc.ToString(), "ERROR");
            }

            return currentJobStatus;
        }

        private static Models.JobCurrentStatus TranslateStatus(DailyScheduleRecord mostRecentRunRecord)
        {
            Models.JobCurrentStatus jobStatus = Models.JobCurrentStatus.None;

            if (mostRecentRunRecord.ScheduleStatus == "On Hold")
            {
                jobStatus = Models.JobCurrentStatus.OnHold;
            }
            if (mostRecentRunRecord.RunStatus == "Q" || mostRecentRunRecord.RunStatus == null)
            {
                jobStatus = Models.JobCurrentStatus.Queued;
            }
            else if (mostRecentRunRecord.RunStatus == "S")
            {
                //For now, this is how I'm identifying web reports. We don't want to force web reports to wait 15 minutes to rerun.
                if (mostRecentRunRecord.ScheduleId == 0 && mostRecentRunRecord.QueuedTime == null && mostRecentRunRecord.RequestedBy.ToUpper().Contains(@"SPARROW\")) 
                {
                    jobStatus = Models.JobCurrentStatus.None;
                }
                else
                {
                    jobStatus = Models.JobCurrentStatus.Started;
                }
            }
            else if (mostRecentRunRecord.RunStatus == "F")
            {
                if (mostRecentRunRecord.JobOutcome == "ERROR")
                {
                    if (mostRecentRunRecord.ScheduleId == 0 && mostRecentRunRecord.QueuedTime == null && mostRecentRunRecord.RequestedBy.ToUpper().Contains(@"SPARROW\"))
                    {
                        jobStatus = Models.JobCurrentStatus.None;
                    }
                    else
                    {
                        jobStatus = Models.JobCurrentStatus.Errored;
                    }
                }
                else if (mostRecentRunRecord.JobOutcome == "MILESTONE")
                {
                    jobStatus = Models.JobCurrentStatus.Finished;
                }
                else
                {
                    jobStatus = Models.JobCurrentStatus.None;
                }
            }

            return jobStatus;
        }


        /// <summary>
        /// Launches parameterized job
        /// </summary>
        /// <param name="jobId">Job Identifier from the Job Index</param>
        /// <param name="tool"></param>
        /// <param name="parametersJson">Parameters specific to the job launched</param>
        /// <returns></returns>
        public ActionResult LaunchParameterJob(String jobId, String tool, String parametersJson, String owner = "Sparrow")
        {
            ActionResult result = null;

            Services.LoggingService.WriteToLog("WEB0004", DateTime.Now, User.Identity.Name, "Launching " + jobId + " which is a " + tool + " the parameters were: " + parametersJson, "INFO");

            try
            {
                if (tool == ".Net-MrData-WebReport")
                {
                    parametersJson = "{\"arg1\":\"Mr_Data\"}";
                }
                string returnValue = SharedServices.CallDriverApi("RUNREQUEST", jobId, testMode, User.Identity.Name, parametersJson, owner);

                if (String.IsNullOrWhiteSpace(returnValue))
                {
                    result = null;
                }
                else
                {
                    returnValue = returnValue.Replace("\"", "");

                    if (System.IO.File.Exists(returnValue))
                    {
                        byte[] fileData = System.IO.File.ReadAllBytes(returnValue);
                        MemoryStream returnVal = new MemoryStream(fileData);

                        string guid = Guid.NewGuid().ToString();
                        Session[guid] = returnVal.ToArray();

                        Services.LoggingService.WriteToLog("WEB0004", DateTime.Now, User.Identity.Name, $"Preparing file for download for {jobId} and fileGuid {guid} - FileName: {returnValue.Split('\\').Last()}; File size: {returnVal.ToArray().Count()}", "AUDIT");
                        result = Json(new { IsFile = true, FileGuid = guid, FileName = returnValue.Split('\\').Last() }, JsonRequestBehavior.AllowGet);
                    }
                    else
                    {
                        result = Json(new { IsFile = false, ErrorMessage = returnValue }, JsonRequestBehavior.AllowGet);
                    }
                }
            }
            catch (Exception e)
            {
                Services.LoggingService.WriteToLog("WEB0004", DateTime.Now, User.Identity.Name, "Error Launching Job " + jobId + Environment.NewLine + e.ToString(), "ERROR");
                result = null;
            }

            return result;
        }


        /// <summary>
        /// Fetches the file that was generated by the job with parameters launched by the user
        /// </summary>
        /// <param name="fileGuid">Unique indentifier for the file in the web server's temporary storage</param>
        /// <param name="fileName">Name of the file</param>
        /// <returns>The file that's generated by the job</returns>
        public ActionResult Download(string fileGuid, string fileName)
        {
            if (Session[fileGuid] != null)
            {
                byte[] data = Session[fileGuid] as byte[];
                Session.Remove(fileGuid);
                return File(data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            else
            {
                Services.LoggingService.WriteToLog("WEB0004", DateTime.Now, User.Identity.Name, $"Error Downloading file {fileName} for GUID {fileGuid}", "ERROR");
                return new EmptyResult();
            }
        }

        public ActionResult DownloadAttachments(string jobId)
        {
            Data.AppNames db;
            if (!testMode)
            {
                db = Data.AppNames.ExampleProd;
            }
            else
            {
                db = Data.AppNames.ExampleTest;
            }

            DataTable fileRecords = ExtractFactory.ConnectAndQuery(db, $"SELECT TOP (1) [JobId], [FileName], [ContentType], [File] FROM[CONTROLLER].[JobIndex_Attachements_C] WHERE JobId = '{jobId}'");

            if (fileRecords.Rows.Count > 0)
            {
                Byte[] fileBytes = (Byte[])fileRecords.Rows[0]["File"];
                string contentType = fileRecords.Rows[0]["ContentType"].ToString();
                string fileName = fileRecords.Rows[0]["FileName"].ToString();
                return File(fileBytes, contentType, fileName);
            }
            else
            {
                return new EmptyResult();
            }
        }

        private string GetContentType(string fileName)
        {
            string contentType = "application/octetstream";

            string ext = System.IO.Path.GetExtension(fileName).ToLower();

            Microsoft.Win32.RegistryKey registryKey = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(ext);

            if (registryKey != null && registryKey.GetValue("Content Type") != null)

                contentType = registryKey.GetValue("Content Type").ToString();

            return contentType;
        }

        /// <summary>
        /// This function gets called when the user hits the launch button on normal jobs
        /// </summary>
        /// <param name="jobId">Job Identifier from the Job Index</param>
        /// <param name="tool">?</param>
        /// <returns></returns>
        public Boolean LaunchJobToController(String jobId, String tool, String owner)
        {
            bool jobLaunchSuccessful = false;

            Models.JobSummaryDetails fullJobDetails = JobDetailsManager.GetJobDetails(User, jobId).First();
            if (fullJobDetails.UserCanRun == "JobsICanRun")
            {
                if (tool.Contains("MrData"))
                {
                    jobId = "Mr_Data " + jobId;
                }
                else if (tool.Contains("MsData"))
                {
                    jobId = "Ms_Data " + jobId;
                }
                    
                Models.StageRunRequest.LoadRunRequestToController(jobId, User.Identity.Name, testMode, owner);

                jobLaunchSuccessful = true;
            }
            else
            {
                Services.LoggingService.WriteToLog("WEB0004", DateTime.Now, User.Identity.Name, "Authorization Error Attempting to Run " + jobId + " from user " + User.Identity.Name, "WARNING");
            }

            return jobLaunchSuccessful;
        }

        /// <summary>
        /// Returns details pertaining to the launch of the chosen job
        /// </summary>
        /// <param name="jobId">Job Identifier from the Job Index</param>
        /// <returns>Partial view with details for the chosen job</returns>
        public PartialViewResult LaunchDetails(String jobId)
        {
            List<Models.JobSummaryDetails> fullJobDetails = JobDetailsManager.GetJobDetails(User, jobId);

            Models.JobSummaryDetails partialDetails = fullJobDetails.Where(x => x.jobCode == jobId).First();

            if (partialDetails.tool == ".Net-MrData-WebReport")
            {
                partialDetails.Parameters = new List<JobParameter>();
            }
            else if (partialDetails.tool == ".Net-WebReport")
            {
                try
                {
                    if (System.IO.File.Exists(HttpContext.Server.MapPath("~/Views/IWebRequestCustom/_" + jobId + ".cshtml")))
                    {
                        ViewBag.jobId = jobId;
                        return PartialView("~/Views/IWebRequestCustom/_IWebRequestCustomView.cshtml", partialDetails);
                    }
                    else
                    {
                        //set the view parameter fields to the job parameter object fields
                        partialDetails.Parameters = GetParameters(jobId);
                    }
                }
                catch (Exception e)
                {
                    Services.LoggingService.WriteToLog("WEB0004", DateTime.Now, User.Identity.Name, "Error Loading Custom Web Request View for " + jobId + Environment.NewLine + e, "ERROR");
                }
            }

            return PartialView("launchModal", partialDetails);
        }

        /// <summary>
        /// Retrieves run history of the specified job
        /// </summary>
        /// <param name="jobId">Job Identifier from the Job Index</param>
        /// <param name="page">Page number to be retrieved</param>
        /// <returns></returns>
        public PartialViewResult HistoryTable(String jobId, int? page)
        {
            Data.AppNames db;
            if (!testMode)
            {
                db = Data.AppNames.ExampleProd;
            }
            else
            {
                db = Data.AppNames.ExampleTest;
            }

            Models.JobHistory jobHistory = new Models.JobHistory();
            jobHistory.JobCode = jobId;
            int pageSize = 10;
            int pageNumber = (page ?? 1);

            jobHistory.History = ExtractFactory.ConnectAndQuery<DailyScheduleRecord>(db, GetJobHistoryQuery(jobId)).ToList().ToPagedList<DailyScheduleRecord>(pageNumber, pageSize);

            return PartialView("historyTable", jobHistory);
        }

        /// <summary>
        /// Retrieve configuration tables related to this job
        /// </summary>
        /// <param name="jobId">Job Identifier from the Job Index</param>
        /// <returns>Return partial view with config tables</returns>
        [OutputCache(Duration = 500, VaryByParam = "jobId", Location = OutputCacheLocation.ServerAndClient)]
        public PartialViewResult GetJobConfig(string jobId)
        {
            string additionalSearch = "";
            if (jobId.Contains("_"))
            {
                string jobIdMod = jobId.Replace("_", "");
                additionalSearch = $@"OR t.name like '%{jobIdMod}%'";
            }
            List<JobConfig> jobConfigs = new List<JobConfig>();

            Data datasource;
            if (!testMode)
            {
                datasource = new Data(Data.AppNames.ExampleProd);
            }
            else
            {
                datasource = new Data(Data.AppNames.ExampleTest);
            }

            string query = $@"SELECT t.name AS 'TableName'
                            FROM sys.tables t 
                            WHERE t.name like '%_C'
                            AND (t.name like '%{jobId}%' {additionalSearch} )
                            ORDER BY TableName";

            jobConfigs = ExtractFactory.ConnectAndQuery<JobConfig>(datasource, query).ToList();
            if (testMode)
            {
                foreach (var jobConfig in jobConfigs)
                {
                    jobConfig.url = "https://{}/Apps/JobConfiguration/Home/TableSelect?TableName=";
                }
            }

            return PartialView("jobConfiguration", jobConfigs);
        }
        

        public PartialViewResult HelpPage()
        {
            return PartialView("helpModal");
        }

        //this function is for extracting the parameter details from the parameter object from the job class
        private List<JobParameter> GetParameters(string jobCode)
        {
            List<JobParameter> parameters = new List<JobParameter>();
            Data.AppNames datasource = testMode ? Data.AppNames.ExampleTest : Data.AppNames.ExampleProd;
            List<JobParameterDetails> parameterInfo = ExtractFactory.ConnectAndQuery<JobParameterDetails>(datasource, $"select * from PHPconfg.dbo.DA03387_WebReportDetails_C where JobID = '{jobCode}' order by OrderNum").ToList();

            foreach (JobParameterDetails details in parameterInfo)
            {
                Type dataType = Type.GetType(details.DataType);
                List<string> dropDownOptions = null;

                if (details.DropDownDatasource != null && details.DropDownQuery != null)
                {
                    string testProd = testMode ? "Test" : "Prod";
                    Data.AppNames dropdownDatasource;
                    if (!Enum.TryParse(details.DropDownDatasource + testProd, out dropdownDatasource))
                    {
                        Enum.TryParse(details.DropDownDatasource, out dropdownDatasource);
                    }
                    try
                    {
                        dropDownOptions = ExtractFactory.ConnectAndQuery<string>(dropdownDatasource, details.DropDownQuery, tries: 1).ToList();
                    }
                    catch (Exception exc)
                    {
                        Services.LoggingService.WriteToLog("WEB0004", DateTime.Now, User.Identity.Name, "Error Populating Drop-Down for field " + details.ParameterName + Environment.NewLine + exc.ToString(), "ERROR");
                    }
                }

                parameters.Add(new JobParameter(details.ParameterName, details.ParameterDescription, dataType, dropDownOptions, details.MultipleSelect, details.IsOptional));
            }

            return parameters;
        }

        public ContentResult getMems(string sbsb)
        {
            Data.AppNames source = testMode ? Data.AppNames.ExampleTest : Data.AppNames.ExampleProd;
            FinalJSON finalGroup = new FinalJSON();
            finalGroup.results = ExtractFactory.ConnectAndQuery<JSONInfo>(source, string.Format("SELECT RTRIM(CAST(MEME_SFX AS CHAR(2))) as id, RTRIM(CAST(MEME_SFX AS CHAR(2))) as [text] FROM CMC_MEME_MEMBER MEME INNER JOIN CMC_SBSB_SUBSC SBSB ON MEME.SBSB_CK = SBSB.SBSB_CK WHERE SBSB_ID = '{0}'", sbsb)).ToList();
            finalGroup.totalCount = finalGroup.results.Count;
            return Content(JsonConvert.SerializeObject(finalGroup), "application/json");
        }

        protected override void OnException(ExceptionContext filterContext)
        {
            filterContext.ExceptionHandled = true;
            
            Services.LoggingService.WriteToLog("WEB0004", DateTime.Now, User.Identity.Name, filterContext.Exception.ToString(), "ERROR");

            filterContext.Result = new ViewResult { ViewName = "~/Views/Shared/Error.cshtml" };
        }

        public string GetJobHistoryQuery(string jobId)
        {
            return string.Format(@"SELECT * FROM [PHPArchv].[dbo].[CONTROLLER_JobDailySchedule_A] WHERE JobId like '%{0}%'
                                    UNION ALL
                                    SELECT * FROM [PHPArchv].[dbo].[CONTROLLER_PHP_JobDailySchedule_A] WHERE JobId like '%{0}%'
                                    ORDER BY ScheduledStartTime DESC", jobId);
        }

        public static class JobDetailsManager
        {
            internal static List<Models.JobSummaryDetails> GetJobDetails(IPrincipal User, string jobOverride = "")
            {
                List<Models.JobSummaryDetails> jobDetailInformation = new List<JobSummaryDetails>();

                List<Models.JobSummaryDetails> jobDetails = (List<Models.JobSummaryDetails>)System.Web.HttpContext.Current.Session["JobDetails"];

                if (jobDetails != null)
                {
                    if (jobOverride == "")
                    {
                        jobDetailInformation = jobDetails;
                    }
                    else
                    {
                        jobDetailInformation = jobDetails.Where(x => x.jobCode == jobOverride).ToList();
                        System.Web.HttpContext.Current.Session["JobOverride"] = jobOverride;
                    }                    
                }
                else
                {
                    System.Web.HttpContext.Current.Session["JobOverride"] = jobOverride;
                    string servLine = System.Web.HttpContext.Current.Session["serviceLine"]?.ToString();

                    if (servLine != null)
                    {
                        jobDetailInformation = GetRunnableJobsIndex(servLine, User.Identity.Name, jobOverride);
                    }
                    else
                    {
                        jobDetailInformation = GetRunnableJobsIndex(GetServiceLineFromUser(User), User.Identity.Name, jobOverride);
                    }

                    System.Web.HttpContext.Current.Session["JobDetails"] = jobDetailInformation;
                }


                return jobDetailInformation;
            }
        }

        #region ScheduleArea
        [AdminAuthorization]
        public void NewSchedule(string jobId, string startDate, string endDate, string owner, bool onHold, string parameters, string cron_Minute, string cron_Hour, string cron_Day_Month, string cron_Month, string cron_Day_Week)
        {
            
            ScheduleParameters sParameters = new ScheduleParameters
            {
                ScheduleId = "",
                JobId = jobId,
                StartDate = startDate,
                EndDate = endDate,
                Owner = owner,
                OnHold = onHold,
                Parameters = parameters,
                Cron_Minute = cron_Minute,
                Cron_Hour = cron_Hour,
                Cron_Day_Month = cron_Day_Month,
                Cron_Month = cron_Month,
                Cron_Day_Week = cron_Day_Week,
                Requestor = User.Identity.Name
            };
            string jsonString = JsonConvert.SerializeObject(sParameters);

            string serverURL = WebConfigurationManager.AppSettings["DataStationBaseUrl"];

            ScheduleResponse result = ApiService.CallApi<ScheduleResponse>(serverURL + "Schedule/NewSchedule", "POST", jsonString);

        }
        
        [AdminAuthorization]
        public void UpdateSchedule(string scheduleId, string jobId, string startDate, string endDate, string owner, bool onHold, string parameters, string cron_Minute, string cron_Hour, string cron_Day_Month, string cron_Month, string cron_Day_Week)
        {
            
            ScheduleParameters sParameters = new ScheduleParameters
            {
                ScheduleId = scheduleId,
                JobId = jobId,
                StartDate = startDate,
                EndDate = endDate,
                Owner = owner,
                OnHold = onHold,
                Parameters = parameters,
                Cron_Minute = cron_Minute,
                Cron_Hour = cron_Hour,
                Cron_Day_Month = cron_Day_Month,
                Cron_Month = cron_Month,
                Cron_Day_Week = cron_Day_Week,
                Requestor = User.Identity.Name
            };
            string jsonString = JsonConvert.SerializeObject(sParameters);

            string serverURL = WebConfigurationManager.AppSettings["DataStationBaseUrl"];

            ScheduleResponse result = ApiService.CallApi<ScheduleResponse>(serverURL + "Schedule/UpdateSchedule", "PUT", jsonString);

        }
        
        
        
        public PartialViewResult GetScheduleDetails(String jobId, int? page)
        {
            Data.AppNames datasource;
            if (!testMode)
            {
                datasource = Data.AppNames.ExampleTest;
            }
            else
            {
                datasource = Data.AppNames.ExampleProd;
            }

            List<Models.JobSummaryDetails> jobDetails = JobDetailsManager.GetJobDetails(User, jobId);

            IEnumerable<JobSchedule_C> initialJobSchedule_C = ExtractFactory.ConnectAndQuery<JobSchedule_C>(datasource, $@"SELECT * FROM [PHPConfg].[dbo].[Controller_Schedule_C] WHERE JobId = '{jobId}' order by ID desc");
            foreach (JobSchedule_C jS in initialJobSchedule_C)
            {
                jS.RowClass = jS.EndDate < DateTimeOffset.Now ? "danger" : "";
                jS.Cron_Expression = $@"{jS.Cron_Minute} {jS.Cron_Hour} {jS.Cron_Day_Month} {jS.Cron_Month} {jS.Cron_Day_Week}";
                jS.HumanExpression = CronExpressionDescriptor.ExpressionDescriptor.GetDescription(jS.Cron_Expression);
                if(jS.StartDate > DateTime.Now)
                {
                    DateTimeOffset dto = jS.StartDate;
                    jS.NextRun = Cronos.CronExpression.Parse(jS.Cron_Expression).GetNextOccurrence(dto,
                    TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));

                }
                else
                {
                    jS.NextRun = Cronos.CronExpression.Parse(jS.Cron_Expression).GetNextOccurrence(DateTimeOffset.UtcNow,
                    TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));

                }
                
            }
            Models.JobSchedule jobSchedule = new Models.JobSchedule();
            IEnumerable<JobSchedule_C> orderedJobSchedule_C = initialJobSchedule_C.OrderBy(x => x.OnHold).ThenBy(s => s.NextRun);
            IEnumerable<JobSchedule_C> filteredJobSchedule_C = orderedJobSchedule_C.Where(x => x.OnHold == false);
            jobSchedule.JobId = jobId;
            jobSchedule.Tool = jobDetails.Where(x => x.jobCode == jobId).First().tool;

            jobSchedule.RunType = jobDetails.Where(x => x.jobCode == jobId).First().RunType;
            if(filteredJobSchedule_C.Count() > 0)
            {
                jobSchedule.NextRun = filteredJobSchedule_C.First().NextRun;
            }
            else
            {
                if(orderedJobSchedule_C.Count() > 0)
                {
                    jobSchedule.NoSchedule = "There are no active schedules, all schedules are On-Hold";
                }
                else
                {
                    jobSchedule.NoSchedule = "There are no schedules";
                }
                
            }
            
            
            int pageSize = 5;
            int pageNumber = (page ?? 1);
            jobSchedule.jobSchedules = orderedJobSchedule_C.ToPagedList(pageNumber, pageSize);
            return PartialView("scheduleList", jobSchedule);
        }
        [OutputCache(Duration = int.MaxValue, VaryByParam = "*", Location = OutputCacheLocation.ServerAndClient)]
        public PartialViewResult ScheduleForm(string jobId)
        {
            JobSchedule_C jobOb = new JobSchedule_C();

            jobOb.JobId = jobId;


            return PartialView("createSchedule", jobOb);
        }

        public PartialViewResult EditSchedule(string jobId, string scheduleID)
        {

            string query = @"Select * from [dbo].[Controller_Schedule_C]
                                    WHERE [ID] = '" + scheduleID+"' and [JobId] = '" + jobId +"'";

            Data.AppNames datasource;
            if (!testMode)
            {
                datasource = Data.AppNames.ExampleProd;
            }
            else
            {
                datasource = Data.AppNames.ExampleTest;
            }

            JobSchedule_C jobOb = ExtractFactory.ConnectAndQuery<JobSchedule_C>(datasource, query).First();


            return PartialView("createSchedule", jobOb);
        }


        [OutputCache(Duration = 120, VaryByParam = "*", Location = OutputCacheLocation.ServerAndClient)]
        public JObject ValidateCron(string cronExpression, string startDate)
        {
            DateTime dateTime = DateTime.SpecifyKind(DateTime.Parse(startDate), DateTimeKind.Utc);

            
            JObject result = new JObject();
            try
            {


                CronExpression cronos = Cronos.CronExpression.Parse(cronExpression);
                DateTime nextRun = DateTime.Parse(cronos.GetNextOccurrence(dateTime, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time")).ToString());
                string estTime = TimeZoneInfo.ConvertTimeFromUtc(nextRun, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time")).ToString();




                string humantext = CronExpressionDescriptor.ExpressionDescriptor.GetDescription(cronExpression);

                result = JObject.Parse(@"{'NextRun':'Next Run: " + estTime + " (UTC: " + nextRun + ")','HumanExpression':'" + humantext + "'}");
            }
            catch (Exception ex)
            {
                result = JObject.Parse("{'Error':'The Cron expression could not be processed the way that it was entered'}");
            }








            return result;
        }
        [AdminAuthorization]
        public bool UpdateHoldState(string jobId, string scheduleId, bool onHold)
        {
            ScheduleParameters sParameters = new ScheduleParameters
            {
                ScheduleId = scheduleId,
                JobId = jobId,
                OnHold = onHold,
                Requestor = User.Identity.Name
            };
            string jsonString = JsonConvert.SerializeObject(sParameters);

            string serverURL = WebConfigurationManager.AppSettings["DataStationBaseUrl"];

            string result = ApiService.CallApi<string>(serverURL + "Schedule/UpdateHoldState", "PUT", jsonString);

            return true;
        }

        [AdminAuthorization]
        public bool DeleteSchedule(string jobId, string scheduleId)
        {
            ScheduleParameters sParameters = new ScheduleParameters
            {
                ScheduleId = scheduleId,
                JobId = jobId,
                Requestor = User.Identity.Name
            };
            string jsonString = JsonConvert.SerializeObject(sParameters);

            string serverURL = WebConfigurationManager.AppSettings["DataStationBaseUrl"];

            string result = ApiService.CallApi<string>(serverURL + "Schedule/DeleteSchedule", "PUT", jsonString);

            return true;
        }


        #endregion ScheduleArea


        private class FinalJSON
        {
            public int totalCount { get; set; }
            public List<JSONInfo> results { get; set; }
        }

        private class JSONInfo
        {
            public String id { get; set; }
            public String text { get; set; }
        }

        private class JobParameterDetails
        {
            public string JobID { get; set; }
            public string ParameterName { get; set; }
            public string SpacedParameterName { get; set; }
            public string ParameterDescription { get; set; }
            public string DataType { get; set; }
            public string DropDownQuery { get; set; }
            public string DropDownDatasource { get; set; }
            public bool MultipleSelect { get; set; }
            public bool IsOptional { get; set; }
            public byte OrderNum { get; set; }
        }

        private class ScheduleParameters
        {
            public string ScheduleId { get; set; }
            public string JobId { get; set; }
            public string StartDate { get; set; }
            public string EndDate { get; set; }
            public string Owner { get; set; }
            public bool OnHold { get; set; }
            public string Parameters { get; set; }
            public string Cron_Minute { get; set; }
            public string Cron_Hour { get; set; }
            public string Cron_Day_Month { get; set; }
            public string Cron_Month { get; set; }
            public string Cron_Day_Week { get; set; }
            public string Requestor { get; set; }

        }

        private class ScheduleResponse
        {
            public bool Valid { get; set; }
            public string NextRun { get; set; }
            public string HumanExpression { get; set; }
            public string Error { get; set; }
        }

    }
}
