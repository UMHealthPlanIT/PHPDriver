using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utilities;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Globalization;
using System.Data;
using System.Xml;

namespace Driver.IT_0363a
{
    /// <summary>
    /// This needs to be filled in!
    /// </summary>
    public class Control
    {
        private static IT_0363ACAEdgeReporting caller;
        private enum FileType { Enrollment, Medical, Pharmacy, SupplementalDiagnosis };

        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public static void FlowControl(IT_0363ACAEdgeReporting processLog, String EDGEenvironment, ProgramOptions opt)
        {

            caller = processLog;
            String reportDate = DateTime.Today.ToString("yyyyMMdd");

            FileSystem.ReportYearDir(caller.LoggerOutputYearDir);

            //Create Supplemental Files
            if (opt.supplementalReportCreate)
            {
                Supplemental.SupplentalSubmission(caller, EDGEenvironment, opt);
            }

            //Create EDGE input files
            if (opt.enrollmentReportCreate || opt.medClaimsReportCreate || opt.pharmClaimsReportCreate)
            {

                if (opt.enrollmentReportCreate)
                {
                    IT_0363a.EnrollmentExtract.Main(caller, EDGEenvironment, opt.year + "-01-01", opt.year + "-12-31");
                }
                if (opt.medClaimsReportCreate)
                {
                    IT_0363a.MedClaimExtract medExtract = new IT_0363a.MedClaimExtract(caller, EDGEenvironment, opt.year);

                }
                if (opt.pharmClaimsReportCreate)
                {
                    IT_0363a.PharmacyClaimExtract pharmExtract = new IT_0363a.PharmacyClaimExtract(caller, EDGEenvironment, opt.year);
                }
            }

            //FTP submission files to EDGE servers
            List<String> fileList = new List<String>();
            if (opt.enrollmentEdgeSubmit || opt.medClaimsEdgeSubmit || opt.pharmClaimsEdgeSubmit || opt.supplementalEdgeSubmit)
            {
                if (opt.enrollmentEdgeSubmit)
                {
                    FileSystem.ReportYearDir(caller.LoggerOutputYearDir + @"\Submit");
                    fileList.AddRange(Directory.GetFiles(caller.LoggerOutputYearDir + @"\Submit", "?????.E.*." + EDGEenvironment + ".xml"));
                }
                if (opt.medClaimsEdgeSubmit)
                {
                    FileSystem.ReportYearDir(caller.LoggerOutputYearDir + @"\Submit");
                    fileList.AddRange(Directory.GetFiles(caller.LoggerOutputYearDir + @"\Submit", "?????.M.*." + EDGEenvironment + ".xml"));
                }
                if (opt.pharmClaimsEdgeSubmit)
                {
                    FileSystem.ReportYearDir(caller.LoggerOutputYearDir + @"\Submit");
                    fileList.AddRange(Directory.GetFiles(caller.LoggerOutputYearDir + @"\Submit", "?????.P.*." + EDGEenvironment + ".xml"));
                }
                if (opt.supplementalEdgeSubmit)
                {
                    FileSystem.ReportYearDir(caller.LoggerOutputYearDir + @"\Supplemental");
                    fileList.AddRange(Directory.GetFiles(caller.LoggerOutputYearDir + @"\Supplemental", "?????.S.*." + EDGEenvironment + ".xml"));
                }

                string fileName = "";
                string destFile = "";
                string targetPath = @"\\EDGE Files\FromFTP";

                foreach (string file in fileList)
                {
                    fileName = System.IO.Path.GetFileName(file);
                    destFile = System.IO.Path.Combine(targetPath, fileName);
                    System.IO.File.Copy(file, destFile, true);
                }

                FTPFilesToEDGE(fileList);

                ArchiveFileList(fileList);

                Dictionary<Data.AppNames, bool> edgeServers;
                Dictionary<Data.AppNames, Data.AppNames> dbServers;

                //Doesn't matter which database is connected to, each server just needs one ingest command
                edgeServers = new Dictionary<Data.AppNames, bool> { { Data.AppNames.ExampleTest, false }, { Data.AppNames.ExampleTest, false } };
                dbServers = new Dictionary<Data.AppNames, Data.AppNames> { { Data.AppNames.ExampleProd, Data.AppNames.ExampleProd }};
                bool goTime = false;
                int i = 0;

                // Putting this in a loop to attempt 5 times, if we find an error that EDGE can't phone home, we don't want to continue the processing
                while (goTime == false && i < 5)
                {
                    UniversalLogger.WriteToLog(caller, "Starting ingest attempt #" + (i + 1));

                    foreach (KeyValuePair<Data.AppNames, bool> server in edgeServers)
                    {
                        if (server.Value == false)
                        {
                            //Ingest hasn't been successful yet, initiate edge ingest command
                            processLog.WriteToLog("Sending ingest command for server: " + server.Key);
                            Data edgeConnection = new Data(server.Key);
                            Interop.SSHSendCommand(edgeConnection.server, edgeConnection.username, edgeConnection.password, @"/opt/edge/bin/edge ingest");
                        }
                    }

                    string timeStarted = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss");
                    System.Threading.Thread.Sleep(3 * 60 * 1000); //wait for 3 minutes for the ingest to fully complete. The edge comnmand runs asyncronously and if we don't wait, the files won't be ready by the time we run

                    //check edge errors
                    foreach (Data.AppNames key in edgeServers.Keys.ToList())
                    {
                        if (edgeServers[key] == false)//server.Value == false)
                        {
                            string query = "select if(EXIT_MESSAGE like '%PhoneHome%', 'false', 'true') from EDGE_SRVR_COMMON.BATCH_STEP_EXECUTION where EXIT_CODE = 'FAILED' and START_TIME >= '" + timeStarted + "' order by START_TIME desc limit 1";
                            processLog.WriteToLog("Executing query: \n" + query + "\nAgainst Edge Database: " + key);
                            List<string> result = ExtractFactory.ConnectAndQuery<string>(dbServers[key], query).ToList();
                            edgeServers[key] = result.Count > 0 ? Convert.ToBoolean(result[0]) : true;
                        }
                    }

                    goTime = edgeServers.Values.ElementAt(0) && edgeServers.Values.ElementAt(1);
                    processLog.WriteToLog("GoTime : " + goTime);
                    i++;
                }

                if (goTime == false)
                {
                    throw new Exception("Server is unable to phone home, stopping the job from continuing.");
                }
            }

            //Watch for EDGE response files on both EDGE servers
            if (opt.enrollmentGetServerOutput || opt.medClaimsGetServerOutput || opt.pharmClaimsGetServerOutput || opt.supplementalGetServerOutput)
            {
                GetEdgeResponseFiles(reportDate, EDGEenvironment, opt);
            }

            //Close program after run it complete
            if (opt.CloseAfterCompletion)
            {
                //GetBaseLineAndSend(caller, opt);
                Application.Exit();
            }
        }

        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public static Data.AppNames GetEdgeDatabase(String issuerID, String env)
        {
            Data.AppNames EdgeDatabase;

            if (env == "P")
            {
                if (issuerID == "")
                {
                    EdgeDatabase = Data.AppNames.ExampleProd;
                }
                else
                {
                    EdgeDatabase = Data.AppNames.ExampleProd;
                }
            }
            else
            {
                if (issuerID == "")
                {
                    EdgeDatabase = Data.AppNames.ExampleTest;
                }
                else
                {
                    EdgeDatabase = Data.AppNames.ExampleTest;
                }
            }
            return EdgeDatabase;
        }


