using Utilities;
using System;
using System.Net;
using System.Net.Http;
using System.Web.Configuration;
using System.Web.Http;

namespace DataStationApi.Controllers
{
    public class HealthController : ApiController
    {
        readonly bool testMode = WebConfigurationManager.AppSettings["RunMode"].ToUpper().Equals("TEST");

        [HttpGet]
        public HttpResponseMessage Basic()
        {
            return Request.CreateResponse(HttpStatusCode.NoContent);
        }

        [HttpGet]
        public HttpResponseMessage Full()
        {
            HttpResponseMessage response;
            Data.AppNames source = testMode ? Data.AppNames.ExampleTest : Data.AppNames.ExampleProd;
            
            try
            {
                ExtractFactory.ConnectAndQuery(Services.Log.getLog(User), source, "SELECT GETDATE()");
                response = Request.CreateResponse(HttpStatusCode.NoContent);
            }
            catch (Exception e)
            {
                response = Request.CreateResponse((HttpStatusCode) 207, $"DataStation is alive, but could not execute a query against PhpConfig.\n{e.Message}");
            }

            return response;
        }
    }
}