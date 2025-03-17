using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Ionic.Zip;

namespace Utilities
{
    /// <summary>
    /// Custom class that can be used to streamline basic report creation. It inherits logger
    /// Jobs Are Really Very Impressively Simple
    /// https://en.wikipedia.org/wiki/J.A.R.V.I.S.
    /// </summary>
    public class JARVIS : Logger
    {
        /// <summary>
        /// Set of methods used to pick up files from various sources
        /// </summary>
        public Get GetFiles { get; set; }
        /// <summary>
        /// Set of methods used to import data from files
        /// </summary>
        public Import ImportData { get; set; }
        /// <summary>
        /// Set of methods used to transform imported data into specified output types
        /// </summary>
        public Export ExportData { get; set; }
        /// <summary>
        /// Set of methods used to deliver output files in various ways
        /// </summary>
        public Send SendFiles { get; set; }
        /// <summary>
        /// Info about the reports. Includes report ID, input/output file names, and data table
        /// </summary>
        public List<Details> ReportDetails { get; set; }

        public JARVIS(LaunchRequest ProcId) : base(ProcId)
        {
            GetFiles = new Get(this);
            ImportData = new Import(this);
            ExportData = new Export(this);
            SendFiles = new Send(this);
            ReportDetails = new List<Details>();
        }

        /// <summary>
        /// You should not need to interact with this class directly
        /// </summary>
        public class Get
        {
            private readonly JARVIS Parent;

            public Get(JARVIS parent)
            {
                Parent = parent;
            }

            /// <summary>
            /// Get files from ftp site
            /// </summary>
            /// <param name="ftpSiteName">The name of the ftp site where the file(s) are</param>
            /// <param name="fileName">File name to search for, add in "*" as a wildcard to search for multiple files</param>
            /// <param name="ftpChangeDir">FTP directory where the file(s) are if not the folder we connect to. Blank by default</param>
            /// <param name="ftpDeleteAfterDownload">Delete file(s) on ftp site after downloading, true by default</param>
            public void FromFTP(string ftpSiteName, string fileName, string ftpChangeDir = "", bool ftpDeleteAfterDownload = true)
            {
                ProcessInputFiles(FileSystem.GetInputFiles(Parent, ftpSiteName, fileName, ftpChangeDir, ftpDeleteAfterDownload));
            }

            /// <summary>
            /// Get files from any drive location
            /// </summary>
            /// <param name="folderPath">Full folder path for the file</param>
            /// <param name="fileName">File name to search for, add in "*" as a wildcard to search for multiple files</param>
            public void FromDriveLocation(string folderPath, string fileName, bool traverseSubfolders = false)
            {

                List<string> fileList = Directory.GetFiles(folderPath, fileName, traverseSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).ToList();

                FileSystem.ReportYearDir(Parent.LoggerStagingDir);

                foreach (string file in fileList)
                {
                    string newFileName = Path.Combine(Parent.LoggerStagingDir, Path.GetFileName(file));

                    // If we're already pulling files from the LoggerStagingDirectory, we can just skip moving files around (they're already in place)
                    // Otherwise we need to go get our files and put them in the LoggerStagingDirectory
                    if(folderPath != Parent.LoggerStagingDir)
                    {
                        if (File.Exists(newFileName))
                        {
                            File.Delete(newFileName);
                        }

                        if (Parent.TestMode)
                        {
                            File.Copy(file, newFileName); //Don't remove file from source if we're in test
                        }
                        else
                        {
                            Directory.Move(file, newFileName);
                        }
                    }

                }

                ProcessInputFiles(Directory.GetFiles(Parent.LoggerStagingDir, fileName).ToList());
            }

            private void ProcessInputFiles(List<string> inputFiles)
            {
                inputFiles = UnzipIfZipped(inputFiles);

                foreach (string file in inputFiles)
                {
                    Parent.ReportDetails.Add(new Details(file));
                }
            }

            private List<string> UnzipIfZipped(List<string> fileNames)
            {
                List<string> newFiles = new List<string>();

                foreach (string file in fileNames)
                {
                    //if (Zippers.IsZippedFile(file))
                    //{
                    //    string newLocation = Path.GetDirectoryName(file);
                    //    using (ZipFile zip = ZipFile.Read(file))
                    //    {
                    //        foreach (ZipEntry entry in zip)
                    //        {
                    //            entry.Extract(newLocation, ExtractExistingFileAction.OverwriteSilently);
                    //            newFiles.Add(Path.Combine(newLocation, entry.FileName));
                    //        }
                    //    }

                    //    File.Delete(file); //Delete the zip file
                    //}
                    //else
                    //{
                        newFiles.Add(file);
                    //}
                }

                return newFiles;
            }
        }