        private static void FTPFilesToEDGE(List<string> filesList)
        {
            List<String> issuer20662 = new List<String>();
            List<String> issuer60829 = new List<String>();

            foreach (String file in filesList)
            {
                if (Path.GetFileName(file).Substring(0, 5) == "")
                {
                    issuer20662.Add(file);
                }
                else if (Path.GetFileName(file).Substring(0, 5) == "")
                {
                    issuer60829.Add(file);
                }
            }
            //FTP to server for issuer 20662
            FileTransfer.FtpIpSwitchPush(issuer20662, "EdgeServer1", caller, true, "/opt/edge/ingest/inbox");

            //FTP to server for issuer 60829
            FileTransfer.FtpIpSwitchPush(issuer60829, "EdgeServer2", caller, true, "/opt/edge/ingest/inbox");

        }

        private static void GetEdgeResponseFiles(String fileDate, String EDGEEnvironmnet, ProgramOptions opt)
        {
            //Get list of files dated for today, count of files found must equal 3(number of files submitted /2)

            if (opt.enrollmentOutputReportCreate)
            {
                List<String> Issuer1enrollmentFiles = GetResponseFileForSpecificSubmissionType(fileDate, "EdgeServer1", EDGEEnvironmnet, "E", "");
                List<string> Issuer2enrollmentFiles = GetResponseFileForSpecificSubmissionType(fileDate, "EdgeServer2", EDGEEnvironmnet, "E", "");
                ProcessResponseFiles(EDGEEnvironmnet, opt, Issuer1enrollmentFiles, Issuer2enrollmentFiles, FileType.Enrollment);

            }
            if (opt.medClaimsOutputReportCreate)
            {
                List<String> Issuer1medicalFiles = GetResponseFileForSpecificSubmissionType(fileDate, "EdgeServer1", EDGEEnvironmnet, "M", "");
                List<string> Issuer2medicalFiles = GetResponseFileForSpecificSubmissionType(fileDate, "EdgeServer2", EDGEEnvironmnet, "M", "");
                ProcessResponseFiles(EDGEEnvironmnet, opt, Issuer1medicalFiles, Issuer2medicalFiles, FileType.Medical);

            }
            if (opt.pharmClaimsOutputReportCreate)
            {
                List<String> Issuer1pharmFiles = GetResponseFileForSpecificSubmissionType(fileDate, "EdgeServer1", EDGEEnvironmnet, "P", "");
                List<String> Issuer2pharmFiles = GetResponseFileForSpecificSubmissionType(fileDate, "EdgeServer2", EDGEEnvironmnet, "P", "");
                ProcessResponseFiles(EDGEEnvironmnet, opt, Issuer1pharmFiles, Issuer2pharmFiles, FileType.Pharmacy);
            }
            if (opt.supplementalOutputReportCreate)
            {
                List<string> Issuer1supplementalFiles = GetResponseFileForSpecificSubmissionType(fileDate, "EdgeServer1", EDGEEnvironmnet, "S", "");
                List<String> Issuer2supplementalFiles = GetResponseFileForSpecificSubmissionType(fileDate, "EdgeServer2", EDGEEnvironmnet, "S", "");

                if(Issuer1supplementalFiles.Count < 3)
                    SendAlerts.Send(caller.ProcessId, 4, "No Response Supplimental Reports found for the Issuer ", "", caller);

                if (Issuer2supplementalFiles.Count < 3)
                    SendAlerts.Send(caller.ProcessId, 4, "No Response Supplimental Reports found for the Issuer ", "", caller);

                List<String> allFiles = Issuer1supplementalFiles.Concat<String>(Issuer2supplementalFiles).ToList();

                foreach (string file in allFiles)
                {
                    XmlDocument xDoc = new XmlDocument();
                    xDoc.Load(file.ToString());

                    if (file.Contains("SD"))
                    {
                        string fileDT = xDoc.GetElementsByTagName("outboundFileGenerationDateTime").Item(0).InnerText.ToString().Substring(0, 10);
                        string fileTy = xDoc.GetElementsByTagName("outboundFileTypeCode").Item(0).InnerText.ToString();                        
                        List<includedPlanProcessingResult> lstPlanProcessingResult = XMLTranslations.pulldtforNodesfromSDXML<includedPlanProcessingResult>("includedPlanProcessingResult", "", xDoc);
                        List<includedSupplementalDiagnosisProcessingResult> lstincludedSupplDiaProcessingResult = XMLTranslations.pulldtforNodesfromSDXML<includedSupplementalDiagnosisProcessingResult>("includedSupplementalDiagnosisProcessingResult", "insurancePlanIdentifier", xDoc);
                        List<recordedError> lstrecordedError = XMLTranslations.pulldtforNodesfromSDXML<recordedError>("recordedError", "supplementalDiagnosisIdentifier", xDoc);

                        if (file.Contains(""))
                        {
                            if (lstPlanProcessingResult.Count > 0)
                                //ExcelWork.OutputTableToExcel<includedPlanProcessingResult>(lstPlanProcessingResult, "PlanProcessingResult", caller.LoggerOutputYearDir + "\\20662_SupplementDetail_" + fileTy + "_" + fileDT + ".xlsx");
                            if (lstincludedSupplDiaProcessingResult.Count > 0)
                                //ExcelWork.OutputTableToExcel<includedSupplementalDiagnosisProcessingResult>(lstincludedSupplDiaProcessingResult, "SuppProcessingResult", caller.LoggerOutputYearDir + "\\20662_SupplementDetail_" + fileTy + "_" + fileDT + ".xlsx");
                            if (lstrecordedError.Count > 0)
                                //ExcelWork.OutputTableToExcel<recordedError>(lstrecordedError, "Recorded Error", caller.LoggerOutputYearDir + "\\20662_SupplementDetail_" + fileTy + "_" + fileDT + ".xlsx");

                            if (File.Exists(caller.LoggerOutputYearDir + "\\_SupplementDetail_" + fileTy + "_" + fileDT + ".xlsx"))
                                FileTransfer.PushToSharepoint("ITReports", caller.ProcessId, caller.LoggerOutputYearDir + "\\_SupplementDetail_" + fileTy + "_" + fileDT + ".xlsx", caller);

                        }
                        else
                        {
                            if (lstPlanProcessingResult.Count > 0)
                                //ExcelWork.OutputTableToExcel<includedPlanProcessingResult>(lstPlanProcessingResult, "PlanProcessingResult", caller.LoggerOutputYearDir + "\\60829_SupplementDetail_" + fileTy + "_" + fileDT + ".xlsx");
                            if (lstincludedSupplDiaProcessingResult.Count > 0)
                                //ExcelWork.OutputTableToExcel<includedSupplementalDiagnosisProcessingResult>(lstincludedSupplDiaProcessingResult, "SuppProcessingResult", caller.LoggerOutputYearDir + "\\60829_SupplementDetail_" + fileTy + "_" + fileDT + ".xlsx");
                            if (lstrecordedError.Count > 0)
                                //ExcelWork.OutputTableToExcel<recordedError>(lstrecordedError, "Recorded Error", caller.LoggerOutputYearDir + "\\60829_SupplementDetail_" + fileTy + "_" + fileDT + ".xlsx");

                            if (File.Exists(caller.LoggerOutputYearDir + "\\_SupplementDetail_" + fileTy + "_" + fileDT + ".xlsx"))
                                FileTransfer.PushToSharepoint("ITReports", caller.ProcessId, caller.LoggerOutputYearDir + "\\_SupplementDetail_" + fileTy + "_" + fileDT + ".xlsx", caller);
                        }
                    }                    
                }
                //ProcessResponseFiles(EDGEEnvironmnet, opt, Issuer1supplementalFiles, Issuer2supplementalFiles, FileType.SupplementalDiagnosis); consuming the supplemental response files has not been built
                ArchiveFtpFileList(Issuer1supplementalFiles);
                ArchiveFtpFileList(Issuer2supplementalFiles);
            }
        }

