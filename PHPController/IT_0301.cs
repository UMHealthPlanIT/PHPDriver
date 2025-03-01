using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using PHPUtilities;
using PHPUtilities.Integrations;

namespace PHPController
{
    class IT_0301
    {

        public static void PopulateTodaysProcessing(Logger procLog, Boolean controllerTestMode)
        {
            List<JobScheduleItem> results = SharePoint.GetTodaysJobs(controllerTestMode);

            procLog.WriteToLog("Found: " + results.Count() + "calendar items on SharePoint calendar");

            foreach (JobScheduleItem itm in results)
            {
                IT0301_DataDeskPrograms_A programs = new IT0301_DataDeskPrograms_A();
                programs.ProcessingItem = itm.Title;
                programs.ScheduledStartTime = itm.Start;
                programs.ScheduledEndTime = itm.End;
                programs.SharePointID = itm.ID;
                programs.RequestedBy = "LsfUserSchedule";
                programs.RunMode = (controllerTestMode ? "T" : "P");

                if (itm.Category == "On Hold")
                {
                    programs.ScheduleStatus = "On Hold";
                }
                else
                {
                    programs.ScheduleStatus = "Active";
                }

                using (var context2 = new ArchivePHPEntities())
                {
                    context2.IT0301_DataDeskPrograms_A.Add(programs);
                    if (context2.GetValidationErrors().Count() > 0)
                    {
                        Console.WriteLine("Validation Error");
                    }
                    context2.SaveChanges();
                }
            }
        }

    }
}