using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utilities;

namespace Driver
{
    class Mr_Data : Logger, IPhp
    {
        private string requestedJobId;
        public Mr_Data(LaunchRequest Program)
            : base(Program)
        {
            return;
        }
        public bool Initialize(string[] args)
        {
            foreach (String s in args) //log args for debugging
            {
                this.WriteToLog(s + Environment.NewLine);
            }

            requestedJobId = args[1];
            new ExtractFactory2(requestedJobId, this);
            return true;
        }

        public new void OnError(Exception exc)
        {
            this.WriteToLog(exc.Message + Environment.NewLine + exc.StackTrace);
            Console.WriteLine(exc.Message + Environment.NewLine + exc.StackTrace);

            LaunchRequest launchRequest = new LaunchRequest(requestedJobId, this.TestMode, null, this.UniqueID);
            Logger procLog = new Logger(launchRequest);
            UniversalLogger.WriteToLog(procLog, "Mr_Data threw an exception running " + requestedJobId, category: UniversalLogger.LogCategory.ERROR);
        }
    }
}
