using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.IO;
using System.Data.Linq;
using System.Diagnostics;
using Utilities;
using System.Data;
using Newtonsoft.Json;
using System.Threading;

namespace Driver.IT_0363a
{
    /// <summary>
    /// Creates Enrollment XML files from both Issuers 20662 and 60829, for submission to EDGE server
    /// Previously known as IT_0250
    /// </summary>
    public class EnrollmentExtract
    {
        
        public static List<String> Main(IT_0363ACAEdgeReporting caller, string EDGEenvironment, String reportYearStart, String reportYearEnd)
        {

            //Execute stored procedure to populate Enrollment table            
            ExtractFactory.ConnectAndQuery<includedInsuredMember>(caller.LoggerPhpConfig, @"EXEC [dbo].[IT0363_EdgeEnrollmentExtract_SP] N'" + reportYearEnd + @"', N'" + reportYearStart + @"'");


            List<String> files = new List<String>();

            FileSystem.ReportYearDir(caller.LoggerOutputYearDir + @"Submit\");
            //Run Enrollment process for each of the two issuers
            files.Add(RunEnrollment("", EDGEenvironment, caller, reportYearEnd));
            files.Add(RunEnrollment("", EDGEenvironment, caller, reportYearEnd));

            return files;
        }


        public static string RunEnrollment(String issuerID, String env, IT_0363ACAEdgeReporting caller, String reportYearEnd)
        {
            //Set run variables
            String submissionType = "E";
            String runDateTime = DateTime.Now.ToString("MMddyy") + "T" + DateTime.Now.ToString("HHmmssff");
            String fileIdentifier = submissionType + DateTime.Now.ToString("MMddyy") + "T" + DateTime.Now.ToString("HHmm");
            String generationDateTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            String interfaceControlReleaseNumber = "02.01.07";
            caller.LoggerReportYearDir(caller.LoggerOutputYearDir);
            String XMLFilePathIssuer = caller.LoggerOutputYearDir + @"Submit\" + issuerID + "." + submissionType + "." + "D" + runDateTime + "." + env + ".xml";
            int recordID = 1;
            int countMemberProfile = 0;

            //Build top level objects
            edgeServerEnrollmentSubmission EDGEServerEnrollmentSubmission1 = new edgeServerEnrollmentSubmission();
            includedEnrollmentIssuer includedEnrollmentIssuer1 = new includedEnrollmentIssuer();
            EDGEServerEnrollmentSubmission1.includedEnrollmentIssuer = includedEnrollmentIssuer1;

            //Setup to output to XML
            XmlSerializer mySerializer = new System.Xml.Serialization.XmlSerializer(typeof(edgeServerEnrollmentSubmission));
            TextWriter writer = new StreamWriter(XMLFilePathIssuer);

            //Populate header data
            EDGEServerEnrollmentSubmission1.fileIdentifier = fileIdentifier;
            EDGEServerEnrollmentSubmission1.executionZoneCode = env;
            EDGEServerEnrollmentSubmission1.interfaceControlReleaseNumber = interfaceControlReleaseNumber;
            EDGEServerEnrollmentSubmission1.generationDateTime = generationDateTime;
            EDGEServerEnrollmentSubmission1.submissionTypeCode = submissionType;
            includedEnrollmentIssuer1.issuerIdentifier = issuerID;
            EDGEServerEnrollmentSubmission1.includedEnrollmentIssuer.recordIdentifier = recordID;

            //Populate includedInsuredMember objects from flow table
            List<includedInsuredMember> memberList = new List<includedInsuredMember>();
            List<includedInsuredMemberProfile> membersWhoCantHaveOnly001InReportYear = new List<includedInsuredMemberProfile>();
            memberList.AddRange(ExtractFactory.ConnectAndQuery<includedInsuredMember>(caller.LoggerPhpConfig, @"SELECT * FROM [dbo].[IT0363_EdgeEnrollmentExtract_F] where issuerIdentifier = '" + issuerID + "'").ToList());
            int memberCount = 0;
            includedEnrollmentIssuer1.includedInsuredMember = memberList;

            ParallelOptions options = new ParallelOptions();
            options.MaxDegreeOfParallelism = 4;
            try
            {
                Parallel.ForEach(includedEnrollmentIssuer1.includedInsuredMember, options, member =>
                {
                    memberCount += 1;
                    if (memberCount % 100 == 0)
                    {
                        caller.WriteToLog($"We are processing member {memberCount} out of {memberList.Count} for issuerID {issuerID}");
                    }
                    member.recordIdentifier = recordID;

                    List<includedInsuredMemberProfile> memberProfiles = getMemberProfiles(member, caller, reportYearEnd, membersWhoCantHaveOnly001InReportYear);

                    member.includedInsuredMemberProfile = memberProfiles;

                    foreach (includedInsuredMemberProfile memberProfile in member.includedInsuredMemberProfile)
                    {
                        memberProfile.recordIdentifier = recordID;
                        if (memberProfile.subscriberIndicator == "S")
                        {
                            memberProfile.subscriberIdentifier = "";
                        }
                        if (memberProfile.insurancePlanIdentifier == null)
                        {
                            memberProfile.insurancePlanIdentifier = "";
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                caller.WriteToLog($"There was a problem processing a member: {ex}");
            }
            foreach (includedInsuredMember x in EDGEServerEnrollmentSubmission1.includedEnrollmentIssuer.includedInsuredMember)
            {
                recordID += 1;
                x.recordIdentifier = recordID;
                countMemberProfile += x.includedInsuredMemberProfile.Count;
                foreach (includedInsuredMemberProfile y in x.includedInsuredMemberProfile)
                {
                    recordID += 1;
                    y.recordIdentifier = recordID;
                }
            }

            //override premiums when there's an entry for them in the override table
            IEnumerable<PremiumOverride> premiumOverrides = ExtractFactory.ConnectAndQuery<PremiumOverride>(caller.LoggerPhpConfig, "SELECT * FROM IT0363_PremiumOverride_C");
            foreach (PremiumOverride premiumOverride in premiumOverrides.Where(x => x.IssuerID.ToString() == issuerID))
            {
                //all the cool kids use linq queries as frequently as possible
                includedInsuredMember includedInsuredMember = EDGEServerEnrollmentSubmission1.includedEnrollmentIssuer.includedInsuredMember.Where(x => x.insuredMemberIdentifier == premiumOverride.InsuredMemberIdentifier)
                    .FirstOrDefault();
                if (includedInsuredMember == null) //couldn't find member
                {
                    SendAlerts.Send(caller.ProcessId, 4, "EDGE Premium Override Member Missing", "Member " + premiumOverride.InsuredMemberIdentifier + " not found in issuer " + premiumOverride.IssuerID + ", could not override premium.", caller);
                    continue;
                }

                includedInsuredMemberProfile profile = includedInsuredMember.includedInsuredMemberProfile
                    .Where(x => x.coverageStartDate == premiumOverride.CoverageStartDate.ToString("yyyy-MM-dd") && x.coverageEndDate == premiumOverride.CoverageEndDate.ToString("yyyy-MM-dd"))
                    .FirstOrDefault();
                if (profile == null) //couldn't find timeline
                {
                    SendAlerts.Send(caller.ProcessId, 4, "EDGE Premium Override Timeline Missing", "Timeline from " + premiumOverride.CoverageEndDate.ToString("yyyy-MM-dd") + " to " + premiumOverride.CoverageEndDate.ToString("yyyy-MM-dd")
                        + " not found for member " + premiumOverride.InsuredMemberIdentifier + ", could not override premium", caller);
                    continue;
                }

                profile.insurancePlanPremiumAmount = premiumOverride.OverridenPremium.ToString();
                caller.WriteToLog("Overrode premium for " + premiumOverride.InsuredMemberIdentifier + " from " + premiumOverride.CoverageStartDate + " to " + premiumOverride.CoverageEndDate + " as " + premiumOverride.OverridenPremium);
            }

            //override rating areas when there's an entry for them in the override table
            IEnumerable<RatingAreaOverride> ratingAreaOverrides = ExtractFactory.ConnectAndQuery<RatingAreaOverride>(caller.LoggerPhpConfig, "SELECT * FROM IT0363_EDGE_RatingAreaOverride_C");
            foreach (RatingAreaOverride ratingAreaOverride in ratingAreaOverrides.Where(x => x.IssuerID.ToString() == issuerID))
            {
                //all the cool kids use linq queries as frequently as possible
                includedInsuredMember includedInsuredMember = EDGEServerEnrollmentSubmission1.includedEnrollmentIssuer.includedInsuredMember.Where(x => x.insuredMemberIdentifier == ratingAreaOverride.InsuredMemberIdentifier)
                    .FirstOrDefault();
                if (includedInsuredMember == null) //couldn't find member
                {
                    SendAlerts.Send(caller.ProcessId, 4, "EDGE Rating Area Override Member Missing", "Member " + ratingAreaOverride.InsuredMemberIdentifier + " not found in issuer " + ratingAreaOverride.IssuerID + ", could not override rating area.", caller);
                    continue;
                }

                includedInsuredMemberProfile profile = includedInsuredMember.includedInsuredMemberProfile
                    .Where(x => x.coverageStartDate == ratingAreaOverride.CoverageStartDate.ToString("yyyy-MM-dd") && x.coverageEndDate == ratingAreaOverride.CoverageEndDate.ToString("yyyy-MM-dd"))
                    .FirstOrDefault();
                if (profile == null) //couldn't find timeline
                {
                    SendAlerts.Send(caller.ProcessId, 4, "EDGE Rating Area Override Timeline Missing", "Timeline from " + ratingAreaOverride.CoverageEndDate.ToString("yyyy-MM-dd") + " to " + ratingAreaOverride.CoverageEndDate.ToString("yyyy-MM-dd")
                        + " not found for member " + ratingAreaOverride.InsuredMemberIdentifier + ", could not override rating area", caller);
                    continue;
                }

                profile.rateAreaIdentifier = ratingAreaOverride.OverridenRatingArea.ToString();
                caller.WriteToLog("Overrode Rating Area for " + ratingAreaOverride.InsuredMemberIdentifier + " from " + ratingAreaOverride.CoverageStartDate + " to " + ratingAreaOverride.CoverageEndDate + " as " + ratingAreaOverride.OverridenRatingArea);
            }

            //Total counts and money
            EDGEServerEnrollmentSubmission1.insuredMemberTotalQuantity = includedEnrollmentIssuer1.includedInsuredMember.Count;
            EDGEServerEnrollmentSubmission1.insuredMemberProfileTotalQuantity = countMemberProfile;
            includedEnrollmentIssuer1.issuerInsuredMemberTotalQuantity = includedEnrollmentIssuer1.includedInsuredMember.Count;
            includedEnrollmentIssuer1.issuerInsuredMemberProfileTotalQuantity = countMemberProfile;

            //Write out XML
            mySerializer.Serialize(writer, EDGEServerEnrollmentSubmission1);
            writer.Close();
            return XMLFilePathIssuer;
        }


        private class EligibilityHistory
        {
            public Int32 MemberKey { get; set; }
            public DateTime EligibilityStart { get; set; }
            public DateTime EligibilityEnd { get; set; }
            public String ProductID { get; set; }
            public String HIOS_ID { get; set; }
            public String BusCatType { get; set; }
            public Nullable<DateTime> SubscriberEffectiveDate{ get; set; }
            public Nullable<DateTime> SubscriberEndDate { get; set; }
            public string GroupRenewalDate { get; set; }
            public string GroupID { get; set; }
            public string ZipCode { get; set; }
        }

        private class PremiumChangeHistory
        {
            public decimal MonthlyPremium { get; set; }
            public DateTime CoverageThruDateAdjusted { get; set; }
        }

        /// <summary>
        /// Method to transform MEPE records to Edge Member Profile records. Basically, if there was no Edge relevant change during a MEPE period, 
        /// or even between two contiguious MEPE periods we merge them into a single Member Profile. However, if a subscriber added a dependent, changed a product, premium changed, had a HIOS change
        /// or a break in coverage as we troll through their MEPE history (both within a single MEPE range or across contiguous MEPE ranges) and start to split the MEPE timelines to 
        /// create the Edge Member Profile periods.
        /// </summary>
        /// <param name="extractedMember">Edge Member Record to Interrogate Their MEPE History</param>
        /// <param name="caller">Calling Program</param>
        /// <param name="reportYearEnd">End of the Reporting year we're using for the MEPE periods</param>
        /// <returns>List of Edge Member Profile Records that can be serialized to the XML file, truncated to the last two years</returns>
        private static List<includedInsuredMemberProfile> getMemberProfiles(includedInsuredMember extractedMember, IT_0363ACAEdgeReporting caller, String reportYearEnd, List<includedInsuredMemberProfile> membersWhoCantHaveOnly001InReportYear)
        {
            List<includedInsuredMemberProfile> memberProfiles = new List<includedInsuredMemberProfile>();
            List<EligibilityHistory> mepeHistory = ExtractFactory.ConnectAndQuery<EligibilityHistory>(caller.LoggerPhpConfig, @"EXEC [dbo].[IT0363_EdgeGetMemberEligibility_SP] " + extractedMember.insuredMemberIdentifier + ",'" + reportYearEnd + "'").ToList();
            int reportYear;
            try{reportYear = DateTime.Parse(reportYearEnd).Year;}
            catch{reportYear = 1900;}
            List<PremiumChangeHistory> PremiumDetails = GetInitialPremiumDetails(extractedMember.subscriberIndicator, extractedMember.subscriberIdentifier, caller);

            for (int i = 0; i < mepeHistory.Count; i++)
            {

                List<EligibilityHistory> addedDepEligibility;

                Boolean didSubAddDep = DidSubscriberAddDependent(mepeHistory[i], out addedDepEligibility, extractedMember.subscriberIndicator, extractedMember.subscriberIdentifier, caller);
                Boolean didProductChange = DidProductChange(i, mepeHistory);
                Boolean didHIOSChange = DidHIOSChange(i, mepeHistory);
                Boolean breakInCoverage = WasThereABreakInCoverage(i, mepeHistory);

                String renewalEpaiCode = (i == 0 || breakInCoverage ? "021028" : "021041");

                if (extractedMember.subscriberIndicator == "S")
                {
                    List<PremiumChangeHistory> premiumChangeBetweenTermsData;

                    //Did the premium change when we went from one MEPE to the next
                    Boolean didPremiumChangeBetweenTerms = DidPremiumChangeAcrossTerms(mepeHistory[i], extractedMember.subscriberIndicator, PremiumDetails, out premiumChangeBetweenTermsData);

                    //Did the premium change within the current MEPE term
                    List<PremiumChangeHistory> premiumChangeWithinTermsData;

                    Boolean didPremiumChange = DidPremiumChangeWithinTerm(mepeHistory[i], extractedMember.subscriberIndicator, PremiumDetails, out premiumChangeWithinTermsData);

                    //If block added to circumvent the rare instance where user has no premium
                    decimal OriginalPremiumAmount;
                    if (premiumChangeBetweenTermsData.Count > 0)
                    {
                        OriginalPremiumAmount = premiumChangeBetweenTermsData[0].MonthlyPremium;
                    }
                    else
                    {
                        OriginalPremiumAmount = 0;
                    }
                    //decimal OriginalPremiumAmount = premiumChangeBetweenTermsData[0].MonthlyPremium;

                    if (didPremiumChange) //did it change within a term
                    {
                        for (int p = 0; p < premiumChangeWithinTermsData.Count; p++)
                        {
                            DateTime profileStartDate;
                            DateTime profileEndDate;

                            if (p == 0 && premiumChangeWithinTermsData.Count > 1)
                            {
                                profileStartDate = mepeHistory[i].EligibilityStart;
                                profileEndDate = premiumChangeWithinTermsData[p].CoverageThruDateAdjusted;
                                if (profileStartDate > profileEndDate)
                                {
                                    continue;
                                }
                                else
                                {
                                    memberProfiles.AddRange(generateMemberProfile(extractedMember, mepeHistory[i], getPremiumEPAICode(profileStartDate, mepeHistory[i].GroupRenewalDate, breakInCoverage, i), premiumChangeWithinTermsData[p].MonthlyPremium, profileStartDate, profileEndDate));
                                }
                            }
                            else if (p == premiumChangeWithinTermsData.Count - 1) //last premium change
                            {
                                profileStartDate = premiumChangeWithinTermsData[p - 1].CoverageThruDateAdjusted.AddDays(1);
                                profileEndDate = mepeHistory[i].EligibilityEnd;
                                if (profileStartDate > profileEndDate)
                                {
                                    continue;
                                }
                                else
                                {
                                    memberProfiles.AddRange(generateMemberProfile(extractedMember, mepeHistory[i], getPremiumEPAICode(profileStartDate, mepeHistory[i].GroupRenewalDate), premiumChangeWithinTermsData[p].MonthlyPremium, profileStartDate, profileEndDate));
                                }
                            }
                            else
                            {
                                profileStartDate = premiumChangeWithinTermsData[p - 1].CoverageThruDateAdjusted.AddDays(1);
                                profileEndDate = premiumChangeWithinTermsData[p].CoverageThruDateAdjusted;
                                if (profileStartDate > profileEndDate)
                                {
                                    continue;
                                }
                                else
                                {
                                    memberProfiles.AddRange(generateMemberProfile(extractedMember, mepeHistory[i], getPremiumEPAICode(profileStartDate, mepeHistory[i].GroupRenewalDate), premiumChangeWithinTermsData[p].MonthlyPremium, profileStartDate, profileEndDate));
                                }
                            }

                        }
                    }
                    else if (didPremiumChangeBetweenTerms && i > 0) //did it change when we moved from one mepe to the next
                    {
                        memberProfiles.AddRange(generateMemberProfile(extractedMember, mepeHistory[i], getPremiumEPAICode(mepeHistory[i - 1].EligibilityEnd.AddDays(1), mepeHistory[i].GroupRenewalDate), premiumChangeBetweenTermsData[1].MonthlyPremium, mepeHistory[i - 1].EligibilityEnd.AddDays(1), mepeHistory[i].EligibilityEnd));
                    }
                    else if (didSubAddDep) //did the subscriber add a dependent
                    {
                        if (mepeHistory[i].EligibilityStart == addedDepEligibility[0].EligibilityStart) //this dependent was added at beginning of term
                        {
                            memberProfiles.AddRange(generateMemberProfile(extractedMember, mepeHistory[i], renewalEpaiCode, OriginalPremiumAmount));

                        }
                        else //if they added a dependent mid-year
                        {
                            memberProfiles.AddRange(generateMemberProfile(extractedMember, mepeHistory[i], renewalEpaiCode, OriginalPremiumAmount, mepeHistory[i].EligibilityStart, addedDepEligibility[0].EligibilityStart.AddDays(-1)));

                            memberProfiles.AddRange(generateMemberProfile(extractedMember, mepeHistory[i], "001", OriginalPremiumAmount, addedDepEligibility[0].EligibilityStart, mepeHistory[i].EligibilityEnd));
                        }
                    }
                    else if (didProductChange || didHIOSChange)
                    {
                        memberProfiles.AddRange(generateMemberProfile(extractedMember, mepeHistory[i], "021028", OriginalPremiumAmount)); //if they switch products mid-year or HIOS has changed
                    }
                    else //nothing was changed
                    {
                        if (i == 0)
                        {
                            memberProfiles.AddRange(generateMemberProfile(extractedMember, mepeHistory[i], "021028", OriginalPremiumAmount));
                        }
                        else
                        {
                            String epaiCode = (breakInCoverage ? "021028" : "021041");
                            memberProfiles.AddRange(generateMemberProfile(extractedMember, mepeHistory[i], epaiCode, OriginalPremiumAmount));

                        }

                    }
                }
                else //dependents
                {
                    if (didSubAddDep)
                    {
                        if (addedDepEligibility.Count(x => x.MemberKey == extractedMember.insuredMemberIdentifier) > 0) //if their MEME_CK is in the subscriber's added list, then 021EC they were added
                        {
                            memberProfiles.AddRange(generateMemberProfile(extractedMember, mepeHistory[i], "021EC", 0)); //dependent added mid-term

                        }
                        else
                        {
                            memberProfiles.AddRange(generateMemberProfile(extractedMember, mepeHistory[i], "021028", 0)); //a different dependent was added
                        }
                    }
                    else if (didProductChange || didHIOSChange)
                    {
                        memberProfiles.AddRange(generateMemberProfile(extractedMember, mepeHistory[i], "021028", 0)); //if they switch products mid-year or HIOS has changed
                    }
                    else //nothing was changed
                    {
                        if (i == 0)
                        {
                            memberProfiles.AddRange(generateMemberProfile(extractedMember, mepeHistory[i], "021028", 0));
                        }
                        else
                        {
                            String epaiCode = (breakInCoverage ? "021028" : "021041");
                            memberProfiles.AddRange(generateMemberProfile(extractedMember, mepeHistory[i], epaiCode, 0));
                        }
                    }
                }
            }

            //set member's first timeline maintenance code to 021028 since that's the start code       
            memberProfiles.OrderBy(m => m.coverageStartDate).First().enrollmentMaintenanceTypeCode = "021028";
            try
            {
                //If member does not have any enrollment timeline with an end date in the measurement year with maintenance code of 021028 or 021041
                if (!(memberProfiles.Any(x => DateTime.Parse(x.coverageEndDate) >= new DateTime(reportYear, 01, 01) && DateTime.Parse(x.coverageEndDate) <= new DateTime(reportYear, 12, 31) && (x.enrollmentMaintenanceTypeCode == "021028" || x.enrollmentMaintenanceTypeCode == "021041"))))
                {
                    //then...
                    foreach (includedInsuredMemberProfile member in memberProfiles)
                    {
                        //for any timeline with a 1/1 start date in the measurement year and a maintenance code of 001, modify the maintenance code to be 021041
                        if (DateTime.Parse(member.coverageStartDate) == new DateTime(reportYear, 01, 01) && member.enrollmentMaintenanceTypeCode == "001")
                        {
                            member.enrollmentMaintenanceTypeCode = "021041";
                            includedInsuredMemberProfile memberToKeepTrackOf = new includedInsuredMemberProfile();
                            memberToKeepTrackOf.coverageEndDate = member.coverageEndDate;
                            memberToKeepTrackOf.coverageStartDate = member.coverageStartDate;
                            memberToKeepTrackOf.enrollmentMaintenanceTypeCode = member.enrollmentMaintenanceTypeCode;
                            memberToKeepTrackOf.insurancePlanIdentifier = member.insurancePlanIdentifier;
                            memberToKeepTrackOf.insurancePlanPremiumAmount = member.insurancePlanPremiumAmount;
                            memberToKeepTrackOf.rateAreaIdentifier = member.rateAreaIdentifier;
                            memberToKeepTrackOf.recordIdentifier = member.recordIdentifier;
                            memberToKeepTrackOf.subscriberIdentifier = member.subscriberIdentifier;
                            memberToKeepTrackOf.subscriberIndicator = member.subscriberIndicator;
                            membersWhoCantHaveOnly001InReportYear.Add(memberToKeepTrackOf);
                        }
                    }   
                }
            }
            catch(Exception ex){}
            //If we made the change to a subscriber, then we need to check whether there's any dependents that need to update their history to match the subscriber date too.
            if (membersWhoCantHaveOnly001InReportYear.Any(x => x.subscriberIdentifier == memberProfiles[0].subscriberIdentifier) && memberProfiles[0].subscriberIndicator != "S")
            {
                includedInsuredMemberProfile x = membersWhoCantHaveOnly001InReportYear.FirstOrDefault(c => c.subscriberIdentifier == memberProfiles[0].subscriberIdentifier);
                includedInsuredMemberProfile y = memberProfiles.LastOrDefault();
                if ((y.coverageStartDate != x.coverageStartDate || y.coverageEndDate != x.coverageEndDate || y.enrollmentMaintenanceTypeCode != x.enrollmentMaintenanceTypeCode) && extractedMember.subscriberIdentifier != "10367751")
                {
                    memberProfiles.LastOrDefault().coverageStartDate = x.coverageStartDate;
                    memberProfiles.LastOrDefault().coverageEndDate = x.coverageEndDate;
                    memberProfiles.LastOrDefault().enrollmentMaintenanceTypeCode = x.enrollmentMaintenanceTypeCode;
                }
            }
            foreach (includedInsuredMemberProfile x in memberProfiles)
            {
                if (x.subscriberIndicator != "S" && x.enrollmentMaintenanceTypeCode == "001")
                {
                    x.enrollmentMaintenanceTypeCode = "002";
                }
            }
            try
            {
                //This is duct tape. Please remove eventually. 4/5/2024
                switch (extractedMember.insuredMemberIdentifier.ToString())
                {
                    case "7578702":
                        var serialized7578702 = JsonConvert.SerializeObject(memberProfiles[5]);
                        includedInsuredMemberProfile test7578702 = JsonConvert.DeserializeObject<includedInsuredMemberProfile>(serialized7578702);
                        memberProfiles.Add(test7578702);
                        memberProfiles[5].coverageStartDate = "2022-09-01";
                        memberProfiles[5].coverageEndDate = "2022-12-31";
                        memberProfiles[6].coverageStartDate = "2023-01-01";
                        memberProfiles[6].coverageEndDate = "2023-08-31";
                        break;
                    case "7578704":
                        var serialized7578704 = JsonConvert.SerializeObject(memberProfiles[5]);
                        includedInsuredMemberProfile test7578704 = JsonConvert.DeserializeObject<includedInsuredMemberProfile>(serialized7578704);
                        memberProfiles.Add(test7578704);
                        memberProfiles[5].coverageStartDate = "2022-09-01";
                        memberProfiles[5].coverageEndDate = "2022-12-31";
                        memberProfiles[6].coverageStartDate = "2023-01-01";
                        memberProfiles[6].coverageEndDate = "2023-08-31";
                        break;
                    case "7578705":
                        var serialized7578705 = JsonConvert.SerializeObject(memberProfiles[5]);
                        includedInsuredMemberProfile test7578705 = JsonConvert.DeserializeObject<includedInsuredMemberProfile>(serialized7578705);
                        memberProfiles.Add(test7578705);
                        memberProfiles[5].coverageStartDate = "2022-09-01";
                        memberProfiles[5].coverageEndDate = "2022-12-31";
                        memberProfiles[6].coverageStartDate = "2023-01-01";
                        memberProfiles[6].coverageEndDate = "2023-08-31";
                        break;
                    case "7700351":
                        memberProfiles[1].coverageStartDate = "2022-10-01";
                        memberProfiles[1].coverageEndDate = "2022-12-31";
                        memberProfiles[1].enrollmentMaintenanceTypeCode = "001";
                        var serialized7700351 = JsonConvert.SerializeObject(memberProfiles[1]);
                        includedInsuredMemberProfile test7700351 = JsonConvert.DeserializeObject<includedInsuredMemberProfile>(serialized7700351);
                        test7700351.coverageStartDate = "2023-01-01";
                        test7700351.coverageEndDate = "2023-08-31";
                        test7700351.enrollmentMaintenanceTypeCode = "021028";
                        memberProfiles.Add(test7700351);
                        break;
                    case "7700354":
                        memberProfiles[0].coverageStartDate = "2022-09-01";
                        memberProfiles[0].coverageEndDate = "2022-12-31";
                        memberProfiles[0].enrollmentMaintenanceTypeCode = "021028";
                        var serialized7700354 = JsonConvert.SerializeObject(memberProfiles[0]);
                        includedInsuredMemberProfile test7700354 = JsonConvert.DeserializeObject<includedInsuredMemberProfile>(serialized7700354);
                        test7700354.coverageStartDate = "2023-01-01";
                        test7700354.coverageEndDate = "2023-08-31";
                        test7700354.enrollmentMaintenanceTypeCode = "021028";
                        memberProfiles.Add(test7700354);
                        break;
                    case "7700356":
                        memberProfiles[0].coverageStartDate = "2022-09-01";
                        memberProfiles[0].coverageEndDate = "2022-12-31";
                        memberProfiles[0].enrollmentMaintenanceTypeCode = "021028";
                        var serialized7700356 = JsonConvert.SerializeObject(memberProfiles[0]);
                        includedInsuredMemberProfile test7700356 = JsonConvert.DeserializeObject<includedInsuredMemberProfile>(serialized7700356);
                        test7700356.coverageStartDate = "2023-01-01";
                        test7700356.coverageEndDate = "2023-08-31";
                        test7700356.enrollmentMaintenanceTypeCode = "021028";
                        memberProfiles.Add(test7700356);
                        break;
                    //60829
                    case "10300302":
                        memberProfiles[11].coverageStartDate = "2022-10-01";
                        memberProfiles[11].coverageEndDate = "2022-12-31";
                        memberProfiles[11].enrollmentMaintenanceTypeCode = "001";
                        var serialized10300302 = JsonConvert.SerializeObject(memberProfiles[11]);
                        includedInsuredMemberProfile test10300302 = JsonConvert.DeserializeObject<includedInsuredMemberProfile>(serialized10300302);
                        test10300302.coverageStartDate = "2023-01-01";
                        test10300302.coverageEndDate = "2023-01-15";
                        test10300302.enrollmentMaintenanceTypeCode = "021028";
                        memberProfiles.Add(test10300302);
                        break;
                    case "10367751":
                        memberProfiles[1].enrollmentMaintenanceTypeCode = "001";
                        break;
                    case "1372350":
                        memberProfiles[11].coverageStartDate = "2022-10-01";
                        memberProfiles[11].coverageEndDate = "2022-12-31";
                        memberProfiles[11].enrollmentMaintenanceTypeCode = "001";
                        var serialized1372350 = JsonConvert.SerializeObject(memberProfiles[11]);
                        includedInsuredMemberProfile test1372350 = JsonConvert.DeserializeObject<includedInsuredMemberProfile>(serialized1372350);
                        test1372350.coverageStartDate = "2023-01-01";
                        test1372350.coverageEndDate = "2023-01-15";
                        test1372350.enrollmentMaintenanceTypeCode = "021028";
                        memberProfiles.Add(test1372350);
                        break;
                    case "1372351":
                        memberProfiles[6].coverageStartDate = "2022-05-01";
                        memberProfiles[6].coverageEndDate = "2022-12-31";
                        memberProfiles[6].enrollmentMaintenanceTypeCode = "021028";
                        var serialized1372351 = JsonConvert.SerializeObject(memberProfiles[6]);
                        includedInsuredMemberProfile test1372351 = JsonConvert.DeserializeObject<includedInsuredMemberProfile>(serialized1372351);
                        test1372351.coverageStartDate = "2023-01-01";
                        test1372351.coverageEndDate = "2023-01-15";
                        test1372351.enrollmentMaintenanceTypeCode = "021028";
                        memberProfiles.Add(test1372351);
                        break;
                    case "1372352":
                        memberProfiles[6].coverageStartDate = "2022-05-01";
                        memberProfiles[6].coverageEndDate = "2022-12-31";
                        memberProfiles[6].enrollmentMaintenanceTypeCode = "021028";
                        var serialized1372352 = JsonConvert.SerializeObject(memberProfiles[6]);
                        includedInsuredMemberProfile test1372352 = JsonConvert.DeserializeObject<includedInsuredMemberProfile>(serialized1372352);
                        test1372352.coverageStartDate = "2023-01-01";
                        test1372352.coverageEndDate = "2023-01-15";
                        test1372352.enrollmentMaintenanceTypeCode = "021028";
                        memberProfiles.Add(test1372352);
                        break;
                    case "1372353":
                        memberProfiles[0].coverageStartDate = "2022-09-24";
                        memberProfiles[0].coverageEndDate = "2022-12-31";
                        memberProfiles[0].enrollmentMaintenanceTypeCode = "021028";
                        var serialized1372353 = JsonConvert.SerializeObject(memberProfiles[0]);
                        includedInsuredMemberProfile test1372353 = JsonConvert.DeserializeObject<includedInsuredMemberProfile>(serialized1372353);
                        test1372353.coverageStartDate = "2023-01-01";
                        test1372353.coverageEndDate = "2023-01-15";
                        test1372353.enrollmentMaintenanceTypeCode = "021028";
                        memberProfiles.Add(test1372353);
                        break;
                    case "1372354":
                        memberProfiles[0].coverageStartDate = "2022-09-24";
                        memberProfiles[0].coverageEndDate = "2022-12-31";
                        memberProfiles[0].enrollmentMaintenanceTypeCode = "021028";
                        var serialized1372354 = JsonConvert.SerializeObject(memberProfiles[0]);
                        includedInsuredMemberProfile test1372354 = JsonConvert.DeserializeObject<includedInsuredMemberProfile>(serialized1372354);
                        test1372354.coverageStartDate = "2023-01-01";
                        test1372354.coverageEndDate = "2023-01-15";
                        test1372354.enrollmentMaintenanceTypeCode = "021028";
                        memberProfiles.Add(test1372354);
                        break;
                    case "5932003":
                        memberProfiles[1].coverageEndDate = "2023-12-31";
                        var item = memberProfiles[2];
                        memberProfiles.Remove(item);
                        break;
                    case "9125552":
                        memberProfiles[3].coverageEndDate = "2023-12-31";
                        var item1 = memberProfiles[4];
                        memberProfiles.Remove(item1);
                        break;
                    case "9125553":
                        memberProfiles[2].coverageEndDate = "2023-12-31";
                        var item2 = memberProfiles[3];
                        memberProfiles.Remove(item2);
                        break;
                    case "9706004":
                        memberProfiles[11].coverageStartDate = "2022-10-01";
                        memberProfiles[11].coverageEndDate = "2022-12-31";
                        memberProfiles[11].enrollmentMaintenanceTypeCode = "001";
                        var serialized9706004 = JsonConvert.SerializeObject(memberProfiles[11]);
                        includedInsuredMemberProfile test9706004 = JsonConvert.DeserializeObject<includedInsuredMemberProfile>(serialized9706004);
                        test9706004.coverageStartDate = "2023-01-01";
                        test9706004.coverageEndDate = "2023-01-15";
                        test9706004.enrollmentMaintenanceTypeCode = "021028";
                        memberProfiles.Add(test9706004);
                        break;
                }
            }
            catch (Exception ex)
            {
                
            }

            //SaveProfile(memberProfiles, extractedMember.insuredMemberIdentifier); //Glauch - only save the profiles for debugging purposes
            return memberProfiles;
        }

        private static string getPremiumEPAICode(DateTime start, string GroupRenewalDate, bool breakInCoverage = false, int index = 1)
        {
            if (index == 0 || breakInCoverage)
            {
                return "021028";
            }
            int renewalMonth = Convert.ToInt32(GroupRenewalDate.Substring(0, 2));
            int renewalDay = Convert.ToInt32(GroupRenewalDate.Substring(2, 2));
            if ((start.Month == renewalMonth && start.Day == renewalDay))
            {
                return "021041";
            }
            else
            {
                return "001";
            }
        }

        private static bool WasThereABreakInCoverage(int i, List<EligibilityHistory> mepeHistory)
        {
            if (i == 0)
            {
                return false;
            }
            else
            {
                if (mepeHistory[i - 1].EligibilityEnd.AddDays(1) != mepeHistory[i].EligibilityStart)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        private static string GetWarehouseInfo(string memberCK)
        {
            return $@"";
        }
        /// <summary>
        /// Get's the premium history for the current subscriber, else returns nothing
        /// </summary>
        /// <param name="subIndicator">If this isn't S, we return an empty list</param>
        /// <param name="subscriberMemberKey">Subscriber's MEME_CK is used to find the appropriate BLSB records</param>
        /// <returns>Premiums with thru dates</returns>
        private static List<PremiumChangeHistory> GetInitialPremiumDetails(String subIndicator, String subscriberMemberKey, IT_0363ACAEdgeReporting caller)
        {
            List<PremiumChangeHistory> PremiumDetails;

            if (subIndicator == "S")
            {
                try
                {
                    PremiumDetails = ExtractFactory.ConnectAndQuery<PremiumChangeHistory>(caller.LoggerPhpConfig, "EXEC [dbo].[IT0363_ISUIPA_Premium_Change_SP] " + subscriberMemberKey).ToList();
                }
                catch
                {
                    PremiumDetails = new List<PremiumChangeHistory>();
                }
                if (PremiumDetails.Count == 0)
                {
                    PremiumDetails = ExtractFactory.ConnectAndQuery<PremiumChangeHistory>(caller.LoggerPhpConfig, "EXEC [dbo].[IT0363_EdgePremiumChange_SP] " + subscriberMemberKey).ToList();
                }
            }
            else
            {
                PremiumDetails = new List<PremiumChangeHistory>();
            }
            return PremiumDetails;
        }

        private static Boolean DidProductChange(int HistCount, List<EligibilityHistory> EligHist)
        {
            if (HistCount == 0)
            {
                return false;
            }
            else
            {
                if (EligHist[HistCount - 1].ProductID == EligHist[HistCount].ProductID)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }

        private static Boolean DidHIOSChange(int HistCount, List<EligibilityHistory> EligHist)
        {
            if (HistCount == 0)
            {
                return false;
            }
            else
            {
                if (EligHist[HistCount - 1].HIOS_ID == EligHist[HistCount].HIOS_ID)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }

        private static Boolean DidPremiumChangeWithinTerm(EligibilityHistory EligHist, String subscriberInd, List<PremiumChangeHistory> PremiumHistory, out List<PremiumChangeHistory> PremiumTerms)
        {
            PremiumTerms = new List<PremiumChangeHistory>();
            List<PremiumChangeHistory> ComingPremiums = PremiumHistory.FindAll(x => x.CoverageThruDateAdjusted >= EligHist.EligibilityStart && x.CoverageThruDateAdjusted <= EligHist.EligibilityEnd);

            if (subscriberInd != "S" || (ComingPremiums.Count == 1 && EligHist.EligibilityEnd > ComingPremiums[0].CoverageThruDateAdjusted)) //if this is the subscriber, or we're at the end of the BLSBs
            {
                return false;
            }
            else
            {

                decimal lastPremium;

                if (ComingPremiums.Count == 0) //this is because the MEPE term wasn't at least a month
                {
                    lastPremium = 0;
                    return false;
                }
                else
                {
                    lastPremium = ComingPremiums[0].MonthlyPremium;
                }

                for (int i = 1; i < ComingPremiums.Count; i++)
                {

                    if (lastPremium == ComingPremiums[i].MonthlyPremium)
                    {
                        continue;
                    }
                    else
                    {
                        PremiumTerms.Add(ComingPremiums[i - 1]);
                        lastPremium = ComingPremiums[i].MonthlyPremium;
                    }
                }

                PremiumTerms.Add(ComingPremiums[ComingPremiums.Count - 1]); //add the last one


                return PremiumTerms.Select(x => x.MonthlyPremium).Distinct().Count() > 1;
            }
        }

        /// <summary>
        /// When the MEPE terms changed, was there a new premium effective?
        /// </summary>
        /// <param name="EligHist">Current MEPE term being processed</param>
        /// <param name="subscriberInd">Subscriber indicator - always returns false if this isn't "S"</param>
        /// <param name="PremiumHistory">Full premium history for this subscriber</param>
        /// <param name="PremiumTerms">Premium data for the MEPE term (you'll get 2 if we switched at the start of the MEPE term</param>
        /// <returns>If the premium changed when the MEPE term switched to the next</returns>
        private static Boolean DidPremiumChangeAcrossTerms(EligibilityHistory EligHist, String subscriberInd, List<PremiumChangeHistory> PremiumHistory, out List<PremiumChangeHistory> PremiumTerms)
        {
            PremiumTerms = new List<PremiumChangeHistory>();

            //List<PremiumChangeHistory> ComingTerms = PremiumHistory.FindAll(x => x.BLSB_COV_THRU_DT >= EligHist.MEPE_EFF_DT || x.BLSB_COV_THRU_DT == EligHist.MEPE_EFF_DT.AddDays(-1));

            if (PremiumHistory.Count == 0)
            {
                return false;
            }

            PremiumChangeHistory InitialPrem;

            try
            {
                InitialPrem = PremiumHistory.First(x => x.CoverageThruDateAdjusted >= EligHist.EligibilityStart);
            }
            catch
            {
                //there were no premiums since the MEPE_EFF_DT
                return false;
            }


            PremiumChangeHistory LastTerm = PremiumHistory.FindLast(x => x.CoverageThruDateAdjusted <= EligHist.EligibilityEnd);

            if (subscriberInd != "S" || (LastTerm == null) || InitialPrem.MonthlyPremium == LastTerm.MonthlyPremium)
            {
                PremiumTerms.Add(InitialPrem);
                return false;
            }
            else
            {
                PremiumTerms.Add(LastTerm);
                PremiumTerms.Add(InitialPrem);
                return true;
            }
        }

        private static Boolean DidSubscriberAddDependent(EligibilityHistory Mepe, out List<EligibilityHistory> addedDepEligHistory, String subscriberInd, String SubscriberMemberKey, IT_0363ACAEdgeReporting caller)
        {
            addedDepEligHistory = ExtractFactory.ConnectAndQuery<EligibilityHistory>(caller.LoggerPhpConfig, String.Format(@"EXEC [dbo].[IT0363_EdgeDidSubscriberAddDependent_SP] '{0}', '{1}', {2}", Mepe.EligibilityEnd, Mepe.EligibilityStart, SubscriberMemberKey)).ToList();

            if (addedDepEligHistory.Count == 0) //and you didn't find any added members
            {
                return false;
            }
            else
            {
                return true;
            }

        }

        private static IEnumerable<includedInsuredMemberProfile> generateMemberProfile(includedInsuredMember extractedMember, EligibilityHistory elgHist, String maintCode, decimal premiumAmount)
        {
            return generateMemberProfile(extractedMember, elgHist, maintCode, premiumAmount, elgHist.EligibilityStart, elgHist.EligibilityEnd);

        }

        /// <summary>
        /// Generates member profile as well as doing any timeline modifications that may be necessary
        /// </summary>
        /// <param name="extractedMember">Member this is for</param>
        /// <param name="eligHistory">The specific "slice" of elig history for the time period we're looking at</param>
        /// <param name="maintCode">Maintenance code for this timeline. Note that if it splits, the new one will likely have a different maintenance code.</param>
        /// <param name="premiumAmount">Premium amount for this elig period</param>
        /// <param name="coverageStart">Start of their timeline</param>
        /// <param name="coverageEnd">End of their timeline</param>
        /// <returns>One or more member profile objects depending on if any timeline modifications need to be made</returns>
        private static IEnumerable<includedInsuredMemberProfile> generateMemberProfile(includedInsuredMember extractedMember, EligibilityHistory eligHistory, String maintCode, decimal premiumAmount, DateTime coverageStart, DateTime coverageEnd)
        {
            //goal of this function: create timeline(s) no longer than a year that optimally start on the group renewal date
            List<includedInsuredMemberProfile> toReturn = new List<includedInsuredMemberProfile>();
            List<Timeline> timelines = new List<Timeline>();
            //get renewal month and day
            int renewalMonth = Convert.ToInt32(eligHistory.GroupRenewalDate.Substring(0, 2));
            int renewalDay = Convert.ToInt32(eligHistory.GroupRenewalDate.Substring(2, 2));

            //if timeline is longer than a year, we have to split it up
            if (coverageStart.AddYears(1) <= coverageEnd)
            {
                DateTime startDate, endDate;
                if (coverageStart.Month == renewalMonth && coverageStart.Day == renewalDay) //we're starting in the right place
                {
                    endDate = coverageStart.AddYears(1).AddDays(-1);
                }
                else //have to manipulate timeline to start on renewal date by ending this one a day before it
                {
                    if (renewalMonth > coverageStart.Month)
                    {
                        endDate = new DateTime(coverageStart.Year, renewalMonth, renewalDay).AddDays(-1);
                    }
                    else
                    {
                        endDate = new DateTime(coverageStart.Year + 1, renewalMonth, renewalDay).AddDays(-1);
                    }
                }
                timelines.Add(new Timeline() { Start = coverageStart, End = endDate, PremiumAmount = premiumAmount, MaintenanceCode = maintCode }); //first timeline has orig maint code
                while (endDate <= coverageEnd) //main loop once timeline's synced up with renewal date
                {
                    startDate = endDate.AddDays(1); //always gonna be one after the last end date
                    endDate = endDate.AddYears(1);
                    if (endDate.Month == 2 && endDate.Day == 28 && DateTime.IsLeapYear(endDate.Year)) //leap year
                    {
                        endDate = endDate.AddDays(1);
                    }
                    if (endDate <= coverageEnd)
                    {
                        timelines.Add(new Timeline() { Start = startDate, End = endDate, PremiumAmount = premiumAmount, MaintenanceCode = "021041" }); //next timeline(s) get renewal maint code
                    }
                }
                if (endDate.AddYears(-1) != coverageEnd && coverageEnd.AddYears(1) != endDate) //we broke out of that loop since the end date overflowed coverageEnd so we gotta subtract. Also gotta "belt and suspenders" it because of leap years
                {
                    endDate = endDate.AddYears(-1);
                    if (endDate.Month == 2 && endDate.Day == 28 && DateTime.IsLeapYear(endDate.Year))
                    {
                        endDate = endDate.AddDays(2); //so feb 29 doesn't mess you up on a leap year
                    }
                    else
                    {
                        endDate = endDate.AddDays(1); //normal
                    }
                    timelines.Add(new Timeline() { Start = endDate, End = coverageEnd, PremiumAmount = premiumAmount, MaintenanceCode = "021041" });
                }
            }
            else //period passed into function was less than a year
            {
                DateTime renewalDate;
                if (renewalMonth > coverageStart.Month)
                {
                    renewalDate = new DateTime(coverageStart.Year, renewalMonth, renewalDay);
                }
                else
                {
                    renewalDate = new DateTime(coverageStart.Year + 1, renewalMonth, renewalDay);
                }
                //if it starts on the renewal date, is about to end a day before the renewal date, or this is a particularly short timeline that won't reach the renewal date, leave it alone
                if ((coverageStart.Day == renewalDay && coverageStart.Month == renewalMonth) || (coverageEnd.AddDays(1) == renewalDate) || (coverageEnd < renewalDate))
                {
                    timelines.Add(new Timeline() { Start = coverageStart, End = coverageEnd, PremiumAmount = premiumAmount, MaintenanceCode = maintCode });
                }
                else
                {
                    //make it two timelines: one from coverageStart to renewal, one from renewal to coverageEnd
                    //this syncs it up to the renewal date and allows for an age recalculation
                    timelines.Add(new Timeline() { Start = coverageStart, End = renewalDate.AddDays(-1), PremiumAmount = premiumAmount, MaintenanceCode = maintCode });
                    timelines.Add(new Timeline() { Start = renewalDate, End = coverageEnd, PremiumAmount = premiumAmount, MaintenanceCode = "021041" });
                }
            }

            //do premium reductions for subscribers in small groups, April/May 2020 for now
            List<string> excludedGroups = new List<string>() { "", "" };
            List<Timeline> premiumReducedTimelines = getPremiumReducedTimelines(timelines, eligHistory, premiumAmount, excludedGroups, extractedMember.subscriberIndicator);

            foreach (Timeline timeline in premiumReducedTimelines)
            {
                includedInsuredMemberProfile enrollmentMemberTimeline = new includedInsuredMemberProfile();
                enrollmentMemberTimeline.subscriberIdentifier = extractedMember.subscriberIdentifier;
                enrollmentMemberTimeline.subscriberIndicator = extractedMember.subscriberIndicator;
                enrollmentMemberTimeline.coverageStartDate = timeline.Start.ToString("yyyy-MM-dd");
                enrollmentMemberTimeline.coverageEndDate = timeline.End.ToString("yyyy-MM-dd");
                enrollmentMemberTimeline.enrollmentMaintenanceTypeCode = timeline.MaintenanceCode;
                enrollmentMemberTimeline.insurancePlanIdentifier = eligHistory.HIOS_ID;
                enrollmentMemberTimeline.insurancePlanPremiumAmount = timeline.PremiumAmount.ToString("0.00"); //To format dollar amounts 
                enrollmentMemberTimeline.rateAreaIdentifier = getRateArea(eligHistory.BusCatType, extractedMember.SmallGroupRatingArea, extractedMember.IndividualGroupRatingArea);
                enrollmentMemberTimeline.recordIdentifier = 0;
                enrollmentMemberTimeline.zipCode             =  extractedMember.subscriberIndicator == "S"                                                                        ? eligHistory.ZipCode : "";
                enrollmentMemberTimeline.federalAPTC         = (extractedMember.subscriberIndicator == "S" && (eligHistory.GroupID == "" || eligHistory.GroupID == "")) ? "0"              : "";
                enrollmentMemberTimeline.statePremiumSubsidy = (extractedMember.subscriberIndicator == "S" && (eligHistory.GroupID == "" || eligHistory.GroupID == "")) ? "0"              : "";
                enrollmentMemberTimeline.stateCSR            =  extractedMember.subscriberIndicator == "S"                                                                        ? "0"              : "";
                enrollmentMemberTimeline.ICHRA_QSEHRA        =  extractedMember.subscriberIndicator == "S"                                                                        ? "U"              : "";
                enrollmentMemberTimeline.QSEHRA_Spousal      =  extractedMember.subscriberIndicator == "S"                                                                        ? "U"              : "";
                enrollmentMemberTimeline.QSEHRA_Medical      =  extractedMember.subscriberIndicator == "S"                                                                        ? "U"              : "";
                toReturn.Add(enrollmentMemberTimeline);
            }
            return toReturn;
        }

        private static List<Timeline> getPremiumReducedTimelines(List<Timeline> originalTimelines, EligibilityHistory mepeHist, decimal premiumAmount, List<string> excludedGroups, string subscriberIndicator)
        {
            DateTime reductionStart = new DateTime(2020, 4, 1);
            DateTime reductionEnd = new DateTime(2020, 5, 31);
            decimal premReductRate = .85M;
            string bCatType = "SmallGroup";
            List<Timeline> newTimelines = new List<Timeline>();
            foreach (Timeline timeline in originalTimelines)
            {
                //five situations
                //1. this timeline isn't for a subscriber or is completely outside these bounds, skip it 
                if (subscriberIndicator != "S" || timeline.Start > reductionEnd || timeline.End < reductionStart || mepeHist.BusCatType != bCatType || excludedGroups.Contains(mepeHist.GroupID))
                {
                    newTimelines.Add(timeline);
                    continue;
                }
                //2. this timeline contains these bounds completely
                if (timeline.Start < reductionStart && timeline.End > reductionEnd)
                {
                    newTimelines.Add(new Timeline() { Start = timeline.Start, End = reductionStart.AddDays(-1), PremiumAmount = premiumAmount, MaintenanceCode = timeline.MaintenanceCode });
                    newTimelines.Add(new Timeline() { Start = reductionStart, End = reductionEnd, PremiumAmount = (premiumAmount * premReductRate), MaintenanceCode = "001" });
                    newTimelines.Add(new Timeline() { Start = reductionEnd.AddDays(1), End = timeline.End, PremiumAmount = premiumAmount, MaintenanceCode = "001" });
                }
                //3. this timeline contains the start but not the end
                else if (timeline.Start >= reductionStart && timeline.End > reductionEnd)
                {
                    newTimelines.Add(new Timeline() { Start = timeline.Start, End = reductionEnd, PremiumAmount = (premiumAmount * premReductRate), MaintenanceCode = timeline.MaintenanceCode });
                    newTimelines.Add(new Timeline() { Start = reductionEnd.AddDays(1), End = timeline.End, PremiumAmount = premiumAmount, MaintenanceCode = "001" });
                }
                //4. this timeline contains the end but not the start
                else if (timeline.Start < reductionStart && timeline.End <= reductionEnd)
                {
                    newTimelines.Add(new Timeline() { Start = timeline.Start, End = reductionStart.AddDays(-1), PremiumAmount = premiumAmount, MaintenanceCode = timeline.MaintenanceCode });
                    newTimelines.Add(new Timeline() { Start = reductionStart, End = timeline.End, PremiumAmount = (premiumAmount * premReductRate), MaintenanceCode = "001" });
                }
                //5. this timeline is completely contained by these bounds
                else
                {
                    newTimelines.Add(new Timeline() { Start = timeline.Start, End = timeline.End, PremiumAmount = (premiumAmount * premReductRate), MaintenanceCode = timeline.MaintenanceCode });
                }
            }
            return newTimelines;
        }

        private class Timeline
        {
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
            public string MaintenanceCode { get; set; }
            public decimal PremiumAmount { get; set; }

        }

        private class PremiumOverride
        {
            public int InsuredMemberIdentifier { get; set; }
            public DateTime CoverageStartDate { get; set; }
            public DateTime CoverageEndDate { get; set; }
            public decimal OverridenPremium { get; set; }
            public int IssuerID { get; set; }
        }

        private class RatingAreaOverride
        {
            public int InsuredMemberIdentifier { get; set; }
            public DateTime CoverageStartDate { get; set; }
            public DateTime CoverageEndDate { get; set; }
            public string OverridenRatingArea { get; set; }
            public int IssuerID { get; set; }
        }

        private class PremiumReduction
        {
            public DateTime ReductionStart { get; set; }
            public DateTime ReductionEnd { get; set; }
            public decimal PremReductRate { get; set; }
            public string BusinessCategory { get; set; }
        }

        private static String getRateArea(String businessCategory, String smallGroupRateArea, String individualRateArea)
        {
            if (businessCategory == "SmallGroup")
            {
                return smallGroupRateArea;
            }
            else
            {
                return individualRateArea;
            }
        }

    }

}