        /// <summary>
        /// You should not need to interact with this class directly
        /// </summary>
        public class Import
        {
            private readonly JARVIS Parent;

            public Import(JARVIS parent)
            {
                Parent = parent;
            }

            /// <summary>
            /// Import data from a SQL query
            /// </summary>
            /// <param name="query">SQL query to run</param>
            /// <param name="datasource">Connection to use as the source</param>
            /// <param name="reportID">Unique ID used for this report, will be auto created if left blank</param>
            /// <returns>Returns the report ID</returns>
            public string FromSQL(string query, Data.AppNames datasource, string reportID = "")
            {
                reportID = CreateReportID(reportID);

                if (DataWork.IsOdbcDataSource(datasource))
                {
                    Parent.ReportDetails.Add(new Details(DataWork.QueryToDataTableODBC(Parent, query, datasource), reportID));
                }
                else
                {
                    Parent.ReportDetails.Add(new Details(DataWork.QueryToDataTable(Parent, query, datasource), reportID));
                }

                return reportID;
            }

            /// <summary>
            /// Import data from an Excel file
            /// </summary>
            /// <param name="hasHeaders">Does this file have header fields?</param>
            /// <param name="reportID">ID for the report. Will default to the first report ID if not provided</param>
            public void FromExcelFile(bool hasHeaders, string reportID = "")
            {
                Details report = Parent.GetReportDetails(reportID);
                //report.AddData(ExcelWork.ImportXlsx(report.InputFileName, hasHeaders, Parent));
            }

            /// <summary>
            /// Import data from a text file
            /// </summary>
            /// <param name="delimiter">Character used as a field delimiter in the file</param>
            /// <param name="hasHeaders">Does this file have header fields?</param>
            /// <param name="reportID">ID for the report. Will default to the first report ID if not provided</param>
            public void FromTextFile(string delimiter, bool hasHeaders, string reportID = "")
            {
                Details report = Parent.GetReportDetails(reportID);
                report.AddData(InputFile.ReadInTextFile(report.InputFileName, delimiter, hasHeaders, Parent));
            }

            private string CreateReportID(string reportID)
            {
                string finalName = reportID;
                bool duplicateName = Parent.ReportDetails.Where(x => x.ReportID == finalName).Count() > 0;

                if (reportID == "" || duplicateName)
                {
                    int defaultNameCount = Parent.ReportDetails.Where(x => x.ReportID.StartsWith("SQLReport_")).Count();
                    finalName = $"SQLReport_{defaultNameCount}";
                }

                return finalName;
            }
        }

        /// <summary>
        /// You should not need to interact with this class directly
        /// </summary>
        public class Export
        {
            private readonly JARVIS Parent;

            public Export(JARVIS parent)
            {
                Parent = parent;
            }

            /// <summary>
            /// Export data to an Excel file
            /// </summary>
            /// <param name="fileName">Name of file</param>
            /// <param name="reportID">ID for the report. Will default to the first report ID if not provided</param>
            /// <param name="worksheetName">Name of the worksheet</param>
            /// <param name="overrideFileName">Use the passed in file name without prepending the job code or appending the date stamp</param>
            public void ToExcelFile(string fileName, string reportID = "", string worksheetName = "Data", bool overrideFileName = false)
            {
                Details report = PrepareOutput(fileName, reportID, "xlsx", overrideFileName);
                //ExcelWork.OutputDataTableToExcel(report.ReportData, worksheetName, report.OutputFileNames.Last(), true);
            }

            /// <summary>
            /// Export data to a SQL table and archives the file if there is one
            /// </summary>
            /// <param name="tableName">Name of the table in SQL (it must already exist)</param>
            /// <param name="datasource">Connection to use as the destination</param>
            /// <param name="reportID">ID for the report. Will default to the first report ID if not provided</param>
            public void ToSQLTable(string tableName, Data.AppNames datasource, string reportID = "")
            {
                Details report = Parent.GetReportDetails(reportID);
                DataWork.LoadTable(datasource, tableName, report.ReportData, Parent);
                Parent.ArchiveFiles(reportID);
            }

            /// <summary>
            /// Export data to a text file
            /// </summary>
            /// <param name="fileName">Name of file</param>
            /// <param name="separator">Separator to use between fields</param>
            /// <param name="reportID">ID for the report. Will default to the first report ID if not provided</param>
            /// <param name="fileExtension">Extension to use for the file (txt is the default)</param>
            /// <param name="addHeaders">Add headers to the file (true is the default)</param>
            /// <param name="quoteQualify">Add quotes around each value and header</param>
            /// <param name="overrideFileName">Use the passed in file name without prepending the job code or appending the date stamp</param>
            public void ToTextFile(string fileName, string separator, string reportID = "", string fileExtension = "txt", bool addHeaders = true, bool quoteQualify = false, bool overrideFileName = false)
            {
                Details report = PrepareOutput(fileName, reportID, fileExtension, overrideFileName);
                //OutputFile.WriteSeparated(report.OutputFileNames.Last(), report.ReportData, separator, Parent, addHeaders, false, quoteQualify, "", "", null, true, quoteQualify);
            }

