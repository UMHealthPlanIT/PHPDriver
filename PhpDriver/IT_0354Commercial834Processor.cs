using Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Utilities.Eight34Outputs;


namespace Driver
{
    class IT_0354Commercial834Processor : Logger, IPhp
    {
        Data.AppNames CommTarget;
        Utilities.Eight34Outputs.PopulateFrom834.OutputPaths oPaths;
        string EmailMessage;
        string EmailSubject;
        string linkedServerPointer;
        SourceMatcher Matcher;

        /// <summary>
        /// Integrated process picking up 834 files, transforming them to transactions and staging them for IT_0116 to wrap up into a single file to push.
        /// </summary>
        /// <param name="Program">IT_0354</param>
        public IT_0354Commercial834Processor(LaunchRequest Program) : base(Program) { }

        public bool Initialize(string[] args)
        {
            EmailMessage = "";
            EmailSubject = "Comm834 Processing Summary";
            int EmailExitCode = 0;
            bool localOnly = false;

            linkedServerPointer = TestMode ? "." : ".";

            if (args.Count() > 1)
            {
                if (args[1].ToUpper() == "DEV")
                {
                    CommTarget = GetComm834Database(args[1].ToUpper());
                    linkedServerPointer = ".";
                }
                else if (args[1].ToUpper() == "P") //force to match against prod even in test mode
                {
                    CommTarget = GetComm834Database(args[1].ToUpper());
                    linkedServerPointer = ".";
                }
                else if (args[1].ToUpper() == "T") //force to match against test even in test mode
                {
                    CommTarget = GetComm834Database();
                    linkedServerPointer = ".";
                }
                else
                {
                    CommTarget = GetComm834Database();
                }
                if (args.Any(x => x.ToUpper() == "LOCAL"))
                {
                    WriteToLog("Only pulling from local staging and not looking at FTP Sites.");
                    localOnly = true;
                }
            }
            else
            {
                CommTarget = GetComm834Database();
            }

            string outBoundGroupIdOverride = args.Any(x => x.ToUpper().Contains("GROUPID:")) ? args.Where(x => x.ToUpper().Contains("GROUPID:")).First().Substring(8)
                : "";

            WriteToLog("Comm834 Database Target is: " + CommTarget.ToString());
            WriteToLog("Staging Database Target is: " + linkedServerPointer);
            oPaths = new Utilities.Eight34Outputs.PopulateFrom834.OutputPaths(this);
            List<GroupConfiguration> ActiveGroups = ExtractFactory.ConnectAndQuery<GroupConfiguration>(this, LoggerPhpConfig, $@"Select * FROM Eight34GroupLookup_C WHERE [Active] = 1 AND DBName = 'Comm834' 
                {(string.IsNullOrWhiteSpace(outBoundGroupIdOverride) ? "" : " AND OutgoingGroupID = '" + outBoundGroupIdOverride + "'")}").ToList();
            WriteToLog("Loading members + subscribers for matching...");
            Matcher = new SourceMatcher(CommTarget, linkedServerPointer, ActiveGroups.Select(g => g.OutgoingGroupID), this, "Output");
            WriteToLog("Source data loaded");
            foreach (GroupConfiguration grp in ActiveGroups)
            {

                WriteToLog("Starting to process group " + grp.GroupName + " " + grp.OutgoingGroupID);

                string FtpSourceSite, changeDir;
                GetFtpDetails(grp, out FtpSourceSite, out changeDir);

                List<string> foundEightThirtyFour;

                if (localOnly)
                {
                    string searchPattern = (grp.InputFileFilter.Contains("*") ? "*.*" : "*" + grp.InputFileFilter + "*");
                    foundEightThirtyFour = System.IO.Directory.GetFiles(LoggerStagingDir, searchPattern).ToList(); //we're going to pull everything from stage that way users can place files here and still get them picked up
                }
                else
                {
                    foundEightThirtyFour = FileSystem.GetInputFiles(this, FtpSourceSite, grp.InputFileFilter, deleteAfterDownload: true, changeDir: changeDir);
                }
                WriteToLog($"Found {foundEightThirtyFour.Count} file(s) for {grp.GroupName}.");

                if (foundEightThirtyFour.Count == 0)
                {
                    string fileExpected;
                    //check if file should have been received
                    CheckFileExpected(grp, out fileExpected);
                    EmailMessage += grp.GroupName + " " + grp.Description + ":" + Environment.NewLine + "No 834 File Found" + fileExpected + Environment.NewLine + Environment.NewLine;
                }
                else if (foundEightThirtyFour.Count == 2 && grp.GroupName == "")
                {
                    //figure out which is the change file, process that, leaving the other in staging for pick up in next run
                    foreach (string file in foundEightThirtyFour)
                    {
                        string delimi;
                        if (!Eight34.IsFullFile(file, out delimi))
                        {
                            try
                            {
                                GenerateTransactionsFromEightThirtyFour(grp, file);
                            }
                            catch (EmptyFileException exc)
                            {
                                WriteToLog("File received was empty: " + exc.FileName, UniversalLogger.LogCategory.ERROR);
                                FtpFactory.ArchiveFile(this, file);
                            }
                            catch (WrongPayerFileException exc)
                            {
                                WriteToLog("File Header Payer Name Segment not expected (N1*IN*...) for: " + exc.FileName, UniversalLogger.LogCategory.ERROR);
                                EmailSubject = "ERROR - Comm834 Processing Summary";
                                EmailMessage += grp.GroupName + " " + grp.Description + ":" + Environment.NewLine + "ERROR Intervention Required - Header Payer Information does not match our records" + Environment.NewLine + Environment.NewLine;
                                EmailExitCode = 6000;
                            }
                            catch (Exception exc)
                            {
                                WriteToLog(exc.ToString());
                                SendAlerts.Send(ProcessId, 1012, "The file failed to be generated.", file + Environment.NewLine + exc.ToString(), this);
                            }
                        }
                    }

                    EmailMessage += "NOTE: TWO Files were found, we processed the change file and left the full file to be picked up by next run of IT_0354" + Environment.NewLine + Environment.NewLine;

                }
                else //LBWL is allowed to run multiple files through in a day, all else should only have one file here.
                {
                    foreach (string file in foundEightThirtyFour)
                    {
                        try
                        {
                            GenerateTransactionsFromEightThirtyFour(grp, file);

                        }
                        catch (EmptyFileException exc)
                        {
                            WriteToLog("File received was empty: " + exc.FileName, UniversalLogger.LogCategory.ERROR);
                            FtpFactory.ArchiveFile(this, file);
                        }
                        catch (WrongPayerFileException exc)
                        {
                            WriteToLog("File Header Payer Name Segment not expected (N1*IN*...) for: " + exc.FileName, UniversalLogger.LogCategory.ERROR);
                            EmailSubject = "ERROR - Comm834 Processing Summary";
                            EmailMessage += grp.GroupName + " " + grp.Description + ":" + Environment.NewLine + "ERROR Intervention Required - Header Payer Information does not match our records" + Environment.NewLine + Environment.NewLine;
                            EmailExitCode = 6000;

                        }
                        catch (TestFileInProdModeException exc)
                        {
                            WriteToLog("Test File was Found Running in Prod Mode for: " + exc.FileName, UniversalLogger.LogCategory.ERROR);
                            EmailSubject = "ERROR - Comm834 Processing Summary";
                            EmailMessage += grp.GroupName + " " + grp.Description + ":" + Environment.NewLine + "ERROR Intervention Required - File was found in Prod Mode with Test flag in header" + Environment.NewLine + Environment.NewLine;
                            EmailExitCode = 6000;
                        }
                        catch (Exception exc)
                        {
                            WriteToLog(exc.ToString());
                            SendAlerts.Send(ProcessId, 1012, "The file failed to be generated.", file + Environment.NewLine + exc.ToString(), this);
                        }
                    }
                }

                if (!localOnly)
                {
                    System.Threading.Thread.Sleep(10 * 1000); //Speculative fix for Sparrow's FTP server freaking out when we process multiple successive files from them in a row
                }
                
            }

            SendAlerts.Send(ProcessId, EmailExitCode, EmailSubject, EmailMessage, this);

            return true;
        }

        private static void GetFtpDetails(GroupConfiguration group, out string ftpSite, out string ftpDirectory)
        {
            ftpSite = string.IsNullOrWhiteSpace(group.FtpSourceSite) ? "" : group.FtpSourceSite;

            if (ftpSite == "")
            {
                string baseDir = "";
                string groupDir = string.IsNullOrWhiteSpace(group.FtpDirectory) ? group.GroupName : group.FtpDirectory;

                ftpDirectory = (baseDir + groupDir).Replace("//", "/");
            }
            else
            {
                ftpDirectory = group.FtpDirectory;
            }
        }

        /// <summary>
        /// Loads the 834 file into the Comm834 database and transforms the values and structure to output file format
        /// </summary>
        /// <param name="grp">Admin834 group configuration</param>
        /// <param name="file">Source 834 file to process, note this is optional because SPHN no longer uses files</param>
        private void GenerateTransactionsFromEightThirtyFour(GroupConfiguration grp, string file)
        {
            string OutboundGroupID;

            OutboundGroupID = grp.OutgoingGroupID;

            if (string.Equals(Path.GetExtension(file), ".PGP", StringComparison.OrdinalIgnoreCase))
            {
                bool allowCAST5 = OutboundGroupID == "";
                EncryptionWork.PgpDecrypt(file, this, allowCAST5);
                FtpFactory.ArchiveFile(this, file);
                file = file.Replace(".pgp", "");
            }

            string fieldDelimiter;
            bool FullFile = Eight34.IsFullFile(file, out fieldDelimiter);

            if (CheckForTestFileInProd(file, fieldDelimiter))
            {
                //Ut oh, we found a test file running in prod
                throw new TestFileInProdModeException(file);
            }

            Eight34.LoadM834TransactionDetailsToDatabase(file, this, CommTarget, grp.OutgoingGroupID, fieldDelimiter);

            if (FullFile)
            {
                CheckForDuplicateSSNsOnFile();
            }

            TransformFileToOutputValues(grp, Path.GetFileName(file), grp.OutgoingGroupID, FullFile);

            List<string> ftpFiles = new List<string>();

            string EligiblityActLocation = EightThirtyFourReports.Generate834Report(CommTarget, this, grp.GroupName, OutboundGroupID, oPaths.OutputEnrollmentReportPath, "ELIGIBILITY");
            EightThirtyFourReports.Generate834Report(CommTarget, this, grp.GroupName, OutboundGroupID, oPaths.OutputEnrollmentReportPath, "ITDETAILS", Eight34FileName: file);

            if (ExtractFactory.ConnectAndQuery(this, CommTarget, "select * from Output where OOADeps = 1").Rows.Count > 0)
            {
                EightThirtyFourReports.Generate834Report(CommTarget, this, grp.GroupName, OutboundGroupID, oPaths.OutputEnrollmentReportPath, "OUTOFAREA");
            }

            FileTransfer.PushToSharepoint("ITReports", ProcessId, EligiblityActLocation, this);

            if (!string.IsNullOrEmpty(grp.FtpTBO) && grp.FtpTBO == "1")
            {
                ftpFiles.Add(EligiblityActLocation);
            }

            if (grp.GroupName == "SOMComm")
            {
                string discFile = EightThirtyFourReports.GenerateDiscrepancy(CommTarget, this, grp.GroupName, OutboundGroupID, oPaths.OutputEnrollmentReportPath);
                FileTransfer.PushToSharepoint("ITReports", ProcessId, discFile, this);
            }

            EmailMessage += grp.GroupName + " " + grp.Description + ":" + Environment.NewLine +
                "Eligibility Activity Report for " + OutboundGroupID + " at " + EligiblityActLocation + Environment.NewLine + Environment.NewLine;

            if (FullFile)
            {
                string excludedClasses = ExtractFactory.ConnectAndQuery<string>(LoggerPhpConfig, string.Format("select coalesce(ClassID_Exclusions, '') from PHPConfg.dbo.Eight34GroupLookup_C where Active = 1 and DBName = 'Comm834' and GroupName = '{0}'", grp.GroupName)).First();
                string TBOReportLocation = EightThirtyFourReports.Generate834Report(CommTarget, this, grp.GroupName, OutboundGroupID
                    , oPaths.OutputEnrollmentReportPath, "TBO", Eight34.GetTBOQuery(linkedServerPointer, grp.OutgoingGroupID, excludedClasses, grp.InputFileFilter));
                FileTransfer.PushToSharepoint("ITReports", ProcessId, TBOReportLocation, this);

                EmailMessage += "TBO Report for " + OutboundGroupID + " at " + TBOReportLocation + Environment.NewLine + Environment.NewLine;

                if (!string.IsNullOrEmpty(grp.FtpTBO) && grp.FtpTBO == "1")
                {
                    ftpFiles.Add(TBOReportLocation);
                }
            }

            if (ftpFiles.Count > 0)
            {
                if (grp.GroupName == "")
                {
                    List<string> encryptedFtpFiles = new List<string>();
                    foreach (string fName in ftpFiles)
                    {
                        EncryptionWork.PgpEncrypt(fName, "", this);
                        File.Move(fName + ".gpg", fName + ".pgp");
                        encryptedFtpFiles.Add(fName + ".pgp");
                    }

                    FileTransfer.DropToPhpDoorStep(this, encryptedFtpFiles, ProcessId + grp.OutboundRoute);
                }
                else if (grp.OutgoingGroupID == "")
                {
                    DataSet badData = ExtractFactory.ConnectAndQuery_Dataset(this, CommTarget, DataCheck);
                    if (badData.AsEnumerable().Any(x => x.Rows.Count > 0))
                    {
                        string fileName = $"{LoggerOutputYearDir}_DataIssues_{DateTime.Today.ToString("yyyyMMdd")}.xlsx";
                        ExcelWork.OutputDataSetToExcel(badData, fileName, new List<string>() { "3000-6000", "4000", "Other" });
                        ftpFiles.Add(fileName);
                    }

                    //Send UofM files
                    FileTransfer.FtpIpSwitchPush(ftpFiles, this, ProcessId + grp.OutboundRoute);
                }
            }

            PopulateFrom834.ArchiveForEnrollment(file, this, oPaths.InputEnrollmentPath);
            PopulateFrom834.ArchiveForIT(file, LoggerFtpFromDir, this);
        }


        /// <summary>
        /// Checks to see if we're running in production mode with a file with the internal flag marked as Test
        /// </summary>
        /// <param name="file">Given 834 file to process</param>
        /// <param name="fieldDelimiter">Field delimiter identified in the file - used to construct the proper search term</param>
        /// <returns>True if we found a test file running in prod mode (bad), false if the flag is aligned to the run-mode</returns>
        private bool CheckForTestFileInProd(string file, string fieldDelimiter)
        {
            string fileContents = File.ReadAllText(file);

            if (fileContents.Contains(fieldDelimiter + "T" + fieldDelimiter + ":~") & !TestMode)
            {

                WriteToLog("Test File Found When Running in Prod. Please review file " + Path.GetFileName(file) + ". Note, we left it in staging but didn't process it.", UniversalLogger.LogCategory.ERROR);

                return true;
            }
            else
            {
                return false;
            }
        }

        private string DataCheck = @"";

        private static void CheckFileExpected(GroupConfiguration grp, out string fileExpected)
        {
            fileExpected = "";
            if (grp.DayOfWeek != null && grp.DayOfWeek != "Daily")
            {
                string[] fileDays = grp.DayOfWeek.Split(',').Select(day => day.Trim()).ToArray();
                for (int i = 0; i < fileDays.Length; i++)
                {
                    if (fileDays[i] == DateTime.Today.DayOfWeek.ToString())
                    {
                        fileExpected = " (FILE EXPECTED)";
                    }
                }
            }
        }

        /// <summary>
        /// Runs a series of stored procedures on the Comm834 database to convert the 834 values to values, including TBO
        /// </summary>
        /// <param name="grp">Admin834 group lookup configuration</param>
        /// <param name="file">Source file to process</param>
        /// <param name="OutboundGroupID">PHP Group ID for the group being processed (most often these come in on the file, but there are scenarios where we need to map from a vendor id to ours)</param>
        /// <param name="IsFullAuditFile">Whether this is a full audit file (where we process TBOs) or a change file (where we don't)</param>
        private void TransformFileToOutputValues(GroupConfiguration grp, string file, string OutboundGroupID, Boolean IsFullAuditFile)
        {
            DataWork.RunSqlCommand(this, "UPDATE [Output] SET FileName = '" + file + "'", CommTarget);
            if (grp.GroupName == "") 
            {
                DataWork.RunSqlCommand(this, "update Output set GroupNo = '" + OutboundGroupID + "'", CommTarget);
            }

            DataWork.RunSqlCommand(this, GetMissingSubscribersQuery(), CommTarget);

            Matcher.MatchToFacets<OutputRecord>(CommTarget);
            Matcher.AssignMemeSfxForNewMembers(CommTarget);

            Matcher.PreOutputEligibilityDataConversion(CommTarget, OutboundGroupID);

            Utilities.Eight34Outputs.Eight34EditsAndErrors.HoldTerminationsOfTerminatedMembers(this, CommTarget, "Output");


            if (OutboundGroupID == "L0001102")
            {
                if (IsFullAuditFile)
                {
                    DataWork.RunSqlCommand(this, "UPDATE [Output] SET FileName = 'FULL_' + FileName, FileTypeDesc = 'Verify/Full'", CommTarget);

                }
            }

            DataWork.RunSqlCommand(this, "EXEC SP_GroupCustomLogic '" + OutboundGroupID + "'", CommTarget);
            DataWork.RunSqlCommand(this, "UPDATE [Output] SET [Output] = 0, [Output].[ErrCode] = LTRIM(RTRIM([Output].[ErrCode])) + '027,' WHERE [GroupNo] <> '" + OutboundGroupID + "'", CommTarget);
            DataWork.RunSqlCommand(this, "EXEC SP_FormatDates", CommTarget);

            if (OutboundGroupID == "L0002184")
            {
                DataWork.RunSqlCommand(this, "UPDATE [Output] SET [ErrCode] = ltrim(rtrim(ErrCode)) + '044,' WHERE MedicareBegin338 <> '' AND MedicarePlanCode <> 'C' UPDATE [Output] SET [ErrCode] = ltrim(rtrim(ErrCode)) + '045,', [Output] = 0  WHERE MedicareEnd339 <> '' AND MedicarePlanCode <> 'C'", CommTarget);
            }

            if (grp.GenerateOutputRecords_Ind.ToUpper() == "Y")
            {
                PopulateFrom834.GenerateEnrollmentTransactionFile(OutboundGroupID, this, CommTarget, file, oPaths.OutputKWPath, grp.GroupName);
            }
            DataWork.RunSqlCommand(this, "EXEC SP_Translate_Error_Codes", CommTarget);
        }


        private string GetMissingSubscribersQuery()
        {
            return $@""; 
        }

        /// <summary>
        /// Places transactions with a 040 error code if the SSN is duplicated on the file
        /// </summary>
        private void CheckForDuplicateSSNsOnFile()
        {

            DataWork.RunSqlCommand(this, @"update O
                                    set O.Output = 0, ErrCode = ltrim(rtrim(ErrCode)) + '040,'
                                    from Output O
                                    where SSN in (
                                    select SSN
                                    from Output
                                    where SSN not in ('000000000','999999999')
                                    group by SSN
                                    having count(*) > 1)
                                    AND SubscriberFlag = 'Y'", CommTarget);
        }

        private Data.AppNames GetComm834Database(string environment = "")
        {
            Data.AppNames CommTarget;

            if(environment == "" || TestMode == true)
            {

                CommTarget = Data.AppNames.ExampleTest;
            }
            else
            {
                CommTarget = Data.AppNames.ExampleProd;
            }
            return CommTarget;
        }

        class GroupConfiguration
        {
            public string InputFileFilter { get; set; }
            public string GroupName { get; set; }
            public string Description { get; set; }
            public string OutgoingGroupID { get; set; }
            public string FtpEnrollment { get; set; }
            public string FtpTBO { get; set; }
            public string OutboundRoute { get; set; }
            public string ClassID_Exclusions { get; set; }
            public string GenerateOutputRecords_Ind { get; set; }
            public string DayOfWeek { get; set; }
            public string FtpSourceSite { get; set; }
            public string FtpDirectory { get; set; }
        }
    }
}