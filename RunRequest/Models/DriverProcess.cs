using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace RunRequest.Models
{
    public class DriverProcess
    {
        public string[] args { get; set; }
        public string commandline { get; set; }
        public string path { get; set; }
        public string winProcID { get; set; }
        public string server { get; set; }
        public DateTime startedTime { get; set; }
        public string averageRunTime { get; set; }
        public string Owner { get; set; }
    }
}