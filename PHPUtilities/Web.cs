using System;
using System.Collections.Generic;
using System.Web;
using System.Net;
using System.DirectoryServices.AccountManagement;
using System.Collections;
using System.Linq;

namespace Utilities
{
    public class Web
    {
        public static string RedirectHttpToHttps(HttpContext Context, HttpResponse response, int TestHttpPort, int TestHttpsPort, out Boolean redirect)
        {
            UriBuilder uri = new UriBuilder(Context.Request.Url);
            uri.Scheme = "https";

            if (!Context.Request.IsSecureConnection)
            {
                // This is an insecure connection, so redirect to the secure version
                if (!uri.Host.Equals("localhost"))
                {
                    if (uri.Port == TestHttpPort) //test app is on 555
                    {
                        uri.Port = TestHttpsPort;
                    }
                    else
                    {
                        uri.Port = 443;
                    }
                    redirect = true;
                }
                else
                {
                    redirect = false;
                }


            }
            else
            {
                redirect = false;
            }

            return uri.ToString();
        }
        /// <summary>
        /// REST call to a web URL, and return response as a string.
        /// </summary>
        /// <param name="url">String that is the URL that will be the endpoint with any parameters to be passed.</param>
        /// <param name="headers">Web Client headers that are to be added.</param>
        /// <returns></returns>
        public static string ConnectAndQueryWebURL(string url, Dictionary<string, string> headers = null, bool defaultCreds = true)
        {
            string responseString = "";
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls12;
            using (WebClient client = new WebClient())
            {
                client.UseDefaultCredentials = defaultCreds;
                if (headers != null)
                {
                    foreach (KeyValuePair<string, string> header in headers)
                    {
                        client.Headers.Add(header.Key, header.Value);
                    }
                }

                responseString = client.DownloadString(url);
            }

            return responseString;
        }

        public class WEB0004_RunPermissions_C
        {
            public string JobCode { get; set; }
            public string Permission { get; set; }
        }

        //this function only returns the jobs that the logged in user has access to
        public static void SetJobsAllowedToRunForUser(String adUser, Logger procLog, List<JobIndexRunRequest> sharePointJobs)
        {

            procLog.WriteToLog("Checking Allowed Jobs for User :" + adUser);

            PrincipalContext ctx = new PrincipalContext(ContextType.Domain, "");
            GroupPrincipal developers = GroupPrincipal.FindByIdentity(ctx, "");
            GroupPrincipal interfacers = GroupPrincipal.FindByIdentity(ctx, "");
            GroupPrincipal admins = GroupPrincipal.FindByIdentity(ctx, "");
            GroupPrincipal analysts = GroupPrincipal.FindByIdentity(ctx, "");
            GroupPrincipal itphp = GroupPrincipal.FindByIdentity(ctx, "");

            UserPrincipal user = UserPrincipal.FindByIdentity(ctx, adUser);

            //dictionary with key being the ad group name and the value being the list of jobs connected to that ad group
            Dictionary<string, List<string>> permissions = Utilities.ExtractFactory.ConnectAndQuery<WEB0004_RunPermissions_C>(procLog.LoggerPhpConfig, "select * from WEB0004_RunPermissions_C").GroupBy(x => x.Permission).ToDictionary(x => x.Key, x => x.Select(y => y.JobCode).ToList());

            bool allowAll = false, allowMrData = false, allowPHP = false;
            List<string> customJobsAllowed = new List<string>();

            if (user.IsMemberOf(developers) || user.IsMemberOf(interfacers))
            {
                procLog.WriteToLog("Allowed to see all jobs, member of ");
                allowAll = true;
            }
            else if (user.IsMemberOf(admins) || user.IsMemberOf(analysts))
            {
                procLog.WriteToLog("Allowed to see all mr/ms data jobs, member of ");
                allowMrData = true;
            }
            else if (user.IsMemberOf(itphp))
            {
                procLog.WriteToLog("Allowed to see all php jobs, member of ");
                allowPHP = true;
            }

            // only look for individual group permission if it's not us
            if (!allowAll)
            {
                procLog.WriteToLog(adUser + " is not one of our IT folks, seeing what limited permissions they should have");

                string customJobsLog = "";
                //loop through each permission group and add the jobs if the user is a member of the group
                foreach (var entry in permissions)
                {
                    string groupName = entry.Key;
                    GroupPrincipal group = GroupPrincipal.FindByIdentity(ctx, groupName);
                    if (group != null && user.IsMemberOf(group))
                    {
                        customJobsAllowed.AddRange(entry.Value);
                        foreach (var job in entry.Value)
                            customJobsLog += job + " ";
                    }
                }

                procLog.WriteToLog("Allowed to see custom jobs: " + customJobsLog);

            }

            //loop through all jobs and see if user has access to it
            foreach (JobIndexRunRequest job in sharePointJobs)
            {
                if (allowAll || customJobsAllowed.Contains(job.JobId) || (allowMrData && (job.Tool.Contains("MrData") || job.Tool.Contains("MsData"))) || (allowPHP && job.Department.Contains("PHP")))
                {
                    job.RunnableByThisUser = true;
                }
                else
                {
                    job.RunnableByThisUser = false;
                }
            }
            return;
        }

    }
}
