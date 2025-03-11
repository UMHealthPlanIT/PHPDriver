using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.IO;
using Utilities;

namespace Driver.IT_0363a
{
    class PharmacyClaimExtract
    {
        List<includedPharmacyClaimInsurancePlan> finalPlansToSubmit;
        //entire file counts
        int recordID;
        int claimDetailTotalCount; //Total number of claims
        decimal issuerPaidTotal; //Total money paid by issuer
        //By plan counts
        decimal planPaidTotal; //Total money paid by plan
        Data.AppNames EdgeDataSource;
        String MySqlDatabaseName;
        IT_0363ACAEdgeReporting caller;

        public PharmacyClaimExtract(IT_0363ACAEdgeReporting incomingCaller, string EDGEenvironment, String reportYear)
        {

            List<String> files = new List<String>();
            this.caller = incomingCaller;

            //Fill the NDC comparison table
            DataWork.TruncateWorkTable("IT0363_NDCMaster_F", incomingCaller.LoggerPhpConfig);
            DataWork.LoadTableFromQuery(Data.AppNames.ExampleProd, @"SELECT NDC_CODE, NDC_CD_EFCTV_STRT_DT, NDC_CD_EFCTV_END_DT FROM EDGE_SRVR_COMMON.NDC_CD_TYPE", incomingCaller.LoggerPhpConfig, "DBO.IT0363_NDCMaster_F", incomingCaller);

            //Run stored procedure to populate data tables
            ExtractFactory.ConnectAndQuery<includedInsuredMember>(caller.LoggerPhpConfig, @"EXEC [dbo].[IT0363_EdgePharmacyClaims_SP] '" + reportYear + "'");
            ExtractFactory.ConnectAndQuery<includedInsuredMember>(caller.LoggerPhpConfig, @"EXEC [dbo].[IT0363_EdgePharmacyRetroTermClaims_SP] '" + reportYear + "'");

            //Run Pharmacy process for each of the two issuers
            files.Add(RunPharmClaims("20662", EDGEenvironment));
            files.Add(RunPharmClaims("60829", EDGEenvironment));

        }

        private String RunPharmClaims(String issuerID, String env)
        {
            recordID = 1;
            claimDetailTotalCount = 0;
            issuerPaidTotal = 0;
            planPaidTotal = 0;
            EdgeDataSource = Control.GetEdgeDatabase(issuerID, env);
            finalPlansToSubmit = new List<includedPharmacyClaimInsurancePlan>();
            //Set run variables
            String submissionType = "P";
            String runDateTime = DateTime.Now.ToString("MMddyy") + "T" + DateTime.Now.ToString("HHmmssff");
            String fileIdentifier = submissionType + DateTime.Now.ToString("MMddyy") + "T" + DateTime.Now.ToString("HHmm");
            String generationDateTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            String interfaceControlReleaseNumber = "02.01.07";
            caller.LoggerReportYearDir(caller.LoggerOutputYearDir);
            String XMLFilePathIssuer = caller.LoggerOutputYearDir + @"Submit\" + issuerID + "." + submissionType + "." + "D" + runDateTime + "." + env + ".xml";


            //Build top level objects
            edgeServerPharmacyClaimSubmission EDGEServerPharmacySubmission1 = new edgeServerPharmacyClaimSubmission();
            includedPharmacyClaimIssuer includedPharmacyClaimIssuer1 = new includedPharmacyClaimIssuer();
            EDGEServerPharmacySubmission1.includedPharmacyClaimIssuer = includedPharmacyClaimIssuer1;

            //Setup to output to XML
            XmlSerializer mySerializer = new System.Xml.Serialization.XmlSerializer(typeof(edgeServerPharmacyClaimSubmission));
            TextWriter writer = new StreamWriter(XMLFilePathIssuer);

            //Populate header data
            EDGEServerPharmacySubmission1.fileIdentifier = fileIdentifier;
            EDGEServerPharmacySubmission1.executionZoneCode = env;
            EDGEServerPharmacySubmission1.interfaceControlReleaseNumber = interfaceControlReleaseNumber;
            EDGEServerPharmacySubmission1.generationDateTime = generationDateTime;
            EDGEServerPharmacySubmission1.submissionTypeCode = submissionType;
            includedPharmacyClaimIssuer1.recordIdentifier = recordID++;
            includedPharmacyClaimIssuer1.issuerIdentifier = issuerID;

            //Query for all unique insurancePlanIdentifier based on issuerID/issuerIdentifier
            List<includedPharmacyClaimInsurancePlan> initialplansList = ExtractFactory.ConnectAndQuery<includedPharmacyClaimInsurancePlan>(caller.LoggerPhpConfig, @"SELECT DISTINCT insurancePlanIdentifier FROM [FLW].[IT0363_EdgePharmacyClaims_F] where issuerIdentifier = '" + issuerID + "'").ToList();

            if (caller.TestMode)
            {
                MySqlDatabaseName = "EDGE_SRVR_TEST";
            }
            else
            {
                MySqlDatabaseName = "EDGE_SRVR_PROD";
        }

        List<String> previouslyAcceptedEdgeClaims = ExtractFactory.ConnectAndQuery<String>(EdgeDataSource, String.Format(@"SELECT CLAIM_IDENTIFIER FROM {0}.PHARMACY_CLAIM where VOID_REPLACE_CODE is null", MySqlDatabaseName)).ToList();

            List<String> previouslyVoidedEdgeClaims = ExtractFactory.ConnectAndQuery<String>(EdgeDataSource, String.Format(@"SELECT CLAIM_IDENTIFIER FROM {0}.PHARMACY_CLAIM where VOID_REPLACE_CODE = 'V'", MySqlDatabaseName)).ToList();


            //For each insurancePlanIdentifier, populate includedPharmacyClaimInsurancePlan properties
            foreach (includedPharmacyClaimInsurancePlan plan in initialplansList)
            {
                List<includedPharmacyClaimDetail> activeEdgeClaims = GetCurrenEdgeActiveClaims(EdgeDataSource, MySqlDatabaseName, plan.insurancePlanIdentifier);

                plan.includedPharmacyClaimDetail = GetPharmClaimsToSubmit(caller, previouslyAcceptedEdgeClaims, plan, previouslyVoidedEdgeClaims, activeEdgeClaims);

                LoadHeaderData(finalPlansToSubmit, plan);
            }


            AddPlansWithVoidedClaimsThatWereNotInFacets(issuerID, finalPlansToSubmit); 

            includedPharmacyClaimIssuer1.includedPharmacyClaimInsurancePlan = finalPlansToSubmit;

            //Total counts and money
            EDGEServerPharmacySubmission1.claimDetailTotalQuantity = claimDetailTotalCount;
            includedPharmacyClaimIssuer1.issuerClaimDetailTotalQuantity = claimDetailTotalCount;
            EDGEServerPharmacySubmission1.insurancePlanPaidOnFileTotalAmount = issuerPaidTotal;
            includedPharmacyClaimIssuer1.issuerPlanPaidTotalAmount = issuerPaidTotal;

            //Write out XML
            mySerializer.Serialize(writer, EDGEServerPharmacySubmission1);
            writer.Close();

             return XMLFilePathIssuer;
        }

