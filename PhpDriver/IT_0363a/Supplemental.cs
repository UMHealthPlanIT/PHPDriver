using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utilities;

namespace Driver.IT_0363a
{

    class Supplemental
    {
        public static int RecordCount { get; set; }
        public static int detailRecordCount { get; set; }
        public static void SupplentalSubmission(IT_0363ACAEdgeReporting caller, String EDGEenvironment, ProgramOptions opt)
        {

            List<string> supplementalFiles = new List<string>();
            RecordCount = 0;

            Data.AppNames EdgeDatabase20662 = Control.GetEdgeDatabase("", EDGEenvironment);

            Data.AppNames EdgeDatabase60829 = Control.GetEdgeDatabase("", EDGEenvironment);

            //CheckForClaimsNotYetSubmitted(caller, BaseQuery);  //Allowing EDGE to tell us whether it has the claim. IM-569190
            supplementalFiles.AddRange(RunSupplementalDx(caller, "", EdgeDatabase20662, EdgeDatabase60829));
            supplementalFiles.AddRange(RunSupplementalDx(caller, "", EdgeDatabase60829, EdgeDatabase20662));

            if (supplementalFiles.Count == 0)
            {
                SendAlerts.Send(caller.ProcessId, 0, "No Supplemental Diagnosis Files Created", FtpFactory.buildBody(supplementalFiles, false), caller);
            }
            else
            {

                SendAlerts.Send(caller.ProcessId, 0, "Supplemental Diagnosis Files Submitted", FtpFactory.buildBody(supplementalFiles, false), caller);

            }
        }