        private static void ProcessResponseFiles(String EDGEEnvironmnet, ProgramOptions opt, List<String> Issuer1PulledFiles, List<String> Issuer2PulledFiles, FileType edgeFileType)
        {
            List<String> allFiles = Issuer1PulledFiles.Concat<String>(Issuer2PulledFiles).ToList();

            if (allFiles.Count < 6)
            {
                SendAlerts.Send(caller.ProcessId, 6000, "Edge Error: Not Enough Response Reports Found", "We didn't find at least one Header, Detail and Summary report for this " + edgeFileType.ToString() + " submission for each Issuer", caller);
            }
            else
            {
                if (edgeFileType == FileType.Enrollment)
                {
                    IT_0363a.EnrollmentOutboundConsumption.Main(caller, EDGEEnvironmnet, allFiles);
                }
                else if (edgeFileType == FileType.Medical)
                {
                    IT_0363a.MedicalClaimOutboundConsumption.Main(caller, EDGEEnvironmnet, allFiles);   
                }
                if (edgeFileType == FileType.Pharmacy)
                {
                    IT_0363a.PharmacyClaimOutboundConsumption.Main(caller, EDGEEnvironmnet, allFiles);
                }

                CreateEDGEReports(EDGEEnvironmnet, opt, Issuer1PulledFiles, Issuer2PulledFiles, edgeFileType);
                ArchiveFtpFileList(allFiles);
            }
        }

