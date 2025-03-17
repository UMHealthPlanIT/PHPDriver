using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utilities;
using System.Data;
using System.IO;
using Utilities.Eight34Outputs;

namespace Utilities.Eight34Outputs
{
    public class PopulateFrom834
    {


        public static void GenerateEnrollmentTransactionFile(string GroupNum, Logger process, Data.AppNames DatabaseSource, string inputFileName, string outputLoc, string GroupName, string outputTableName = "Output")
        {
            List<OutputRecord> returnData = new List<OutputRecord>();

            string pullquery = GetInboundLoadQuery("Comm834", outputTableName);
            returnData.AddRange(ExtractFactory.ConnectAndQuery<OutputRecord>(DatabaseSource, pullquery));

            GenerateEnrollmentTransactionFile(GroupNum, process, inputFileName, outputLoc, GroupName, returnData);
        }

        /// <summary>
        /// Grabs all records to be output inside the 834 database and creates the appropriate objects, then serializes the data to file
        /// </summary>
        /// <param name="GroupNum">Group Number being processed</param>
        /// <param name="process">Calling program (this)</param>
        /// <param name="inputFile">We'll use this to notify you if your file doesn't generate any records (which is odd)</param>
        public static void GenerateEnrollmentTransactionFile(string GroupNum, Logger process, string inputFileName, string outputLoc, string GroupName, List<OutputRecord> transformedInputdata)
        {



        }



        private static string GetSubscriberSmokingIndicator(ExchangeOutputRecord subscriber, ExchangeOutputRecord spouse)
        {
            return "";
        }

        private static string GetInboundLoadQuery(string DbName, string outputTableName)
        {
            return string.Format(@"select '{0}' as [DBName], * from [{1}] where [Output] = 1", DbName, outputTableName);
        }


        private static string MakeMiddleInit(string receivedMidInit)
        {
            if (!string.IsNullOrWhiteSpace(receivedMidInit) && receivedMidInit.Length > 1)
            {
                return receivedMidInit.Substring(0, 1);

            } else if(receivedMidInit == null)
            {
                return "";
            }
            else
            {
                return receivedMidInit;
            }
        }

        private static string PopulateShortName(OutputRecord outputRec)
        {
            if (outputRec.SubscriberFlag == "Y")
            {
                return " ";
            }
            else
            {
                int endOfRead = outputRec.FirstName.Length >= 6 ? 6 : outputRec.FirstName.Length;

                return outputRec.FirstName.Substring(0, endOfRead);
            }


        }

        private static string GetMemberRelationship(OutputRecord Dependent)
        {
            if (Dependent.SubscriberFlag == "Y")
            {
                return "M";
            }
            else
            {
                if (Dependent.Gender == "M")
                {
                    if (Dependent.Relationship == "01" || Dependent.Relationship == "53")
                    {
                        return "H";

                    }
                    else if (Dependent.Relationship == "17" || Dependent.Relationship == "19")
                    {
                        return "S";
                    }
                }
                else if (Dependent.Gender == "F")
                {
                    if (Dependent.Relationship == "01" || Dependent.Relationship == "53")
                    {
                        return "W";

                    }
                    else if (Dependent.Relationship == "17" || Dependent.Relationship == "19")
                    {
                        return "D";
                    }
                }
                else
                {
                    return "O";
                }
            }
            return "";

        }

        private static string GetExchangeEffDate(ExchangeOutputRecord subscriber)
        {
            //if there is a change in the class code or subgroup for an exchange subscriber, we might need to use the DTP*303 date.
            if ((subscriber.ClassCodeOut || subscriber.SubGroupOut) && subscriber.MaintenanceCode == "001" && !string.IsNullOrWhiteSpace(subscriber.ChangeEffDt)) 
            {
                return subscriber.ChangeEffDt;
            }
            else
            {
                return subscriber.CovEffDate;
            }
        }

        /// <summary>
        /// When run in test/prod the outputs are written to our normal folder locations. When run in prod, reports and copies of keywords are written to V, and a keyword is written to
        /// the IT_0116 staging folder, where IT_0116 will run at the end of the day to merge all files into one and send to TZ
        /// </summary>
        public class OutputPaths
        {
            public string OutputTransactionFilePath { get; set; }
            public string InputEnrollmentPath { get; set; }
            public string OutputEnrollmentReportPath { get; set; }

            public OutputPaths(Logger caller)
            {
                OutputEnrollmentReportPath = caller.LoggerOutputYearDir;
                Utilities.FileSystem.ReportYearDir(OutputEnrollmentReportPath);

                if (caller.TestMode)
                {
                    OutputTransactionFilePath = caller.LoggerOutputYearDir;
                    Utilities.FileSystem.ReportYearDir(OutputTransactionFilePath);

                    InputEnrollmentPath = caller.LoggerFtpFromDir;

                }
                else
                {
                    OutputTransactionFilePath = @"\\JobOutput\IT_0116\Staging\MMS";

                    InputEnrollmentPath = @"\\Enrollment\834_Inbound_Files";

                }
            }
        }

        public static void ArchiveForEnrollment(string receivedEightThirtyFour, Logger proclog, string enrollmentArchive)
        {
            try
            {
                FileSystem.ReportYearDir(enrollmentArchive);
                File.Copy(receivedEightThirtyFour, enrollmentArchive + @"\" + Path.GetFileName(receivedEightThirtyFour), true);
                proclog.WriteToLog("Copied file to enrollment directory");
            }
            catch (Exception exc)
            {
                proclog.WriteToLog(exc.ToString());
                SendAlerts.Send(proclog.ProcessId, 6000, receivedEightThirtyFour + " was not copied to the enrollment directory", "Please review", proclog);
            }

        }

        public static void ArchiveForIT(string receivedEightThirtyFour, string bakFolder, Logger proclog)
        {
            FileSystem.ReportYearDir(bakFolder);
            string newFile = bakFolder + @"\" + System.IO.Path.GetFileName(receivedEightThirtyFour);

            if (System.IO.File.Exists(newFile))
            {
                System.IO.File.Delete(newFile);
            }

            System.IO.File.Copy(receivedEightThirtyFour, newFile, true);

            proclog.WriteToLog("Copied input file to archive location: " + newFile);
            System.IO.File.Delete(receivedEightThirtyFour);

        }
    }

}
