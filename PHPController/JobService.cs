using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Utilities;

namespace PHPController
{
    public partial class JobService : ServiceBase
    {
        private Logger myLog = null;
        private Controller jobController;
        bool isTestMode;

        public JobService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            isTestMode = false;

            if (!System.Security.Principal.WindowsIdentity.GetCurrent().Name.ToUpper().Contains("LSFUSER"))
            {
                isTestMode = true;

            }

            if ((args.Count() > 0 && args[0].ToUpper() == "TEST"))
            {
                isTestMode = true;
                //System.Threading.Thread.Sleep(15000); //use this if you are going to attach to the process after it starts, rather than launch a new VS debugger
                System.Diagnostics.Debugger.Launch();
            }

            myLog = new Logger("Controller", setTestMode: isTestMode, logOnly: true, uniqueID: Guid.NewGuid().ToString());
            UniversalLogger.WriteToLogProgramStart(myLog);

            try
            {
                jobController = new Controller(myLog, isTestMode);
            }
            catch (Exception ex)
            {
                myLog.WriteToLog(ex.ToString(), UniversalLogger.LogCategory.ERROR);
            }
        }

        protected override void OnStop()
        {
            jobController.controlTimer.Stop();
            jobController = null;
            UniversalLogger.WriteToLogProgramComplete(myLog);
        }
    }
}
