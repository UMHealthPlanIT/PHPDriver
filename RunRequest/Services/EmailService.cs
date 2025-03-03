using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Configuration;
using RunRequest.Models;
using Newtonsoft.Json;

namespace RunRequest.Services
{
    public class EmailService
    {
        private static readonly string EmailEndpoint = WebConfigurationManager.AppSettings["GetEmailEndpoint"];
        
        public static bool SendEmail(EmailParameters paramObject)
        {
            string endpoint = EmailEndpoint + "SendEmail";
            string payload = JsonConvert.SerializeObject(paramObject);

            return ApiService.CallApi<bool>(endpoint, "POST", payload: payload);
        }
    }
}