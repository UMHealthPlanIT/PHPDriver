using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http.Filters;
using Utilities;

namespace DataStationApi.Services
{
    public class LogExceptionFilterAttribute : ExceptionFilterAttribute
    {

        public override void OnException(HttpActionExecutedContext context)
        {
            ErrorLogService.LogError(context.Exception);
        }

        public static class ErrorLogService
        {
            public static void LogError(Exception ex)
            {
                Logger log = Services.Log.getLog(HttpContext.Current.User);

                log.WriteToLog(ex.ToString());
            }
        }

    }
}