using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace PHPController
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            //to install do this, running as Administrator https://msdn.microsoft.com/en-us/library/aa984379(v=vs.71).aspx
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[] 
            { 
                new JobService() 
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
