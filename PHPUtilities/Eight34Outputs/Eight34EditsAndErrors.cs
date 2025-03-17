using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Utilities.Eight34;

namespace Utilities.Eight34Outputs
{
    public class Eight34EditsAndErrors
    {

        private Data.AppNames targetDb { get; set; }
        private Logger callingProcess { get; set; }
        private string outputTable { get; set; }
        private string GroupId { get; set; }

        public Eight34EditsAndErrors(Data.AppNames targetDatabase, Logger caller, string outTable, string groupId)
        {
            targetDb = targetDatabase;
            callingProcess = caller;
            outputTable = outTable;
            GroupId = groupId;
        }

        /// <summary>
        /// Do Not Pass Termination Transactions If the Member is Already Termed On That Day
        /// </summary>
        public static void HoldTerminationsOfTerminatedMembers(Logger caller, Data.AppNames DbTarget, string outputTable)
        {
            string handleEGWP = caller.ProcessId == "IT_0354" ? "OR (o.GroupNo = '' AND\tClassProductCategory = 'E' AND o.MedicarePlanCode = 'C')" : "";

            string UpdateQuery = $@"update o
                    set ErrCode = ltrim(rtrim(ErrCode)) + '012,', o.Output = 0
                    from {outputTable} o 
                    inner join [dbo].[vMostRecentActiveMemberTimelines] member
	                    on o.SBSB_ID = member.SubscriberId and cast(o.MemDep as int) = cast(member.MemberSuffix as int) and TerminationDateKey = o.CovEndDate
                    where ClassProductCategory = 'M' {handleEGWP}";

            DataWork.RunSqlCommand(caller, UpdateQuery, DbTarget, 0);
        }

        private string TwoTransForMembers()
        {

            string querytoReturn = $@"select SubNo, LastName, FirstName, SSN
                                                    from {outputTable} 
                                                    group by SubNo, LastName, FirstName, SSN
                                                    having count(*) = 2";

            return querytoReturn;
        }

        private string GreaterThanTwoTransForMembers()
        {

            return $@"select SubNo, LastName, FirstName, SSN
                                                                from {outputTable} 
                                                                group by SubNo, LastName, FirstName, SSN
                                                                having count(*) > 2";
        }



        public void HandleExactlyTwoTrans()
        {
            List<DuplicateMember> twoTransMembers = ExtractFactory.ConnectAndQuery<DuplicateMember>(callingProcess, targetDb, TwoTransForMembers()).ToList();

            List<Eight34Outputs.OutputRecord> typedRecordsFromDB = SourceMatcher.GetOutputRecordsFromApplicableDatabase(callingProcess, targetDb, GroupId, this.outputTable);

            foreach (DuplicateMember dup in twoTransMembers)
            {

                List<Eight34Outputs.OutputRecord> transactions = typedRecordsFromDB.Where(x => x.FirstName == dup.FirstName && x.LastName == dup.LastName && x.SSN == dup.SSN && x.SubNo == dup.SubNo).ToList();

                // If we have one term and one add
                if ((transactions.Count(x => x.MaintenanceCode == "024") == 1 && transactions.Count(x => x.MaintenanceCode == "021") == 1)) 
                {

                    Eight34Outputs.OutputRecord termTran = transactions.First(x => x.MaintenanceCode == "024");
                    Eight34Outputs.OutputRecord addTran = transactions.First(x => x.MaintenanceCode == "021");

                    if (termTran.CovEndDate.Trim().Length == 0 || addTran.CovEffDate.Trim().Length == 0)
                    {
                        ShutOffRecord("033", addTran); //turn off the add, we'll keep the term
                    }
                    else
                    {
                        DateTime termEnd = ConvertToDateTime(termTran.CovEndDate);

                        DateTime addEff = ConvertToDateTime(addTran.CovEffDate);

                        if (termEnd.AddDays(1) == addEff || termEnd == addEff)
                        {
                            ShutOffRecord("032", termTran); //turn off the term record, we'll just keep the add
                        }
                        else
                        {
                            ShutOffRecord("033", addTran);  //turn off the add, we'll keep the term
                        }
                    }

                }
                // If one is a change and one an add
                else if ((transactions.Count(x => x.MaintenanceCode == "001") == 1 && transactions.Count(x => x.MaintenanceCode == "021") == 1)) 
                {
                    Eight34Outputs.ExchangeOutputRecord dupChangeTran = transactions.First(x => x.MaintenanceCode == "001") as Eight34Outputs.ExchangeOutputRecord;
                    Eight34Outputs.ExchangeOutputRecord dupAddTran = transactions.First(x => x.MaintenanceCode == "021") as Eight34Outputs.ExchangeOutputRecord;

                    //Merge demographic details from the change transaction into the details on the Add, and suppress the change (001) record
                    AbsorbChangeRecord(dupAddTran, dupChangeTran, "037");

                }
                else if ((transactions.Count(x => x.MaintenanceCode == "001") == 1 && transactions.Count(x => x.MaintenanceCode == "024") == 1)) // If one is a change and one a term
                {
                    Eight34Outputs.ExchangeOutputRecord dupChangeTran = transactions.First(x => x.MaintenanceCode == "001") as Eight34Outputs.ExchangeOutputRecord;
                    Eight34Outputs.ExchangeOutputRecord dupTermTran = transactions.First(x => x.MaintenanceCode == "024") as Eight34Outputs.ExchangeOutputRecord;

                    AbsorbChangeRecord(dupTermTran, dupChangeTran, "036");

                }
                else //if we are dealing with two adds or two terms pick the lowest UniqueKey, drop the others to report
                {
                    ShutOffRecord("033", transactions.OrderByDescending(x => x.UniqueKey).FirstOrDefault());
                }
            }

            SourceMatcher.SaveCSharpTranslationsBacktoDatabase(targetDb, GroupId, typedRecordsFromDB, outputTable);
        }

