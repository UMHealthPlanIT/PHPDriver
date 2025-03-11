using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Utilities;
using System.Threading.Tasks;
using Renci.SshNet;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;

namespace Driver
{
    class DA03248LogShipping : Logger, IPhp
    {
        public DA03248LogShipping(LaunchRequest Program) : base(Program)
        {
            return;
        }

        private string source;
        private void SendPage()
        {
            if (!TestMode && source.ToUpper() == "PHP_Staging_PROD".ToUpper())
            {
                string query = @"SELECT Number FROM [dbo].[DeveloperOnCall_C]
                                    WHERE (GETDATE() NOT BETWEEN InactiveStart AND InactiveStop 
										OR InactiveStart IS NULL)
                                    AND Active = 1";
                List<string> numbers = ExtractFactory.ConnectAndQuery<string>(this, Data.AppNames.ExampleProd, query).ToList();

                List<SMS.TwilioResponse> resp = SMS.SendShsSms(this, "Error in DA03248 Staging Refresh, See Logs", numbers, SMS.SHSNumbers._0);
                foreach (SMS.TwilioResponse r in resp)
                {
                    WriteToLog($"Paged {r.to}, at {r.date_created}, last known status: {r.status}. Error code: {r.error_code ?? "null"}, error message: {r.error_message ?? "null"}");
                }
            }
        }

        public bool Initialize(string[] args)
        {
            bool recovery = false;
            bool FullDownload = false;
            bool testOverride = false;
            bool ignoreTrn = false;
            bool ignoreBak = false;

            // Skips the downloads and uses the files present in the local drive to kick off the ingest. Useful if an ingest error occured that wasn't due to bad files.
            bool skipDownloads = args.Any(x => string.Equals("SKIPDOWNLOAD", x, StringComparison.OrdinalIgnoreCase));

            if (args.Any(x => x.ToUpper().Contains("R")))
            {
                recovery = true;//Will pickup the download where it left off. Probably only don't want this if we come across corruption in the download
            }
            if (args.Any(x => x.ToUpper().Contains("")))
            {
                FullDownload = true;//Forces job to wait for a full backup file to be available
            }

            if (args.Any(x => x.ToUpper().Contains("RUNTEST")))
            {
                testOverride = true;//Allows the download/ingest to actually happen in test mode. Will not delete files still
            }
            if (args.Any(x => x.ToUpper().Contains("IGNORETRN")))
            {
                ignoreTrn = true;//Will proceed with download without detecting any TRN files - only use if we only want to pull down a full backup but not change files for whatever reason
            }
            if (args.Any(x => x.ToUpper().Contains("IGNOREBAK")))
            {
                ignoreBak = true;//Will proceed with download without detecting any BAK files - only use if we only want to pull down change files but not a backup file for whatever reason
            }

            try
            {
                Parallel.Invoke(
                    () => {
                        if (args.Any(x => x.ToUpper().Contains("")))
                        {
                            string ftpSite = "";
                            string ftpSiteName = "";
                            string remoteDir = "";
                            string localDir = TestMode ? @"" : @"";
                            string database = "PHP_Staging";
                            string statusQuery = "";
                            string remoteQuery = "";
                            string searchForTrn = ".trn";
                            string searchForBak = ".bak";
                            int port = 25092;
                            source = database + "_PROD";
                            if (!TestMode || testOverride)
                            {
                                Data target = new Data(Data.AppNames.ExampleProd);
                                bool success = RunDownload(
                                    ftpSite, 
                                    remoteDir, 
                                    localDir, 
                                    ftpSiteName, 
                                    recovery, 
                                    FullDownload, 
                                    target, 
                                    ignoreTrn, 
                                    ignoreBak,
                                    skipDownloads,
                                    database, 
                                    statusQuery, 
                                    remoteQuery, 
                                    searchForTrn, 
                                    searchForBak, 
                                    bakWaitTimes: 228, 
                                    trnWaitTimes: 228, 
                                    port: port);//this is 228 because sometimes  is late in delivering full backups. This is a maximum wait, not minumum.
                                //trnWaitTimes is 228 because  can take upwards of 18+ hours to deliver the logs on maintanence weekends;
                                if (!success)
                                {
                                    SendPage();
                                }
                            }
                        }
                    }
                    );
            }
            catch (Exception E)
            {
                WriteToLog(E.ToString(), UniversalLogger.LogCategory.ERROR);
                throw E.InnerException;
            }

            return true;
        }