        /// <summary>
        /// Pulls the supplemental diagnosis claims from the config table, then goes to the Edge tables to fill in the additional detail we need to construct the submission
        /// and then generates the XML file
        /// </summary>
        /// <param name="caller">Calling program (this)</param>
        /// <param name="issuer">Edge Issuer - 20662 or 60829</param>
        /// <param name="edgeDatabase">Datbase for the issuer</param>
        /// <param name="otherEdgeDb">Database for the other issue (so we can check for dxs that don't match to claims in either)</param>
        /// <returns></returns>
        private static List<string> RunSupplementalDx(IT_0363ACAEdgeReporting caller, String issuer, Data.AppNames edgeDatabase, Data.AppNames otherEdgeDb)
        {
            detailRecordCount = 0;

            List<DxExtract> rawSupplementalDiagnosis = ExtractFactory.ConnectAndQuery<DxExtract>(caller.LoggerPhpConfig, @"SELECT [originalClaimIdentifier]
      ,[diagnosisTypeCode]
      ,[supplementalDiagnosisCode]
      ,/*SUBSTRING(*/UPPER(EntryType)/*, 1,1)*/ AS [EntryType]
  FROM [PHPConfg].[dbo].[IT0363_SupplementalDx_C]
  where coalesce(Submitted,'N') <> 'Y'
    AND FreezeFlag = 'N'
    /*AND EntryType = 'ADD'*/").ToList();

            String MySqlDatabaseName;
            if (caller.TestMode)
            {
                MySqlDatabaseName = "EDGE_SRVR_TEST";
            }
            else
            {
                MySqlDatabaseName = "EDGE_SRVR_PROD";
            }

            List<DxExtract> supplementalDiagnosis = GetSupplementalClaimsToSubmit(issuer, edgeDatabase, otherEdgeDb, rawSupplementalDiagnosis, MySqlDatabaseName);
            //List<DxExtract> supplementalDiagnosisVoids = GetSupplementalClaimsToSubmit(issuer, edgeDatabase, otherEdgeDb, rawSupplementalDiagnosisVoids, MySqlDatabaseName);

            List<string> supplementalFiles = new List<string>();

            if (supplementalDiagnosis.Count == 0)
            {
                //SendAlerts.Send(this.ProcessId, 6000, "Issuer " + issuer + " didn't have any supplemental diagnoses to submit", "", this);
                return supplementalFiles;
            }

            //int supplementalDxLines = supplementalDiagnosis.GroupBy(x => x.originalClaimIdentifier).Distinct().Count();//Need to fix record count. Believe issuerFileDetailTotalQuantity or fileDetailTotalQuantity may be wrong
            RecordCount = 1;
            EdgeServerSupplementalClaimSubmission suppDxFile = new EdgeServerSupplementalClaimSubmission();

            suppDxFile.fileIdentifier = "D" + DateTime.Now.ToString("MMddyy") + "T" + DateTime.Now.ToString("HHmm");
            suppDxFile.executionZoneCode = (caller.TestMode ? EdgeServerSupplementalClaimSubmissionExecutionZoneCode.T : EdgeServerSupplementalClaimSubmissionExecutionZoneCode.P);
            suppDxFile.interfaceControlReleaseNumber = "02.01.07";
            suppDxFile.submissionTypeCode = EdgeServerSupplementalClaimSubmissionSubmissionTypeCode.S;

            IncludedSupplementalDiagnosisIssuer includedIssuer = new IncludedSupplementalDiagnosisIssuer();

            includedIssuer.issuerIdentifier = issuer;
            includedIssuer.recordIdentifier = RecordCount++.ToString();

            includedIssuer.includedSupplementalDiagnosisPlan = GetPlans(issuer, supplementalDiagnosis);
            suppDxFile.fileDetailTotalQuantity = detailRecordCount.ToString();
            includedIssuer.issuerFileDetailTotalQuantity = detailRecordCount.ToString();

            suppDxFile.includedSupplementalDiagnosisIssuer = includedIssuer;

            String runType = caller.TestMode ? "T" : "P";
            caller.LoggerReportYearDir(caller.LoggerOutputYearDir + @"\Supplemental");
            String outputPath = caller.LoggerOutputYearDir + @"Supplemental\" + issuer + ".S.D" + DateTime.Now.ToString("MMddyy") + "T" + DateTime.Now.ToString("HHmmssff") + "." + runType + ".xml";

            suppDxFile.generationDateTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");

            supplementalFiles.Add(outputPath);
            XMLTranslations.Seril<EdgeServerSupplementalClaimSubmission>(outputPath, suppDxFile, caller.TestMode);

            //PushFile(outputPath, issuer);

            NoteAsSubmitted(supplementalDiagnosis);
            return supplementalFiles;
        }

        private static List<DxExtract> GetSupplementalClaimsToSubmit(string issuer, Data.AppNames edgeDatabase, Data.AppNames otherEdgeDatabase, List<DxExtract> rawSupplementalDiagnosis, string MySqlDatabaseName)
        {
            List<DxExtract> supplementalDiagnosis = new List<DxExtract>();

            foreach (DxExtract diag in rawSupplementalDiagnosis)
            {


                String checkEdgeForClaim = String.Format(@"SELECT MEDICAL_CLAIM_ID as originalClaimIdentifier
, RECEIVED_INSURED_MEMBER_ID as insuredMemberIdentifier
, INSURANCE_PLAN_ID as insurancePlanIdentifier
, STATEMENT_COVERS_FROM_DATE as statementCoverFromDate
, STATEMENT_COVERS_TO_DATE as statementCoverToDate
FROM {0}.MEDICAL_CLAIM
where MEDICAL_CLAIM_ID = '{1}';", MySqlDatabaseName, diag.originalClaimIdentifier);


                DxExtract mainClaimDetails = ExtractFactory.ConnectAndQuery<DxExtract>(edgeDatabase, checkEdgeForClaim).ToList().FirstOrDefault();

                if (mainClaimDetails != null && mainClaimDetails.insurancePlanIdentifier.ToString().StartsWith(issuer))
                {
                    diag.insuredMemberIdentifier = mainClaimDetails.insuredMemberIdentifier;
                    diag.insurancePlanIdentifier = mainClaimDetails.insurancePlanIdentifier;
                    diag.statementCoverFromDate = mainClaimDetails.statementCoverFromDate;
                    diag.statementCoverToDate = mainClaimDetails.statementCoverToDate;
                    supplementalDiagnosis.Add(diag);
                }
                else if (mainClaimDetails == null)
                {

                    //DxExtract otherIssuerClaim = ExtractFactory.ConnectAndQuery<DxExtract>(otherEdgeDatabase, checkEdgeForClaim).ToList().FirstOrDefault();

                    //if(otherIssuerClaim == null) //this means the claim was not in either database
                    //{
                    //    using (var db = new EFPhpConfig.PHPConfgEntities())
                    //    {
                    //        /*string EntryType = "";
                    //        if(diag.EntryType == 'A')
                    //        {
                    //            EntryType = "ADD";
                    //        }
                    //        else if(diag.EntryType == 'D')
                    //        {
                    //            EntryType = "DELETE";
                    //        }*/

                    //        var submissionRec = db.IT0363_SupplementalDx_C.Find(diag.originalClaimIdentifier, diag.diagnosisTypeCode, diag.supplementalDiagnosisCode, diag.EntryType);
                    //        submissionRec.NotSubmittedReason = "Original Claim Not Found in Our Submission History on " + DateTime.Today.ToString("yyyy-MM-dd");
                    //        submissionRec.SubmittedDate = DateTime.Today;
                    //        db.SaveChanges();
                    //    }
                    //}

                }


            }

            return supplementalDiagnosis;
        }

        //private static void NoteAsSubmitted(List<DxExtract> supplementalDiagnosis)
        //{
        //    using (var db = new EFPhpConfig.PHPConfgEntities())
        //    {

        //        foreach (DxExtract ext in supplementalDiagnosis)
        //        {
        //            /*string EntryType = "";
        //            if (ext.EntryType == 'A')
        //            {
        //                EntryType = "ADD";
        //            }
        //            else if (ext.EntryType == 'D')
        //            {
        //                EntryType = "DELETE";
        //            }*/

        //            var submissionRec = db.IT0363_SupplementalDx_C.Find(ext.originalClaimIdentifier, ext.diagnosisTypeCode, ext.supplementalDiagnosisCode, ext.EntryType);
        //            submissionRec.Submitted = "Y";
        //            submissionRec.SubmittedDate = DateTime.Today;
        //        }
        //        db.SaveChanges();

        //    }

        //}

        //public static void PushFile(Driver.IT_0363 caller, String filePush, String issuer)
        //{
        //    String ftpSite = (issuer == "" ? "EdgeServer1" : "EdgeServer2");
        //    FileTransfer.FtpIpSwitchPush(caller.LoggerWorkDir, filePush, ftpSite, caller, "/opt/edge/ingest/inbox");
        //}


        private static IncludedSupplementalDiagnosisPlan[] GetPlans(String issuer, List<DxExtract> supplementalDiagnosis)
        {
            List<String> plans = (from r in supplementalDiagnosis orderby r.insurancePlanIdentifier select r.insurancePlanIdentifier).Distinct().ToList();

            List<IncludedSupplementalDiagnosisPlan> plansElement = new List<IncludedSupplementalDiagnosisPlan>();

            foreach (String plan in plans)
            {
                IncludedSupplementalDiagnosisPlan includedPlan = new IncludedSupplementalDiagnosisPlan();
                includedPlan.recordIdentifier = RecordCount++.ToString();
                includedPlan.insurancePlanIdentifier = plan;

                includedPlan.includedSupplementalDiagnosisDetail = GetDiagnoses(plan, supplementalDiagnosis);

                includedPlan.insurancePlanFileDetailTotalQuantity = includedPlan.includedSupplementalDiagnosisDetail.Count().ToString();
                plansElement.Add(includedPlan);
            }

            return plansElement.ToArray();

        }

        private static IncludedSupplementalDiagnosisDetail[] GetDiagnoses(String hios, List<DxExtract> suppDiags)
        {
            IEnumerable<DxExtract> diagsForPlan = suppDiags.Where(x => x.insurancePlanIdentifier == hios);

            IEnumerable<String> Claims = diagsForPlan.Select(x => x.originalClaimIdentifier).Distinct();

            List<IncludedSupplementalDiagnosisDetail> includedDiags = new List<IncludedSupplementalDiagnosisDetail>();

            foreach (string claim in Claims)
            {
                IncludedSupplementalDiagnosisDetail suppDiagDetail = new IncludedSupplementalDiagnosisDetail();
                List<DxExtract> DxRecs = diagsForPlan.Where(x => x.originalClaimIdentifier == claim).ToList();
                List<DxExtract> adds = new List<DxExtract>();
                List<DxExtract> deletes = new List<DxExtract>();
                if (DxRecs.Count > 1)
                {
                    foreach(DxExtract rec in DxRecs)
                    {
                        if (rec.EntryType == "ADD")
                            adds.Add(rec);
                        else if (rec.EntryType == "DELETE")
                            deletes.Add(rec);
                    }
                    if(adds.Count > 0)
                    {
                        includedDiags.Add(SetDiagnosesForClaim(hios, claim, adds));
                    }
                    if(deletes.Count > 0)
                    {
                        if(adds.Count > 0)
                        {
                            System.Threading.Thread.Sleep(1000);
                        }
                        includedDiags.Add(SetDiagnosesForClaim(hios, claim, deletes));
                    }
                }
                else
                {
                    includedDiags.Add(SetDiagnosesForClaim(hios, claim, DxRecs));
                }

                //includedDiags.Add(suppDiagDetail);
            }
            return includedDiags.ToArray();
        }
        private static IncludedSupplementalDiagnosisDetail SetDiagnosesForClaim(string hios, string claim, List<DxExtract> DxRecs)
        {
            detailRecordCount++;
            IncludedSupplementalDiagnosisDetail suppDiagDetail = new IncludedSupplementalDiagnosisDetail();
            suppDiagDetail.recordIdentifier = RecordCount++.ToString();
            suppDiagDetail.insuredMemberIdentifier = DxRecs[0].insuredMemberIdentifier.ToString();
            suppDiagDetail.supplementalDiagnosisDetailRecordIdentifier = (claim + DxRecs[0].diagnosisTypeCode + DxRecs[0].supplementalDiagnosisCode).TrimEnd();
            suppDiagDetail.originalClaimIdentifier = claim;
            suppDiagDetail.detailRecordProcessedDateTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");

            /*bool match = true;
            char c = DxRecs[0].EntryType;
            foreach(DxExtract rec in DxRecs)
            {
                if (c != rec.EntryType)
                    match = false;
            }*/

            IncludedSupplementalDiagnosisDetailAddDeleteVoidCode code;
            switch (DxRecs[0].EntryType)
            {//just use delete
                case "ADD":
                    code = IncludedSupplementalDiagnosisDetailAddDeleteVoidCode.A;
                    break;
                case "VOID":
                    code = IncludedSupplementalDiagnosisDetailAddDeleteVoidCode.V;
                    break;
                case "DELETE":
                    code = IncludedSupplementalDiagnosisDetailAddDeleteVoidCode.D;
                    break;
                default:
                    code = IncludedSupplementalDiagnosisDetailAddDeleteVoidCode.A;
                    break;
            }
            suppDiagDetail.addDeleteVoidCode = code;
            if (code == IncludedSupplementalDiagnosisDetailAddDeleteVoidCode.V)
            {
                suppDiagDetail.originalSupplementalDetailID = suppDiagDetail.supplementalDiagnosisDetailRecordIdentifier;
            }
            else
            {
                suppDiagDetail.originalSupplementalDetailID = "";
            }
            suppDiagDetail.serviceFromDate = Convert.ToDateTime(DxRecs[0].statementCoverFromDate);
            suppDiagDetail.serviceToDate = Convert.ToDateTime(DxRecs[0].statementCoverToDate);
            suppDiagDetail.diagnosisTypeCode = (DxRecs[0].diagnosisTypeCode == "9" ? IncludedSupplementalDiagnosisDetailDiagnosisTypeCode.Item01 : IncludedSupplementalDiagnosisDetailDiagnosisTypeCode.Item02);
            suppDiagDetail.supplementalDiagnosisCode = DxRecs.Select(x => x.supplementalDiagnosisCode.TrimEnd()).ToArray();

            return suppDiagDetail;
        }


        public class DxExtract
        {
            public String originalClaimIdentifier { get; set; }
            public String diagnosisTypeCode { get; set; }
            public String supplementalDiagnosisCode { get; set; }
            public String insuredMemberIdentifier { get; set; }
            public String insurancePlanIdentifier { get; set; }
            public DateTime statementCoverFromDate { get; set; }
            public DateTime statementCoverToDate { get; set; }
            public string EntryType { get; set; }
        }

        private class DxMissingOriginalExtract
        {
            public String originalClaimIdentifier { get; set; }
            public String diagnosisTypeCode { get; set; }
            public String supplementalDiagnosisCode { get; set; }
        }
    }


    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://vo.edge.fm.cms.hhs.gov")]
    [System.Xml.Serialization.XmlRootAttribute("edgeServerSupplementalClaimSubmission", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
    public partial class EdgeServerSupplementalClaimSubmission
    {

        private string fileIdentifierField;

        private EdgeServerSupplementalClaimSubmissionExecutionZoneCode executionZoneCodeField;

        private string interfaceControlReleaseNumberField;

        private String generationDateTimeField;

        private EdgeServerSupplementalClaimSubmissionSubmissionTypeCode submissionTypeCodeField;

        private string fileDetailTotalQuantityField;

        private IncludedSupplementalDiagnosisIssuer includedSupplementalDiagnosisIssuerField;

        /// <remarks/>
        public string fileIdentifier
        {
            get
            {
                return this.fileIdentifierField;
            }
            set
            {
                this.fileIdentifierField = value;
            }
        }

        /// <remarks/>
        public EdgeServerSupplementalClaimSubmissionExecutionZoneCode executionZoneCode
        {
            get
            {
                return this.executionZoneCodeField;
            }
            set
            {
                this.executionZoneCodeField = value;
            }
        }

        /// <remarks/>
        public string interfaceControlReleaseNumber
        {
            get
            {
                return this.interfaceControlReleaseNumberField;
            }
            set
            {
                this.interfaceControlReleaseNumberField = value;
            }
        }

        /// <remarks/>
        public String generationDateTime
        {
            get
            {
                return this.generationDateTimeField;
            }
            set
            {
                this.generationDateTimeField = value;
            }
        }

        /// <remarks/>
        public EdgeServerSupplementalClaimSubmissionSubmissionTypeCode submissionTypeCode
        {
            get
            {
                return this.submissionTypeCodeField;
            }
            set
            {
                this.submissionTypeCodeField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
        public string fileDetailTotalQuantity
        {
            get
            {
                return this.fileDetailTotalQuantityField;
            }
            set
            {
                this.fileDetailTotalQuantityField = value;
            }
        }

        /// <remarks/>
        public IncludedSupplementalDiagnosisIssuer includedSupplementalDiagnosisIssuer
        {
            get
            {
                return this.includedSupplementalDiagnosisIssuerField;
            }
            set
            {
                this.includedSupplementalDiagnosisIssuerField = value;
            }
        }
    }

    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://vo.edge.fm.cms.hhs.gov")]
    public enum EdgeServerSupplementalClaimSubmissionExecutionZoneCode
    {

        /// <remarks/>
        T,

        /// <remarks/>
        P,
    }

    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://vo.edge.fm.cms.hhs.gov")]
    public enum EdgeServerSupplementalClaimSubmissionSubmissionTypeCode
    {

        /// <remarks/>
        S,
    }

    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://vo.edge.fm.cms.hhs.gov")]
    [System.Xml.Serialization.XmlRootAttribute("includedSupplementalDiagnosisIssuer", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
    public partial class IncludedSupplementalDiagnosisIssuer
    {

        private string recordIdentifierField;

        private string issuerIdentifierField;

        private string issuerFileDetailTotalQuantityField;

        private IncludedSupplementalDiagnosisPlan[] includedSupplementalDiagnosisPlanField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
        public string recordIdentifier
        {
            get
            {
                return this.recordIdentifierField;
            }
            set
            {
                this.recordIdentifierField = value;
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
        [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
        public string issuerFileDetailTotalQuantity
        {
            get
            {
                return this.issuerFileDetailTotalQuantityField;
            }
            set
            {
                this.issuerFileDetailTotalQuantityField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("includedSupplementalDiagnosisPlan")]
        public IncludedSupplementalDiagnosisPlan[] includedSupplementalDiagnosisPlan
        {
            get
            {
                return this.includedSupplementalDiagnosisPlanField;
            }
            set
            {
                this.includedSupplementalDiagnosisPlanField = value;
            }
        }
    }

    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://vo.edge.fm.cms.hhs.gov")]
    [System.Xml.Serialization.XmlRootAttribute("includedSupplementalDiagnosisPlan", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
    public partial class IncludedSupplementalDiagnosisPlan
    {

        private string recordIdentifierField;

        private string insurancePlanIdentifierField;

        private string insurancePlanFileDetailTotalQuantityField;

        private IncludedSupplementalDiagnosisDetail[] includedSupplementalDiagnosisDetailField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
        public string recordIdentifier
        {
            get
            {
                return this.recordIdentifierField;
            }
            set
            {
                this.recordIdentifierField = value;
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
        [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
        public string insurancePlanFileDetailTotalQuantity
        {
            get
            {
                return this.insurancePlanFileDetailTotalQuantityField;
            }
            set
            {
                this.insurancePlanFileDetailTotalQuantityField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("includedSupplementalDiagnosisDetail")]
        public IncludedSupplementalDiagnosisDetail[] includedSupplementalDiagnosisDetail
        {
            get
            {
                return this.includedSupplementalDiagnosisDetailField;
            }
            set
            {
                this.includedSupplementalDiagnosisDetailField = value;
            }
        }
    }

    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://vo.edge.fm.cms.hhs.gov")]
    [System.Xml.Serialization.XmlRootAttribute("includedSupplementalDiagnosisDetail", Namespace = "http://vo.edge.fm.cms.hhs.gov", IsNullable = false)]
    public partial class IncludedSupplementalDiagnosisDetail
    {

        private string recordIdentifierField;

        private string insuredMemberIdentifierField;

        private string supplementalDiagnosisDetailRecordIdentifierField;

        private string originalClaimIdentifierField;

        private String detailRecordProcessedDateTimeField;

        private IncludedSupplementalDiagnosisDetailAddDeleteVoidCode addDeleteVoidCodeField;

        private string originalSupplementalDetailIDField;

        private System.DateTime serviceFromDateField;

        private System.DateTime serviceToDateField;

        private IncludedSupplementalDiagnosisDetailDiagnosisTypeCode diagnosisTypeCodeField;

        private string[] supplementalDiagnosisCodeField;

        private IncludedSupplementalDiagnosisDetailSourceCode sourceCodeField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType = "integer")]
        public string recordIdentifier
        {
            get
            {
                return this.recordIdentifierField;
            }
            set
            {
                this.recordIdentifierField = value;
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
        public string supplementalDiagnosisDetailRecordIdentifier
        {
            get
            {
                return this.supplementalDiagnosisDetailRecordIdentifierField;
            }
            set
            {
                this.supplementalDiagnosisDetailRecordIdentifierField = value;
            }
        }

        /// <remarks/>
        public string originalClaimIdentifier
        {
            get
            {
                return this.originalClaimIdentifierField;
            }
            set
            {
                this.originalClaimIdentifierField = value;
            }
        }

        /// <remarks/>
        public String detailRecordProcessedDateTime
        {
            get
            {
                return this.detailRecordProcessedDateTimeField;
            }
            set
            {
                this.detailRecordProcessedDateTimeField = value;
            }
        }

        /// <remarks/>
        public IncludedSupplementalDiagnosisDetailAddDeleteVoidCode addDeleteVoidCode
        {
            get
            {
                return this.addDeleteVoidCodeField;
            }
            set
            {
                this.addDeleteVoidCodeField = value;
            }
        }

        /// <remarks/>
        public string originalSupplementalDetailID
        {
            get
            {
                return this.originalSupplementalDetailIDField;
            }
            set
            {
                this.originalSupplementalDetailIDField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType = "date")]
        public System.DateTime serviceFromDate
        {
            get
            {
                return this.serviceFromDateField;
            }
            set
            {
                this.serviceFromDateField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType = "date")]
        public System.DateTime serviceToDate
        {
            get
            {
                return this.serviceToDateField;
            }
            set
            {
                this.serviceToDateField = value;
            }
        }

        /// <remarks/>
        public IncludedSupplementalDiagnosisDetailDiagnosisTypeCode diagnosisTypeCode
        {
            get
            {
                return this.diagnosisTypeCodeField;
            }
            set
            {
                this.diagnosisTypeCodeField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("supplementalDiagnosisCode")]
        public string[] supplementalDiagnosisCode
        {
            get
            {
                return this.supplementalDiagnosisCodeField;
            }
            set
            {
                this.supplementalDiagnosisCodeField = value;
            }
        }

        /// <remarks/>
        public IncludedSupplementalDiagnosisDetailSourceCode sourceCode
        {
            get
            {
                return this.sourceCodeField;
            }
            set
            {
                this.sourceCodeField = value;
            }
        }
    }

    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://vo.edge.fm.cms.hhs.gov")]
    public enum IncludedSupplementalDiagnosisDetailAddDeleteVoidCode
    {
        //Right here 
        /// <remarks/>
        A,

        /// <remarks/>
        D,

        /// <remarks/>
        V,
    }

    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://vo.edge.fm.cms.hhs.gov")]
    public enum IncludedSupplementalDiagnosisDetailDiagnosisTypeCode
    {

        /// <remarks/>
        [System.Xml.Serialization.XmlEnumAttribute("01")]
        Item01,

        /// <remarks/>
        [System.Xml.Serialization.XmlEnumAttribute("02")]
        Item02,
    }

    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.33440")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://vo.edge.fm.cms.hhs.gov")]
    public enum IncludedSupplementalDiagnosisDetailSourceCode
    {

        /// <remarks/>
        MR,

        /// <remarks/>
        EDI,
    }

}