        public void HandleMoreThanTwoTransactions()
        {
            List<DuplicateMember> MoreThanTwoTransMembers = ExtractFactory.ConnectAndQuery<DuplicateMember>(callingProcess, targetDb, GreaterThanTwoTransForMembers()).ToList();

            List<Eight34Outputs.OutputRecord> typedRecordsFromDB = SourceMatcher.GetOutputRecordsFromApplicableDatabase(callingProcess, targetDb, GroupId, this.outputTable);


            foreach (DuplicateMember dup in MoreThanTwoTransMembers)
            {

                List<Eight34Outputs.OutputRecord> transForMem = typedRecordsFromDB.Where(x => x.FirstName == dup.FirstName && x.LastName == dup.LastName && x.SSN == dup.SSN && x.SubNo == dup.SubNo).ToList();


                if (transForMem.Count(x => x.MaintenanceCode == "024") == 1) //if there is just one term, keep it but suppress all others
                {

                    Eight34Outputs.OutputRecord singleTermTran = transForMem.First(x => x.MaintenanceCode == "024");
                    TurnOffAllOtherTrans(transForMem, singleTermTran.UniqueKey);

                }
                else
                {
                    Int32 LowestTran = transForMem.Min(x => x.UniqueKey);

                    TurnOffAllOtherTrans(transForMem, LowestTran);
                }
            }

            SourceMatcher.SaveCSharpTranslationsBacktoDatabase(targetDb, GroupId, typedRecordsFromDB, outputTable);

        }


        /// <summary>
        /// Orphaned transactions are those where we've turned off all of the other transactions on that subscriber, but have left a member outstanding.
        /// This method turns them off and writes an error code.
        /// Fixed this to use SubNo to find transactions for the rest of the family insead of SBSB_ID, which will be * for new members
        /// </summary>
        public void FindOrphanTransactions()
        {

            DataWork.RunSqlCommand(callingProcess, EightThirtyFourTurnOffOprhans(), targetDb);
        }


        private string EightThirtyFourTurnOffOprhans()
        {
            return $@"update otp
                        set ErrCode = rtrim(ErrCode) + '034,', Output = 0
                        FROM [dbo].{outputTable} otp
                        where exists (select * from {outputTable} 
				        where SubNo = otp.SubNo and MaintenanceCode = otp.MaintenanceCode and Output = 0 and UniqueKey <> otp.UniqueKey and (ErrCode like '032%' or ErrCode like '033%') and SubscriberFlag = 'Y')
                        and Output = 1 and SubscriberFlag = 'N'";

        }



        private void TurnOffAllOtherTrans(List<Eight34Outputs.OutputRecord> transForMem, Int32 LowestTran)
        {

            List<Eight34Outputs.OutputRecord> TurnOffTrans = transForMem.FindAll(x => x.UniqueKey != LowestTran);

            foreach (Eight34Outputs.OutputRecord dupTran in TurnOffTrans) //turn off all but the lowest unique key
            {
                ShutOffRecord("033", dupTran);
            }
        }


        private void ShutOffRecord(string errorCode, Eight34Outputs.OutputRecord member)
        {
            member.Output = false;
            member.ErrCode += errorCode + ",";

        }

        public class DuplicateMember
        {
            public string LastName { get; set; }
            public string FirstName { get; set; }
            public string SSN { get; set; }
            public string SubNo { get; set; }
        }

        private void AbsorbChangeRecord(Eight34Outputs.ExchangeOutputRecord keptRecord, Eight34Outputs.ExchangeOutputRecord changeRecord, string turnOffErrorCode)
        {
            keptRecord.AddressOne = changeRecord.AddressOne;
            keptRecord.AddressTwo = changeRecord.AddressTwo;
            keptRecord.City = changeRecord.City;
            keptRecord.County = changeRecord.County;
            keptRecord.DOB = changeRecord.DOB;
            keptRecord.Fax = changeRecord.Fax;
            keptRecord.FirstName = changeRecord.FirstName;
            keptRecord.Gender = changeRecord.Gender;
            keptRecord.Language = changeRecord.Language;
            keptRecord.LastName = changeRecord.LastName;
            keptRecord.MailingAddressOne = changeRecord.MailingAddressOne;
            keptRecord.MailingAddressTwo = changeRecord.MailingAddressTwo;
            keptRecord.MailingCity = changeRecord.MailingCity;
            keptRecord.MailingState = changeRecord.MailingState;
            keptRecord.MailingZip = changeRecord.MailingZip;
            keptRecord.MaritalStatus = changeRecord.MaritalStatus;
            keptRecord.MemberSmokingIndicator = changeRecord.MemberSmokingIndicator;
            keptRecord.MidInit = changeRecord.MidInit;
            keptRecord.SubscriberMailingAddressType = changeRecord.SubscriberMailingAddressType;
            keptRecord.SubscriberSmokingIndicator = changeRecord.SubscriberSmokingIndicator;
            keptRecord.ShortName = changeRecord.ShortName;
            keptRecord.SSN = changeRecord.SSN;
            keptRecord.State = changeRecord.State;
            keptRecord.Telephone = changeRecord.Telephone;
            keptRecord.Zip = changeRecord.Zip;

            ShutOffRecord(turnOffErrorCode, changeRecord);

        }

    }

}