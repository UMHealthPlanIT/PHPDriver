using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Diagnostics;

namespace JobCentralControlAPI.Controllers
{
    public class KillDriverProcess
    {
        public static string KillProcess(int procID)
        {
            Process driverProcess = new Process();
            try
            {
                try
                {
                    driverProcess = Process.GetProcessById(procID);
                }
                catch (ArgumentException ex)
                {
                    return "That instance of PhpDriver no longer exists:" + ex;
                }

                if (driverProcess.ProcessName == "PhpDriver")
                {
                    try
                    {
                        driverProcess.Kill();
                        return "PhpDriver process was successfully terminated.";
                    }
                    catch (Exception ex)
                    {
                        return "PhpDriver was NOT terminated: " + ex;
                    }

                }
                else
                {
                    return "Windows Process " + procID.ToString() + " is no longer a PhpDriver process and was not killed.";
                }
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }
        }
    }
}