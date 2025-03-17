using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Net;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Threading;
using System.Text.RegularExpressions;
using System.Configuration;

namespace Utilities
{
    public class FileTransfer
    {

        private static CountdownEvent threadCountDown;

        /// <summary>
        /// FtpIpSwitchPush uses IpSwitch WS_FTP to push files to a target site
        /// </summary>
        /// <param name="PushFile"></param>
        /// <param name="TargetSite">Ipswitch site name</param>
        /// <param name="proclog">Calling program</param>
        /// <param name="overRide">Indicates whether the target should be redirected in test</param>
        /// <param name="TargetDirectory">Directory in Ipswitch to change to</param>
        /// <param name="renameTo">Rename file on delivery</param>
        public static void FtpIpSwitchPush(string PushFile, string TargetSite, Logger proclog, bool overRide = false, string TargetDirectory = "", string renameTo = "")
        {
            List<string> filesToPush = new List<string>();

            if (PushFile != "")
            {
                filesToPush.Add(PushFile);
            }

            FtpIpSwitchPush(filesToPush, TargetSite, proclog, overRide, TargetDirectory, renameTo);
        }


        /// <summary>
        /// Leverages the Ipswitch Scripting interface to push a file to the specified remote site
        /// </summary>
        /// <param name="pushFile">Path to the file to push</param>
        /// <param name="targetSite">Site to push the file to - must be setup as a SharedSite in IpSwitch</param>
        /// <param name="proclog">The object calling the utility (should be 'this')</param>
        /// <param name="targetDirectory">(Optional) Sub-directory on the Remote Site to write the file to. Do not start with a backslash, we'll add that in.</param>
        /// <param name="renameTo">(Optional) Just the filename to rename the source to on the push</param>
        /// <param name="overrideInTest">Instructs the method to pass the file to the given site location even when running in test. If false, we'll not push the file when running in Test. Note, standard is to not use test environment to deliver data out of network/environment</param>
        public static void FtpIpSwitchPush(List<string> pushFile, string targetSite, Logger proclog, bool overrideInTest, string targetDirectory = "", string renameTo = "")
        {
            string workDir = proclog.LoggerWorkDir;


            if (proclog.TestMode && overrideInTest == false)
            {
                proclog.WriteToLog("This is where we would have shipped the file... IF WE WERE IN PROD!");
                return;
            }


            string scriptFile = workDir + @"\put.scp";
            if (System.IO.File.Exists(scriptFile))
            {
                System.IO.File.Delete(scriptFile);
            }

            string putCommand = "";
            foreach (string s in pushFile)
            {
                if (!System.IO.File.Exists(s))
                {
                    throw new Exception("Input file not found");
                }

                string renameWrapping = "";

                if (renameTo != "")
                {
                    renameWrapping = @"""" + renameTo + @"""";
                }
                putCommand += @"put """ + s + @""" " + renameWrapping + Environment.NewLine;
            }

            FileStream putScript = new FileStream(scriptFile, FileMode.Create);
            StreamWriter sw = new StreamWriter(putScript);

            string cdFolder = targetDirectory == "" ? "" : string.Format(@"cd ""{0}""", targetDirectory);
            string connect = string.Format(@"connect ""SharedSites!{0} """ + Environment.NewLine, targetSite);
            string changeDir = string.Format("{0} " + Environment.NewLine, cdFolder);
            string closeLine = "close";

            //string fullLine = acceptCert + connect + changeDir + putCommand + closeLine;
            string fullLine = connect + changeDir + putCommand + closeLine; //acceptCert removed while we figure out why certain sites (PNC) won't connect with that option

            sw.WriteLine(fullLine);
            sw.Flush();
            sw.Close();

            string commandExecution = string.Format(@"""C:\Program Files (x86)\Ipswitch\WS_FTP 12\ftpscrpt"" -f ""{0}""", scriptFile);
            string commandLineText = @"/c """ + commandExecution + @"""";

            Process proc = new Process { StartInfo = new ProcessStartInfo { FileName = "cmd.exe", Arguments = commandLineText, UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true } };
            proc.Start();
            string response = "";

            while (!proc.StandardOutput.EndOfStream)
            {
                response += proc.StandardOutput.ReadLine() + Environment.NewLine;

            }

            proclog.WriteToLog("FTP Response: " + IpswitchObfuscationOMatic(response));

            if (response.ToUpper().Contains("FAILURE IN COMMAND") || response == "")
            {
                throw new Exception("FTP push failed, check the process logs");
            }
            else if (response.ToUpper().Contains("FAILED TO LOAD THE SCRIPT"))
            {
                throw new Exception("FTP push failed to load the script, check process logs");
            }

        }


        /// <summary>
        /// Returns a list of files that match the given
        /// </summary>
        /// <param name="sourceSite">FTP site to search</param>
        /// <param name="filenameContains"> to do a contains test on against the directory of the FTP site</param>
        /// <param name="proclog">Calling program (this)</param>
        /// <param name="overRide">Override redirection to test FTP site</param>
        /// <param name="SourceDirectory">(Optional) Sub directory on the FTP site to search</param>
        /// <returns></returns>
        public static List<string> FtpIpswitchSearch(string sourceSite, string filenameContains, Logger proclog, Boolean overRide, string SourceDirectory = "")
        {
            string workDir = proclog.LoggerWorkDir;

            if (proclog.TestMode && overRide == false)
            {
                sourceSite = proclog.TestFTP;
                proclog.WriteToLog("Overwriting the source site because we're in test mode");

            }

            string commandResponse = "";

            string dirScript = workDir + @"\dir.scp";
            if (System.IO.File.Exists(dirScript))
            {
                System.IO.File.Delete(dirScript);
            }

            FileStream newDirScript = new FileStream(dirScript, FileMode.Create);
            StreamWriter sw = new StreamWriter(newDirScript);

            string cdFolder = (SourceDirectory == "" ? "" : string.Format(@"cd ""{0}""", SourceDirectory));
            string acceptCert = "AUTH SSH 2" + Environment.NewLine;
            string connect = string.Format(@"connect ""SharedSites!{0} """ + Environment.NewLine, sourceSite);
            string changeDir = string.Format("{0} " + Environment.NewLine, cdFolder);
            string dirCommand = "dir" + Environment.NewLine;
            string closeLine = "close";
            //string fullLine = acceptCert + connect + changeDir + dirCommand + closeLine;
            string fullLine = connect + changeDir + dirCommand + closeLine; //acceptCert removed while we figure out why certain sites (PNC) won't connect with that option

