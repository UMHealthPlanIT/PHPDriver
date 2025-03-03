using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using DataStationApi.Models;
using DataStationApi.Services;
using Newtonsoft.Json;
using Utilities;
using System.Data;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.Web.Configuration;
using System.Collections.Concurrent;


namespace DataStationApi.Controllers
{
    public class WEB0020Controller : ApiController
    {

        /// <summary>
        /// Returns a comparison for either NDC or GPI based on the compareResults pK provided
        /// </summary>
        /// <param name="pkResult">pk from CompareResults table</param>
        /// <param name="version">which version of this api call to use</param>
        /// <returns></returns>
        [HttpGet]
        public HttpResponseMessage compareResult(string pkResult, int v = 1)
        {
            Logger procLog = Services.Log.getLog(User);
            procLog.WriteToLog($"{User} is comparing on {pkResult}");
            string json = "";
            if (v == 2)
            {
                json = JsonConvert.SerializeObject(FormularyUtilities.CompareResultByNDC(pkResult, procLog), Formatting.Indented);
            }
            else
            {
                json = JsonConvert.SerializeObject(FormularyUtilities.compareResultByPk(pkResult, procLog), Formatting.Indented);
            }
            

            HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.OK);
            resp.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            return resp;
        }

        /// <summary>
        /// Gets a full crosswalk of GPI Names to GPI Code, picks min match
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public HttpResponseMessage getGPINames()
        {
            Logger procLog = Services.Log.getLog(User);
            string json = JsonConvert.SerializeObject(FormularyUtilities.getGPINames(procLog), Newtonsoft.Json.Formatting.Indented);

            HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.OK);
            resp.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            return resp;
        }

        /// <summary>
        /// Gets a full crosswalk of NDC names to NDC Code, picks min match
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public HttpResponseMessage getNDCNames()
        {
            Logger procLog = Services.Log.getLog(User);
            string json = JsonConvert.SerializeObject(FormularyUtilities.getNDCNames(procLog), Newtonsoft.Json.Formatting.Indented);

            HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.OK);
            resp.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            return resp;
        }

        /// <summary>
        /// Given TableName pkValue that needs copied over will insert to Config Master
        /// </summary>
        /// <param name="pk">Which table pk value needs to be copied from archive to master</param>
        /// <param name="tableName">Where will it be inserted into</param>
        /// <returns>True/False pass/fail</returns>
        [System.Web.Mvc.HttpPost]
        public IHttpActionResult resolveInsert(string pk, string tableName)
        {
            Logger procLog = Services.Log.getLog(User);
            procLog.WriteToLog($"{User} is copying archive pk: {pk} information from table: {tableName} to config master");

            bool x = FormularyUtilities.InsertPk(procLog, pk, tableName);

            return Json(x);
        }
    }
}
