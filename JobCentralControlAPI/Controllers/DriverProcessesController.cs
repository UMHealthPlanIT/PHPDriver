using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Utilities;

namespace JobCentralControlAPI.Controllers
{
    public class DriverProcessesController : ApiController
    {
        private static bool TestMode = System.Configuration.ConfigurationManager.AppSettings["RunMode"].ToUpper() == "TEST" ? true : false;
        private static bool VerboseLogging = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["VerboseLogging"]);
        private Logger procLog = new Logger(new Logger.LaunchRequest("WEB0019", TestMode, null));

        [System.Web.Http.Route("api/DriverProcesses")]
        public IEnumerable<Models.DriverProcess> Get()
        {
            return GetDriverProcesses.GetDriverProcess();
        }

        [System.Web.Http.Route("api/DriverProcesses/{id}")]
        public string Delete(int id)
        {
            return KillDriverProcess.KillProcess(id);
        }

        [System.Web.Http.AcceptVerbs("POST")]
        [System.Web.Http.Route("api/DriverProcesses/Start/{source}/{jobID}/{testMode}/{requestor}")]
        public string Start(string source, string jobID, string testMode, string requestor, JObject args, bool rerun = false)
        {
            try
            {
                UniversalLogger.WriteToLog(procLog, source + "; " + jobID + "; " + testMode + "; " + requestor + (rerun ? "; rerun" : "") + "; " + args.ToString());
            }
            catch (Exception E)
            {

            }

            bool sourceReady = DataSourceManagement.JobSourcesAreReady(jobID, procLog);
            
            return LaunchDriverProcess.LaunchProcess(source, jobID, testMode, requestor, sourceReady, args, rerun);
        }
    }
}