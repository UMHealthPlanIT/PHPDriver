using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using System.IO.Compression;

namespace Utilities
{
    public static class FileInfoTracking
    {
        /// <summary>
        /// This method will compare the size of the file that is passed in to the history recorded in the ULogger. There are optional parameters to specify the amount of
        /// variance desired and the number of history records to check. Returns true if size is comparable or if there are no existing log entries.
        /// </summary>
        /// <param name="procLog">A class that extends the Logger class (all jobs do this, so pass itself).</param>
        /// <param name="filePath">The path of the file to compare.</param>
        /// <param name="fileIdentifier">A unique name to use for finding the file stats in the logs.</param>
        /// <param name="allowedVariance">The amount of variance +/- to use for the comparison. This is a decimal representation of a percentage.</param>
        /// <param name="numFilesToCheck">The number of past records to check in the logs.</param>
        public static bool CompareFileSize(Logger procLog, string filePath, string fileIdentifier, double allowedVariance = 0.1, int numFilesToCheck = 5)
        {
            bool fileComparable;
            long fileSize = GetFileSize(procLog, filePath);
            long sum = 0;
            double average = 0;
            string jobIndex = procLog.ProcessId.Substring(0, Math.Min(10, procLog.ProcessId.Length));

            if (fileSize < 0)
            {
                // file could not be found or didn't have permission to directory
                return false;
            }

            List<string> logEntries = ExtractFactory.ConnectAndQuery<string>(procLog.LoggerPhpConfig, "select top " + numFilesToCheck + " LogContent from PHPConfg.ULOGGER.LoggerRecord with (nolock) where JobIndex = '" + jobIndex + "' and LogCategory = 'AUDIT' and LogContent like '(File Size)%" + fileIdentifier + "%' order by LogDateTime desc").ToList();

            if (logEntries.Count() == 0)
            {
                // this is the first file size log entry
                return true;
            }

            foreach (string entry in logEntries)
            {
                sum += Convert.ToInt64(Regex.Replace(entry, @"[\D+]", ""));
            }

            if (logEntries.Count() < numFilesToCheck)
            {
                UniversalLogger.WriteToLog(procLog, "(File Size) " + fileIdentifier + " doesn't have a large enough history for a proper comparison.", category: UniversalLogger.LogCategory.INFO);
                average = (double)sum / logEntries.Count();
            }
            else
            {
                average = (double)sum / numFilesToCheck;
            }

            // make sure fileSize is within the allowedVariance of the average
            fileComparable = fileSize > (average * (1 - allowedVariance)) && fileSize < (average * (1 + allowedVariance));
            double sizeComparison = fileSize / average;

            if (fileComparable)
            {
                UniversalLogger.WriteToLog(procLog, "(File Size) The size of " + fileIdentifier + " is within the allowed variance. It is " + (sizeComparison * 100).ToString("F2") + "% of the average of the last " + numFilesToCheck + " files", category: UniversalLogger.LogCategory.INFO);
            }
            else
            {
                UniversalLogger.WriteToLog(procLog, "(File Size) The size of " + fileIdentifier + " is outside of the allowed variance. It is " + (sizeComparison * 100).ToString("F2") + "% of the average of the last " + numFilesToCheck + " files", category: UniversalLogger.LogCategory.WARNING);
            }

            return fileComparable;
        }

        /// <summary>
        /// This method logs the file size.
        /// </summary>
        /// <param name="procLog">A class that extends the Logger class (all jobs do this, so pass itself).</param>
        /// <param name="filePath">The path of the file to compare.</param>
        /// <param name="fileIdentifier">A unique name to use for finding the file stats in the logs.</param>
        public static void LogFileSize(Logger procLog, string filePath, string fileIdentifier)
        {
            long fileSize = GetFileSize(procLog, filePath);
            UniversalLogger.WriteToLog(procLog, "(File Size) " + fileIdentifier + " size in bytes is: " + fileSize, category: UniversalLogger.LogCategory.AUDIT);
        }

        private static long GetFileSize(Logger procLog, string filePath)
        {
            long fileSize = -1;

            if (!Directory.Exists(Path.GetDirectoryName(filePath)))
            {
                UniversalLogger.WriteToLog(procLog, "(File Size) Could not access file location at " + filePath + ".", category: UniversalLogger.LogCategory.WARNING);
            }
            else if (File.Exists(filePath))
            {
                fileSize = new FileInfo(filePath).Length;

            }
            else if (File.Exists(filePath.Substring(0, filePath.Length - 4) + ".zip"))
            {
                // this means the file was zipped, so get the length of zipped file
                ZipArchiveEntry zippedFile = ZipFile.OpenRead(filePath.Substring(0, filePath.Length - 4) + ".zip").Entries[0];

                fileSize = zippedFile.Length;
            } else
            {
                UniversalLogger.WriteToLog(procLog, "(File Size) " + filePath + " file does not exist.", category: UniversalLogger.LogCategory.WARNING);
            }

            return fileSize;
        }
    }
}