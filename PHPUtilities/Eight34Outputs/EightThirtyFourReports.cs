using Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Utilities.Eight34Outputs
{
    public class EightThirtyFourReports
    {
        /// <summary>
        /// A Dictionary of the Error Code Numbers (set in SPs and code before, with their english description. Note, there are some contention between the error codes used
        /// in Exchange versus Commercial so for now I'm calling this Exchange Error Messages until we can clean it up.
        /// </summary>
        private static readonly Dictionary<string, string> ExchangeErrorMessages = new Dictionary<string, string>
        {
            { "001", "Missing last name" },
            { "002", "Missing first name" },
            { "003", "Missing DOB" },
            { "004", "Future DOB" },
            { "005", "Missing/invalid state" },
            { "006", "Missing/invalid ZIP" },
            { "007", "Missing/invalid gender" },
            { "008", "Missing RateCode" },
            { "009", "Add record, member already active in Facets" },
            { "010", "Add record, adult member with effective date more than 90 days ago" },
            { "011", "Retroactive, adult member terminated in Facets, new effective date more than 90 days" },
            { "012", "Terming a termed member" },
            { "013", "Missing recipient, member not in Facets" },
            { "014", "Term/Add record, member in Facets but doesn't have an active timeline to work" },
            { "015", "Term/Add record, member not in Facets" },
            { "016", "Invalid Last Name" },
            { "017", "Invalid First Name" },
            { "018", "Has Medicare" },
            { "019", "New Born" },
            { "020", "Member has COB" },
            { "021", "retro-enrollment" },
            { "022", "Duplicate Entry" },
            { "023", "Class code or Class EffDate is empty/missing" },
            { "024", "Subgroup or Subgroup EffDate is empty/missing" },
            { "025", "Group number is empty/missing" },
            { "026", "COBRA" },
            { "027", "Invalid Group Number" },
            { "028", "Associated Subscriber not sent" },
            { "029", "Cov Eff Date on 834 Before Cov Eff Date in Facets " },
            { "030", "Member moved to Pension" },
            { "031", "Invalid CoverageLevel (SBEL_FI)" },
            { "032", "Plan transfer - this record was not processed.  The add record was processed instead." },
            { "033", "Multiple transactions - this record was not processed." },
            { "034", "Multiple transactions - Orphaned transaction" },
            { "035", "Associated Subscriber not in file" },
            { "036", "Change Record Applied to Term Record" },
            { "037", "Change Record Applied to Add Record" },
            { "038", "Manual update of member eligiblity change required because subscriber change was detected" },
            { "039", "Reinstate of more members than on file" },
            { "046", "Subscriber has a dummy SSN - cannot pass to MMS" },
            { "047", "Subscriber's Cov Eff Date is before their Term Date" },
            { "054", "Did not recognize county code - Manual Update Required" },
            { "055", "Did not recognize language code - Manual Update Required" },
            { "TBO", "Term By Omission" },
            { "BOD", "TBO Dependant" },
            { "056", "Incorrect length for Ref*23" },
            { "057", "Missing HIOS on Ref*CE" },
            { "058", "Incorrect length for SSN" }
        };

        /// <summary>
        /// The 834 Excel Report
        /// </summary>
        /// <param name="dbTarget">...</param>
        /// <param name="proclog">...</param>
        /// <param name="groupNameAbv">The name of the 834 name</param>
        /// <param name="groupId">The Id of this group</param>
        /// <param name="DirectoryPath">The Path the file goes to</param>
        /// <param name="Type">Which report is it</param>
        public static string Generate834Report(Data.AppNames dbTarget, Logger proclog, String groupNameAbv, String groupId, string DirectoryPath, string Type, string tboQuery = "", string outputTableName = "Output", bool portal = false, string Eight34FileName = "")
        {
            proclog.WriteToLog("We've received a request to Generate " + Type);
            List<ExcelWork.ReportView> Reports;
            string ReportType = "";
            
            switch (Type)
            {
                case "ELIGIBILITY":

                    Reports = new List<ExcelWork.ReportView>()
                    {
                        new ExcelWork.ReportView("select * from SummaryView", dbTarget, "Summary","Enrollment Activity Summary" ,"SentFacets,AdditionalInformation,'Eligibility Action'", true),
                        new ExcelWork.ReportView("select * from DetailsView", dbTarget, "Detail","Enrollment Activity Detail" ,"UniqueKey"),
                    };

                    try //Archive Table Creation
                    {
                        DataTable enrollmentActivity = ExtractFactory.ConnectAndQuery(proclog, dbTarget, $"Select * From OutputBI_V");
                        DataColumn dateProcessedColumnEN = new DataColumn("DateProcessed", typeof(DateTime));
                        DataColumn groupNameColumnEN = new DataColumn("GroupName", typeof(String));
                        DataColumn portalTransaction = new DataColumn("PortalTransaction", typeof(bool));
                        dateProcessedColumnEN.DefaultValue = DateTime.Now;
                        groupNameColumnEN.DefaultValue = groupNameAbv;
                        portalTransaction.DefaultValue = portal;
                        enrollmentActivity.Columns.Add(dateProcessedColumnEN);
                        enrollmentActivity.Columns.Add(groupNameColumnEN);
                        enrollmentActivity.Columns.Add(portalTransaction);

                        //possibly need to rename filename for archive
                        enrollmentActivity.Columns.Remove("FileName");
                        DataColumn fileName = new DataColumn("FileName", typeof(string));
                        fileName.DefaultValue = getOutputFileName(dbTarget, proclog, true);
                        enrollmentActivity.Columns.Add(fileName);
                        fileName.SetOrdinal(0);

                        DataWork.RunSqlCommand(proclog, string.Format("INSERT INTO [dbo].[EnrollmentActivity_Historical_A] SELECT * FROM [dbo].[EnrollmentActivity_A] WHERE DateProcessed < DATEADD(MONTH, -2, GETDATE())"), proclog.LoggerPhpArchive);
                        DataWork.RunSqlCommand(proclog, string.Format("DELETE FROM [PHPArchv].[dbo].[EnrollmentActivity_A] WHERE DateProcessed < DATEADD(MONTH, -2, GETDATE())"), proclog.LoggerPhpArchive);
                        DataWork.SaveDataTableToDb("EnrollmentActivity_A", enrollmentActivity, proclog.LoggerPhpArchive);
                    }
                    catch (Exception e)
                    {
                        proclog.WriteToLog($"Error generating {Type} for {groupId}, please investigate and correct. \n{e}", UniversalLogger.LogCategory.ERROR);
                    }
                    ReportType = "Enrollment_Activity";
                    break;
                case "TBO":
                    Reports = new List<ExcelWork.ReportView>()
                    {
                        new ExcelWork.ReportView(tboQuery, dbTarget, "TBO", "Term by Omission","SubscriberID"),
                    };
                    //My additions so far.
                    try
                    {
                        DataTable termByOmission = ExtractFactory.ConnectAndQuery(proclog, dbTarget, tboQuery);
                        DataColumn dateProcessedColumnTBO = new DataColumn("DateProcessed", typeof(DateTime));
                        DataColumn fileNameColumnTBO = new DataColumn("FileName", typeof(String));
                        DataColumn groupNameColumnTBO = new DataColumn("GroupName", typeof(String));
                        dateProcessedColumnTBO.DefaultValue = DateTime.Now;
                        fileNameColumnTBO.DefaultValue = getOutputFileName(dbTarget, proclog, false);
                        groupNameColumnTBO.DefaultValue = groupNameAbv;
                        termByOmission.Columns.Add(dateProcessedColumnTBO);
                        termByOmission.Columns.Add(fileNameColumnTBO);
                        termByOmission.Columns.Add(groupNameColumnTBO);
                        Data.AppNames targetTBO = proclog.TestMode ? Data.AppNames.ExampleTest : Data.AppNames.ExampleProd;
                        DataWork.RunSqlCommand(proclog, string.Format("INSERT INTO [dbo].[IT0354_TermByOmission_Historical_A] SELECT * FROM [dbo].[IT0354_TermByOmission_A] WHERE DateProcessed < DATEADD(MONTH, -2, GETDATE())"), targetTBO);
                        DataWork.RunSqlCommand(proclog, string.Format("DELETE FROM [PHPArchv].[dbo].[IT0354_TermByOmission_A] WHERE DateProcessed < DATEADD(MONTH, -2, GETDATE())"), targetTBO);
                        DataWork.SaveDataTableToDb("IT0354_TermByOmission_A", termByOmission, targetTBO);
                    }
                    catch (Exception e)
                    {
                        proclog.WriteToLog($"Error generating {Type} for {groupId}, please investigate and correct. \n{e}", UniversalLogger.LogCategory.ERROR);
                    }
                    ReportType = "TBO";
                    break;
                case "ITDETAILS":
                    proclog.LoggerReportYearDir(proclog.LoggerOutputYearDir);
                    DirectoryPath = proclog.LoggerOutputYearDir;
                    Reports = new List<ExcelWork.ReportView>()
                    {
                         new ExcelWork.ReportView($"select * from {outputTableName}", dbTarget, "Detail"
                            , "IT Details","UniqueKey"),
                    };
                    ReportType = "IT_Details";
                    break;
                case "OUTOFAREA":
                    Reports = new List<ExcelWork.ReportView>()
                    {
                        new ExcelWork.ReportView($"select case when WouldHaveOutput = 1 then 'Y' else 'N' end as HasChange, * from {outputTableName} where OOADeps = 1", dbTarget, "Detail", "Out of Area Dependents", "HasChange desc, UniqueKey"),
                    };
                    ReportType = "Out_Of_Area_Dependents";
                    break;
                default:
                    Reports = new List<ExcelWork.ReportView>()
                    {
                         new ExcelWork.ReportView("", dbTarget, "", ""),
                    };
                    ReportType = "";
                    break;
            }
            Tuple<string, string, string> TitleandDirectoryandDate = GetDirectoryandTitleandDate(dbTarget, proclog, groupNameAbv, groupId, DirectoryPath, ReportType, ".xlsx", outputTableName, Eight34FileName);
            ExcelWork.ReportList834 ReportList = new ExcelWork.ReportList834(Reports, TitleandDirectoryandDate.Item1,
            TitleandDirectoryandDate.Item2, TitleandDirectoryandDate.Item3);
            //ExcelWork.OutputDataTableToExcelFromView(ReportList);

            return ReportList.OutputLocation;
        }


        public static string Generate834EnrollmentActivityReportfromMemory(Logger proclog, IEnumerable<OutputRecord> transformedRecords, string groupNameAbv, string groupId, string sourceFileName)
        {
            DataTable enrollmentActivity = SetupDataTableforEAreport();

            LoadEnrollmentActivityDataTablefromMemory(enrollmentActivity, transformedRecords);

            DataTable enrollmentSummary = SetupSummaryDataTableforEAreport();

            enrollmentSummary = enrollmentActivity.AsEnumerable().GroupBy(r => new { SentToFacets = r["Sent to Facets?"], AdditionalInformation = r["AdditionalInformation"], EligibilityAction = r["EligAction"] })
                .Select(g =>
                {
                    DataRow r = enrollmentSummary.NewRow();
                    r["Sent to Facets?"] = g.Key.SentToFacets;
                    r["EligAction"] = g.Key.EligibilityAction;
                    r["AdditionalInformation"] = g.Key.AdditionalInformation;
                    r["Count"] = g.Count();
                    return r;
                }).CopyToDataTable();

            List<ExcelWork.ReportView> Reports = new List<ExcelWork.ReportView>()
            {
                new ExcelWork.ReportView(enrollmentSummary, sheet: "Summary", header: "Enrollment Activity Summary"),
                new ExcelWork.ReportView(enrollmentActivity, sheet: "Detail", header: "Enrollment Activity Detail"),
            };

            string source = new FileInfo(sourceFileName).Name;
            ExcelWork.ReportList834 ReportList = new ExcelWork.ReportList834(Reports, $"{proclog.LoggerOutputYearDir}{groupNameAbv}_{groupId}_Enrollment_Activity_{DateTime.Now:yyyyMMddhhmmss}.xlsx", $"{groupNameAbv} {groupId}", DateTime.Now.ToString("MM/dd/yyyy hhmmss"), source);
            //ExcelWork.OutputDataTableToExcelFromView(ReportList);

            return ReportList.OutputLocation;

        }

        private static DataTable GetTBOdMembers(List<OutputRecord> receivedMembers, Logger caller, String groupId)
        {
            //Note, Exchange is going to hit Facets directly - so the linked server isn't needed
            DataTable CurrentPopulation = ExtractFactory.ConnectAndQuery(caller, caller.LoggerExampleDb, Eight34.GetTBOQuery("", groupId, callingProcess: caller.ProcessId));

            DataTable MissingRecords = CurrentPopulation.Clone();

            foreach(DataRow r in CurrentPopulation.Rows)
            {
                if(receivedMembers.Where(x => x.SubNo == r["SBSB_ID"].ToString() && x.MemDep == r["MemberSuffix"].ToString()).Count() > 0)
                {
                    //found member in current file - move along
                }
                else
                {
                    MissingRecords.ImportRow(r);
                }
            }

            return MissingRecords;

        }

        public static string GenerateTBOReportFromMemory(List<OutputRecord> receivedMembers, Logger caller, String groupId, String groupAbbreviation)
        {

            DataTable TBOMissingRecords = GetTBOdMembers(receivedMembers, caller, groupId);
            List<ExcelWork.ReportView> Reports = new List<ExcelWork.ReportView>()
                        {
                            new ExcelWork.ReportView(TBOMissingRecords, "TBO", "Term By Omission Report"),
                        };

            ExcelWork.ReportList834 ReportList = new ExcelWork.ReportList834(Reports, caller.LoggerOutputYearDir + groupAbbreviation + "_" + groupId + "_TBO_" + DateTime.Now.ToString("yyyyMMddhhmmss") + ".xlsx", groupAbbreviation + " " + groupId, DateTime.Now.ToString("MM/dd/yyyy hhmmss"));
            //ExcelWork.OutputDataTableToExcelFromView(ReportList);

            return ReportList.OutputLocation;

        }

        private static DataTable SetupSummaryDataTableforEAreport()
        {
            DataTable enrollmentsummary = new DataTable();
            enrollmentsummary.Columns.Add(new DataColumn("Sent to System?", typeof(String)));
            enrollmentsummary.Columns.Add(new DataColumn("EligAction", typeof(String)));
            enrollmentsummary.Columns.Add(new DataColumn("AdditionalInformation", typeof(String)));
            enrollmentsummary.Columns.Add(new DataColumn("Count", typeof(String)));

            return enrollmentsummary;
        }

        private static void LoadEnrollmentActivityDataTablefromMemory(DataTable enrollmentActivity, IEnumerable<OutputRecord> transformedRecords)
        {
            foreach (OutputRecord rec in transformedRecords)
            {
                ExchangeOutputRecord eRec = rec as ExchangeOutputRecord;

                DataRow r = enrollmentActivity.NewRow();

                foreach (DataColumn c in enrollmentActivity.Columns)
                {
                    switch (c.ColumnName)
                    {

                        case "Subscriber":
                            r[c] = eRec.SubscriberFlag;
                            break;
                        case "MaintCode":
                            r[c] = eRec.MaintenanceCode;
                            break;
                        case "MaintReason":
                            r[c] = eRec.MaintenanceRSN;
                            break;
                        case "MemberOriginalEffDate":
                            r[c] = eRec.OriginalEffectiveDate;
                            break;
                        case "CoverageEffDate":
                            r[c] = eRec.CovEffDate;
                            break;
                        case "CoverageEndDate":
                            r[c] = eRec.CovEndDate;
                            break;
                        case "AdditionalInformation":
                            r[c] = TranslateErrorCodes(eRec.ErrCode);
                            break;
                        case "Sent to Facets?":
                            r[c] = eRec.Output;
                            break;
                        default:
                            List<PropertyInfo> props = eRec.GetType().GetProperties().ToList();

                            r[c] = props.Find(x => x.Name == c.ColumnName).GetValue(eRec);
                            break;
                    }

                }

                enrollmentActivity.Rows.Add(r);
            }


            enrollmentActivity.AcceptChanges();
        }

        private static string TranslateErrorCodes(string errCode)
        {
            string fullErrorMessage = "";

            string[] splitErrorCodes = errCode.TrimEnd(' ', ',').Split(',');

            foreach (string err in splitErrorCodes) 
            {
                if (!String.IsNullOrEmpty(err))
                {
                    if (ExchangeErrorMessages.TryGetValue(err.Trim(), out string errorText))
                    {
                        fullErrorMessage += errorText.Trim() + ", ";
                    }
                    else
                    {
                        throw new Exception($"ExchangeErrorMessages does not contain the provided error code ({err}).");
                    }
                }
            }

            return fullErrorMessage.TrimEnd(' ', ',');
        }

        private static DataTable SetupDataTableforEAreport()
        {
            DataTable enrollmentActivity = new DataTable();

            return enrollmentActivity;
        }

        /// <summary>
        /// Gets the filename from the output table and checks if it has ever processsed before, renames if so.
        /// </summary>
        /// <param name="dbTarget">Comm or Exchange Output Tables</param>
        /// <param name="procLog">Logger</param>
        /// <param name="makeNew">Will timestamp a re-run if new, otherwise finds the most recent filename in archive and re-uses</param>
        /// <param name="SPHN">If we need to look at a different output table on this server for Sparrow</param>
        /// <returns></returns>
        public static string getOutputFileName(Data.AppNames dbTarget, Logger procLog, bool makeNew = false)
        {
            string outputFile = ExtractFactory.ConnectAndQuery<string>(procLog, dbTarget, $"Select Distinct Top 1 FileName From Output Where FileName <> ''").FirstOrDefault();
            if (makeNew)
            {
                if (outputFile == null)
                {
                    throw new Exception("No FileName Found in Output Table");//Should be something there still
                }

                if (ExtractFactory.ConnectAndQuery(procLog, procLog.LoggerPhpArchive, $"Select * From EnrollmentActivity_A Where FileName like '%{outputFile}%'").Rows.Count > 0)//if it is already in the archive need to rename it for tracking
                {
                    outputFile = "RERUN_" + System.DateTime.Now.ToString("yyyyMMddhhmmss") + "_" + outputFile;
                }
            }
            else
            {
                outputFile = ExtractFactory.ConnectAndQuery<string>(procLog, procLog.LoggerPhpArchive, $"Select Top 1 FileName From EnrollmentActivity_A Where FileName like '%{outputFile}%' Order By DateProcessed desc").FirstOrDefault();
            }

            return outputFile;

        }

        /// <summary>
        /// Get the directory,title, and Date for the 834. These are all intertwined so we use a tuple.
        /// </summary>
        /// <param name="dbTarget">The target database</param>
        /// <param name="proclog"></param>
        /// <param name="groupNameAbv">The 834 given name for </param>
        /// <param name="groupId">The Id from the group needed for the group name</param>
        /// <param name="DirectoryPath">The path that the 834 files will go</param>
        /// <param name="ReportType">The kind of 834 report</param>
        /// <param name="FileType">is it xlsx or csv(used for Discrepancy)</param>
        /// <returns>Returns all three of the strings</returns>
        private static Tuple<string, string, string> GetDirectoryandTitleandDate(Data.AppNames dbTarget, Logger proclog, String groupNameAbv, String groupId, string DirectoryPath, string ReportType, string FileType, string outputTableName = "Output", string Eight34FileName = "")
        {
            string groupName = "";
            string PopulationSet = "";
            string OutputLocation = "";
            string Date = ExtractFactory.ConnectAndQuery<string>(dbTarget, $"SELECT max([FileDate]) FROM {outputTableName}").ToList()[0] + DateTime.Now.ToString("HHMMss"); //Date received from File
            string FileDate = Date.Replace("/", ""); //Can't have slashes in filename
            if (groupId == "L0001631") //Board of Water and Light Customization Only
            {
                PopulationSet = "_" + ExtractFactory.ConnectAndQuery<string>(dbTarget, "select distinct PopulationSET from Output where PopulationSet <> ''").ToList()[0];
                if (ReportType == "TBO" || ReportType == "Enrollment_Activity")
                {
                    OutputLocation = DirectoryPath + groupNameAbv + "_" +
                        groupId + "_" + ReportType + PopulationSet + "_" + FileDate + FileType; //The Directory and File Name the file will go.
                }
            }
            if (OutputLocation == "")
            {
                OutputLocation = DirectoryPath + (!string.IsNullOrEmpty(Eight34FileName) ? System.IO.Path.GetFileNameWithoutExtension(Eight34FileName) + "_" : "") + groupNameAbv + "_" +
                    groupId + "_" + ReportType + "_" + FileDate + FileType; //The Directory and File Name the file will go.
            }

            OutputLocation = OutputLocation.Replace(" ", "_"); //In case names have spaces
            string ReportTitle = groupName + " " + groupId + PopulationSet; //The Title on all 834    sheets
            return new Tuple<string, string, string>(OutputLocation, ReportTitle, Date);
        }

        /// <summary>
        /// This a duplicate of the 834 and should be merged in :/
        /// </summary>
        /// <param name="dbTarget"></param>
        /// <param name="proclog"></param>
        /// <param name="groupNameAbv"></param>
        /// <param name="groupId"></param>
        /// <param name="DirectoryPath"></param>
        public static string GenerateDiscrepancy(Data.AppNames dbTarget, Logger proclog, String groupNameAbv, String groupId, string DirectoryPath)
        {
            Tuple<string, string, string> TitleandDirectoryandDate = GetDirectoryandTitleandDate
                (dbTarget, proclog, groupNameAbv, groupId, DirectoryPath, "Discrepancy", ".csv"); //This is repetitive :/
            proclog.WriteToLog("We've received a request to Generate Discrepancy");
            string Query = "select * from OutputRpt_L0001102_DiscrepancyRpt_View"; //Need a query since OutputBulkText doesn't take Data Table
            OutputFile.OutputBulkText(dbTarget, Query, ",", proclog,
                TitleandDirectoryandDate.Item1, endWithSeparator: false, AddHeaders: true); //CSV with headers
            return TitleandDirectoryandDate.Item1;
        }
    }
}
