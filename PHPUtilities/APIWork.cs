using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.IO;
using System.Web;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using RestSharp;
using RestSharp.Authenticators;

namespace Utilities
{
    public class APIWork
    {
        public class DataSource
        {
            public string name;
            public bool sourceReady;
            public bool manualSwitch;
            public bool overrideReadinessQuery;
            public bool queryResult;
        }

        public static JObject CallRestEndPoint(String endpoint, String method, String authHeader, String apiKey, String postBody = "", string contentType = "application/json", string username = null, string password = null, int timeoutInSeconds = 100)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(endpoint);
            request.Accept = "application/json";
            request.ContentType = contentType;
            request.Method = method;
            request.Timeout = timeoutInSeconds * 1000; //request.Timeout is in milliseconds
            if (username == null || password == null)
            {
                request.Headers.Add(authHeader, apiKey);
            }
            else
            {
                request.Headers["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.Default.GetBytes(username + ":" + password));
            }

            if (postBody != "")
            {
                byte[] binary = System.Text.ASCIIEncoding.Default.GetBytes(postBody);
                request.ContentLength = binary.Length;

                using (System.IO.Stream requestStream = request.GetRequestStream())
                {
                    requestStream.Write(binary, 0, binary.Length);
                }
            }

            string result;
            var httpResponse = (HttpWebResponse)request.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                result = streamReader.ReadToEnd();
            }
            return (JObject)JsonConvert.DeserializeObject(result);
        }

        public static JArray CallRestEndPointToJArray(String endpoint, String method, String authHeader, String apiKey, String postBody = "", string contentType = "application/json", string username = null, string password = null)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(endpoint);
            request.Accept = "application/json";
            request.ContentType = contentType;
            request.Method = method;
            if (username == null || password == null)
            {
                request.Headers.Add(authHeader, apiKey);
            }
            else
            {
                request.Headers["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.Default.GetBytes(username + ":" + password));
            }

            if (postBody != "")
            {
                byte[] binary = System.Text.ASCIIEncoding.Default.GetBytes(postBody);
                request.ContentLength = binary.Length;

                using (System.IO.Stream requestStream = request.GetRequestStream())
                {
                    requestStream.Write(binary, 0, binary.Length);
                }
            }

            string result;
            var httpResponse = (HttpWebResponse)request.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                result = streamReader.ReadToEnd();
            }
            return (JArray)JsonConvert.DeserializeObject(result);
        }

        public static string GenerateJWTToken(string keyString, string issuer, DateTime issuedTime, int expirationTimeInDays, string audience, string subject)
        {
            string tokenString = "";

            int issuedTimeInSeconds = (int)(issuedTime.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            int expirationTimeInSeconds = (int)(issuedTime.AddDays(expirationTimeInDays).Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            SymmetricSecurityKey securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
            SigningCredentials credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256Signature);
            JwtHeader header = new JwtHeader(credentials);

            JwtPayload payload = new JwtPayload
            {
                { "iss", issuer},
                { "iat", issuedTimeInSeconds},
                { "exp", expirationTimeInSeconds},
                { "aud", audience},
                { "sub", subject},
            };

            JwtSecurityToken secToken = new JwtSecurityToken(header, payload);
            JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();

            tokenString = handler.WriteToken(secToken);

            return tokenString;
        }

        public enum AuthenticationType
        {
            Basic, NTLM, None
        }
        public static T CallAPI<T>(string URL, RestSharp.Method method, AuthenticationType authenticationType, Data creds = null, object body = null, string selectToken = "")
        {
            string deserializeString = "";
            T results = default(T);


            RestClient restClient = new RestClient(URL);
            if(authenticationType == AuthenticationType.NTLM)
            {
                restClient.Authenticator = new RestSharp.Authenticators.NtlmAuthenticator();
            }
            if(authenticationType == AuthenticationType.Basic)
            {
                restClient.Authenticator = new HttpBasicAuthenticator(creds.username, creds.password);
            }
            restClient.Timeout = -1;
            restClient.ReadWriteTimeout = -1;
            RestRequest restRequest = new RestRequest(method);
            
            if (body != null)
            {
                restRequest.AddJsonBody(body);
            }

            IRestResponse restResponse = restClient.Execute(restRequest);
            
            deserializeString = restResponse.Content;

            if(selectToken != "")
            {
                JObject o = JObject.Parse(deserializeString);
                JToken jToken = o.GetValue(selectToken);
                results = jToken.ToObject<T>();
            }
            else
            {
                results = JsonConvert.DeserializeObject<T>(deserializeString);
            }

            return results;


        }
        public static string CallAPI(string URL, RestSharp.Method method, AuthenticationType authenticationType, Data creds = null, object body = null, string selectToken = "")
        {
            string deserializeString = "";
            string results = "";


            RestClient restClient = new RestClient(URL);
            if (authenticationType == AuthenticationType.NTLM)
            {
                restClient.Authenticator = new RestSharp.Authenticators.NtlmAuthenticator();
            }
            if (authenticationType == AuthenticationType.Basic)
            {
                restClient.Authenticator = new HttpBasicAuthenticator(creds.username, creds.password);
            }
            restClient.Timeout = -1;
            restClient.ReadWriteTimeout = -1;
            RestRequest restRequest = new RestRequest(method);

            if (body != null)
            {
                restRequest.AddJsonBody(body);
            }

            IRestResponse restResponse = restClient.Execute(restRequest);

            results = restResponse.Content;

            

            return results;


        }

        public static JObject CallDatastation(string endpoint, string method, string payload = "", string contentType = "application/json")
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(endpoint);
            request.UseDefaultCredentials = true;
            request.Accept = "application/json";
            request.ContentType = contentType;
            request.Method = method;

            if (payload != "")
            {
                byte[] binary = Encoding.Default.GetBytes(payload);
                request.ContentLength = binary.Length;

                using (Stream requestStream = request.GetRequestStream())
                {
                    requestStream.Write(binary, 0, binary.Length);
                }
            }

            string result;
            var httpResponse = (HttpWebResponse)request.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                result = streamReader.ReadToEnd();
            }
            return (JObject)JsonConvert.DeserializeObject(result);
        }
    }
}
