using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using SevenZip;


namespace Utilities.Zip
{
    public class UMichZippers : IUMichZippers
    {
        /// <summary>
        /// Job executing the zipping functionality
        /// </summary>
        public Logger Job { get; set; }
        /// <summary>
        /// Encryption method used when passwording an archive. Default method is AES 256
        /// </summary>
        public ZipEncryptionMethod EncryptionMethod { get; set; } = ZipEncryptionMethod.Aes256;
        /// <summary>
        /// Constructor for UMichZippers class
        /// </summary>
        /// <param name="job">Job executing the zipping functionality</param>
        public UMichZippers(Logger job)
        {
            SevenZipBase.SetLibraryPath(@"7z.dll");
            Job = job;
        }
        /// <summary>
        /// Method to compress a file or folder of files into a single zip archive
        /// </summary>
        /// <param name="fileOrFolder">Full path to an individual file or a folder of files to be archived</param>
        /// <param name="outputFile">Path and file name for zip archive to be created</param>
        /// <param name="password">Optional parameter to password protect the archive file</param>
        public void Zip(string fileOrFolder, string outputFile, string password = null)
        {
            IEnumerable<string> files = GetFiles(fileOrFolder);
            Zip(files, outputFile, password);
        }
        /// <summary>
        /// Method to compress a file or folder of files into a single zip archive
        /// </summary>
        /// <param name="files">Collection of file names with full paths to be archived</param>
        /// <param name="outputFile">Path and file name for zip archive to be created</param>
        /// <param name="password">Optional parameter to password protect the archive file</param>
        public void Zip(IEnumerable<string> files, string outputFile, string password = null)
        {
            Job.WriteToLog("Zipping operation beginning");
            SevenZipCompressor compressor = new SevenZipCompressor();

            compressor.CompressionLevel = CompressionLevel.Normal;
            compressor.ArchiveFormat = OutArchiveFormat.Zip;

            Job.WriteToLog("Compressing " + files.Count() + " files. Compression level:" + CompressionLevel.Normal.ToString() + " Archive Format: " + OutArchiveFormat.Zip + "Passworded: " + (password == null ? "No" : "Yes") + " Encryption Method: " + EncryptionMethod);

            try
            {
                if (string.IsNullOrEmpty(password))
                {
                    compressor.CompressFiles(outputFile, files.ToArray());
                    Job.WriteToLog(outputFile + " successfully created with no password.");
                }
                else
                {
                    compressor.ZipEncryptionMethod = EncryptionMethod;
                    compressor.CompressFilesEncrypted(outputFile, password, files.ToArray());
                    Job.WriteToLog(outputFile + " successfully created with password.");
                }
            }
            catch (Exception ex)
            {
                Job.WriteToLog(ex.ToString(), UniversalLogger.LogCategory.ERROR);
            }
        }
        /// <summary>
        /// Method to decompress a single zip archive or a folder containing multiple zip archives to a given target folder
        /// </summary>
        /// <param name="fileOrFolder">Path and file name to a zip archive, or to a folder containing multiple archives to be unzipped</param>
        /// <param name="target">Folder to output unzipped files</param>
        /// <param name="password">Optional parameter to unzip passworded archives</param>
        public void UnZip(string fileOrFolder, string target, string password = null)
        {
            IEnumerable<string> files = GetFiles(fileOrFolder);
            UnZip(files, target, password);
        }
        /// <summary>
        /// Method to decompress a single zip archive or a folder containing multiple zip archives to a given target folder
        /// </summary>
        /// <param name="files">Collection of file names with full paths to be unzipped</param>
        /// <param name="target">Folder to output unzipped files</param>
        /// <param name="password">Optional parameter to unzip passworded archives</param>
        public void UnZip(IEnumerable<string> files, string target, string password = null)
        {
            Job.WriteToLog("Unzipping operation beginning");
            foreach (string file in files)
            {
                Job.WriteToLog("Extracting " + file);
                using (SevenZipExtractor extractor = new SevenZipExtractor(file, password))
                {
                    extractor.ExtractArchive(target);
                }
            }
        }

        private IEnumerable<string> GetFiles(string fileOrFolder)
        {
            Job.WriteToLog("Searching for file, or in folder:" + fileOrFolder);
            List<string> files = new List<string>();
            try
            {
                if (File.Exists(fileOrFolder))
                {
                    files.Add(fileOrFolder);
                    Job.WriteToLog(fileOrFolder + " found. Adding to archive.");
                }
                else
                {
                    files = Directory.GetFiles(fileOrFolder).ToList();
                    Job.WriteToLog(files.Count + " files found in " + fileOrFolder + ". Adding to archive.");
                }

            }
            catch (IOException ex)
            {
                Job.WriteToLog(ex.Message, UniversalLogger.LogCategory.ERROR);
            }

            return files;
        }

    }
}