            sw.WriteLine(fullLine);
            sw.Flush();
            sw.Close();

            string commandExecution = string.Format(@"""C:\Program Files (x86)\Ipswitch\WS_FTP 12\ftpscrpt"" -f ""{0}""", dirScript);
            string commandLineText = @"/c """ + commandExecution + @"""";

            Process proc = new Process { StartInfo = new ProcessStartInfo { FileName = "cmd.exe", Arguments = commandLineText, UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true } };
            proc.Start();
            List<string> filesFound = new List<string>();
            string response;

            bool startFiles = false;
            bool endFiles = false;

            while (!proc.StandardOutput.EndOfStream)
            {
                response = proc.StandardOutput.ReadLine();

                int segmentStart = response.TrimEnd().LastIndexOf("\t");

                if (segmentStart != -1)
                {
                    response = response.Substring(segmentStart, response.Length - segmentStart);
                }

                if (startFiles && response.Trim().Contains("Bytes"))
                {
                    endFiles = true;
                }

                if (startFiles && !endFiles && (Regex.IsMatch(response, @"(^|\s)" + filenameContains + @"(\s|$)") || response.Contains(filenameContains)) && response.Trim() != "")
                {
                    filesFound.Add(response.Trim());
                }

                if (response.Trim().Contains("Directory of"))
                {
                    startFiles = true;
                }

                commandResponse += response + Environment.NewLine;
            }

            proclog.WriteToLog("FTP Response: " + IpswitchObfuscationOMatic(commandResponse));

            if (commandResponse.ToUpper().Contains("FAILURE IN COMMAND") || commandResponse == "")
            {
                throw new Exception("Error in Dir call to FTP site, check the process logs");

            }
            else if (commandResponse.ToUpper().Contains("FAILED TO LOAD THE SCRIPT"))
            {
                throw new Exception("FTP search failed to load the script, check process logs");
            }
            return filesFound;

        }

        /// <summary>
        /// Returns a list of files that match the given 
        /// </summary>
        /// <param name="sourceSite"></param>
        /// <param name="filenameContains"></param>
        /// <param name="proclog"></param>
        /// <param name="SourceDirectory"></param>
        /// <returns></returns>
        public static List<string> FtpIpswitchSearch(string sourceSite, string filenameContains, Logger proclog, string SourceDirectory = "", Boolean overrideSite = false)
        {
            List<string> theList = FtpIpswitchSearch(sourceSite, filenameContains, proclog, overrideSite, SourceDirectory);
            return theList;
        }

        /// <summary>
        /// Leverages the IpSwitch scripting interface to get files from remote FTP sites
        /// </summary>
        /// <param name="destination">Where to download the file (if you don't resolve down to the filename extension, the source name will be retained)</param>
        /// <param name="sourceSite">Name of the IpSwitch Shared Site to pull the file from</param>
        /// <param name="sourceFilename">Name of the file on the remote FTP site</param>
        /// <param name="proclog">The object calling the utility (should be 'this')</param>
        /// <param name="SourceDirectory">(Optional) Directory path to move to in order to find the file</param>
        /// <param name="deleteSourceAfter">(Optional) Whether or not the source file should be deleted upon successful transfer</param>
        /// <param name="overRide">Overrides the built-in behavior to pull from TestFTP site in test mode (so will go to the real location)</param>
        /// <returns>Name of the downloaded file</returns>
        public static string FtpIpswitchGet(string destination, string sourceSite, string sourceFilename, Logger proclog, Boolean overRide, string sourceDirectory = "", Boolean deleteSourceAfter = false)
        {

            FileSystem.ReportYearDir(System.IO.Path.GetDirectoryName(destination));
            string workDir = proclog.LoggerWorkDir;
            string scriptFile = workDir + @"\get.scp";
            if (System.IO.File.Exists(scriptFile))
            {
                System.IO.File.Delete(scriptFile);
            }

            if (proclog.TestMode)
            {
                deleteSourceAfter = false;
                proclog.WriteToLog("In Test Mode. Files will NOT be deleted from source.");
            }

            if (proclog.TestMode && overRide == false)
            {
                sourceSite = proclog.TestFTP;
                proclog.WriteToLog("Overwriting the source site because we're in test mode");
            }

            string dirScript = workDir + @"\get.scp";
            if (System.IO.File.Exists(dirScript))
            {
                System.IO.File.Delete(dirScript);
            }

            FileStream putScript = new FileStream(scriptFile, FileMode.Create);
            StreamWriter sw = new StreamWriter(putScript);

            string cdFolder = sourceDirectory == "" ? "" : string.Format(@"cd ""{0}""", sourceDirectory);
            string acceptCert = "AUTH SSH 2" + Environment.NewLine;
            string connect = string.Format(@"connect ""SharedSites!{0} """ + Environment.NewLine, sourceSite);
            string changeDir = string.Format("{0} " + Environment.NewLine, cdFolder);
            string postTransferCommandOn = "";
            if (deleteSourceAfter == true)
            {
                postTransferCommandOn = "POSTXFER PXDELETE" + Environment.NewLine;
            }
            string getCommand = "GET " + "\"" + sourceFilename + "\"" + "\"" + destination + "\"" + Environment.NewLine;
            string postTransferCommandOff = "POSTXFER PXOFF" + Environment.NewLine;
            string closeLine = "close";


            //string fullLine = acceptCert + connect + changeDir + postTransferCommandOn + getCommand + postTransferCommandOff + closeLine;
            string fullLine = connect + changeDir + postTransferCommandOn + getCommand + postTransferCommandOff + closeLine; //Certain sites are failing with the acceptCert line. Investigating.

            sw.WriteLine(fullLine);
            sw.Flush();
            sw.Close();

            string commandExecution = string.Format(@"""C:\Program Files (x86)\Ipswitch\WS_FTP 12\ftpscrpt"" -f ""{0}""", scriptFile);
            string commandLineText = @"/c """ + commandExecution + @"""";

            Process proc = new Process { StartInfo = new ProcessStartInfo { FileName = "cmd.exe", Arguments = commandLineText, UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true } };
            proc.Start();
            string response = "";

            while (!proc.StandardOutput.EndOfStream)
            {
                response += proc.StandardOutput.ReadLine() + Environment.NewLine;
            }

            proclog.WriteToLog("FTP Response: " + IpswitchObfuscationOMatic(response));

            if (FileSystem.IsDirectory(destination))
            {
                return destination + sourceFilename;
            }
            else
            {
                return destination;
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="sourceSite"></param>
        /// <param name="sourceFilename"></param>
        /// <param name="proclog"></param>
        /// <param name="SourceDirectory"></param>
        /// <param name="deleteSourceAfter"></param>
        /// <returns></returns>
        public static string FtpIpswitchGet(string destination, string sourceSite, string sourceFilename, Logger proclog, string SourceDirectory = "", Boolean deleteSourceAfter = false)
        {
            string response = FtpIpswitchGet(destination, sourceSite, sourceFilename, proclog, false, SourceDirectory, deleteSourceAfter);
            return response;
        }

        /// <summary>
        /// Combines the above search and get files into a single method
        /// </summary>
        /// <param name="workDir">Program's work directory, where the bat files will be found/written</param>
        /// <param name="downloadTo">Location to download the file to, probably the loggers FTP dir</param>
        /// <param name="sourceSite">Where to look for/get the file</param>
        /// <param name="findFilenameContains">Search term to look for</param>
        /// <param name="proclog">Object to write the logs to (this)</param>
        /// <param name="gotFiles">List of the found files, won't have anything in it if the search failes and we return false</param>
        /// <param name="SourceDirectory">Optional, additional directories to go through to get to the files</param>
        /// <returns>True if file(s) were found and downloaded, false if not</returns>
        public static bool FtpIpSwitchSearchAndGet(string workDir, string downloadTo, string sourceSite, string findFilenameContains, Logger proclog, out List<string> gotFiles, string SourceDirectory = "", Boolean deleteAfterDown = false, Boolean multiThread = false, Boolean OverrideSite = false)
        {
            List<string> foundFiles = FtpIpswitchSearch(sourceSite, findFilenameContains, proclog, OverrideSite, SourceDirectory);

            gotFiles = new List<string>();
            if (foundFiles.Count == 0)
            {

                proclog.WriteToLog(findFilenameContains + " was not found on the " + sourceSite + " FTP site");

                return false;
            }
            else
            {

                if (multiThread)
                {
                    MultiThreadIpswitchGet(foundFiles, downloadTo, sourceSite, proclog, out gotFiles, SourceDirectory, deleteAfterDown);
                }
                else
                {

                    foreach (string f in foundFiles)
                    {
                        if (deleteAfterDown)
                        {
                            proclog.WriteToLog("Deleting files after download");
                        }
                        proclog.WriteToLog("Getting " + f + " from the FTP Site: " + sourceSite);
                        string gotFile = FtpIpswitchGet(downloadTo, sourceSite, f, proclog, OverrideSite, SourceDirectory, deleteAfterDown);
                        proclog.WriteToLog("Got " + f + " from the FTP site: " + sourceSite);
                        gotFiles.Add(gotFile);
                    }

                }

                return true;
            }
        }

        private static void MultiThreadIpswitchGet(List<string> foundFiles, string downloadTo, string sourceSite, Logger proclog, out List<string> gotFiles, string SourceDirectory = "", Boolean deleteAfterDown = false)
        {
            threadCountDown = new CountdownEvent(foundFiles.Count);
            string workDir = proclog.LoggerWorkDir;
            List<GetFileDetail> gotFileDetails = new List<GetFileDetail>();
            foreach (string file in foundFiles)
            {
                Logger FileLog = new Logger(proclog.ProcessId + "_" + System.IO.Path.GetFileName(file), logOnly: true);
                GetFileDetail detailsForFileToGet = new GetFileDetail() { WorkingDirectory = workDir, DownloadTarget = downloadTo, SourceSite = sourceSite, File = file, deleteAfterDown = deleteAfterDown, SourceDir = SourceDirectory, runningJob = FileLog };
                gotFileDetails.Add(detailsForFileToGet);
                Task getFile = Task.Run(() => GetFileMultiThread(detailsForFileToGet));
            }

            threadCountDown.Wait();

            gotFiles = new List<string>();
            foreach (GetFileDetail gotFile in gotFileDetails)
            {
                gotFiles.Add(gotFile.GotFileLocation);
            }

        }

        private static void GetFileMultiThread(object fileDetails)
        {
            GetFileDetail detailsOnFile = (GetFileDetail)fileDetails;

            detailsOnFile.runningJob.WriteToLog("Thread started, getting " + detailsOnFile.File);
            string gotFile = FtpIpswitchGet(detailsOnFile.DownloadTarget, detailsOnFile.SourceSite, detailsOnFile.File,
                detailsOnFile.runningJob, detailsOnFile.SourceDir, detailsOnFile.deleteAfterDown);

            detailsOnFile.GotFileLocation = gotFile;
            detailsOnFile.runningJob.WriteToLog("Finished getting " + detailsOnFile.File + " now signaling");
            threadCountDown.Signal();
        }

        private class GetFileDetail
        {
            public string WorkingDirectory { get; set; }
            public string DownloadTarget { get; set; }
            public string SourceSite { get; set; }
            public string File { get; set; }
            public Logger runningJob { get; set; }
            public string SourceDir { get; set; }
            public Boolean deleteAfterDown { get; set; }
            public string GotFileLocation { get; set; }
        }


        /// <summary>
        /// Uploads files to sharepoint via the web service
        /// </summary>
        /// <param name="sharepointSite">Sharepoint site to push to</param>
        /// <param name="documentLibrary">Document library within the sharepoint site to push to</param>
        /// <param name="sourceFile">Source file to upload to sharepoint</param>
        /// <param name="proclog">Process requesting this service (this)</param>
        /// <param name="rename">File the name should be written out as (optional)</param>
        /// <param name="targetDir">Sub-directory in the document library to write to (optional)</param>
        /// <param name="autoFixFilenames">If true, silently fixes filenames Sharepoint can't store. If false, throws an error message if an invalid fileName is used</param>
        /// <returns></returns>
        public static string PushToSharepoint(string sharepointSite, string documentLibrary, string sourceFile, Logger proclog, string rename = "", string targetDir = "", string sharepointUrl = @"https://insidephp.sparrow.org/", bool autoFixFilenames = false)
        {
            //How to get around auth issue: http://stackoverflow.com/questions/7677611/the-http-request-is-unauthorized-with-client-authentication-scheme-anonymous
            //Model: http://cecildt.blogspot.com/2010/10/upload-documents-to-sharepoint-2010.html


            string logMessage = "SharePoint URL: " + sharepointUrl
                + "SharepointSite: " + sharepointSite
                + ", DocumentLibrary: " + documentLibrary
                + ", SourceFile: " + sourceFile
                + ", rename: " + rename
                + ", targetDir: " + targetDir;

            //Get the fileName if a custom one isn't supplied
            string fileName = (String.IsNullOrWhiteSpace(rename) ? System.IO.Path.GetFileName(sourceFile) : rename);
            char[] invalidChars = "\"\\~#%&*{}:<>?/+|".ToCharArray();

            if (fileName.IndexOfAny(invalidChars) != -1)
            {
                if (autoFixFilenames)
                {
                    foreach (char c in invalidChars) { fileName = fileName.Replace(c, '_'); }
                    logMessage += ", autocorrected filename: " + fileName;
                }
                else
                {
                    throw new Exception($"Filename contains characters that Sharepoint does not allow: ({fileName} contains one of {new string(invalidChars)})");
                }
            }

            proclog.WriteToLog(logMessage);

            if (targetDir == "")
            {
                return SparrowSharePoint(sharepointUrl, sourceFile, fileName, proclog, documentLibrary, sharepointSite);
            }
            else
            {
                return SparrowSharePoint(sharepointUrl, sourceFile, fileName, proclog, documentLibrary + @"\" + targetDir, sharepointSite);
            }

        }


        private static string SparrowSharePoint(string sharePointUrl, string sourceFile, string fileName, Logger proclog, string libname, string sharePointSite)
        {
            string finalSite;
            if (proclog.TestMode && sharePointSite.ToUpper() == "ITREPORTS") //if were pushing to the new sharepoint consolidated site and in test mode, send it to the Dev site
            {
                //finalSite = sharePointSite + "/dev" + @"/";
                finalSite = sharePointSite + @"/itreportsdev" + @"/";
                libname = "IT_TEST";
            }
            else
            {
                finalSite = sharePointSite + @"/";
            }

            string sharePointUrlWithSite = sharePointUrl + finalSite;

            return UploadUsingRest(sharePointUrlWithSite, libname, sourceFile, fileName, proclog);
        }

        //Recursive Method to upload File
        public static string UploadUsingRest(string siteurl, string libraryName, string filePath, string fileName, Logger proclog, int attempts = 3)
        {
            attempts--;
            try
            {

                byte[] binary = System.IO.File.ReadAllBytes(filePath);
                string result = string.Empty;
                //Url to upload file
                string resourceUrl = siteurl + "/_api/web/GetFolderByServerRelativeUrl('" + libraryName + "')/Files/add(url='" + fileName + "',overwrite=true)";
                HttpWebRequest wreq = HttpWebRequest.Create(resourceUrl) as HttpWebRequest;
                wreq.UseDefaultCredentials = false;
                //credential who has edit access on document library

                NetworkCredential credentials = System.Net.CredentialCache.DefaultNetworkCredentials;
                wreq.Credentials = credentials;

                //Get formdigest value from site
                string formDigest = GetFormDigestValue(siteurl, credentials);
                wreq.Headers.Add("X-RequestDigest", formDigest);
                wreq.Method = "POST";
                wreq.Timeout = 1000000; //timeout should be large in order to upload file which are of large size
                wreq.Accept = "application/json; odata=verbose";
                wreq.ContentLength = binary.Length;

                using (System.IO.Stream requestStream = wreq.GetRequestStream())
                {
                    requestStream.Write(binary, 0, binary.Length);
                }



                HttpWebResponse wresp = (HttpWebResponse)wreq.GetResponse();
                using (System.IO.StreamReader sr = new System.IO.StreamReader(wresp.GetResponseStream()))
                {
                    result = sr.ReadToEnd();
                    var jObj = (JObject)JsonConvert.DeserializeObject(result);

                    string fileUrl = jObj["d"]["ServerRelativeUrl"].ToString();
                    proclog.WriteToLog("SharePoint Web response: " + result);

                    if (wresp.StatusCode != HttpStatusCode.OK)
                    {
                        throw new Exception("SharePoint Status Code Not OK");

                    }

                    return "https://insidephp.sparrow.org" + fileUrl;
                }
            }
            catch (Exception ex)
            {
                proclog.WriteToLog("Error pushing to Sharepoint: " + ex.Message);
                proclog.WriteToLog("Attempts left: " + attempts.ToString());
                if (attempts > 0)
                {
                    System.Threading.Thread.Sleep(1 * 60 * 1000);//Wait for 1 minute before trying again
                    return UploadUsingRest(siteurl, libraryName, filePath, fileName, proclog, attempts);
                }
                else
                {
                    throw ex;
                }
            }
        }


        //Recursive Method to upload File
        public static string UploadByteArrayUsingRest(string siteurl, string libraryName, string fileName, byte[] fileContent, Logger proclog, int attempts = 1)
        {
            attempts--;
            try
            {

                byte[] binary = fileContent;
                string fname = fileName;
                string result = string.Empty;

                //Url to upload file
                string resourceUrl = siteurl + "/_api/web/GetFolderByServerRelativeUrl('" + libraryName + "')/Files/add(url='" + fname + "',overwrite=true)";

                HttpWebRequest wreq = HttpWebRequest.Create(resourceUrl) as HttpWebRequest;
                wreq.UseDefaultCredentials = true;
                //credential who has edit access on document library

                NetworkCredential credentials = System.Net.CredentialCache.DefaultNetworkCredentials;
                wreq.Credentials = credentials;

                //Get formdigest value from site
                string formDigest = GetFormDigestValue(siteurl, credentials);
                wreq.Headers.Add("X-RequestDigest", formDigest);
                wreq.Method = "POST";
                wreq.Timeout = 1000000; //timeout should be large in order to upload file which are of large size
                wreq.Accept = "application/json; odata=verbose";
                wreq.ContentLength = binary.Length;

                using (System.IO.Stream requestStream = wreq.GetRequestStream())
                {
                    requestStream.Write(binary, 0, binary.Length);
                }

                HttpWebResponse wresp = (HttpWebResponse)wreq.GetResponse();
                using (System.IO.StreamReader sr = new System.IO.StreamReader(wresp.GetResponseStream()))
                {
                    result = sr.ReadToEnd();
                    var jObj = (JObject)JsonConvert.DeserializeObject(result);

                    string fileUrl = jObj["d"]["ServerRelativeUrl"].ToString();
                    proclog.WriteToLog("SharePoint Web response: " + result);

                    if (wresp.StatusCode != HttpStatusCode.OK)
                    {
                        throw new Exception("SharePoint Status Code Not OK");
                    }

                    return "https://insidephp.sparrow.org" + fileUrl;
                }
            }
            catch (Exception ex)
            {
                proclog.WriteToLog("Error pushing to Sharepoint: " + ex.Message);
                proclog.WriteToLog("Attempts left: " + attempts.ToString());
                if (attempts > 0)
                {
                    Thread.Sleep(1 * 60 * 1000);//Wait for 1 minute before trying again
                    return UploadByteArrayUsingRest(siteurl, libraryName, fileName, fileContent, proclog, attempts);
                }
                else
                {
                    throw ex;
                }
            }
        }


        //Method which return form digest value
        private static string GetFormDigestValue(string siteurl, NetworkCredential credentials)
        {
            string newFormDigest = "";
            HttpWebRequest endpointRequest = (HttpWebRequest)HttpWebRequest.Create(siteurl + "/_api/contextinfo");
            endpointRequest.Method = "POST";
            endpointRequest.ContentLength = 0;
            endpointRequest.Credentials = credentials;
            endpointRequest.Accept = "application/json;odata=verbose";

            try
            {
                HttpWebResponse endpointResponse = (HttpWebResponse)endpointRequest.GetResponse();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            try
            {

                WebResponse webResp = endpointRequest.GetResponse();
                Stream webStream = webResp.GetResponseStream();
                StreamReader responseReader = new StreamReader(webStream);
                string response = responseReader.ReadToEnd();
                var jObj = (JObject)JsonConvert.DeserializeObject(response);
                foreach (var item in jObj["d"].Children())
                {
                    newFormDigest = item.First()["FormDigestValue"].ToString();
                }
                responseReader.Close();

            }
            catch (Exception ex)
            {

                Console.WriteLine(ex.Message);
            }

            return newFormDigest;
        }


        private static void TranslateSharePointErrorMessage(string sharePointErrorMessage, Logger proclog)
        {
            if (sharePointErrorMessage == "Object reference not set to an instance of an object.")
            {
                proclog.WriteToLog("This 'object reference' message comes back when Sharepoint can't find the given site, translating that to 'Sharepoint Site Not Found'");
                throw new Exception("Sharepoint Site Not Found");
            }
            else
            {
                throw new Exception("Upload error: " + sharePointErrorMessage);
            }
        }

        /// <summary>
        /// Given a list of files, creates an email message describing the files as being placed on SharePoint and a NewLine separated list of affected files
        /// </summary>
        /// <param name="files">List of files that were pushed to SharePoint</param>
        /// <returns>Email body describing the files placed on SharePoint</returns>
        public static string BuildSharepointBody(List<string> files)
        {

            string body = "The following file(s) have been placed on insidephp:" + Environment.NewLine;

            foreach (string s in files)
            {
                body += s + Environment.NewLine;
            }

            return body;
        }

        /// <summary>
        /// Following the conventions of our interface to Rhapsody, will prefix the file with the program code and drop to the PHPDoorstep 'FromPHP' share
        /// </summary>
        /// <param name="proclog">Calling program - we'll tag the file appropriately for pick-up by Rhapsody</param>
        /// <param name="outboundFile">File to deliver to the third-party</param>
        /// <param name="progCodeOverride">Used by SasPush to push to Rhapsody as the calling file, not itself</param>
        /// <param name="overrideInTest">If true, this will override the IT_TEST route and send it to tint.shs.org with the real job code</param>

        public static void DropToPhpDoorStep(Logger proclog, string outboundFile, string rename = "", string progCodeOverride = "", bool overrideInTest = false)
        {
            if (IsPHPJob(proclog, progCodeOverride))
            {
                proclog.WriteToLog(proclog.ProcessId + " was found to be a PHP job. Using IPSwitch for transfer.");
                try
                {
                    FtpIpSwitchPush(outboundFile, proclog, rename, progCodeOverride, overrideInTest);
                }
                catch
                {
                    proclog.WriteToLog($"First Pass at dropping {outboundFile} to SFTP failed, waiting 2 minutes and trying again.", UniversalLogger.LogCategory.WARNING);
                    System.Threading.Thread.Sleep(1000 * 60 * 2);
                    FtpIpSwitchPush(outboundFile, proclog, rename, progCodeOverride, overrideInTest);
                }
            }
            else
            {
                //Should be made into an error after we ditch Rhapsody FTP!
                proclog.WriteToLog(proclog.ProcessId + " was found to be a non-PHP job. Using Rhapsody for transfer.");
                string targetLoc = @"\\shs.org\dfs\";

                if (proclog.TestMode)
                {
                    targetLoc += @"IntBiDaToExtTest\";
                }
                else
                {
                    targetLoc += @"IntBiDaToExtProd\";
                }

                if (progCodeOverride == "")
                {
                    targetLoc += proclog.ProcessId;
                }
                else
                {
                    targetLoc += progCodeOverride;
                }

                targetLoc += "_";

                if (rename == "")
                {
                    targetLoc += System.IO.Path.GetFileName(outboundFile);
                }
                else
                {
                    targetLoc += rename;
                }

                targetLoc += "_GUID" + proclog.UniqueID;

                if (!System.IO.File.Exists(targetLoc))
                {
                    System.IO.File.Copy(outboundFile, targetLoc, false);
                }
                else
                {
                    System.Threading.Thread.Sleep(2 * 60 * 1000);
                    System.IO.File.Copy(outboundFile, targetLoc, false);
                }
            }
        }


        /// <summary>
        /// Drops files with the appropriate hand-off naming convention to Rhapsody
        /// </summary>
        /// <param name="proclog">Calling program</param>
        /// <param name="outboundFiles">list of files</param>
        /// <param name="dropOverride">If you  need to drop as a different program code, use this to specify</param>
        /// <param name="overrideInTest">If true, this will override the IT_TEST route and send it to tint.shs.org with the real job code</param>

        public static void DropToPhpDoorStep(Logger proclog, List<string> outboundFiles, string dropOverride = "", bool overrideInTest = false)
        {
            foreach (string s in outboundFiles)
            {
                proclog.WriteToLog("Delivering File: " + System.IO.Path.GetFileName(s));
            }

            if (IsPHPJob(proclog, dropOverride))
            {
                proclog.WriteToLog(proclog.ProcessId + " was found to be a PHP job. Using IPSwitch for transfer.");
                FtpIpSwitchPush(outboundFiles, proclog, dropOverride, overrideInTest);
            }
            else
            {
                //Should be made into an error after we ditch Rhapsody FTP!
                proclog.WriteToLog(proclog.ProcessId + " was found to be a non-PHP job. Using Rhapsody for transfer.");
                foreach (string file in outboundFiles)
                {
                    DropToPhpDoorStep(proclog, file, progCodeOverride: dropOverride, overrideInTest: overrideInTest);
                }
            }
        }

        public static void UpdateRhapsodyBatch(Logger procLog, int BatchCount)
        {
            string query = string.Format(@"UPDATE [dbo].[RhapsodyEmailOutputs_C]
                    SET [FilesPerBatch] = '{1}'
                    WHERE ProgramCode='{0}'", procLog.ProcessId, BatchCount);

            DataWork.RunSqlCommand(query, procLog.LoggerPhpConfig);
        }

        public static bool IsPHPJob(Logger proclog, string progCodeOverride)
        {
            bool isPHP = false;
            string jobName = progCodeOverride != "" ? progCodeOverride : proclog.ProcessId;

            JobConnectionInfo jobInfo = ExtractFactory.ConnectAndQuery<JobConnectionInfo>(proclog.LoggerPhpConfig, GetConnectionNameQuery(jobName)).FirstOrDefault();
            if (jobInfo != null)
            {
                isPHP = true;
            }
            else
            {
                proclog.WriteToLog("No record was found in the FTPJobIndexCrossWalk_C table for the Job ID " + proclog.ProcessId);
            }
            return isPHP;
        }

        private static string CreateConnectionString(JobConnectionInfo jobInfo)
        {
            string connString = "";

            KeyValuePair<string, string> creds = GetKeyPassCredentials(jobInfo.ConnectionName);
            connString = "USER " + creds.Key + Environment.NewLine;
            connString += "PASS " + creds.Value + Environment.NewLine;
            connString += "CONNECT " + jobInfo.SiteAddress + Environment.NewLine;

            return connString;
        }

        public static string GetKeyPassPassword(string profileName)
        {
            return GetElementFromKeyPass("Password", profileName);
        }

        public static KeyValuePair<string, string> GetKeyPassCredentials(string credProfileName)
        {
            string userName = GetElementFromKeyPass("UserName", credProfileName);
            string pass = GetElementFromKeyPass("Password", credProfileName);

            KeyValuePair<string, string> creds = new KeyValuePair<string, string>(userName, pass);

            return creds;
        }

        public static string GetElementFromKeyPass(string element, string credProfileName, bool acceptBlanks = false, bool retry = true)
        {
            //temp log that we're hitting KeePass
            UniversalLogger.WriteToLog(new Logger(new Logger.LaunchRequest("ADMIN", false, null)), "KeePass hit for element " + element + " credProfileName " + credProfileName);

            string keepassValue = "";
            string commandExecutionUser = string.Format(@"""{0}"" -c:GetEntryString ""{1}"" -pw:BravoIndiaDeltaAlfa -Field:{2} -ref-Title:{3}", ConfigurationManager.AppSettings["KeePassExe"], ConfigurationManager.AppSettings["KeePassData"], element, credProfileName);
            string commandLineTextUser = @"/c """ + commandExecutionUser + @"""";

            Process procUser = new Process { StartInfo = new ProcessStartInfo { FileName = "cmd.exe", Arguments = commandLineTextUser, UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true } };
            procUser.Start();

            while (!procUser.StandardOutput.EndOfStream)
            {
                keepassValue += procUser.StandardOutput.ReadLine() + Environment.NewLine;
            }

            string[] seps = new string[] { "\r\n" };
            string[] outputUser = keepassValue.Split(seps, StringSplitOptions.None);

            int completionMessageIndex = Array.IndexOf(outputUser, "OK: Operation completed successfully.");

            if (completionMessageIndex == 0 || outputUser.Length == 2) //ouput length of 2 lines = this credential set does not exist
            {
                throw new Exception(element + " not found for " + credProfileName + " in Keypass!");
            }
            else if (completionMessageIndex == -1 && !acceptBlanks)
            {
                if (retry == true)
                {
                    UniversalLogger.WriteToLog(new Logger(new Logger.LaunchRequest("ADMIN", false, null)), "'OK' message from KeePass not seen; retrying.", category: UniversalLogger.LogCategory.WARNING);
                    return GetElementFromKeyPass(element, credProfileName, false);
                }
                else
                {
                    throw new Exception("Error Pulling " + element + " from Keepass for " + credProfileName + Environment.NewLine + "Keepass CommandLine Response: " + keepassValue);
                }
            }

            return String.Join("\r\n", outputUser, 0, completionMessageIndex);

        }

        private static string GetConnectionNameQuery(string jobName)
        {
            return string.Format(@"SELECT 
                                   [ProgramCode]
                                  ,[ConnectionName]
                                  ,[SiteAddress]
                                  ,[ChangeDirectory]
                                  ,[UseSharedSite]
                                  ,[UseSFTPKeyMethod]
                              FROM [PHPConfg].[dbo].[FTPJobIndexCrossWalk_C]
                              WHERE ProgramCode = '{0}'", jobName);
        }

        public static void FtpIpSwitchPush(string pushFile, Logger proclog, string rename = "", string progCodeOverride = "", bool overrideInTest = false)
        {
            List<string> files = new List<string>();
            //string path = Path.GetDirectoryName(pushFile);
            if (rename != "")
            {
                proclog.LoggerReportYearDir(proclog.LoggerWorkDir + @"rename\");
                string renamedFile = proclog.LoggerWorkDir + @"rename\" + rename;
                File.Copy(pushFile, renamedFile);
                files.Add(renamedFile);
                FtpIpSwitchPush(files, proclog, progCodeOverride, overrideInTest);
                File.Delete(renamedFile);
            }
            else
            {
                files.Add(pushFile);
                FtpIpSwitchPush(files, proclog, progCodeOverride, overrideInTest);
            }
        }

        public static void FtpIpSwitchPush(List<string> pushFile, Logger proclog, string progCodeOverride = "", bool overrideInTest = false, bool securityStepdown = false)
        {
            string workDir = proclog.LoggerWorkDir;
            string jobName = proclog.ProcessId;
            if (progCodeOverride != "")
            {
                jobName = progCodeOverride;
            }

            #pragma warning disable CS0618 // We do not want to clutter the log with configuration lookups from libraries
            JobConnectionInfo jobInfo = ExtractFactory.ConnectAndQuery<JobConnectionInfo>(proclog.LoggerPhpConfig, GetConnectionNameQuery(jobName)).FirstOrDefault();
            #pragma warning restore CS0618

            if (proclog.TestMode && overrideInTest == false)
            {
                proclog.WriteToLog("This is where we would have shipped the file... IF WE WERE IN PROD!");
                return;
            }

            if (jobInfo.UseSFTPKeyMethod == true)
            {
                FtpFactory.SSHDropFile(jobInfo.ConnectionName, pushFile.ToArray(), jobInfo.ChangeDirectory);
                return;
            }

            string rawScriptFile = workDir + @"\put{0}.scp";
            string scriptFile = "";
            int i = 0;
            try
            {
                if (System.IO.File.Exists(string.Format(rawScriptFile, "")))
                {
                    System.IO.File.Delete(string.Format(rawScriptFile, ""));
                }
                scriptFile = string.Format(rawScriptFile, "");
            }
            catch (Exception E)
            {
                proclog.WriteToLog("Unable to reach file: " + string.Format(rawScriptFile, ""));
                bool success = false;
                do
                {
                    try
                    {
                        if (System.IO.File.Exists(string.Format(rawScriptFile, i.ToString())))
                        {
                            System.IO.File.Delete(string.Format(rawScriptFile, i.ToString()));
                        }
                        proclog.WriteToLog("Using script file: " + string.Format(rawScriptFile, i.ToString()));
                        success = true;
                        scriptFile = string.Format(rawScriptFile, i.ToString());
                    }
                    catch
                    {
                        i++;
                    }
                } while (!success);
            }

            string putCommand = "";
            foreach (string s in pushFile)
            {
                if (!System.IO.File.Exists(s))
                {
                    throw new Exception("Input file not found");
                }

                string renameWrapping = "";

                putCommand += @"put """ + s + @""" " + renameWrapping + Environment.NewLine;
            }

            FileStream putScript = new FileStream(scriptFile, FileMode.Create);
            StreamWriter sw = new StreamWriter(putScript);

            string cdFolder = jobInfo.ChangeDirectory == "" ? "" : string.Format(@"cd ""{0}""", jobInfo.ChangeDirectory);

            string connect = "";
            string changeDir = string.Format("{0} " + Environment.NewLine, cdFolder);
            string closeLine = "close";
            string acceptCert = "";

            if (jobInfo.UseSharedSite)
            {
                connect = "CONNECT SharedSites!" + jobInfo.ConnectionName + Environment.NewLine;
            }
            else
            {
                string securityType = "SSH";
                if (securityStepdown)
                {
                    securityType = "SFTP";
                }
                acceptCert = "AUTH " + securityType + " 2" + Environment.NewLine;
                connect = CreateConnectionString(jobInfo);
            }

            string fullLine = acceptCert + connect + changeDir + putCommand + closeLine;

            sw.WriteLine(fullLine);
            sw.Flush();
            sw.Close();

            string commandExecution = string.Format(@"""C:\Program Files (x86)\Ipswitch\WS_FTP 12\ftpscrpt"" -f ""{0}""", scriptFile);
            string commandLineText = @"/c """ + commandExecution + @"""";

            Process proc = new Process { StartInfo = new ProcessStartInfo { FileName = "cmd.exe", Arguments = commandLineText, UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true } };
            proc.Start();
            string response = "";

            while (!proc.StandardOutput.EndOfStream)
            {
                response += proc.StandardOutput.ReadLine() + Environment.NewLine;
            }

            proclog.WriteToLog("FTP Response: " + IpswitchObfuscationOMatic(response));
            System.IO.File.Delete(scriptFile);

            if (response.ToUpper().Contains("FAILURE IN COMMAND") || response == "")
            {
                if (securityStepdown)
                {
                    throw new Exception("FTP push failed, check the process logs");
                }
                else
                {
                    proclog.WriteToLog("Could not establish an SSH connection. Step-down security enacted.", UniversalLogger.LogCategory.WARNING);
                    FtpIpSwitchPush(pushFile, proclog, progCodeOverride, overrideInTest, true);
                }
            }
            else if (response.ToUpper().Contains("FAILED TO LOAD THE SCRIPT"))
            {
                throw new Exception("FTP push failed to load the script, check process logs");
            }

        }

        public class JobConnectionInfo
        {
            public string ProgramCode { get; set; }
            public string ConnectionName { get; set; }
            public string SiteAddress { get; set; }
            public string ChangeDirectory { get; set; }
            public bool UseSharedSite { get; set; }
            public bool UseSFTPKeyMethod { get; set; }
        }

        /// <summary>
        /// Please delete this once we stop using IPSwitch for ftp!!!
        /// </summary>
        public static string IpswitchObfuscationOMatic(string response)
        {
            //Replacing all instances of the username/password with asterisks so we aren't storing them in plain text in sql
            Match userMatch = Regex.Match(response, @"\[USER [^\s]*");
            if (userMatch.Success)
            {
                string userName = userMatch.Value.Substring(6, userMatch.Value.Length - 7);
                response = response.Replace(userName, new string('*', userName.Length));
            }

            Match passMatch = Regex.Match(response, @"\[PASS [^\s]*");
            if (passMatch.Success)
            {
                string password = passMatch.Value.Substring(6, passMatch.Value.Length - 7);
                response = response.Replace(password, new string('*', password.Length));
            }

            return response;
        }
    }
}
