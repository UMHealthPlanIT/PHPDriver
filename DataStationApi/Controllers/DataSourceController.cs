using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Utilities;

namespace DataStationApi.Controllers
{
    public class DataSourceController : ApiController
    {
        private static bool TestMode = System.Configuration.ConfigurationManager.AppSettings["RunMode"].ToUpper() == "TEST" ? true : false;
        private static bool VerboseLogging = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["VerboseLogging"]);
        private Logger procLog = new Logger(new Logger.LaunchRequest("WEB0019", TestMode, null));

        [HttpGet]
        // http://localhost:52897/api/DataSource/CheckDataSource?parameters=Epic%20Clarity
        public HttpResponseMessage CheckDataSource([FromUri] string[] parameters)
        {
            if (parameters.Count() == 0)
            {
                if (VerboseLogging)
                {
                    UniversalLogger.WriteToLog(procLog, "CheckDataSource parameters: parameters = empty");
                }
                // return list of sources
                List<string> sourceList = ExtractFactory.ConnectAndQuery<string>(procLog.LoggerPhpConfig, "select Name from dbo.ControllerMasterSwitch_C order by case when Name='Master' then 0 else 1 end, Name").ToList();

                return Request.CreateResponse(HttpStatusCode.OK, sourceList);
            }
            else
            {
                if (VerboseLogging)
                {
                    UniversalLogger.WriteToLog(procLog, "CheckDataSource parameters: parameters = \"" + parameters[0] + "\"");
                }
                // if we can't ascertain readiness for any reason, we assume that piece returns true 
                bool sourceReady = true;
                bool switchResult = true;
                bool overrideReadinessQuery = false;

                try
                {
                    DataTable result = ExtractFactory.ConnectAndQuery(procLog.LoggerPhpConfig, "select isnull(ConnectionName, ''), isnull(ReadinessQuery, '') from dbo.ControllerMasterSwitch_C where Name = '" + parameters[0] + "'");

                    if (result.Rows.Count == 0)
                    {
                        UniversalLogger.WriteToLog(procLog, "'" + parameters[0] + "' is not a valid datasource in the table!", category: UniversalLogger.LogCategory.WARNING);
                        return Request.CreateResponse(HttpStatusCode.BadRequest);
                    }

                    switchResult = CheckSwitch(parameters[0], false); //Check individual data source switch, if it's off, we won't do any further checks
                    if (switchResult)
                    {

                        overrideReadinessQuery = CheckSwitch(parameters[0], true);
                        if (!overrideReadinessQuery) //Check override switch. If it's flipped we won't do any further checks 
                        {
                            if (result.Rows[0].Field<string>(0) != "" && result.Rows[0].Field<string>(1) != "")
                            {
                                Enum.TryParse(result.Rows[0].Field<string>(0), out Data.AppNames connection);
                                string query = result.Rows[0].Field<string>(1);

                                try
                                {
                                    DataTable queryResultTable = ExtractFactory.ConnectAndQuery(new Data(connection), query, 10, 1);
                                    sourceReady = Convert.ToBoolean(queryResultTable.Rows[0][0]);
                                }
                                catch (Exception e)
                                {
                                    procLog.WriteToLog(e.ToString(), UniversalLogger.LogCategory.WARNING);
                                    sourceReady = false;
                                }
                            }
                            else
                            {
                                if (VerboseLogging)
                                {
                                    UniversalLogger.WriteToLog(procLog, parameters[0] + " has no connection information, assuming the source is ready.");
                                }
                            }

                            if (sourceReady)
                            {
                                if (VerboseLogging)
                                {
                                    UniversalLogger.WriteToLog(procLog, "The " + parameters[0] + " database is ready.");
                                }
                            }
                            else
                            {
                                UniversalLogger.WriteToLog(procLog, "The " + parameters[0] + " database is not ready. Query returned a status of not ready and 'override readiness query' was not turned on.", category: UniversalLogger.LogCategory.WARNING);
                            }
                        }
                        else
                        {
                            UniversalLogger.WriteToLog(procLog, "The " + parameters[0] + " database is ready. Override is in use.", category: UniversalLogger.LogCategory.WARNING);
                        }
                    }
                    else
                    {
                        UniversalLogger.WriteToLog(procLog, "The " + parameters[0] + " database is not ready. Datasource switch is turned off.", category: UniversalLogger.LogCategory.WARNING);
                        sourceReady = false;
                    }

                }
                catch (Exception ex)
                {
                    UniversalLogger.WriteToLog(procLog, "Unable to ascertain " + parameters[0] + " readiness. Assuming " + parameters[0] + " is available. " + ex.ToString(), category: UniversalLogger.LogCategory.ERROR);
                }

                APIWork.DataSource dataSource = new APIWork.DataSource() { name = parameters[0], sourceReady = sourceReady, manualSwitch = switchResult, overrideReadinessQuery = overrideReadinessQuery };
                return Request.CreateResponse(HttpStatusCode.OK, dataSource);
            }
        }

        [HttpGet]
        public HttpResponseMessage ToggleSource(string source, bool yesOrNo, bool overrideInsteadOfReady)
        {
            UniversalLogger.WriteToLog(procLog, "ToggleSource parameters: source = \"" + source + "\", ready = " + yesOrNo.ToString() + ", override = " + overrideInsteadOfReady.ToString());
            try
            {
                string setText = yesOrNo ? "Yes" : "No";
                string updateField = overrideInsteadOfReady ? "OverrideReadinessQuery" : "Ready";
                ExtractFactory.ConnectAndQuery(procLog.LoggerPhpConfig, "update dbo.ControllerMasterSwitch_C set " + updateField + " = '" + setText + "' where Name = '" + source.Replace("|", " ") + "'");
            }
            catch (Exception ex)
            {
                UniversalLogger.WriteToLog(procLog, "Invalid parameters or data source does not exist. " + ex.ToString(), category: UniversalLogger.LogCategory.ERROR);
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            return Request.CreateResponse(HttpStatusCode.OK);
        }

        private bool CheckSwitch(string sourceName, bool checkForOverride)
        {
            if (VerboseLogging)
            {
                UniversalLogger.WriteToLog(procLog, "CheckSwitch parameters: sourceName = \"" + sourceName);
            }
            try
            {
                string checkField = checkForOverride ? "OverrideReadinessQuery" : "Ready";
                string source = ExtractFactory.ConnectAndQuery<string>(procLog.LoggerPhpConfig, "select " + checkField + " from dbo.ControllerMasterSwitch_C where Name='" + sourceName + "'").ToList()[0];

                return source == "Yes";
            }
            catch (Exception ex)
            {
                UniversalLogger.WriteToLog(procLog, "Unable to access master switch records. Assuming data source is not available. " + ex.ToString(), category: UniversalLogger.LogCategory.ERROR);

                return false;
            }
        }
    }
}
