using DataStationApi.Models;
using DataStationApi.Services;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Configuration;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Web;
using Newtonsoft.Json;

namespace DataStationApi.Controllers
{
    [Authorize]
    public class ULoggerController : ApiController
    {
        private ULoggerService  LoggerService;
        
        // Allows us to check the request headers on entry into the controller, rather than at each method call.
        // Calls the base (parent) class to process normal initialization as well.
        protected override void Initialize(HttpControllerContext controllerContext)
        {
            base.Initialize(controllerContext);
            CheckRequestHeaders(out String database, out String table);
            LoggerService = new ULoggerService(database, table);
        }

        /// <summary>
        /// POST: Create a new log entry in the logger table.
        /// </summary>
        /// <param name="logEntry">JSON String mapped to logEntry object. Contains information to build the log record.</param>
        /// <param name="sensitive">Used for security logging. Sends to FairWarning if true, otherise sends to QRadar.</param>
        /// <returns>Http Status Code; BadRequest on failure, OK on success.</returns>
        [HttpPost]
        [Route("api/ULogger/WriteToLog")]
        public HttpResponseMessage WriteToLog(ULogEntryModel logEntry, bool sensitive = false)
        {
            Task<string> rawContent = RawContentReader.Read(this.Request);
            string clientIp = HttpContext.Current.Request.UserHostAddress;
            if (!ModelState.IsValid || logEntry == null) // Didn't pass validation
            {
                if(logEntry == null)
                {
                    try
                    {
                        //This is part of the two-pronged attack on stopping failed log messages
                        //Apparently sometimes we can convert the content into a ULogEntryModel, but the behind the scenes API magic cannot?
                        ULogEntryModel entryTryTwo = JsonConvert.DeserializeObject<ULogEntryModel>(rawContent.Result.ToString());
                        if(LogMessage(entryTryTwo, sensitive))
                        {
                            return Request.CreateResponse(HttpStatusCode.OK);
                        }
                        else
                        {
                            return Request.CreateResponse(HttpStatusCode.BadRequest);
                        }
                    }
                    catch
                    {
                        HttpRequestMessage whatever = Request;
                        ULogEntryModel failedAttempt = new ULogEntryModel();
                        failedAttempt.JobIndex = "ULogger";
                        failedAttempt.LogCategory = "ERROR";
                        failedAttempt.LogDateTime = DateTime.Now;
                        failedAttempt.LoggedByUser = "ULogger";
                        failedAttempt.LogContent = "Inbound object was NULL. Values submitted were unable to be boxed. Requester IP: " + clientIp + " Inbound values: " + rawContent.Result.ToString();
                        LoggerService.CreateNewLogRecord(failedAttempt);
                    }
                }
                else
                {
                    ULogEntryModel failedAttempt = new ULogEntryModel();
                    failedAttempt.JobIndex = "ULogger";
                    failedAttempt.LogCategory = "ERROR";
                    failedAttempt.LogDateTime = DateTime.Now;
                    failedAttempt.LoggedByUser = "ULogger";
                    failedAttempt.LogContent = "Values were found, but could not be validated. Requester IP: " + clientIp + " Inbound values: " + rawContent.Result.ToString();
                    LoggerService.CreateNewLogRecord(failedAttempt);
                }
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ModelState);
            }

            if(LogMessage(logEntry, sensitive))
            {
                return Request.CreateResponse(HttpStatusCode.OK);
            }
            else
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            //int tries = 1;
            //try
            //{
            //    LoggerService.CreateNewLogRecord(logEntry);
                
            //    ShipLogToSecurity(sensitive);
                