        private bool DownloadFull(Data dataTarget, string database)
        {
            DateTime today = DateTime.Today;
            DataTable table = ExtractFactory.ConnectAndQuery(this, dataTarget.ApplicationName, $"SELECT * FROM [PHPConfg].[dbo].[DA03248_FullBackupSchedule_C] WHERE [Database] = '{database}' AND DayOfWeek = '{today.DayOfWeek}'");

            if (table.Rows.Count > 0)
            {
                int day = today.Day;
                int week = 0;
                while (day > 0)
                {
                    day -= 7;
                    week++;
                }
                if (table.AsEnumerable().Any(x => x.Field<short>("WeekInMonth") == week))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ftpSite">Address of the site we're connecting to</param>
        /// <param name="remoteDir">Directory on the FTP site to expect files at</param>
        /// <param name="localDir">Where we want to download files to</param>
        /// <param name="ftpSiteName">Site name within our PW store, for credential recovery</param>
        /// <param name="recovery">Whether we want to pick up partial downloads where they were left off at or not.</param>
        /// <param name="expectFull">Set this to make the job wait for a full backup file. Will still download any full backup files that are found even if this is false.</param>
        /// <param name="dataTarget">Data object of the DB we're launching from (Generally PHPConfg)</param>
        /// <param name="ignoreTrn">Set this to not go looking for transaction logs.</param>
        /// <param name="ignoreBak">Set this to not go looking for backup files.</param>
        /// <param name="skipDownloads">Set this to skip all downloads and use what we have locally.</param>
        /// <param name="database">Local name of the database.</param>
        /// <param name="statusQuery">Query used to identify the load status.</param>
        /// <param name="remoteQuery">Query used to identify remote server timestamp</param>
        /// <param name="searchForTrn">Specific type of .trn file to look for on the site. </param>
        /// <param name="searchForBak">Specific type of .bak file to look for on the site.</param>
        /// <param name="tries">How many times should we retry the full process?</param>
        /// <param name="trnWaitTimes">How long should we wait for transactions files to be available? Default indefinite. 5 minute intervals.</param>
        /// <param name="bakWaitTimes">How long should we wait for backup files to be available? Default indefinite. 5 minute intervals. Only applies when explicitely told to wait for backup files through expectFull or the backup schedule table. This is a maximum wait time, not a minimum.</param>
        /// <param name="port"></param>
        /// <param name="maxDegreesOfParallelism"></param>
        /// <returns></returns>
        private bool RunDownload(string ftpSite, string remoteDir, string localDir, string ftpSiteName, bool recovery, bool expectFull, Data dataTarget, bool ignoreTrn, bool ignoreBak, bool skipDownloads, string database, string statusQuery, string remoteQuery, string searchForTrn, string searchForBak, int tries = 2, int trnWaitTimes = -1, int bakWaitTimes = -1, int port = 22, int maxDegreesOfParallelism = 0)
        {
            bool success = false;

            expectFull = DownloadFull(dataTarget, database) || expectFull;
            int filesDownloaded = 0;
            int trnFilesDownloaded = 0;
            int bakFilesDownloaded = 0;
            string localStagingDir = localDir + @"Staging\";
            string localArchiveDir = localDir + @"Archive\";
            FileSystem.ReportYearDir(localDir);
            FileSystem.ReportYearDir(localArchiveDir);
            FileSystem.ReportYearDir(localStagingDir);
            List<string> fileNames = new List<string>();
            List<string> trnFileNames = new List<string>();
            DateTime fileDateTime = DateTime.MinValue;

            WriteToLog("Beginning download process for " + source);

            Parallel.Invoke(
                () => {
                    if (!(ignoreBak || skipDownloads))
                    {
                        fileNames = CheckFiles(ftpSite, ftpSiteName, remoteDir, bakWaitTimes, tries != 1 && expectFull, searchForBak, "full backup", port: port);

                        if (fileNames.Count > 0)
                        {
                            WriteToLog("Found a full backup file");
                            GetFileDatetime(fileNames[0], "yyyyMMddHHmm", ref fileDateTime);
                            List<string> localFiles = Directory.GetFiles(localDir, "*.bak*").ToList();

                            if (localFiles.Count > 0)//Only want to delete/archive old files if the .bak is there, because if it's not it means we already have.
                            {
                                foreach (string thisFile in Directory.GetFiles(localArchiveDir))
                                {
                                    DateTime thisFileDateTime = DateTime.MaxValue;
                                    GetFileDatetime(thisFile, "yyyyMMddHHmm", ref thisFileDateTime);

                                    if (thisFileDateTime < fileDateTime)
                                    {
                                        File.Delete(thisFile);
                                    }
                                }
                                foreach (string thisFile in Directory.GetFiles(localDir))
                                {
                                    DateTime thisFileDateTime = DateTime.MaxValue;
                                    GetFileDatetime(thisFile, "yyyyMMddHHmm", ref thisFileDateTime);

                                    if (thisFileDateTime < fileDateTime)
                                    {
                                        if (File.Exists(localArchiveDir + Path.GetFileName(thisFile)))
                                        {
                                            File.Move(thisFile, localArchiveDir + Path.GetFileNameWithoutExtension(thisFile) + "_bak" + Path.GetExtension(thisFile));
                                        }
                                        else
                                        {
                                            File.Move(thisFile, localArchiveDir + Path.GetFileName(thisFile));
                                        }
                                    }
                                }
                            }
                        }
                        if (fileNames.Count > 0)
                        {
                            WriteToLog("Downloading full backup");
                            try
                            {
                                bakFilesDownloaded += FtpFactory.SFTPRecoverableDownload(ftpSite, ftpSiteName, remoteDir, localStagingDir, localDir, this, searchForBak, !TestMode, recovery, port: port, maxDegreesOfParallelism: maxDegreesOfParallelism).Count;
                            }
                            catch (System.Net.Sockets.SocketException e)
                            {
                                if (e.StackTrace.ToUpper().Contains("FAILED DELETING FILE"))
                                {
                                    WriteToLog("Failed to delete file " + e.ToString(), UniversalLogger.LogCategory.WARNING);
                                    bool successfullDelete = true;
                                    foreach (string file in Directory.GetFiles(localDir, "*.bak"))
                                    {
                                        System.Threading.Thread.Sleep(1000);
                                        bool thisDelete = FtpFactory.SFTPDeleteFile(ftpSite, ftpSiteName, remoteDir, Path.GetFileName(file), this, port);
                                        if (successfullDelete == true)
                                        {
                                            successfullDelete = thisDelete;
                                        }
                                    }
                                    if (successfullDelete == false)
                                    {
                                        WriteToLog("Failed to delete all files downloaded from the FTP site", UniversalLogger.LogCategory.ERROR);
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                if (e.ToString().ToUpper().Contains("THERE IS NOT ENOUGH SPACE ON THE DISK"))
                                {
                                    WriteToLog("Ran out of space on disk, emptying the archive dir then trying again. If this is a regular thing should definitely expand storage space.", UniversalLogger.LogCategory.WARNING);

                                    foreach (string thisFile in Directory.GetFiles(localArchiveDir))
                                    {
                                        File.Delete(thisFile);
                                    }
                                    bakFilesDownloaded += FtpFactory.SFTPRecoverableDownload(ftpSite, ftpSiteName, remoteDir, localStagingDir, localDir, this, searchForBak, !TestMode, recovery, onlyRunCleanup: true, port: port, maxDegreesOfParallelism: maxDegreesOfParallelism).Count;
                                }
                                else
                                {
                                    bakFilesDownloaded += FtpFactory.SFTPRecoverableDownload(ftpSite, ftpSiteName, remoteDir, localStagingDir, localDir, this, searchForBak, !TestMode, recovery, port: port, maxDegreesOfParallelism: maxDegreesOfParallelism).Count;
                                }
                            }
                        }
                    }
                    else
                    {
                        WriteToLog("Not checking for a full backup file");
                    }
                },
                () => {
                    if (!(ignoreTrn || skipDownloads))
                    {
                        trnFileNames = CheckFiles(ftpSite, ftpSiteName, remoteDir, trnWaitTimes, tries != 1, searchForTrn, "log", port: port);

                        WriteToLog("Downloading hourly logs");
                        try
                        {
                            trnFilesDownloaded += FtpFactory.SFTPRecoverableDownload(ftpSite, ftpSiteName, remoteDir, localStagingDir, localDir, this, searchForTrn, !TestMode, recovery, port: port, maxDegreesOfParallelism: maxDegreesOfParallelism).Count;
                        }
                        catch (System.Net.Sockets.SocketException e)
                        {
                            if (e.StackTrace.ToUpper().Contains("FAILED DELETING FILE"))
                            {
                                WriteToLog("Failed to delete file " + e.ToString(), UniversalLogger.LogCategory.WARNING);
                                bool successfullDelete = true;
                                foreach (string file in Directory.GetFiles(localDir, "*.trn"))
                                {
                                    System.Threading.Thread.Sleep(1000);
                                    bool thisDelete = FtpFactory.SFTPDeleteFile(ftpSite, ftpSiteName, remoteDir, Path.GetFileName(file), this, port);
                                    if(successfullDelete == true)
                                    {
                                        successfullDelete = thisDelete;
                                    }
                                }
                                if(successfullDelete == false)
                                {
                                    WriteToLog("Failed to delete all files downloaded from the FTP site", UniversalLogger.LogCategory.ERROR);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            if (e.ToString().ToUpper().Contains("THERE IS NOT ENOUGH SPACE ON THE DISK"))
                            {
                                WriteToLog("Ran out of space on disk, emptying the archive dir then trying again. If this is a regular thing should definitely expand storage space.", UniversalLogger.LogCategory.WARNING);

                                foreach (string thisFile in Directory.GetFiles(localArchiveDir))
                                {
                                    File.Delete(thisFile);
                                }
                                trnFilesDownloaded += FtpFactory.SFTPRecoverableDownload(ftpSite, ftpSiteName, remoteDir, localStagingDir, localDir, this, searchForTrn, !TestMode, recovery, onlyRunCleanup: true, port: port, maxDegreesOfParallelism: maxDegreesOfParallelism).Count;
                            }
                            else
                            {
                                trnFilesDownloaded += FtpFactory.SFTPRecoverableDownload(ftpSite, ftpSiteName, remoteDir, localStagingDir, localDir, this, searchForTrn, !TestMode, recovery, port: port, maxDegreesOfParallelism: maxDegreesOfParallelism).Count;
                            }
                        }
                    }
                    else
                    {
                        WriteToLog("Not checking for transaction logs");
                    }
                }

                );

            // We are skipping downloads and just processing "local" files
            if (skipDownloads)
            {
                bakFilesDownloaded = Directory.GetFiles(localDir, "*.trn").Count();
                trnFilesDownloaded = Directory.GetFiles(localDir, "*.bak").Count();
            }

            filesDownloaded = trnFilesDownloaded + bakFilesDownloaded;

            List<DateTime> statusBefore = statusQuery == "" ? new List<DateTime>() { DateTime.Now } : ExtractFactory.ConnectAndQuery<DateTime>(this, dataTarget, statusQuery).ToList();
            bool ingestSuccess = false;
            string jobName = "";

            CheckFilesExist(localDir, localArchiveDir);

            if (filesDownloaded > 0 || tries == 1)
            {
                if (database == "CAE")
                {
                    string otherRefreshStatus = @"SELECT  sj.name AS JobName
                                                FROM msdb.dbo.sysjobactivity AS sja
                                                INNER JOIN msdb.dbo.sysjobs AS sj 
                                                        ON sja.job_id = sj.job_id
                                                INNER JOIN msdb.dbo.sysjobsteps AS sjs 
                                                        ON sjs.job_id = sj.job_id AND sjs.step_id = sja.last_executed_step_id
                                                WHERE sja.start_execution_date IS NOT NULL
                                                  AND sj.name in ('Refresh database', 'Refresh CAE database', 'Refresh PHP_Staging database')
                                                  AND sja.stop_execution_date IS NULL
                                                  AND sja.start_execution_date >= GETDATE() - 2
                                                ORDER BY sja.start_execution_date desc";
                    do
                    {
                        jobName = ExtractFactory.ConnectAndQuery<string>(this, dataTarget, otherRefreshStatus).FirstOrDefault();
                        if (jobName != null)
                        {
                            WriteToLog("Waiting for other refresh process to complete.", UniversalLogger.LogCategory.AUDIT);
                            System.Threading.Thread.Sleep(1000 * 60 * 5);
                        }
                    } while (jobName != null);
                }
                WriteToLog("All downloads completed, launching ingest.");
                DateTime launchTime = DateTime.Now;
                DataWork.RunSqlCommand(this, $"EXEC msdb.dbo.sp_start_job @job_name = N'Refresh {database} database'", dataTarget.ApplicationName);
                WriteToLog("Ingest launched. Waiting for completion flag");
                string query = $"SELECT [Ready] FROM [PHPConfg].[dbo].[DatabaseRefresh_A] WHERE [Database] = '{database}' and DateStamp >= '{launchTime:yyyy-MM-dd HH:mm}'";
                WriteToLog(query);
                List<bool> table = ExtractFactory.ConnectAndQuery<bool>(this, dataTarget.ApplicationName, query).ToList();
                while (table.Count == 0)
                {
                    WriteToLog("Waiting for completion flag");
                    table = ExtractFactory.ConnectAndQuery<bool>(this, dataTarget.ApplicationName, query, tries: 4).ToList();
                    System.Threading.Thread.Sleep(1000 * 60);
                }
                if (table[0] == false)
                {
                    WriteToLog("Ingest completed in error", tries > 1 ? UniversalLogger.LogCategory.WARNING : UniversalLogger.LogCategory.ERROR);
                    ingestSuccess = false;
                }
                else
                {
                    ingestSuccess = true;
                }
            }
            else
            {
                WriteToLog("No files downloaded", UniversalLogger.LogCategory.ERROR);
            }


            if (!ingestSuccess)
            {
                if (tries > 1)
                {
                    tries--;
                    success = RunDownload(ftpSite, remoteDir, localDir, ftpSiteName, recovery, false, dataTarget, ignoreTrn, ignoreBak, skipDownloads, database, statusQuery, remoteQuery, searchForTrn, searchForBak, tries, port: port);
                    ingestSuccess = success;
                }
            }

            List<DateTime> statusAfter = statusQuery == "" ? new List<DateTime>() { DateTime.Now } : ExtractFactory.ConnectAndQuery<DateTime>(this, dataTarget, statusQuery).ToList();


            if (statusQuery != "")
            {
                WriteToLog($"Status Time before ingest: {statusBefore[0]:yyyyMMddHHmm}; Status Time after ingest: {statusAfter[0]:yyyyMMddHHmm}");
                if (statusAfter[0] <= statusBefore[0])
                {
                    success = false;
                    bool timeMatches = false;
                    if (source == "PHP_Staging_TEST" || source == "_TEST")
                    {
                        List<DateTime> statusRemote = ExtractFactory.ConnectAndQuery<DateTime>(this, dataTarget, string.Format(remoteQuery, statusAfter[0].ToString("yyyy-MM-dd HH:mm:ss:fff"))).ToList();
                        if (statusAfter[0] == statusRemote[0])
                        {
                            timeMatches = true;
                            AddDatabaseRefreshRecord(database, dataTarget, 1);
                            success = true;
                        }
                    }
                    if (timeMatches == false)
                    {
                        AddDatabaseRefreshRecord(database, dataTarget, 0);
                        if (tries > 1)
                        {
                            tries--;
                            WriteToLog("Download and ingest process didn't work the first time. Trying again from the top.", UniversalLogger.LogCategory.WARNING);
                            System.Threading.Thread.Sleep(1000 * 60 * 5);
                            success = RunDownload(ftpSite, remoteDir, localDir, ftpSiteName, recovery, false, dataTarget, ignoreTrn, ignoreBak, skipDownloads, database, statusQuery, remoteQuery, searchForTrn, searchForBak, tries, port: port);
                        }
                        else
                        {
                            DateTime syinDt = ExtractFactory.ConnectAndQuery<DateTime>(this, dataTarget, "").FirstOrDefault();
                            WriteToLog("Logs downloaded and ingested did not get us to the next batch. Check that all logs downloaded, and that they ingested properly. "
                                + (ingestSuccess ? "No catastrophic errors in ingest, but missing files or corrupt files could have prevented ingest."
                                : "Catastrophic failure detected in ingest. DBAs have been paged for SQL Layer (if this is prod).")
                                + $"\nFor Prod, the load_time is updated via batch. Here's the latest SYIN_CREATE_DTM for comparison: {syinDt}", UniversalLogger.LogCategory.ERROR);
                        }
                    }
                }
                else
                {
                    AddDatabaseRefreshRecord(database, dataTarget, 1);
                    success = true;
                }
            }
            else
            {
                if (!ingestSuccess)
                {
                    AddDatabaseRefreshRecord(database, dataTarget, 0);
                    if (tries > 1)
                    {
                        tries--;
                        WriteToLog("Download and ingest process didn't work the first time. Trying again from the top.", UniversalLogger.LogCategory.WARNING);
                        System.Threading.Thread.Sleep(1000 * 60 * 5);
                        success = RunDownload(ftpSite, remoteDir, localDir, ftpSiteName, recovery, false, dataTarget, ignoreTrn, ignoreBak, skipDownloads, database, statusQuery, remoteQuery, searchForTrn, searchForBak, tries, port: port);
                    }
                    else
                    {
                        WriteToLog("Catastrophic failure detected in ingest. DBAs have been paged from SQL Layer (if this is prod).", UniversalLogger.LogCategory.ERROR);
                    }
                }
                else
                {
                    success = true;
                    AddDatabaseRefreshRecord(database, dataTarget, 1);
                }
            }

            return success;
        }

        private void AddDatabaseRefreshRecord(string database, Data dataTarget, int status)
        {
            if (status == 0)
            {
                string queryOne = $@"INSERT INTO [dbo].[DatabaseRefresh_A]
                       ([Ready]
                       ,[Database]
                       ,[DateStamp])
                 VALUES
                       (0
                       , '{database}'
                       , GETDATE())";
                DataWork.RunSqlCommand(this, queryOne, dataTarget.ApplicationName);
            }//Keeping this first one here for legacy for Warehouse uses

            string query = $@"INSERT INTO [dbo].[DatabaseRefresh_A]
                       ([Ready]
                       ,[Database]
                       ,[ProcessFlow]
                       ,[DateStamp])
                 VALUES
                       ({status}
                       , '{database}'
                       ,'VALIDATED'
                       , GETDATE())";
            DataWork.RunSqlCommand(this, query, dataTarget.ApplicationName);
        }

        private List<string> CheckFiles(string ftpSite, string ftpSiteName, string remoteDir, int waitTimes, bool expectFiles, string contains, string log, int port = 22)
        {
            KeyValuePair<string, string> creds = FileTransfer.GetKeyPassCredentials(ftpSiteName);
            string userName = creds.Key;
            string password = creds.Value;
            List<string> fileNames = new List<string>();
            WriteToLog($"Checking for a {log} file");
            bool waitingForFile = false;
            do
            {
                try
                {
                    using (SftpClient sftp = new SftpClient(ftpSite, port, userName, password))
                    {
                        sftp.Connect();
                        fileNames = sftp.ListDirectory(remoteDir).Where(f => f.IsDirectory == false && Regex.IsMatch(f.Name, contains, RegexOptions.IgnoreCase)).Select(f => f.Name).ToList();
                        sftp.Disconnect();
                    }
                    if (fileNames.Count == 0 && expectFiles && (waitTimes > 0 || waitTimes == -1))
                    {
                        if (waitTimes > 0)
                        {
                            waitTimes--;
                        }
                        waitingForFile = true;
                        System.Threading.Thread.Sleep(1000 * 60 * 5);
                        WriteToLog($"Expected {log} and haven't found one yet", UniversalLogger.LogCategory.WARNING);
                    }
                }
                catch (Exception E)
                {
                    WriteToLog(E.ToString());
                    System.Threading.Thread.Sleep(1000 * 60 * 5);
                    fileNames = CheckFiles(ftpSite, ftpSiteName, remoteDir, waitTimes, expectFiles, contains, log, port: port);
                }
            } while (fileNames.Count == 0 && expectFiles && (waitTimes > 0 || waitTimes == -1));
            if (fileNames.Count == 0 && expectFiles && waitTimes == 0)
            {
                WriteToLog($"Expected {log} and never found one. Moving on. Contact PHP IT to have them contact vendor about missing files.", UniversalLogger.LogCategory.AUDIT);
            }
            if (waitingForFile && fileNames.Count > 0)
            {
                System.Threading.Thread.Sleep(1000 * 60 * 7);
            }
            return fileNames;
        }

        private void CheckFilesExist(string localDir, string localArchiveDir)
        {
            List<string> bakFile = Directory.GetFiles(localDir).Where(x => x.ToUpper().Contains(".BAK")).ToList();
            List<string> trnFile = Directory.GetFiles(localDir).Where(x => x.ToUpper().Contains(".TRN")).OrderBy(x => x).ToList();
            DateTime bakTime = DateTime.MinValue;
            DateTime trnTime = DateTime.MinValue;
            DateTime trnFirstTime = DateTime.MinValue;

            GetFileDatetime(bakFile[0], "yyyyMMddHH", ref bakTime);

            if (trnFile.Count == 0)
            {
                return;
            }

            GetFileDatetime(trnFile.Last(), "yyyyMMddHH", ref trnTime);
            GetFileDatetime(trnFile.First(), "yyyyMMddHH", ref trnFirstTime);

            //handling for no TRN
            DateTime thisTime = trnFirstTime < bakTime ? trnFirstTime : bakTime;

            int fileCount = 0;
            while (thisTime <= trnTime)
            {
                List<string> file = trnFile.Where(x => x.Contains(thisTime.ToString("yyyyMMddHH"))).ToList();
                if (file.Count == 1)
                {
                    fileCount++;

                    if (thisTime < bakTime)
                    {
                        if (File.Exists(localArchiveDir + Path.GetFileName(file[0])))
                        {
                            File.Move(file[0], localArchiveDir + Path.GetFileNameWithoutExtension(file[0]) + "_bak" + Path.GetExtension(file[0]));
                        }
                        else
                        {
                            File.Move(file[0], localArchiveDir + Path.GetFileName(file[0]));
                        }
                    }
                }
                else if (file.Count > 1)
                {
                    WriteToLog("More than one file found for " + thisTime.ToString("yyyyMMddHH"), UniversalLogger.LogCategory.WARNING);
                    fileCount += file.Count;
                }
                else
                {
                    WriteToLog($"No file found for {thisTime:yyyyMMddHH}, if this time was during maintanence on this environment this isn't an issue. Otherwise request file from vendor, especially if you're looking at these logs because the ingest failed (in that case, this is why the ingest failed).", UniversalLogger.LogCategory.WARNING);
                }
                thisTime = thisTime.AddHours(1);
            }

            if (fileCount != trnFile.Count)
            {
                WriteToLog("File count mismatch", UniversalLogger.LogCategory.WARNING);
            }
        }
        ///<summary>
        ///Vendor file names come in all shapes and sizes. Use this method to return the parsed DateTime from a file name.
        ///</summary>
        ///<param name="file">File that you pass in to extract the DateTime stamp off of</param>
        ///<param name="dateTimeFormat">Use HH or MM for most specific time type</param>
        ///<param name="parsedDateTime">Returns datetime from file name</param>
        private DateTime GetFileDatetime(string file, string dateTimeFormat, ref DateTime parsedDateTime)
        {
            //This will find the group of numbers that's at least 8 digits long in the file.
            Match fileDateTime = Regex.Match(file, @"(?<=\D)\d{8,}(?=.*$)");

            if (fileDateTime.Success)
            {
                string dateTimeString = fileDateTime.Value.PadRight(dateTimeFormat.Length, '0').Substring(0, dateTimeFormat.Length);
                parsedDateTime = DateTime.ParseExact(dateTimeString, dateTimeFormat, CultureInfo.InvariantCulture);
            }
            else
            {
                WriteToLog($"File name missing date (or name format is incorrect): {file}", UniversalLogger.LogCategory.ERROR);
            }

            return parsedDateTime;
        }

        /// <summary>
        /// If 3248 Errors, we need to override who gets the error message so that we get paged.
        /// </summary>
        /// <param name="exc">The exception to handle</param>
        new public void OnError(Exception exc)
        {
            WriteToLog(exc.ToString(), UniversalLogger.LogCategory.ERROR);

            SendPage();
        }
    }
}