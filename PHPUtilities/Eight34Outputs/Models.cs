using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Linq.Mapping;
using System.Globalization;

namespace Utilities.Eight34Outputs
{
    [InheritanceMapping(Type = typeof(ExchangeOutputRecord), Code = "PHPConfg")]
    [InheritanceMapping(Type = typeof(OutputRecord), IsDefault = true, Code = "")]
    [Table]
    /// <summary>
    /// Core source record for the 834 processor
    /// </summary>
    public class OutputRecord
    {
        [Column(IsDiscriminator = true, Name = "DBName")]
        public string DBName { get; set; }
        [Column]
        public string LastName { get; set; }
        [Column]
        public string FirstName { get; set; }
        [Column]
        public string MidInit { get; set; }
        [Column]
        public string NamePrefix { get; set; }
        [Column]
        public string NameSuffix { get; set; }
        [Column]
        public string ShortName { get; set; }
        [Column]
        public string MemberRecordNumber { get; set; }
        [Column]
        public string SubscriberFlag { get; set; }
        [Column]
        public string Relationship { get; set; }
        [Column]
        public string MaintenanceCode { get; set; }
        [Column]
        public string MaintenanceRSN { get; set; }
        [Column]
        public string MaintenanceDate { get; set; }
        [Column]
        public string OriginalEffectiveDate { get; set; }
        [Column]
        public string EarliestEffDate { get; set; }
        [Column]
        public string CovEffDate { get; set; }
        [Column]
        public string LatestInternalTermDate { get; set; }
        [Column]
        public string CovEndDate { get; set; }
        [Column]
        public string EnrollElig { get; set; }
        [Column]
        public string EnrollTerm { get; set; }
        [Column]
        public string Retro { get; set; }
        [Column]
        public string COBRAStart { get; set; }
        [Column]
        public string COBRAEnd { get; set; }
        [Column]
        public string COBRALastPremPdDate { get; set; }
        [Column]
        public string PlanCoverageDesc { get; set; }
        [Column]
        public string IDCardType { get; set; }
        [Column]
        public string IDActionCD { get; set; }
        [Column]
        public string PopulationSET { get; set; }
        [Column]
        public string BenefitStatus { get; set; }
        [Column]
        public string MedicarePlanCode { get; set; }
        [Column]
        public string EmployStatusCD { get; set; }
        [Column]
        public string EmployeeID { get; set; }
        [Column]
        public string HandicapInd { get; set; }
        [Column]
        public string DeathDate { get; set; }
        [Column]
        public string BirthSequence { get; set; }
        [Column]
        public string MaritalStatus { get; set; }
        [Column]
        public string InputRace { get; set; }
        [Column]
        public string InputEthnicity { get; set; }
        [Column]
        public string Race { get; set; }
        [Column]
        public string Ethnicity { get; set; }
        [Column]
        public string SubNo { get; set; }
        [Column]
        public string GroupNo { get; set; }
        [Column]
        public string SubGroup { get; set; }
        [Column]
        public bool SubGroupOut { get; set; }
        [Column]
        public string ClassCode { get; set; }
        [Column]
        public bool ClassCodeOut { get; set; }
        [Column]
        public bool TermElig { get; set; }
        [Column]
        public string RateCell { get; set; }
        [Column]
        public string CaseNumber { get; set; }
        [Column]
        public string FosterCare { get; set; }
        [Column]
        public string PriorCoveredMonths { get; set; }
        [Column]
        public string HealthCovMaintType { get; set; }
        [Column]
        public string InsPlanDesc { get; set; }
        [Column]
        public string InsLineCode { get; set; }
        [Column]
        public string CoverageLevel { get; set; }
        [Column]
        public string CoverageLevelOut { get; set; }
        [Column]
        public string SSN { get; set; }
        [Column]
        public string AddressOne { get; set; }
        [Column]
        public string AddressTwo { get; set; }
        [Column]
        public string City { get; set; }
        [Column]
        public string State { get; set; }
        [Column]
        public string County { get; set; }
        [Column]
        public string Zip { get; set; }
        [Column]
        public string Telephone { get; set; }
        [Column]
        public string MailingAddressOne { get; set; }
        [Column]
        public string MailingAddressTwo { get; set; }
        [Column]
        public string MailingCity { get; set; }
        [Column]
        public string MailingState { get; set; }
        [Column]
        public string MailingZip { get; set; }
        [Column]
        public string MailingAddressType { get; set; }
        [Column]
        public string Gender { get; set; }
        [Column]
        public string DOB { get; set; }
        [Column]
        public string MemDep { get; set; }
        [Column]
        public string RespFirstName { get; set; }
        [Column]
        public string RespLastName { get; set; }
        [Column]
        public string RespAddress1 { get; set; }
        [Column]
        public string RespAddress2 { get; set; }
        [Column]
        public string RespCity { get; set; }
        [Column]
        public string RespState { get; set; }
        [Column]
        public string RespZip { get; set; }
        [Column]
        public string RespTelephone { get; set; }
        [Column]
        public string COBBegDate { get; set; }
        [Column]
        public string COBEndDate { get; set; }
        [Column]
        public string COBSeqNo { get; set; }
        [Column]
        public string COBPolicy { get; set; }
        [Column]
        public string COBFlag { get; set; }
        [Column]
        public string COBEmpID { get; set; }
        [Column]
        public string COBGroupID { get; set; }
        [Column]
        public string COBGroupSuff { get; set; }
        [Column]
        public string COBInsurer { get; set; }
        [Column]
        public string COBInsurerCodeQual { get; set; }
        [Column]
        public string COBIdentificationCode { get; set; }
        [Column]
        public string COBBegDate1 { get; set; }
        [Column]
        public string COBEndDate1 { get; set; }
        [Column]
        public string COBSeqNo1 { get; set; }
        [Column]
        public string COBPolicy1 { get; set; }
        [Column]
        public string COBFlag1 { get; set; }
        [Column]
        public string COBEmpID1 { get; set; }
        [Column]
        public string COBGroupID1 { get; set; }
        [Column]
        public string COBGroupSuff1 { get; set; }
        [Column]
        public string COBInsurer1 { get; set; }
        [Column]
        public string COBInsurerCodeQual1 { get; set; }
        [Column]
        public string COBIdentificationCode1 { get; set; }
        [Column]
        public string COBBegDate2 { get; set; }
        [Column]
        public string COBEndDate2 { get; set; }
        [Column]
        public string COBSeqNo2 { get; set; }
        [Column]
        public string COBPolicy2 { get; set; }
        [Column]
        public string COBFlag2 { get; set; }
        [Column]
        public string COBEmpID2 { get; set; }
        [Column]
        public string COBGroupID2 { get; set; }
        [Column]
        public string COBGroupSuff2 { get; set; }
        [Column]
        public string COBInsurer2 { get; set; }
        [Column]
        public string COBInsurerCodeQual2 { get; set; }
        [Column]
        public string COBIdentificationCode2 { get; set; }
        [Column]
        public string COBBegDate3 { get; set; }
        [Column]
        public string COBEndDate3 { get; set; }
        [Column]
        public string COBSeqNo3 { get; set; }
        [Column]
        public string COBPolicy3 { get; set; }
        [Column]
        public string COBFlag3 { get; set; }
        [Column]
        public string COBEmpID3 { get; set; }
        [Column]
        public string COBGroupID3 { get; set; }
        [Column]
        public string COBGroupSuff3 { get; set; }
        [Column]
        public string COBInsurer3 { get; set; }
        [Column]
        public string COBInsurerCodeQual3 { get; set; }
        [Column]
        public string COBIdentificationCode3 { get; set; }
        [Column]
        public string COBBegDate4 { get; set; }
        [Column]
        public string COBEndDate4 { get; set; }
        [Column]
        public string COBSeqNo4 { get; set; }
        [Column]
        public string COBPolicy4 { get; set; }
        [Column]
        public string COBFlag4 { get; set; }
        [Column]
        public string COBEmpID4 { get; set; }
        [Column]
        public string COBGroupID4 { get; set; }
        [Column]
        public string COBGroupSuff4 { get; set; }
        [Column]
        public string COBInsurer4 { get; set; }
        [Column]
        public string COBInsurerCodeQual4 { get; set; }
        [Column]
        public string COBIdentificationCode4 { get; set; }
        [Column]
        public string COBBegDate5 { get; set; }
        [Column]
        public string COBEndDate5 { get; set; }
        [Column]
        public string COBSeqNo5 { get; set; }
        [Column]
        public string COBPolicy5 { get; set; }
        [Column]
        public string COBFlag5 { get; set; }
        [Column]
        public string COBEmpID5 { get; set; }
        [Column]
        public string COBGroupID5 { get; set; }
        [Column]
        public string COBGroupSuff5 { get; set; }
        [Column]
        public string COBInsurer5 { get; set; }
        [Column]
        public string COBInsurerCodeQual5 { get; set; }
        [Column]
        public string COBIdentificationCode5 { get; set; }
        [Column]
        public string SubscriberID { get; set; }
        [Column]
        public string SubscriberHomeAddressType { get; set; }
        [Column]
        public string SubscriberMailingAddressType { get; set; }
        [Column]
        public string MemberRelationship { get; set; }
        [Column]
        public int SubStatus { get; set; }
        [Column]
        public string ProviderID { get; set; }
        [Column]
        public string ClassPlanID { get; set; }
        [Column]
        public string ClassPlanPharmID { get; set; }
        [Column]
        public string FileDate { get; set; }
        [Column]
        public string FileName { get; set; }
        [Column]
        public string FileTransactionSet { get; set; }
        [Column]
        public string ReferenceID { get; set; }
        [Column]
        public string FileType { get; set; }
        [Column]
        public string FileTypeDesc { get; set; }
        [Column]
        public bool Output { get; set; }
        [Column]
        public bool SubscriberTransactions { get; set; }
        [Column]
        public bool MemberTransactions { get; set; }
        [Column]
        public string Action { get; set; }
        [Column]
        public string EligAction { get; set; }
        [Column]
        public string EligType { get; set; }
        [Column]
        public string ErrCode { get; set; }
        [Column]
        public int UniqueKey { get; set; }
        [Column]
        public string ErrDescription { get; set; }
        [Column]
        public bool MEHDOut { get; set; }
        [Column]
        public bool OOADeps { get; set; }
        [Column]
        public bool WouldHaveOutput { get; set; }
        [Column]
        public string MedicareBegin338 { get; set; }
        [Column]
        public string MedicareEnd339 { get; set; }
        [Column]
        public string EGWPEligDt { get; set; }
        [Column]
        public string MedicareBeneficiaryIndicator { get; set; }
    }

