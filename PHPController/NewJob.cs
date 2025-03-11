using System;
using System.Threading;
using System.Diagnostics;
using Utilities;
using System.Linq;
using System.Collections.Generic;
using Utilities.Schedules;


namespace PHPController
{
    class NewJob
    {
        /// <summary>
        /// Queue the job that is to be launched.
        /// </summary>
        /// <param name="data">Information for the process to be launched.</param>
        public static void SpawnProgram(object data)
        {
            
            QueuedProcess processToSpawn = (QueuedProcess)data;

            Controller.QueuedJobs.Remove(processToSpawn.pendingProgramRec.JobId);

            //half second delay to prevent Queue and Start status updates to the database from stepping on each other
            Thread.Sleep(500);

            processToSpawn.pendingProgramRec.ActualStartTime = DateTime.Now;
            processToSpawn.pendingProgramRec.RunStatus = "S";
            processToSpawn.pendingProgramRec.RecordStartedJob(Controller.ControllerLog.TestMode, Controller.ownerGroup);

            try
            {
                List<string> arguments = GetProgramCode(processToSpawn.arguments);
                string programId = arguments[0];
                if (arguments[0] == "Mr_Data")
                {
                    programId = arguments[1];
                }

                Boolean overrideTestMode = (processToSpawn.pendingProgramRec.Environment == "T" ? true : false);
                string requestedBy = processToSpawn.pendingProgramRec.RequestedBy;

                Logger.LaunchRequest controllerLaunchRequest = new Logger.LaunchRequest(programId, overrideTestMode, arguments.ToArray(), requester:requestedBy);

                LaunchJob(controllerLaunchRequest);
                string job = "";
                if (arguments[0] == "Mr_Data" || arguments[0] == "Ms_Data")
                {
                    job = arguments[1];
                }
                else
                {
                    job = arguments[0];
                }
                Controller.ControllerLog.WriteToLog("Started " + job);
            }
            catch (Exception exc)
            {
                Controller.ControllerLog.WriteToLog(processToSpawn.arguments[0] + " caused Driver to throw an unhandled exception" + Environment.NewLine + exc.ToString(), UniversalLogger.LogCategory.ERROR);
            }
            
        }

        /// <summary>
        /// Launch job that was queued.
        /// </summary>
        /// <param name="queuedProc">Information about the queued job that is to be launched.</param>
        /// <returns></returns>
        public static Boolean LaunchJob(Logger.LaunchRequest queuedProc)
        {
            Process jobToLaunch = new Process();

            jobToLaunch.StartInfo.FileName = Environment.GetEnvironmentVariable("DriverPath");


            int loopStart = 0;
            if (queuedProc.providedArgs.Length > 1)
            {
                if(queuedProc.providedArgs[1] == "Mr_Data" || queuedProc.providedArgs[1] == "Ms_Data")
                {
                    jobToLaunch.StartInfo.Arguments = queuedProc.providedArgs[1];
                    jobToLaunch.StartInfo.Arguments += queuedProc.providedArgs[0];
                    loopStart = 2;
                }
            }

            for (int loops = loopStart; loops < queuedProc.providedArgs.Length; loops += 1)
            {
                jobToLaunch.StartInfo.Arguments += queuedProc.providedArgs[loops] + " ";
            }

            if (queuedProc.overrideRunMode)
            {
                jobToLaunch.StartInfo.Arguments += "-t -r " + queuedProc.requestedBy;
            }

            return jobToLaunch.Start();

        }

        /// <summary>
        /// Queue job that should be launched now in accordance with the schedule.
        /// </summary>
        /// <param name="pendProg">Execution information for the job to be queued.</param>
        /// <param name="processLog">Logger object used for determining current environment and logging on behalf of the calling class.</param>
        /// <param name="ownerGroup">Group that owns the executing server (Sparrow or PHP).</param>
        public static void QueueProgram(DailyScheduleRecord pendProg, Logger processLog, string ownerGroup)
        {
            List<string> args = ConvertPendProgToArg(pendProg);

            QueuedProcess queuedProc = new QueuedProcess();
            queuedProc.arguments = args;
            Controller.QueuedJobs.Add(pendProg.JobId);
            pendProg.QueuedTime = DateTime.Now;
            queuedProc.pendingProgramRec = pendProg;

            if (ThreadPool.QueueUserWorkItem(NewJob.SpawnProgram, queuedProc))
            {
                string argString = string.Join(",", args);
                processLog.WriteToLog("Queued " + args[0] + " for processing");
            }
            else
            {
                processLog.WriteToLog(args[0] + " could not be queued", UniversalLogger.LogCategory.WARNING);
            }
        }

        /// <summary>
        /// Convert the information in the schedule record into commandline parameters.
        /// </summary>
        /// <param name="pendProg">Execution information for the job.</param>
        /// <returns></returns>
        public static List<string> ConvertPendProgToArg(DailyScheduleRecord pendProg)
        {
            List<string> argsFinal = new List<string>();
            List<string> argsRaw = pendProg.Parameters.Split(',').ToList();

            foreach (string parm in argsRaw)
            {
                if (parm == "Mr_Data" || parm == "Ms_Data")
                {
                    argsFinal.Insert(0, parm);
                }
                else
                {
                    argsFinal.Add(parm);
                }
            }

            return argsFinal;
        }

        public static List<string> GetProgramCode(List<string> args)
        {
            if (args.Count() > 1 && (args[1].ToString().ToUpper().Trim() == "MR_DATA" || args[1].ToString().ToUpper().Trim() == "MS_DATA"))
            {
                string ProcessName = args[1].ToString().Trim();
                string DataRequestedJob = args[0];
                args[0] = ProcessName;
                args[1] = DataRequestedJob;

            }

            return args;
        }

        /// <summary>
        /// Execution information about the job being queued.
        /// </summary>
        public class QueuedProcess
        {
            public List<string> arguments { get; set; }
            public DailyScheduleRecord pendingProgramRec { get; set; }
        }
    }
}
