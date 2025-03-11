using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Timers;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices;
using Utilities;
using System.Configuration;

namespace Driver
{
    /// <summary>
    /// Transforms command line parameters into program calls
    /// </summary>
    public class Driver
    {
        /// <summary>
        /// Number of timeouts that have completed since the subroutine began
        /// </summary>
        public static int TimeOutCounter;
        public static Logger uLogger;
        private static bool hasPoppedStartMessage = false;
        private static string uID = Guid.NewGuid().ToString();

        /// <summary>
        /// Core access method for Driver application
        /// </summary>
        /// <param name="args">Command line arguments</param>
        public static void Main(string[] args)
        {
            try
            {
                Boolean forceTestMode = false;
                String requesterName = null;
                string job = args[0]; ;
                try
                {
                    if (args.Contains("Mr_Data") || args.Contains("Ms_Data"))
                    {
                        if (args[0] == "Mr_Data" || args[0] == "Ms_Data")
                        {
                            job = args[1];
                        }
                        else if (args[1] == "Mr_Data")
                        {
                            job = args[0];
                            args[0] = "Mr_Data";
                            args[1] = job;
                        }
                    }
                }
                catch (Exception E)
                {
                    string error = E.ToString();
                }


                args = isTestModeRequester(args, out forceTestMode, out requesterName);

                uLogger = new Logger(job, forceTestMode, uniqueID: uID);

                string programId = args[0];
                if (args.Contains("Mr_Data"))
                {
                    programId = "Mr_Data";
                }

                Logger.LaunchRequest driverLaunchRequest = new Logger.LaunchRequest(programId, forceTestMode, args, requesterName, uid: uID);

                if (!hasPoppedStartMessage)
                {
                    UniversalLogger.WriteToLogProgramStart(uLogger);
                    hasPoppedStartMessage = true;
                }

                if (args.Length > 1)
                {
                    uLogger.WriteToLog("Runtime arguments: \"" + string.Join("\", \"", args, 1, args.Length - 1) + "\"");
                }

                LaunchJob(driverLaunchRequest);
                UniversalLogger.WriteToLogProgramComplete(uLogger);
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
                Console.WriteLine(exc.StackTrace);
                Environment.ExitCode = 5001;
                string argString = "";
                foreach (string arg in args.ToList())
                {
                    argString += arg + " ";
                }
                Logger driver = new Logger("Driver", uniqueID: uID);
                UniversalLogger.WriteToLog(driver, "This call caused an error in the Driver class. Arguments:" + argString + " Error: " + exc.ToString(), category: UniversalLogger.LogCategory.ERROR);
                return;
            }

        }

        public static void LaunchJob(Logger.LaunchRequest request)
        {
            Type typeArgument;
            Logger logger = new Logger("Driver", uniqueID: uID);

            string typeName = "Driver." + request.programCode;
            Console.WriteLine("TypeName:" + typeName);

            typeArgument = Type.GetType(typeName);

            if (typeArgument == null)
            {
                // Check to see if the job is a MrData Job
                List<GenericExtractGenerator> mrDataCheck = ExtractFactory.ConnectAndQuery<GenericExtractGenerator>(Data.AppNames.ExampleProd, "select * from dbo.MrDataJobs_C where ProgramCode LIKE '" + request.programCode + "%'").ToList();
                if(mrDataCheck.Count > 0)
                {
                    //set up the args to run this in as a MrData
                    List<string> newArgs = new List<string>();

                    newArgs.Add("Mr_Data");

                    foreach (string initialArg in request.providedArgs)
                    {
                        newArgs.Add(initialArg);

                    }

                    //initiate a new Launch request with the same info but MrData
                    Logger.LaunchRequest driverReLaunchRequest = new Logger.LaunchRequest("Mr_Data", request.overrideRunMode, 
                        newArgs.ToArray(), request.requestedBy, uid: request.uniqueID);
                    LaunchJob(driverReLaunchRequest); //Launch the new request
                    return; 
                }
                else
                {
                    throw new Exception("Program's class not found, check to make sure it is in the Driver namespace");
                }
                
            }

            IPhp control;
            try
            {
                control = (IPhp)Activator.CreateInstance(typeArgument, new object[] { request }); //we prefer the new default Logger constructor
            }
            catch (System.MissingMethodException exc1)
            {
                logger.WriteToLog(exc1.ToString());
                try
                {
                    control = (IPhp)Activator.CreateInstance(typeArgument, new object[] { request.programCode }); //this handles Php subclasses that have constructors defined (most jobs)
                }
                catch (System.MissingMethodException exc)
                {
                    logger.WriteToLog(exc.ToString());
                    control = (IPhp)Activator.CreateInstance(typeArgument);
                }
            }

            System.Threading.TimerCallback TimerDelegate = new System.Threading.TimerCallback(ProgramTimeOut);
            System.Threading.Timer aTime = new System.Threading.Timer(ProgramTimeOut, request.providedArgs, 3600000, 3600000);

            try
            {
                if (control.Initialize(request.providedArgs))
                {
                    control.Finish();
                }

            }
            catch (Exception exc)
            {
                Environment.ExitCode = 5000;
                control.OnError(exc);

            }
            finally
            {
                aTime.Dispose();
            }

            Console.WriteLine("Program Complete");

        }

