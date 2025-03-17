using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

namespace Utilities
{
    public class FileSystem
    {
        /// <summary>
        /// Creates the given directory if it doesn't exist
        /// </summary>
        /// <param name="Create">Path to the directory to create</param>
        public static void ReportYearDir(String Create)
        {
            try
            {
                if (!System.IO.Directory.Exists(Create))
                {
                    System.IO.Directory.CreateDirectory(Create);
                }
            }
            catch
            {//In case access was denied / system glitch, try again before failing
                System.Threading.Thread.Sleep(1000 * 60 * 1);
                if (!System.IO.Directory.Exists(Create))
                {
                    System.IO.Directory.CreateDirectory(Create);
                }
            }
        }

        /// <summary>
        /// Creates the given directory if it doesn't exist. With job index passed in, it will also set folder permissions.
        /// </summary>
        /// <param name="create">Path to the directory to create</param>
        /// <param name="logger">Logger object for ID and error logging</param>
        public static void ReportYearDir(string Create, Logger logger)
        {
            if (!System.IO.Directory.Exists(Create))
            {
                System.IO.Directory.CreateDirectory(Create);
                FolderPermissions.SetFolderPermissions(logger, Create, setOnBaseJobFolder: true);
            }
        }

        /// <summary>
        /// Copies a file from the given path to the specified path. If the destination directory does not exist, it is created.
        /// </summary>
        /// <param name="sourceFileName">Path to the existing file/directory.</param>
        /// <param name="destFileName">Path to the directory to copy the file to.</param>
        /// <param name="overwrite"></param>
        public static void CopyToDir(String sourceFileName, String destFileName, bool overwrite = false)
        {
            String dirToCreate = destFileName.Substring(0, destFileName.LastIndexOf("\\"));

            System.IO.Directory.CreateDirectory(dirToCreate);

            System.IO.File.Copy(sourceFileName, destFileName, overwrite);
        }

        /// <summary>
        /// Deletes all files in a given path, use this to provide re-runability
        /// </summary>
        /// <param name="path">Folder to clear of files</param>
        public static void ClearDirectoryOfFiles(String path)
        {
            FileSystem.ReportYearDir(path);

            List<String> oldFiles = System.IO.Directory.GetFiles(path).ToList();

            foreach (String file in oldFiles)
            {
                System.IO.File.Delete(file);
            }
        }

        /// <summary>
        /// Searches a given FTP site for incoming files according to a given, downloads those files to the staging directory, then pulls in all files from
        /// that staging directory. This gives us a back-door to inserting files into the processor.
        /// </summary>
        /// <param name="proclog">Program being processed (this)</param>
        /// <param name="ftpSite">FTP site to search and get files from (note, this follows our rules for pinging the test site)</param>
        /// <param name="file">The keyword for the file to pull from the FTP site</param>
        /// <returns></returns>
        public static List<String> PullInFiles(Logger proclog, String ftpSite, String file, String password = "")
        {
            List<String> foundFiles = new List<string>();

            ReportYearDir(proclog.LoggerStagingDir, proclog);

            FileTransfer.FtpIpSwitchSearchAndGet(proclog.LoggerWorkDir, proclog.LoggerStagingDir, ftpSite, file, proclog, out foundFiles);

            if (password != "")
            {
                foreach (String s in foundFiles)
                {
                    //Zippers.UnZip(s, proclog.LoggerStagingDir, proclog, password);
                }
            }

            foreach (String s in System.IO.Directory.GetFiles(proclog.LoggerStagingDir))
            {
                foundFiles.Add(s);
            }

            return foundFiles;
        }

        /// <summary>
        /// Leverages the configuration file to wait a given amount of time before trying to create the given folder or copy the given file
        /// </summary>
        /// <param name="process">Calling process (this)</param>
        /// <param name="ioEx">The input/output exception that caused the call</param>
        /// <param name="SourceDirOrFile">If a directory, will be created after the wait time, if a file will be copied to the given location after the wait</param>
        /// <param name="copyTarget">If the SourceDirOrFile is a file, this is the location we'll copy to after the wait time</param>
        public static void TryRecoverFromNetwork(Logger process, IOException ioEx, String SourceDirOrFile, String copyTarget = "")
        {

            //we're doing this because of random connectivity issues to sharepoint
            process.WriteToLog(ioEx.ToString());

            int waitTime = 3600000;
            process.WriteToLog(String.Format("There was a problem on the initial copy, waiting {0} milliseconds...", waitTime)); //we're doing this because of random connectivity issues to sharepoint
            System.Threading.Thread.Sleep(waitTime);

            FileSystem.ReportYearDir(System.IO.Path.GetDirectoryName(SourceDirOrFile));

            FileAttributes fileAtr = File.GetAttributes(SourceDirOrFile);

            if (!fileAtr.HasFlag(FileAttributes.Directory))
            {
                if (copyTarget == "")
                {
                    throw new Exception("Provided a file, but didn't give us a copy target for the retry");
                }
                System.IO.File.Copy(SourceDirOrFile, copyTarget, true);
            }
            process.WriteToLog("Second time worked");
        }

