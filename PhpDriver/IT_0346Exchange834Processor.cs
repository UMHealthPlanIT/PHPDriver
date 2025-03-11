using Utilities;
using Utilities.Eight34Outputs;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Utilities.Eight34Outputs;


namespace Driver
{
    /// <summary>
    /// 834 to Transaction Processor for Exchange/Healthcare.gov Files
    /// </summary>
    class IT_0346Exchange834Processor : Logger, IPhp
    {
        List<string> reportList = new List<string>();
        string linkedServerPointer = "";
        PopulateFrom834.OutputPaths oPaths;
        Data.AppNames exchangeDb;
        SourceMatcher facetsMatcher;
        string prefix = "";
        string outputTable;

        public IT_0346Exchange834Processor(LaunchRequest Program) : base(Program) { }

        public bool Initialize(string[] args)
        {


            exchangeDb = GetProcessorDB();
            bool localOnly = false;

            if (args.Count() > 1)
            {
                if (args[1].ToUpper() == "DEV")
                {
                    exchangeDb = GetProcessorDB(true);
                    linkedServerPointer = "."; //Note, we are adding this period here after we moved the DB into PHPConfg. In production it doesn't require a linked server so resolves to blank (why we need a period here if we want to override).
                }
                else if (args[1].ToUpper() == "P") //force to match against prod even in test mode
                {
                    //WARNING: there are API calls in PopulateFrom834 that IT_0346 uses that use the appconfig to point to SIT when when from local.
                    //these will need to be adjusted to be fully pointed to production

                    linkedServerPointer = ".";
                }
                if (args.Contains("TEST"))
                {
                    prefix = "TEST_";
                }
            }

            if (args.Any(x => x.ToUpper() == "LOCAL"))
            {
                WriteToLog("Only pulling from local staging and not looking at FTP Sites.");
                localOnly = true;
            }

            outputTable = "IT0346_Output_F";

            WriteToLog("Loading Facets members + subscribers for matching...");
            facetsMatcher = new SourceMatcher(exchangeDb, linkedServerPointer, new List<string>() { "", "" }, this, outputTable);
            WriteToLog("Facets data loaded");

            oPaths = new PopulateFrom834.OutputPaths(this);

            List<string> foundFiles;
            if (localOnly)
            {
                string searchPattern = $"*{prefix}*";
                foundFiles = System.IO.Directory.GetFiles(this.LoggerStagingDir, searchPattern).ToList(); //we're going to pull everything from stage that way users can place files here and still get them picked up
            }
            else
            {
                foundFiles = FileSystem.GetInputFiles(this, "", $"{prefix}", changeDir: "", deleteAfterDownload: true);
            }
            
            string emailBody = "";
            emailBody += Process834FilesForExchange(foundFiles, "", "", "", ".AUD");

            if (!foundFiles.Any())
            {
                return false;
            }

            string sharePointPath = TestMode ? "https://itreports/itreportsdev/IT_TEST/" : "https://itreports/IT_0346";
            SendAlerts.Send(ProcessId, 0, "Enrollment EDI 834 File Pick Up and Processing", "The following files were picked up and processed:" + Environment.NewLine + emailBody + Environment.NewLine + @"Please review the reports at " + sharePointPath, this);

            return true;
        }


        private string Process834FilesForExchange(List<string> found834s, string vendor, string OnExchangeNameKey, string OffExchangeNameKey, string fullFileNameKey)
        {
            string emailBody = "";

            if (found834s == null || found834s.Count == 0)
            {
                SendAlerts.Send(ProcessId, 4, "No 834 Files Found from " + vendor, "Please contact IT if this is unexpected", this);
                return "";
            }
            else
            {
                // We are going to exclude full (audit) files from processing for now
                IEnumerable<string> OnExchangeFiles = found834s.Where(x => Path.GetFileName(x).Contains(OnExchangeNameKey));
                IEnumerable<string> OffExchangeFiles = found834s.Where(x => Path.GetFileName(x).Contains(OffExchangeNameKey));
                IEnumerable<string> unusedFiles = found834s.Where(x => !OnExchangeFiles.Contains(x) && !OffExchangeFiles.Contains(x));

                if (unusedFiles.Any())
                {
                    WriteToLog($"Archiving unused files from {vendor}: ");

                    foreach (string file in unusedFiles)
                    {
                        FtpFactory.ArchiveFile(this, file);
                    }
                }

                emailBody += ParseFoundFiles(OnExchangeFiles, "", "FEDEXCH", fullFileNameKey, vendor);
                emailBody += ParseFoundFiles(OffExchangeFiles, "", "OFFEXCH", fullFileNameKey, vendor);
            }

            foreach (string file in reportList)
            {
                FileTransfer.PushToSharepoint("ITReports", ProcessId, file, this);
            }

            return emailBody;
        }