        public static List<string> GetProgramCode(List<string> args)
        {
            if (args.Count() > 1 && (args[1].ToString().ToUpper().Trim() == "MR_DATA" || args[1].ToString().ToUpper().Trim() == "MS_DATA"))
            {
                String ProcessName = args[1].ToString().Trim();
                String DataRequestedJob = args[0];
                args[0] = ProcessName;
                args[1] = DataRequestedJob;

            }

            return args;
        }

        private static void ProgramTimeOut(Object source)
        {
            TimeOutCounter++;
            IList<string> args = (IList<string>)source;
            String argumentsReceived = string.Join(",", args);
            String programNumber = args[0];
            Logger thisJob = new Logger(programNumber);
            UniversalLogger.WriteToLog(thisJob, "A program is still running and has missed " + TimeOutCounter + " sixty minute window(s), please check the program.", category: UniversalLogger.LogCategory.WARNING);
            string[] timeoutWarning = new string[2];
            timeoutWarning[0] = programNumber;
            timeoutWarning[1] = "Timeout " + TimeOutCounter;
        }


        private static string[] parseLine(String line)
        {
            List<string> parsedArgs = new List<string>();

            String word = "";
            bool OpenSentence = false;

            int i = 0;
            foreach (char c in line)
            {
                i++;
                if (c != ' ')
                {
                    if (c == '"' && !OpenSentence) //if we find a double quote and we're not in an open sentence
                    {
                        OpenSentence = true;
                    }
                    else if (c == '"' && OpenSentence)
                    {
                        parsedArgs.Add(word);
                        word = "";
                        OpenSentence = false;

                    }
                    else
                    {
                        word += c;
                        if (line.Length == i) //if we're at the last element
                        {
                            parsedArgs.Add(word);
                        }
                    }
                }
                else if (OpenSentence)
                {
                    word += c;
                }
                else
                {
                    if (word.Length > 0)
                    {
                        parsedArgs.Add(word);
                    }
                    word = "";
                }
            }

            return parsedArgs.ToArray();
        }

        private static string[] isTestModeRequester(string[] args, out Boolean forceTestMode, out String requesterName)
        {
            List<String> argsList = args.ToList();

            if (argsList.Contains("-t"))
            {
                forceTestMode = true;
                argsList.Remove("-t");
            }
            else
            {
                forceTestMode = false;
            }

            if (argsList.Contains("-r"))
            {
                try
                {
                    requesterName = argsList[argsList.IndexOf("-r") + 1];
                    argsList.Remove(requesterName);
                }
                catch (IndexOutOfRangeException)
                {
                    Console.WriteLine("Requester flag [-r] set without a user specified. Running in test mode without setting requester.");
                    requesterName = null;
                }

                argsList.Remove("-r");
            }
            else
            {
                requesterName = null;
            }

            return argsList.ToArray();
        }
    }
}

