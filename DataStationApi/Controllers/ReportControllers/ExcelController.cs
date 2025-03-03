using Newtonsoft.Json;
using Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Configuration;
using System.Web.Http;
using static DataStationApi.Controllers.DatabaseController;

namespace DataStationApi.Controllers.ReportControllers
{
    [Authorize]
    public class ExcelController : ApiController
    {
        readonly bool testMode = WebConfigurationManager.AppSettings["RunMode"].ToUpper().Equals("TEST");

        [HttpPost]
        public HttpResponseMessage RunExcelReportFromQuery(ExcelParameters parameters)
        {
            Logger log = Services.Log.getLog(User);
            HttpResponseMessage goodResp = new HttpResponseMessage(HttpStatusCode.OK);
            HttpResponseMessage badResp = new HttpResponseMessage(HttpStatusCode.BadRequest);
            string defaultDB = testMode ? "PhpConfgTest" : "PhpConfgProd";


            try
            {
                string dbName = parameters.DatabaseName is null ? defaultDB : parameters.DatabaseName;
                Data.AppNames dataSource = (Data.AppNames)Enum.Parse(typeof(Data.AppNames), dbName);
                string spParams = string.Join(",", parameters.Parameters.Select(x => $"@{x.Key} = '{x.Value.Replace("'", "''")}'").ToList());
                string query = $"exec {parameters.Schema}.{parameters.StoredProcedureName} {spParams}";
                string cleanFileName = Regex.Replace(parameters.FileName, @"[\/?:*""><|]+", "", RegexOptions.Compiled);
                string modifiedFileName = $"ExcelController/{cleanFileName}";
                string validateMessage = Services.Validators.ValidateParameters(parameters.DatabaseName, parameters.StoredProcedureName, parameters.Schema, parameters.Parameters);

                if (validateMessage != "")
                {
                    log.WriteToLog(validateMessage);
                    badResp.Content = new StringContent(validateMessage);
                    return badResp;
                }

                string fileName = ExtractFactory.RunExcelExtractForDataSet(dataSource, query, log, modifiedFileName, parameters.TabNames.ToList());
                byte[] fileBytes = File.ReadAllBytes(fileName);
                MemoryStream fileStream = new MemoryStream(fileBytes);
                goodResp.Content = new StreamContent(fileStream);
                goodResp.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment");
                goodResp.Content.Headers.ContentDisposition.FileName = Path.GetFileName(fileName);
                goodResp.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                return goodResp;
            }
            catch (Exception ex)
            {
                log.WriteToLog($"Attempted to use the following parameters: {JsonConvert.SerializeObject(parameters)}");
                log.WriteToLog(ex.ToString());
                badResp.Content = new StringContent(ex.ToString());
                return badResp;
            }
        }

        public class ExcelParameters
        {
            public string DatabaseName { get; set; } = null;
            public string StoredProcedureName { get; set; }
            public Dictionary<string, string> Parameters { get; set; }
            public string Schema { get; set; } = "dbo";
            public string FileName { get; set; }
            public string[] TabNames { get; set; } = new string[] { "data" };
        }
    }
}