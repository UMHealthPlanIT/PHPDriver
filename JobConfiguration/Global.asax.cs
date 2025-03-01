using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Configuration;

namespace JobConfiguration
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }
        protected void Application_BeginRequest()
        {

            Boolean prodMode = ConfigurationManager.AppSettings["RunMode"].ToUpper() == "PROD";

            if (prodMode)
            {
                JobConfiguration.Models.AbstractController.dataSource = Utilities.Data.AppNames.PhpConfgProd;
                JobConfiguration.Controllers.BulkUpdateController.dataSource = Utilities.Data.AppNames.PhpConfgProd;

            }
            else
            {
                JobConfiguration.Models.AbstractController.dataSource = Utilities.Data.AppNames.PhpConfgTest;
                JobConfiguration.Controllers.BulkUpdateController.dataSource = Utilities.Data.AppNames.PhpConfgTest;

            }

            if (!Context.Request.IsSecureConnection)
            {
                UriBuilder uri = new UriBuilder(Context.Request.Url);
                // This is an insecure connection, so redirect to the secure version
                uri.Scheme = "https";
                if (!uri.Host.Equals("localhost") && prodMode)
                {

                    uri.Port = 443;

                    Response.Redirect(uri.ToString());
                }
            }

        }
    }
}