            private Details PrepareOutput(string fileName, string reportID, string extension, bool overrideFileName)
            {
                Details report = Parent.GetReportDetails(reportID);
                //recreating full file name so it works with whatever is passed in
                if (overrideFileName == false)
                {
                    fileName = $"{Parent.ProcessId}_{Path.GetFileNameWithoutExtension(fileName)}_{DateTime.Now:yyyyMMddhhmmss}.{extension.Replace(".", "")}";
                }
                fileName = Path.Combine(Parent.LoggerOutputYearDir, fileName);
                report.OutputFileNames.Add(fileName);

                return report;
            }
        }

        /// <summary>
        /// You should not need to interact with this class directly
        /// </summary>
        public class Send
        {
            private readonly JARVIS Parent;

            public Send(JARVIS parent)
            {
                Parent = parent;
            }

            /// <summary>
            /// Send file to a FTP location. FTP connection information should be filled out in FTPJobIndexCrossWalk_C
            /// </summary>
            /// <param name="reportID">ID for the report to send. Will default to the first report ID if not provided</param>
            /// <param name="zipFiles">Do you want to zip the file before sending?</param>
            /// <param name="zipPassword">Optional password for the zip file</param>
            public void ToFTP(string reportID = "", bool zipFiles = false, string zipPassword = null)
            {
                ToFTP(new List<string> { reportID }, zipFiles, zipPassword);
            }

            /// <summary>
            /// Send files to a FTP location. FTP connection information should be filled out in FTPJobIndexCrossWalk_C
            /// </summary>
            /// <param name="reportIDs">List of IDs for the reports to send</param>
            /// <param name="zipFiles">Do you want to zip the files before sending?</param>
            /// <param name="zipPassword">Optional password for the zip files</param>
            public void ToFTP(List<string> reportIDs, bool zipFiles = false, string zipPassword = null)
            {
                List<string> fileList = CreateFileList(reportIDs, zipFiles, zipPassword);
                FileTransfer.DropToPhpDoorStep(Parent, fileList);
                Parent.ArchiveFiles(reportIDs);
            }

            /// <summary>
            /// Send file through email
            /// </summary>
            /// <param name="emailSubject">Subject line for the email</param>
            /// <param name="emailBody">Body text for the email</param>
            /// <param name="reportID">ID for the report to send. Will default to the first report ID if not provided</param>
            /// <param name="zipFiles">Do you want to zip the file before sending?</param>
            /// <param name="zipPassword">Optional password for the zip file</param>
            public void ToEmail(string emailSubject, string emailBody, string reportID = "", bool zipFiles = false, string zipPassword = null)
            {
                ToEmail(emailSubject, emailBody, new List<string> { reportID }, zipFiles, zipPassword);
            }

            /// <summary>
            /// Send files through email
            /// </summary>
            /// <param name="emailSubject">Subject line for the email</param>
            /// <param name="emailBody">Body text for the email</param>
            /// <param name="reportIDs">List of IDs for the reports to send</param>
            /// <param name="zipFiles">Do you want to zip the files before sending?</param>
            /// <param name="zipPassword">Optional password for the zip files</param>
            public void ToEmail(string emailSubject, string emailBody, List<string> reportIDs, bool zipFiles = false, string zipPassword = null)
            {
                List<string> fileList = CreateFileList(reportIDs, zipFiles, zipPassword);
                SendAlerts.Send(Parent.ProcessId, 0, emailSubject, emailBody, Parent, fileList);
                Parent.ArchiveFiles(reportIDs);
            }

            /// <summary>
            /// Drop file to a drive location
            /// </summary>
            /// <param name="fullLocation">Full name of the folder you want the files in</param>
            /// <param name="reportID">ID for the report to drop. Will default to the first report ID if not provided</param>
            /// <param name="zipFiles">Do you want to zip the file before dropping?</param>
            /// <param name="zipPassword">Optional password for the zip file</param>
            public void ToDriveLocation(string fullLocation, string reportID = "", bool zipFiles = false, string zipPassword = null)
            {
                ToDriveLocation(fullLocation, new List<string> { reportID }, zipFiles, zipPassword);
            }

