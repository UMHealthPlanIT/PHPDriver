using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace RunRequest.Models
{
    public class JobConfig
    {
        public string TableName { get; set; }
        public string url { get; set; } = "https://{}/Apps/JobConfiguration/Home/TableSelect?TableName=";
    }
}