        private static void ArchiveFileList(List<String> files)
        {
            foreach (String file in files)
            {
                FtpFactory.ArchiveFileToOutput(caller, file);
            }
        }

        private static void ArchiveFtpFileList(List<String> files)
        {
            foreach (String file in files)
            {
                FtpFactory.ArchiveFile(caller, file);
            }
        }

        private static List<String> GetResponseFileForSpecificSubmissionType(String fileDate, String FTPServer, String EDGEEnvironmnet, String fileType, String filePrefix)
        {
            List<string> gotFiles = new List<string>();

            List<String> foundFiles = FileTransfer.FtpIpswitchSearch(FTPServer, @".*" + fileType + ".*" + EDGEEnvironmnet + ".xml", caller, true, @"/opt/edge/ingest/outbox/issuer");

            if (foundFiles.Count == 0)
            {
                gotFiles = System.IO.Directory.GetFiles(caller.LoggerStagingDir, filePrefix + @"*" + fileType + "*" + EDGEEnvironmnet + ".xml").ToList();
                
            }
            else
            {
                foreach (String file in foundFiles)
                {
                    if (file.Split('.')[1].Length == 2)
                        gotFiles.AddRange(FileSystem.GetInputFiles(caller, FTPServer, file, @"/opt/edge/ingest/outbox/issuer", true, overrideSite: true));
                }
            }

            return gotFiles;
        }