            /// <summary>
            /// Drop files to a drive location
            /// </summary>
            /// <param name="fullLocation">Full name of the folder you want the files in</param>
            /// <param name="reportIDs">List of IDs for the reports to drop</param>
            /// <param name="zipFiles">Do you want to zip the files before dropping?</param>
            /// <param name="zipPassword">Optional password for the zip files</param>
            public void ToDriveLocation(string fullLocation, List<string> reportIDs, bool zipFiles = false, string zipPassword = null)
            {
                List<string> fileList = CreateFileList(reportIDs, zipFiles, zipPassword);
                
                foreach (string file in fileList)
                {
                    string destinationFileName = Path.Combine(fullLocation, Path.GetFileName(file));
                    FileSystem.CopyToDir(file, destinationFileName, true);
                }

                Parent.ArchiveFiles(reportIDs);
            }

            private List<string> CreateFileList(List<string> reportIDs, bool zipFiles = false, string zipPassword = null)
            {
                List<string> fileList = new List<string>();

                foreach(string reportID in reportIDs)
                {
                    Details report = Parent.GetReportDetails(reportID);

                    if (report.OutputFileNames.Count > 0)
                    {
                        fileList.AddRange(report.OutputFileNames);
                    }
                    else
                    {
                        //If no output files were made, then we're just moving the files
                        fileList.AddRange(new List<string>() { report.InputFileName });
                        Parent.WriteToLog($"Report {reportID} does not have an output file name and so is being sent without any transformation.");
                    }
                }

                if (zipFiles)
                {
                    fileList = new List<string> { ZipFiles(fileList, zipPassword) };
                }

                return fileList;
            }

            private string ZipFiles(List<string> fileList, string zipPassword)
            {
                string zipName = $"{Parent.LoggerOutputYearDir}{Parent.ProcessId}_Files_{DateTime.Today:yyyyMMdd}.zip";
                using (ZipFile zip = new ZipFile())
                {
                    zip.Password = zipPassword;
                    zip.AddFiles(fileList, "");
                    zip.Save(zipName);
                }

                return zipName;
            }
        }

        private Details GetReportDetails(string reportID)
        {
            Details reportDetails;
            
            if (ReportDetails.Count == 0)
            {
                throw new Exception("JARVIS: No reports to reference. Please make sure to call a GetFiles method or import the data from SQL first!");
            }
            else if (reportID == "")
            {
                reportDetails = ReportDetails.First();

                if (ReportDetails.Count > 1)
                {
                    WriteToLog("More than one report has been created but no report name was given, so the first one is returned", UniversalLogger.LogCategory.WARNING);
                }
            }
            else
            {
                List<Details> reportSubset = ReportDetails.Where(x => x.ReportID == reportID).ToList();

                if (reportSubset.Count == 0)
                {
                    throw new Exception("JARVIS: ReportID used does not exist!");
                }
                else if (reportSubset.Count > 1)
                {
                    throw new Exception("JARVIS: Multiple reports found with this ReportID, please be more specific!");
                }
                else
                {
                    reportDetails = reportSubset.First();
                }
            }

            return reportDetails;
        }

        /// <summary>
        /// Returns a list of all the currently existing report IDs
        /// </summary>
        /// <returns>Returns a list of all the currently existing report IDs</returns>
        public List<string> GetReportIDs()
        {
            return ReportDetails.Select(x => x.ReportID).ToList();
        }

        private void ArchiveFiles(string reportID)
        {
            ArchiveFiles(new List<string> { reportID });
        }

        private void ArchiveFiles(List<string> reportIDs)
        {
            foreach (string report in reportIDs)
            {
                Details reportDetails = GetReportDetails(report);
                if (reportDetails.InputFileName != null)
                {
                    FtpFactory.ArchiveFile(this, reportDetails.InputFileName, addDateStamp: true, addTimeStamp: true);
                }
            }
        }

        public new void Finish()
        {
            foreach(Details report in ReportDetails)
            {
                if (File.Exists(report.InputFileName))
                {
                    WriteToLog($"{report.InputFileName} was imported, but was not fully processed. Archiving the file now", UniversalLogger.LogCategory.WARNING);
                    ArchiveFiles(report.ReportID);
                }
            }

            base.Finish();
        }

        public class Details
        {
            public string ReportID;
            public string InputFileName;
            public List<string> OutputFileNames = new List<string>(); //Making this a list just in case anyone wants to send multiple outputs of the same data?
            public DataTable ReportData;

            public Details(string inputFileName)
            {
                InputFileName = inputFileName;
                ReportID = Path.GetFileNameWithoutExtension(inputFileName);
            }

            public Details(DataTable data, string reportID)
            {
                ReportData = data;
                ReportID = reportID;
            }

            public void AddData(DataTable reportData)
            {
                ReportData = reportData;
            }
        }
    }
}