using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation;



namespace Utilities.Integrations
{
    public static class PowerShellExecution
    {
        public static bool ExecutePS(Logger caller, string psScript)
        {
            
            PowerShell powerShell = PowerShell.Create().AddScript(psScript);
            powerShell.AddCommand("Out-String");

            caller.WriteToLog("Powershell script beginning execution");
            System.Collections.ObjectModel.Collection<PSObject> response = powerShell.Invoke();
            foreach (PSObject item in response)
            {
                string[] output = item.ToString().Split(':');
                foreach (string line in output)
                {
                    caller.WriteToLog(line);
                }

                foreach (ErrorRecord err in powerShell.Streams.Error)
                {
                    caller.WriteToLog(err.ToString(), UniversalLogger.LogCategory.ERROR);
                }
                
            }
            caller.WriteToLog("Powershell script execution complete");
            return true;
        }
    }
}