        private static void CreateEDGEReports(String EDGEenvironment, ProgramOptions opt, List<string> twoZeroSixSixTwoFiles, List<string> sixZeroEightTwoNineFiles, FileType fileType)
        {
            List<string> twoZeroSixSixTwoFileNames = new List<string>();
            List<String> sixZeroEightTwoNineFileNames = new List<string>();

            foreach (String file in twoZeroSixSixTwoFiles)
            {
                twoZeroSixSixTwoFileNames.Add(System.IO.Path.GetFileName(file));
            }

            foreach (String file in sixZeroEightTwoNineFiles)
            {
                sixZeroEightTwoNineFileNames.Add(System.IO.Path.GetFileName(file));
            }

            String pharmColumns = (fileType == FileType.Pharmacy ? @",pharm.FillDate,pharm.MedicationName": "");

            String medColumn = (fileType == FileType.Medical ? @",GET_CDML_FROM_DT.CDML_FROM_DT" : "");

            //This was not being used
            /*String EdgeResponseSummaryQuery = @"SELECT
	[year],
	[month],
	[recordsReceived],
	[recordsAccepted],
	[recordsRejected]
FROM dbo.IT0363_EdgeReleaseSummary_A
WHERE
	statusTypeCode = 'R'
	and Filename in ('{0}')
    and ([year] <> 0 OR [month] <> 0)
ORDER BY
	[year] DESC,
	[month] DESC";*/

            String EdgeResponseReportQuery = @"";

            //Based on run options chosen, determine last files to populate the table
            String fileNameSuccessSubject;

            String Issuer1ReportQuery = String.Format(EdgeResponseReportQuery, String.Join("','", twoZeroSixSixTwoFileNames));
            String Issuer2ReportQuery = String.Format(EdgeResponseReportQuery, String.Join("','", sixZeroEightTwoNineFileNames));


            if (fileType == FileType.Enrollment || fileType == FileType.SupplementalDiagnosis)
            {
                fileNameSuccessSubject = (fileType == FileType.Enrollment ? "EDGE Enrollment Response Report" : "EDGE Supplemental Diagnosis Response Report");
                String outputLoc = caller.LoggerOutputYearDir + caller.ProcessId + "_" + fileNameSuccessSubject + "_" + DateTime.Today.ToString("yyyyMMdd") + ".xlsx";
                ExtractFactory.RunExcelExtract(Issuer1ReportQuery, caller.LoggerPhpArchive, "", caller, outputLoc);
                ExtractFactory.RunExcelExtract(Issuer2ReportQuery, caller.LoggerPhpArchive, "", caller, fileNameSuccessSubject, "This report contains the " + fileType.ToString() + " records rejected by EDGE along with reason codes for remediation.", "No Enrollment Response Errors Found", "Please review, this is unexpected", 4, multipleSheets: true);

            }
            else
            {
                //Glauch - removing this for now. The existing approach won't work because we aren't parsing the year/month from the XML in all cases
                // and Matt is looking for a rolling summary, when this is a report of what happened on this submission.
                //String Issuer1SummaryQuery = String.Format(EdgeResponseSummaryQuery, String.Join("','", twoZeroSixSixTwoFileNames));
                //String Issuer2SummaryQuery = String.Format(EdgeResponseSummaryQuery, String.Join("','", sixZeroEightTwoNineFileNames));

                fileNameSuccessSubject = (fileType == FileType.Medical ? "EDGE Medical Response Report" : "EDGE Pharmacy Response Report");
                String outputLoc = caller.LoggerOutputYearDir + caller.ProcessId + "_" + fileNameSuccessSubject + "_" + DateTime.Today.ToString("yyyyMMdd") + ".xlsx";
                //ExtractFactory.RunExcelExtract(Issuer1SummaryQuery, caller.LoggerPhpArchive, " Summary", caller, outputLoc);
                ExtractFactory.RunExcelExtract(Issuer1ReportQuery, caller.LoggerPhpArchive, " Detail", caller, outputLoc, multipleSheets: true);
                //ExtractFactory.RunExcelExtract(Issuer2SummaryQuery, caller.LoggerPhpArchive, " Summary", caller, outputLoc, multipleSheets: true);
                ExtractFactory.RunExcelExtract(Issuer2ReportQuery, caller.LoggerPhpArchive, " Detail", caller, fileNameSuccessSubject, "This report contains the " + fileType.ToString() + " claims records rejected by EDGE along with reason codes for remediation.", "No " + fileType.ToString() + " Claims Response Errors Found", "Please review if this is unexpected", 4, multipleSheets: true);

            }
        }

    }

