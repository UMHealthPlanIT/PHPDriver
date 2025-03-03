using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Web.Configuration;

namespace RunRequest.Services
{
    public class LoggingService
    {
        /** 
         ** For this service to work correctly, you must have the the logger endpoints setup in your Web.config
         * 
         * <configuration>
         *      <appSettings>
         *          <add key="CreateLogEndpoint" value="<logger_endpoint>" />
         *      </appSettings>
         *  ...
         *  </configuration>
         *
         *
         *  The EndpointUrl string will then dynamically pull the correct test/prod api string for you.
         **/

        private static readonly String EndpointUrl = WebConfigurationManager.AppSettings["CreateLogEndpoint"];

        public enum LoggerMode
        {
            DEFAULT, TEST, PROD
        }

        public static void WriteToLog(Models.LogEntryModel logEntry, LoggerMode logMode = LoggerMode.DEFAULT)
        {
            Dictionary<String, String> headers = new Dictionary<String, String>();

            if (logMode == LoggerMode.PROD || logMode == LoggerMode.TEST)
            {
                headers.Add("LogDatabase", logMode.ToString());
            }

            String payload = JsonConvert.SerializeObject(logEntry);

            String result = ApiService.CallApi<String>(EndpointUrl, "POST", payload, headers: headers);
            
            if (!String.IsNullOrWhiteSpace(result)) // Should only return a result when something goes wrong.
            {
                throw new Exception(); // Throw custom exception as needed for your web app.
            }
        }

        public static void WriteToLog(String jobIndex, DateTime logDateTime, String loggedByUser, String logContent, String logCategory = "INFO", LoggerMode logMode = LoggerMode.DEFAULT)
        {
            Models.LogEntryModel logEntry = new Models.LogEntryModel()
            {
                JobIndex = jobIndex,
                LogDateTime = logDateTime,
                LoggedByUser = loggedByUser,
                LogContent = logContent,
                LogCategory = logCategory
            };

            WriteToLog(logEntry, logMode: logMode);
        }

    }
}