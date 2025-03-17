using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections.Concurrent;
using Utilities;
using Utilities.Eight34Outputs;
using Microsoft.SharePoint.Client;
using RestSharp.Extensions;
using System.Text.RegularExpressions;
using System.Configuration;

namespace Utilities
{
    public class SourceMatcher
    {
        public DataTable MembersPresent, MembersTermed, MembersFuture;
        public DataTable CurrentGroupMembersPresent, CurrentGroupMembersTermed, CurrentGroupMembersFuture;
        public List<string> DummySSNs;
        public Logger logger;
        public string LogFileName;
        public List<string> Logs = new List<string>();
        public ConcurrentQueue<string> textLogs = new ConcurrentQueue<string>();
        private bool writing = false;
        public string OutputTableName;
        static object locker = new Object();

        public SourceMatcher(Data.AppNames commTarget, string linkedServer, IEnumerable<string> groupIDs, Logger logger, string outputTableName)
        {
            DataWork.RunSqlCommand(logger, "DELETE FROM [dbo].[IT0354_MatchingLogs_A] WHERE LogDateTime <= GETDATE()-365", logger.LoggerPhpArchive);
            string groupIDString = "";
            foreach (string ID in groupIDs)
            {
                groupIDString += "'" + ID + "',";
            }
            groupIDString = groupIDString.Substring(0, groupIDString.Length - 1);
            string baseQuery = Select(linkedServer, groupIDString);
            Debug.WriteLine(baseQuery + Termed);
            MembersPresent = DataWork.QueryToDataTable(logger, baseQuery + Present, commTarget);
            MembersTermed = DataWork.QueryToDataTable(logger, baseQuery + Termed, commTarget);
            MembersFuture = DataWork.QueryToDataTable(logger, baseQuery + Future, commTarget);
            DummySSNs = new List<string>() { "", "000000000", "111111111", "222222222", "3333333333", "4444444444", "555555555", "666666666", "777777777", "888888888", "999999999" };
            this.logger = logger;
            LogFileName = "MemberMatchingLogs";
            logger.WriteToLog("Writing member matching info to text logs, please check the execution logs folder");
            OutputTableName = outputTableName;
        }

