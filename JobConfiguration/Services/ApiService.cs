using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace JobConfiguration.Services
{
    public class ApiService
    {
        public static T CallApi<T>(String endPoint, String method, String payload = "", Dictionary<String, String> headers = null)
        {
            WebRequest http = WebRequest.Create(endPoint);

            http.UseDefaultCredentials = true;
            http.Method = method;
            http.ContentType = "application/json";
            
            if (headers != null)
            {
                foreach (String headerName in headers.Keys)
                {
                    http.Headers.Add(headerName, headers[headerName]);
                }
            }

            if (payload != "")
            {
                byte[] binary = System.Text.ASCIIEncoding.Default.GetBytes(payload);
                http.ContentLength = binary.Length;

                using (System.IO.Stream requestStream = http.GetRequestStream())
                {
                    requestStream.Write(binary, 0, binary.Length);
                }
            }

            using (Stream ms = http.GetResponse().GetResponseStream())
            {
                StreamReader reader = new StreamReader(ms, System.Text.Encoding.UTF8);

                String responseString = reader.ReadToEnd();

                return JsonConvert.DeserializeObject<T>(responseString);
            }
        }
    }
}