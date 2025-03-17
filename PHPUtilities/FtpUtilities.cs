using Utilities.FTP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utilities
{
    public class FtpUtilities
    {
        /// <summary>
        /// Picks up a file from an FTP site and places it on Sharepoint
        /// </summary>
        /// <param name="proclog">Calling program (this)</param>
        /// <param name="filenameContains">Filename search criteria</param>
        /// <param name="documentLibrary">Sharepoint library to write file to</param>
        /// <param name="sharepointSite">Sharepoint site to push to</param>
        /// <param name="changeDir">Change directory on the source FTP site</param>
        /// <param name="deleteAfterDownload">If the file should be removed from the FTP site after we download it</param>
        /// <param name="FileRenameDel">A function we can use to rename the file when pushing to Sharepoint</param>
        /// <param name="localOnlyOverride">Skips downloading files and only returns local files</param>
        public static void FtpPullAndPublish(Logger proclog, string filenameContains, string documentLibrary, string sharepointSite = "ITReports", string changeDir = "", bool deleteAfterDownload = false, bool localOnlyOverride = false)
        {
            UMichFTP ftp = new UMichFTP(proclog);

            if (!string.IsNullOrWhiteSpace(changeDir))
            {
                ftp.ConnInfo.ChangeDirectory = changeDir;
            }

            IEnumerable<string> foundFiles = new List<string>();

            if (!localOnlyOverride)
            {
                ftp.Download(filenameContains, proclog.LoggerStagingDir, deleteAfterDownload: deleteAfterDownload);
            }
            foundFiles = Directory.GetFiles(proclog.LoggerStagingDir, $"*{filenameContains}*");

            string sharePointLibraryUrl = "";
            foreach (string file in foundFiles)
            {
                string renameAtSharePoint = "";
                //if (FileRenameDel != null)
                //{
                //    renameAtSharePoint = FileRenameDel(file);
                //}
                sharePointLibraryUrl = FileTransfer.PushToSharepoint(sharepointSite, documentLibrary, file, proclog, rename: renameAtSharePoint);

                FtpFactory.ArchiveFile(proclog, file, addDateStamp: true);
            }

        }

        /// <summary>
        /// Ships a set of files to an FTP site and then pushes the files to Sharepoint
        /// </summary>
        /// <param name="proclog">Calling program (this)</param>
        /// <param name="filesToPush">An enumerable of files to send</param>
        /// <param name="documentLibrary">Sharepoint library to write the files to</param>
        /// <param name="sharepointSite">Sharepoint site to push to</param>
        /// <param name="changeDir">Change directory on the destination FTP site</param>
        /// <param name="FileRenameDel">A function we can use to rename the file when pushing to Sharepoint</param>
        public static void FtpPushAndPublish(Logger proclog, IEnumerable<string> filesToPush, string documentLibrary, string sharepointSite = "ITReports", string changeDir = "")
        {
            UMichFTP ftp = new UMichFTP(proclog);

            if (!string.IsNullOrWhiteSpace(changeDir))
            {
                ftp.ConnInfo.ChangeDirectory = changeDir;
            }

            foreach (string file in filesToPush)
            {
                ftp.Upload(file);

                string renameAtSharePoint = "";
                //if (FileRenameDel != null)
                //{
                //    renameAtSharePoint = FileRenameDel(file);
                //}
                string sharePointLibraryUrl = FileTransfer.PushToSharepoint(sharepointSite, documentLibrary, file, proclog, rename: renameAtSharePoint);

                FtpFactory.ArchiveFile(proclog, file, addDateStamp: true);
            }
        }

        /// <summary>
        /// Ships a file to an FTP site and then pushes the file to Sharepoint
        /// </summary>
        /// <param name="proclog">Calling program (this)</param>
        /// <param name="fileToPush">The file to send</param>
        /// <param name="documentLibrary">Sharepoint library to write the files to</param>
        /// <param name="sharepointSite">Sharepoint site to push to</param>
        /// <param name="changeDir">Change directory on the destination FTP site</param>
        /// <param name="FileRenameDel">A function we can use to rename the file when pushing to Sharepoint</param>
        public static void FtpPushAndPublish(Logger proclog, string fileToPush, string documentLibrary, string sharepointSite = "ITReports", string changeDir = "")
        {
            List<string> files = new List<string>() { fileToPush };

            FtpPushAndPublish(proclog, files, documentLibrary, sharepointSite, changeDir);
        }
    }
}
