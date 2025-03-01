using JobConfiguration.Models;
using Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Configuration;

namespace JobConfiguration.Controllers
{
    public class BulkUpdateController : Controller
    {
        public static Data.AppNames dataSource;
        public static Logger log;
        public static String getConfigTables = @"SELECT TABLE_NAME FROM information_schema.tables WHERE TABLE_TYPE='BASE TABLE' and TABLE_NAME like '%_C' order by TABLE_NAME";

        [HttpGet]
        public ActionResult Index(String tableName)
        {
            if (Permissions.ValidateIsTable(tableName, dataSource, getConfigTables) &&
                Permissions.AllowsBulkUpdate(tableName, dataSource, HttpContext))
            {
                log = getLog(HttpContext);
                ViewBag.TargetTable = tableName;
                ViewBag.tableSchema = GetTableSchema(tableName);

                BulkUpdateTable model = new BulkUpdateTable(tableName, GetTableSchema(tableName));
                Session.Add("bulkUpdateModel", model);

                return View("~/Views/BulkUpdate/BulkUpdate.cshtml", model);
            }
            else
            {
                return HttpNotFound("Page Not Found");
            }
        }

        [HttpGet]
        public ActionResult Continue()
        {
            BulkUpdateTable model = (BulkUpdateTable)Session["bulkUpdateModel"];
            model.errorMessage = "";

            String returnView = "";

            if (model != null && Permissions.ValidateIsTable(model.tableName, dataSource, getConfigTables))
            {

                int currentStep = model.currentStep;

                switch (currentStep)
                {
                    case 1:
                        returnView = "~/Views/BulkUpdate/_Download.cshtml";
                        break;
                    case 2:
                        returnView = "~/Views/BulkUpdate/_Modify.cshtml";
                        break;
                    case 3:
                        returnView = "~/Views/BulkUpdate/_Upload.cshtml";
                        break;
                    default:
                        model.errorMessage = "An error occurred and we need to start over, sorry for the inconvenience.";
                        model.currentStep = 0;
                        returnView = "~/Views/BulkUpdate/_Information.cshtml";
                        break;
                }
            }
            else
            {
                return HttpNotFound("Page Not Found");
            }

            model.currentStep++;
            Session.Add("bulkUpdateModel", model);

            return PartialView(returnView, model);
        }

