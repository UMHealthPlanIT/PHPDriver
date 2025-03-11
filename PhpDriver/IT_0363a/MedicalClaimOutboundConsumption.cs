using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using Utilities;
using System.Xml.Serialization;
using System.Xml;

namespace Driver.IT_0363a
{
    public class MedicalClaimOutboundConsumption
    {
        //EFPhpArchive.IT0363_EdgeReleaseSummary_A MedicalSummaryTable;
        //EFPhpArchive.IT0363_EdgeReleaseDetail_A MedicalDetailTable;

        bool MasterClassException = false;

        public static bool Main(IT_0363ACAEdgeReporting caller, String EDGEenvironment, List<String> Files)
        {

            List<IssuerPlanMedicalClaimDetail> MedicalDetail = new List<IssuerPlanMedicalClaimDetail>();
            List<FileProcessingResultStatus> MedicalHeader = new List<FileProcessingResultStatus>();
            List<MedicalClaimSummaryAcceptReject> MedicalSummary = new List<MedicalClaimSummaryAcceptReject>();

            caller.WriteToLog("Found files: " + Environment.NewLine + String.Join(Environment.NewLine, Files));


            List<string> DetailFileNames = new List<string>();
            List<string> HeaderFileNames = new List<string>();
            List<string> SummaryFileNames = new List<string>();
            caller.WriteToLog("Adding Files");
            foreach (string XMLFile in Files)
            {
                if (XMLFile.Contains("MD"))
                {
                    try
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(IssuerPlanMedicalClaimDetail));
                        IssuerPlanMedicalClaimDetail detail;
                        using (XmlReader reader = XmlReader.Create(XMLFile))
                        {
                            detail = (IssuerPlanMedicalClaimDetail)serializer.Deserialize(reader);
                        }
                        MedicalDetail.Add(detail);
                        //MedicalDetail.Add(XMLTranslations.Deseril<IssuerPlanMedicalClaimDetail>(XMLFile));
                        string fileshort = System.IO.Path.GetFileName(XMLFile);
                        DetailFileNames.Add(fileshort);
                    }
                    catch (Exception ex)
                    {
                        caller.WriteToLog($"We tried deserializing {XMLFile} but there was an issue.", UniversalLogger.LogCategory.ERROR);
                        caller.WriteToLog(ex.ToString(), UniversalLogger.LogCategory.ERROR);
                    }
                }
                else if (XMLFile.Contains("MH"))
                {
                    try
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(FileProcessingResultStatus));
                        FileProcessingResultStatus header;
                        using (XmlReader reader = XmlReader.Create(XMLFile))
                        {
                            header = (FileProcessingResultStatus)serializer.Deserialize(reader);
                        }
                        MedicalHeader.Add(header);
                        //MedicalHeader.Add(XMLTranslations.Deseril<FileProcessingResultStatus>(XMLFile));
                        string fileshort = System.IO.Path.GetFileName(XMLFile);
                        HeaderFileNames.Add(fileshort);
                    }
                    catch (Exception ex)
                    {
                        caller.WriteToLog($"We tried deserializing {XMLFile} but there was an issue.", UniversalLogger.LogCategory.ERROR);
                        caller.WriteToLog(ex.ToString(), UniversalLogger.LogCategory.ERROR);
                    }
                }
                else if (XMLFile.Contains("MS"))
                {
                    try
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(MedicalClaimSummaryAcceptReject));
                        MedicalClaimSummaryAcceptReject summary;
                        using (XmlReader reader = XmlReader.Create(XMLFile))
                        {
                            summary = (MedicalClaimSummaryAcceptReject)serializer.Deserialize(reader);
                        }
                        MedicalSummary.Add(summary);
                        //MedicalSummary.Add(XMLTranslations.Deseril<MedicalClaimSummaryAcceptReject>(XMLFile));
                        string fileshort = System.IO.Path.GetFileName(XMLFile);
                        SummaryFileNames.Add(fileshort);
                    }
                    catch (Exception ex)
                    {
                        caller.WriteToLog($"We tried deserializing {XMLFile} but there was an issue.", UniversalLogger.LogCategory.ERROR);
                        caller.WriteToLog(ex.ToString(), UniversalLogger.LogCategory.ERROR);
                    }
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
                foreach (IssuerPlanMedicalClaimDetail Detail in MedicalDetail)
                {
                    DetailCount += 1;
                    string spillageQuery = string.Format(@"DELETE FROM [dbo].[IT0363_EdgeReleaseDetail_A]
                    WHERE FileName='{0}'", DetailFileNames[DetailCount]);
                    DataTable PreviousData = ExtractFactory.ConnectAndQuery(caller.LoggerPhpArchive, spillageQuery);
                    foreach (MedicalClaimIssuerProcessingResult Issuer in Detail.includedIssuerProcessingResult)
                    {
                        if (Issuer.recordedError == null)
                        {
                            MedicalDetailIssuer(context, Detail, Issuer, DetailFileNames[DetailCount]);

                        }
                        else
                        {
                            foreach (ErrorMessageType ErrorMessage in Issuer.recordedError)
                            {
                                MedicalDetailIssuer(context, Detail, Issuer, DetailFileNames[DetailCount], ErrorMessage);
                            }
                        }
                        foreach (MedicalClaimPlanProcessingResult Plan in Issuer.includedPlanProcessingResult)
                        {
                            RecordCount++;
                            if (RecordCount % 1000 == 0)
                            {
                                context.SaveChanges();
                            }
                            if (Plan.recordedError == null)
                            {
                                MedicalDetailPlan(context, Detail, Plan, DetailFileNames[DetailCount]);
                            }
                            else
                            {
                                foreach (ErrorMessageType ErrorMessage in Plan.recordedError)
                                {
                                    MedicalDetailPlan(context, Detail, Plan, DetailFileNames[DetailCount], ErrorMessage);
                                }
                            }
                            foreach (MedicalClaimProcessingResult Claim in Plan.includedClaimProcessingResult)
                            {
                                RecordCount++;
                                if (RecordCount % 100 == 0)
                                {
                                    context.SaveChanges();
                                }
                                if (Claim.recordedError == null)
                                {
                                    MedicalDetailClaim(context, Detail, Plan, Claim, DetailFileNames[DetailCount]);
                                }
                                else
                                {
                                    foreach (ErrorMessageType ErrorMessage in Claim.recordedError)
                                    {
                                        MedicalDetailClaim(context, Detail, Plan, Claim, DetailFileNames[DetailCount], ErrorMessage);
                                    }
                                }
                                foreach (MedicalClaimServiceLineProcessingResult Line in Claim.includedClaimServiceLineProcessingResult)
                                {
                                    RecordCount++;
                                    if (RecordCount % 100 == 0)
                                    {
                                        context.SaveChanges();
                                    }
                                    if (Line.recordedError == null)
                                    {
                                        MedicalDetailLine(context, Detail, Plan, Line, Claim, DetailFileNames[DetailCount]);
                                    }
                                    else
                                    {
                                        foreach (ErrorMessageType ErrorMessage in Line.recordedError)
                                        {
                                            MedicalDetailLine(context, Detail, Plan, Line, Claim, DetailFileNames[DetailCount], ErrorMessage);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                caller.WriteToLog("Detail Table Updates Done.");
                #endregion

                #region SummaryTableUpdate
                caller.WriteToLog("Summary Table");
                SummaryCount = -1;
                foreach (MedicalClaimSummaryAcceptReject Summary in MedicalSummary)
                {
                    SummaryCount += 1;
                    string spillageQuery = string.Format(@"DELETE FROM [dbo].[IT0363_EdgeReleaseSummary_A]
                    WHERE FileName='{0}'", SummaryFileNames[SummaryCount]);
                    DataTable PreviousData = ExtractFactory.ConnectAndQuery(caller.LoggerPhpArchive, spillageQuery);
                    EFPhpArchive.IT0363_EdgeReleaseSummary_A MedicalSummaryTable = new EFPhpArchive.IT0363_EdgeReleaseSummary_A();
                    MedicalSummaryTable.FileType = "M";
                    MedicalSummaryTable.ErrorType = "";
                    MedicalSummaryTable.issuerID = Summary.includedFileHeader.issuerID;
                    MedicalSummaryTable.inboundFileIdentifier = Summary.includedFileHeader.inboundFileIdentifier;
                    MedicalSummaryTable.month = 0;
                    MedicalSummaryTable.year = 0;
                    MedicalSummaryTable.statusTypeCode = "A";
                    MedicalSummaryTable.recordType = "Total";
                    MedicalSummaryTable.recordsReceived = Convert.ToInt32(Summary.claimHeaderCounts.recordsReceived);
                    MedicalSummaryTable.recordsAccepted = Convert.ToInt32(Summary.claimHeaderCounts.recordsAccepted);
                    MedicalSummaryTable.recordsResolved = Convert.ToInt32(Summary.claimHeaderCounts.recordsResolved);
                    MedicalSummaryTable.recordsRejected = Convert.ToInt32(Summary.claimHeaderCounts.recordsRejected);
                    MedicalSummaryTable.newRecordsAccepted = Convert.ToInt32(Summary.claimHeaderCounts.newRecordsAccepted);
                    MedicalSummaryTable.FileName = SummaryFileNames[SummaryCount];
                    context.IT0363_EdgeReleaseSummary_A.Add(MedicalSummaryTable);
                    if (Summary.includedErrorCodeFrequency != null)
                    {
                        foreach (ErrorCodeCounts ErrorCode in Summary.includedErrorCodeFrequency) //Error Summary
                        {
                            MedicalSummaryTable = new EFPhpArchive.IT0363_EdgeReleaseSummary_A();
                            MedicalSummaryTable.FileType = "M";
                            MedicalSummaryTable.issuerID = Summary.includedFileHeader.issuerID;
                            MedicalSummaryTable.inboundFileIdentifier = Summary.includedFileHeader.inboundFileIdentifier;
                            MedicalSummaryTable.ErrorType = ErrorCode.offendingElementErrorTypeCode;
                            MedicalSummaryTable.month = 0;
                            MedicalSummaryTable.year = 0;
                            MedicalSummaryTable.statusTypeCode = "R";
                            MedicalSummaryTable.recordType = "Error";
                            MedicalSummaryTable.recordsReceived = 0;
                            MedicalSummaryTable.recordsAccepted = 0;
                            MedicalSummaryTable.recordsResolved = 0;
                            MedicalSummaryTable.recordsRejected = Convert.ToInt32(ErrorCode.offendingElementErrorTypeCodeFrequency);
                            MedicalSummaryTable.newRecordsAccepted = 0;
                            MedicalSummaryTable.FileName = SummaryFileNames[SummaryCount];
                            context.IT0363_EdgeReleaseSummary_A.Add(MedicalSummaryTable);
                            context.SaveChanges();
                        }
                    }
                    MedicalClaimIssuerSummary IssuerSummary = Summary.includedIssuerSummary;
                    foreach (MedicalClaimIssuerYear Year in IssuerSummary.includedIssuerYear) //Month Summary
                    {
                        foreach (MedicalClaimIssuerMonth Month in Year.includedIssuerMonth)
                        {
                            MedicalSummaryTable = new EFPhpArchive.IT0363_EdgeReleaseSummary_A();
                            MedicalSummaryTable.FileType = "M";
                            MedicalSummaryTable.ErrorType = "";
                            MedicalSummaryTable.issuerID = Summary.includedFileHeader.issuerID;
                            MedicalSummaryTable.inboundFileIdentifier = Summary.includedFileHeader.inboundFileIdentifier;
                            MedicalSummaryTable.month = Convert.ToInt32(Month.issuerMonth); //Change To Month
                            MedicalSummaryTable.year = Convert.ToInt32(Year.issuerYear); //Change To Month
                            MedicalSummaryTable.statusTypeCode = IssuerSummary.classifyingProcessingStatusType.statusTypeCode;
                            MedicalSummaryTable.recordType = "Header";
                            int recordsRecivedTotal = Convert.ToInt32(Month.includedIssuerFormType[0].includedIssuerClaimCounts.recordsReceived);
                            int recordsAcceptedTotal = Convert.ToInt32(Month.includedIssuerFormType[0].includedIssuerClaimCounts.recordsAccepted);
                            int recordsResolvedTotal = Convert.ToInt32(Month.includedIssuerFormType[0].includedIssuerClaimCounts.recordsResolved);
                            int recordsRejectedTotal = Convert.ToInt32(Month.includedIssuerFormType[0].includedIssuerClaimCounts.recordsRejected);
                            int newRecordsAcceptedTotal = Convert.ToInt32(Month.includedIssuerFormType[0].includedIssuerClaimCounts.newRecordsAccepted); //P Form Type
                            if (Month.includedIssuerFormType.Count() == 2)
                            {
                                recordsRecivedTotal += Convert.ToInt32(Month.includedIssuerFormType[1].includedIssuerClaimCounts.recordsReceived);
                                recordsAcceptedTotal += Convert.ToInt32(Month.includedIssuerFormType[1].includedIssuerClaimCounts.recordsAccepted);
                                recordsResolvedTotal += Convert.ToInt32(Month.includedIssuerFormType[1].includedIssuerClaimCounts.recordsResolved);
                                recordsRejectedTotal += Convert.ToInt32(Month.includedIssuerFormType[1].includedIssuerClaimCounts.recordsRejected);
                                newRecordsAcceptedTotal += Convert.ToInt32(Month.includedIssuerFormType[1].includedIssuerClaimCounts.newRecordsAccepted); //I Form Type
                            }
                            MedicalSummaryTable.recordsReceived = recordsRecivedTotal;
                            MedicalSummaryTable.recordsAccepted = recordsAcceptedTotal;
                            MedicalSummaryTable.recordsResolved = recordsResolvedTotal;
                            MedicalSummaryTable.recordsRejected = recordsRejectedTotal;
                            MedicalSummaryTable.newRecordsAccepted = newRecordsAcceptedTotal;
                            MedicalSummaryTable.FileName = SummaryFileNames[SummaryCount];
                            context.IT0363_EdgeReleaseSummary_A.Add(MedicalSummaryTable);
                        }
                    }

                }
                caller.WriteToLog("Summary Table Done");
                #endregion
                context.SaveChanges();
            }

            caller.WriteToLog("All Files Done");
            return true;


        }

        private static void MedicalDetailLine(EFPhpArchive.PHPArchvEntities context, IssuerPlanMedicalClaimDetail Detail, MedicalClaimPlanProcessingResult Plan, MedicalClaimServiceLineProcessingResult Line, MedicalClaimProcessingResult Claim, string filename, ErrorMessageType ErrorMessage = null)
        {
            EFPhpArchive.IT0363_EdgeReleaseDetail_A MedicalDetailTable = new EFPhpArchive.IT0363_EdgeReleaseDetail_A();
            MedicalDetailTable.FileType = "M";
            MedicalDetailTable.issuerID = Detail.includedFileHeader.issuerID;
            MedicalDetailTable.inboundFileIdentifier = Detail.includedFileHeader.inboundFileIdentifier;
            MedicalDetailTable.recordID = Convert.ToInt32(Line.medicalClaimServiceLineRecordIdentifier);
            MedicalDetailTable.Identifier = "";
            MedicalDetailTable.statusTypeCode = Line.classifyingProcessingStatusType.statusTypeCode;
            MedicalDetailTable.Identifier = Claim.medicalClaimIdentifier;
            MedicalDetailTable.recordType = "Line";
            MedicalDetailTable.FileName = filename;
            MedicalDetailTable.insurancePlanIdentifier = Plan.insurancePlanIdentifier;
            ErrorCheck(MedicalDetailTable, ErrorMessage);
            context.IT0363_EdgeReleaseDetail_A.Add(MedicalDetailTable);
        }
        private static void MedicalDetailClaim(EFPhpArchive.PHPArchvEntities context, IssuerPlanMedicalClaimDetail Detail, MedicalClaimPlanProcessingResult Plan, MedicalClaimProcessingResult Claim, string filename, ErrorMessageType ErrorMessage = null)
        {
            EFPhpArchive.IT0363_EdgeReleaseDetail_A MedicalDetailTable = new EFPhpArchive.IT0363_EdgeReleaseDetail_A();
            MedicalDetailTable.FileType = "M";
            MedicalDetailTable.issuerID = Detail.includedFileHeader.issuerID;
            MedicalDetailTable.inboundFileIdentifier = Detail.includedFileHeader.inboundFileIdentifier;
            MedicalDetailTable.recordID = Convert.ToInt32(Claim.medicalClaimRecordIdentifier);
            MedicalDetailTable.statusTypeCode = Claim.classifyingProcessingStatusType.statusTypeCode;
            MedicalDetailTable.Identifier = Claim.medicalClaimIdentifier;
            MedicalDetailTable.recordType = "Claim";
            MedicalDetailTable.FileName = filename;
            MedicalDetailTable.insurancePlanIdentifier = Plan.insurancePlanIdentifier;
            ErrorCheck(MedicalDetailTable, ErrorMessage);
            context.IT0363_EdgeReleaseDetail_A.Add(MedicalDetailTable);
        }

        private static void MedicalDetailPlan(EFPhpArchive.PHPArchvEntities context, IssuerPlanMedicalClaimDetail Detail, MedicalClaimPlanProcessingResult Plan, string filename, ErrorMessageType ErrorMessage = null)
        {
            EFPhpArchive.IT0363_EdgeReleaseDetail_A MedicalDetailTable = new EFPhpArchive.IT0363_EdgeReleaseDetail_A();
            MedicalDetailTable.FileType = "M";
            MedicalDetailTable.issuerID = Detail.includedFileHeader.issuerID;
            MedicalDetailTable.inboundFileIdentifier = Detail.includedFileHeader.inboundFileIdentifier;
            MedicalDetailTable.recordID = Convert.ToInt32(Plan.planRecordIdentifier);
            MedicalDetailTable.Identifier = Plan.insurancePlanIdentifier;
            MedicalDetailTable.statusTypeCode = Plan.classifyingProcessingStatusType.statusTypeCode;
            MedicalDetailTable.recordType = "Plan";
            MedicalDetailTable.FileName = filename;
            MedicalDetailTable.insurancePlanIdentifier = Plan.insurancePlanIdentifier;
            ErrorCheck(MedicalDetailTable, ErrorMessage);
            context.IT0363_EdgeReleaseDetail_A.Add(MedicalDetailTable);
        }

        private static void MedicalDetailIssuer(EFPhpArchive.PHPArchvEntities context, IssuerPlanMedicalClaimDetail Detail, MedicalClaimIssuerProcessingResult Issuer, string filename, ErrorMessageType ErrorMessage = null)
        {
            EFPhpArchive.IT0363_EdgeReleaseDetail_A MedicalDetailTable = new EFPhpArchive.IT0363_EdgeReleaseDetail_A();
            MedicalDetailTable.FileType = "M";
            MedicalDetailTable.issuerID = Detail.includedFileHeader.issuerID;
            MedicalDetailTable.inboundFileIdentifier = Detail.includedFileHeader.inboundFileIdentifier;
            MedicalDetailTable.recordID = Convert.ToInt32(Issuer.issuerRecordIdentifier);
            MedicalDetailTable.Identifier = Issuer.issuerIdentifier;
            MedicalDetailTable.statusTypeCode = Issuer.classifyingProcessingStatusType.statusTypeCode;
            MedicalDetailTable.recordType = "Issuer";
            MedicalDetailTable.FileName = filename;
            MedicalDetailTable.insurancePlanIdentifier = "";
            ErrorCheck(MedicalDetailTable, ErrorMessage);
            context.IT0363_EdgeReleaseDetail_A.Add(MedicalDetailTable);
        }
        private static void ErrorCheck(EFPhpArchive.IT0363_EdgeReleaseDetail_A MedicalDetailTable, ErrorMessageType ErrorMessage = null)
        {
            if (ErrorMessage == null || ErrorMessage.offendingElementValue == null)
            {
                MedicalDetailTable.ErrorElementValue = "";
            }
            else
            {

                MedicalDetailTable.ErrorElementValue = string.Join(",", ErrorMessage.offendingElementValue);
            }
            if (ErrorMessage == null || ErrorMessage.offendingElementName == null)
            {
                MedicalDetailTable.ErrorName = "";
            }
            else
            {

                MedicalDetailTable.ErrorName = string.Join(",", ErrorMessage.offendingElementName);
            }

            if (ErrorMessage == null || ErrorMessage.offendingElementErrorTypeMessage == null)
            {
                MedicalDetailTable.ErrorMessage = "";
            }
            else
            {

                MedicalDetailTable.ErrorMessage = string.Join(",", ErrorMessage.offendingElementErrorTypeMessage);
            }
            if (ErrorMessage == null || ErrorMessage.offendingElementErrorTypeCode == null)
            {
                MedicalDetailTable.ErrorTypeCode = "";
            }
            else
            {
                MedicalDetailTable.ErrorTypeCode = string.Join(",", ErrorMessage.offendingElementErrorTypeCode);
            }
            if (ErrorMessage == null || ErrorMessage.offendingElementErrorTypeDetail == null)
            {
                MedicalDetailTable.ErrorTypeDetail = "";
            }
            else
            {
                MedicalDetailTable.ErrorTypeDetail = string.Join(",", ErrorMessage.offendingElementErrorTypeDetail);
            }
        }
    }
}
