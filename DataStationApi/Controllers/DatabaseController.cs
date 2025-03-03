using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using Utilities;
using System.Data.SqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Web.Configuration;

namespace DataStationApi.Controllers
{
    [Authorize]
    public class DatabaseController : ApiController
    {
        readonly bool testMode = WebConfigurationManager.AppSettings["RunMode"].ToUpper().Equals("TEST");

        public HttpResponseMessage Get(String database, String table, String outputType = "JSON", String schema = "dbo")
        {
            string cleanTable = table.Replace("'", "").Replace(";", "").Replace(@"""", "");
            string cleanSchema = schema.Replace("'", "").Replace(";", "").Replace(@"""", "");
            String query = "Select * from " + cleanSchema + "." + cleanTable;
            return Services.DataExtractor.DataGetter(database, query, outputType, Request, User, cleanTable);
        }

        //this method takes dynamic parameters per Steve's request
        //format is filterBy{ColumnName}={value}
        [HttpGet]
        public HttpResponseMessage QueryJobConfigTable(String table, bool testMode, String outputType = "JSON", String schema = "dbo", bool recordFailures=false)
        {
            string cleanTable = table.Replace("'", "").Replace(";", "").Replace(@"""", "");
            string cleanSchema = schema.Replace("'", "").Replace(";", "").Replace(@"""", "");
            string database = testMode ? "PhpConfgTest" : "PhpConfgProd";
            Data.AppNames dataSource = testMode ? Data.AppNames.ExampleTest : Data.AppNames.ExampleProd;

            //get column names from table for trimming
            string columnQuery = string.Format("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = N'{0}'", cleanTable);
            IEnumerable<string> columnNames = ExtractFactory.ConnectAndQuery<string>(dataSource, columnQuery);

            //build select string
            string selectString = "select ";
            foreach(string columnName in columnNames)
            {
                selectString += "rtrim(ltrim(" + columnName + ")) as " + columnName + ", ";
            }
            selectString = selectString.Substring(0, selectString.Length - 2); //get rid of trailing comma
        
            //get dynamic parameters for filtering
            IEnumerable<KeyValuePair<string, string>> parameters = Request.GetQueryNameValuePairs();
            IEnumerable<KeyValuePair<string, string>> filterParameters = parameters.Where(x => x.Key.Contains("filterBy"));
            string query = "";
            if (filterParameters.Count() > 0)
            { 
                query += " WHERE 1=1"; //dummy so you don't have to mess around with "is it where or is it and"
            }
            foreach(KeyValuePair<string, string> filterParameter in filterParameters)
            {
                query += " AND " + filterParameter.Key.Replace("filterBy", "") + " = '" + filterParameter.Value + "'";
            }

            // if recordFailures is set to true, let's record any failed lookups
            if (recordFailures)
            {
                // let's see if the query will return any data.
                DataTable outputData = ExtractFactory.ConnectAndQuery(dataSource, 
                    "SELECT COUNT(*) AS c FROM "+cleanSchema+"."+cleanTable+query);
                
                if ((int)outputData.Rows[0]["c"]==0)
                {
                    // no rows returned - which means it's a failed lookup.
                    // now we need to work out what the name of the failures table is.
                    // for example, if table name is IntEng_Files_C
                    // then the failed lookup table name would be IntEng_Files_Failed_C
                    string failTable = cleanTable.Substring(0, cleanTable.Length - 2)
                        + "_Failed"
                        + cleanTable.Substring(cleanTable.Length - 2, 2);

                    // summary of following query:
                    // if (failed lookup record exists)
                    //     update existing row with new LastLookup date and increase the hit count
                    // else
                    //     create a new failed lookup record
                    string q = "";
                    //
                    q+= "IF (SELECT COUNT(*) FROM "+cleanSchema+"."+failTable+" WHERE 1=1";
                    foreach (KeyValuePair<string, string> filterParameter in filterParameters)
                    {
                        q += " AND [" + filterParameter.Key.Replace("filterBy", "") + "]='" + String.Format("{0}", filterParameter.Value) + "'";
                    }
                    q = q+") > 0\n";
                    //
                    q += "UPDATE " + cleanSchema + "." + failTable 
                        + " SET HitCount = HitCount + 1, LastLookup = GETDATE() WHERE 1=1";
                    foreach (KeyValuePair<string, string> filterParameter in filterParameters)
                    {
                        q += " AND [" + filterParameter.Key.Replace("filterBy", "") + "]='"+ String.Format("{0}", filterParameter.Value) + "'";
                    }
                    //
                    q += "\nELSE\n";
                    //
                    q += "INSERT INTO [dbo].[" + failTable + "] (";
                    foreach (KeyValuePair<string, string> filterParameter in filterParameters)
                    {
                        q += "[" + filterParameter.Key.Replace("filterBy", "") + "],";
                    }
                    q = q + "[LastLookup],[HitCount]) VALUES (";
                    foreach (KeyValuePair<string, string> filterParameter in filterParameters)
                    {
                        q += "'" + String.Format("{0}", filterParameter.Value) + "',";
                    }
                    q += "getdate(),1)";

                    ExtractFactory.ConnectAndQuery(dataSource,q);
                }
            }
            
            return Services.DataExtractor.DataGetter(database, 
                selectString + " from " + cleanSchema + "." + cleanTable + query, 
                outputType, Request, User, cleanTable);
        }

        [Obsolete("Post is deprecated, please use CallDBStoredProcedure instead.")]
        [HttpPost]
        [Route("api/Database/{database}/SP/{procedure}")]
        public HttpResponseMessage Post(String database, String procedure, [FromBody]JObject paramBody, String schema = "dbo", String outputType = "JSON")
        {
            List<SqlParameter> parameterSet = new List<SqlParameter>();

            if (paramBody != null)
            {
                foreach(JProperty tok in paramBody.Properties())
                {
                    String parameterName = tok.Name;
                    SqlParameter parm = new SqlParameter("@" + parameterName, paramBody[parameterName].ToString());
                    parameterSet.Add(parm);
                }
            }
            string cleanProcedure = procedure.Replace("'", "").Replace(";", "").Replace(@"""", "");
            string cleanSchema = schema.Replace("'", "").Replace(";", "").Replace(@"""", "");
            String procCall = cleanSchema + "." + cleanProcedure;
            return Services.DataExtractor.DataGetter(database, procCall, outputType, Request, User, cleanProcedure, parameterSet, true);

        }

        /// <summary>
        /// /api/Database/CallDBStoredProcedure
        ///
        /// DatabaseName and StoredProcedureName are required in the JSON.
        /// 
        /// Example JSON in message body:
        /// {
        ///   "DatabaseName": "ClinicalWebDbTest",
        ///   "StoredProcedureName": "GetHistologyRequestRecords_SP",
        ///   "Parameters": {
        ///     "StartDate": "2022-01-01",
        ///     "EndDate": "2023-01-01"
        ///   },
        ///   "Schema": "WEB0022",
        ///   "VerboseLogging": "true"
        /// }
        /// 
        /// </summary>
        /// <param name="pMessageParameters"></param>
        /// <returns></returns>
        [HttpPost]
        public HttpResponseMessage CallDBStoredProcedure(MessageParameters pMessageParameters)
        {
            Logger log = Services.Log.getLog(User);
            log.ProcessId = "CallDBStoredProcedure";
            HttpResponseMessage goodResp = new HttpResponseMessage(HttpStatusCode.OK);
            HttpResponseMessage badResp = new HttpResponseMessage(HttpStatusCode.BadRequest);
            string defaultDB = testMode ? "PhpConfgTest" : "PhpConfgProd";

            if (pMessageParameters.VerboseLogging)
            {
                log.WriteToLog($"Received the following parameters: {JsonConvert.SerializeObject(pMessageParameters)}");
            }

            try
            {
                string dbName = pMessageParameters.DatabaseName is null ? defaultDB : pMessageParameters.DatabaseName;
                string spName = pMessageParameters.StoredProcedureName;
                string schema = pMessageParameters.Schema;
                Dictionary<string, string> spParams = new Dictionary<string, string>(pMessageParameters.Parameters);
                List<string> keys = new List<string>();

                //Make sure parameters that contain apostrophes are escaped before running SQL.
                foreach (KeyValuePair<string, string> item in spParams)
                {
                    keys.Add(item.Key);
                }

                foreach (string item in keys)
                {
                    if (spParams[item].Contains("'"))
                    {
                        spParams[item] = spParams[item].Replace("'", "''");
                    }
                }

                //Creating this copy so it doesn't change the parameters as you modify the dictionary in the validation.
                Dictionary<string, string> validateSpParams = new Dictionary<string, string>(spParams);
                string logMessage = Services.Validators.ValidateParameters(dbName, spName, schema, validateSpParams);

                if (logMessage != "")
                {
                    log.WriteToLog(logMessage);
                    return badResp;
                }

                string fullExecSP = "EXEC " + schema + "." + spName;

                if (spParams.Count > 0)
                {
                    int i = 1;

                    foreach (KeyValuePair<string, string> item in spParams)
                    {
                        string a = i > 1 ? " , " : " ";
                        fullExecSP = fullExecSP + a + "@" + item.Key + " = '" + item.Value + "'";
                        i++;
                    }
                }

                Data.AppNames dataSource = (Data.AppNames)Enum.Parse(typeof(Data.AppNames), dbName);
                DataTable returnData = ExtractFactory.ConnectAndQuery(dataSource, fullExecSP);
                string json = JsonConvert.SerializeObject(returnData, Newtonsoft.Json.Formatting.Indented);
                goodResp.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                return goodResp;
            }
            catch (Exception ex)
            {
                log.WriteToLog($"Attempted to use the following. pParameters: {JsonConvert.SerializeObject(pMessageParameters)}");
                log.WriteToLog(ex.ToString());
                return badResp;
            }
        }



        public class MessageParameters
        {
            public string DatabaseName { get; set; } = null;
            public string StoredProcedureName { get; set; }
            public Dictionary<string, string> Parameters { get; set; }
            public string Schema { get; set; } = "dbo";
            public bool VerboseLogging { get; set; } = false;
        }
    }
}