            //    return Request.CreateResponse(HttpStatusCode.OK);
            //}
            //catch(TimeoutException to)
            //{
            //    if(tries > 2)
            //    {
            //        ULogEntryModel failedAttempt = new ULogEntryModel();
            //        failedAttempt.JobIndex = "ULogger";
            //        failedAttempt.LogCategory = "ERROR";
            //        failedAttempt.LogDateTime = DateTime.Now;
            //        failedAttempt.LoggedByUser = "ULogger";
            //        failedAttempt.LogContent = "Error attempting to submit log record. Error:" + to.ToString();
            //        LoggerService.CreateNewLogRecord(failedAttempt);
            //        return Request.CreateResponse(HttpStatusCode.BadRequest, to.Message);
            //    }
            //    else
            //    {
            //        LoggerService.CreateNewLogRecord(logEntry);
            //    }

            //}
            //catch (Exception e)
            //{
            //    ULogEntryModel failedAttempt = new ULogEntryModel();
            //    failedAttempt.JobIndex = "ULogger";
            //    failedAttempt.LogCategory = "ERROR";
            //    failedAttempt.LogDateTime = DateTime.Now;
            //    failedAttempt.LoggedByUser = "ULogger";
            //    failedAttempt.LogContent = "Error attempting to submit log record. Error:" + e.ToString();
            //    LoggerService.CreateNewLogRecord(failedAttempt);
            //    return Request.CreateResponse(HttpStatusCode.BadRequest, e.Message);
            //}
        }

