
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using System.Net.Http;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Schema;

namespace Utilities.Integrations
{
    public class GitHub
    {
        public string baseUri = "https://api.github.com";
        private RestClient client;
        private string jwttoken;
        private string token;
        private Dictionary<string, string> header;
        private int currentPage;
        public GitHub()
        {
            client = new RestClient(baseUri);
            GetJwtToken();
            GetAppToken();
            header = new Dictionary<string, string>()
            {
                { "Authorization",$"Bearer {token}"},
                { "X-GitHub-Api-Version","2022-11-28"}
            };
        }

        private void GetAppToken()
        {
            HttpClient client = new HttpClient();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://api.github.com/app/installations/{}/access_tokens");
            request.Headers.Add("Authorization", $"Bearer {jwttoken}");
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            request.Headers.Add("User-Agent", "");
            var resp = client.SendAsync(request).Result;
            var content = resp.Content.ReadAsStringAsync().Result;
            var apptoken = JsonConvert.DeserializeObject<AppToken>(content);
            token = apptoken.token;
        }

        private void GetJwtToken()
        {
            HttpClient client = new HttpClient(new HttpClientHandler()
            {
                UseDefaultCredentials = true
            });
            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "http://api/auth/jwt/token?id=1");
            var response = client.SendAsync(httpRequestMessage).Result;
            var content = response.Content.ReadAsStringAsync().Result;
            var auth = JsonConvert.DeserializeObject<Auth>(content);
            jwttoken = auth.Token;

        }



        private void AddHeaders(RestRequest request)
        {
            foreach (var header in header)
            {
                request.AddHeader(header.Key, header.Value);
            }
        }

        public class AppToken
        {
            public string token { get; set; }
            public DateTime expires_at { get; set; }
            public Permissions permissions { get; set; }
            public string repository_selection { get; set; }
        }

        public class Permissions
        {
            public string issues { get; set; }
            public string metadata { get; set; }
        }


        public class Auth
        {
            [JsonProperty(PropertyName = "token")]
            public string Token { get; set; }
        }

        public class Issue
        {
            public long id { get; set; }
            public string node_id { get; set; }
            public string url { get; set; }
            public string repository_url { get; set; }
            public string labels_url { get; set; }
            public string comments_url { get; set; }
            public string events_url { get; set; }
            public string html_url { get; set; }
            public int number { get; set; }
            public string state { get; set; }
            public string title { get; set; }
            public string body { get; set; }
            public bool locked { get; set; }
            public string active_lock_reason { get; set; }
            public int comments { get; set; }
            public object closed_at { get; set; }
            public DateTime created_at { get; set; }
            public DateTime updated_at { get; set; }
            public string author_association { get; set; }
            public string state_reason { get; set; }
        }




    }
}
