using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace JobCentralControlAPI.Models
{
    public class DriverProcess
    {
        public List<string> args
        {
            get
            {
                return this.commandline.Split(' ').ToList();
            }
            set
            {

            }
        }
        public string commandline { get; set; }
        public string path { get; set; }
        public string winProcID { get; set; }
        public DateTime startedTime { get; set; }
        public string averageRunTime { get; set; }
    }
}