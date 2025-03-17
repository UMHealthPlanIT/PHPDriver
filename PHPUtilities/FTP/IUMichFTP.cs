using System;
using System.Collections.Generic;


namespace Utilities.FTP
{
    public interface IUMichFTP
    {
        void Download(string searchString, string destination, bool deleteAfterDownload = false);
        void Download(IEnumerable<string> fileList, string destination, bool deleteAfterDownload = false);

        void DownloadRecoverable(string fileOrFolder, string destination, string stagingPath, bool recover = true, bool deleteAfterDownload = false, int maxThreads = 5);
        void DownloadRecoverable(IEnumerable<string> fileList, string destination, string stagingPath, bool recover = true, bool deleteAfterDownload = false, int maxThreads = 5);

        bool Upload(string searchString);
        bool Upload(IEnumerable<string> fileList);

        bool Delete(string searchString);
        bool Delete(IEnumerable<string> fileList);

        IEnumerable<string> Search(string directory, string searchString);
    }
}