        [HttpGet]
        public ActionResult Download(String tableName)
        {
            BulkUpdateTable model;

            if (Request.UrlReferrer.ToString().Contains("BulkUpdate")) // From BulkUpdate 'Flow'
            {
                model = (BulkUpdateTable)Session["bulkUpdateModel"];
                model.errorMessage = "";
            }
            else if (Permissions.ValidateIsTable(tableName, dataSource, getConfigTables) &&
                     Permissions.AllowsBulkUpdate(tableName, dataSource, HttpContext)) // From TableSelect Download Button
            {
                model = new BulkUpdateTable(tableName, GetTableSchema(tableName));
                log = getLog(HttpContext);
            }
            else // From A Galaxy Far Far Away...
            {
                return HttpNotFound("Page Not Found");
            }

            String fileName = model.tableName + ".xlsx";
            String outputLoc = log.LoggerWorkDir + @"tempfiles\" + fileName;
            String query = String.Format("SELECT * FROM {0}.{1}", model.tableSchema, model.tableName);

            ExtractFactory.RunExcelExtract(query, dataSource, model.tableName.Substring(0, Math.Min(22, model.tableName.Length)), log, outputLoc, outputZeroResults: true, dateFormat: "MM/dd/yyyy hh:mm:ss");
            
            byte[] fileData = System.IO.File.ReadAllBytes(outputLoc);

            Stream stream = new MemoryStream(fileData);
            System.IO.File.Delete(outputLoc);

            Session.Add("bulkUpdateModel", model);

            return (FileResult) File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [HttpPost]
        public ActionResult Upload()
        {
            BulkUpdateTable model = (BulkUpdateTable)Session["bulkUpdateModel"];
            model.errorMessage = "";

            if (model != null)
            {
                String tempFilePath = "";

                try
                {
                    HttpPostedFileBase file = Request.Files[0];

                    // Save temp copy of user uploaded file to be consumed in LoadExcelToTable()
                    tempFilePath = log.LoggerWorkDir + @"tempfiles\";
                    System.IO.Directory.CreateDirectory(tempFilePath);
                    tempFilePath += model.tableName + ".xlsx";
                    file.SaveAs(tempFilePath);

                    Data.AppNames name = log.LoggerPhpConfig;

                    DataWork.LoadExcelToTable(tempFilePath, model.tableSchema + "." + model.tableName, dataSource, log, true);

                    //Send alert that bulk update was successful
                    BulkUpdateAlert(model.tableName, model.tableSchema);
                }
                catch (Exception e)
                {
                    model.errorMessage = e.Message;
                    log.WriteToLog("Bulk Upload failed for table: " + model.tableName + "\n" + e.StackTrace);
                    Session.Add("bulkUpdateModel", model);
                    return PartialView("~/Views/BulkUpdate/_Upload.cshtml", model);
                }
                finally
                {
                    if (!tempFilePath.Equals(""))
                    {
                        System.IO.File.Delete(tempFilePath);
                    }
                }

                Session.Add("bulkUpdateModel", model);
                return PartialView("~/Views/BulkUpdate/_Success.cshtml", model);
            }
            else
            {
                return HttpNotFound("Page Not Found");
            }
        }

        private void BulkUpdateAlert(string pTableName, string pTableSchema)
        {
            DataTable bulkUpdateEmail = ExtractFactory.ConnectAndQuery(dataSource, $"select TableName, Recipient, AdditionalComments from dbo.BulkUpdateEmailDistribution_C where TableName = '{pTableName}'");

            if (bulkUpdateEmail.Rows.Count > 0)
            {
                string userId = User.Identity.Name;
                string backupOutputLocation = log.LoggerOutputYearDir + pTableSchema  + "." + pTableName + "\\" + pTableSchema + "." + pTableName + DateTime.Now.ToString("yyyyMMdd-hhmm") + ".xlsx";

                foreach (DataRow row in bulkUpdateEmail.Rows)
                {
                    string emailAddress = row["Recipient"].ToString();
                    string emailBody = $@"{pTableName} has been bulk updated by {userId}.<br><br>Please submit a ticket if you need this table restored, and include this link:<br><a href=""{backupOutputLocation}"">{backupOutputLocation}</a>";

                    if (row["AdditionalComments"].ToString().Length > 0)
                    {
                        emailBody += "<br><br>Additional Comments:<br>" + row["AdditionalComments"].ToString();
                    }

                    SendAlerts.Send(log.ProcessId, 0, $"Bulk Update for {pTableName}", emailBody, log, SendToOverride: emailAddress, htmlEmail: true);
                }
            }
        }

        private static string GetTableSchema(string TableName)
        {
            return ExtractFactory.ConnectAndQuery(dataSource, "SELECT TABLE_SCHEMA FROM information_schema.tables WHERE TABLE_TYPE='BASE TABLE' and TABLE_NAME = @TableName", new List<SqlParameter>() { new SqlParameter() { ParameterName = "TableName", Value = TableName } }).Rows[0][0].ToString();
        }

        private static Logger getLog(HttpContextBase context)
        {
            Logger webLog = context.Session["webLog"] as Logger;

            if (webLog == null)
            {

                if (ConfigurationManager.AppSettings["RunMode"].ToUpper() == "PROD")
                {
                    webLog = new Logger("WEB0003", false);
                    webLog.WriteToLog("Running in Prod Mode");
                }
                else
                {
                    webLog = new Logger("WEB0003", true);
                    webLog.WriteToLog("Running in Test Mode");
                }

                webLog.WriteToLog(context.User.Identity.Name + " has accessed the job configuration tool");

                context.Session["webLog"] = webLog;
            }

            return webLog;
        }

        protected override void OnException(ExceptionContext filterContext)
        {
            filterContext.ExceptionHandled = true;

            Logger errorLog = getLog(HttpContext);

            errorLog.WriteToLog(filterContext.Exception.ToString());
            filterContext.Result = new ViewResult { ViewName = "~/Views/Shared/Error.cshtml", ViewBag = { LogID = System.IO.Path.GetFileNameWithoutExtension(errorLog.logLocation).Replace(" ", "") } };
        }
    }
}