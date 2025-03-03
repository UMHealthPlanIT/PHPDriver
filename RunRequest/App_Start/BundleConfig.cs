using System.Web;
using System.Web.Optimization;

namespace RunRequest
{
    public class BundleConfig
    {
        // For more information on bundling, visit http://go.microsoft.com/fwlink/?LinkId=301862
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                        "~/Scripts/jquery-{version}.js",
                        "~/Scripts/jquery-ui-1.12.1.js"));

            // Use the development version of Modernizr to develop with and learn from. Then, when you're
            // ready for production, use the build tool at http://modernizr.com to pick only the tests you need.
            bundles.Add(new ScriptBundle("~/bundles/modernizr").Include(
                        "~/Scripts/modernizr-*"));

            bundles.Add(new ScriptBundle("~/bundles/bootstrap").Include(
                      "~/Scripts/bootstrap.js",
                      "~/Scripts/respond.js"));

            bundles.Add(new StyleBundle("~/Content/css").Include(
                      "~/Content/bootstrap.css",
                      "~/Content/Site.css",
                      "~/Content/font-awesome.css",
                      "~/Content/themes/base/jquery-ui.min.css",
                      "~/Content/all.css",
                      "~/Content/select2.min.css"));

            bundles.Add(new ScriptBundle("~/bundles/select2").Include(
                "~/Scripts/select2.full.min.js"));

            bundles.Add(new ScriptBundle("~/bundles/unobtrusive").Include(
                 "~/Scripts/jquery.unobtrusive*"));
            
            bundles.Add(new ScriptBundle("~/bundles/plumage").Include(
                        "~/Scripts/plumage.js",
                        "~/Scripts/navigation.js",
				        "~/Scripts/navigation-parameters.js",
                        "~/Scripts/navigation-parametersjc.js"));

            bundles.Add(new StyleBundle("~/Content/plumage").Include(
                        "~/Content/plumage.css"));
        }
    }
}
