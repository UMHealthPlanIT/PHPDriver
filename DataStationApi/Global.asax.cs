using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Configuration;
using Utilities;

namespace DataStationApi
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            GlobalConfiguration.Configuration.Filters.Add(new Services.LogExceptionFilterAttribute());
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        /// <summary>
        /// Note this method doesn't get called for exceptions arising from API Controllers, thus we had to do the LogExceptionFilterAttribute class
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void Application_Error(object sender, EventArgs e)
        {
            
            Exception ex = Server.GetLastError();

            Logger log = Services.Log.getLog(Context.User);

            log.WriteToLog(ex.ToString());
        }
    }
}
