using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.IO;
using System.DirectoryServices;
using Microsoft.SharePoint.Client;
using ListItem = Microsoft.SharePoint.Client.ListItem;
using RestSharp;
using RestSharp.Authenticators;
using System.Configuration;

namespace Utilities.Integrations
{
    public class SharePoint
    {
        public static JObject CallSharePointRestAPI(String url, int attempts = 3)
        {
            attempts--;
            string response = "";
            HttpWebRequest wreq = HttpWebRequest.Create(url) as HttpWebRequest;
            wreq.UseDefaultCredentials = false;
            NetworkCredential credentials = System.Net.CredentialCache.DefaultNetworkCredentials;
            wreq.Credentials = credentials;
            wreq.Accept = "application/json; odata=verbose";

            try
            {
                HttpWebResponse wresp = (HttpWebResponse)wreq.GetResponse();
                System.IO.StreamReader sr = new System.IO.StreamReader(wresp.GetResponseStream());
                response = sr.ReadToEnd();
                sr.Close();
            }
            catch (Exception ex)
            {
                if (attempts > 0)
                {
                    System.Threading.Thread.Sleep(1 * 60 * 1000);//Wait for 1 minute before trying again                    
                    JObject result = CallSharePointRestAPI(url, attempts);
                    return result;
                }
                else
                {
                    throw ex;
                }
            }

            return (JObject)JsonConvert.DeserializeObject(response);

        }

        public static JObject CallSharePointOnlineRestAPI(String url, int attempts = 3, string token = "")
        {
            if(token == "")
            {
                token = GenerateToken();
            }
            attempts--;
           
            IRestResponse r;
            RestClient client = new RestClient(url);
            client.Timeout = -1;
            RestRequest request = new RestRequest(Method.GET);
            request.AddHeader("Accept", "application/json;odata=verbose");
            request.AddHeader("Content-Type", "application/json;odata=verbose");
            request.AddHeader("Authorization", $"Bearer {token}");
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            try
            {
                r = client.Execute(request);
            }
            catch (Exception ex)
            {
                if (attempts > 0)
                {
                    //System.Threading.Thread.Sleep(1 * 60 * 1000);//Wait for 1 minute before trying again                    
                    JObject result = CallSharePointOnlineRestAPI(url, attempts, token);
                    return result;
                }
                else
                {
                    throw ex;
                }
            }


            return (JObject)JsonConvert.DeserializeObject(r.Content);

        }

        private static string GenerateToken()
        {
            string resource = ConfigurationManager.AppSettings["SharePointResource"]; 
            string clientId = ConfigurationManager.AppSettings["SharePointClientID"];
            string clientSecret = ConfigurationManager.AppSettings["SharePointClientSecret"];
            RestClient client = new RestClient("");
            client.Timeout = -1;
            RestRequest request = new RestRequest(Method.POST);
            request.AddHeader("Accept", "application/json;odata=verbose");
            request.AddHeader("Content-Type", "application/json;odata=verbose");
            request.AlwaysMultipartFormData = true;
            request.AddParameter("grant_type", "client_credentials");
            request.AddParameter("client_id", clientId);
            request.AddParameter("client_secret", clientSecret);
            request.AddParameter("resource", resource);
            IRestResponse response = client.Execute(request);
            Console.WriteLine(response.Content);


            JToken tokenResult = JToken.Parse(response.Content);
            string token = tokenResult.SelectToken("access_token").ToString();
            return token;
        }

        public static string GetXRequestDigestValue(string URL)
        {
            URL = String.Concat(URL, "_api/contextinfo");
            string json = string.Empty;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(URL);
            request.Accept = "application/json;odata=verbose";
            request.Method = "POST";
            request.ContentLength = 0;
            request.Credentials = CredentialCache.DefaultCredentials;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                json = reader.ReadToEnd();
            }
            JObject result = (JObject)JsonConvert.DeserializeObject(json);
            return result["d"]["GetContextWebInformation"]["FormDigestValue"].ToString();
        }

        public static string StartWorkflow(string baseURL, string subID, int ID, string XDigest)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls|SecurityProtocolType.Tls11|SecurityProtocolType.Tls12|SecurityProtocolType.Ssl3;

