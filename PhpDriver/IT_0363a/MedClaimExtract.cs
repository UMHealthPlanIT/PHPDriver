using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.IO;
using Utilities;
//using Driver.EFPhpConfig;
using System.Data;

namespace Driver.IT_0363a
{
    public class MedClaimExtract
    {
        private IT_0363ACAEdgeReporting caller;
        private string EdgeEnvironment;
        private string reportYear;
        private string MySqlDatabaseName;
        List<MedicalClaimErrorToReport> preLoadErrorsForClaimsTeam;

        public MedClaimExtract(IT_0363ACAEdgeReporting callingProc, string EdgeTargetEnvironment, String reportingYear)
        {
            caller = callingProc;
            EdgeEnvironment = EdgeTargetEnvironment;
            reportYear = reportingYear;

            if (caller.TestMode)
            {
                MySqlDatabaseName = "EDGE_SRVR_TEST";
            }
            else
            {
                MySqlDatabaseName = "EDGE_SRVR_PROD";
            }

            preLoadErrorsForClaimsTeam = new List<MedicalClaimErrorToReport>();

            RunExtract();
        }

        /// <summary>
        /// Generates the Edge Medical Extracts
        /// </summary>
        public void RunExtract()
        {
            //Run the query to truncate the existing claims for the report year records in archive tables.            
            String removePriorClaims = String.Format(@"delete from [dbo].[IT0363_Edge234ClmExt_A]
                                                        where Report_Year = '{0}'", reportYear);
            DataWork.RunSqlCommand(removePriorClaims, this.caller.LoggerPhpArchive);

            //Run stored procedure to truncate flow and archive tables, then repopulate with updated data
            DataWork.TruncateWorkTable("FLW.IT0363_EdgeMedicalHeader_F", caller.LoggerPhpConfig);

            DataWork.RunSqlCommand(@"EXEC [dbo].[IT0363_EdgeMedicalClaims_SP] " + reportYear, caller.LoggerPhpConfig, 6000);

            List<String> files = new List<String>();

            //Run Medical process for each issuer
            files.Add(RunMedClaims("", EdgeEnvironment, caller));

            files.Add(RunMedClaims("", EdgeEnvironment, caller));


            if (preLoadErrorsForClaimsTeam.Count > 0)
            {
                String preloadErrorPath = caller.LoggerOutputYearDir + "EdgePreLoadErrors" + DateTime.Today.ToString("yyyyMMdd") + ".xlsx";
                ExcelWork.OutputTableToExcel<MedicalClaimErrorToReport>(preLoadErrorsForClaimsTeam, "PreLoadErrors", preloadErrorPath);
                FileTransfer.PushToSharepoint("ITReports", caller.ProcessId, preloadErrorPath, caller);
            }
        }

        private String RunMedClaims(String issuerID, String env, IT_0363ACAEdgeReporting caller)
        {
            //Set run variables
            String submissionType = "M";
            String runDateTime = DateTime.Now.ToString("MMddyy") + "T" + DateTime.Now.ToString("HHmmssff");
            String fileIdentifier = submissionType + DateTime.Now.ToString("MMddyy") + "T" + DateTime.Now.ToString("HHmm");
            String generationDateTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            String interfaceControlReleaseNumber = "02.01.07";
            caller.LoggerReportYearDir(caller.LoggerOutputYearDir);
            String XMLFilePathIssuer = caller.LoggerOutputYearDir + @"Submit\" + issuerID + "." + submissionType + "." + "D" + runDateTime + "." + env + ".xml";
            Data.AppNames EdgeDatabase = Control.GetEdgeDatabase(issuerID, env);

            //whole file counts
            int recordID = 1;
            int claimDetailTotalQuantity = 0;
            int claimServiceLineTotalQuantity = 0;
            decimal insurancePlanPaidOnFileTotalAmount = 0;


            //Build top level objects
            edgeServerMedicalClaimSubmission edgeServerMedicalClaimSubmission1 = new edgeServerMedicalClaimSubmission();
            includedMedicalClaimIssuer includedMedicalClaimIssuer1 = new includedMedicalClaimIssuer();
            edgeServerMedicalClaimSubmission1.includedMedicalClaimIssuer = includedMedicalClaimIssuer1;

            //Setup to output to XML
            XmlSerializer mySerializer = new System.Xml.Serialization.XmlSerializer(typeof(edgeServerMedicalClaimSubmission));
            TextWriter writer = new StreamWriter(XMLFilePathIssuer);

            //Populate header data
            edgeServerMedicalClaimSubmission1.fileIdentifier = fileIdentifier;
            edgeServerMedicalClaimSubmission1.executionZoneCode = env;
            edgeServerMedicalClaimSubmission1.interfaceControlReleaseNumber = interfaceControlReleaseNumber;
            edgeServerMedicalClaimSubmission1.generationDateTime = generationDateTime;
            edgeServerMedicalClaimSubmission1.submissionTypeCode = submissionType;
            includedMedicalClaimIssuer1.recordIdentifier = recordID;
            recordID += 1;
            includedMedicalClaimIssuer1.issuerIdentifier = issuerID;

            //Query database to populate objects

            //This is the initial set of plans coming out of the initial extract. Once we iterate through we will drop claims, so we need a final list of plans with claims to output to the xml file
            List<includedMedicalClaimPlan> initialplansList = ExtractFactory.ConnectAndQuery<includedMedicalClaimPlan>(caller.LoggerPhpConfig, @"SELECT DISTINCT insurancePlanIdentifier FROM [FLW].[IT0363_EdgeMedicalHeader_F] where issuerIdentifier = '" + issuerID + "'").ToList();

            List<String> previouslyAcceptedEdgeClaims = ExtractFactory.ConnectAndQuery<String>(EdgeDatabase, String.Format(@"SELECT MEDICAL_CLAIM_ID FROM {0}.MEDICAL_CLAIM ORDER BY MEDICAL_CLAIM_ID DESC", MySqlDatabaseName)).ToList();

            List<ClaimAllowedAmount> claimAllowedAmounts = ExtractFactory.ConnectAndQuery<ClaimAllowedAmount>(caller.LoggerPhpConfig, @"select sum(allowedAmount) as AllowedAmount, claimIdentifier from dbo.IT0363_EdgeClaimLines_V group by claimIdentifier").ToList();

            List<includedMedicalClaimPlan> finalPlansList = new List<includedMedicalClaimPlan>();

            //get all claims for each planID
            foreach (includedMedicalClaimPlan plan in initialplansList)
            {

                List<includedMedicalClaimDetail> claimsPulled = ExtractFactory.ConnectAndQuery<includedMedicalClaimDetail>(caller.LoggerPhpConfig, @"SELECT * FROM [FLW].[IT0363_EdgeMedicalHeader_F] where insurancePlanIdentifier = '" + plan.insurancePlanIdentifier + "'").ToList();

                plan.includedMedicalClaimDetail = GetClaimsToSubmitToEdge(previouslyAcceptedEdgeClaims, claimAllowedAmounts, claimsPulled);

                //Add 234 claims
                plan.includedMedicalClaimDetail.AddRange(GetTwoThreeFourClaims(EdgeDatabase, plan.insurancePlanIdentifier).ToList());

                if (plan.includedMedicalClaimDetail.Count > 0)
                {
                    plan.recordIdentifier = recordID++;

                    claimDetailTotalQuantity += plan.includedMedicalClaimDetail.Count();

                    plan.PopulatePlanSummaries(ref recordID, ref insurancePlanPaidOnFileTotalAmount, ref claimServiceLineTotalQuantity);

                    finalPlansList.Add(plan);
                }
                else
                {
                    continue;
                }
                

            }

            includedMedicalClaimIssuer1.includedMedicalClaimPlan = finalPlansList;

            //Total counts and money
            edgeServerMedicalClaimSubmission1.claimDetailTotalQuantity = claimDetailTotalQuantity;
            edgeServerMedicalClaimSubmission1.claimServiceLineTotalQuantity = claimServiceLineTotalQuantity;
            edgeServerMedicalClaimSubmission1.insurancePlanPaidOnFileTotalAmount = insurancePlanPaidOnFileTotalAmount.ToString("0.00");
            includedMedicalClaimIssuer1.issuerClaimDetailTotalQuantity = claimDetailTotalQuantity;
            includedMedicalClaimIssuer1.issuerClaimServiceLineTotalQuantity = claimServiceLineTotalQuantity;
            includedMedicalClaimIssuer1.issuerPlanPaidTotalAmount = insurancePlanPaidOnFileTotalAmount.ToString("0.00");

            //Write out XML
            mySerializer.Serialize(writer, edgeServerMedicalClaimSubmission1);
            writer.Close();


            return XMLFilePathIssuer;
        }


        /// <summary>
        /// This job is always trying to syncrhonize our Facets claims with the Edge Server, so there is a complex set of logic that drives when we submit a claim from Facets to 
        /// Edge, and what it looks like when we do submit it (for example, original - replacement or void)
        /// </summary>
        /// <param name="previouslyAcceptedEdgeClaims">List of claim Ids that are already submitted and inside the Edge Server</param>
        /// <param name="claimAllowedAmounts">Sum of the Allowed Amounts for the claims we are considering to submit (that way we don't have to pull these once for each claim)</param>
        /// <param name="claimsPulled">Complete set of claims we have pulled from Facets that we might submit</param>
        /// <returns>List of professional or regular (non-2,3,4) hospital claims to submit to Edge</returns>
        private List<includedMedicalClaimDetail> GetClaimsToSubmitToEdge(List<String> previouslyAcceptedEdgeClaims, List<ClaimAllowedAmount> claimAllowedAmounts, List<includedMedicalClaimDetail> claimsPulled)
        {
            List<includedMedicalClaimDetail> claimsToSubmit = new List<includedMedicalClaimDetail>();
            foreach (includedMedicalClaimDetail medClm in claimsPulled)
            {
                Decimal claimAllowedAmt = claimAllowedAmounts.Where(x => x.claimIdentifier == medClm.claimIdentifier).Select(x => x.AllowedAmount).First();

                Boolean ClaimPreviouslySubmitted = previouslyAcceptedEdgeClaims.Exists(x => x == medClm.claimIdentifier);

                medClm.billTypeCode = TranslateBillTypeCode(medClm);
                Boolean claimAdjustingClaimAlreadySubmitted = previouslyAcceptedEdgeClaims.Exists(x => x == medClm.originalClaimIdentifier);
                Boolean claimAdjustingClaimInCurrentSubmission = claimsPulled.Exists(x => x.claimIdentifier == medClm.originalClaimIdentifier); //is this a zero dollar adjustment to a claim in the current submission?

                Boolean findClaimForMissingAdjustmentInEdge = previouslyAcceptedEdgeClaims.Exists(x => x.StartsWith(medClm.claimIdentifier.Substring(0, 10)));

                //ToDo: Update non-submitted originalClaimIdentifier with the submitted one in EDGE
                if (!claimAdjustingClaimAlreadySubmitted && medClm.originalClaimIdentifier != null)
                {
                    foreach (String EdgeClmId in previouslyAcceptedEdgeClaims)
                    {
                        if (medClm.originalClaimIdentifier != EdgeClmId && medClm.claimIdentifier.Substring(0, 10) == EdgeClmId.Substring(0, 10))
                        {
                            medClm.originalClaimIdentifier = EdgeClmId;
                            continue;
                        }
                    }
                }

                if (ClaimPreviouslySubmitted)
                {
                    //caller.WriteToLog(medClm.claimIdentifier + " was not submitted b/c it was already in the Edge claims tables"); glauch: commenting out b/c the logs are too big
                    continue;
                }
                else //claim wasn't previously submitted
                {
                    if (claimAllowedAmt > 0)
                    {
                        if (medClm.ClaimType == "M" || medClm.Frequency == "1" || medClm.Frequency == "9" || medClm.Frequency == "7" || medClm.Frequency == "8")
                        {
                            if (medClm.originalClaimIdentifier == string.Empty && findClaimForMissingAdjustmentInEdge) //original claims
                            {
                                SubmitAsOriginal(ref claimsToSubmit, medClm);
                                continue;
                            }
                            else
                            {
                                if (claimAdjustingClaimAlreadySubmitted || claimAdjustingClaimInCurrentSubmission || findClaimForMissingAdjustmentInEdge)

                                {
                                    if (medClm.ClaimType == "M" || medClm.Frequency == "1" || medClm.Frequency == "9" || medClm.Frequency == "7" || findClaimForMissingAdjustmentInEdge)

                                    {
                                        medClm.voidReplaceCode = "R";
                                    }
                                    else if (medClm.Frequency == "8")
                                    {
                                        medClm.voidReplaceCode = "V";
                                    }

                                    AddClaimToSubmissionAndPopulateDetails(ref claimsToSubmit, medClm);
                                    continue;
                                }
                                else
                                {
                                        SubmitAsOriginal(ref claimsToSubmit, medClm);
                                        continue;
                                }
                            }
                        }
                        else
                        {
                            MedicalClaimErrorToReport errorClaim = new MedicalClaimErrorToReport(medClm, caller);
                            preLoadErrorsForClaimsTeam.Add(errorClaim);

                            continue;
                        }

                    }
                    else
                    {
                        if (medClm.originalClaimIdentifier == string.Empty)
                        {
                            //discard this claim, we don't need to submit originals without money on them
                            continue;
                        }
                        else
                        {
                            if (claimAdjustingClaimAlreadySubmitted || claimAdjustingClaimInCurrentSubmission)
                            {
                                medClm.voidReplaceCode = "V";
                                AddClaimToSubmissionAndPopulateDetails(ref claimsToSubmit, medClm);
                                continue;
                            }
                            
                        }
                    }


                }
            }
            return claimsToSubmit;
        }

        /// <summary>
        /// Once we've identified that a claim needs to be submitted to Edge, this method will populate additional necessary information and add it to our list to be submitted
        /// </summary>
        /// <param name="claimsToSubmit">Array capturing the batch of claims to be submitted</param>
        /// <param name="medClm">Individual claim to complete loading and add to the submission batch</param>
        private void AddClaimToSubmissionAndPopulateDetails(ref List<includedMedicalClaimDetail> claimsToSubmit, includedMedicalClaimDetail medClm)
        {
            //Get diagnosis codes
            medClm.diagnosisCode = ExtractFactory.ConnectAndQuery<String>(caller.LoggerExampleDb, @"SELECT DISTINCT FROM  medClm.claimIdentifier").ToList();
            medClm.includedDetailServiceLine = ExtractFactory.ConnectAndQuery<includedServiceLine>(caller.LoggerPhpConfig, @"SELECT * FROM [dbo].[IT0363_EdgeClaimLines_V] where claimIdentifier = '" + medClm.claimIdentifier + "' order by serviceLineNumber").ToList();

            decimal policyPaidTotalAmount = 0;
            decimal allowedTotalAmount = 0;

            foreach (includedServiceLine claimLine in medClm.includedDetailServiceLine)
            {
                //Load service modifier codes if any exist
                claimLine.serviceModifierCode = CheckModifier(claimLine);
                policyPaidTotalAmount += Convert.ToDecimal(claimLine.policyPaidAmount); //claim level total
                allowedTotalAmount += Convert.ToDecimal(claimLine.allowedAmount); //claim level total
            }

            medClm.allowedTotalAmount = allowedTotalAmount;
            medClm.policyPaidTotalAmount = policyPaidTotalAmount;
            
            claimsToSubmit.Add(medClm);
        }

        /// <summary>
        /// Adds the claim to the submission as long as it isn't a CLHP_FREQ 7 or 8, which is an explicit adjustment hospital claim. We can't submit these as originals.
        /// </summary>
        /// <param name="claimsToSubmit">Reference to the batch we're building for the claims submission</param>
        /// <param name="medClm">Individual claim being interrogated</param>
        private void SubmitAsOriginal(ref List<includedMedicalClaimDetail> claimsToSubmit, includedMedicalClaimDetail medClm)
        {
            if ((medClm.Frequency == "7" || medClm.Frequency == "8") && medClm.originalClaimIdentifier == String.Empty)
            {
                MedicalClaimErrorToReport badClaim = new MedicalClaimErrorToReport(medClm, caller);
                preLoadErrorsForClaimsTeam.Add(badClaim);
            }
            else
            {
                if (medClm.Frequency == "7")
                {
                    medClm.billTypeCode = medClm.billTypeCode.Substring(0, 2) + "1";
                }
                medClm.voidReplaceCode = "";
                medClm.originalClaimIdentifier = String.Empty;
                AddClaimToSubmissionAndPopulateDetails(ref claimsToSubmit, medClm);
            }
        }

        private string TranslateBillTypeCode(includedMedicalClaimDetail medClm)
        {
            if (medClm.ClaimType == "M")
            {
                return "";
            }
            else
            {
                if (medClm.Frequency == "9")
                {
                    return medClm.FacilityType.Substring(1,1) + medClm.BillClass + "1";
                }
                else
                {
                    return medClm.FacilityType.Substring(1, 1) + medClm.BillClass + medClm.Frequency;
                }
            }
        }



        private static List<String> CheckModifier(includedServiceLine line)
        {

            List<string> modifiers = new List<string>();

            if (line.serviceModifierCode1 != null && line.serviceModifierCode1.Trim() != "")
            {
                modifiers.Add(line.serviceModifierCode1);
            }
            if (line.serviceModifierCode2 != null && line.serviceModifierCode2.Trim() != "")
            {
                modifiers.Add(line.serviceModifierCode2);
            }
            if (line.serviceModifierCode3 != null && line.serviceModifierCode3.Trim() != "")
            {
                modifiers.Add(line.serviceModifierCode3);
            }
            if (line.serviceModifierCode4 != null && line.serviceModifierCode4.Trim() != "")
            {
                modifiers.Add(line.serviceModifierCode4);
            }

            if (modifiers.Count() == 0 || modifiers[0] == null || modifiers[0] == "")
            {
                modifiers.Add("");
            }

            return modifiers;
        }

        /// <summary>
        /// CLHP_FREQ 2,3,4 claims are a special type of hospital claim that PHP considers distinct claims, but Edge considers parts of a single claim. Therefore, we need to 
        /// group these claim lines, assign a new claim ID and submit them as a single claim
        /// </summary>
        /// <param name="EdgeDataSource"></param>
        /// <param name="planHIOS"></param>
        /// <param name="previouslySubmittedClaimIds"></param>
        /// <returns></returns>
        private List<includedMedicalClaimDetail> GetTwoThreeFourClaims(Data.AppNames EdgeDataSource, String planHIOS)
        {
            String TwoThreeFourClaimsQuery = String.Format(@"EXEC IT0363_Edge234ClaimHeader_SP {0}, '{1}'", reportYear, planHIOS);

            List<IT0363_Edge234ClaimHeader_V> twothreefourClmsExt = ExtractFactory.ConnectAndQuery<IT0363_Edge234ClaimHeader_V>(caller.LoggerPhpConfig, TwoThreeFourClaimsQuery).ToList();

            List<IT0363_Edge234ClaimHeader_V> consolidated = twothreefourClmsExt.GroupBy(x => x.claimIdentifier).Select(g => g.First()).ToList(); //get the first instance of a given contrived Claim Id for us to iterate over

            List<includedMedicalClaimDetail> xmlClaims = new List<includedMedicalClaimDetail>();

            //Populate the table with CLCL ids that are included in 234 grouping
            DataTable ttfDtCLCL = new DataTable();

            ttfDtCLCL.Columns.Add("CONTRIVED_ID", typeof(string));
            ttfDtCLCL.Columns.Add("CLCL_ID", typeof(string));
            ttfDtCLCL.Columns.Add("CLCL_ALLOW", typeof(decimal));
            ttfDtCLCL.Columns.Add("REPORT_YEAR", typeof(string));
            ttfDtCLCL.Columns.Add("SUBMIT_DATE",typeof(DateTime));            

            foreach (IT0363_Edge234ClaimHeader_V consClm in consolidated)
            {
                String newClaimId;
                String oldClaimId;
                Boolean noChanges;

                List<IT0363_Edge234ClaimHeader_V> groupedClaims = twothreefourClmsExt.Where(x => x.claimIdentifier == consClm.claimIdentifier).ToList(); //grab all claims that fall under this contrived claim ID

                DateTime MaxClaimProcessedDateTime = Convert.ToDateTime(groupedClaims.Max(x => x.claimProcessedDateTime));
                Boolean alreadySubmitted = HasMemberProviderAdmDtAlreadyBeenSubmitted(consClm.claimIdentifier, out newClaimId, out oldClaimId, EdgeDataSource, out noChanges, MaxClaimProcessedDateTime);

                List<String> originalCLCL_IDs = groupedClaims.Select(x => x.CLCL_ID).ToList();
                List<includedServiceLine> contrivedClaimIdLines = GetLinesForAllConsolidatedClaims(originalCLCL_IDs);

                if (noChanges) //this 234 claim hasn't received any new CLCL_IDs since our last load
                {
                    foreach (string clmID in originalCLCL_IDs)
                    {
                        List<includedServiceLine> disobj = contrivedClaimIdLines.Where(x => x.ClaimID == "" + clmID + "").ToList();
                        decimal allAmt = disobj.Sum(x => x.allowedAmount);
                        ttfDtCLCL.Rows.Add(newClaimId, clmID, allAmt, reportYear, System.DateTime.Now);
                    }
                    continue;
                }
                
                includedMedicalClaimDetail newClm = new includedMedicalClaimDetail();               

                decimal AllowedAmount = contrivedClaimIdLines.Sum(x => x.allowedAmount);

                if (AllowedAmount == 0)
                {
                    if (!alreadySubmitted)
                    {
                        //this is a zero dollar claim and it hasn't been submitted previosuly so we have nothing more to do.
                        continue;
                    }
                    else
                    {
                        String currentStatus = GetCurrentClaimStatusInEdge(EdgeDataSource, oldClaimId);

                        if (currentStatus == "O")
                        {
                            newClm.voidReplaceCode = "V";
                            newClm.originalClaimIdentifier = oldClaimId;
                        }
                        else
                        {
                            //this is a voided claim that has already been submitted as avoid to Edge.
                            continue;
                        }
                    }
                }
                else //claims with money on them
                {
                    if (!alreadySubmitted) //this contrived claim isn't in Edge at all, it's a brand new original
                    {
                        newClm.voidReplaceCode = "";
                        newClm.originalClaimIdentifier = "";
                    }
                    else
                    {
                        String currentStatus = GetCurrentClaimStatusInEdge(EdgeDataSource, oldClaimId);

                        if (currentStatus == "V") //this claim was previously voided (implying it didn't have money before), but now it does - so let's submit as an original
                        {
                            newClm.voidReplaceCode = "";
                            newClm.claimIdentifier = newClaimId;
                            newClm.originalClaimIdentifier = oldClaimId;
                            
                        }
                        else //the implication of this block is intentional. Even if a claim hasn't changed, we're replacing it at each run
                        {
                            newClm.voidReplaceCode = "R";
                            newClm.claimIdentifier = newClaimId;
                            newClm.originalClaimIdentifier = oldClaimId;
                        }
                    }
                }

                newClm.includedDetailServiceLine = contrivedClaimIdLines;
                newClm.medicalNetworkIndicator = contrivedClaimIdLines[0].medicalNetworkIndicator;
                newClm.insuredMemberIdentifier = consClm.insuredMemberIdentifier;
                newClm.formTypeCode = consClm.formTypeCode;
                newClm.claimIdentifier = newClaimId;
                newClm.claimProcessedDateTime = groupedClaims.Max(x => x.claimProcessedDateTime);
                newClm.billTypeCode = consClm.billTypeCode;
                newClm.diagnosisTypeCode = getDxTypeCode(groupedClaims);
                newClm.diagnosisCode = GetDiagsForAllConsolidatedClaims(originalCLCL_IDs, groupedClaims, caller);
                newClm.dischargeStatusCode = GetDischargeStatusCode(newClm, groupedClaims);
                newClm.statementCoverFromDate = groupedClaims.Min(x => x.statementCoverFromDate);
                newClm.statementCoverToDate = groupedClaims.Max(x => x.statementCoverToDate);
                newClm.billingProviderIDQualifier = consClm.billingProviderIDQualifier;
                newClm.billingProviderIdentifier = consClm.billingProviderIdentifier;
                newClm.issuerClaimPaidDate = groupedClaims.Max(x => x.issuerClaimPaidDate);
                newClm.allowedTotalAmount = groupedClaims.Sum(x => x.allowedTotalAmount);
                newClm.policyPaidTotalAmount = groupedClaims.Sum(x => x.policyPaidTotalAmount);
                newClm.derivedServiceClaimIndicator = consClm.derivedServiceClaimIndicator;
                newClm.insurancePlanIdentifier = consClm.insurancePlanIdentifier;
                newClm.issuerIdentifier = consClm.issuerIdentifier;

                xmlClaims.Add(newClm);
                if (newClm.voidReplaceCode != "V")
                {
                    foreach (string clmID in originalCLCL_IDs)
                    {
                        List<includedServiceLine> disobj = contrivedClaimIdLines.Where(x => x.ClaimID == "" + clmID + "").ToList();
                        decimal allAmt = disobj.Sum(x => x.allowedAmount);
                        ttfDtCLCL.Rows.Add(newClm.claimIdentifier, clmID, allAmt,reportYear, System.DateTime.Now);
                    }
                }
            }
            //populate the ttfDtCLCL datatable
            if(ttfDtCLCL.Rows.Count>0)
                DataWork.LoadTable(this.caller.LoggerPhpArchive, "IT0363_Edge234ClmExt_A", ttfDtCLCL, this.caller, true);

            return xmlClaims;

        }

        private string GetCurrentClaimStatusInEdge(Data.AppNames EdgeDataSource, String oldClaimId)
        {
            String currentStatus = ExtractFactory.ConnectAndQuery<String>(EdgeDataSource, String.Format(@"select TRANSACTION_TYPE from {1}.MEDICAL_CLAIM where MEDICAL_CLAIM_ID = '{0}'", oldClaimId, MySqlDatabaseName)).First();
            return currentStatus;
        }

        /// <summary>
        /// We need to get the discharge status from header record with the latest line's serviceFromDate
        /// </summary>
        /// <param name="newClm">Consolidated claim (in progress of begin built)</param>
        /// <param name="groupedClaims">Original CLCL_ID claims being consolidated</param>
        /// <returns>The discharge status code for the consolidated claim</returns>
        private static string GetDischargeStatusCode(includedMedicalClaimDetail newClm, List<IT0363_Edge234ClaimHeader_V> groupedClaims)
        {
            String highestServiceFromDt = newClm.includedDetailServiceLine.Max(x => x.serviceFromDate);
            String claimWithLatestServiceFromDate = newClm.includedDetailServiceLine.First(x => x.serviceFromDate == highestServiceFromDt).ClaimID;

            return groupedClaims.First(x => x.CLCL_ID == claimWithLatestServiceFromDate).dischargeStatusCode;

        }

        /// <summary>
        /// If claims being consolidated contain different diagnosis type codes, we're going to convert them to ICD10 ('02' in Edge speak)
        /// </summary>
        /// <param name="groupedClaims">Claims we're going to group</param>
        /// <returns>Diagnosis Type Code to use on the consolidated claim</returns>
        private String getDxTypeCode(List<IT0363_Edge234ClaimHeader_V> groupedClaims)
        {
            if (groupedClaims.Exists(x => x.diagnosisTypeCode == "01") && groupedClaims.Exists(x => x.diagnosisTypeCode == "02"))
            {
                return "02";
            }
            else
            {
                return groupedClaims[0].diagnosisTypeCode;
            }
        }

        /// <summary>
        /// Checks to see if the MEME_CK/CLCL.PRPR_ID/CLHP_ADM_DT combination already exists in our submission history
        /// </summary>
        /// <param name="newClaimIdentifier">MEME_CK/CLCL.PRPR_ID/CLHP_ADM_DT from the extraction</param>
        /// <param name="maxClaimId">This will have the incremented new claimIdentifier to use, or the original if no history is found</param>
        /// <param name="lastClaimId">If this claimIdentifier was already submitted, this is the one we'll need to point to for replacing</param>
        /// <param name="NoChanges">Will be true if the claim already in Edge has the same CLAIM_PROCESSED_DATETIME as what we just pulled, implying that no new CLCL_IDs have been found under the 234 grouping keys</param>
        /// <param name="issuerDb">Edge Issuer Identifier (these go to different databases)</param>
        /// <param name="FacetsMaxClaimProcessedDate">Maximum claim processed date found on the grouped claims in Facets. We'll use this to test to see if a later CLCL_ID was received since our last submission of this claim grouping</param>
        /// <returns>Whether this is an original 234 claim, or a replacement</returns>
        private Boolean HasMemberProviderAdmDtAlreadyBeenSubmitted(String newClaimIdentifier, out String maxClaimId, out String lastClaimId, Data.AppNames issuerDb, out Boolean NoChanges, DateTime FacetsMaxClaimProcessedDate)
        {
            TwoThreeFourHistClaim prevSubmissions = ExtractFactory.ConnectAndQuery<TwoThreeFourHistClaim>(issuerDb, String.Format("select MEDICAL_CLAIM_ID, CLAIM_PROCESSED_DATETIME from {0}.MEDICAL_CLAIM where MEDICAL_CLAIM_ID = (SELECT max(MEDICAL_CLAIM_ID) FROM {0}.MEDICAL_CLAIM where length(rtrim(MEDICAL_CLAIM_ID)) = 45 and left(rtrim(MEDICAL_CLAIM_ID),43) = '{1}')",MySqlDatabaseName,newClaimIdentifier.Substring(0, 43))).FirstOrDefault();

            if (prevSubmissions == null)
            {
                maxClaimId = newClaimIdentifier;
                lastClaimId = newClaimIdentifier;
                NoChanges = false;
                return false;
            } else if(prevSubmissions.CLAIM_PROCESSED_DATETIME >= FacetsMaxClaimProcessedDate)
            {
                NoChanges = true;
                maxClaimId = newClaimIdentifier;
                lastClaimId = newClaimIdentifier;
                return false;
            }
            else
            {
                maxClaimId = prevSubmissions.MEDICAL_CLAIM_ID.Substring(0, 43) + (Convert.ToInt32(prevSubmissions.MEDICAL_CLAIM_ID.Substring(43, 2)) + 1).ToString("00").Trim();
                lastClaimId = prevSubmissions.MEDICAL_CLAIM_ID;
                NoChanges = false;
                return true;
            }

        }

        private class TwoThreeFourHistClaim
        {
            public string MEDICAL_CLAIM_ID { get; set; }
            public DateTime CLAIM_PROCESSED_DATETIME { get; set; }
        }


        private List<String> GetDiagsForAllConsolidatedClaims(List<string> claimSet, List<IT0363_Edge234ClaimHeader_V> groupedClaims, IT_0363ACAEdgeReporting caller)
        {
            List<string> emptyTags = new List<string>();
            emptyTags.Add("");

            String claimList = String.Join("','", claimSet);
            if (groupedClaims.Exists(x => x.diagnosisTypeCode == "01") && groupedClaims.Exists(x => x.diagnosisTypeCode == "02")) //if this set of claims consists of both ICD9 and ICD10 codes we need to convert to 10
            {
                List<string> diags = ExtractFactory.ConnectAndQuery<String>(caller.LoggerExampleDb, String.Format(@"s", claimList)).ToList();

                if(diags.Count == 0)
                {
                    return emptyTags;
                }
                else
                {
                    return diags;
                }
                
            }
            else
            {
                List<string> diags = ExtractFactory.ConnectAndQuery<String>(caller.LoggerExampleDb, "select distinct  from   where  in ('" + claimList + "') and exists (select * from  where . =  and  > 0)").ToList(); 

                if(diags.Count == 0)
                {
                    return emptyTags;
                }
                else
                {
                    return diags;
                }
            }

        }


        private List<includedServiceLine> GetLinesForAllConsolidatedClaims(List<string> claimSet)
        {
            String clcls = String.Join("','", claimSet);
            //Heads up - here we are pulling lines by claimIdentifier here, which in facets terms here means CLCL_IDs. claimIdentifier in the rest of the 234 process
            //refers to MEME_CK, PRPR_ID and CLHP_ADM_DT (which is a variance specific to 2,3,4 claims). Here since we're using the same view as the regular claims process we'll
            //pull from the 'normal' claimIdentifier column.
            List<LineExtractClass> flattenedLine = ExtractFactory.ConnectAndQuery<LineExtractClass>(caller.LoggerPhpConfig, String.Format(@"SELECT * from IT0363_EdgeClaimLines_V WHERE claimIdentifier in ('{0}') order by claimIdentifier, serviceLineNumber", clcls)).ToList();

            List<includedServiceLine> linesForXml = new List<includedServiceLine>();

            int lineCounter = 1;
            foreach (LineExtractClass line in flattenedLine)
            {
                includedServiceLine xmlLine = new includedServiceLine();
                xmlLine.serviceLineNumber = Convert.ToInt16(lineCounter++);
                xmlLine.medicalNetworkIndicator = line.medicalNetworkIndicator;
                xmlLine.serviceFromDate = line.serviceFromDate;
                xmlLine.serviceToDate = line.serviceToDate;
                xmlLine.revenueCode = line.revenueCode;
                xmlLine.serviceTypeCode = line.serviceTypeCode;
                xmlLine.serviceCode = line.serviceCode;
                xmlLine.serviceModifierCode = getServiceModsInList(line);
                xmlLine.serviceFacilityTypeCode = line.serviceFacilityTypeCode;
                xmlLine.renderingProviderIDQualifier = line.renderingProviderIDQualifier;
                xmlLine.renderingProviderIdentifier = line.renderingProviderIdentifier;
                xmlLine.allowedAmount = line.allowedAmount;
                xmlLine.policyPaidAmount = line.policyPaidAmount;
                xmlLine.derivedServiceClaimIndicator = line.derivedServiceClaimIndicator;
                xmlLine.ClaimID = line.claimIdentifier;

                linesForXml.Add(xmlLine);
            }

            return linesForXml;
        }

        private List<String> getServiceModsInList(LineExtractClass line)
        {
            List<string> servModCodes = new List<string>();

            if (line.serviceModifierCode1 != null)
            {
                servModCodes.Add(line.serviceModifierCode1);
            }

            if (line.serviceModifierCode2 != null)
            {
                servModCodes.Add(line.serviceModifierCode2);
            }

            if (line.serviceModifierCode3 != null)
            {
                servModCodes.Add(line.serviceModifierCode3);
            }

            if (line.serviceModifierCode4 != null)
            {
                servModCodes.Add(line.serviceModifierCode4);
            }

            if (servModCodes.Count == 0)
            {
                servModCodes.Add("");
            }

            return servModCodes;
        }


        private class LineExtractClass
        {
            public String claimIdentifier { get; set; }
            public Int16 serviceLineNumber { get; set; }
            public String medicalNetworkIndicator { get; set; }
            public string serviceFromDate { get; set; }
            public string serviceToDate { get; set; }
            public string revenueCode { get; set; }
            public string serviceTypeCode { get; set; }
            public string serviceCode { get; set; }
            public string serviceModifierCode1 { get; set; }
            public string serviceModifierCode2 { get; set; }
            public string serviceModifierCode3 { get; set; }
            public string serviceModifierCode4 { get; set; }
            public string serviceFacilityTypeCode { get; set; }
            public string renderingProviderIDQualifier { get; set; }
            public string renderingProviderIdentifier { get; set; }
            public decimal allowedAmount { get; set; }
            public decimal policyPaidAmount { get; set; }
            public string derivedServiceClaimIndicator { get; set; }
        }

        private class ClaimAllowedAmount
        {
            public decimal AllowedAmount { get; set; }
            public string claimIdentifier { get; set; }
        }

        private class MedicalClaimErrorToReport
        {
            public string ClaimId { get; set; }
            public String BillTypeCode { get; set; }
            public String ErrorDescription { get; set; }

            public MedicalClaimErrorToReport(includedMedicalClaimDetail claimToReport, Logger procLog)
            {
                ClaimId = claimToReport.claimIdentifier;
                BillTypeCode = claimToReport.billTypeCode;
                if (claimToReport.Frequency == "7" || claimToReport.Frequency == "8")
                {
                    ErrorDescription = "Void/Replace Bill Type on Original Claim";
                }
                else
                {
                    ErrorDescription = "Invalid Bill Type";
                }
                
            }

        }
        private class IT0363_Edge234ClaimHeader_V
        {
            public int insuredMemberIdentifier { get; set; }
            public string formTypeCode { get; set; }
            public string claimIdentifier { get; set; }
            public string CLCL_ID { get; set; }
            public string originalClaimIdentifier { get; set; }
            public string claimProcessedDateTime { get; set; }
            public string billTypeCode { get; set; }
            public string voidReplaceCode { get; set; }
            public string diagnosisTypeCode { get; set; }
            public string dischargeStatusCode { get; set; }
            public string statementCoverFromDate { get; set; }
            public string statementCoverToDate { get; set; }
            public string billingProviderIDQualifier { get; set; }
            public string billingProviderIdentifier { get; set; }
            public string issuerClaimPaidDate { get; set; }
            public Nullable<decimal> allowedTotalAmount { get; set; }
            public Nullable<decimal> policyPaidTotalAmount { get; set; }
            public string derivedServiceClaimIndicator { get; set; }
            public string issuerIdentifier { get; set; }
            public string insurancePlanIdentifier { get; set; }
            public string GRGR_ID { get; set; }
            public string CLHP_FREQUENCY { get; set; }
        }
    }


}