        /// <summary>
        /// Leverages the FTP utility to download files matcing the give naming convention, then loads all files in the download location (the staging directory), this allows 2 access points for processing
        /// </summary>
        /// <param name="caller">The calling program</param>
        /// <param name="ftpSite">Ftp site to download from</param>
        /// <param name="name">Look for files that contain this string in their name</param>
        /// <param name="changeDir">Change the directory of the source location</param>
        /// <param name="deleteAfterDownload">Opional Bool if deleted after</param>
        /// <param name="MultipleThreads">If you would like to apply mulithreading to the download of the files</param>
        /// <param name="unzip">if the files should be unzipped after download</param>
        /// <param name="overrideSite">Will override the normal handling to not look for files over FTP when running in test. Note, this parameter should only be used in unique circumstances as our standard is to not hook up our test environment to vendor/other FTP servers</param>
        /// <returns>List of the files that were found (including their local path)</returns>
        public static List<String> GetInputFiles(Logger caller, String ftpSite, String name, String changeDir = "", Boolean deleteAfterDownload = false, Boolean unzip = false, Boolean MultipleThreads = false, Boolean overrideSite = false, String unzipPassword = "")
        {
            List<String> gotFiles;

            String stagingDirectory = caller.LoggerStagingDir;
            ReportYearDir(stagingDirectory);

            if (ftpSite != null && (!caller.TestMode || overrideSite)) //Only look to FTPs if we were given an FTP site and its either production or the overridesite parameter has been used
            {


                try
                {
                    FileTransfer.FtpIpSwitchSearchAndGet(caller.LoggerWorkDir, stagingDirectory, ftpSite, name, caller, out gotFiles, changeDir, deleteAfterDownload, multiThread: MultipleThreads, OverrideSite: overrideSite);
                    caller.WriteToLog("Downloaded " + gotFiles.Count + " from the " + ftpSite + "FTP Site");
                }
                catch (Exception exc)
                {

                    if (caller.ProcessId == "IT_0354" || caller.ProcessId == "IT_0233") // these are unique in that they pull from multiple ftp sites, so we don't want a break in one to kill the whole thing
                    {
                        caller.WriteToLog("Errored Connecting to " + ftpSite + Environment.NewLine + exc.ToString());
                        caller.OnError(exc);
                    }
                    else
                    {
                        throw exc;
                    }


                }
            }

            caller.WriteToLog("Now pulling all files from the staging area");

            String searchPattern = (name.Contains("*") ? "*.*" : "*" + name + "*");
            List<String> stagedFiles = System.IO.Directory.GetFiles(stagingDirectory, searchPattern).ToList(); //we're going to pull everything from stage that way users can place files here and still get them picked up

            if (unzip)
            {
                String UnzipDir = caller.LoggerWorkDir + @"\Unzip";

                FileSystem.ReportYearDir(UnzipDir);
                FileSystem.ClearDirectoryOfFiles(UnzipDir);

                foreach (String zippedFile in stagedFiles)
                {
                    //Zippers.UnZip(zippedFile, UnzipDir, caller, unzipPassword);

                    FtpFactory.ArchiveFile(caller, zippedFile);

                }
                return System.IO.Directory.GetFiles(UnzipDir).ToList();

            }
            else
            {
                return stagedFiles;
            }


        }

        /// <summary>
        /// Allows for execution of external programs and watches for default error returns as well as any specified via the optional watchWords value
        /// </summary>
        /// <param name="fileAndPath">Executable and path being called</param>
        /// <param name="proclog">Standard Process Log</param>
        /// <param name="execArgs">Arguments passed along to the executable</param>
        /// <param name="watchWords">Optional value containing error words to watch for to indicate a failure on the part of the called program</param>
        public static int ExternalExecutor(String fileAndPath, Logger proclog, String execArgs, List<String> watchWords = null)
        {
            fileAndPath = fileAndPath.ToUpper();

            //If the executable being called is cmd.exe, include "/C" to close command window after completion
            if (fileAndPath.Contains("CMD.EXE"))
            {
                if (!execArgs.Contains(@"/C"))
                {
                    execArgs = execArgs.Trim();
                    execArgs = @"/C " + execArgs;
                }
            }

            //Run external command with given parameters
            var proc = new Process { StartInfo = new ProcessStartInfo { FileName = fileAndPath, Arguments = execArgs, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = false } };


            proc.Start();
            string output = proc.StandardOutput.ReadToEnd();
            string error = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            string totalOutput = output + error;
            proclog.WriteToLog(totalOutput);

            if (watchWords != null)
            {
                if (!String.IsNullOrEmpty(totalOutput))
                {
                    foreach (String item in watchWords)
                    {
                        if (totalOutput.ToUpper().Contains(item.ToUpper()))
                        {
                            //Kick out exception and write error logs
                            throw new Exception(@"One or more watchwords were found in the output.  '" + item + "'   Check process logs for more details.\r\n Commandline used: " + fileAndPath + " " + execArgs);
                        }
                    }
                }
            }

            return proc.ExitCode;
        }

