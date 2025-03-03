using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Configuration;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace RunRequest
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        // Redirect http requests to the https URL
        protected void Application_BeginRequest()
        {
            bool testMode = Convert.ToBoolean(WebConfigurationManager.AppSettings["testMode"]);

            int httpPort = testMode ? 296 : 80;
            int httpsPort = testMode ? 297 : 443;

            Boolean doWeRedirect;
            String url = Utilities.Web.RedirectHttpToHttps(Context, Response, httpPort, httpsPort, out doWeRedirect);

            if (doWeRedirect)
            {
                Response.Redirect(url);
            }
        }
    }
}