        private void AddPlansWithVoidedClaimsThatWereNotInFacets(string issuerID, List<includedPharmacyClaimInsurancePlan> finalPlansToSubmit)
        {
            List<includedPharmacyClaimInsurancePlan> plansWithVoidedClaims = new List<includedPharmacyClaimInsurancePlan>();
            
            //Get the list of active HIOS ID's from EDGE
            List<String> retroEdgeHiosIds = ExtractFactory.ConnectAndQuery<String>(EdgeDataSource, String.Format(@"SELECT DISTINCT INSURANCE_PLAN_IDENTIFIER FROM {0}.PHARMACY_CLAIM where VOID_REPLACE_CODE is null AND ISSUER_IDENTIFIER = '{1}'", MySqlDatabaseName, issuerID)).ToList();


            List<String> retroEdgeHiosIdsNotInFacets = new List<string>();

            foreach (String plan in retroEdgeHiosIds)
            {
                if (!finalPlansToSubmit.Exists(x => x.insurancePlanIdentifier == plan)) //check to see if this claim is flagged as a retro term from above query

                {
                    retroEdgeHiosIdsNotInFacets.Add(plan);
                }

            }

            //Pulling the HIOS IDs that are missing from Facets 
            foreach (string HIOSID in retroEdgeHiosIdsNotInFacets)
            {

                includedPharmacyClaimInsurancePlan plan = new includedPharmacyClaimInsurancePlan();

                plan.insurancePlanIdentifier = HIOSID;

                //Check to see if any of those HIOS ids have claims in IT0363_EdgePharmacyRetroTermClaims_SP
                List<includedPharmacyClaimDetail> retroEdgeClaims = GetCurrenEdgeActiveClaims(EdgeDataSource, MySqlDatabaseName, plan.insurancePlanIdentifier);

                //Picking up any retro termed claims based on the missing HIOS IDs from Facets
                plan.includedPharmacyClaimDetail = AddClaimsForRetroTermedMembers(caller, retroEdgeClaims, plan); //todo: add the list of claims returned from AddClaimsForRetroTermedMembers to List<includedPharmacyClaimInsurancePlan
                if (plan.includedPharmacyClaimDetail.Count > 0)
                {
                    LoadHeaderData(finalPlansToSubmit, plan);
                    plansWithVoidedClaims.Add(plan);
                }
            }

        }

