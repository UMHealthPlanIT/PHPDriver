using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;

namespace Utilities.Integrations
{
    public class SourceControl
    {


        /// <summary>
        /// This token is a private key that allows access to VSTS and is configured on a user level.
        /// Go to "phpsso.visualstudio.com/_details/security/tokens" to set one up under your account.
        /// </summary>
        /// <returns>Visual Studio Team Services API Access Key</returns>
        private static string GetPersonalAccessToken()
        {
            Data sourceControlCreds = new Data(Data.AppNames.ExampleProd);
            //phpsso.visualstudio.com/_details/security/tokens
            return "Basic" +
                Convert.ToBase64String(
                    System.Text.ASCIIEncoding.ASCII.GetBytes(
                        string.Format("{0}:{1}", "", sourceControlCreds.password)));
        }

        public static JObject CallVisualStudioAPI(String endPoint, Boolean local = false, String projectCollection = "")
        {
            string resourceUrl;

            resourceUrl = endPoint;


            HttpWebRequest wreq = HttpWebRequest.Create(resourceUrl) as HttpWebRequest;

            if (!local)
            {
                wreq.UseDefaultCredentials = false;

                wreq.Headers.Add("Authorization", GetPersonalAccessToken());

            }
            else
            {
                wreq.UseDefaultCredentials = true;
            }

            wreq.Timeout = 1000000; //timeout should be large in order to upload file which are of large size
            wreq.Accept = "application/json; odata=verbose";

            HttpWebResponse wresp = (HttpWebResponse)wreq.GetResponse();

            if (wresp.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception("VSTS Status Code Not OK");
            }

            using (System.IO.StreamReader sr = new System.IO.StreamReader(wresp.GetResponseStream()))
            {
                String result = sr.ReadToEnd();
                return (JObject)JsonConvert.DeserializeObject(result);

            }
        }

        public static string GenerateSqlQueryFromTfs(string pathToSql, bool onPrem, string[] sqlParams = null, string projectCollection = "")
        {
            JObject result = Utilities.Integrations.SourceControl.CallVisualStudioAPI("items?path=" + pathToSql + "&includeContent=true", onPrem, projectCollection);
            string sqlFromTfs = result["content"].ToString();
            string regexPattern = "";
            int count = 0;
            if(sqlParams != null)
            {
                foreach (string param in sqlParams)
                {
                    regexPattern = @"{(" + count + ")}"; //even I can understand this regex - get number between curly brackets. C# automatically applies global in replace
                    Regex rx = new Regex(regexPattern);
                    MatchCollection matches = rx.Matches(sqlFromTfs);
                    sqlFromTfs = Regex.Replace(sqlFromTfs, regexPattern, param);
                    count++;
                }
            }
            return sqlFromTfs;
        }

    }
}
