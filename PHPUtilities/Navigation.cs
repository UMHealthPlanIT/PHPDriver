using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using System.Diagnostics;

namespace Utilities
{
    public class Navigation
    {
        /// <summary>
        /// Figures out where the current executable file is sitting, and translates to a URI if necessary. Current use is to find the appropriate DataConfiguration.xml file
        /// </summary>
        /// <returns>The current executable's location</returns>
        public static String GetProgramDirectory()
        {
            String runningCurrentDir = System.IO.Directory.GetCurrentDirectory();
            String progDir = @"\";
            try
            {
                progDir = Path.GetDirectoryName(Environment.GetEnvironmentVariable("DriverPath"));
                progDir += "\\";
            }
            catch (Exception ex) //If an environment variable named "DriverPath" doesn't exist, we'll use the PHPDriver.exe that exists in the current folder 
            {
                string exe = Process.GetCurrentProcess().MainModule.FileName;
                progDir = Path.GetDirectoryName(exe) + "\\";
            }
            if (progDir == @"\" || string.IsNullOrWhiteSpace(progDir))
            {
                string exe = Process.GetCurrentProcess().MainModule.FileName;
                progDir = Path.GetDirectoryName(exe) + "\\";
            }

            return progDir;
        }

        //Just being overly cautious while moving servers. If you're reading this and it's after Christmas 2021, delete the commented code. 🎄
        //public static String getDataHome()
        //{
        //    Data dataHomeLoc = new Data(Data.AppNames.DataHome);

        //    return @"\\" + dataHomeLoc.server + @"\";
        //}

    }
}
