using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Net;
using System.Net.Http;
using System.IO;
using Newtonsoft.Json;
using System.Web.Configuration;

namespace RunRequest.Services
{
    public class SharedServices
    {
        public static T callDataApi<T>(String endPoint)
        {
            String dataStationPointer = WebConfigurationManager.AppSettings["DataStationBaseUrl"];
            String endPointUrl = dataStationPointer + endPoint;

            WebRequest http = WebRequest.Create(endPointUrl);

            http.UseDefaultCredentials = true;

            http.Method = "GET";

            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

            using (Stream ms = http.GetResponse().GetResponseStream())
            {
                StreamReader reader = new StreamReader(ms, System.Text.Encoding.UTF8);

                String responseString = reader.ReadToEnd();

                return JsonConvert.DeserializeObject<T>(responseString);
            }
        }

        public static string CallDriverApi(string caller ,string jobID, bool testMode, string requestor, string json, string launchServerOwner, bool rerun = false)
        {
            string test = testMode ? "Test" : "Prod";
            string env = "t";
            string launchServer = "1";
            if (launchServerOwner == "PHP")
            {
                launchServer = "2";
            }
            if (!testMode)
            {
                env = "p";
            }
            string serverName = "driver" + env + "vas0" + launchServer;

            Uri uri = new Uri(string.Format("http://{4}/api/DriverProcesses/Start/{0}/{1}/{2}/{3}?rerun={5}", caller, jobID, test, requestor.Replace('\\', '~'), serverName, rerun));

            var httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
            httpWebRequest.UseDefaultCredentials = true;
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";
            httpWebRequest.Timeout = 1000 * 60 * 10;

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                streamWriter.Write(json);
            }

            string result = "";
            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                result = streamReader.ReadToEnd();
            }
            httpResponse.Close();

            return result;
        }
    }
}