using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using Utilities;

namespace Driver.IT_0363a
{
    public class PharmacyClaimOutboundConsumption
    {
        //EFPhpArchive.IT0363_EdgeReleaseSummary_A PharmacySummaryTable;
        //EFPhpArchive.IT0363_EdgeReleaseDetail_A PharmacyDetailTable;

        bool MasterClassException = false;

        public static bool Main(IT_0363ACAEdgeReporting caller, String EDGEenvironment, List<String> Files)
        {

            List<IssuerPlanPharmacyClaimDetail> PharmacyDetail = new List<IssuerPlanPharmacyClaimDetail>();
            List<FileProcessingResultStatus> PharmacyHeader = new List<FileProcessingResultStatus>();
            List<PharmacyClaimSummaryAcceptReject> PharmacySummary = new List<PharmacyClaimSummaryAcceptReject>();

            caller.WriteToLog("Found files: " + Environment.NewLine + String.Join(Environment.NewLine, Files));
            caller.WriteToLog("Adding Files");
            List<string> DetailFileNames = new List<string>();
            List<string> HeaderFileNames = new List<string>();
            List<string> SummaryFileNames = new List<string>();
            foreach (string XMLFile in Files)
            {
                if (XMLFile.Contains("PD.D"))
                {
                    PharmacyDetail.Add(XMLTranslations.Deseril<IssuerPlanPharmacyClaimDetail>(XMLFile));
                    string fileshort = System.IO.Path.GetFileName(XMLFile);
                    DetailFileNames.Add(fileshort);
                }
                else if (XMLFile.Contains("PH.D"))
                {
                    PharmacyHeader.Add(XMLTranslations.Deseril<FileProcessingResultStatus>(XMLFile));
                    string fileshort = System.IO.Path.GetFileName(XMLFile);
                    HeaderFileNames.Add(fileshort);
                }
                else if (XMLFile.Contains("PS.D"))
                {
                    PharmacySummary.Add(XMLTranslations.Deseril<PharmacyClaimSummaryAcceptReject>(XMLFile));
                    string fileshort = System.IO.Path.GetFileName(XMLFile);
                    SummaryFileNames.Add(fileshort);
                }
            }
            caller.WriteToLog("Finished Adding Files");
            caller.WriteToLog("Found " + Files.Count + " Files");
            double RecordCount = 0;
            using (EFPhpArchive.PHPArchvEntities context = new EFPhpArchive.PHPArchvEntities())
            {
                context.Configuration.AutoDetectChangesEnabled = false;
                context.Configuration.ValidateOnSaveEnabled = false;
                int DetailCount = -1;
                int SummaryCount = -1;
                #region DetailTableUpdate
                caller.WriteToLog("Detail Table Updates");
                foreach (IssuerPlanPharmacyClaimDetail Detail in PharmacyDetail)
                {
                    DetailCount += 1;
                    string spillageQuery = string.Format(@"DELETE FROM [dbo].[IT0363_EdgeReleaseDetail_A]
                    WHERE FileName='{0}'", DetailFileNames[DetailCount]);
                    DataTable PreviousData = ExtractFactory.ConnectAndQuery(caller.LoggerPhpArchive, spillageQuery);
                    foreach (PharmacyClaimIssuerProcessingResult Issuer in Detail.includedIssuerProcessingResult)
                    {
                        if (Issuer.recordedError == null)
                        {
                            PharmacyDetailIssuer(context, Detail, Issuer, DetailFileNames[DetailCount]);

                        }
                        else
                        {
                            foreach (ErrorMessageType ErrorMessage in Issuer.recordedError)
                            {
                                PharmacyDetailIssuer(context, Detail, Issuer, DetailFileNames[DetailCount], ErrorMessage);
                            }
                        }
                        foreach (PharmacyClaimPlanProcessingResult Plan in Issuer.includedPlanProcessingResult)
                        {
                            RecordCount++;
                            if (RecordCount % 1000 == 0)
                            {
                                context.SaveChanges();
                            }
                            if (Plan.recordedError == null)
                            {
                                PharmacyDetailPlan(context, Detail, Plan, DetailFileNames[DetailCount]);
                            }
                            else
                            {
                                foreach (ErrorMessageType ErrorMessage in Plan.recordedError)
                                {
                                    PharmacyDetailPlan(context, Detail, Plan, DetailFileNames[DetailCount], ErrorMessage);
                                }
                            }
                            foreach (PharmacyClaimProcessingResult Claim in Plan.includedClaimProcessingResult)
                            {
                                RecordCount++;
                                if (RecordCount % 1000 == 0)
                                {
                                    List<EFPhpArchive.IT0363_EdgeReleaseDetail_A> test = context.IT0363_EdgeReleaseDetail_A.Local.Where(x => x.recordID == 2).ToList();
                                    context.SaveChanges();
                                }
                                if (Claim.recordedError == null)
                                {
                                    PharmacyDetailClaim(context, Detail, Plan, Claim, DetailFileNames[DetailCount]);
                                }
                                else
                                {
                                    foreach (ErrorMessageType ErrorMessage in Claim.recordedError)
                                    {
                                        PharmacyDetailClaim(context, Detail, Plan, Claim, DetailFileNames[DetailCount], ErrorMessage);
                                    }
                                }
                            }
                        }
                    }
                    context.SaveChanges();

                }
                caller.WriteToLog("Detail Table Updates Done.");
                #endregion
 
                #region SummaryTableUpdate
                caller.WriteToLog("Summary Table");
                SummaryCount = -1;
                foreach (PharmacyClaimSummaryAcceptReject Summary in PharmacySummary)
                {
                    SummaryCount += 1;
                    string spillageQuery = string.Format(@"DELETE FROM [dbo].[IT0363_EdgeReleaseSummary_A]
                    WHERE FileName='{0}'", SummaryFileNames[SummaryCount]);
                    DataTable PreviousData = ExtractFactory.ConnectAndQuery(caller.LoggerPhpArchive, spillageQuery);
                    EFPhpArchive.IT0363_EdgeReleaseSummary_A PharmacySummaryTable = new EFPhpArchive.IT0363_EdgeReleaseSummary_A();
                    PharmacySummaryTable.FileType = "P";
                    PharmacySummaryTable.ErrorType = "";
                    PharmacySummaryTable.issuerID = Summary.includedFileHeader.issuerID;
                    PharmacySummaryTable.inboundFileIdentifier = Summary.includedFileHeader.inboundFileIdentifier;
                    PharmacySummaryTable.month = 0;
                    PharmacySummaryTable.year = 0;
                    PharmacySummaryTable.statusTypeCode = "A";
                    PharmacySummaryTable.recordType = "Total";
                    PharmacySummaryTable.recordsReceived = Convert.ToInt32(Summary.includedRecordCounts.recordsReceived);
                    PharmacySummaryTable.recordsAccepted = Convert.ToInt32(Summary.includedRecordCounts.recordsAccepted);
                    PharmacySummaryTable.recordsResolved = Convert.ToInt32(Summary.includedRecordCounts.recordsResolved);
                    PharmacySummaryTable.recordsRejected = Convert.ToInt32(Summary.includedRecordCounts.recordsRejected);
                    PharmacySummaryTable.newRecordsAccepted = Convert.ToInt32(Summary.includedRecordCounts.newRecordsAccepted);
                    PharmacySummaryTable.FileName = SummaryFileNames[SummaryCount];
                    context.IT0363_EdgeReleaseSummary_A.Add(PharmacySummaryTable);
                    if (Summary.includedErrorCodeFrequency != null)
                    {
                        foreach (ErrorCodeCounts ErrorCode in Summary.includedErrorCodeFrequency) //Error Summary
                        {
                            PharmacySummaryTable = new EFPhpArchive.IT0363_EdgeReleaseSummary_A();
                            PharmacySummaryTable.FileType = "P";
                            PharmacySummaryTable.issuerID = Summary.includedFileHeader.issuerID;
                            PharmacySummaryTable.inboundFileIdentifier = Summary.includedFileHeader.inboundFileIdentifier;
                            PharmacySummaryTable.ErrorType = ErrorCode.offendingElementErrorTypeCode;
                            PharmacySummaryTable.month = 0;
                            PharmacySummaryTable.year = 0;
                            PharmacySummaryTable.statusTypeCode = "R";
                            PharmacySummaryTable.recordType = "Error";
                            PharmacySummaryTable.recordsReceived = 0;
                            PharmacySummaryTable.recordsAccepted = 0;
                            PharmacySummaryTable.recordsResolved = 0;
                            PharmacySummaryTable.recordsRejected = Convert.ToInt32(ErrorCode.offendingElementErrorTypeCodeFrequency);
                            PharmacySummaryTable.newRecordsAccepted = 0;
                            PharmacySummaryTable.FileName = SummaryFileNames[SummaryCount];
                            context.IT0363_EdgeReleaseSummary_A.Add(PharmacySummaryTable);
                            context.SaveChanges();
                        }
                    }
                    PharmacyClaimIssuerSummary IssuerSummary = Summary.includedIssuerSummary;
                    foreach (PharmacyClaimIssuerYear Year in IssuerSummary.includedIssuerYear) //Month Summary
                    {
                        foreach (PharmacyClaimIssuerMonth Month in Year.includedIssuerMonth)
                        {
                            PharmacySummaryTable = new EFPhpArchive.IT0363_EdgeReleaseSummary_A();
                            PharmacySummaryTable.FileType = "P";
                            PharmacySummaryTable.ErrorType = "";
                            PharmacySummaryTable.issuerID = Summary.includedFileHeader.issuerID;
                            PharmacySummaryTable.inboundFileIdentifier = Summary.includedFileHeader.inboundFileIdentifier;
                            PharmacySummaryTable.month = Convert.ToInt32(Month.issuerMonth); //Change To Month
                            PharmacySummaryTable.year = Convert.ToInt32(Year.issuerYear); //Change To Month
                            PharmacySummaryTable.statusTypeCode = IssuerSummary.classifyingProcessingStatusType.statusTypeCode;
                            PharmacySummaryTable.recordType = "Header";
                            PharmacySummaryTable.recordsReceived = Convert.ToInt32(Month.includedIssuerMonthCounts.recordsReceived);
                            PharmacySummaryTable.recordsAccepted = Convert.ToInt32(Month.includedIssuerMonthCounts.recordsAccepted);
                            PharmacySummaryTable.recordsResolved = Convert.ToInt32(Month.includedIssuerMonthCounts.recordsResolved);
                            PharmacySummaryTable.recordsRejected = Convert.ToInt32(Month.includedIssuerMonthCounts.recordsRejected);
                            PharmacySummaryTable.newRecordsAccepted = Convert.ToInt32(Month.includedIssuerMonthCounts.newRecordsAccepted); //P Form Type
                            PharmacySummaryTable.FileName = SummaryFileNames[SummaryCount];
                            context.IT0363_EdgeReleaseSummary_A.Add(PharmacySummaryTable);
                        }
                    }
                    //Summary.includedPlan[0]

                }
                caller.WriteToLog("Summary Table Done");
                #endregion
                context.SaveChanges();
            }
            caller.WriteToLog("All Files Done");

            return true;

        }

        private static void PharmacyDetailClaim(EFPhpArchive.PHPArchvEntities context, IssuerPlanPharmacyClaimDetail Detail, PharmacyClaimPlanProcessingResult Plan, PharmacyClaimProcessingResult Claim, string filename, ErrorMessageType ErrorMessage = null)
        {
            EFPhpArchive.IT0363_EdgeReleaseDetail_A PharmacyDetailTable = new EFPhpArchive.IT0363_EdgeReleaseDetail_A();
            PharmacyDetailTable.FileType = "P";
            PharmacyDetailTable.issuerID = Detail.includedFileHeader.issuerID;
            PharmacyDetailTable.inboundFileIdentifier = Detail.includedFileHeader.inboundFileIdentifier;
            PharmacyDetailTable.recordID = Convert.ToInt32(Claim.pharmacyClaimRecordIdentifier);
            PharmacyDetailTable.Identifier = Claim.pharmacyClaimIdentifier;
            PharmacyDetailTable.statusTypeCode = Claim.classifyingProcessingStatusType.statusTypeCode;
            PharmacyDetailTable.recordType = "Claim";
            PharmacyDetailTable.FileName = filename;
            PharmacyDetailTable.insurancePlanIdentifier = Plan.insurancePlanIdentifier;
            ErrorCheck(PharmacyDetailTable, ErrorMessage);
            context.IT0363_EdgeReleaseDetail_A.Add(PharmacyDetailTable);
            // context.SaveChanges();
        }

        private static void PharmacyDetailPlan(EFPhpArchive.PHPArchvEntities context, IssuerPlanPharmacyClaimDetail Detail, PharmacyClaimPlanProcessingResult Plan, string filename, ErrorMessageType ErrorMessage = null)
        {
            EFPhpArchive.IT0363_EdgeReleaseDetail_A PharmacyDetailTable = new EFPhpArchive.IT0363_EdgeReleaseDetail_A();
            PharmacyDetailTable.FileType = "P";
            PharmacyDetailTable.issuerID = Detail.includedFileHeader.issuerID;
            PharmacyDetailTable.inboundFileIdentifier = Detail.includedFileHeader.inboundFileIdentifier;
            PharmacyDetailTable.recordID = Convert.ToInt32(Plan.planRecordIdentifier);
            PharmacyDetailTable.Identifier = Plan.insurancePlanIdentifier;
            PharmacyDetailTable.statusTypeCode = Plan.classifyingProcessingStatusType.statusTypeCode;
            PharmacyDetailTable.recordType = "Plan";
            PharmacyDetailTable.FileName = filename;
            PharmacyDetailTable.insurancePlanIdentifier = Plan.insurancePlanIdentifier;
            ErrorCheck(PharmacyDetailTable, ErrorMessage);
            context.IT0363_EdgeReleaseDetail_A.Add(PharmacyDetailTable);
            //context.SaveChanges();
        }

        private static void PharmacyDetailIssuer(EFPhpArchive.PHPArchvEntities context, IssuerPlanPharmacyClaimDetail Detail, PharmacyClaimIssuerProcessingResult Issuer, string filename, ErrorMessageType ErrorMessage = null)
        {
            EFPhpArchive.IT0363_EdgeReleaseDetail_A PharmacyDetailTable = new EFPhpArchive.IT0363_EdgeReleaseDetail_A();
            PharmacyDetailTable.FileType = "P";
            PharmacyDetailTable.issuerID = Detail.includedFileHeader.issuerID;
            PharmacyDetailTable.inboundFileIdentifier = Detail.includedFileHeader.inboundFileIdentifier;
            PharmacyDetailTable.recordID = Convert.ToInt32(Issuer.issuerRecordIdentifier);
            PharmacyDetailTable.Identifier = Issuer.issuerIdentifier;
            PharmacyDetailTable.statusTypeCode = Issuer.classifyingProcessingStatusType.statusTypeCode;
            PharmacyDetailTable.recordType = "Issuer";
            PharmacyDetailTable.FileName = filename;
            PharmacyDetailTable.insurancePlanIdentifier = "";
            ErrorCheck(PharmacyDetailTable, ErrorMessage);
            context.IT0363_EdgeReleaseDetail_A.Add(PharmacyDetailTable);
            // context.SaveChanges();
        }

        private static void ErrorCheck(EFPhpArchive.IT0363_EdgeReleaseDetail_A PharmacyDetailTable, ErrorMessageType ErrorMessage = null)
        {
            if (ErrorMessage == null || ErrorMessage.offendingElementValue == null)
            {
                PharmacyDetailTable.ErrorElementValue = "";
            }
            else
            {

                PharmacyDetailTable.ErrorElementValue = string.Join(",", ErrorMessage.offendingElementValue);
            }

            if (ErrorMessage == null || ErrorMessage.offendingElementName == null)
            {
                PharmacyDetailTable.ErrorName = "";
            }
            else
            {

                PharmacyDetailTable.ErrorName = string.Join(",", ErrorMessage.offendingElementName);
            }

            if (ErrorMessage == null || ErrorMessage.offendingElementErrorTypeMessage == null)
            {
                PharmacyDetailTable.ErrorMessage = "";
            }
            else
            {
                PharmacyDetailTable.ErrorMessage = string.Join(",", ErrorMessage.offendingElementErrorTypeMessage);
            }
            if (ErrorMessage == null || ErrorMessage.offendingElementErrorTypeCode == null)
            {
                PharmacyDetailTable.ErrorTypeCode = "";
            }
            else
            {
                PharmacyDetailTable.ErrorTypeCode = string.Join(",", ErrorMessage.offendingElementErrorTypeCode);
            }
            if (ErrorMessage == null || ErrorMessage.offendingElementErrorTypeDetail == null)
            {
                PharmacyDetailTable.ErrorTypeDetail = "";
            }
            else
            {
                PharmacyDetailTable.ErrorTypeDetail = string.Join(",", ErrorMessage.offendingElementErrorTypeDetail);
            }
        }
    }
    #region Detail


    // 
    // This source code was auto-generated by xsd, Version=4.0.30319.33440.
    // 


    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://vo.edge.fm.cms.hhs.gov")]
    [System.Xml.Serialization.XmlRootAttribute("issuerPlanPharmacyClaimDetail", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
    public partial class IssuerPlanPharmacyClaimDetail
    {

        private CommonOutboundFileHeader includedFileHeaderField;

        private PharmacyClaimIssuerProcessingResult[] includedIssuerProcessingResultField;

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
        public PharmacyClaimIssuerProcessingResult[] includedIssuerProcessingResult
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
    [System.Xml.Serialization.XmlRootAttribute("pharmacyClaimProcessingResult", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
    public partial class PharmacyClaimProcessingResult
    {

        private string pharmacyClaimRecordIdentifierField;

        private string pharmacyClaimIdentifierField;

        private SubmissionProcessingStatusType classifyingProcessingStatusTypeField;

        private ErrorMessageType[] recordedErrorField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
        public string pharmacyClaimRecordIdentifier
        {
            get
            {
                return this.pharmacyClaimRecordIdentifierField;
            }
            set
            {
                this.pharmacyClaimRecordIdentifierField = value;
            }
        }

        /// <remarks/>
        public string pharmacyClaimIdentifier
        {
            get
            {
                return this.pharmacyClaimIdentifierField;
            }
            set
            {
                this.pharmacyClaimIdentifierField = value;
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
    [System.Xml.Serialization.XmlRootAttribute("pharmacyClaimPlanProcessingResult", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
    public partial class PharmacyClaimPlanProcessingResult
    {

        private string planRecordIdentifierField;

        private string insurancePlanIdentifierField;

        private SubmissionProcessingStatusType classifyingProcessingStatusTypeField;

        private ErrorMessageType[] recordedErrorField;

        private PharmacyClaimProcessingResult[] includedClaimProcessingResultField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
        public string planRecordIdentifier
        {
            get
            {
                return this.planRecordIdentifierField;
            }
            set
            {
                this.planRecordIdentifierField = value;
            }
        }

        /// <remarks/>
        public string insurancePlanIdentifier
        {
            get
            {
                return this.insurancePlanIdentifierField;
            }
            set
            {
                this.insurancePlanIdentifierField = value;
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
        [System.Xml.Serialization.XmlElementAttribute("includedClaimProcessingResult")]
        public PharmacyClaimProcessingResult[] includedClaimProcessingResult
        {
            get
            {
                return this.includedClaimProcessingResultField;
            }
            set
            {
                this.includedClaimProcessingResultField = value;
            }
        }
    }

    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://vo.edge.fm.cms.hhs.gov")]
    [System.Xml.Serialization.XmlRootAttribute("pharmacyClaimIssuerProcessingResult", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
    public partial class PharmacyClaimIssuerProcessingResult
    {

        private string issuerRecordIdentifierField;

        private string issuerIdentifierField;

        private SubmissionProcessingStatusType classifyingProcessingStatusTypeField;

        private ErrorMessageType[] recordedErrorField;

        private PharmacyClaimPlanProcessingResult[] includedPlanProcessingResultField;

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
        [System.Xml.Serialization.XmlElementAttribute("includedPlanProcessingResult")]
        public PharmacyClaimPlanProcessingResult[] includedPlanProcessingResult
        {
            get
            {
                return this.includedPlanProcessingResultField;
            }
            set
            {
                this.includedPlanProcessingResultField = value;
            }
        }
    }


    #endregion
    #region Summary

    // 
    // This source code was auto-generated by xsd, Version=4.0.30319.33440.
    // 


    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://vo.edge.fm.cms.hhs.gov")]
    [System.Xml.Serialization.XmlRootAttribute("pharmacyClaimSummaryAcceptReject", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
    public partial class PharmacyClaimSummaryAcceptReject
    {

        //private CommonOutboundFileHeader includedFileHeaderField;

        //private string inboundFileHashField;

        //private string detailReportHashField;

        //private ErrorCodeCounts[] includedErrorCodeFrequencyField;

        //private ClaimCountMessageType includedRecordCountsField;

        //private PharmacyClaimIssuerSummary includedIssuerSummaryField;

        //private PharmacyClaimPlanSummary[] includedPlanSummaryField;

        /// <remarks/>
        //public CommonOutboundFileHeader includedFileHeader
        //{
        //    get
        //    {
        //        return this.includedFileHeaderField;
        //    }
        //    set
        //    {
        //        this.includedFileHeaderField = value;
        //    }
        //}

        ///// <remarks/>
        //public string inboundFileHash
        //{
        //    get
        //    {
        //        return this.inboundFileHashField;
        //    }
        //    set
        //    {
        //        this.inboundFileHashField = value;
        //    }
        //}

        ///// <remarks/>
        //public string detailReportHash
        //{
        //    get
        //    {
        //        return this.detailReportHashField;
        //    }
        //    set
        //    {
        //        this.detailReportHashField = value;
        //    }
        //}

        ///// <remarks/>
        //public ClaimCountMessageType includedRecordCounts
        //{
        //    get
        //    {
        //        return this.includedRecordCountsField;
        //    }
        //    set
        //    {
        //        this.includedRecordCountsField = value;
        //    }
        //}

        ///// <remarks/>
        //public PharmacyClaimIssuerSummary includedIssuerSummary
        //{
        //    get
        //    {
        //        return this.includedIssuerSummaryField;
        //    }
        //    set
        //    {
        //        this.includedIssuerSummaryField = value;
        //    }
        //}

        ///// <remarks/>
        //[System.Xml.Serialization.XmlElementAttribute("includedErrorCodeFrequency")]
        //public ErrorCodeCounts[] includedErrorCodeFrequency
        //{
        //    get
        //    {
        //        return this.includedErrorCodeFrequencyField;
        //    }
        //    set
        //    {
        //        this.includedErrorCodeFrequencyField = value;
        //    }
        //}

        ///// <remarks/>
        //[System.Xml.Serialization.XmlElementAttribute("includedPlanSummary")]
        //public PharmacyClaimPlanSummary[] includedPlanSummary
        //{
        //    get
        //    {
        //        return this.includedPlanSummaryField;
        //    }
        //    set
        //    {
        //        this.includedPlanSummaryField = value;
        //    }
        //}
    }



    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://vo.edge.fm.cms.hhs.gov")]
    [System.Xml.Serialization.XmlRootAttribute("pharmacyClaimPlanMonth", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
    public partial class PharmacyClaimPlanMonth
    {

        private string planMonthField;

        private ClaimCountMessageType includedPlanMonthCountsField;

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
        public ClaimCountMessageType includedPlanMonthCounts
        {
            get
            {
                return this.includedPlanMonthCountsField;
            }
            set
            {
                this.includedPlanMonthCountsField = value;
            }
        }
    }


    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://vo.edge.fm.cms.hhs.gov")]
    [System.Xml.Serialization.XmlRootAttribute("pharmacyClaimPlanYear", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
    public partial class PharmacyClaimPlanYear
    {

        private string planCalendarYearField;

        private PharmacyClaimPlanMonth[] includedPlanMonthField;

        /// <remarks/>
        public string planCalendarYear
        {
            get
            {
                return this.planCalendarYearField;
            }
            set
            {
                this.planCalendarYearField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("includedPlanMonth")]
        public PharmacyClaimPlanMonth[] includedPlanMonth
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
    [System.Xml.Serialization.XmlRootAttribute("pharmacyClaimPlanSummary", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
    public partial class PharmacyClaimPlanSummary
    {

        private string planRecordIdentifierField;

        private string planIdentifierField;

        private SubmissionProcessingStatusType classifyingProcessingStatusTypeField;

        private PharmacyClaimPlanYear[] includedPlanYearField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
        public string planRecordIdentifier
        {
            get
            {
                return this.planRecordIdentifierField;
            }
            set
            {
                this.planRecordIdentifierField = value;
            }
        }

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
        [System.Xml.Serialization.XmlElementAttribute("includedPlanYear")]
        public PharmacyClaimPlanYear[] includedPlanYear
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
    [System.Xml.Serialization.XmlRootAttribute("pharmacyClaimIssuerMonth", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
    public partial class PharmacyClaimIssuerMonth
    {

        private string issuerMonthField;

        private ClaimCountMessageType includedIssuerMonthCountsField;

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
        public ClaimCountMessageType includedIssuerMonthCounts
        {
            get
            {
                return this.includedIssuerMonthCountsField;
            }
            set
            {
                this.includedIssuerMonthCountsField = value;
            }
        }
    }

    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://vo.edge.fm.cms.hhs.gov")]
    [System.Xml.Serialization.XmlRootAttribute("pharmacyClaimIssuerYear", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
    public partial class PharmacyClaimIssuerYear
    {

        private string issuerYearField;

        private PharmacyClaimIssuerMonth[] includedIssuerMonthField;

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
        [System.Xml.Serialization.XmlElementAttribute("includedIssuerMonth")]
        public PharmacyClaimIssuerMonth[] includedIssuerMonth
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
    [System.Xml.Serialization.XmlRootAttribute("pharmacyClaimIssuerSummary", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
    public partial class PharmacyClaimIssuerSummary
    {

        private string issuerRecordIdentifierField;

        private string issuerIdentifierField;

        private SubmissionProcessingStatusType classifyingProcessingStatusTypeField;

        private string issuerPlanCountField;

        private PharmacyClaimIssuerYear[] includedIssuerYearField;

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
        [System.Xml.Serialization.XmlElementAttribute("includedIssuerYear")]
        public PharmacyClaimIssuerYear[] includedIssuerYear
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