    /// <summary>
    /// This needs to be filled in!
    /// </summary>
    public class ProgramOptions
    {
        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public Boolean enrollmentReportCreate { get; set; }

        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public Boolean medClaimsReportCreate { get; set; }

        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public Boolean pharmClaimsReportCreate { get; set; }

        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public Boolean enrollmentEdgeSubmit { get; set; }

        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public Boolean medClaimsEdgeSubmit { get; set; }

        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public Boolean pharmClaimsEdgeSubmit { get; set; }

        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public Boolean enrollmentGetServerOutput { get; set; }

        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public Boolean medClaimsGetServerOutput { get; set; }

        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public Boolean pharmClaimsGetServerOutput { get; set; }

        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public Boolean enrollmentOutputReportCreate { get; set; }

        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public Boolean medClaimsOutputReportCreate { get; set; }

        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public Boolean pharmClaimsOutputReportCreate { get; set; }

        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public Boolean enrollmentOutputReportSend { get; set; }

        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public Boolean medClaimsOutputReportSend { get; set; }

        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public Boolean pharmClaimsOutputReportSend { get; set; }

        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public Boolean CloseAfterCompletion { get; set; }

        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public Boolean supplementalReportCreate { get; set; }

        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public Boolean supplementalEdgeSubmit { get; set; }

        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public Boolean supplementalGetServerOutput { get; set; }

        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public Boolean supplementalOutputReportCreate { get; set; }

        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public Boolean supplementalOutputReportSend { get; set; }

        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public String year { get; set; }

        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public void RunAll(String submissionYear)
        {
            this.enrollmentReportCreate = true;
            this.medClaimsReportCreate = true;
            this.pharmClaimsReportCreate = true;
            this.enrollmentEdgeSubmit = true;
            this.medClaimsEdgeSubmit = true;
            this.pharmClaimsEdgeSubmit = true;
            this.enrollmentGetServerOutput = true;
            this.medClaimsGetServerOutput = true;
            this.pharmClaimsGetServerOutput = true;
            this.enrollmentOutputReportCreate = true;
            this.medClaimsOutputReportCreate = true;
            this.pharmClaimsOutputReportCreate = true;
            this.enrollmentOutputReportSend = true;
            this.medClaimsOutputReportSend = true;
            this.pharmClaimsOutputReportSend = true;
            this.CloseAfterCompletion = true;
            this.year = submissionYear;
        }

    }

    /// <summary>
    /// This needs to be filled in!
    /// </summary>
    public class includedPlanProcessingResult
    {
        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public int issuerRecordIdentifier { get; set; }
        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public int issuerIdentifier { get; set; }
        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public char statusTypeCode { get; set; }
        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public int planRecordIdentifier { get; set; }
        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public string insurancePlanIdentifier { get; set; }
        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public char PlanstatusTypeCode { get; set; }
    }

    /// <summary>
    /// This needs to be filled in!
    /// </summary>
    public class includedSupplementalDiagnosisProcessingResult
    {
        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public string insurancePlanIdentifier { get; set; }
        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public int supplementalDiagnosisRecordIdentifier { get; set; }
        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public string supplementalDiagnosisIdentifier { get; set; }
        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public char statusTypeCode { get; set; }
    }

    /// <summary>
    /// This needs to be filled in!
    /// </summary>
    public class recordedError
    {
        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public string supplementalDiagnosisIdentifier { get; set; }
        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public string offendingElementName { get; set; }
        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public string offendingElementValue { get; set; }
        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public string offendingElementErrorTypeCode { get; set; }
        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public string offendingElementErrorTypeMessage { get; set; }
        /// <summary>
        /// This needs to be filled in!
        /// </summary>
        public string offendingElementErrorTypeDetail { get; set; }
    }

}
