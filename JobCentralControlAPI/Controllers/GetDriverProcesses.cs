using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using JobCentralControlAPI.Models;
using System.Management;

namespace JobCentralControlAPI.Controllers
{
    public class GetDriverProcesses
    {
        public static List<DriverProcess> GetDriverProcess()
        {
            List<DriverProcess> listOfDriverInstances = new List<DriverProcess>();
            string wmiQuery = string.Format("select * from Win32_Process where Name='{0}'", "PhpDriver.exe");
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(wmiQuery);
            ManagementObjectCollection retObjectCollection = searcher.Get();
            foreach (ManagementObject obj in retObjectCollection)
            {
                DriverProcess dObj = new DriverProcess();

                dObj.commandline = obj["CommandLine"].ToString();
                dObj.path = obj["ExecutablePath"].ToString();
                dObj.winProcID = obj["ProcessId"].ToString();
                dObj.startedTime = ManagementDateTimeConverter.ToDateTime(obj["CreationDate"].ToString());

                listOfDriverInstances.Add(dObj);
            }
            return listOfDriverInstances;
        }
    }
}