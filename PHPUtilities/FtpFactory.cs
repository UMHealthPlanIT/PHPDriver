using Renci.SshNet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Utilities
{
    public class FtpFactory
    {
        /// <summary>
        /// Moves a given file to the program's FTP archive, overwriting any previously existing of the same name
        /// </summary>
        /// <param name="proclog">Calling program (this)</param>
        /// <param name="file">File to place inside the FtpFromDir</param>
        /// <param name="month">Whether this should be archived according to month, or just our default year</param>
        public static void ArchiveFile(Logger proclog, String file, bool month = false, bool addDateStamp = false, bool addTimeStamp = false)
        {

            String archiveTarget;
            if (month)
            {
                archiveTarget = proclog.LoggerFtpFromDir + DateTime.Today.ToString("MM") + @"\" + System.IO.Path.GetFileName(file);
            }
            else
            {
                archiveTarget = proclog.LoggerFtpFromDir + System.IO.Path.GetFileName(file);
            }

            if (addDateStamp)
            {
                archiveTarget = System.IO.Path.GetDirectoryName(archiveTarget) + @"\" + System.IO.Path.GetFileNameWithoutExtension(archiveTarget) + "_" + DateTime.Today.ToString("yyyyMMdd") + System.IO.Path.GetExtension(archiveTarget);
            }

            if (addTimeStamp)
            {
                archiveTarget = archiveTarget.Substring(0, archiveTarget.LastIndexOf('.')) + DateTime.Now.ToString("HHmmss") + Path.GetExtension(archiveTarget);
            }

            FileSystem.ReportYearDir(System.IO.Path.GetDirectoryName(archiveTarget));

            if (System.IO.File.Exists(archiveTarget))
            {
                System.IO.File.Delete(archiveTarget);
            }
            System.IO.File.Move(file, archiveTarget);

            proclog.WriteToLog("Archived " + file);
        }

        /// <summary>
        /// Moves a given file to the program's FTP archive, overwriting any previously existing of the same name
        /// </summary>
        /// <param name="proclog">Calling program (this)</param>
        /// <param name="file">File to place inside the FtpFromDir</param>
        /// <param name="month">Whether this should be archived according to month, or just our default year</param>
        public static string ArchiveFileAndReturnPath(Logger proclog, String file, bool month = false, bool addDateStamp = false, bool addTimeStamp = false)
        {

            String archiveTarget;
            if (month)
            {
                archiveTarget = proclog.LoggerFtpFromDir + DateTime.Today.ToString("MM") + @"\" + System.IO.Path.GetFileName(file);
            }
            else
            {
                archiveTarget = proclog.LoggerFtpFromDir + System.IO.Path.GetFileName(file);
            }

            if (addDateStamp)
            {
                archiveTarget = System.IO.Path.GetDirectoryName(archiveTarget) + @"\" + System.IO.Path.GetFileNameWithoutExtension(archiveTarget) + "_" + DateTime.Today.ToString("yyyyMMdd") + System.IO.Path.GetExtension(archiveTarget);
            }

            if (addTimeStamp)
            {
                archiveTarget = archiveTarget.Substring(0, archiveTarget.LastIndexOf('.')) + DateTime.Now.ToString("HHmmss") + Path.GetExtension(archiveTarget);
            }

            FileSystem.ReportYearDir(System.IO.Path.GetDirectoryName(archiveTarget));

            if (System.IO.File.Exists(archiveTarget))
            {
                System.IO.File.Delete(archiveTarget);
            }
            System.IO.File.Move(file, archiveTarget);

            proclog.WriteToLog("Archived " + file);

            return archiveTarget;
        }

        /// <summary>
        /// Moves a given file to the program's output archive, overwriting any previously existing of the same name
        /// </summary>
        /// <param name="proclog">Calling program (this)</param>
        /// <param name="file">File to place inside Output dir</param>
        public static void ArchiveFileToOutput(Logger proclog, String file, bool month = false, bool addDateStamp = false)
        {
            String archiveTarget;
            if (month)
            {
                archiveTarget = proclog.LoggerOutputYearDir + DateTime.Today.ToString("MM") + @"\" + System.IO.Path.GetFileName(file);
            }
            else
            {
                archiveTarget = proclog.LoggerOutputYearDir + System.IO.Path.GetFileName(file);
            }

            if (addDateStamp)
            {
                archiveTarget = System.IO.Path.GetDirectoryName(archiveTarget) + @"\" + System.IO.Path.GetFileNameWithoutExtension(archiveTarget) + "_" + DateTime.Today.ToString("yyyyMMdd") + System.IO.Path.GetExtension(archiveTarget);
            }

            FileSystem.ReportYearDir(proclog.LoggerOutputYearDir);

            if (System.IO.File.Exists(archiveTarget))
            {
                System.IO.File.Delete(archiveTarget);
            }
            System.IO.File.Move(file, archiveTarget);


        }

        /// <summary>
        /// Looks for files for processing on the given FTP site or the FromFtp\Staging directory. If none are found sends an email with the provided details
        /// </summary>
        /// <param name="proclog">Calling program (this)</param>
        /// <param name="ftpSite">Site to search for the given file</param>
        /// <param name="filenameContains">String to search for on the files in the given location</param>
        /// <param name="NoRecordsCode">Exit code to set if no files are found in the FTP location or the staging directory</param>
        /// <param name="NoRecordsSubject">Email subject to use if no files are found in the FTP location or the staging directory</param>
        /// <param name="NoRecordsBody">Email body to use if no files are found in the FTP location or the staging directory</param>
        /// <param name="MultipleThreads">If you would like to apply mulithreading to the download of the files</param> 
        /// <returns>List of the files that were found, including their paths</returns>
        public static List<String> GetAndNotifyForZero(Logger proclog, String ftpSite, String filenameContains, int NoRecordsCode, String NoRecordsSubject, String NoRecordsBody, String changeDir = "", Boolean deleteAfterDownload = false, Boolean MultipleThreads = false, Boolean overrideSite = false)
        {
            List<String> gotFiles = FileSystem.GetInputFiles(proclog, ftpSite, filenameContains, changeDir, deleteAfterDownload, MultipleThreads: MultipleThreads, overrideSite: overrideSite);

            FileSystem.ReportYearDir(proclog.LoggerFtpFromDir);
            if (gotFiles.Count == 0)
            {
                if (proclog.TestMode && NoRecordsCode == 6000)
                {
                    NoRecordsCode = 6004; //don't flag a real error in test mode
                }

                SendAlerts.Send(proclog.ProcessId, NoRecordsCode, NoRecordsSubject, NoRecordsBody, proclog);
            }

            return gotFiles;
        }

        /// <summary>
        /// Looks for files to process on the given network location. If none are found, sends an email with the provided details
        /// </summary>
        /// <param name="proclog">Calling program (this)</param>
        /// <param name="localDir">Network directory to search for input files</param>
        /// <param name="filenameContains">String to search for on the files in the given location</param>
        /// <param name="NoRecordsCode">Exit code to set if no files are found in the FTP location or the staging directory</param>
        /// <param name="NoRecordsSubject">Email subject to use if no files are found in the FTP location or the staging directory</param>
        /// <param name="NoRecordsBody">Email body to use if no files are found in the FTP location or the staging directory</param>
        /// <returns></returns>
        public static List<String> GetAndNotifyLocalForZero(Logger proclog, String localDir, String filenameContains, int NoRecordsCode, String NoRecordsSubject, String NoRecordsBody)
        {
            List<String> gotFiles = System.IO.Directory.GetFiles(localDir, "*" + filenameContains + "*").ToList();

            FileSystem.ReportYearDir(proclog.LoggerStagingDir);

            if (localDir != proclog.LoggerStagingDir)
            {
                gotFiles = gotFiles.Concat(System.IO.Directory.GetFiles(proclog.LoggerStagingDir, "*" + filenameContains + "*")).ToList();
            }


            if (gotFiles.Count == 0)
            {
                SendAlerts.Send(proclog.ProcessId, NoRecordsCode, NoRecordsSubject, NoRecordsBody, proclog);
            }

            return gotFiles;
        }

        /// <summary>
        /// Leverages a list of file names, and builds a paragraph listing the file names for an email body
        /// </summary>
        /// <param name="ftpFiles">List of file names to build a paragraph out of</param>
        /// <param name="fullPath">Whether or not the full path should be printed, or just the filename</param>
        /// <param name="EmailBodyHeader">(Optional) Text to output before listing the files in the string</param>
        /// <returns></returns>
        public static String buildBody(List<String> ftpFiles, Boolean fullPath, String EmailBodyHeader = "The following file(s) have been placed on the FTP site:")
        {
            String body = EmailBodyHeader + Environment.NewLine;
            String pathPiece;
            foreach (String s in ftpFiles)
            {
                if (!fullPath)
                {
                    pathPiece = System.IO.Path.GetFileName(s);
                }
                else
                {
                    pathPiece = s;
                }
                body += pathPiece + Environment.NewLine;
            }

            return body;
        }

        /// <summary>
        /// Factory method that picks up a file from an FTP site and places it on sharepoint
        /// </summary>
        /// <param name="proclog">Calling program (this)</param>
        /// <param name="ftpSite">Site to pull a file from</param>
        /// <param name="fileNameContains">Filename search criteria</param>
        /// <param name="noFileFoundExit">Exit code if no file is found</param>
        /// <param name="noFileFoundMessage">Subject and body of email if no file is found</param>
        /// <param name="fileFoundSubject">Subject of email if file is found</param>
        /// <param name="sharepointSite">Sharepoint site to push to</param>
        /// <param name="sharepointLibrary">Sharepoint library to write file to</param>
        /// <param name="changeDir">Change directory on the source FTP site (optional)</param>
        /// <param name="delAfterDownload">If the file should be removed from the FTP site after we download it</param>
        /// <param name="FileRenameDel">A function we can use to rename the file after we've downloaded it</param>
        /// <param name="overrideSite">Overrides the base functionality to only pull from TestFTP site in test mode</param>
        [Obsolete("Use of this method is highly discouraged as it uses IPSwitch for FTP functionality. Use FtpUtilities.FtpPullAndPublish() instead.")]
        public static void FtpPullAndPublish(Logger proclog, String ftpSite, String fileNameContains, int noFileFoundExit, String noFileFoundMessage, String fileFoundSubject, String sharepointSite, String sharepointLibrary, String changeDir = "", Boolean delAfterDownload = false, Boolean overrideSite = false)
        {
            List<String> foundFiles = FtpFactory.GetAndNotifyForZero(proclog, ftpSite, fileNameContains, noFileFoundExit, noFileFoundMessage, noFileFoundMessage, changeDir, delAfterDownload, overrideSite: overrideSite);

            if (foundFiles.Count == 0)
            {
                proclog.WriteToLog("we didn't find a file, ending processing");
                return;
            }

            string SharePointLibraryUrl = "";
            foreach (String file in foundFiles)
            {
                String renameAtSharePoint = "";
                //if (FileRenameDel != null)
                //{
                //    renameAtSharePoint = FileRenameDel(file);
                //}
                SharePointLibraryUrl = FileTransfer.PushToSharepoint(sharepointSite, sharepointLibrary, file, proclog, rename: renameAtSharePoint);

                FtpFactory.ArchiveFile(proclog, file, addDateStamp: true);

            }

            SendAlerts.Send(proclog.ProcessId, 0, fileFoundSubject, FtpFactory.buildBody(foundFiles, false, @"The following file(s) have been placed at """ + SharePointLibraryUrl.Substring(0, SharePointLibraryUrl.LastIndexOf("/") + 1) + @""": "), proclog);
            return;

        }

        /// <summary>
        /// Factory method that picks up a file from an FTP site and places it on sharepoint
        /// </summary>
        /// <param name="proclog">Calling program (this)</param>
        /// <param name="ftpSite">Site to pull a file from</param>
        /// <param name="fileNameContains">Filename search criteria</param>
        /// <param name="noFileFoundExit">Exit code if no file is found</param>
        /// <param name="noFileFoundMessage">Subject and body of email if no file is found</param>
        /// <param name="fileFoundSubject">Subject of email if file is found</param>
        /// <param name="changeDir">Folder to change to before looking for a file on the report site</param>
        /// <param name="delAfterDownload">If we should delete the file from the remote site after we've downloaded it</param>
        [Obsolete("Use of this method is highly discouraged as it uses IPSwitch for FTP functionality. Use FtpUtilities.FtpPullAndPublish() instead.")]
        public static void FtpPullAndPublish(Logger proclog, String ftpSite, String fileNameContains, int noFileFoundExit, String noFileFoundMessage, String fileFoundSubject, String changeDir = "", Boolean delAfterDownload = false)
        {
            FtpPullAndPublish(proclog, ftpSite, fileNameContains, noFileFoundExit, noFileFoundMessage, fileFoundSubject, "ITReports", proclog.ProcessId, changeDir, delAfterDownload: delAfterDownload);
        }

        /// <summary>
        /// Factory method that picks up a file from an FTP and places it on SharePoint in a specified document library (the other overload implies it from the program number)
        /// </summary>
        /// <param name="proclog">Calling program (this)</param>
        /// <param name="ftpSite">Site to pull a file from</param>
        /// <param name="fileNameContains">Filename search criteria</param>
        /// <param name="noFileFoundExit">Exit code if no file is found</param>
        /// <param name="noFileFoundMessage">Subject and body of email if no file is found</param>
        /// <param name="fileFoundSubject">Subject of email if file is found</param>
        /// <param name="documentLibrary">SharePoint document library to push the files to</param>
        /// <param name="changeDir">Folder to change to before looking for a file on the report site</param>
        /// <param name="delAfterDownload">If we should delete the file from the remote site after we've downloaded it</param>
        [Obsolete("Use of this method is highly discouraged as it uses IPSwitch for FTP functionality. Use FtpUtilities.FtpPullAndPublish() instead.")]
        public static void FtpPullAndPublish(Logger proclog, String ftpSite, String fileNameContains, int noFileFoundExit, String noFileFoundMessage, String fileFoundSubject, String documentLibrary, String changeDir = "", Boolean delAfterDownload = false)
        {
            FtpPullAndPublish(proclog, ftpSite, fileNameContains, noFileFoundExit, noFileFoundMessage, fileFoundSubject, "ITReports", documentLibrary, changeDir, delAfterDownload: delAfterDownload);
        }

        /// <summary>
        /// A factory method to pick up and deliver a file from an FTP site and deliver to another FTP site
        /// </summary>
        /// <param name="proclog">Calling program (this)</param>
        /// <param name="pickUpLoc">FTP Site to pick the file from</param>
        /// <param name="sourceChangeDir">Change directory locationon the source FTP site</param>
        /// <param name="fileNameContains">Filename search criteria</param>
        /// <param name="noRecsSubject">Subject line of email to send if file isn't found</param>
        /// <param name="noRecsBody">Body of email to send if file isn't found</param>
        /// <param name="targetFtpLoc">FTP Site to deliver file to</param>
        /// <param name="targetchangeDir">Change directory location on target FTP site</param>
        /// <param name="successSubject">Subject of email to send if the file is pushed successfully</param>
        /// <param name="deleteAfterDownload">If the source file should be deleted after download</param>
        /// <returns>Whether one or more files were picked up</returns>
        public static Boolean FtpPullAndFtpPush(Logger proclog, String pickUpLoc, String sourceChangeDir, String fileNameContains, String noRecsSubject, String noRecsBody, String targetFtpLoc, String targetchangeDir, String successSubject, bool deleteAfterDownload)
        {
            proclog.WriteToLog("We've received a request to pick up a file");
            List<string> Filelist = FtpFactory.GetAndNotifyForZero(proclog, pickUpLoc, fileNameContains, 0, noRecsSubject, noRecsBody, sourceChangeDir, deleteAfterDownload);

            if (Filelist.Count == 0)
            {
                return false;
            }

            foreach (String file in Filelist)
            {
                FileTransfer.DropToPhpDoorStep(proclog, file);
                FtpFactory.ArchiveFile(proclog, file);
            }



            return true;
        }

        /// <summary>
        /// Leveraging the column widths defined in SQL, reads in a text file
        /// </summary>
        /// <param name="proclog">Calling program (we'll use this to find PHP_Config)</param>
        /// <param name="ftpSite">FTP site to pull the file from</param>
        /// <param name="fileNameContains">Search criteria to find the file</param>
        /// <param name="targetTable">Table to load file into</param>
        /// <param name="headerQualifier">Qualifier denoting a header (line will be skipped)</param>
        /// <param name="trailerQualifier">Qualifier denoting a trailer (line will be skipped)</param>
        public static Boolean GetAndLoadFixed(Logger proclog, String ftpSite, String fileNameContains, String targetTable, String headerQualifier, String trailerQualifier, out List<String> foundFiles, Data.AppNames targetDB, string dirChange)
        {
            foundFiles = FileSystem.GetInputFiles(proclog, ftpSite, fileNameContains, dirChange, overrideSite: true);

            if (foundFiles.Count == 0)
            {
                proclog.WriteToLog(String.Format("We didn't find any files on the {0} site with a name like {1}, returning false", ftpSite, fileNameContains));
                return false;
            }

            foreach (String file in foundFiles)
            {
                using (StreamReader sr = new StreamReader(file))
                {
                    DataTable fileLoaded = DataWork.GetTableSchema(targetTable, targetDB);

                    while (!sr.EndOfStream)
                    {
                        String incomingRow = sr.ReadLine();

                        if (!incomingRow.StartsWith(headerQualifier) && !incomingRow.StartsWith(trailerQualifier))
                        {
                            DataRow newRow = fileLoaded.NewRow();

                            int marker = 0;

                            foreach (System.Data.DataColumn col in newRow.Table.Columns)
                            {
                                int endPosition = col.MaxLength;

                                newRow[col] = incomingRow.Substring(marker, endPosition);

                                marker += endPosition;
                            }
                            fileLoaded.Rows.Add(newRow);
                        }
                        else
                        {
                            proclog.WriteToLog("Skipping line: " + Environment.NewLine + incomingRow + Environment.NewLine + "Because it has a header or trailer qualifier");
                        }

                    }

                    DataWork.SaveDataTableToDb(targetTable, fileLoaded, targetDB);

                }

                FtpFactory.ArchiveFile(proclog, file);
            }

            return true;
        }


        /// <summary>
        /// Downloads and imports a text file into a database table
        /// </summary>
        /// <param name="proclog">Calling program (we'll use this to find PHP_Config)</param>
        /// <param name="ftpSite">FTP site to pull the file from</param>
        /// <param name="dirChange">change FTP site to locate file</param>
        /// <param name="fileNameContains">Search criteria to find the file</param>
        /// <param name="foundFiles">Ouput of files that were found</param>
        /// <param name="headerQualifier">Qualifier denoting a header (line will be skipped)</param>
        /// <param name="trailerQualifier">Qualifier denoting a trailer (line will be skipped)</param>
        /// <param name="delim">File delimiters</param>
        /// <param name="targetDB">DB where table exists</param>
        /// <param name="targetTable">Table to load file into</param>
        public static Boolean GetAndLoadDelimited(Logger proclog, Data.AppNames targetDB, String targetTable, bool TruncTable, String ftpSite, bool deleteAfterDownload, string dirChange, String fileNameContains, out List<String> foundFiles, String headerQualifier, String trailerQualifier, string delim = "|", char eolDelim = ' ', bool OverRideSite = false, Boolean dateStampArchive = false)
        {
            foundFiles = FileSystem.GetInputFiles(proclog, ftpSite, fileNameContains, dirChange, deleteAfterDownload: deleteAfterDownload, overrideSite: OverRideSite);


            if (foundFiles.Count == 0)
            {
                return false;
            }
            else
            {
                if (TruncTable)
                {
                    Utilities.DataWork.RunSqlCommand("TRUNCATE TABLE " + targetTable, targetDB);
                }

                foreach (string file in foundFiles)
                {
                    InputFile.ReadInTextFile(file, delim, true, targetTable, proclog, targetDB, endOfLine: eolDelim);
                    FtpFactory.ArchiveFile(proclog, file, addDateStamp: dateStampArchive);
                }

                return true;
            }

        }

        /// <summary>
        /// Connects to <paramref name="ftpSite"/>, navigates to <paramref name="remoteDir"/> and 
        /// searches for subdirectories matching <paramref name="searchFor"/>.
        /// </summary>
        /// 
        /// <example>
        /// Remote directory contains: /Directory/SubDir20240101, /Directory/SubDir20240205, etc...
        /// Provided <paramref name="remoteDir"/> = "Directory" and <paramref name="searchFor"/> = "SubDir202402"
        /// Return result would be a collection containing "/Directory/SubDir20240205"
        /// </example>
        /// 
        /// <param name="username">Username for given ftp site.</param>
        /// <param name="password">Password for given ftp site.</param>
        /// <param name="ftpSite">Ftp site to connect to.</param>
        /// <param name="remoteDir">Directory on the ftp site to search in.</param>
        /// <param name="searchFor">Subdirectory pattern to search for (supports Regex expressions).</param>
        /// <param name="logger">Calling job.</param>
        /// <param name="port">Port connection overide.</param>
        /// <returns>A collection of subdirectories that match <paramref name="searchFor"/>.</returns>
        /// <exception cref="IOException"></exception>
        public static IEnumerable<string> SFTPDirectorySearch(string username, string password, string ftpSite, string remoteDir, string searchFor, Logger logger, int port = 22)
        {
            IEnumerable<string> subDirectories;

            using (SftpClient client = new SftpClient(ftpSite, port, username, password))
            {
                int tries = 3;

                do
                {
                    try
                    {
                        logger.WriteToLog("Getting directories from FTP location");
                        client.OperationTimeout = new TimeSpan(0, 5, 0);

                        client.Connect();
                        var foo = client.ListDirectory(remoteDir);
                        var bar = foo.Where(x => x.IsDirectory);
                        var foobar = bar.Where(x => Regex.IsMatch(x.Name, searchFor, RegexOptions.IgnoreCase));
                        subDirectories = client.ListDirectory(remoteDir)
                                                .Where(x => x.IsDirectory && Regex.IsMatch(x.Name, searchFor, RegexOptions.IgnoreCase))
                                                .Select(x => x.FullName);
                        return subDirectories;
                    }
                    catch (Exception e)
                    {
                        logger.WriteToLog(e.ToString(), UniversalLogger.LogCategory.WARNING);
                        System.Threading.Thread.Sleep(1000 * 60 * 3); //see if waiting fixes anything
                    } 
                } while (!(--tries == 0));
            }
            
            throw new IOException("Unable to reach FTP Site, see warnings");
        }

        /// <summary>
        /// Connects to an FTP site and searches for files.
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="password"></param>
        /// <param name="ftpSite"></param>
        /// <param name="searchFor"></param>
        /// <param name="remoteDir"></param>
        /// <returns></returns>
        public static List<string> SFTPFileSearch(string userName, string password, string ftpSite, string remoteDir, string searchFor, Logger logger, int port = 22)
        {
            int tries = 3;
            bool success = false;
            List<string> fileNames = new List<string>();
            do
            {
                try
                {
                    using (SftpClient sftp = new SftpClient(ftpSite, port, userName, password))
                    {
                        logger.WriteToLog("Finding Files from FTP Site");
                        sftp.OperationTimeout = new TimeSpan(0, 5, 0);
                        sftp.Connect();
                        fileNames = sftp.ListDirectory(remoteDir).Where(f => f.IsDirectory == false && Regex.IsMatch(f.Name, searchFor, RegexOptions.IgnoreCase)).Select(f => f.Name).ToList();
                        sftp.Disconnect();
                        success = true;
                    }
                }
                catch (Exception E)
                {
                    success = false;
                    logger.WriteToLog(E.ToString(), UniversalLogger.LogCategory.WARNING);
                    System.Threading.Thread.Sleep(1000 * 60 * 3); //see if waiting fixes anything
                }
            }
            while (!(--tries == 0) && !success);
            if (!success)
            {
                throw new IOException("Unable to reach FTP Site, see warnings");
            }
            return fileNames;
        }

        public static void SFTPParallelUpload(List<string> fileNames, string userName, string password, string ftpSite, string remoteDir, Logger logger, int MaxDegreeOfParallelism = 0, int port = 22)
        {
            SFTPAction(true, userName, password, ftpSite, remoteDir, null, logger, fileNames, null, false, MaxDegreeOfParallelism, port);
        }

        public static List<string> SFTPParallelDownload(string userName, string password, string ftpSite, string remoteDir, string localPath, Logger logger, string searchFor, bool deleteAfterDownload, int MaxDegreeOfParallelism = 0, int port = 22)
        {
            return SFTPAction(false, userName, password, ftpSite, remoteDir, localPath, logger, new List<string>(), searchFor, deleteAfterDownload, MaxDegreeOfParallelism, port);
        }

        public static List<string> SFTPAction(bool upload, string userName, string password, string ftpSite, string remoteDir, string localPath, Logger logger, List<string> fileNames, string searchFor, bool deleteAfterDownload, int MaxDegreeOfParallelism = 0, int port = 22)
        {
            string writtenAction = upload ? "Upload" : "Download";
            List<string> returnFiles = new List<string>();
            if (upload == false) //downloading so get filenames from remote server
            {
                fileNames = SFTPFileSearch(userName, password, ftpSite, remoteDir, searchFor, logger, port);
            }
            ParallelOptions options = new ParallelOptions();
            if (fileNames.Count > 0)
            {
                options.MaxDegreeOfParallelism = MaxDegreeOfParallelism == 0 ? fileNames.Count() : MaxDegreeOfParallelism; //the more threads, the more faster
            }
            else
            {
                return new List<string>() { }; //no files
            }
            foreach (string file in fileNames)
            {
                logger.WriteToLog(file);
            }
            Parallel.ForEach(fileNames.AsEnumerable(), options, file =>
            {
                using (SftpClient sftp = new SftpClient(ftpSite, port, userName, password))
                {
                    int tries = 5;
                    while (true)
                    {
                        try
                        {
                            //sftp.OperationTimeout = new TimeSpan(0, 5, 0);
                            sftp.Connect();
                            if (upload == true)
                            {
                                using (var localStream = new FileStream(file, FileMode.Open))
                                {
                                    sftp.ChangeDirectory(remoteDir);
                                    logger.WriteToLog(writtenAction + "ing " + file);
                                    sftp.UploadFile(localStream, Path.GetFileName(file));
                                    logger.WriteToLog(writtenAction + "ed " + file);
                                }
                                sftp.Disconnect();
                                break;
                            }
                            else
                            {
                                FileSystem.ReportYearDir(localPath);
                                using (var localStream = File.Create(localPath + file))
                                {
                                    logger.WriteToLog(writtenAction + "ing " + remoteDir + file);
                                    sftp.DownloadFile(remoteDir + file, localStream);
                                    logger.WriteToLog(writtenAction + "ed " + remoteDir + file);
                                    returnFiles.Add(localPath + file);
                                }
                                if (deleteAfterDownload)
                                {
                                    sftp.DeleteFile(remoteDir + file);
                                    logger.WriteToLog("Deleted " + remoteDir + file + " off FTP");
                                }
                                sftp.Disconnect();
                                break;
                            }
                        }
                        catch (Exception e)
                        {
                            logger.WriteToLog("An exception has been caught for " + file + ": " + e.ToString(), UniversalLogger.LogCategory.WARNING);
                            if (--tries == 0)
                            {
                                sftp.Disconnect();
                                throw new Exception("Error: " + file + " won't " + writtenAction.ToLower() + ". This is probably an issue with their FTP site." + e.InnerException);
                            }
                            sftp.Disconnect();
                            System.Threading.Thread.Sleep(1000 * 60 * 3); //see if waiting fixes anything
                        }
                    }
                }
            });
            return returnFiles;
        }

        /// <summary>
        /// Uploads a collection of files to a destination FTP site using a single thread and single connection.
        /// </summary>
        /// <param name="files">The collection of files to be uploaded.</param>
        /// <param name="site">Target FTP site to drop the files.</param>
        /// <param name="username">Username to login to the target FTP site.</param>
        /// <param name="password">Password to login to the target FTP site.</param>
        /// <param name="remoteDir">Directory on the FTP site to drop the files in, pass an empty string for root.</param>
        /// <param name="logger">Calling process, used to log important information.</param>
        /// <param name="port">If not using the default port (22), this is the port override for the FTP site.</param>
        /// <returns>A collection of files that failed to upload, if any.</returns>
        public static IEnumerable<string> SFTPUploadFiles(IEnumerable<string> files, string site, string username, string password, string remoteDir, Logger logger, int port = 22)
        {
            if (!files.Any())
            {
                logger.WriteToLog($"File list was empty, not uploading any files to {site}.");
            }

            logger.WriteToLog($"Uploading {files.Count()} files to {site}.");

            List<string> failedFiles = new List<string>();

            using (SftpClient client = new SftpClient(site, port, username, password))
            {
                client.Connect();
                client.ChangeDirectory(remoteDir);

                foreach (string file in files)
                {
                    try
                    {
                        using (FileStream localStream = new FileStream(file, FileMode.Open))
                        {
                            logger.WriteToLog($"Uploading {file}.");
                            client.UploadFile(localStream, Path.GetFileName(file));
                            logger.WriteToLog($"Uploaded {file}.");
                        }
                    }
                    catch (Exception e)
                    {
                        failedFiles.Add(file);
                        logger.WriteToLog($"Failed to upload ${file}.\n{e}", UniversalLogger.LogCategory.WARNING);
                    }
                }

                client.Disconnect();
            }

            return failedFiles;
        }

        /// <summary>
        /// This is used to handle credential management.
        /// </summary>
        /// <param name="ftpSiteName">The site name as defined within Keepass</param>
        /// <returns></returns>
        private static KeyValuePair<string, string> SFTPSecretsHandling(string ftpSiteName)
        {
            KeyValuePair<string, string> creds = FileTransfer.GetKeyPassCredentials(ftpSiteName);
            return creds;
        }

        public static bool SFTPDeleteFile(string ftpSite, string ftpSiteName, string remoteDir, string fileToDelete, Logger logger, int port = 22)
        {
            KeyValuePair<string, string> creds = SFTPSecretsHandling(ftpSiteName);
            string userName = creds.Key;
            string password = creds.Value;


            bool fileDeleted = false;
            using (SftpClient sftp = new SftpClient(ftpSite, port, userName, password))
            {
                TimeSpan keepAlive = new TimeSpan(0, 0, 0, 1);
                sftp.KeepAliveInterval = keepAlive;
                sftp.OperationTimeout = new TimeSpan(0, 5, 0);
                sftp.Connect();
                logger.WriteToLog($"Deleting {fileToDelete} from source server.");
                try
                {
                    sftp.DeleteFile(remoteDir + fileToDelete);
                    fileDeleted = true;
                    logger.WriteToLog("Deleted " + remoteDir + fileToDelete + " off FTP");
                }
                catch (Exception e)
                {
                    logger.WriteToLog("Failed to delete file from the remote directory: " + fileToDelete + "\n " + e.ToString(), UniversalLogger.LogCategory.WARNING);
                    fileDeleted = false;
                }
                sftp.Disconnect();
            }
            return fileDeleted;
        }

        /// <summary>
        /// This method collects credentials based on the ftpSite and launches a recoverable download
        /// </summary>
        /// <param name="ftpSite">IP/URL of the ftp site to hit</param>
        /// <param name="ftpSiteName">FTP site name as defined in keepass</param>
        /// <param name="remoteDir">the FTP sites directory to download from</param>
        /// <param name="stagingPath">Where you want to initially download to (this method downloads in chunks, so best for this to be a sub directory)</param>
        /// <param name="localPath">Where you want the files to be once download is complete</param>
        /// <param name="logger">For to log</param>
        /// <param name="searchFor">The string used to identify files for downloading. Uses a non-case sensitive Regex match</param>
        /// <param name="deleteAfterDownload">Delete the file from the FTP after it's done</param>
        /// <param name="recover">if true, it will pickup downloads where previously left off. If false it will nuke any download partials</param>
        /// <param name="onlyRunCleanup">If true, this wont run download, but will pickup after (ie assembling the final file from partials and running deletes)</param>
        /// <param name="maxDegreesOfParallelism">MAX threads allowed to run. Some FTP sites choke with too many. Default is unlimited</param>
        /// <param name="port">Port used to connect to the given FTP site. Default is 22</param>
        /// <returns></returns>
        public static List<string> SFTPRecoverableDownload(string ftpSite, string ftpSiteName, string remoteDir, string stagingPath, string localPath, Logger logger, string searchFor, bool deleteAfterDownload, bool recover, bool onlyRunCleanup = false, int maxDegreesOfParallelism = 0, int port = 22)
        {
            KeyValuePair<string, string> creds = SFTPSecretsHandling(ftpSiteName);
            string userName = creds.Key;
            string password = creds.Value;
            return SFTPRecoverableDownload(userName, password, ftpSite, remoteDir, stagingPath, localPath, logger, searchFor, deleteAfterDownload, recover, onlyRunCleanup: onlyRunCleanup, maxDegreesOfParallelism: maxDegreesOfParallelism, port: port);
        }

        [Obsolete("Use the SFTPRecoverableDownload overload that handles creds through keepass (and hopefully through something better in the future)")]
        public static List<string> SFTPRecoverableDownload(string userName, string password, string ftpSite, string remoteDir, string stagingPath, string localPath, Logger logger, string searchFor, bool deleteAfterDownload, bool recover, bool onlyRunCleanup = false, int maxDegreesOfParallelism = 0, int port = 22)
        {
            List<string> fileNames = new List<string>();
            if (localPath != null)
            {
                FileSystem.ReportYearDir(localPath);
            }

            FileSystem.ReportYearDir(stagingPath);
            FileSystem.ReportYearDir(stagingPath + "\\BadFiles\\");
            //bool recover = false;
            ConcurrentBag<string> returnFiles = new ConcurrentBag<string>();
            fileNames = SFTPFileSearch(userName, password, ftpSite, remoteDir, searchFor, logger, port);
            logger.WriteToLog("Found " + fileNames.Count.ToString() + " files");
            ParallelOptions options = new ParallelOptions();
            if (fileNames.Count > 0)
            {
                options.MaxDegreeOfParallelism = maxDegreesOfParallelism == 0 ? fileNames.Count() : maxDegreesOfParallelism; //the more threads, the more faster
            }
            else
            {
                return new List<string>() { }; //no files
            }
            foreach (string file in fileNames)
            {
                logger.WriteToLog(file);
            }
            try
            {
                Parallel.ForEach(fileNames.AsEnumerable(), options, file =>
                {
                    int streams = 11;
                    long sourceSize = 0;
                    using (SftpClient sftp = new SftpClient(ftpSite, port, userName, password))
                    {
                        TimeSpan keepAlive = new TimeSpan(0, 0, 0, 1);
                        sftp.KeepAliveInterval = keepAlive;
                        int attempts = 0;
                        do
                        {
                            try
                            {
                                //sftp.OperationTimeout = new TimeSpan(0, 5, 0);
                                sftp.Connect();
                                var attr = sftp.GetAttributes(remoteDir + file);
                                sourceSize = attr.Size;
                                logger.WriteToLog("Source size: " + sourceSize.ToString() + " bytes.");

                                //another download thread every 8 MB isn't crazy, I think. Maybe increase it?
                                streams = (int)Math.Min(11, sourceSize / 8e6);
                                streams =      Math.Max(1, streams); //0 results in ArgumentOutOfRangeException
                                long remainder = sourceSize % streams;
                                
                                ParallelOptions opts = new ParallelOptions();
                                opts.MaxDegreeOfParallelism = streams;
                                if (!onlyRunCleanup)
                                {
                                    try
                                    {
                                        Parallel.For(0, streams, opts,
                                                i =>
                                                {
                                                    downloadChunk(remoteDir + file, stagingPath + Path.GetFileNameWithoutExtension(file) + "_" + i.ToString() + Path.GetExtension(file), recover, sourceSize, logger, streams, sftp, i, userName, password, ftpSite, i == (streams - 1) ? remainder : 0);
                                                    /*Type.GetType("Utilities.FtpFactory").GetMethod("downloadChunk", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { remoteDir + file, stagingPath + Path.GetFileNameWithoutExtension(file) + "_" + i.ToString() + Path.GetExtension(file), recover, sourceSize, logger, streams, sftp, i, i == (streams - 1) ? remainder : 0 });*/
                                                }
                                            );//This will create a thread for each chunk we want to download, and call the method once per thread/. 
                                    }
                                    catch (Exception e)
                                    {
                                        logger.WriteToLog("An exception has been caught for " + file + ": " + e.ToString(), UniversalLogger.LogCategory.WARNING);
                                        throw e;
                                    }
                                }

                                sftp.Disconnect();
                                recover = false;
                            }
                            catch (Exception e)
                            {
                                logger.WriteToLog("Attempt: " + attempts.ToString() + " An exception has been caught for " + file + ": " + e.ToString(), UniversalLogger.LogCategory.WARNING);
                                attempts++;
                                if (e.ToString().ToUpper().Contains("THERE IS NOT ENOUGH SPACE ON THE DISK"))
                                {
                                    logger.WriteToLog("An exception has been caught for " + file + ": " + e.ToString(), UniversalLogger.LogCategory.WARNING);
                                    throw e;
                                }
                                if (attempts == 3)
                                {
                                    throw e;
                                }
                                sftp.Disconnect();
                                recover = true;
                                System.Threading.Thread.Sleep(1000 * 30); //see if waiting fixes anything
                            }
                        } while (recover);
                    }
                    bool writeSuccess = true;
                    using (FileStream writeStream = new FileStream(stagingPath + file, FileMode.OpenOrCreate))
                    {
                        writeStream.Seek(0, SeekOrigin.End);
                        logger.WriteToLog($"Starting assembly of final file: {stagingPath + file}");
                        for (int i = 0; i < streams; i++)
                        {//reassembling the downloaded chunks into a single complete file.
                            string sourceFile = stagingPath + Path.GetFileNameWithoutExtension(file) + "_" + i.ToString() + Path.GetExtension(file);
                            long currentSize = writeStream.Length;
                            try
                            {
                                using (FileStream readStream = new FileStream(sourceFile, FileMode.Open))
                                {
                                    readStream.CopyTo(writeStream);
                                }
                            }
                            catch (Exception E)
                            {
                                writeStream.SetLength(currentSize);
                                throw E;
                            }
                            File.Delete(sourceFile);
                        }
                        logger.WriteToLog($"Finished assembling file: {stagingPath + file}");
                        if (writeStream.Length != sourceSize)
                        {
                            logger.WriteToLog(file + " writeStream and sourceSize don't match - not deleting source file. " +
                                "\n SourceSize: " + sourceSize.ToString() +
                                "\n writeStream: " + writeStream.Length.ToString(), UniversalLogger.LogCategory.WARNING);
                            deleteAfterDownload = false;//This is all for validation purposes
                            writeSuccess = false;
                        }
                    }
                    if (!writeSuccess)
                    {
                        FileSystem.ReportYearDir(stagingPath + @"Bad\");
                        File.Move(stagingPath + file, stagingPath + @"Bad\" + file);
                        throw new Exception(file + " writeStream and sourceSize don't match, file moved to Bad folder in Staging dir");
                    }
                    logger.WriteToLog($"Moving {file} to its final resting place.");
                    if(localPath != null)
                    {
                        if (File.Exists(localPath + file))
                        {
                            File.Delete(localPath + file);
                        }
                        File.Move(stagingPath + file, localPath + file);//Moving the final file to its final resting place

                        returnFiles.Add(localPath + file);
                    }
                    else
                    {
                        returnFiles.Add(stagingPath + file);
                    }


                    if (deleteAfterDownload)
                    {
                        bool filesDeleted = false;
                        int deleteAttempts = 5;
                        do
                        {
                            try
                            {
                                using (SftpClient sftp = new SftpClient(ftpSite, port, userName, password))
                                {
                                    TimeSpan keepAlive = new TimeSpan(0, 0, 0, 1);
                                    sftp.KeepAliveInterval = keepAlive;
                                    sftp.OperationTimeout = new TimeSpan(0, 5, 0);
                                    sftp.Connect();
                                    logger.WriteToLog($"Deleting {file} from source server.");
                                    try
                                    {
                                        sftp.DeleteFile(remoteDir + file);
                                        filesDeleted = true;
                                    }
                                    catch (Exception e)
                                    {
                                        logger.WriteToLog("Failed to delete file from the remote directory: " + file + "\n " + e.ToString(), UniversalLogger.LogCategory.ERROR);
                                        throw e;
                                    }
                                    logger.WriteToLog("Deleted " + remoteDir + file + " off FTP");
                                    sftp.Disconnect();
                                }
                            }
                            catch (Exception E)
                            {
                                deleteAttempts--;
                                if (deleteAttempts == 0)
                                {
                                    try
                                    {
                                        System.Threading.Thread.Sleep(1000 * 30); //see if waiting fixes anything
                                        using (SftpClient sftp = new SftpClient(ftpSite, port, userName, password))
                                        {
                                            TimeSpan keepAlive = new TimeSpan(0, 0, 0, 1);
                                            sftp.KeepAliveInterval = keepAlive;
                                            sftp.OperationTimeout = new TimeSpan(0, 5, 0);
                                            sftp.Connect();
                                            logger.WriteToLog($"Deleting {file} from source server.");
                                            try
                                            {
                                                sftp.Delete(remoteDir + file);
                                                filesDeleted = true;
                                            }
                                            catch (Exception e)
                                            {
                                                logger.WriteToLog("Failed to delete file from the remote directory: " + file + "\n " + e.ToString(), UniversalLogger.LogCategory.ERROR);
                                                throw e;
                                            }
                                            logger.WriteToLog("Deleted " + remoteDir + file + " off FTP");
                                            sftp.Disconnect();
                                        }
                                    }
                                    catch (Exception C)
                                    {
                                        throw new AggregateException("Failed to delete files via either method", new Exception[] { C, E });
                                    }
                                }
                                logger.WriteToLog($"Failed deleting file: {file}. Attempts remaining: {deleteAttempts}; \n" + E.ToString(), UniversalLogger.LogCategory.WARNING);
                                System.Threading.Thread.Sleep(1000 * 30); //see if waiting fixes anything
                            }
                        } while (filesDeleted == false);
                    }

                });
            }
            catch (Exception e)
            {
                logger.WriteToLog("An exception has been caught: " + e.ToString(), UniversalLogger.LogCategory.WARNING);
                if (e.InnerException != null)
                {
                    logger.WriteToLog(e.InnerException.ToString(), UniversalLogger.LogCategory.WARNING);
                    throw e.InnerException;
                }
                throw e;
            }

            return returnFiles.ToList();
        }


        private static void downloadChunk(string remoteFile, string localFile, bool recover, long sourceSize, Logger logger, int streams, SftpClient sftp, int streamNum, string userName, string password, string ftpSite, long remainder = 0)
        {
            {
                using (FileStream localStream = new FileStream(localFile, recover ? FileMode.Append : FileMode.Create))
                {
                    long thisSource = sourceSize / streams;//Determining initial size of this chunk
                    long total = localStream.Length;//Determining how much has already been downloaded
                    bool done = false;
                    int tries = 0;
                    do
                    {
                        try
                        {
                            logger.WriteToLog($"About to open connection {remoteFile} {streamNum}");
                            using (var sourceStream = sftp.Open(remoteFile, FileMode.Open, FileAccess.Read))
                            {
                                logger.WriteToLog($"Connection opened successfully {remoteFile} {streamNum}");
                                logger.WriteToLog("Downloading " + remoteFile + ": " + localFile);
                                sourceStream.Seek(localStream.Length + (thisSource * streamNum), SeekOrigin.Begin);//Seeking to the correct spot in the stream based on stream number and how much has already been downloaded
                                thisSource = thisSource + remainder;//Adding the remainder, in case this is the last chunk and it couldn't cleanly divide be 11. Adding after so the math above doesnt break

                                byte[] buffer = new byte[819200];//How much is downloaded at a time
                                int read;
                                int counter = 0;
                                while ((read = sourceStream.Read(buffer, 0, localStream.Length + buffer.Length > thisSource ? (int)(thisSource - localStream.Length) : buffer.Length)) != 0 && localStream.Length != thisSource)
                                {//this long statement above is basically say keep going while theres still data to read and while the local file hasn't met or exceeded it's alloted length.
                                    localStream.Write(buffer, 0, read);
                                    total = total + (long)read;//Keeping track of how much has been downloaded. Doing it this way rather than using localSteam.Length because .Length on the stream is slow - it reads through counting the size each time its called. Gets really slow on huge files
                                    // report progress
                                    if (counter == 200)
                                    {
                                        logger.WriteToLog(Path.GetFileName(localFile) + " download progress: " + Math.Round((decimal)total / (decimal)thisSource * (decimal)100.0, 2).ToString() + "%");
                                        counter = 0;//Reporting the percent complete ever 100 cycles
                                    }
                                    counter++;
                                }

                                done = true;
                                logger.WriteToLog("Downloaded " + remoteFile + ": " + localFile);
                            }
                        }
                        catch (Exception e)
                        {
                            tries++;
                            logger.WriteToLog("Attempt: " + tries.ToString() + " An exception has been caught for " + localFile + ": " + e.ToString(), UniversalLogger.LogCategory.WARNING);
                            if (e.ToString().ToUpper().Contains("CLIENT NOT CONNECTED"))
                            {
                                try
                                {
                                    sftp = new SftpClient(ftpSite, sftp.ConnectionInfo.Port, userName, password);
                                    sftp.OperationTimeout = new TimeSpan(0, 5, 0);
                                    sftp.Connect();
                                }
                                catch (Exception f)
                                {
                                    logger.WriteToLog("Error trying to reestablish connection: " + f.ToString());
                                }//probably already connected
                            }//Lots of error handling
                            else if (e.ToString().ToUpper().Contains("THERE IS NOT ENOUGH SPACE ON THE DISK"))
                            {
                                throw e;
                            }
                            if (tries == 6)
                            {
                                throw e;
                            }
                            recover = true;
                            System.Threading.Thread.Sleep(1000 * 30 * tries); //see if waiting fixes anything
                        }
                    } while (!done);

                    if (total != thisSource)
                    {
                        logger.WriteToLog(remoteFile + ": " + localFile + " writeStream and sourceSize don't match. " +
                            "\n SourceSize: " + sourceSize.ToString() +
                            "\n writeStream: " + total.ToString(), UniversalLogger.LogCategory.WARNING);
                        logger.WriteToLog($"Moving {localFile} to BadFiles");
                        File.Move(localFile, Path.GetFullPath(localFile) + "\\BadFiles\\" + Path.GetFileName(localFile));
                        throw new IOException(remoteFile + ": " + localFile + " writeStream and sourceSize don't match. " +
                            "\n SourceSize: " + sourceSize.ToString() +
                            "\n writeStream: " + total.ToString());
                    }
                }
            }
        }

        public static void Push(Logger proclog, List<string> filesToDeliver)
        {
            foreach (String file in filesToDeliver)
            {
                proclog.WriteToLog("Delivering File " + file, UniversalLogger.LogCategory.INFO);
                FileTransfer.DropToPhpDoorStep(proclog, file);
            }
        }

        public static void Push(Logger proclog, string fileToDeliver)
        {
            proclog.WriteToLog("Delivering File " + fileToDeliver, UniversalLogger.LogCategory.INFO);
            FileTransfer.DropToPhpDoorStep(proclog, fileToDeliver);
        }

        public static void PushAndNotify(Logger proclog, List<string> filesToDeliver, String ftpSite, String changeDir, String successSubject, String successBody)
        {
            foreach (String file in filesToDeliver)
            {
                proclog.WriteToLog("Delivering File " + file, UniversalLogger.LogCategory.INFO);
                FileTransfer.DropToPhpDoorStep(proclog, file);
            }

            SendAlerts.Send(proclog.ProcessId, 0, successSubject, successBody, proclog);
        }

        public static void SSHDropFile(string connName, string[] fileLocations, string dropDirectory = @"/", int port = 22, bool usePasswordAuth = false)
        {
            
            string userName = FileTransfer.GetElementFromKeyPass("UserName", connName);
            string ftpurl = FileTransfer.GetElementFromKeyPass("URL", connName);

            AuthenticationMethod[] authMethod;
            if (usePasswordAuth == false)
            {
                string privateKey = FileTransfer.GetElementFromKeyPass("PrivateKey", connName);
                PrivateKeyFile[] keyFile = new PrivateKeyFile[] { new PrivateKeyFile(new MemoryStream(Encoding.UTF8.GetBytes(privateKey))) };
                authMethod = new AuthenticationMethod[] { new PrivateKeyAuthenticationMethod(userName, keyFile) };
            }
            else
            {
                string password = FileTransfer.GetElementFromKeyPass("Password", connName);
                authMethod = new AuthenticationMethod[] { new PasswordAuthenticationMethod(userName, password) };
            }

            ConnectionInfo conn = new ConnectionInfo(ftpurl, port, userName, authMethod);

            using (SftpClient client = new SftpClient(conn))
            {
                client.Connect();

                foreach (string file in fileLocations)
                {
                    using (FileStream stream = File.OpenRead(file))
                    {
                        client.UploadFile(stream, $"{dropDirectory}{Path.GetFileName(file)}");
                    }
                }
            }
        }



        public static void SFTPUploadFiles2(string connName, string[] fileLocations, string dropDirectory = @"/", int port = 22, bool usePasswordAuth = true, bool useKeypairAuth = false)
        {
            if (!dropDirectory.EndsWith("/")) { dropDirectory += "/"; }

            string userName = FileTransfer.GetElementFromKeyPass("UserName", connName);
            string ftpurl = FileTransfer.GetElementFromKeyPass("URL", connName);

            List<AuthenticationMethod> authMethodsList = new List<AuthenticationMethod>();
            if (usePasswordAuth == true)
            {
                string password = FileTransfer.GetElementFromKeyPass("Password", connName);
                authMethodsList.Add(new PasswordAuthenticationMethod(userName, password));
            }
            if (useKeypairAuth == true) //Order of these methods seems to matter in my initial testing with Lumeris -James
            {
                string privateKey = FileTransfer.GetElementFromKeyPass("PrivateKey", connName);
                PrivateKeyFile[] keyFile = new PrivateKeyFile[] { new PrivateKeyFile(new MemoryStream(Encoding.UTF8.GetBytes(privateKey))) };
                authMethodsList.Add(new PrivateKeyAuthenticationMethod(userName, keyFile));
            }


            ConnectionInfo conn = new ConnectionInfo(ftpurl, port, userName, authMethodsList.ToArray());
            using (SftpClient client = new SftpClient(conn))
            {
                client.Connect();

                foreach (string file in fileLocations)
                {
                    using (FileStream stream = File.OpenRead(file))
                    {
                        client.UploadFile(stream, $"{dropDirectory}{Path.GetFileName(file)}");
                    }
                }
            }
        }

    }
}
