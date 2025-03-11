using Utilities;
using RunRequest.Models;
using System;
using System.Collections.Generic;
using System.Web.Mvc;

namespace RunRequest.Controllers
{
    public class JobCentralControlController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.recovery = null;
            if(Security.IsUserAllowed(HttpContext.User.Identity.Name))
            {                
                return View();
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }

        public ActionResult KillJob(string winProcID, string jobID, string server, string owner)
        {
            try
            {                
                ViewBag.Message = JobProcessManagement.KillDriverProcess(winProcID, jobID, HttpContext.User.Identity.Name, server, owner);
            }
            catch(Exception ex)
            {
                ViewBag.Message = ex.ToString();
            }
            return PartialView("_CurrentlyRunningJobs", JobProcessManagement.GetDriverProcesses());
        }

        public ActionResult GetLogs(string jobIndex, string dateTicks, string owner)
        {
            DateTime date = new DateTime(Int64.Parse(dateTicks));
            List<LogRecord> allLogs = JobProcessManagement.GetLogsFromDatabase(jobIndex, date);

            JobIndex job = new JobIndex(jobIndex);

            foreach (LogRecord log in allLogs)
            {
                if (log.LogCategory == "ERROR")
                {
                    if (job == null || string.IsNullOrWhiteSpace(job.RecoveryDetails))
                    {
                        ViewBag.recovery = "Recovery Details: Not Available";
                    }
                    else
                    {
                        ViewBag.recovery = job.RecoveryDetails;
                    }
                }
                log.Owner = owner;
            }
            return PartialView("_LogView", allLogs);
        }

        public ActionResult MarkResolved(string jobIndex, string dateTicks, string owner)
        {
            DateTime date = new DateTime(Int64.Parse(dateTicks));
            JobProcessManagement.ResolveErroredJob(jobIndex, date, owner);
            return View("Index");
        }

        [AcceptVerbs(HttpVerbs.Get | HttpVerbs.Post)]
        public ActionResult GetSchedule(string date)
        {
            List<ScheduledJob> schedule = JobProcessManagement.GetJobSchedule(date);
            return PartialView("_ScheduleView", schedule);
        }

        public ActionResult GetRunningJobs()
        {
            return PartialView("_CurrentlyRunningJobs", JobProcessManagement.GetDriverProcesses());
        }

        public ActionResult GetDataSourceReadiness()
        {
            return PartialView("_DataSourceReadiness", DataSourceManagement.SourceStatusAsDataSource(HomeController.testMode));
        }

        public ActionResult GetDataSourceNames()
        {
            return PartialView("_DataSourceReadiness", DataSourceManagement.DataSourceNamesAsDataSource(HomeController.testMode));
        }

        public ActionResult GetErroredJobs()
        {
            List<ScheduledJob> errors = JobProcessManagement.GetErroredJobs(21);
            return PartialView("_ErrorView", errors);
        }

        public ActionResult GetAllJobs(string date)
        {
            List<RunJob> jobs = JobProcessManagement.GetAllJobs(date);
            return PartialView("_AllJobsView", jobs);
        }

        public ActionResult ToggleSource(string dataSource, bool yesOrNo, bool overrideInsteadOfReady)
        {
            DataSourceManagement.ToggleSource(dataSource, yesOrNo, overrideInsteadOfReady, HomeController.testMode);
            return PartialView("_DataSourceReadiness", DataSourceManagement.SourceStatusAsDataSource(HomeController.testMode));
        }
        public ActionResult CheckSource(string dataSource)
        {
            APIWork.DataSource data = DataSourceManagement.GetIndividualDataSourceStatus(dataSource, HomeController.testMode);
            return Json(data, JsonRequestBehavior.AllowGet);
        }

        public ActionResult ReRunJob(String jobIndex, String owner)
        {
            bool jobLaunched = JobProcessManagement.ReRunJob(jobIndex, User.Identity.Name, owner);
            HttpStatusCodeResult response = new HttpStatusCodeResult(500);
            if(jobLaunched)
            {
                response = new HttpStatusCodeResult(200);
            }
            return response;
        }

        public ActionResult GetJobNotes(string jobIndex, string dateTicks)
        {
            DateTime date = new DateTime(Int64.Parse(dateTicks));
            List<JobNotes> notes = JobProcessManagement.GetNotes(jobIndex, date);
            return PartialView("_NotesModal", notes);
        }

        [HttpPost]
        public ActionResult SaveNote(JobNotes newNote)
        {

            newNote.AdminUser = HttpContext.User.Identity.Name;
            newNote.NoteDateTime = DateTime.Now;
            JobProcessManagement.PostJobNote(newNote);
            return new HttpStatusCodeResult(200);
        }

        [HttpPost]
        public ActionResult RunJobAdhoc(String jobIndex, String parametersJson, String launchServerName)
        {
            bool jobLaunched = JobProcessManagement.RunAdhoc(jobIndex.Trim(), User.Identity.Name, parametersJson, launchServerName);
            HttpStatusCodeResult response = new HttpStatusCodeResult(500);
            if (jobLaunched)
            {
                response = new HttpStatusCodeResult(200);
            }
            return response;
        }
    }
}