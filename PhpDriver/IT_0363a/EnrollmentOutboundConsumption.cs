using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.IO;
using Utilities;

namespace Driver.IT_0363a
{
    public class EnrollmentOutboundConsumption 
    {
        EFPhpArchive.IT0363_EdgeReleaseSummary_A EnrollmentSummaryTable = new EFPhpArchive.IT0363_EdgeReleaseSummary_A(); //The Table we will be inserting rows into
        EFPhpArchive.IT0363_EdgeReleaseDetail_A EnrollmentDetailTable;

        //bool MasterClassException = false;

        public static bool Main(IT_0363ACAEdgeReporting caller, String EDGEenvironment, List<string> Files)
        {


            #region FileGathering

            caller.WriteToLog("Found files: " + Environment.NewLine + String.Join(Environment.NewLine, Files));

            List<IssuerPlanEnrollmentDetail> EnrollmentDetail = new List<IssuerPlanEnrollmentDetail>(); //generates XSD classes List
            List<FileProcessingResultStatus> EnrollmentHeader = new List<FileProcessingResultStatus>();
            List<EnrollmentSummaryAcceptReject> EnrollmentSummary = new List<EnrollmentSummaryAcceptReject>();
            caller.WriteToLog("Adding Files");
            List<string> DetailFileNames = new List<string>(); //generates file name list
            List<string> HeaderFileNames = new List<string>();
            List<string> SummaryFileNames = new List<string>();
            foreach (string XMLFile in Files)
            {
                if (XMLFile.Contains("ED")) //Detail Files
                {
                    EnrollmentDetail.Add(XMLTranslations.Deseril<IssuerPlanEnrollmentDetail>(XMLFile));
                    string fileshort = System.IO.Path.GetFileName(XMLFile);  //This is used later to for archive spillage
                    DetailFileNames.Add(fileshort);

                }
                else if (XMLFile.Contains("EH")) //Header Files
                {
                    EnrollmentHeader.Add(XMLTranslations.Deseril<FileProcessingResultStatus>(XMLFile));
                    string fileshort = System.IO.Path.GetFileName(XMLFile);
                    HeaderFileNames.Add(fileshort);
                }
                else if (XMLFile.Contains("ES")) //Summary Files
                {
                    EnrollmentSummary.Add(XMLTranslations.Deseril<EnrollmentSummaryAcceptReject>(XMLFile));
                    string fileshort = System.IO.Path.GetFileName(XMLFile);
                    SummaryFileNames.Add(fileshort);
                }

            }
            #endregion
            caller.WriteToLog("Finished Adding Files");
            caller.WriteToLog("Found " + Files.Count + " Files");
            double RecordCount = 0;
            using (EFPhpArchive.PHPArchvEntities context = new EFPhpArchive.PHPArchvEntities()) //Open up context so we can add rows to database
            {
                context.Configuration.AutoDetectChangesEnabled = false;
                context.Configuration.ValidateOnSaveEnabled = false;
                int DetailCount = -1; //These counts are used to figuire out which file to delete for archive spillage
                int HeaderCount = -1;
                int SummaryCount = -1;
                //The First region just inserts into the Detail Table
                #region DetailTableUpdate
                caller.WriteToLog("Detail Table Updates");
                foreach (IssuerPlanEnrollmentDetail Detail in EnrollmentDetail) //For each file in Enrollment Detail
                {
                    DetailCount += 1;
                    string spillageQuery = string.Format(@"DELETE FROM [dbo].[IT0363_EdgeReleaseDetail_A] 
                    WHERE FileName='{0}'", DetailFileNames[DetailCount]);//This is the Spillage Query
                    DataTable PreviousData = ExtractFactory.ConnectAndQuery(caller.LoggerPhpArchive, spillageQuery);//Prevent spillage deltes all with same filename
                    foreach (EnrollmentIssuerProcessingResult Issuer in Detail.includedIssuerProcessingResult) //Issuer For Loop
                    {
                        if (Issuer.recordedError == null) //Issuer Non Error 
                        {
                            EnrollmentDetailIssuer(context, Detail, Issuer, DetailFileNames[DetailCount]);

                        }
                        else
                        {
                            foreach (ErrorMessageType ErrorMessage in Issuer.recordedError) //Issuer with Error 
                            {
                                EnrollmentDetailIssuer(context, Detail, Issuer, DetailFileNames[DetailCount], ErrorMessage);
                            }
                        }
                        foreach (InsuredMemberProcessingResult Member in Issuer.includedInsuredMemberProcessingResult) //Member For Loop
                        {
                            RecordCount++;
                            if (Member.recordedError == null) //Member Non Error
                            {
                                EnrollmentDetailMember(context, Detail, Member, DetailFileNames[DetailCount]);
                            }
                            else
                            {
                                foreach (ErrorMessageType ErrorMessage in Member.recordedError) //Member With Error
                                {
                                    EnrollmentDetailMember(context, Detail, Member, DetailFileNames[DetailCount], ErrorMessage);
                                }
                            }
                            if (RecordCount % 1000 == 0) //Prevent massive time loss by saving too much
                            {
                                context.SaveChanges();
                            }
                            foreach (InsuredMemberProfileProcessingResult Profile in Member.includedInsuredMemberProfileProcessingResult) //Profile For Loop
                            {
                                RecordCount++;
                                if (Profile.recordedError == null) //Profile Non Error
                                {
                                    EnrollmentDetailProfile(context, Detail, Profile, Member, DetailFileNames[DetailCount]);
                                }
                                else
                                {
                                    foreach (ErrorMessageType ErrorMessage in Profile.recordedError) //Profile With Error
                                    {
                                        EnrollmentDetailProfile(context, Detail, Profile, Member, DetailFileNames[DetailCount], ErrorMessage);
                                    }
                                }
                                if (RecordCount % 1000 == 0)
                                {
                                    context.SaveChanges();
                                }
                            }
                        }
                    }
                }

                caller.WriteToLog("Detail Table Updates Done.");
                #endregion
                //The Header Table is used to make sure all files are found and and all errors are found.

                #region SummaryTableUpdate
                caller.WriteToLog("Summary Table");
                SummaryCount = -1;
                //Summary File Proccessing
                foreach (EnrollmentSummaryAcceptReject Summary in EnrollmentSummary)
                {
                    SummaryCount += 1;
                    string spillageQuery = string.Format(@"DELETE FROM [dbo].[IT0363_EdgeReleaseSummary_A]
                    WHERE FileName='{0}'", SummaryFileNames[SummaryCount]);
                    DataTable PreviousData = ExtractFactory.ConnectAndQuery(caller.LoggerPhpArchive, spillageQuery);
                    EFPhpArchive.IT0363_EdgeReleaseSummary_A EnrollmentSummaryTable = new EFPhpArchive.IT0363_EdgeReleaseSummary_A();
                    EnrollmentSummaryTable.FileType = "E";
                    EnrollmentSummaryTable.ErrorType = "";
                    EnrollmentSummaryTable.issuerID = Summary.includedFileHeader.issuerID;
                    EnrollmentSummaryTable.inboundFileIdentifier = Summary.includedFileHeader.inboundFileIdentifier;
                    EnrollmentSummaryTable.month = 0;
                    EnrollmentSummaryTable.year = 0;
                    EnrollmentSummaryTable.statusTypeCode = "A";
                    EnrollmentSummaryTable.recordType = "Total";
                    EnrollmentSummaryTable.recordsReceived = Convert.ToInt32(Summary.includedEnrolleeRecordCounts.recordsReceived);
                    EnrollmentSummaryTable.recordsAccepted = Convert.ToInt32(Summary.includedEnrolleeRecordCounts.recordsAccepted);
                    EnrollmentSummaryTable.recordsResolved = Convert.ToInt32(Summary.includedEnrolleeRecordCounts.recordsResolved);
                    EnrollmentSummaryTable.recordsRejected = Convert.ToInt32(Summary.includedEnrolleeRecordCounts.recordsRejected);
                    EnrollmentSummaryTable.newRecordsAccepted = Convert.ToInt32(Summary.includedEnrolleeRecordCounts.newRecordsAccepted);
                    EnrollmentSummaryTable.FileName = SummaryFileNames[SummaryCount];
                    context.IT0363_EdgeReleaseSummary_A.Add(EnrollmentSummaryTable);
                    if (Summary.includedErrorCodeFrequency != null)
                    {
                        foreach (ErrorCodeCounts ErrorCode in Summary.includedErrorCodeFrequency) //Error Summary
                        {
                            EnrollmentSummaryTable = new EFPhpArchive.IT0363_EdgeReleaseSummary_A();
                            EnrollmentSummaryTable.FileType = "E";
                            EnrollmentSummaryTable.issuerID = Summary.includedFileHeader.issuerID;
                            EnrollmentSummaryTable.inboundFileIdentifier = Summary.includedFileHeader.inboundFileIdentifier;
                            EnrollmentSummaryTable.ErrorType = ErrorCode.offendingElementErrorTypeCode;
                            EnrollmentSummaryTable.month = 0;
                            EnrollmentSummaryTable.year = 0;
                            EnrollmentSummaryTable.statusTypeCode = "R";
                            EnrollmentSummaryTable.recordType = "Error";
                            EnrollmentSummaryTable.recordsReceived = 0;
                            EnrollmentSummaryTable.recordsAccepted = 0;
                            EnrollmentSummaryTable.recordsResolved = 0;
                            EnrollmentSummaryTable.recordsRejected = Convert.ToInt32(ErrorCode.offendingElementErrorTypeCodeFrequency);
                            EnrollmentSummaryTable.newRecordsAccepted = 0;
                            EnrollmentSummaryTable.FileName = SummaryFileNames[SummaryCount];
                            context.IT0363_EdgeReleaseSummary_A.Add(EnrollmentSummaryTable);
                            context.SaveChanges();
                        }
                    }
                    //Summary.includedPlan[0]

                }
                caller.WriteToLog("Summary Table Done");
                #endregion
                context.SaveChanges(); //Backup Last Save Just in case

            }


            caller.WriteToLog("All Files Done");
            return true;


        }
        ///// <summary>
        ///// The special on error class so you can do master or standard errors doesnt work yet
        ///// </summary>
        ///// <param name="exc"></param>
        //public void OnError(Exception exc, Driver.IT_0363 caller)
        //{

        //    // 272 use base  base.OnError(exc);
        //    if (MasterClassException == false)
        //    {
        //        base.OnError(exc);
        //    }
        //    //361 throw exception
        //    caller.WriteToLog(exc.Message + Environment.NewLine + exc.StackTrace);
        //    Console.WriteLine(exc.Message + Environment.NewLine + exc.StackTrace);
        //    base.OnError(exc);
        //    //Utilities.SendAlerts.Send(ProcessId, 1012, "Threw an exception", "Error Message: " + exc.Message + Environment.NewLine + "CallStack: " + exc.StackTrace, this, this.logLocation);
        //}

        /* For the Detail Files This region is the actual Table insertion
         * Each layer goes a bit deeper into the XMl but they all use the same Table  
         */
        #region Detail Processing

        /// <summary>
        /// The Enrollement Detail File First Section There should one be record
        /// </summary>
        /// <param name="context">The Archive Table</param>
        /// <param name="Detail">The Entire Detail XML object</param>
        /// <param name="Issuer">The XML object Issuer right under the entire object</param>
        /// <param name="filename">The name of the File</param>
        /// <param name="ErrorMessage">The Error Object</param>
        private static void EnrollmentDetailIssuer(EFPhpArchive.PHPArchvEntities context, IssuerPlanEnrollmentDetail Detail, EnrollmentIssuerProcessingResult Issuer, string filename, ErrorMessageType ErrorMessage = null)
        {
            EFPhpArchive.IT0363_EdgeReleaseDetail_A EnrollmentDetailTable = new EFPhpArchive.IT0363_EdgeReleaseDetail_A();
            EnrollmentDetailTable.FileType = "E"; //Always E for Enrollment
            EnrollmentDetailTable.issuerID = Detail.includedFileHeader.issuerID; //Which Pharamacy Issuer
            EnrollmentDetailTable.inboundFileIdentifier = Detail.includedFileHeader.inboundFileIdentifier;
            EnrollmentDetailTable.recordID = Convert.ToInt32(Issuer.issuerRecordIdentifier);//The ID given by Edge 
            EnrollmentDetailTable.Identifier = Issuer.issuerIdentifier; ///Either 20662 or 60829
            EnrollmentDetailTable.statusTypeCode = Issuer.classifyingProcessingStatusType.statusTypeCode; //A accepted R rejected I 
            EnrollmentDetailTable.recordType = "Issuer"; //The Function Called
            EnrollmentDetailTable.FileName = filename; //The name of the File
            EnrollmentDetailTable.insurancePlanIdentifier = ""; //Only in Medical and Pharmacy
            ErrorCheck(EnrollmentDetailTable, ErrorMessage); //For Errors the most important part of these records
            context.IT0363_EdgeReleaseDetail_A.Add(EnrollmentDetailTable);
            //   context.SaveChanges();
        }

        /// <summary>
        /// This is one layer deeper
        /// </summary>
        /// <param name="context">same</param>
        /// <param name="Detail">same</param>
        /// <param name="Member">For each issuer theres members</param>
        /// <param name="filename">same</param>
        /// <param name="ErrorMessage">same</param>
        private static void EnrollmentDetailMember(EFPhpArchive.PHPArchvEntities context, IssuerPlanEnrollmentDetail Detail, InsuredMemberProcessingResult Member, string filename, ErrorMessageType ErrorMessage = null)
        {
            EFPhpArchive.IT0363_EdgeReleaseDetail_A EnrollmentDetailTable = new EFPhpArchive.IT0363_EdgeReleaseDetail_A();
            EnrollmentDetailTable.FileType = "E";
            EnrollmentDetailTable.issuerID = Detail.includedFileHeader.issuerID;
            EnrollmentDetailTable.inboundFileIdentifier = Detail.includedFileHeader.inboundFileIdentifier;
            EnrollmentDetailTable.recordID = Convert.ToInt32(Member.insuredMemberRecordIdentifier);
            EnrollmentDetailTable.Identifier = Member.insuredMemberIdentifier;
            EnrollmentDetailTable.statusTypeCode = Member.classifyingProcessingStatusType.statusTypeCode;
            EnrollmentDetailTable.recordType = "Member"; //Members of Issuers
            EnrollmentDetailTable.FileName = filename;
            EnrollmentDetailTable.insurancePlanIdentifier = "";
            ErrorCheck(EnrollmentDetailTable, ErrorMessage);
            context.IT0363_EdgeReleaseDetail_A.Add(EnrollmentDetailTable);
            // context.SaveChanges();
        }
        /// <summary>
        /// The Final layer of member profiles
        /// </summary>
        /// <param name="context">same</param>
        /// <param name="Detail">same</param>
        /// <param name="Profile">The Profile of members </param>
        /// <param name="Member">same</param>
        /// <param name="filename">same</param>
        /// <param name="ErrorMessage">same</param>
        private static void EnrollmentDetailProfile(EFPhpArchive.PHPArchvEntities context, IssuerPlanEnrollmentDetail Detail, InsuredMemberProfileProcessingResult Profile, InsuredMemberProcessingResult Member, string filename, ErrorMessageType ErrorMessage = null)
        {
            EFPhpArchive.IT0363_EdgeReleaseDetail_A EnrollmentDetailTable = new EFPhpArchive.IT0363_EdgeReleaseDetail_A();
            EnrollmentDetailTable.FileType = "E";
            EnrollmentDetailTable.issuerID = Detail.includedFileHeader.issuerID;
            EnrollmentDetailTable.inboundFileIdentifier = Detail.includedFileHeader.inboundFileIdentifier;
            EnrollmentDetailTable.recordID = Convert.ToInt32(Profile.insuredMemberProfileRecordIdentifier);
            EnrollmentDetailTable.statusTypeCode = Profile.classifyingProcessingStatusType.statusTypeCode;
            EnrollmentDetailTable.Identifier = Member.insuredMemberIdentifier; //   IMPORTANT INDENTIFIER IS OF WHICH MEMBER IT's FROM
            EnrollmentDetailTable.recordType = "Profile"; //Profiles of members
            EnrollmentDetailTable.FileName = filename;
            EnrollmentDetailTable.insurancePlanIdentifier = "";
            ErrorCheck(EnrollmentDetailTable, ErrorMessage);
            context.IT0363_EdgeReleaseDetail_A.Add(EnrollmentDetailTable);
            // context.SaveChanges();
        }



        /// <summary>
        /// This is a seperate Function used to add all error info or just make blanks.This prevents NULLs
        /// </summary>
        /// <param name="ErrorMessage">Error Object containing the error info</param>
        private static void ErrorCheck(EFPhpArchive.IT0363_EdgeReleaseDetail_A EnrollmentDetailTable, ErrorMessageType ErrorMessage = null)
        {

            if (ErrorMessage == null || ErrorMessage.offendingElementValue == null)
            {
                
                EnrollmentDetailTable.ErrorElementValue = "";
            }
            else
            {
                EnrollmentDetailTable.ErrorElementValue = string.Join(",", ErrorMessage.offendingElementValue);
            }
            if (ErrorMessage == null || ErrorMessage.offendingElementName == null)
            {
                EnrollmentDetailTable.ErrorName = "";
            }
            else
            {

                EnrollmentDetailTable.ErrorName = string.Join(",", ErrorMessage.offendingElementName);
            }

            if (ErrorMessage == null || ErrorMessage.offendingElementErrorTypeMessage == null)
            {
                EnrollmentDetailTable.ErrorMessage = "";
            }
            else
            {

                EnrollmentDetailTable.ErrorMessage = string.Join(",", ErrorMessage.offendingElementErrorTypeMessage);
            }
            if (ErrorMessage == null || ErrorMessage.offendingElementErrorTypeCode == null)
            {
                EnrollmentDetailTable.ErrorTypeCode = "";
            }
            else
            {
                EnrollmentDetailTable.ErrorTypeCode = string.Join(",", ErrorMessage.offendingElementErrorTypeCode);
            }
            if (ErrorMessage == null || ErrorMessage.offendingElementErrorTypeDetail == null)
            {
                EnrollmentDetailTable.ErrorTypeDetail = "";
            }
            else
            {
                EnrollmentDetailTable.ErrorTypeDetail = string.Join(",", ErrorMessage.offendingElementErrorTypeDetail);
            }
        }
        #endregion

    }

    //These are the auto generated XSD classes for Enrollemnt and Summary more info see XML Translations
    #region Detail
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://vo.edge.fm.cms.hhs.gov")]
    [System.Xml.Serialization.XmlRootAttribute("issuerPlanEnrollmentDetail", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
    public partial class IssuerPlanEnrollmentDetail
    {

        private CommonOutboundFileHeader includedFileHeaderField;

        private EnrollmentIssuerProcessingResult[] includedIssuerProcessingResultField;

        /// <remarks/>
        public CommonOutboundFileHeader includedFileHeader
        {
            get
            {
                return this.includedFileHeaderField;
            }
            set
            {
                this.includedFileHeaderField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("includedIssuerProcessingResult")]
        public EnrollmentIssuerProcessingResult[] includedIssuerProcessingResult
        {
            get
            {
                return this.includedIssuerProcessingResultField;
            }
            set
            {
                this.includedIssuerProcessingResultField = value;
            }
        }
    }



    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://vo.edge.fm.cms.hhs.gov")]
    [System.Xml.Serialization.XmlRootAttribute("insuredMemberProfileProcessingResult", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
    public partial class InsuredMemberProfileProcessingResult
    {

        private string insuredMemberProfileRecordIdentifierField;

        private SubmissionProcessingStatusType classifyingProcessingStatusTypeField;

        private ErrorMessageType[] recordedErrorField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
        public string insuredMemberProfileRecordIdentifier
        {
            get
            {
                return this.insuredMemberProfileRecordIdentifierField;
            }
            set
            {
                this.insuredMemberProfileRecordIdentifierField = value;
            }
        }

        /// <remarks/>
        public SubmissionProcessingStatusType classifyingProcessingStatusType
        {
            get
            {
                return this.classifyingProcessingStatusTypeField;
            }
            set
            {
                this.classifyingProcessingStatusTypeField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("recordedError")]
        public ErrorMessageType[] recordedError
        {
            get
            {
                return this.recordedErrorField;
            }
            set
            {
                this.recordedErrorField = value;
            }
        }
    }





    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://vo.edge.fm.cms.hhs.gov")]
    [System.Xml.Serialization.XmlRootAttribute("insuredMemberProcessingResult", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
    public partial class InsuredMemberProcessingResult
    {

        private string insuredMemberRecordIdentifierField;

        private string insuredMemberIdentifierField;

        private SubmissionProcessingStatusType classifyingProcessingStatusTypeField;

        private ErrorMessageType[] recordedErrorField;

        private InsuredMemberProfileProcessingResult[] includedInsuredMemberProfileProcessingResultField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
        public string insuredMemberRecordIdentifier
        {
            get
            {
                return this.insuredMemberRecordIdentifierField;
            }
            set
            {
                this.insuredMemberRecordIdentifierField = value;
            }
        }

        /// <remarks/>
        public string insuredMemberIdentifier
        {
            get
            {
                return this.insuredMemberIdentifierField;
            }
            set
            {
                this.insuredMemberIdentifierField = value;
            }
        }

        /// <remarks/>
        public SubmissionProcessingStatusType classifyingProcessingStatusType
        {
            get
            {
                return this.classifyingProcessingStatusTypeField;
            }
            set
            {
                this.classifyingProcessingStatusTypeField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("recordedError")]
        public ErrorMessageType[] recordedError
        {
            get
            {
                return this.recordedErrorField;
            }
            set
            {
                this.recordedErrorField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("includedInsuredMemberProfileProcessingResult")]
        public InsuredMemberProfileProcessingResult[] includedInsuredMemberProfileProcessingResult
        {
            get
            {
                return this.includedInsuredMemberProfileProcessingResultField;
            }
            set
            {
                this.includedInsuredMemberProfileProcessingResultField = value;
            }
        }
    }

    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://vo.edge.fm.cms.hhs.gov")]
    [System.Xml.Serialization.XmlRootAttribute("enrollmentIssuerProcessingResult", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
    public partial class EnrollmentIssuerProcessingResult
    {

        private string issuerRecordIdentifierField;

        private string issuerIdentifierField;

        private SubmissionProcessingStatusType classifyingProcessingStatusTypeField;

        private ErrorMessageType[] recordedErrorField;

        private InsuredMemberProcessingResult[] includedInsuredMemberProcessingResultField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
        public string issuerRecordIdentifier
        {
            get
            {
                return this.issuerRecordIdentifierField;
            }
            set
            {
                this.issuerRecordIdentifierField = value;
            }
        }

        /// <remarks/>
        public string issuerIdentifier
        {
            get
            {
                return this.issuerIdentifierField;
            }
            set
            {
                this.issuerIdentifierField = value;
            }
        }

        /// <remarks/>
        public SubmissionProcessingStatusType classifyingProcessingStatusType
        {
            get
            {
                return this.classifyingProcessingStatusTypeField;
            }
            set
            {
                this.classifyingProcessingStatusTypeField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("recordedError")]
        public ErrorMessageType[] recordedError
        {
            get
            {
                return this.recordedErrorField;
            }
            set
            {
                this.recordedErrorField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("includedInsuredMemberProcessingResult")]
        public InsuredMemberProcessingResult[] includedInsuredMemberProcessingResult
        {
            get
            {
                return this.includedInsuredMemberProcessingResultField;
            }
            set
            {
                this.includedInsuredMemberProcessingResultField = value;
            }
        }
    }
    #endregion
    #region Summary

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://vo.edge.fm.cms.hhs.gov")]
    [System.Xml.Serialization.XmlRootAttribute("enrollmentSummaryAcceptReject", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
    public partial class EnrollmentSummaryAcceptReject
    {

        private CommonOutboundFileHeader includedFileHeaderField;

        private string inboundFileHashField;

        private string detailReportHashField;

        private ErrorCodeCounts[] includedErrorCodeFrequencyField;

        private ClaimCountMessageType includedEnrolleeRecordCountsField;

        private ClaimCountMessageType includedEnrollmentPeriodRecordCountsField;

        private EnrollmentIssuerSummary includedIssuerField;



        private EnrollmentPlan[] includedPlanField;

        /// <remarks/>
        public CommonOutboundFileHeader includedFileHeader
        {
            get
            {
                return this.includedFileHeaderField;
            }
            set
            {
                this.includedFileHeaderField = value;
            }
        }

        /// <remarks/>
        public string inboundFileHash
        {
            get
            {
                return this.inboundFileHashField;
            }
            set
            {
                this.inboundFileHashField = value;
            }
        }

        /// <remarks/>
        public string detailReportHash
        {
            get
            {
                return this.detailReportHashField;
            }
            set
            {
                this.detailReportHashField = value;
            }
        }

        /// <remarks/>
        public ClaimCountMessageType includedEnrolleeRecordCounts
        {
            get
            {
                return this.includedEnrolleeRecordCountsField;
            }
            set
            {
                this.includedEnrolleeRecordCountsField = value;
            }
        }

        /// <remarks/>
        public ClaimCountMessageType includedEnrollmentPeriodRecordCounts
        {
            get
            {
                return this.includedEnrollmentPeriodRecordCountsField;
            }
            set
            {
                this.includedEnrollmentPeriodRecordCountsField = value;
            }
        }

        /// <remarks/>
        public EnrollmentIssuerSummary includedIssuer
        {
            get
            {
                return this.includedIssuerField;
            }
            set
            {
                this.includedIssuerField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("includedErrorCodeFrequency")]
        public ErrorCodeCounts[] includedErrorCodeFrequency
        {
            get
            {
                return this.includedErrorCodeFrequencyField;
            }
            set
            {
                this.includedErrorCodeFrequencyField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("includedPlan")]
        public EnrollmentPlan[] includedPlan
        {
            get
            {
                return this.includedPlanField;
            }
            set
            {
                this.includedPlanField = value;
            }
        }
    }



    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://vo.edge.fm.cms.hhs.gov")]
    [System.Xml.Serialization.XmlRootAttribute("enrollmentPlanMonth", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
    public partial class EnrollmentPlanMonth
    {

        private string planMonthField;

        private string acceptedMonthsField;

        private string rejectedMonthsField;

        /// <remarks/>
        public string planMonth
        {
            get
            {
                return this.planMonthField;
            }
            set
            {
                this.planMonthField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
        public string acceptedMonths
        {
            get
            {
                return this.acceptedMonthsField;
            }
            set
            {
                this.acceptedMonthsField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
        public string rejectedMonths
        {
            get
            {
                return this.rejectedMonthsField;
            }
            set
            {
                this.rejectedMonthsField = value;
            }
        }
    }

    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://vo.edge.fm.cms.hhs.gov")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
    public partial class EnrollmentPlanYear
    {

        private string planYearField;

        private string acceptedMonthsField;

        private string rejectedMonthsField;

        private EnrollmentPlanMonth[] includedPlanMonthField;

        /// <remarks/>
        public string planYear
        {
            get
            {
                return this.planYearField;
            }
            set
            {
                this.planYearField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
        public string acceptedMonths
        {
            get
            {
                return this.acceptedMonthsField;
            }
            set
            {
                this.acceptedMonthsField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
        public string rejectedMonths
        {
            get
            {
                return this.rejectedMonthsField;
            }
            set
            {
                this.rejectedMonthsField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("includedPlanMonth")]
        public EnrollmentPlanMonth[] includedPlanMonth
        {
            get
            {
                return this.includedPlanMonthField;
            }
            set
            {
                this.includedPlanMonthField = value;
            }
        }
    }

    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://vo.edge.fm.cms.hhs.gov")]
    [System.Xml.Serialization.XmlRootAttribute("enrollmentPlan", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
    public partial class EnrollmentPlan
    {

        private string planIdentifierField;

        private SubmissionProcessingStatusType classifyingProcessingStatusTypeField;

        private ClaimCountMessageType includedEnrolleeRecordCountsField;

        private ClaimCountMessageType includedEnrollmentPeriodRecordCountsField;

        private EnrollmentPlanYear[] includedPlanYearField;

        /// <remarks/>
        public string planIdentifier
        {
            get
            {
                return this.planIdentifierField;
            }
            set
            {
                this.planIdentifierField = value;
            }
        }

        /// <remarks/>
        public SubmissionProcessingStatusType classifyingProcessingStatusType
        {
            get
            {
                return this.classifyingProcessingStatusTypeField;
            }
            set
            {
                this.classifyingProcessingStatusTypeField = value;
            }
        }

        /// <remarks/>
        public ClaimCountMessageType includedEnrolleeRecordCounts
        {
            get
            {
                return this.includedEnrolleeRecordCountsField;
            }
            set
            {
                this.includedEnrolleeRecordCountsField = value;
            }
        }

        /// <remarks/>
        public ClaimCountMessageType includedEnrollmentPeriodRecordCounts
        {
            get
            {
                return this.includedEnrollmentPeriodRecordCountsField;
            }
            set
            {
                this.includedEnrollmentPeriodRecordCountsField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("includedPlanYear")]
        public EnrollmentPlanYear[] includedPlanYear
        {
            get
            {
                return this.includedPlanYearField;
            }
            set
            {
                this.includedPlanYearField = value;
            }
        }
    }


    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://vo.edge.fm.cms.hhs.gov")]
    [System.Xml.Serialization.XmlRootAttribute("enrollmentIssuerMonth", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
    public partial class EnrollmentIssuerMonth
    {

        private string issuerMonthField;

        private string acceptedMonthsField;

        private string rejectedMonthsField;

        /// <remarks/>
        public string issuerMonth
        {
            get
            {
                return this.issuerMonthField;
            }
            set
            {
                this.issuerMonthField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
        public string acceptedMonths
        {
            get
            {
                return this.acceptedMonthsField;
            }
            set
            {
                this.acceptedMonthsField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
        public string rejectedMonths
        {
            get
            {
                return this.rejectedMonthsField;
            }
            set
            {
                this.rejectedMonthsField = value;
            }
        }
    }

    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://vo.edge.fm.cms.hhs.gov")]
    [System.Xml.Serialization.XmlRootAttribute("enrollmentIssuerYear", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
    public partial class EnrollmentIssuerYear
    {

        private string issuerYearField;

        private string acceptedMonthsField;

        private string rejectedMonthsField;

        private EnrollmentIssuerMonth[] includedIssuerMonthField;

        /// <remarks/>
        public string issuerYear
        {
            get
            {
                return this.issuerYearField;
            }
            set
            {
                this.issuerYearField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
        public string acceptedMonths
        {
            get
            {
                return this.acceptedMonthsField;
            }
            set
            {
                this.acceptedMonthsField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
        public string rejectedMonths
        {
            get
            {
                return this.rejectedMonthsField;
            }
            set
            {
                this.rejectedMonthsField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("includedIssuerMonth")]
        public EnrollmentIssuerMonth[] includedIssuerMonth
        {
            get
            {
                return this.includedIssuerMonthField;
            }
            set
            {
                this.includedIssuerMonthField = value;
            }
        }
    }

    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://vo.edge.fm.cms.hhs.gov")]
    [System.Xml.Serialization.XmlRootAttribute("enrollmentIssuerSummary", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
    public partial class EnrollmentIssuerSummary
    {

        private string issuerRecordIdentifierField;

        private string issuerPlanCountField;

        private SubmissionProcessingStatusType classifyingProcessingStatusTypeField;

        private ClaimCountMessageType includedEnrolleeRecordCountsField;

        private ClaimCountMessageType includedEnrollmentPeriodRecordCountsField;

        private EnrollmentIssuerYear[] includedIssuerYearField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
        public string issuerRecordIdentifier
        {
            get
            {
                return this.issuerRecordIdentifierField;
            }
            set
            {
                this.issuerRecordIdentifierField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
        public string issuerPlanCount
        {
            get
            {
                return this.issuerPlanCountField;
            }
            set
            {
                this.issuerPlanCountField = value;
            }
        }

        /// <remarks/>
        public SubmissionProcessingStatusType classifyingProcessingStatusType
        {
            get
            {
                return this.classifyingProcessingStatusTypeField;
            }
            set
            {
                this.classifyingProcessingStatusTypeField = value;
            }
        }

        /// <remarks/>
        public ClaimCountMessageType includedEnrolleeRecordCounts
        {
            get
            {
                return this.includedEnrolleeRecordCountsField;
            }
            set
            {
                this.includedEnrolleeRecordCountsField = value;
            }
        }

        /// <remarks/>
        public ClaimCountMessageType includedEnrollmentPeriodRecordCounts
        {
            get
            {
                return this.includedEnrollmentPeriodRecordCountsField;
            }
            set
            {
                this.includedEnrollmentPeriodRecordCountsField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("includedIssuerYear")]
        public EnrollmentIssuerYear[] includedIssuerYear
        {
            get
            {
                return this.includedIssuerYearField;
            }
            set
            {
                this.includedIssuerYearField = value;
            }
        }
    }



    #endregion
}