            string finalURL = baseURL + "_api/SP.WorkflowServices.WorkflowInstanceService.Current/StartWorkflowOnListItemBySubscriptionId(subscriptionId='" + subID + "',itemId='" + ID + "')";
            string json = string.Empty;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(finalURL);
            request.Accept = "application/json;odata=verbose";
            request.Method = "POST";
            request.ContentLength = 0;
            request.ContentType = "application/json;odata=verbose";
            request.Credentials = CredentialCache.DefaultCredentials;
            request.Headers.Add("X-RequestDigest: " + XDigest);
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                json = reader.ReadToEnd();
            }
            //Console.WriteLine(json);
            return json;
        }

        /// <summary>
        /// Like CallSharePointRestAPI but with posting and more specific
        /// </summary>
        /// <param name="site">URL of site</param>
        /// <param name="list">Name of list</param>
        /// <param name="ID">ID of list item to update</param>
        /// <param name="json">JSON string containing what to update</param>
        /// <returns>Returns a BIJobs object containing all returned sharepoint data for the given job code.</returns>
        public static JObject UpdateSharePointListItem(string site, string list, int ID, string json)
        {
            string URL = site + "_api/web/lists/getbytitle('" + list + "')/items(" + ID + ")";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(URL);
            request.Accept = "application/json;odata=verbose";
            request.ContentLength = json.Length;
            request.ContentType = "application/json;odata=verbose";
            request.Headers.Add("IF-MATCH: *");
            request.Headers.Add("X-HTTP-Method: MERGE");
            request.Headers.Add("X-RequestDigest: " + GetXRequestDigestValue(site));
            request.Method = "POST";
            request.Credentials = CredentialCache.DefaultCredentials;
            using (var streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                streamWriter.Write(json);
                streamWriter.Flush();
                streamWriter.Close();
            }
            string result;
            var httpResponse = (HttpWebResponse)request.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                result = streamReader.ReadToEnd();
            }
            return (JObject)JsonConvert.DeserializeObject(result);
        }

        public static string CreateDocumentLibrary(string site, string libraryName, bool attachmentsFolder=false)
        {

            // ClientContext - Get the context for the SharePoint Site      


            ClientContext clientContext = new ClientContext(site);
            // Specifies the properties of the new custom list      

            ListCreationInformation creationInfo = new ListCreationInformation();
            creationInfo.Title = libraryName;
            creationInfo.Description = "";
            creationInfo.TemplateType = (int)ListTemplateType.DocumentLibrary;
            // Create a new custom list      

            List newList = clientContext.Web.Lists.Add(creationInfo);
            // Retrieve the custom list properties      
            clientContext.Load(newList);
            // Execute the query to the server.  
            try
            {
                clientContext.ExecuteQuery();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return "error";
            }
            // Display the custom list Title property      
            Console.WriteLine(newList.Title);
            if (attachmentsFolder)
            {
                string folderName = "Attachments";
                ListItemCreationInformation newItemInfo = new ListItemCreationInformation();
                newItemInfo.UnderlyingObjectType = FileSystemObjectType.Folder;
                newItemInfo.LeafName = folderName;
                ListItem newListItem = newList.AddItem(newItemInfo);
                newListItem["Title"] = folderName;
                newListItem.Update();
                clientContext.ExecuteQuery();
            }
            return newList.Title;

        }


        /// <summary>
        /// Post a Json object to a list. create fields automatically based on JSON Properties
        /// </summary>
        /// <param name="proclog"></param>
        /// <param name="site"></param>
        /// <param name="listname"></param>
        /// <param name="dataToLoad"></param>
        /// <returns></returns>
        public static string PushToList(Logger proclog, string site, string listname, JObject dataToLoad)
        {
            string listDetailsUrl = site + "/_api/web/lists/getbytitle('" + listname + "')";
            JObject listDetail = MakeRestCall(listDetailsUrl, Method.GET, proclog: proclog);
            JToken errorcount = listDetail["error"];
            if (errorcount != null)
            {
                proclog.WriteToLog("List " + listname + " did not exist on the requested site. Site: " + site + " " + dataToLoad );

                return "error";
                //TODO: could create the list if it doesnt exist
                
            }
            string fieldsUri = listDetail.SelectToken("d.Fields.__deferred.uri").ToString();
            string listItemEntityTypeFullName = listDetail.SelectToken("d.ListItemEntityTypeFullName").ToString();
            JObject fields = MakeRestCall(fieldsUri, Method.GET, proclog: proclog);
            var fieldTitles = from p in fields["d"]["results"]
                                     select (string)p["Title"];
            List<string> ft = fieldTitles.ToList();

            var meta = new JObject();
            meta.Add("type", listItemEntityTypeFullName);

            JObject itemBody = new JObject();
            itemBody.Add("__metadata", meta);
            Console.WriteLine(itemBody);

            List<JProperty> props = dataToLoad.Properties().ToList();
            foreach (JProperty p in props)
            {
                if (p.Value.Type == JTokenType.Object) //If there's another layer of JObject, create a new row for each one. Not planning on going deeper than one sublevel
                {
                    foreach (JProperty subP in p.Value.OfType<JProperty>())
                    {
                        string field = char.ToUpper(p.Name[0]) + p.Name.Replace('_', ' ').Substring(1) + "_" + char.ToUpper(subP.Name[0]) + subP.Name.Replace('_', ' ').Substring(1);
                        string value = subP.Value.ToString();
                        if(value == "")
                        {
                            continue;
                        }
                        string intName = "";

                        if (!fieldTitles.Contains(field))
                        {
                            //create the field if it doesnt exist {SP.FieldMultiLineText}
                            string bodystring = @"{'__metadata': {'type': 'SP.FieldMultiLineText'},'Title': '" + field + "','FieldTypeKind': 3,'Required': 'false','EnforceUniqueValues': 'false','StaticName': '" + field + "'}"; ;

                            JObject body = JObject.Parse(bodystring);
                            JObject resp = MakeRestCall(fieldsUri, Method.POST, proclog, body);

                            intName = resp.SelectToken("d.InternalName").ToString();
                            
                        }
                        else
                        {
                            var fieldinternalname = fields.SelectToken("d.results").Select(m => new
                            {
                                internalName = m.SelectToken("InternalName"),
                                title = m.SelectToken("Title")
                            });
                            var c = fieldinternalname.Where(x => x.title.Value<string>().Equals(field));

                            intName = c.Select(x => x.internalName.ToString())
                                .First();

                        }
                        


                        itemBody.Add(intName, value);

                        

                    }
                }
                else
                {

                    string field = char.ToUpper(p.Name[0]) + p.Name.Replace('_', ' ').Substring(1);
                    string value = p.Value.ToString();
                    if (value == "")
                    {
                        continue;
                    }
                    string intName = "";
                    if (!fieldTitles.Contains(field))
                    {
                        //create the field if it doesnt exist
                        proclog.WriteToLog($@"Adding field {field} to sharepoint list.");

                        string bodystring = @"{'__metadata': {'type': 'SP.FieldMultiLineText'},'Title': '" + field + "','FieldTypeKind': 3,'Required': 'false','EnforceUniqueValues': 'false','StaticName': '" + field + "'}"; ;
                        
                        
                        JObject body = JObject.Parse(bodystring);
                        JObject resp = MakeRestCall(fieldsUri, Method.POST, proclog, body);

                        proclog.WriteToLog(resp.ToString());

                        intName = resp.SelectToken("d.InternalName").ToString();
                    }
                    else
                    {
                        var fieldinternalname = fields.SelectToken("d.results").Select(m => new
                        {
                            internalName = m.SelectToken("InternalName"),
                            title = m.SelectToken("Title")
                        });
                        var c = fieldinternalname.Where(x => x.title.Value<string>().Equals(field));

                        intName = c.Select(x => x.internalName.ToString())
                            .First();

                    }

                    itemBody.Add(intName, value);

                    
                }
            }

            JObject r = MakeRestCall(listDetailsUrl + "/items", Method.POST, proclog, itemBody);



        return "success";
        }



        private static JObject MakeRestCall(string uri, Method method, Logger proclog, JObject body = null)
        {
            RestClient client = new RestClient(uri);
            client.Authenticator = new NtlmAuthenticator();
            client.Timeout = -1;
            RestRequest request = new RestRequest(method);
            request.AddHeader("Accept", "application/json;odata=verbose");
            if (body != null)
            {
                request.AddParameter("application/json;odata=verbose", body, ParameterType.RequestBody);
                request.AddHeader("Content-Type", "application/json;odata=verbose");
            }

            IRestResponse response = client.Execute(request);

            if(response.StatusCode != HttpStatusCode.OK)
            {
                proclog.WriteToLog(response.StatusCode.ToString());
                proclog.WriteToLog(response.Content);
                
            }


            JObject r = JObject.Parse(response.Content);

            return r;
        }

    }

    
    public enum JobCurrentStatus
    {
        Queued, Started, Finished, Errored, None
    }



    public class person
    {
        public string firstName { get; set; }
        public string lastName { get; set; }
        public string userName { get; set; }
        public string email { get; set; }
    }

}