        public static Boolean IsDirectory(String path)
        {
            FileAttributes downloadToAttrib = System.IO.File.GetAttributes(path);

            return downloadToAttrib.HasFlag(FileAttributes.Directory);
        }


        /// <summary>
        /// Get the hightest file number from files that have been created in the past, and increment it by one. 
        /// If no files exist in the current year's folder, it will attempt to find a folder for the prior year, and search that.
        /// </summary>
        /// <param name="caller">Logger class calling into method</param>
        /// <param name="fileType">File extention to look for in search location (ex. "zip")</param>
        /// <param name="startIndex">Index to begin getting number from file names</param>
        /// <param name="length">Length of the number (for example if you have leading zeroes 0001, the length would be 4)</param>
        public static int GetNextFileNumber(Logger caller, string fileType, int startIndex, int length)
        {
            string searchLocation = caller.LoggerOutputYearDir;
            int fileNumber = 1;
            List<string> fileList;
            int lastYear = Convert.ToInt16(searchLocation.Substring(searchLocation.Length - 5, 4)) - 1;
            string lastYearsPath = searchLocation.Substring(0, searchLocation.Length - 5) + lastYear.ToString() + @"\";

            try
            {
                fileList = Directory.GetFiles(searchLocation, "*." + fileType).ToList();
            }
            catch (DirectoryNotFoundException dNotFound)
            {
                try
                {
                    fileList = Directory.GetFiles(lastYearsPath, "*." + fileType).ToList();
                }
                catch (DirectoryNotFoundException lastDNotFound)
                {
                    return fileNumber;
                }
            }

            if (fileList.Count > 0)
            {
                fileNumber = GetLastFileNumber(fileList, startIndex, length);
                fileNumber++;
                return fileNumber;
            }
            else
            {
                try
                {
                    fileList = Directory.GetFiles(lastYearsPath, "*." + fileType).ToList();
                }
                catch (DirectoryNotFoundException lastDNotFound)
                {
                    return fileNumber;
                }
                if (fileList.Count > 0)
                {
                    fileNumber = GetLastFileNumber(fileList, startIndex, length);
                    fileNumber++;
                    return fileNumber;
                }
                return fileNumber;
            }
        }

        public static int GetNextFileNumber(Logger caller, string fileType, string delimiter, int numberIndex)
        {
            string searchLocation = caller.LoggerOutputYearDir;
            int fileNumber = 1;
            List<string> fileList;
            int lastYear = Convert.ToInt16(searchLocation.Substring(searchLocation.Length - 5, 4)) - 1;
            string lastYearsPath = searchLocation.Substring(0, searchLocation.Length - 5) + lastYear.ToString() + @"\";

            try
            {
                fileList = Directory.GetFiles(searchLocation, "*." + fileType).ToList();
            }
            catch (DirectoryNotFoundException dNotFound)
            {
                try
                {
                    fileList = Directory.GetFiles(lastYearsPath, "*." + fileType).ToList();
                }
                catch (DirectoryNotFoundException lastDNotFound)
                {
                    return fileNumber;
                }
            }

            if (fileList.Count > 0)
            {

                fileNumber = GetLastFileNumber(fileList, delimiter, numberIndex);
                fileNumber++;
                return fileNumber;
            }
            else
            {
                try
                {
                    fileList = Directory.GetFiles(lastYearsPath, "*." + fileType).ToList();
                }
                catch (DirectoryNotFoundException lastDNotFound)
                {
                    return fileNumber;
                }
                if (fileList.Count > 0)
                {
                    fileNumber = GetLastFileNumber(fileList, delimiter, numberIndex);
                    fileNumber++;
                    return fileNumber;
                }
                return fileNumber;
            }
        }

        private static int GetLastFileNumber(List<string> fileList, int startIndex, int length)
        {
            fileList.Sort();
            string fileName = Path.GetFileNameWithoutExtension(fileList.Last());
            int fileNumber = Convert.ToInt16(fileName.Substring(startIndex, length));
            return fileNumber;
        }

        private static int GetLastFileNumber(List<string> fileList, string delimiter, int numberIndex)
        {
            fileList.Sort();
            string fileName = Path.GetFileNameWithoutExtension(fileList.Last());
            string[] chopped = fileName.Split(Convert.ToChar(delimiter));
            return Convert.ToInt16(chopped[numberIndex]);
        }
    }
}
