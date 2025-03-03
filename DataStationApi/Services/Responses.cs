using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Net.Http;
using Newtonsoft.Json;
using System.Net;

namespace DataStationApi.Models
{
    public class Responses
    {
        /// <summary>
        /// Converts a given object to JSON and creates a response object to pass back to the client
        /// </summary>
        /// <param name="requestContext">Received 'Request' object (should be automatically created by framework and available to you)</param>
        /// <param name="obj">Object to serialize to JSON</param>
        /// <returns>Returns a fully instantiated response object containing the JSON payload</returns>
        public static HttpResponseMessage CreateJSONDataResponse(HttpRequestMessage requestContext, Object obj, List<CustomHeader> head = null)
        {

            string json = JsonConvert.SerializeObject(obj, Formatting.Indented);

            HttpResponseMessage resp = requestContext.CreateResponse(HttpStatusCode.OK);

            if (head != null)
            {
                foreach (CustomHeader customHeader in head)
                {
                    resp.Headers.Add(customHeader.headerName, customHeader.headerValue);
                }
            }
            resp.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            return resp;

        }

        public class CustomHeader
        {
            public string headerName { get; }
            public string headerValue { get; }

            public CustomHeader(string name, string value)
            {
                headerName = name;
                headerValue = value;
            }
        }
    }
}