        private string ParseFoundFiles(IEnumerable<string> foundEightThirtyFours, string groupId, string groupAbbreviation, string fullFileNameKey, string vendorSource)
        {
            string emailBody = "";
            if (!foundEightThirtyFours.Any(x => Path.GetFileName(x).Contains(fullFileNameKey))) //if we didn't find any audit files we won't have any contention so let's process them
            {
                foreach (string f in foundEightThirtyFours)
                {
                    emailBody += ProcessEDIFile(exchangeDb, f, groupId, groupAbbreviation, false, vendorSource);
                }
            }
            else //we found full/audit files - let's process those and leave the change files to get processed next time
            {           
                emailBody += Environment.NewLine + groupAbbreviation + " Change files were found but are being held until tomorrow as a full file was also found.";
                emailBody += ProcessEDIFile(exchangeDb, foundEightThirtyFours.Where(x => Path.GetFileName(x).Contains(fullFileNameKey)).First(), groupId, groupAbbreviation, true, vendorSource); //if we get both files, process the full one and leave the other
            }

            return emailBody;
        }

        private string ProcessEDIFile(Data.AppNames exchangeDb, string Edifile, string GroupID, string nameStub, bool fullFile, string vendorSource)
        {
            Eight34.LoadM834TransactionDetailsToDatabase(Edifile, this, exchangeDb, GroupID, "*", vendorSource);

            facetsMatcher.LoadCurrentGroupMembers(GroupID);
            

            //Classes and SubGroups are calculated from an Exchange Product ID known as a "HIOS ID". This table is updated annually based on products PHP is approved to sell on the Insurance Marketplace.
            DataWork.RunSqlCommand(this, $@"UPDATE [IT0346_Output_F] SET ClassCode = H.Class, SubGroup = H.SubGroup, RateArea = H.RateMod, SubsGroup = H.SubGroup 
FROM dbo.IT0346_Output_F O 
     INNER JOIN 
	 {linkedServerPointer}[PHPConfg].[dbo].[IT0346_HIOSClassPlanXWalk_C] H 
  ON O.HIOS = H.HIOS and H.GroupID = '{GroupID}' and H.CountyIdentifier = O.County", exchangeDb);


            //Glauch TODO: this should all be refactored to align to the C# handling in the Facets Mater class
            this.WriteToLog("Handling duplicate transactions");

            Eight34EditsAndErrors eight34Edits = new Eight34EditsAndErrors(exchangeDb, this, outputTable, GroupID);

            eight34Edits.HandleExactlyTwoTrans();

            this.WriteToLog("Now looking for cases where we have more than 2 transactions for a given member");
            eight34Edits.HandleMoreThanTwoTransactions();

            this.WriteToLog("Now looking for orphaned transactions");
            eight34Edits.FindOrphanTransactions();

            Eight34EditsAndErrors.HoldTerminationsOfTerminatedMembers(this, exchangeDb, outputTable);

            facetsMatcher.PreOutputEligibilityDataConversion(exchangeDb, GroupID);

            //after this, no more changes in SQL

            List<OutputRecord> transformedData = GetTransformedData(exchangeDb);

            TranslateLanguageAndCountyNames(transformedData);

            PopulateFrom834.GenerateEnrollmentTransactionFile(GroupID, this, Edifile, oPaths.OutputKWPath, nameStub, transformedData.Where(x => x.Output == true).ToList());

            //Note - error code translation is now happening inside the reporting generation method
            reportList.Add(EightThirtyFourReports.Generate834EnrollmentActivityReportfromMemory(this, transformedData, nameStub, GroupID, Edifile));

            string tboMessage = "";
            if (fullFile)
            {
                string TBOReportLocation = EightThirtyFourReports.GenerateTBOReportFromMemory(transformedData, this, GroupID, nameStub);
                string TBOPublishedLocation = FileTransfer.PushToSharepoint("ITReports", ProcessId, TBOReportLocation, this);
                tboMessage = "TBO Report for " + GroupID + " at " + TBOPublishedLocation + Environment.NewLine + Environment.NewLine;
            }

            PopulateFrom834.ArchiveForEnrollment(Edifile, this, oPaths.InputEnrollmentPath);
            PopulateFrom834.ArchiveForIT(Edifile, LoggerFtpFromDir, this);

            //TODO: save to the archive tables that Austin created - if needed?
            return Path.GetFileName(Edifile) + Environment.NewLine + tboMessage;
        }

        /// <summary>
        /// Mapping Exchange-specific fields that are added to the KW
        /// </summary>
        /// <param name="transformedData">Data set to be written to</param>
        private void TranslateLanguageAndCountyNames(List<OutputRecord> transformedData)
        {
            //Need Lily's list to dictionary extension
            List<LanguageMap> languageMap = ExtractFactory.ConnectAndQuery<LanguageMap>(this, exchangeDb, "Select * from IT0346_LanguageMap_C").ToList();
            List<FipsCountyMap> countyMap = ExtractFactory.ConnectAndQuery<FipsCountyMap>(this, exchangeDb, "Select * from IT0346_FipsCountyMap_C").ToList();

            foreach (ExchangeOutputRecord outputRecord in transformedData)
            {
                if (!string.IsNullOrEmpty(outputRecord.Language))
                {
                    LanguageMap matchedRecords = languageMap.Where(x => x.ISO == outputRecord.Language).FirstOrDefault();

                    if(matchedRecords == null)
                    {
                        outputRecord.ErrCode += "055,"; //could not translate provided language code
                        outputRecord.Language = "";
                    }
                    else
                    {
                        outputRecord.Language = matchedRecords.Value;
                    }
                }

                if (!string.IsNullOrEmpty(outputRecord.County))
                {
                    FipsCountyMap matchedCounty = countyMap.Where(x => outputRecord.County.Substring(0,2) == x.StateCode && outputRecord.County.TrimEnd().Substring(2,3) == x.CountyCode).FirstOrDefault();

                    if (matchedCounty == null)
                    {
                        outputRecord.ErrCode += "054,"; //no county code provided
                    }
                    else
                    {
                        outputRecord.County = matchedCounty.CountyName;
                    }
                }
                else
                {
                    outputRecord.ErrCode += "054,"; //no county code provided
                }
            }
        }

        private class LanguageMap
        {
            public string ISO { get; set; }
            public string Value { get; set; }
        }

        private class FipsCountyMap
        {
            public string State { get; set; }
            public string StateCode { get; set; }
            public string CountyCode { get; set; }
            public string CountyName { get; set; }
            public string UNK { get; set; }
        }

        private Data.AppNames GetProcessorDB(bool RunAgainstDevDb = false)
        {
            Data.AppNames ConfigTarget;
            if (TestMode)
            {
                ConfigTarget = Data.AppNames.ExampleTest;
            }
            else
            {
                ConfigTarget = Data.AppNames.ExampleProd;
            }

            return ConfigTarget;
        }


        private List<OutputRecord> GetTransformedData(Data.AppNames dataSource)
        {
            List<OutputRecord> returnData = new List<OutputRecord>();

            string pullquery = $@"select 'PHPConfg' as [DBName], * from [IT0346_Output_F]";
            List<ExchangeOutputRecord> unTypedTransformData = ExtractFactory.ConnectAndQuery<ExchangeOutputRecord>(this, dataSource, pullquery).ToList();

            foreach (ExchangeOutputRecord rec in unTypedTransformData)
            {
                returnData.Add(rec as OutputRecord);
            }

            return returnData;
        }
    }
}