        /// <summary>
        /// POST: Update an existing log entry with a remediation status / note.
        /// </summary>
        /// <param name="logEntry">JSON String mapped to logEntry object. Contains information to build the log record.</param>
        /// <returns>Http Status Code; BadRequest on failure, OK on success.</returns>
        [HttpPost]
        public HttpResponseMessage UpdateRemediation(ULogEntryModel logEntry)
        {
            if (!ModelState.IsValid || String.IsNullOrWhiteSpace(logEntry.RemediationNote)) // Didn't pass validation
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ModelState);
            }

            try
            {
                LoggerService.UpdateLogRemediation(logEntry);

                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception e)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, e.Message);
            }
        }

        /// GET: Get log records produced from all jobs today.
        [HttpGet]
        public HttpResponseMessage GetLog()
        {
            try
            {
                return LoggerService.GetLogsByDate(DateTime.Today);
            }
            catch (Exception e)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, e.Message);
            }
        }

        /// GET: Get all log records produced from all jobs on [date].
        [HttpGet]
        public HttpResponseMessage GetLog(DateTime date)
        {
            try
            {
                return LoggerService.GetLogsByDate(date);
            }
            catch (Exception e)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, e.Message);
            }
        }

        /// GET: Get all log records produced from [job_index] today.
        [HttpGet]
        public HttpResponseMessage GetLog(String jobIndex)
        {
            try
            {
                return LoggerService.GetLogsByIndex(jobIndex);
            }
            catch (Exception e)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, e.Message);
            }
        }

        /// GET: Get last [num_records] log records produced from [job_index].
        [HttpGet]
        public HttpResponseMessage GetLog(int numRecords, String jobIndex)
        {
            try
            {
                return LoggerService.GetNumLogsByIndex(numRecords, jobIndex);
            }
            catch (Exception e)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, e.Message);
            }
        }

        /// GET: Get archived log records....but how? params?
        [HttpGet]
        public HttpResponseMessage ArchiveLogs()
        {
            return Request.CreateResponse(HttpStatusCode.OK);
        }

        /// GET: 200 if everything is working, 500 if not, or it takes too long.
        [HttpGet]
        public HttpResponseMessage IsAlive()
        {
            try
            {
               if(LoggerService.CheckForSignsOfLife())
                {
                    return Request.CreateResponse(HttpStatusCode.OK);
                }
                else
                {
                    return Request.CreateResponse(HttpStatusCode.InternalServerError);
                }
            }
            catch (Exception e)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, e.Message);
            }
        }

        /// Checks the request header to see if custom database/table parameters were set.
        /// Default if empty/invalid => flip db phpconfg test/prod, table = LoggerRecord
        private void CheckRequestHeaders(out String database, out String table)
        {
            HttpRequestHeaders headers = Request.Headers;

            // Header key names
            String headerDatabase = "LogDatabase";
            String headerTable = "LogTable";

            // Default values if no header info
            String defaultDatabaseTest = "PhpConfgTest";
            String defaultDatabaseProd = "PhpConfgProd";
            String defaultTable = "LoggerRecord";

            database = "";
            if (headers.Contains(headerDatabase)) // If a database header value was passed in, let's grab it.
            {
                database = headers.GetValues(headerDatabase).First();
                if (database.ToUpper() == "TEST")
                {
                    database = "PhpConfgTest";
                }
                else if (database.ToUpper() == "PROD")
                {
                    database = "PhpConfgProd";
                }
            }

            // No value, or invalid value, passed in for database pointer, let's just run test/prod based on DataStation test/prod endpoint.
            if (String.IsNullOrWhiteSpace(database) || ( !database.ToUpper().Equals(defaultDatabaseProd) && !database.ToUpper().Equals(defaultDatabaseTest) ) )
            {
                bool isProduction = WebConfigurationManager.AppSettings["RunMode"].ToString().ToUpper().Contains("PROD");
                database = isProduction ? defaultDatabaseProd : defaultDatabaseTest;
            }
            
            table = (headers.Contains(headerTable)) ? headers.GetValues(headerTable).First() : defaultTable;
        }

        /// Sends a duplicate log record to the security team. Which endpoint hit depends on the nature of the information.
        /// A vast majority of the logs will just ship to QRadar. Anything containing personal/health information should be
        /// shipped to FairWarning.
        private void ShipLogToSecurity(bool shipToFairWarning)
        {
            if (shipToFairWarning)
            {
                // Ship to FairWarning
            }
            else
            {
                // Ship to QRadar
            }
        }

        private bool LogMessage(ULogEntryModel logEntry, bool sensitive, int tries = 1)
        {

            try
            {
                LoggerService.CreateNewLogRecord(logEntry);

                ShipLogToSecurity(sensitive);

                return true;
            }
            catch (System.Data.SqlClient.SqlException se) //there are multiple kinds of SQL exception, if anything but a timeout happens we're just gonna log it, else try again
            {
                if (tries > 2 || !se.ToString().Contains("Execution Timeout Expired"))
                {
                    ULogEntryModel failedAttempt = new ULogEntryModel();
                    failedAttempt.JobIndex = "ULogger";
                    failedAttempt.LogCategory = "ERROR";
                    failedAttempt.LogDateTime = DateTime.Now;
                    failedAttempt.LoggedByUser = "ULogger";
                    failedAttempt.LogContent = "Error attempting to submit log record for " + logEntry.JobIndex + ". Error:" + se.ToString();
                    LoggerService.CreateNewLogRecord(failedAttempt);
                    return false;
                }
                else
                {
                    tries++;
                    return LogMessage(logEntry, sensitive, tries);
                }

            }
            catch (Exception e)
            {
                ULogEntryModel failedAttempt = new ULogEntryModel();
                failedAttempt.JobIndex = "ULogger";
                failedAttempt.LogCategory = "ERROR";
                failedAttempt.LogDateTime = DateTime.Now;
                failedAttempt.LoggedByUser = "ULogger";
                failedAttempt.LogContent = "Error attempting to submit log record for " + logEntry.JobIndex + ". Error:" + e.ToString();
                LoggerService.CreateNewLogRecord(failedAttempt);
                return false;
            }
        }


    }
    /// <summary>
    /// Thanks William T Mallard!
    /// https://stackoverflow.com/questions/13226817/getting-raw-post-data-from-web-api-method
    /// </summary>
    public class RawContentReader
    {
        public static async Task<string> Read(HttpRequestMessage req)
        {
            using (var contentStream = await req.Content.ReadAsStreamAsync())
            {
                contentStream.Seek(0, System.IO.SeekOrigin.Begin);
                using (var sr = new StreamReader(contentStream))
                {
                    return sr.ReadToEnd();
                }
            }
        }
    }
}