        private void LoadHeaderData(List<includedPharmacyClaimInsurancePlan> finalPlansToSubmit, includedPharmacyClaimInsurancePlan plan)
        {
            if (plan.includedPharmacyClaimDetail.Count > 0)
            {
                //Plan totals
                planPaidTotal = 0;

                plan.insurancePlanClaimDetailTotalQuantity = plan.includedPharmacyClaimDetail.Count;
                //Issuers totals
                claimDetailTotalCount += plan.includedPharmacyClaimDetail.Count;

                plan.recordIdentifier = recordID++;

                foreach (includedPharmacyClaimDetail pharmClaim in plan.includedPharmacyClaimDetail)
                {
                    pharmClaim.recordIdentifier = recordID++;
                    planPaidTotal += Convert.ToDecimal(pharmClaim.policyPaidAmount);
                }

                plan.policyPaidTotalAmount = planPaidTotal;
                issuerPaidTotal += planPaidTotal;

                finalPlansToSubmit.Add(plan);
            }
        }

        private static List<includedPharmacyClaimDetail> GetCurrenEdgeActiveClaims(Data.AppNames EdgeDataSource, string MySqlDatabaseName, String planId)
        {

            return ExtractFactory.ConnectAndQuery<includedPharmacyClaimDetail>(EdgeDataSource, String.Format(@"", MySqlDatabaseName, planId)).ToList();
        }

        private static List<includedPharmacyClaimDetail> GetPharmClaimsToSubmit(IT_0363ACAEdgeReporting caller, List<String> previouslyAcceptedEdgeClaims
            , includedPharmacyClaimInsurancePlan plan, List<String> previouslyVoidedEdgeClaims, List<includedPharmacyClaimDetail> ActiveEdgeClaims)
        {

            List<includedPharmacyClaimDetail> initialClaimsPull = ExtractFactory.ConnectAndQuery<includedPharmacyClaimDetail>(caller.LoggerPhpConfig, @"SELECT * FROM [FLW].[IT0363_EdgePharmacyClaims_F] where insurancePlanIdentifier = '" + plan.insurancePlanIdentifier + "' order by insuredMemberIdentifier, prescriptionServiceReferenceNumber, issuerClaimPaidDate, claimProcessedDateTime").ToList();


            List<includedPharmacyClaimDetail> finalClaims = new List<includedPharmacyClaimDetail>();


            foreach (includedPharmacyClaimDetail claimDetail in initialClaimsPull)
            {

                //Submit this claim if the claim isn't in Edge, or it is in Edge but we are trying to void it now
                //if ((!ExchangeClaimsDetail.Exists(x => x.claimIdentifier == claimDetail.claimIdentifier) && claimDetail.voidReplaceCode != "V"))
                if ((!previouslyAcceptedEdgeClaims.Exists(x => x == claimDetail.claimIdentifier) && claimDetail.voidReplaceCode != "V"))
                {
                    finalClaims.Add(claimDetail);

                }
                //void a claim that wasn't previously voided
                else if (!previouslyVoidedEdgeClaims.Exists(x => x == claimDetail.claimIdentifier) && claimDetail.voidReplaceCode == "V")
                {
                    finalClaims.Add(claimDetail);
                }
                else
                {
                    //caller.WriteToLog(claimDetail.claimIdentifier + " was not submitted b/c it was already in the Edge claims tables"); glauch: commenting out b/c the logs are too big
                }

            }

            finalClaims.AddRange(AddClaimsForRetroTermedMembers(caller, ActiveEdgeClaims, plan));

            return finalClaims;
        }

        private static List<includedPharmacyClaimDetail> AddClaimsForRetroTermedMembers(IT_0363ACAEdgeReporting caller, List<includedPharmacyClaimDetail> EdgeClaims, includedPharmacyClaimInsurancePlan plan)
        {
            List<includedPharmacyClaimDetail> retroTermedClaims = new List<includedPharmacyClaimDetail>();

            List<string> retroClaimsPull = ExtractFactory.ConnectAndQuery<string>(caller.LoggerPhpConfig, @"SELECT DISTINCT claimIdentifier FROM [FLW].[IT0363_EdgeRetroTermedPharmacyClaims_F]").ToList();

            //Add EDGE claims not in Facets as VBO
            foreach (includedPharmacyClaimDetail claimDetail2 in EdgeClaims)
            {
                if (retroClaimsPull.Exists(x => x == claimDetail2.claimIdentifier)) //check to see if this claim is flagged as a retro term from above query

                {
                    claimDetail2.claimProcessedDateTime = claimDetail2.claimProcessedDateTime.Substring(0, 11) + "01:00:00"; //the void has to have a later date time than the original, so just adding to the original
                    claimDetail2.voidReplaceCode = "V";
                    retroTermedClaims.Add(claimDetail2);
                }
            }

            return retroTermedClaims;
        }
    }

}