        public List<MatchedMember> MatchToSources<T>(Data.AppNames DB)
        {
            ConcurrentBag<MatchedMember> membersAndSubscribers;
            membersAndSubscribers = new ConcurrentBag<MatchedMember>();


            List<OutputRecord> outputTable = new List<OutputRecord>();

            outputTable = ExtractFactory.ConnectAndQuery<OutputRecord>(DB, string.Format("Select * From Output")).ToList();
       
            LoadCurrentGroupMembers(outputTable.First().GroupNo);
            List<MemberSearchDTO> subscriberSSNsAndSubNos = outputTable.Where(x => x.SubscriberFlag == "Y").AsEnumerable().Select(
                s => new MemberSearchDTO()
                {
                    SSN = s.SSN,
                    SubNo = s.SubNo,
                    GroupNo = s.GroupNo,
                    FirstName = s.FirstName,
                    LastName = s.LastName,
                    UniqueKey = s.UniqueKey,
                    SubscriberFlag = true
                }).Distinct().ToList();

            //match subscribers and update chrildren
            try
            {
                /*ParallelOptions options = new ParallelOptions()
                {
                    MaxDegreeOfParallelism = 1, 
                };*/
                Parallel.ForEach(subscriberSSNsAndSubNos/*, options*/, subscriber =>
                {
                    Console.WriteLine($"Current Thread ID: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
                    MatchedMember subscriberDetails = MatchMember(subscriber);
                    membersAndSubscribers.Add(subscriberDetails);
                    //account for nulls in uniquekey
                    OutputRecord subscriberRow;
                    if(typeof(T) == typeof(ExchangeOutputRecord))
                    {
                        subscriberRow = new ExchangeOutputRecord();
                    }
                    subscriberRow = outputTable.Where(x => x.SSN == subscriber.SSN && subscriber.UniqueKey == null ? x.UniqueKey == 0 : x.UniqueKey == subscriber.UniqueKey).First();
                    if (subscriberDetails.SubscriberID != "*")
                    {
                        subscriberRow.SubscriberID = subscriberDetails.SubscriberID;
                        subscriberRow.OriginalEffectiveDate = subscriberDetails.OriginalEffectiveDate == DateTime.MinValue ? "" : subscriberDetails.OriginalEffectiveDate.ToString("yyyyMMdd");
                        subscriberRow.MemDep = subscriberDetails.MemDepCounter.ToString();
                        subscriberRow.ShortName = subscriberDetails.MemberIDName;
                    }
                    else
                    {
                        WriteToTextLog("Could not match subscriber " + subscriber.FirstName + " " + subscriber.LastName);
                        subscriberRow.SubscriberID = "*";
                        if (DummySSNs.Contains(subscriberRow.SSN.Trim()))
                        {
                            subscriberRow.SSN = "999999999";
                        }
                    }
                    //get members for subscriber
                    OutputRecord[] membersForSubscriber = outputTable.Where(x => x.SubNo == subscriber.SubNo && x.SubscriberFlag == "N").ToArray();
                    //give members same subscriberID and update personal info
                    foreach (OutputRecord member in membersForSubscriber)
                    {
                        WriteToTextLog("Matched member " + member.FirstName + " " + member.LastName + " to subscriber " + subscriberDetails.SubscriberID);
                        member.SubscriberID = subscriberDetails.SubscriberID;
                        if (DummySSNs.Contains(member.SSN.Trim()))
                        {
                            member.SSN = "999999999";
                            WriteToTextLog("changed " + member.FirstName + " " + member.LastName + " to 999999999");
                        }
                        MatchedMember memberDetails = MatchMember(new MemberSearchDTO
                        {
                            SubscriberID = member.SubscriberID,
                            SSN = member.SSN,
                            GroupNo = member.GroupNo,
                            FirstName = member.FirstName,
                            LastName = member.LastName,
                            DateOfBirth = member.DOB,
                            Sex = member.Gender,
                            SubscriberFlag = false
                        });

                        if (memberDetails.SubscriberID != "*")
                        {
                            membersAndSubscribers.Add(memberDetails);
                            member.OriginalEffectiveDate = memberDetails.OriginalEffectiveDate == DateTime.MinValue ? "" : memberDetails.OriginalEffectiveDate.ToString("yyyyMMdd");
                            member.MemDep = memberDetails.MemDepCounter.ToString();
                            member.ShortName = memberDetails.MemberIDName;
                        }
                        else
                        {
                            WriteToTextLog("Could not match member " + member.FirstName + " " + member.LastName);
                        }
                    }
                });
            }
            catch (Exception E)
            {
                WriteToTextLog(E.InnerException.ToString(), true);
                logger.WriteToLog(E.InnerException.ToString(), UniversalLogger.LogCategory.ERROR);
                throw E.InnerException;
            }
            //if this matching has left a 'new' dependent without a subscriber, error this member out
            OutputRecord[] NoMatch = outputTable.Where(x => x.SubscriberFlag == "N" && x.SubscriberID == "*").ToArray();
            foreach (OutputRecord member in NoMatch)
            {
                OutputRecord[] PossibleSubscribers = outputTable.Where(x => x.SubscriberFlag == "Y" && x.SubNo == member.SubNo).ToArray();
                if (PossibleSubscribers.Count() == 0)
                {
                    member.ErrCode = member.ErrCode + "041,";
                }
            }

            DataWork.DeleteAllRowsFromTable(OutputTableName, DB);
            DataTable temp = DataWork.ObjectToDataTable<OutputRecord>(outputTable, true);
            temp.Columns.Remove("DBName");


            DataWork.SaveDataTableToDb(OutputTableName, temp, DB);

            WriteToTextLog("Finished Matching", true);
            return membersAndSubscribers.ToList();
        }

        public void LoadCurrentGroupMembers(string GroupID)
        {

            DataRow[] membersPresent = MembersPresent.Select("GroupID = '" + GroupID + "'");

            if(membersPresent.Any())
            {
                CurrentGroupMembersPresent = membersPresent.CopyToDataTable();
            }
            else
            {
                CurrentGroupMembersPresent = MembersPresent.Clone();
            }

            DataRow[] memberTermed = MembersTermed.Select("GroupID = '" + GroupID + "'");

            if(memberTermed.Any())
            {
                CurrentGroupMembersTermed = memberTermed.CopyToDataTable();
            }
            else
            {
                CurrentGroupMembersTermed = MembersTermed.Clone();
            }


            DataRow[] memberFuture = MembersFuture.Select("GroupID = '" + GroupID + "'");

            if(memberFuture.Any())
            {
                CurrentGroupMembersFuture = memberFuture.CopyToDataTable();
            }
            else
            {
                CurrentGroupMembersFuture = MembersFuture.Clone();
            }

        }




        public MatchedMember MatchMember(MemberSearchDTO member, int pass = 1)
        {
            logMemberMatcher(member, pass, "Attempting Match");
            IEnumerable<MatchedMember> memberDetails;
            string queryExpression;
            string terminology = member.SubscriberFlag ? "SUBSCRIBER" : "MEMBER";
            if (DummySSNs.Contains(member.SSN.Trim()) && pass == 1) //bad SSN, try matching member by attributes and error subscriber out
            {
                if (!member.SubscriberFlag)
                {
                    WriteToTextLog("Couldn't match " + member.FirstName + " " + member.LastName + " with SSN, let's try by attributes");
                    return MatchMember(member, 2);
                }
                else
                {
                    return new MatchedMember() { SubscriberID = "*", SubscriberFlag = member.SubscriberFlag };
                }
            }
            if (pass == 1 || member.SubscriberFlag) //search by SSN and group for both subscriber and member, add SBSB_ID if member
            {
                queryExpression = "MemberSSN = '" + member.SSN + "' AND GroupID = '" + member.GroupNo + "'";
                if (!member.SubscriberFlag)
                {
                    queryExpression += " AND SubscriberID='" + member.SubscriberID + "' AND MemDep <> 0";
                }
                else
                {
                    queryExpression += " AND MemDep = 0";
                }
            }
            else //attribute search
            {
                string escapedFirstName = member.FirstName.Replace("'", "''");
                string escapedLastName = member.LastName.Replace("'", "''");

                queryExpression = "SubscriberID = '" + member.SubscriberID + "' AND GroupID = '" + member.GroupNo + "' AND MemberFirstName = '" + escapedFirstName + "'"
                + " AND MemberLastName = '" + escapedLastName + "' AND MemberBirthDate = '" + DateTime.ParseExact(member.DateOfBirth, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None) + "' AND MemberSex = '" + member.Sex + "' AND MemDep <> 0";
            }
            IEnumerable<DataRow> matches;
            matches = CurrentGroupMembersPresent.Select(queryExpression); //run through present, then termed, then future like original
            if (matches.Count() == 0)
            {
                matches = CurrentGroupMembersTermed.Select(queryExpression);
                if (matches.Count() == 0)
                {
                    matches = CurrentGroupMembersFuture.Select(queryExpression);
                }
            }
            if (matches.Count() > 0)
            {
                foreach (DataRow rw in matches)
                {
                    logMemberMatcher(rw, pass, "Matched");
                }
                memberDetails = matches.AsEnumerable().Select(s =>
                new MatchedMember()
                {
                    SubscriberID = s.Field<string>(""),
                    OriginalEffectiveDate = s.Field<DateTime>(""),
                    MemDepCounter = s.Field<Int16>(""),
                    MemberIDName = s.Field<string>(""),
                    SubscriberFlag = member.SubscriberFlag,
                });
                if (memberDetails.Select(s => s.SubscriberID.Trim()).AsEnumerable().Distinct().Count() > 1) //if there's more than 1 SBSB and they're not all the same
                {
                    WriteToTextLog(terminology + " MISMATCH: " + member.FirstName + " " + member.LastName + " HAS TOO MANY MATCHES: " + string.Join(",", memberDetails.Select(s => s.SubscriberID)));
                    SendAlerts.Send(logger.ProcessId, 4, "Multiple SBSB_ID for " + terminology.ToLower(), member.FirstName + " " + member.LastName + " has too many matches: " + string.Join(",", memberDetails.Select(s => s.SubscriberID)) + " (group " + member.GroupNo + ")", logger);
                }
                return memberDetails.OrderByDescending(s => s.SubscriberID).First();
            }
            else
            {
                if (pass == 1 && !member.SubscriberFlag) //couldn't find SSN match, try attribute match
                {
                    WriteToTextLog("Couldn't match " + member.FirstName + " " + member.LastName + " with SSN, let's try by attributes");
                    return MatchMember(member, 2);
                }
                else
                {
                    return new MatchedMember() { SubscriberID = "*", SubscriberFlag = member.SubscriberFlag };
                }
            }
        }

        private void logMemberMatcher(object member, int pass, string match)
        {
            string log = match + " - pass " + pass.ToString() + ":: ";
            if (member.GetType() == typeof(DataRow))
            {
                DataRow row = (DataRow)member;
                foreach (DataColumn col in row.Table.Columns)
                {
                    log += col.ColumnName + ": " + row[col].ToString() + "; ";
                }
            }
            else
            {
                foreach (PropertyInfo pi in member.GetType().GetProperties())
                {
                    log += pi.Name + ": " + pi.GetValue(member) + "; ";
                }
            }

            WriteToTextLog(log);
        }

        public class MatchedMember
        {
            public string SubscriberID { get; set; }
            public DateTime OriginalEffectiveDate { get; set; }
            public Int16 MemDepCounter { get; set; }
            public string MemberIDName { get; set; }
            public bool SubscriberFlag { get; set; }
        }

        public class MemberSearchDTO
        {
            public string SSN { get; set; }
            public string SubNo { get; set; }
            public string SubscriberID { get; set; }
            public string GroupNo { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string DateOfBirth { get; set; }
            public string Sex { get; set; }
            public int? UniqueKey { get; set; }
            public bool SubscriberFlag { get; set; }

        }

        public string Select(string linkedServer, string groupIDString)
        {
            return string.Format(@"", linkedServer, groupIDString);
        }

        string Present = @"";

        string Termed = @"";

        string Future = @"";

        public void PreOutputEligibilityDataConversion(Data.AppNames DB, string GroupId)
        {
            List<OutputRecord> outputData = GetOutputRecordsFromApplicableDatabase(logger, DB, GroupId, OutputTableName);

            //grabbing this up here so we can check it on each record
            DataTable rAndEMapping = ExtractFactory.ConnectAndQuery(logger, logger.LoggerPhpConfig, "SELECT * FROM [dbo].[IT0354_RaceAndEthnicityMapping_C]");

            PopulateClassAndPrimaryDataPointsForAllMembers(GroupId, outputData);

            CheckForErrorsAndFinalOutputTranslations(GroupId, outputData, rAndEMapping);

            BlockContractsWithOutOfAreaDependents(GroupId, outputData);

            WriteToTextLog("Done with eligibility conversion, error subscribers with more members than on file (if they exist)");
            outputData = ErrorMoreMembersThanOnFile(outputData);

            SaveCSharpTranslationsBacktoDatabase(DB, GroupId, outputData, OutputTableName);

            WriteToTextLog("", true);
        }

        /// <summary>
        /// Loops through the input data again to assess bad data/error conditions or other simple translations. Want these to happen at the end to catch any errors defined earlier
        /// </summary>
        /// <param name="GroupId"></param>
        /// <param name="outputData"></param>
        /// <param name="rAndEMapping"></param>
        private void CheckForErrorsAndFinalOutputTranslations(string GroupId, List<OutputRecord> outputData, DataTable rAndEMapping)
        {
            foreach (OutputRecord member in outputData.OrderBy(x => x.SubscriberID).ThenBy(x => x.MemDep))
            {

                List<OutputRecord> thisFam = outputData.Where(x => x.SubNo == member.SubNo).ToList();

                AlertForMembersWithCoordinationOfBenefits(member);

                MarkCobraMembers(member);

                BlockBadSSN(member);

                BlockDuplicatesUnlessExchange(GroupId, member, thisFam);

                PopulateShortName(member);

                TranslateRaceAndEthnicity(rAndEMapping, member);

                FlagNewBornsForManualEntry(member);

                BlockActiveCobra(member);

                BlockOriginalEffectiveDateAfterCoverageEffectiveDate(member);

                BlockCoverageEffDateEarlierThanMostRecentFacetsTermination(member);

                BlockMissingKeyDates(member);

                BlockMembersAssociatedtoBlockedSubscribers(member, thisFam);

                BlockMembersMissingSubscribers(member, thisFam);

                member.ErrCode = member.ErrCode.Replace(" ", "");


                //GLAUCH Note: we should implement this for IT_0354 in the future
                if (logger.ProcessId == "IT_0346")
                {
                    ConvertDatesToOutputFormat(member);
                }
            }
        }

        /// <summary>
        /// Runs through the input data again, leveraging dependent settings in the previous method to assess dependent differences with the family and/or subscriber
        /// </summary>
        /// <param name="GroupId"></param>
        /// <param name="outputData"></param>
        private static void BlockContractsWithOutOfAreaDependents(string GroupId, List<OutputRecord> outputData)
        {
            foreach (OutputRecord member in outputData.OrderBy(x => x.SubscriberID).ThenBy(x => x.MemDep))
            {
                List<OutputRecord> thisFam = outputData.Where(x => x.SubNo == member.SubNo).ToList();

                if (member.SubscriberFlag == "Y")
                {
                    //Exchange has situations where we have duplicate members & subscribers that appear in the file (in different transaction sets) that are pulled in the thisFam array -
                    //in this case we are using the sequence of the unique key and the dependent counter to find the subscriber record that immediately
                    //precedes the given member - if there are more than one instances of a subscriber on the file
                    int maxMemDep = thisFam.Max(x => Convert.ToInt32(x.MemDep));

                    IEnumerable<OutputRecord> dependentsForSubscriber = thisFam.Where(x => x.SubscriberFlag == "N" && x.UniqueKey <= member.UniqueKey + maxMemDep);

                    AlertOutOfAreaDependent(member, dependentsForSubscriber);
                }


            }
        }

        /// <summary>
        /// This method does primary mapping, product assignments and eligibility changes for all members on the file (pre-requisite to later processing)
        /// </summary>
        /// <param name="GroupId"></param>
        /// <param name="outputData"></param>
        private void PopulateClassAndPrimaryDataPointsForAllMembers(string GroupId, List<OutputRecord> outputData)
        {
            foreach (OutputRecord member in outputData.OrderBy(x => x.SubscriberID).ThenBy(x => x.MemDep))
            {

                //Glauch Note: there is a risk here for Exchange that this look-up pulls a lot more than just the family associated to that transaction
                List<OutputRecord> thisFam = outputData.Where(x => x.SubNo == member.SubNo).ToList();

                BlockForMismatchingClassOrSubGroups(GroupId, member, thisFam);

                WriteToTextLog("Beginning eligibility conversions for " + member.FirstName + " " + member.LastName);
                //Set output field in output table to initially pass all members
                member.Action = "AP";

                BlockFileTransactionSetForCommercial(GroupId, member);

                object o = GetMemDate(member, true);
                object term = GetMemDate(member, false);

                member.OriginalEffectiveDate = o.ToString() == DateTime.MinValue.ToString("yyyyMMdd") ? "" : o.ToString();
                member.LatestInternalTermDate = term.ToString() == DateTime.MaxValue.ToString("MM/dd/yyyy") ? "" : term.ToString();
                member.CovEffDate = GetCovEffDate(member);


                member.ClassPlanPharmID = "";//check this

                if (member.SubscriberFlag == "Y" || GroupId == "")
                {
                    member.CoverageLevelOut = CalculateCoverageLevel(member, thisFam, logger);
                }

                member.EligAction = GetEligAction(member);

                member.SubscriberTransactions = member.SubscriberFlag == "Y" && (member.EligAction == "" || member.EligAction == "");

                BlockMemberTerminationsIfSubscriberEnrolling(member, thisFam);

                if (GroupId != "" && GroupId != "")
                {
                    member.SubscriberMailingAddressType = string.IsNullOrWhiteSpace(member.MailingAddressOne) ? "H" : "1";//If a mailing address is populated, point the subscriber's mailing address flag to our mailing address code
                }
                else
                {
                    member.SubscriberMailingAddressType = string.IsNullOrWhiteSpace(member.MailingAddressOne) || member.SubscriberFlag == "N" ? "H" : "1";//If a mailing address is populated, point the subscriber's mailing address flag to our mailing address code
                }

                MedicareEGWPErrorChecks(GroupId, member, thisFam);

            }
        }

        private void ConvertDatesToOutputFormat(OutputRecord member)
        {
            if (!String.IsNullOrWhiteSpace(member.CovEffDate))
            {
                member.CovEffDate = DateTime.ParseExact(member.CovEffDate, "yyyyMMdd", CultureInfo.InvariantCulture).ToString("MM/dd/yyyy");
            }

            if (!String.IsNullOrWhiteSpace(member.CovEndDate))
            {
                member.CovEndDate = DateTime.ParseExact(member.CovEndDate, "yyyyMMdd", CultureInfo.InvariantCulture).ToString("MM/dd/yyyy");
            }


            if (!String.IsNullOrWhiteSpace(member.LatestInternalTermDate))
            {
                member.LatestInternalTermDate = DateTime.ParseExact(member.LatestInternalTermDate, "yyyyMMdd", CultureInfo.InvariantCulture).ToString("MM/dd/yyyy");
            }


            if (!String.IsNullOrWhiteSpace(member.DOB))
            {
                member.DOB = DateTime.ParseExact(member.DOB, "yyyyMMdd", CultureInfo.InvariantCulture).ToString("MM/dd/yyyy");
            }

            if (!String.IsNullOrWhiteSpace(member.EnrollElig))
            {
                member.EnrollElig = DateTime.ParseExact(member.EnrollElig, "yyyyMMdd", CultureInfo.InvariantCulture).ToString("MM/dd/yyyy");
            }

            if (!String.IsNullOrWhiteSpace(member.FileDate))
            {
                member.FileDate = DateTime.ParseExact(member.FileDate, "yyyyMMdd", CultureInfo.InvariantCulture).ToString("MM/dd/yyyy");
            }

            if (!String.IsNullOrWhiteSpace(member.OriginalEffectiveDate))
            {
                member.OriginalEffectiveDate = DateTime.ParseExact(member.OriginalEffectiveDate, "yyyyMMdd", CultureInfo.InvariantCulture).ToString("MM/dd/yyyy");
            }


            if (member is ExchangeOutputRecord)
            {
                ExchangeOutputRecord excMember = member as ExchangeOutputRecord;

                if (!String.IsNullOrWhiteSpace(excMember.ChangeEffDt))
                {
                    excMember.ChangeEffDt = DateTime.ParseExact(excMember.ChangeEffDt, "yyyyMMdd", CultureInfo.InvariantCulture).ToString("MM/dd/yyyy");
                }
                
            }
        }

        private static void BlockForMismatchingClassOrSubGroups(string GroupId, OutputRecord member, List<OutputRecord> thisFam)
        {
            if (thisFam.Any(x => x.ClassCode != member.ClassCode) && GroupId == "")
            {
                member.Output = false;
                member.ErrCode += "048,";
            }
            if (thisFam.Any(x => x.SubGroup != member.SubGroup) && GroupId == "")
            {
                member.Output = false;
                member.ErrCode += "049,";
            }
        }

        private static void BlockFileTransactionSetForCommercial(string GroupId, OutputRecord member)
        {
            if (GroupId != "" && GroupId != "")
            {
                if (member.FileTransactionSet != "00")
                {
                    //Do not pass if file is other than the original
                    member.Output = false;
                    member.ErrCode += "034,";
                }
            }
        }

        private static void MedicareEGWPErrorChecks(string GroupId, OutputRecord member, List<OutputRecord> thisFam)
        {
            if (GroupId == "")
            {
                if (member.ClassCode == "" || member.ClassCode == "" || member.ClassCode == "")
                {
                    if (!thisFam.Any(x => x.MedicarePlanCode == "C"))
                    {
                        member.ErrCode += "060,";
                        member.Output = false;
                    }
                    if (member.ClassCode == "4000" && thisFam.Any(x => x.MedicarePlanCode != "C"))
                    {
                        member.ErrCode += "061,";
                        member.Output = false;
                    }
                }
                else
                {
                    if (thisFam.Any(x => x.MedicarePlanCode == "C"))
                    {
                        member.ErrCode += "064,";
                    }
                }
                if (!string.IsNullOrWhiteSpace(member.MedicareBeneficiaryIndicator) && member.MedicarePlanCode == "E")
                {
                    member.ErrCode += "062,";
                }
                if (string.IsNullOrWhiteSpace(member.MedicareBeneficiaryIndicator) && member.MedicarePlanCode == "C")
                {
                    member.ErrCode += "063,";
                    member.Output = false;
                }
            }
        }

        /// <summary>
        /// If the subscriber passed in has at least one dependent that is out of area (based on CSPI_ID), the whole family needs to be blocked
        /// </summary>
        /// <param name="member">The subscriber record to assess the family</param>
        /// <param name="thisFam">The dependent records (excluding the subscriber) to check for any out of area dependents</param>
        private static void AlertOutOfAreaDependent(OutputRecord subscriber, IEnumerable<OutputRecord> dependentsForSubscriber)
        {
            if ((dependentsForSubscriber.Any(x => x.ClassPlanID != "" && x.ClassPlanID != "MED00001" && x.ClassPlanID != "MCR00001" && x.ClassPlanID != "COBRA") || dependentsForSubscriber.Any(x => x.ClassPlanID == "")))
            {

                subscriber.WouldHaveOutput = subscriber.Output;
                subscriber.Output = false;
                subscriber.ErrCode += "065,";
                subscriber.OOADeps = true;

                foreach (OutputRecord dependent in dependentsForSubscriber)
                {
                    dependent.WouldHaveOutput = dependent.Output;
                    dependent.OOADeps = true;
                    dependent.ErrCode += "065,";
                    dependent.Output = false;
                }
                
            }

        }

        private static void BlockBadSSN(OutputRecord member)
        {
            if (member.SubscriberFlag == "Y" && member.SSN == "999999999")
            {
                member.Output = false;
                member.ErrCode += "046,";
            }
        }

        private static void AlertForMembersWithCoordinationOfBenefits(OutputRecord member)
        {
            if ((!string.IsNullOrWhiteSpace(member.COBBegDate) || !string.IsNullOrWhiteSpace(member.COBBegDate1) || !string.IsNullOrWhiteSpace(member.COBBegDate2)
                 || !string.IsNullOrWhiteSpace(member.COBBegDate3) || !string.IsNullOrWhiteSpace(member.COBBegDate4) || !string.IsNullOrWhiteSpace(member.COBBegDate5))
                 && !member.ErrCode.Contains("020"))
            {
                member.ErrCode += "020,";
            }
        }

        private static void MarkCobraMembers(OutputRecord member)
        {
            if (member.BenefitStatus == "C")
            {
                member.Output = false;
                member.ErrCode += "026,";
                member.EligAction = "CB";
                member.ClassPlanID = "COBRA";
                member.ClassPlanPharmID = "";
            }
        }

        /// <summary>
        /// Given how Subscriber Eligiblity Events (SBELs) work, the Subscriber Eligiblity Action "EN" will overwrite the 
        /// Member Eligibility Action (MEEL) that in this case would be trying to term the member. Given that, we'll block the member transaction 
        /// and send it to Enrollment for manual work
        /// </summary>
        /// <param name="member">Member Output Record Being Parsed</param>
        /// <param name="thisFam">Family List Array (note handling in IF statement to protect against duplicate family sets on one file)</param>
        private static void BlockMemberTerminationsIfSubscriberEnrolling(OutputRecord member, List<OutputRecord> thisFam)
        {
            if (member.SubscriberFlag != "Y" && member.EligAction == "TERM" && thisFam.Any(x => x.SubscriberFlag == "Y" && x.EligAction == "ENROLL"
                && x.SubscriberTransactions == true && x.Output == true && member.UniqueKey > x.UniqueKey && member.UniqueKey <= x.UniqueKey + Convert.ToInt32(member.MemDep)))
            {//We're not going to turn on MEELs if there is an SBEL, so let's tag them with an error code.
                member.ErrCode += "038,";
                member.MemberTransactions = false;
            }
        }

        private static void BlockDuplicatesUnlessExchange(string GroupId, OutputRecord member, List<OutputRecord> thisFam)
        {
            if (GroupId != "" && GroupId != "" && thisFam.Any(x => x.MemDep == member.MemDep && x.UniqueKey < member.UniqueKey && x.Output == true && !x.ErrCode.Contains("036") && !x.ErrCode.Contains("TBO")))
            {
                member.Output = false;
                member.ErrCode += "022,";
            }
        }

        private void PopulateShortName(OutputRecord member)
        {
            if(member.SubscriberFlag == "Y")
            {
                member.ShortName = " ";
            }
            else
            {
                if (String.IsNullOrWhiteSpace(member.ShortName))
                {
                    member.ShortName = member.ShortName.Left(6);
                }
            }
        }

        private void BlockMembersMissingSubscribers(OutputRecord member, List<OutputRecord> thisFam)
        {
            //This can theoretically happen if our 'add missing subscriber function' failed to math a dependent to a contract and
            //create the necessary subscriber record to generate the neede SBSB record
            if (member.SubscriberFlag == "N" && !thisFam.Any(x => x.SubscriberFlag == "Y"))
            {
                member.ErrCode += "035,";
                member.Output = false;
            }
        }

        private void BlockCoverageEffDateEarlierThanMostRecentFacetsTermination(OutputRecord member)
        {
            if (member.LatestInternalTermDate != "" && DateTime.ParseExact(member.LatestInternalTermDate, "yyyyMMdd", CultureInfo.InvariantCulture) < DateTime.Today && 
                DateTime.ParseExact(member.CovEffDate, "yyyyMMdd", CultureInfo.InvariantCulture) < DateTime.ParseExact(member.LatestInternalTermDate, "yyyyMMdd", CultureInfo.InvariantCulture))
            {
                member.ErrCode += "047,";
                member.Output = false;

            }
        }

        private void BlockMissingKeyDates(OutputRecord member)
        {

            if (member.SubscriberFlag == "Y" && String.IsNullOrWhiteSpace(member.EnrollElig) && String.IsNullOrWhiteSpace(member.OriginalEffectiveDate))
            {
                member.ErrCode += "032,";
                member.Output = false;
            }

            if (member.SubscriberFlag == "Y" && String.IsNullOrWhiteSpace(member.ClassCode))
            {
                member.ErrCode += "023,";
                member.Output = false;
            }

            if (member.SubscriberFlag == "Y" && String.IsNullOrWhiteSpace(member.SubGroup))
            {
                member.ErrCode += "024,";
                member.Output = false;
            }

            //Glauch - I don't think this gets hit, but there was an odd OR in the SQL so I'm leaving it
            if (member.SubscriberFlag == "Y" && String.IsNullOrWhiteSpace(member.CovEffDate))
            {
                member.ErrCode += "024,";
                member.Output = false;
            }
        }

        private void BlockOriginalEffectiveDateAfterCoverageEffectiveDate(OutputRecord member)
        {
            if (!String.IsNullOrWhiteSpace(member.OriginalEffectiveDate) && DateTime.ParseExact(member.OriginalEffectiveDate, "yyyyMMdd", CultureInfo.InvariantCulture) > DateTime.ParseExact(member.CovEffDate, "yyyyMMdd", CultureInfo.InvariantCulture))
            {
                member.ErrCode += "029,";
                member.Output = false;
            }
        }

        private static void BlockMembersAssociatedtoBlockedSubscribers(OutputRecord member, List<OutputRecord> thisFam)
        {
            //If dependents can't find any subscribers being output, turn the dependent off. Note, for exchange this might be a subscriber
            //coming in on a different transaction set than the original (since we receive duplicates there), and then since we merge duplicates
            //there are situations where we might turn off the "natural" subscriber the dependent came in on - that's OK
            if (member.SubscriberFlag != "Y" && !thisFam.Any(x => x.SubscriberFlag == "Y" && x.Output && x.BenefitStatus == member.BenefitStatus))
            {
                member.Output = false;
                member.ErrCode += "028,";
            }
        }

        private void BlockActiveCobra(Eight34Outputs.OutputRecord member)
        {
            if (member.MaintenanceRSN == "09" && (member.BenefitStatus == "A" || member.BenefitStatus == "ACTI") && member.MaintenanceCode == "021")
            {
                member.ErrCode += "038,";
                member.Output = false;
            }
        }

        private void FlagNewBornsForManualEntry(Eight34Outputs.OutputRecord member)
        {
            //Glauch - not sure why this isn't "Baby " but the original SQL uses an _ to reflect a single charater. ? is the equivalent in C#.
            string pattern = @"^baby .+";
            if (Regex.Match(member.FirstName, pattern, RegexOptions.IgnoreCase).Success)
            {
                member.ErrCode += "019,";
                member.Output = false;
            }
            
        }

        public static void SaveCSharpTranslationsBacktoDatabase(Data.AppNames DB, string GroupId, List<Eight34Outputs.OutputRecord> outputTable, string outputTableName)
        {
            DataWork.TruncateWorkTable(outputTableName, DB);

            if (GroupId == "" || GroupId == "")
            {
                DataTable temp = DataWork.ObjectToDataTable<Eight34Outputs.OutputRecord, Eight34Outputs.ExchangeOutputRecord>(outputTable, true);
                temp.Columns.Remove("DBName");
                List<string> colMappings = new List<string>();

                foreach (DataColumn col in DataWork.GetTableSchema(outputTableName, DB).Columns)
                {
                    colMappings.Add(col.ColumnName);
                }
                DataWork.SaveDataTableToDb(outputTableName, temp, DB, colMappings, colMappings);
            }
            else
            {
                DataTable temp = DataWork.ObjectToDataTable(outputTable, true);
                temp.Columns.Remove("DBName");
                DataWork.SaveDataTableToDb(outputTableName, temp, DB);
            }
        }

        private void TranslateRaceAndEthnicity(DataTable rAndEMapping, OutputRecord member)
        {
            if (!string.IsNullOrWhiteSpace(member.InputRace) || !string.IsNullOrWhiteSpace(member.InputEthnicity))
            {
                if (!string.IsNullOrWhiteSpace(member.InputRace))
                {
                    List<string> values = rAndEMapping.AsEnumerable().Where(x => x["834Value"].ToString() == member.InputRace).Select(x => x["SourceValue"].ToString()).ToList();
                    if (values.Count() == 1)
                    {
                        member.Race = values[0];
                    }
                    else if (values.Count() > 1)
                    {
                        WriteToTextLog(member.FirstName + " " + member.LastName + " too many race matches found: " + values.ToDelimitedString());
                    }
                    else
                    {
                        WriteToTextLog(member.FirstName + " " + member.LastName + " no race matches found: ");
                    }
                }

                if (!string.IsNullOrWhiteSpace(member.InputEthnicity))
                {
                    List<string> values = rAndEMapping.AsEnumerable().Where(x => x["834Value"].ToString() == member.InputEthnicity).Select(x => x["SourceValue"].ToString()).ToList();
                    if (values.Count() == 1)
                    {
                        member.Ethnicity = values[0];
                    }
                    else if (values.Count() > 1)
                    {
                        WriteToTextLog(member.FirstName + " " + member.LastName + " too many race Ethnicity found: " + values.ToDelimitedString());
                    }
                    else
                    {
                        WriteToTextLog(member.FirstName + " " + member.LastName + " no race Ethnicity found: ");
                    }
                }
            }
        }

        public static List<Eight34Outputs.OutputRecord> GetOutputRecordsFromApplicableDatabase(Logger caller, Data.AppNames DB, string GroupId, string outputTableName)
        {
            List<Eight34Outputs.OutputRecord> outputTable = new List<Eight34Outputs.OutputRecord>();
            string dbName = (GroupId == "" || GroupId == "") ? "PHPConfg" : "";

            string queryToLoadOutputRecords = $"Select '{dbName}' as [DBName], * From {outputTableName}";
            if ((GroupId == "" || GroupId == ""))
            {
                
                List<Eight34Outputs.ExchangeOutputRecord> outputTable2 = ExtractFactory.ConnectAndQuery<Eight34Outputs.ExchangeOutputRecord>(caller, DB, queryToLoadOutputRecords).ToList();
                foreach (Eight34Outputs.ExchangeOutputRecord rec in outputTable2)
                {
                    outputTable.Add(rec);
                }
            }
            else
            {
                outputTable = ExtractFactory.ConnectAndQuery<Eight34Outputs.OutputRecord>(caller, DB, queryToLoadOutputRecords).ToList();

            }

            return outputTable;
        }


        protected class DuplicateMember
        {
            public string SubscriberID { get; set; }
            public string MemDep { get; set; }
            public string MemberRelationship { get; set; }
        }

        public static string CalculateCoverageLevel(Eight34Outputs.OutputRecord thisMem, List<Eight34Outputs.OutputRecord> Family, Logger caller)
        {
            IEnumerable<DuplicateMember> duplicatedSet = Family.GroupBy(x => new { x.SubscriberID, x.MemDep, x.MemberRelationship }).Where(g => g.Count() > 1).Select(z => new DuplicateMember() { SubscriberID = z.Key.SubscriberID, MemDep = z.Key.MemDep, MemberRelationship = z.Key.MemberRelationship });

            if(duplicatedSet.Count() == 1)
            {
                DuplicateMember singleDup = duplicatedSet.First();
                if(singleDup.SubscriberID == thisMem.SubscriberID && singleDup.MemDep == thisMem.MemDep && singleDup.MemberRelationship == thisMem.MemberRelationship)
                {
                    OutputRecord DuplicateSingleRecord = Family.Where(x => x.SubscriberID == singleDup.SubscriberID && x.MemDep == singleDup.MemDep && x.MemberRelationship == singleDup.MemberRelationship && x.UniqueKey != thisMem.UniqueKey).First();
                    Family.Remove(DuplicateSingleRecord);
                }
                else
                {
                    Family = DropDuplicateRecordsNotToBeOutput(thisMem, Family, caller);

                }
            }
            else if(duplicatedSet.Count() > 1)
            {
                Family = DropDuplicateRecordsNotToBeOutput(thisMem, Family, caller);
            }

            Family = Family.OrderBy(x => x.MemDep).ToList();

            List<Eight34Outputs.OutputRecord> famOnPlan = Family.Where(x => (x.MedicarePlanCode == thisMem.MedicarePlanCode && thisMem.MedicarePlanCode != "C")
                || (thisMem.MedicarePlanCode == "C" && x.MemDep == thisMem.MemDep)).OrderBy(x => x.MemDep).ToList();

            List<string> EgpwClassCodes = new List<string>() { "", "", "" };

            if ((EgpwClassCodes.Any(x => x == thisMem.ClassCode && thisMem.SubGroup == "1003") 
                || (Family.Any(x => x.MedicarePlanCode == "C" && EgpwClassCodes.Any(y => y == x.ClassCode && x.SubGroup == "1003"))))
                 && Family[0].GroupNo == ""
                )
            {
                switch (famOnPlan.Count)
                {
                    case 1:
                        if (famOnPlan.Where(x => x.Relationship == "18").Count() == 1)
                        {
                            return "C";//Just a subscriber
                        }
                        else if (famOnPlan.Where(x => x.Relationship == "01").Count() == 1)
                        {
                            return "F";
                        }
                        else
                        {
                            return "G";
                        }
                    case 2:
                        if (famOnPlan.Where(x => x.Relationship == "01" || x.Relationship == "18").Count() == 2)
                        {
                            return "B";//Subscriber and spouse
                        }
                        else if (famOnPlan.Where(x => x.Relationship == "18").Count() == 1)
                        {
                            return "D";//subscriber and family
                        }
                        else if (famOnPlan.Where(x => x.Relationship == "01").Count() == 1)
                        {
                            return "E";//spouse and family
                        }
                        else
                        {
                            return "G";//family only
                        }
                    default:
                        if (famOnPlan.Where(x => x.Relationship == "01" || x.Relationship == "18").Count() == 0)
                        {
                            return "G";//family only
                        }
                        else if (famOnPlan.Where(x => x.Relationship == "18").Count() == 1 && famOnPlan.Where(x => x.Relationship == "01").Count() == 0)
                        {
                            return "D";//Subscriber and family
                        }
                        else if (famOnPlan.Where(x => x.Relationship == "01").Count() == 1 && famOnPlan.Where(x => x.Relationship == "18").Count() == 0)
                        {
                            return "E";//spouse and family
                        }
                        else
                        {
                            return "A";//Family
                        }
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(thisMem.CoverageLevelOut))
                {
                    if ((thisMem.CoverageLevel == "EMP" || thisMem.CoverageLevel == "IND")
                        || (Family.Count == 1 && thisMem.CoverageLevel == ""))
                    {
                        return "C";
                    }
                    else
                    {
                        return "A";
                    }
                }
                else
                {
                    return thisMem.CoverageLevelOut;
                }
            }
        }

        private static List<Eight34Outputs.OutputRecord> DropDuplicateRecordsNotToBeOutput(Eight34Outputs.OutputRecord thisMem, List<Eight34Outputs.OutputRecord> Family, Logger caller)
        {
            IEnumerable<DuplicateMember> duplicatedOutput = Family.Where(m => m.Output).GroupBy(x => new { x.SubscriberID, x.MemDep, x.MemberRelationship }).Where(g => g.Count() > 1).Select(z => new DuplicateMember() { SubscriberID = z.Key.SubscriberID, MemDep = z.Key.MemDep, MemberRelationship = z.Key.MemberRelationship });

            if (duplicatedOutput.Count() == 0)
            {
                Family = Family.Where(x => x.Output).ToList();
            }
            else
            {
                caller.WriteToLog("Unable to De-Duplicate SBSB_ID " + thisMem.SubscriberID + " in Calculating Coverage Level", UniversalLogger.LogCategory.ERROR);
            }

            return Family;
        }

        //EffectiveOrTerm -> true will look for earliest effective date while false will look for max term date.
        public object GetMemDate(Eight34Outputs.OutputRecord member, bool EffectiveOrTerm)
        {
            //Austin has documentation on the newest way, not gonna bother with it here since it'll probably be obsolete by the time anyone else reads this
            string memQuery = "GroupID = '" + member.GroupNo + "' AND SubscriberID = '" + member.SubscriberID + "' AND MemDep = " + member.MemDep;
            CultureInfo provider = CultureInfo.InvariantCulture;
            DateTime incomingEarliestEffDate;


            bool gotDateFrom834 = DateTime.TryParseExact(EffectiveOrTerm ? member.EarliestEffDate : member.CovEndDate, "yyyyMMdd", provider, DateTimeStyles.None, out incomingEarliestEffDate);

            List<DateTime> potentialEffOrTermDate = new List<DateTime>();

            DataRow[] matchesPresent = CurrentGroupMembersPresent.Select(memQuery);
            DataRow[] matchesTermed = CurrentGroupMembersTermed.Select(memQuery);
            DataRow[] matchesFuture = CurrentGroupMembersFuture.Select(memQuery);


            DateTime EffOrTermDate;
            if (potentialEffOrTermDate.Count() > 0)
            {
                EffOrTermDate = EffectiveOrTerm ? potentialEffOrTermDate.Min() : potentialEffOrTermDate.Max();
            }
            else
            {
                EffOrTermDate = incomingEarliestEffDate;
                if (EffOrTermDate.ToString("yyyyMMdd") == "00010101")
                {
                    return "";
                }
            }
            return EffOrTermDate.ToString("yyyyMMdd");
        }

        private static string Get834MemberRelationship(Eight34Outputs.OutputRecord Member)
        {
            if (Member.MemberRelationship == "M")
            {
                return "18";
            }
            if (Member.MemberRelationship == "H" || Member.MemberRelationship == "W")
            {
                return "01";
            }
            if (Member.MemberRelationship == "S" || Member.MemberRelationship == "D")
            {
                return "19";
            }
            return "19";
        }

        public string GetEligAction(Eight34Outputs.OutputRecord member)
        {
            //need to implement
            return "";
        }

        public string GetCovEffDate(Eight34Outputs.OutputRecord member)
        {
            string maintenanceCode = member.MaintenanceCode;
            if (maintenanceCode == "024" || (maintenanceCode == "030" && !string.IsNullOrWhiteSpace(member.CovEndDate)))
            {
                return member.CovEndDate;
            }
            else
            {
                return member.CovEffDate;
            }
        }



        public void AssignMemeSfxForNewMembers(Data.AppNames DB)
        {
            
        }

        public List<Eight34Outputs.OutputRecord> ErrorMoreMembersThanOnFile(List<OutputRecord> outputTable)
        {

            return outputTable;
        }

        private void WriteToTextLog(string logContent, bool flush = false)
        {//Still want these text logs, but can't write to them at from multiple threads at the same time. This buffers it then does a bunch at once. 
            //Probably a better way to go about this, I haven't found it. Potential issue I see is that we could lose text logs on a crash, or if major changes 
            //happen without handling the flushing properly.
            //textLogs.Add(logContent); 
            textLogs.Enqueue(logContent);
            if (flush)
            {

                while (writing)
                {
                    System.Threading.Thread.Sleep(new TimeSpan(0, 1, 0));
                }
                writing = true;
                lock (locker)
                {
                    string t = System.Threading.Thread.CurrentThread.ManagedThreadId.ToString();
                    int count = textLogs.Count;
                    string qLog = "";
                    for (int i = 0; i < count; i++)
                    {
                        textLogs.TryDequeue(out qLog);
                        Logs.Add(qLog);
                    }
                    Task.Run(() => {
                        List<string> localLogs = new List<string>(Logs);
                        Logs.Clear();
                        writing = false;
                        List <MatchingLogs> matchingLogs = new List<MatchingLogs>();
                        foreach (string log in localLogs)
                        {
                            MatchingLogs mlog = new MatchingLogs
                            {
                                JobIndex = logger.ProcessId,
                                LogCategory = "INFO",
                                LoggedByUser = string.IsNullOrWhiteSpace(logger.requestedBy) ? System.Security.Principal.WindowsIdentity.GetCurrent().Name : logger.requestedBy,
                                LogContent = log,
                                UID = logger.UniqueID
                            };
                            matchingLogs.Add(mlog);
                            //Logger.WriteToLog(log);
                        }
                        DataWork.SaveDataTableToDb("[dbo].[IT0354_MatchingLogs_A]", DataWork.ObjectToDataTable(matchingLogs), logger.LoggerPhpArchive);
                    });                          
                }
                    
            }
        }

        private class MatchingLogs
        {
            public string JobIndex { get; set; }
            public DateTime LogDateTime { get { return DateTime.Now; } }
            public string LogCategory { get; set; }
            public string LoggedByUser { get; set; }
            public string LogContent { get; set; }
            public string UID { get; set; }
        }
    }
}
