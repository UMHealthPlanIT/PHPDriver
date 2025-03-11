using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using Utilities;
using System.Timers;
using System.Windows.Forms;
using Driver.IT_0363a;

namespace Driver
{
    public class IT_0363ACAEdgeReporting : Logger, IPhp
    {
        //public IT_0363(String Program)
        //   : base(Program)
        //{
        //    return;
        //}

        public IT_0363ACAEdgeReporting(LaunchRequest ProcId) : base(ProcId)
        {
        }


        public bool Initialize(string[] args)
        {

            String EDGEenvironment; //Designate whether this will be for EDGE Test or Prod

            if (args.Length > 1)
            {
                EDGEenvironment = args[1];
            }
            else
            {
                EDGEenvironment = this.TestMode ? "T" : "P";
            }

            if (args.Length == 2)
            {
                if (args[1].Equals("S", StringComparison.OrdinalIgnoreCase))
                {
                    ProgramOptions opt = new ProgramOptions();
                    opt.supplementalReportCreate = true;
                    opt.supplementalEdgeSubmit = true;
                    opt.supplementalGetServerOutput = true;
                    opt.supplementalOutputReportCreate = true;

                    EDGEenvironment = this.TestMode ? "T" : "P";

                    IT_0363a.Control.FlowControl(this, EDGEenvironment, opt);

                    return true;
                }
                else if (args[1].Equals("GUI", StringComparison.OrdinalIgnoreCase))
                {
                    Application.EnableVisualStyles();
                    Application.Run(new IT_0363a.EDGEGUI(this));
                    return true;
                }
                else
                {
                    return parseArgs(args, ref EDGEenvironment);
                }
            }
            else if (args.Length > 2)
            {
                return parseArgs(args, ref EDGEenvironment);
            }
            else
            {
            
                ProgramOptions opt = new ProgramOptions();
                //NOTE: This will make runYear the previous year before a given month, otherwise it is the same year
                int runYear = 0;

                if(DateTime.Today.Month < 6)
                {
                    runYear = DateTime.Today.Year - 1;
                }
                else
                {
                    runYear = DateTime.Today.Year;
                }

                opt.RunAll(runYear.ToString());

                IT_0363a.Control.FlowControl(this, EDGEenvironment, opt);

                return true;
            }

        }

        private bool parseArgs(string[] args, ref string EDGEenvironment)
        {
            int runYear = 0;
            bool run = false;
            ProgramOptions opt = new ProgramOptions();
            if (DateTime.Today.Month < 6)
            {
                runYear = DateTime.Today.Year - 1;
            }
            else
            {
                runYear = DateTime.Today.Year;
            }
            if (args.Contains("E"))
            {
                opt.enrollmentReportCreate = true;
                opt.enrollmentEdgeSubmit = true;
                opt.enrollmentGetServerOutput = true;
                opt.enrollmentOutputReportCreate = true;
                opt.enrollmentOutputReportSend = true;
                run = true;
            }
            if (args.Contains("M"))
            {
                opt.medClaimsReportCreate = true;
                opt.medClaimsEdgeSubmit = true;
                opt.medClaimsGetServerOutput = true;
                opt.medClaimsOutputReportCreate = true;
                opt.medClaimsOutputReportSend = true;
                run = true;
            }
            if (args.Contains("P"))
            {
                opt.pharmClaimsReportCreate = true;
                opt.pharmClaimsEdgeSubmit = true;
                opt.pharmClaimsGetServerOutput = true;
                opt.pharmClaimsOutputReportCreate = true;
                opt.pharmClaimsOutputReportSend = true;
                run = true;
            }

            if (run)
            {
                opt.year = runYear.ToString();
                opt.CloseAfterCompletion = true;
                EDGEenvironment = this.TestMode ? "T" : "P";
                IT_0363a.Control.FlowControl(this, EDGEenvironment, opt);
                return true;
            }

            return false;
        } 

        /// <summary>
        /// If edge errors, we need to clean-up any half-generated files to avoid sending them to Edge in a subsequent run
        /// </summary>
        /// <param name="exc"></param>
        new public void OnError(Exception exc)
        {
            this.WriteToLog(exc.ToString());
            this.WriteToLog("IT_0363 threw an unhandled exception, we're going to move any files we were working on to a errored folder to ensure they don't get picked up in a subsequent run",UniversalLogger.LogCategory.ERROR);

            String initialOutputLoc = this.LoggerOutputYearDir + @"Submit\";

            String archiveForFilesThatWereGeneratedWhen363Errored = initialOutputLoc + @"Errored\";
            FileSystem.ReportYearDir(archiveForFilesThatWereGeneratedWhen363Errored);

            List<string> pendedFiles = System.IO.Directory.GetFiles(initialOutputLoc).ToList();

            foreach(String file in pendedFiles)
            {
                System.IO.File.Move(file, archiveForFilesThatWereGeneratedWhen363Errored + System.IO.Path.GetFileName(file));
            }
        }

    }
}