    public class ExchangeOutputRecord : OutputRecord
    {
        [Column]
        public string AptcIndicator { get; set; }
        [Column]
        public string HIOS { get; set; }
        [Column]
        public string PolicyNBR { get; set; }
        [Column]
        public string MemberNBR { get; set; }
        [Column]
        public string AptcAmount { get; set; }
        [Column]
        public string CsrAmount { get; set; }
        [Column]
        public string MemberSmokingIndicator { get; set; }
        [Column]
        public string SubscriberSmokingIndicator { get; set; }
        [Column]
        public string SubscriberRatingArea { get; set; }
        [Column]
        public string EmailAddress { get; set; }
        [Column]
        public string Fax { get; set; }
        [Column]
        public string Language { get; set; }
        [Column] 
        public string ChangeEffDt { get; set; }
        [Column]
        public string SmokingIndicator { get; set; }
        [Column]
        public string TotalPremium { get; set; }
        [Column]
        public string PremiumAmount { get; set; }
        [Column]
        public string TotalResponsible { get; set; }
        [Column]
        public string SubGroupEffDt { get; set; }
        [Column]
        public string ClassEffDt { get; set; }
        [Column]
        public string PCPName { get; set; }
        [Column]
        public string PCPAddressOne { get; set; }
        [Column]
        public string PCPAddressTwo { get; set; }
        [Column]
        public string PCPCity { get; set; }
        [Column]
        public string PCPState { get; set; }
        [Column]
        public string PCPZip { get; set; }
    }


}
