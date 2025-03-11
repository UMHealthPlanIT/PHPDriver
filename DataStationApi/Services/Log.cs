using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Utilities;
using System.Web.Configuration;


namespace DataStationApi.Services
{
    public class Log
    {
        public static Logger getLog(System.Security.Principal.IPrincipal user)
        {

            String runModeString = WebConfigurationManager.AppSettings["RunMode"];
            Logger webLog;

            if (runModeString.ToUpper() == "TEST")
            {
                webLog = new Logger("WEB0006", true, true);
                webLog.WriteToLog(user.Identity.Name + " accessing DataStationAPI in Test Mode");
            }
            else
            {
                webLog = new Logger("WEB0006", false, true);
                webLog.WriteToLog(user.Identity.Name + " accessing DataStationAPI in Prod Mode");
            }


            return webLog;
        }
    }
}