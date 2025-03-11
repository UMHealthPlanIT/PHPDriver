using Microsoft.Win32;

using Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Linq.Mapping;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Driver
{
    class DA02656ProviderIncentiveProgram : Logger, IPhp
    {
        public DA02656ProviderIncentiveProgram(LaunchRequest Program) : base(Program) {}

        public bool Initialize(string[] args)
        {
            if(DateTime.Today.Month == 2 && !args.Any(x => x.ToUpper() == "RUN"))
            {
                SendAlerts.Send(ProcessId, 4, "PIP Not Running", "PIP is set to not run in February by default. This can be overridden by having IT run it with the parameter \"RUN\"", this);
                return false;
            }

            DateTime reportDate;
            string yearMonth;

            bool managementReportOnly = args.Any(x => x.ToUpper() == "MNG");
            bool buildAndDontSendReportsOnly = args.Any(x => x.ToUpper() == "RPT");
            bool buildAndSendReportsOnly = args.Any(x => x.ToUpper() == "RPTSND");
            bool dataOnly = args.Any(x => x.ToUpper() == "DAT");
            bool annualRunFlag = args.Any(x => x.ToUpper() == "A");
            reportDate = args.Any(x => x.ToUpper().Contains("RPTDT:")) ? DateTime.Parse(args.Where(x => x.ToUpper().Contains("RPTDT:")).First().Substring(6)) : DateTime.Today;//yyyy-MM-dd
            yearMonth = args.Any(x => x.ToUpper().Contains("YEARMTH:")) ? args.Where(x => x.ToUpper().Contains("YEARMTH:")).First().Substring(8) 
                : annualRunFlag ? reportDate.AddYears(-1).ToString("yyyy") : reportDate.AddMonths(-1).ToString("yyyyMM");

            bool fullRun = !managementReportOnly && !buildAndSendReportsOnly && !dataOnly && !buildAndDontSendReportsOnly;

            int intYear = int.Parse(yearMonth.Substring(0, 4));
            int intMonth = annualRunFlag ? 0 : int.Parse(yearMonth.Substring(4, 2));

            string year = intYear.ToString();
            DateTime firstOfYear = new DateTime(int.Parse(year), 1, 1);
            DateTime lastOfYear = new DateTime(int.Parse(year), 12, 31);
            List<string> provMeasures = ExtractFactory.ConnectAndQuery<string>(this, LoggerPhpConfig, $"SELECT DISTINCT MeasureCode FROM dbo.DA02656_HEDISMeasureMapping_C WHERE MeasureYear = {year}").ToList();
            List<string> memMeasures = ExtractFactory.ConnectAndQuery<string>(this, LoggerPhpConfig, $"SELECT DISTINCT MeasureCode FROM dbo.DA02656_HEDISMeasureMapping_C WHERE MeasureYear = {year}").ToList();
            string memberTable = $"dbo.DA02656_Member_A_{year}";
            string providerTable = $"dbo.DA02656_Provider_A_{year}";



            if (dataOnly || fullRun)
            {
                DataTable dataCheck = ExtractFactory.ConnectAndQuery(this, LoggerPhpArchive, string.Format(@"SELECT TOP(1) *
              FROM[PHPArchv].[dbo].[DA02670_HEDIS_{2}_A]
              WHERE year{3} = '{0}'
				--AND REVIEWSETID = 'MY{1}'
                AND PRODUCT_ROLLUP_ID IN ('1', '7')", yearMonth, year, annualRunFlag ? "Annual_Production_Details" : "Extract_Report", annualRunFlag ? "" : "Month"));
                if (dataCheck.Rows.Count == 0)
                {
                    WriteToLog(string.Format("DA02670_HEDIS_{0}_A has no data for this run. PIP Cannot run until DA02670 has processed a file from Cotivity", annualRunFlag ? "Annual_Production_Details" : "Extract_Report"), UniversalLogger.LogCategory.ERROR);
                    return false;
                }
                else
                {
                    dataCheck.Dispose();
                }

                // Previous run's records should no longer be "active"
                DataWork.RunSqlCommand(this, $@"
	            UPDATE {providerTable}
	            SET IsActive = 0
	            WHERE
		            IsActive = 1;
	            UPDATE {memberTable}
	            SET IsActive = 0
	            WHERE
		            IsActive = 1;", LoggerPhpArchive);      

                List<ProviderMatcher.Provider> providers = ProviderMatcher.loadProviders(reportDate, year, annualRunFlag, firstOfYear, lastOfYear, this, provMeasures, providerTable);

                memMeasures.Add("WCCP");
                memMeasures.Add("WCCN");
                memMeasures.Add("W30E");
                memMeasures.Add("W30L");

                LoadMembers(reportDate, year, annualRunFlag, firstOfYear, lastOfYear, ref providers, memMeasures);

                LoadHedisClaims(ref providers, yearMonth, year, annualRunFlag);

                CalculateHedisMeasures(ref providers, year, annualRunFlag);

                CalculatePayouts(ref providers, year);

                SaveProvidersMembersAndClaims(providers, memMeasures, provMeasures, providerTable, memberTable);
            }

            if(fullRun || buildAndSendReportsOnly || managementReportOnly || buildAndDontSendReportsOnly)
            {
                GenerateReports(intYear, intMonth, annualRunFlag, managementReportOnly, buildAndDontSendReportsOnly, providerTable);
            }

            return true;

        }

        private void CalculateHedisMeasures(ref List<ProviderMatcher.Provider> providers, string year, bool annualRunFlag)
        {
            WriteToLog("Beginning HEDIS Measure Calculation");
            DataTable measureMapping = ExtractFactory.ConnectAndQuery(this, LoggerPhpConfig, string.Format("SELECT * FROM DA02656_HEDISMeasureMapping_C where MeasureYear = {0}", year));
            try
            {
                Parallel.ForEach(providers.AsEnumerable(), provider =>
                {
                    string measTypeSelector = provider.ProviderSpec.ToUpper() == "PEDS" || provider.ProviderSpec.ToUpper() == "PDIM" ? "PEDIATRIC" : "ADULT";
                    foreach (ProviderMatcher.Member member in provider.Members)
                    {
                        
                        foreach (DataRow measure in measureMapping.Select(string.Format("MeasureType = '{0}'", measTypeSelector)))
                        {
                            foreach (ProviderMatcher.Claim claim in member.Claims)
                            {
                                if (claim.MEASURE_ABBR.ToUpper() == measure["MeasureCode"].ToString().ToUpper() && (claim.NUM.ToUpper() == measure["SubMeasure"].ToString().ToUpper() || string.IsNullOrWhiteSpace(measure["SubMeasure"].ToString())))
                                {
                                    switch (claim.MEASURE_ABBR.ToUpper())
                                    {
                                        case "WCC":
                                            if (claim.NUM.ToUpper() == "COUNSELING FOR NUTRITION")
                                            {
                                                member.has_Denominator["WCCN"] = claim.DENOMINATOR;
                                                if (member.has_Numerator["WCCN"] == 0)
                                                {
                                                    member.has_Numerator["WCCN"] = claim.NUMERATOR;
                                                }
                                            }
                                            else if (claim.NUM.ToUpper() == "COUNSELING FOR PHYSICAL ACTIVITY")
                                            {
                                                member.has_Denominator["WCCP"] = claim.DENOMINATOR;
                                                if (member.has_Numerator["WCCP"] == 0)
                                                {
                                                    member.has_Numerator["WCCP"] = claim.NUMERATOR;
                                                }
                                            }
                                            break;
                                        case "WCV":
                                            if (!member.Claims.Any(x => x.MEASURE_ABBR.ToUpper() == "W30" && x.NUM.ToUpper() == "Well Child Visits for Age 15 Months - 30 Months".ToUpper() && x.DENOMINATOR == 1))
                                            {
                                                member.has_Denominator[claim.MEASURE_ABBR.ToUpper()] = claim.DENOMINATOR;
                                                if (member.has_Numerator[claim.MEASURE_ABBR.ToUpper()] == 0)
                                                {
                                                    member.has_Numerator[claim.MEASURE_ABBR.ToUpper()] = claim.NUMERATOR;
                                                }
                                            }
                                            else
                                            {
                                                WriteToLog($"Member MemberKey:{member.MemberKey}, MEM_NBR:{member.MEM_NBR} not given WCV Credit for {claim.CLAIM_ID} on DOS {claim.SERV_DT} due to at least one record falling under W30 on that given date",
                                                    UniversalLogger.LogCategory.AUDIT);
                                            }
                                            
                                            break;
                                        case "W30":                                            
                                            if (claim.NUM.ToUpper() == "Well Child Visits for Age 15 Months - 30 Months".ToUpper())
                                            {
                                                member.has_Denominator["W30L"] = claim.DENOMINATOR;
                                                if (member.has_Numerator["W30L"] == 0)
                                                {
                                                    member.has_Numerator["W30L"] = claim.NUMERATOR;
                                                }
                                            }
                                            else if (claim.NUM.ToUpper() == "Well Child Visits in the First 15 Months".ToUpper())
                                            {
                                                member.has_Denominator["W30E"] = claim.DENOMINATOR;
                                                if (member.has_Numerator["W30E"] == 0)
                                                {
                                                    member.has_Numerator["W30E"] = claim.NUMERATOR;
                                                }
                                            }                                            
                                            break;
                                        default:
                                            member.has_Denominator[claim.MEASURE_ABBR.ToUpper()] = claim.DENOMINATOR;
                                            if (member.has_Numerator[claim.MEASURE_ABBR.ToUpper()] == 0)
                                            {
                                                member.has_Numerator[claim.MEASURE_ABBR.ToUpper()] = claim.NUMERATOR;
                                            }
                                            break;
                                    }
                                }
                            }
                        }
                        if(member.has_Denominator["WCCN"] != 0 || member.has_Denominator["WCCP"] != 0)
                        {
                            member.has_Denominator["WCC"] = 1;
                            if(member.has_Numerator["WCCN"] != 0 && member.has_Numerator["WCCP"] != 0)
                            {
                                member.has_Numerator["WCC"] = 1;
                            }
                        }
                        if (member.has_Denominator["W30L"] != 0)
                        {
                            member.has_Denominator["W30"] = member.has_Denominator["W30L"];
                            member.has_Numerator["W30"] = member.has_Numerator["W30L"];
                        }
                        else if (member.has_Denominator["W30E"] != 0)
                        {
                            member.has_Denominator["W30"] = member.has_Denominator["W30E"];
                            member.has_Numerator["W30"] = member.has_Numerator["W30E"];
                        }
                        
                    }

                });
            }
            catch (Exception ex)
            {
                throw (ex.InnerException);
            }

            WriteToLog("HEDIS Measure Calculation Complete");
        }

        private void CalculatePayouts(ref List<ProviderMatcher.Provider> providers, string year)
        {
            WriteToLog("Beginning Payout Calculation");
            DataTable measureMapping = ExtractFactory.ConnectAndQuery(this, LoggerPhpConfig, string.Format("SELECT * FROM DA02656_HEDISMeasureMapping_C where MeasureYear = {0}", year));
            DataTable measureCalc = ExtractFactory.ConnectAndQuery(this, LoggerPhpConfig, string.Format("SELECT * FROM DA02656_DimMeasure_C where MeasureYear = {0} and IsActive = 1", year));
            DataTable rewardCalc = ExtractFactory.ConnectAndQuery(this, LoggerPhpConfig, string.Format("SELECT * FROM DA02656_DimRewardBracket_C where RewardYear = {0} and IsActive = 1", year));
            try
            {
                Parallel.ForEach(providers.AsEnumerable(), provider =>
                {
                    string measTypeSelector = provider.ProviderSpec.ToUpper() == "PEDS" || provider.ProviderSpec.ToUpper() == "PDIM" ? "PEDIATRIC" : "ADULT";

                    foreach (DataRow measure in measureMapping.Select(string.Format("MeasureType = '{0}'", measTypeSelector)))
                    {
                        decimal percent;
                        string measureCode = measure["MeasureCode"].ToString().ToUpper();
                        string submeasureCode = measure["SubMeasure"].ToString();
                        switch (measureCode)
                        {
                            default:
                                provider.Total_Records[measureCode] = provider.Members.Select(x => x.has_Denominator[measureCode]).Sum();
                                provider.Eligible_Records[measureCode] = provider.Members.Select(x => x.has_Numerator[measureCode]).Sum();
                                if (provider.Total_Records[measureCode] > 0)
                                {
                                    percent = Decimal.Round((decimal)provider.Eligible_Records[measureCode] / (decimal)provider.Total_Records[measureCode], 4);
                                }
                                else
                                {
                                    percent = 0;
                                }
                                DataRow[] value = measureCalc.Select(string.Format("MeasureDataKey = '{0}' and SubMeasure = '{1}' and {2} >= MinValue and {2} <= MaxValue"
                                    , measureCode, submeasureCode, percent));
                                provider._MeasureKey[measureCode] = int.Parse(value[0]["MeasureKey"].ToString());
                                provider._Points[measureCode] = int.Parse(value[0]["Points"].ToString());
                                break;
                        }
                    }

                    foreach(KeyValuePair<string, int> measure in provider.Total_Records)
                    {
                        if(measure.Value > 0)
                        {
                            provider.TotalMeasuresApplicable += 1;
                            if (provider._Points[measure.Key] > 0)
                            {
                                provider.TotalPoints += provider._Points[measure.Key];
                                provider.TotalMeasuresMet += 1;
                            }
                        }
                    }

                    if (provider.TotalMeasuresApplicable > 0)
                    {
                        provider.CompositeScore = (double)provider.TotalPoints / (double)provider.TotalMeasuresApplicable;
                    }
                    else
                    {
                        provider.CompositeScore = 0;
                    }

                    if (provider.TotalMeasuresMet >= 2)
                    {
                        DataRow[] value = rewardCalc.Select(string.Format("{0} >= MinValue and {0} <= MaxValue", provider.CompositeScore));
                        provider.RewardBracketKey = int.Parse(value[0]["RewardBracketKey"].ToString());
                        provider.PMPMAmount = double.Parse(value[0]["Reward"].ToString());
                        provider.TotalReward = (double)provider.TotalMembers * provider.PMPMAmount * 12;
                    }

                });
            }
            catch (Exception ex)
            {
                throw (ex.InnerException);
            }

            WriteToLog("Payout Calculation Complete");
        }

        private void SaveProvidersMembersAndClaims(List<ProviderMatcher.Provider> providers, List<string> memMeasures, List<string> provMeasures, string providerTable, string memberTable)
        {
            WriteToLog("Beginning Data Save");
            List<ProviderMatcher.Member> members = new List<ProviderMatcher.Member>();
            foreach (ProviderMatcher.Provider provider in providers)
            {
                if (provider.Members.Count > 0)
                {
                    members.AddRange(provider.Members);
                }
            }

            DataTable providersDT = new DataTable();
            try
            {
                foreach (PropertyInfo prop in typeof(ProviderMatcher.Provider).GetProperties())
                {
                    if(prop.PropertyType == typeof(Dictionary<string, int>))
                    {
                        foreach(string measure in provMeasures)
                        {
                            providersDT.Columns.Add(prop.Name.Replace("_", measure), typeof(int));
                        }
                    }
                    else if (prop.Name == "Members")
                    {
                        // Do nothing
                    }
                    else
                    {
                        Type dataType = prop.PropertyType;
                        providersDT.Columns.Add(prop.Name, dataType);
                    }

                }

                // It's getting out of sync somewhere, trying to place PMPMAmount on DateCreated. Figure this out ASAP
                foreach (ProviderMatcher.Provider item in providers)
                {
                    PropertyInfo[] props = item.GetType().GetProperties();
                    DataRow row = providersDT.NewRow();

                    for (int i = 0, a = 0; i < props.Length; i++)
                    {
                        if (props[i].PropertyType == typeof(Dictionary<string, int>))
                        {
                            Dictionary<string, int> values = (Dictionary<string, int>)props[i].GetValue(item);
                            foreach (KeyValuePair<string, int> measure in values)
                            {
                                row[a] = measure.Value;
                                a++;
                            }
                        }
                        else if (props[i].Name == "Members")
                        {
                            // Do nothing
                        }
                        else
                        {
                            if(props[i].Name == "CompositeScore")
                            {

                            }
                            try
                            {
                                var value = props[i].GetValue(item);
                                row[a] = value;
                                a++;
                            }
                            catch (Exception)
                            {

                            }
                        }
                    }
                    providersDT.Rows.Add(row);
                }
            }
            catch (Exception)
            {

            }

            DataTable membersDT = new DataTable();
            try
            {
                foreach (PropertyInfo prop in typeof(ProviderMatcher.Member).GetProperties())
                {
                    if (prop.PropertyType == typeof(Dictionary<string, int>))
                    {
                        foreach (string measure in memMeasures)
                        {
                            if (!(measure == "WCCN" || measure == "WCCP"))
                            {
                                membersDT.Columns.Add(prop.Name.Replace("_", measure), typeof(int));
                            }
                        }
                    }
                    else if (prop.Name == "Claims" || prop.Name == "hasWCCNDenominator" || prop.Name == "hasWCCPDenominator" || prop.Name == "hasWCCNNumerator" || prop.Name == "hasWCCPNumerator")
                    {
                        // Do nothing
                    }
                    else
                    {
                        Type dataType = prop.PropertyType;
                        membersDT.Columns.Add(prop.Name, dataType);
                    }
                }

                foreach (ProviderMatcher.Member item in members)
                {
                    PropertyInfo[] props = item.GetType().GetProperties();
                    DataRow row = membersDT.NewRow();

                    for (int i = 0, a = 0; i < props.Length; i++)
                    {
                        if (props[i].PropertyType == typeof(Dictionary<string, int>))
                        {
                            Dictionary<string, int> values = (Dictionary<string, int>)props[i].GetValue(item);
                            foreach (KeyValuePair<string, int> measure in values)
                            {
                                if (measure.Key == "WCCN" || measure.Key == "WCCP")
                                {
                                    // Do nothing
                                }
                                else
                                {
                                    row[a] = measure.Value;
                                    a++;
                                }
                            }
                        }
                        else if (props[i].Name == "Claims" || props[i].Name == "hasWCCNDenominator" || props[i].Name == "hasWCCPDenominator" || props[i].Name == "hasWCCNNumerator" || props[i].Name == "hasWCCPNumerator")
                        {
                            // Do nothing
                        }
                        else
                        {
                            var value = props[i].GetValue(item);
                            row[a] = value;
                            a++;
                        }
                    }
                    membersDT.Rows.Add(row);
                }
            }
            catch (Exception)
            {

            }

            DataWork.SaveDataTableToDb(memberTable, membersDT, LoggerPhpArchive, membersDT.Columns.Cast<DataColumn>().Select(x => x.ColumnName).ToArray().ToList(), 
                membersDT.Columns.Cast<DataColumn>().Select(x => x.ColumnName).ToArray().ToList());
            DataWork.SaveDataTableToDb(providerTable, providersDT, LoggerPhpArchive, providersDT.Columns.Cast<DataColumn>().Select(x => x.ColumnName).ToArray().ToList(), 
                providersDT.Columns.Cast<DataColumn>().Select(x => x.ColumnName).ToArray().ToList());
            WriteToLog("Data Save Complete");
        }

        private void LoadHedisClaims(ref List<ProviderMatcher.Provider> providers, string yearMonth, string year, bool annualRunFlag)
        {
            WriteToLog("Beginning HEDIS Claims Load");
            Dictionary<string, string> mapping = new Dictionary<string, string>();

            foreach (var prop in typeof(ProviderMatcher.Claim).GetProperties())
            {
                mapping.Add(prop.Name, prop.Name == "YearMonth" && annualRunFlag ? "Year" : prop.Name);
            }
            try
            {
                Parallel.ForEach(providers.AsEnumerable(), /*options,*/ provider =>
                {
                    try
                    {
                        Parallel.ForEach(provider.Members.AsEnumerable(), /*options,*/ member =>
                        {
                            DataTable memClaim = ExtractFactory.ConnectAndQuery(this, LoggerPhpConfig, ClaimQ(member.MEM_NBR.ToString(), member.PAT_ID, yearMonth, year, annualRunFlag));
                            member.Claims = new List<ProviderMatcher.Claim>();
                            member.Claims.AddRange(memClaim.ToList<ProviderMatcher.Claim>(mapping));
                        });
                    }
                    catch (Exception ex)
                    {
                        throw (ex.InnerException);
                    }
                });
            }
            catch (Exception ex)
            {
                throw (ex.InnerException);
            }
            WriteToLog("Completed HEDIS Claims Load");
        }

        private string ClaimQ(string MEM_NBR, string PAT_ID, string yearMonth, string year, bool annualRunFlag)
        {
            return string.Format(@"", MEM_NBR, PAT_ID, yearMonth, year, annualRunFlag ? "Annual_Production_Details" : "Extract_Report", annualRunFlag ? "" : "Month");
        }

        private void LoadMembers(DateTime reportDate, string year, bool annualRunFlag, DateTime firstOfYear, DateTime lastOfYear, ref List<ProviderMatcher.Provider> providers, List<string> measures)
        {
            WriteToLog("Beginning Member Load");
            try
            {
                Parallel.ForEach(providers.AsEnumerable(), provider =>
                {
                    List<ProviderMatcher.Member> provMems = ExtractFactory.ConnectAndQuery<ProviderMatcher.Member>(this, LoggerPhpConfig,
                       MemberQ(reportDate, year, annualRunFlag, firstOfYear, lastOfYear, provider.PCPId)).ToList();
                    
                    foreach(ProviderMatcher.Member mem in provMems)
                    {
                        mem.has_Denominator = ProviderMatcher.createMeasDictionary(measures);
                        mem.has_Numerator = ProviderMatcher.createMeasDictionary(measures);
                    }
                    provider.Members = provMems;
                    provider.TotalMembers = provMems.Count;
                });
            }
            catch (Exception ex)
            {
                throw (ex.InnerException);
            }

            WriteToLog("Completed Member Load");
        }

        private string MemberQ(DateTime reportDate, string year, bool annualRunFlag, DateTime firstOfYear, DateTime lastOfYear, string PRPR_ID)
        {
            DateTime endDate = annualRunFlag ? lastOfYear : DateTime.Today.AddDays(-(DateTime.Today.Day));
            return string.Format(@"", reportDate.ToString("yyyy-MM-dd"), year, annualRunFlag.ToString(), firstOfYear.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"), PRPR_ID);
        }

        private void GenerateReports(int year, int month, bool annualRunFlag, bool managementReportsOnly, bool buildAndDontSendReportsOnly, string providerTable)
        {
            WriteToLog("Beginning Report Generation");

            FileSystem.ReportYearDir(LoggerWorkDir + "Zip");

            List<ProvidersToReport> providersToReport = ExtractFactory.ConnectAndQuery<ProvidersToReport>(this, LoggerPhpArchive, 
                $@"SELECT 
	                    ProviderKey, 
	                    RTRIM(PCPId) as ProviderId, 
	                    RTRIM(ProviderTaxID) as ProviderTaxID, 
	                    ProviderLastName, 
	                    ProviderSpec,
	                    STUFF(
	                    (SELECT DISTINCT CONCAT(',',accesslistid)
	                    FROM PHPConfg.dbo.DA03620_PayerAdministrativeUserReport_A paur 
	                    WHERE paur.TaxIdNumber = prov.ProviderTaxID
                        AND paur.UserRole = 'Provider'
	                    FOR XML PATH ('')), 1, 1, '') as AccessListIds
                    FROM {providerTable} as prov
                    WHERE IsActive = 1 
                    AND TotalMembers > 0
                    --AND TotalEEDRecords <> 0").ToList();

            List<ProviderPedsData> provPedsData = FetchProviderPedsData(year, annualRunFlag);
            List<ProviderAdultData> provAdultData = FetchProviderAdultData(year, annualRunFlag);

            if (!managementReportsOnly)
            {
                ParallelOptions options = new ParallelOptions();
                options.MaxDegreeOfParallelism = 1; // DO NOT INCREASE THIS!!!!! FURTHER INVESTIGATION NEEDED
                                                    // BUT GENERATING IN PARALLEL CAUSES PARTS OF THE TEMPLATE
                                                    // TO NOT RENDER OR RENDER INCOMPLETE DATA
                try
                {
                    Parallel.ForEach(providersToReport.AsEnumerable(), options, prov =>
                    {
                        string outfile = "";
                        if (prov.ProviderSpec.ToLower() == "peds" || prov.ProviderSpec.ToLower() == "pdim")
                        {
                            ProviderPedsData pedsProvider = provPedsData.Find(x => x.ProviderKey == prov.ProviderKey);
                            if (provPedsData != null && provPedsData.Count > 0)
                            {
                                outfile = RenderTemplate(pedsProvider, year, annualRunFlag);
                            }
                        }
                        else
                        {
                            ProviderAdultData adultProvider = provAdultData.Find(x => x.ProviderKey == prov.ProviderKey);
                            if (provAdultData != null && provAdultData.Count > 0)
                            {
                                outfile = RenderTemplate(adultProvider, year, annualRunFlag);
                            }
                        }

                    });
                }
                catch (Exception ex)
                {
                    throw (ex.InnerException);
                }

                if (!buildAndDontSendReportsOnly)
                {
                    TransferFiles();
                }
            }

            string reportLoc = RenderManagementReport(year, month, annualRunFlag);

            FileTransfer.PushToSharepoint("ITReports", ProcessId, reportLoc, this);

            string zipArchive = LoggerOutputYearDir + "PIPFileArchive" + DateTime.Today.ToString("yyyyMMdd") + (annualRunFlag ? "_FINAL" : "") + ".zip";

            Zippers.Zip(zipArchive, LoggerWorkDir + "/Zip/", this);
            FileSystem.CopyToDir(zipArchive, $@"\\TReports\DA02656\{Path.GetFileName(zipArchive)}");

            WriteToLog("Report Generation Complete");
        }

        private void TransferFiles()
        {

        }

        private List<ProviderAdultData> FetchProviderAdultData(int year, bool annualRunFlag)
        {
            string query = MainAdultQuery(year, annualRunFlag);
            return ExtractFactory.ConnectAndQuery<ProviderAdultData>(this, LoggerPhpArchive, query).ToList();
        }

        private List<ProviderPedsData> FetchProviderPedsData(int year, bool annualRunFlag)
        {
            string query = MainPedsQuery(year, annualRunFlag);
            return ExtractFactory.ConnectAndQuery<ProviderPedsData>(this, LoggerPhpArchive, query).ToList();
        }

        private string RenderTemplate(ProviderData provider, int year, bool annualRunFlag)
        {
            string outputLocation = "";
            string templateFileLocation = "";
            if (provider.GetType() == typeof(ProviderAdultData)) {
                templateFileLocation = this.WorkingDirectory + $@"\ExternalResources\DA02656\DA02656_PIP_Progress_Report_Adult.html";
            } else if (provider.GetType() == typeof(ProviderPedsData))
            {
                templateFileLocation = this.WorkingDirectory + $@"\ExternalResources\DA02656\DA02656_PIP_Progress_Report_Peds.html";
            }

            string templateContents = File.ReadAllText(templateFileLocation);


            string title;
            if (annualRunFlag)
            {
                title = provider.ProviderID + "_" + provider.ProviderTaxID + "_" + provider.ProviderLastName + "_PCPI_FINAL_" + year.ToString();
            }
            else
            {
                title = provider.ProviderID + "_" + provider.ProviderTaxID + "_" + provider.ProviderLastName + "_PCPI_" + DateTime.Today.ToString("MMMyyyy");
            }
            outputLocation = this.LoggerWorkDir + "Zip/" + title + ".pdf";
            
            return outputLocation;
        }

        private string RenderManagementReport(int year, int month, bool annualRunFlag)
        {
            DataTable reportData = DataWork.QueryToDataTable(this, ManagementQuery(year, annualRunFlag), LoggerPhpArchive);
            reportData.TableName = "Payouts";
            DataTable payoutData = DataWork.QueryToDataTable(this, ManagementPayoutSummaryQuery(year), LoggerPhpArchive);
            payoutData.TableName = "Providers";

            string outputLocation = LoggerWorkDir + $"/Zip/PCPI_Management_Report{(annualRunFlag ? "_FINAL" : "")}_{DateTime.Today.ToString("MMMyyyy")}.xlsx";
            string sheetName = "Sheet1";

            //create excel file
            return outputLocation;
        }

        /// <summary>
        /// Use Google Chrome to render html templates and save to a PDF.
        /// Uses existing Chrome install.
        /// </summary>
        /// <param name="content"></param>
        /// <param name="outputFile"></param>
        /// <returns></returns>
        private async Task GeneratePDF(string content, string outputFile)
        {
            string chromeAppKey = @"\Software\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe";
            string chromePath = (string)(Registry.GetValue("HKEY_LOCAL_MACHINE" + chromeAppKey, "", null) ??
                                Registry.GetValue("HKEY_CURRENT_USER" + chromeAppKey, "", null));

        }

        private string MainAdultQuery(int reportYear, bool annualRun)
        {
            return string.Format(@"
                WITH Tot AS (SELECT reportyear,
                                    sum(EligibleHBDRecords)                          AS PHPMetGSD,
                                    sum(TotalHBDRecords)                             AS TotGSD,
                                    sum(TotalHBDRecords) - sum(EligibleHBDRecords)   AS PHPDidNotMeetGSD,
                                    sum(EligibleEEDRecords)                          AS PHPMetEED,
                                    sum(TotalEEDRecords)                             AS TotEED,
                                    sum(TotalEEDRecords) - sum(EligibleEEDRecords)   AS PHPDidNotMeetEED,
                                    sum(EligibleKEDRecords)                          AS PHPMetKED,
                                    sum(TotalKEDRecords)                             AS TotKED,
                                    sum(TotalKEDRecords) - sum(EligibleKEDRecords)   AS PHPDidNotMeetKED,
                                    sum(EligibleBCSERecords)                         AS PHPMetBCSE,
                                    sum(TotalBCSERecords)                            AS TotBCSE,
                                    sum(TotalBCSERecords) - sum(EligibleBCSERecords) AS PHPDidNotMeetBCSE,
                                    sum(EligibleCBPRecords)                          AS PHPMetCBP,
                                    sum(TotalCBPRecords)                             AS TotCBP,
                                    sum(TotalCBPRecords) - sum(EligibleCBPRecords)   AS PHPDidNotMeetCBP

                             FROM PHPArchv.dbo.DA02656_Provider_A_{0}
                             WHERE
                --AnnualRun = 'FALSE' AND
                                 ((AnnualRun = 'FALSE' AND {1} = 0) OR (AnnualRun = 'TRUE' AND {1} = 1))
                               AND IsActive = 1
                               AND ProviderSpec NOT IN ('PDIM', 'PEDS')
                             GROUP BY ReportYear)

                SELECT inc.PCPId                                      AS ProviderID,
                       inc.ProviderKey,
                       inc.ProviderTaxID,
                       inc.ProviderLastName,
                       'Adult'                                        AS ProviderSpec,
                       cast(inc.reportdate AS datetime)               AS ReportDate,
                       inc.ReportYear,
                       INC.ANNUALRUN                                  AS AnnualRun,
                       cast(CASE
                                WHEN datepart(YEAR, inc.ReportDate) > inc.ReportYear THEN convert(date, concat(inc.ReportYear, '1231'))
                                ELSE convert(date, dateadd(DAY, -(datepart(DAY, inc.ReportDate)), inc.ReportDate))
                            END AS datetime)                          AS PeriodEndDate,
                       inc.ProviderName,
                       CASE
                           WHEN
                               inc.GroupName <> '' THEN
                               concat(inc.GroupName, CHAR(13)+CHAR(10), adr.Address)
                           ELSE
                               adr.Address
                       END                                            AS AddressGroup,
                       CASE
                           WHEN
                               inc.GroupName <> '' THEN
                               concat(inc.GroupName, '
                ', inc.PCPId, '
                ', inc.ProviderSpecDesc)
                           ELSE concat(inc.PCPId, '
                ', inc.ProviderSpecDesc)
                       END                                            AS GroupProviderIDSpecialty,
                       inc.TotalMeasuresMet                           AS EligibleMeasures,
                       inc.CompositeScore                             AS CompositeScore,
                       inc.TotalMembers,
                       inc.EligibleHBDRecords                         AS MetGSD,
                       inc.TotalHBDRecords                            AS TotalGSDRecords,
                       inc.TotalHBDRecords - inc.EligibleHBDRecords   AS DidNotMeetGSD,
                       inc.HBDPoints                                  AS GSDPoints,
                       inc.EligibleKEDRecords                         AS MetKED,
                       inc.TotalKEDRecords,
                       inc.TotalKEDRecords - inc.EligibleKEDRecords   AS DidNotMeetKED,
                       inc.KEDPoints,
                       inc.EligibleBCSERecords                        AS MetBCSE,
                       inc.TotalBCSERecords                           AS TotalBCSERecords,
                       inc.TotalBCSERecords - inc.EligibleBCSERecords AS DidNotMeetBCSE,
                       inc.BCSEPoints                                 AS BCSEPoints,
                       inc.EligibleEEDRecords                         AS MetEED,
                       inc.TotalEEDRecords,
                       inc.TotalEEDRecords - inc.EligibleEEDRecords   AS DidNotMeetEED,
                       inc.EEDPoints,
                       inc.EligibleCBPRecords                         AS MetCBP,
                       inc.TotalCBPRecords,
                       inc.TotalCBPRecords - inc.EligibleCBPRecords   AS DidNotMeetCBP,
                       inc.CBPPoints,
                       inc.TotalPoints,
                       inc.RewardBracketKey,
                       inc.PMPMAmount,
                       inc.TotalReward,
                       Tot.*,
                       inc.GroupName

                FROM PHPArchv.dbo.DA02656_Provider_A_{0} inc

                     INNER JOIN (SELECT PCPId,
                                        ProviderKey,
                                        CASE
                                            WHEN
                                                AddressLine2 <> '' THEN
                                                concat(AddressLine1, '
                ', AddressLine2, '
                ', City, ', ', State, ' ', Zip)
                                            ELSE
                                                concat(AddressLine1, '
                ', City, ', ', State, ' ', Zip)
                                        END AS Address
                                 FROM PHPArchv.dbo.DA02656_Provider_A_{0}) adr
                                ON adr.ProviderKey = inc.ProviderKey

                     INNER JOIN Tot
                                ON Tot.ReportYear = inc.ReportYear

                WHERE inc.isactive = 1
                  AND ProviderSpec NOT IN ('PDIM', 'PEDS')
                  AND ((AnnualRun = 'FALSE' AND {1} = 0) OR (AnnualRun = 'TRUE' AND {1} = 1))
                  AND inc.reportyear = {0}
            ", reportYear, annualRun == true ? 1 : 0);
        }

        private string MainPedsQuery(int reportYear, bool annualRun)
        {
            return string.Format(@"
                WITH Tot AS (SELECT reportyear,
                                    sum(EligibleWCVRecords)                        AS PHPMetWCV,
                                    sum(TotalWCVRecords)                           AS TotWCV,
                                    sum(TotalWCVRecords) - sum(EligibleWCVRecords) AS PHPDidNotMeetWCV,
                                    sum(EligibleIMARecords)                        AS PHPMetIMA,
                                    sum(TotalIMARecords)                           AS TotIMA,
                                    sum(TotalIMARecords) - sum(EligibleIMARecords) AS PHPDidNotMeetIMA,
                                    sum(EligibleWCCRecords)                        AS PHPMetWCC,
                                    sum(TotalWCCRecords)                           AS TotWCC,
                                    sum(TotalWCCRecords) - sum(EligibleWCCRecords) AS PHPDidNotMeetWCC,
                                    sum(EligibleCISRecords)                        AS PHPMetCIS,
                                    sum(TotalCISRecords)                           AS TotCIS,
                                    sum(TotalCISRecords) - sum(EligibleCISRecords) AS PHPDidNotMeetCIS,
                                    sum(EligibleW30Records)                        AS PHPMetW30,
                                    sum(TotalW30Records)                           AS TotW30,
                                    sum(TotalW30Records) - sum(EligibleW30Records) AS PHPDidNotMeetW30

                             FROM PHPArchv.dbo.DA02656_Provider_A_{0}
                             WHERE ((AnnualRun = 'FALSE'
                               AND {1} = 0)
                                OR (AnnualRun = 'TRUE'
                               AND {1} = 1) )
                               AND
                                 IsActive = 1
                               AND
                                 ProviderSpec IN ('PDIM'
                                 , 'PEDS')
                             GROUP BY ReportYear)

                SELECT inc.PCPId                                    AS ProviderID,
                       inc.ProviderKey,
                       inc.ProviderTaxID,
                       inc.ProviderLastName,
                       'Peds'                                       AS ProviderSpec,
                       cast(inc.reportdate AS datetime)             AS ReportDate,
                       inc.ReportYear,
                       INC.ANNUALRUN                                AS AnnualRun,
                       cast(CASE
                                WHEN datepart(YEAR, inc.ReportDate) > inc.ReportYear THEN convert(date, concat(inc.ReportYear, '1231'))
                                ELSE convert(date, dateadd(DAY, -(datepart(DAY, inc.ReportDate)), inc.ReportDate))
                            END AS datetime)                        AS PeriodEndDate,
                       inc.ProviderName,
                       CASE
                           WHEN
                               inc.GroupName <> '' THEN
                               concat(inc.GroupName, '
                ', adr.Address)
                           ELSE
                               adr.Address
                       END                                          AS AddressGroup,
                       CASE
                           WHEN
                               inc.GroupName <> '' THEN
                               concat(inc.GroupName, '
                ', inc.PCPId, '
                ', inc.ProviderSpecDesc)
                           ELSE concat(inc.PCPId, '
                ', inc.ProviderSpecDesc)
                       END                                          AS GroupProviderIDSpecialty,
                       inc.TotalMeasuresMet                         AS EligibleMeasures,
                       inc.CompositeScore                           AS CompositeScore,
                       inc.TotalMembers,
                       inc.EligibleIMARecords                       AS MetIMA,
                       inc.TotalIMARecords,
                       inc.TotalIMARecords - inc.EligibleIMARecords AS DidNotMeetIMA,
                       inc.IMAPoints,
                       inc.EligibleWCCRecords                       AS MetWCC,
                       inc.TotalWCCRecords,
                       inc.TotalWCCRecords - inc.EligibleWCCRecords AS DidNotMeetWCC,
                       inc.WCCPoints,
                       inc.EligibleWCVRecords                       AS MetWCV,
                       inc.TotalWCVRecords,
                       inc.TotalWCVRecords - inc.EligibleWCVRecords AS DidNotMeetWCV,
                       inc.WCVPoints,
                       inc.EligibleCISRecords                       AS MetCIS,
                       inc.TotalCISRecords,
                       inc.TotalCISRecords - inc.EligibleCISRecords AS DidNotMeetCIS,
                       inc.CISPoints,
                       inc.EligibleW30Records                       AS MetW30,
                       inc.TotalW30Records,
                       inc.TotalW30Records - inc.EligibleW30Records AS DidNotMeetW30,
                       inc.W30Points,
                       inc.TotalPoints,
                       inc.RewardBracketKey,
                       inc.PMPMAmount,
                       inc.TotalReward,
                       Tot.*,
                       inc.GroupName                                AS GroupName

                FROM PHPArchv.dbo.DA02656_Provider_A_{0} inc

                INNER JOIN (SELECT PCPId,
                ProviderKey,
                CASE
                WHEN
                AddressLine2 <> '' THEN
                CONCAT(AddressLine1,'
                ',AddressLine2,'
                ',City,', ',STATE,' ',Zip)
                ELSE
                CONCAT(AddressLine1,'
                ',City,', ',STATE,' ',Zip)
                END AS ADDRESS
                FROM PHPArchv.dbo.DA02656_Provider_A_{0}) adr
                ON adr.ProviderKey = inc.ProviderKey
                    INNER JOIN Tot
                    ON Tot.ReportYear = inc.ReportYear

                WHERE inc.isactive = 1
                  AND ProviderSpec IN ('PDIM'
                    , 'PEDS')
                  AND ((AnnualRun = 'FALSE'
                  AND {1} = 0)
                   OR (AnnualRun = 'TRUE'
                  AND {1} = 1) )
                  AND inc.reportyear = {0}
            ", reportYear, annualRun == true ? 1 : 0);
        }

        public  string ManagementQuery(int reportYear, bool annualRun)
        {
            return string.Format(@"
            ", reportYear, annualRun == true ? 1 : 0);
        }

        public string ManagementPayoutSummaryQuery(int reportYear)
        {
            return string.Format(@"
                WITH G AS
                         (SELECT T.ReportYear            AS [Report_Year],
                                 t.AnnualRun,
                                 t.ReportYear            AS [Measure_Year],
                                 t.SubMeasure,
                                 T.Report                AS [Measured_By],
                                 sum(T.Total_Volume)     AS [Total_Volume],
                                 sum(T.Total_Volume_Met) AS [Total_Volume_Met],
                                 T.Measure_Key,
                                 CASE
                                     WHEN sum(T.Total_Volume) = 0 THEN 0
                                     ELSE (round((convert(decimal, sum(T.Total_volume_Met)) / convert(decimal, sum(T.total_volume))),
                                                 4))
                                 END                     AS percentage

                          FROM (SELECT m.reportyear,
                                       m.annualRun,
                                       (SELECT d.measuredatakey
                                        FROM [PHPConfg].dbo.DA02656_DimMeasure_C d
                                        WHERE m.CISMeasureKey = d.MeasureKey
                                          AND d.IsActive = '1') AS 'Measure_Key',
                                       (SELECT d.SubMeasure
                                        FROM [PHPConfg].dbo.DA02656_DimMeasure_C d
                                        WHERE m.CISMeasureKey = d.MeasureKey
                                          AND d.IsActive = '1') AS 'SubMeasure',
                                       'Peds'                   AS 'Report',
                                       m.TotalCISRecords        AS 'Total_Volume',
                                       m.EligibleCISRecords     AS 'Total_Volume_Met',
                                       m.CISPoints              AS 'Points_Achieved'
                                FROM PHPArchv.dbo.DA02656_Provider_A_{0} M
                                WHERE m.isactive = 1 AND m.TotalMembers <> 0
                                UNION ALL
                                (
                                SELECT
                                    m.reportyear, m.annualRun, (SELECT d.measuredatakey FROM [PHPConfg].dbo.DA02656_DimMeasure_C d WHERE m.IMAMeasureKey = d.MeasureKey AND d.IsActive = '1') AS 'Measure_Key', (SELECT d.SubMeasure FROM [PHPConfg].dbo.DA02656_DimMeasure_C d WHERE m.IMAMeasureKey = d.MeasureKey AND d.IsActive = '1') AS 'SubMeasure', 'Peds' AS 'Report', m.TotalIMARecords AS 'Total_Volume', m.EligibleIMARecords AS 'Total_Volume_Met', m.IMAPoints AS 'Points_Achieved'
                                FROM PHPArchv.dbo.DA02656_Provider_A_{0} M
                                WHERE m.isactive = 1 AND m.TotalMembers <> 0)
                                UNION ALL
                                (
                                SELECT
                                    m.reportyear, m.annualRun, (SELECT d.measuredatakey FROM [PHPConfg].dbo.DA02656_DimMeasure_C d WHERE m.WCVMeasureKey = d.MeasureKey AND d.IsActive = '1') AS 'Measure_Key', (SELECT d.SubMeasure FROM [PHPConfg].dbo.DA02656_DimMeasure_C d WHERE m.WCVMeasureKey = d.MeasureKey AND d.IsActive = '1') AS 'SubMeasure', 'Peds' AS 'Report', m.TotalWCVRecords AS 'Total_Volume', m.EligibleWCVRecords AS 'Total_Volume_Met', m.WCVPoints AS 'Points_Achieved'
                                FROM PHPArchv.dbo.DA02656_Provider_A_{0} M
                                WHERE m.isactive = 1 AND m.TotalMembers <> 0)
                                UNION ALL
                                (
                                SELECT
                                    m.reportyear, m.annualRun, (SELECT d.measuredatakey FROM [PHPConfg].dbo.DA02656_DimMeasure_C d WHERE m.WCCMeasureKey = d.MeasureKey AND d.IsActive = '1') AS 'Measure_Key', (SELECT d.SubMeasure FROM [PHPConfg].dbo.DA02656_DimMeasure_C d WHERE m.WCCMeasureKey = d.MeasureKey AND d.IsActive = '1') AS 'SubMeasure', 'Peds' AS 'Report', m.TotalWCCRecords AS 'Total_Volume', m.EligibleWCCRecords AS 'Total_Volume_Met', m.WCCPoints AS 'Points_Achieved'
                                FROM PHPArchv.dbo.DA02656_Provider_A_{0} M
                                WHERE m.isactive = 1 AND m.TotalMembers <> 0)
                                UNION ALL
                                (
                                SELECT
                                    m.reportyear, m.annualRun, (SELECT d.measuredatakey FROM [PHPConfg].dbo.DA02656_DimMeasure_C d WHERE m.W30MeasureKey = d.MeasureKey AND d.IsActive = '1') AS 'Measure_Key', (SELECT d.SubMeasure FROM [PHPConfg].dbo.DA02656_DimMeasure_C d WHERE m.W30MeasureKey = d.MeasureKey AND d.IsActive = '1') AS 'SubMeasure', 'Peds' AS 'Report', m.TotalW30Records AS 'Total_Volume', m.EligibleW30Records AS 'Total_Volume_Met', m.W30Points AS 'Points_Achieved'
                                FROM PHPArchv.dbo.DA02656_Provider_A_{0} M
                                WHERE m.isactive = 1 AND m.TotalMembers <> 0 )
                                UNION ALL
                                (
                                SELECT
                                    m.reportyear, m.annualRun, (SELECT d.measuredatakey FROM [PHPConfg].dbo.DA02656_DimMeasure_C d WHERE m.EEDMeasureKey = d.MeasureKey AND d.IsActive = '1') AS 'Measure_Key', (SELECT d.SubMeasure FROM [PHPConfg].dbo.DA02656_DimMeasure_C d WHERE m.EEDMeasureKey = d.MeasureKey AND d.IsActive = '1') AS 'SubMeasure', 'Adult' AS 'Report', m.TotalEEDRecords AS 'Total_Volume', m.EligibleEEDRecords AS 'Total_Volume_Met', m.EEDPoints AS 'Points_Achieved'
                                FROM PHPArchv.dbo.DA02656_Provider_A_{0} M
                                WHERE m.isactive = 1 AND m.TotalMembers <> 0 )
                                UNION ALL
                                (
                                SELECT
                                    m.reportyear, m.annualRun, (SELECT d.measuredatakey FROM [PHPConfg].dbo.DA02656_DimMeasure_C d WHERE m.HBDMeasureKey = d.MeasureKey AND d.IsActive = '1') AS 'Measure_Key', (SELECT d.SubMeasure FROM [PHPConfg].dbo.DA02656_DimMeasure_C d WHERE m.HBDMeasureKey = d.MeasureKey AND d.IsActive = '1') AS 'SubMeasure', 'Adult' AS 'Report', m.TotalHBDRecords AS 'Total_Volume', m.EligibleHBDRecords AS 'Total_Volume_Met', m.HBDPoints AS 'Points_Achieved'
                                FROM PHPArchv.dbo.DA02656_Provider_A_{0} M
                                WHERE m.isactive = 1 AND m.TotalMembers <> 0 )
                                UNION ALL
                                (
                                SELECT
                                    m.reportyear, m.annualRun, (SELECT d.measuredatakey FROM [PHPConfg].dbo.DA02656_DimMeasure_C d WHERE m.KEDMeasureKey = d.MeasureKey AND d.IsActive = '1') AS 'Measure_Key', (SELECT d.SubMeasure FROM [PHPConfg].dbo.DA02656_DimMeasure_C d WHERE m.KEDMeasureKey = d.MeasureKey AND d.IsActive = '1') AS 'SubMeasure', 'Adult' AS 'Report', m.TotalKEDRecords AS 'Total_Volume', m.EligibleKEDRecords AS 'Total_Volume_Met', m.KEDPoints AS 'Points_Achieved'
                                FROM PHPArchv.dbo.DA02656_Provider_A_{0} M
                                WHERE m.isactive = 1 AND m.TotalMembers <> 0 )
                                UNION ALL
                                (
                                SELECT
                                    m.reportyear, m.annualRun, (SELECT d.measuredatakey FROM [PHPConfg].dbo.DA02656_DimMeasure_C d WHERE m.CBPMeasureKey = d.MeasureKey AND d.IsActive = '1') AS 'Measure_Key', (SELECT d.SubMeasure FROM [PHPConfg].dbo.DA02656_DimMeasure_C d WHERE m.CBPMeasureKey = d.MeasureKey AND d.IsActive = '1') AS 'SubMeasure', 'Adult' AS 'Report', m.TotalCBPRecords AS 'Total_Volume', m.EligibleCBPRecords AS 'Total_Volume_Met', m.CBPPoints AS 'Points_Achieved'
                                FROM PHPArchv.dbo.DA02656_Provider_A_{0} M
                                WHERE m.isactive = 1 AND m.TotalMembers <> 0 )
                                UNION ALL
                                (
                                SELECT
                                    m.reportyear, m.annualRun, (SELECT d.measuredatakey FROM [PHPConfg].dbo.DA02656_DimMeasure_C d WHERE m.BCSEMeasureKey = d.MeasureKey AND d.IsActive = '1') AS 'Measure_Key', (SELECT d.SubMeasure FROM [PHPConfg].dbo.DA02656_DimMeasure_C d WHERE m.BCSEMeasureKey = d.MeasureKey AND d.IsActive = '1') AS 'SubMeasure', 'Adult' AS 'Report', m.TotalBCSERecords AS 'Total_Volume', m.EligibleBCSERecords AS 'Total_Volume_Met', m.BCSEPoints AS 'Points_Achieved'
                                FROM PHPArchv.dbo.DA02656_Provider_A_{0} M
                                WHERE m.isactive = 1 AND m.TotalMembers <> 0 )) T --Pivots 

                          GROUP BY T.reportyear,
                                   T.AnnualRun,
                                   T.Report,
                                   T.Measure_Key,
                                   t.SubMeasure)

                SELECT me.MeasureDescription [Incentive Measure],
                       g.Measured_By         [Measured By],
                       g.Total_Volume        [Total Plan Volume],
                       g.Total_Volume_Met    [Total Plan Volume Met],
                       g.percentage          [Plan Percentage Achieved],
                       me.Points             [Total Plan Points]

                FROM G
                     LEFT JOIN [PHPConfg].[dbo].[DA02656_DimMeasure_C] me
                               ON G.Measure_Key = me.MeasureDataKey AND g.SubMeasure = me.SubMeasure AND me.IsActive = '1' AND
                                  G.percentage BETWEEN me.MinValue AND me.MaxValue AND G.Measure_Year = me.MeasureYear

                WHERE G.Report_Year ={0}
                  AND g.measure_key IS NOT NULL
            ", reportYear);
        }

        public class ReportFunctions
        {
            public static Data.AppNames connection;
            public static string providerSpec;

            public ReportFunctions(Data.AppNames connection, string providerSpec)
            {
                ReportFunctions.connection = connection;
                ReportFunctions.providerSpec = providerSpec;
            }

            public class IncentiveMeasures
            {
                public int ProviderKey { get; set; }
                public string IncentiveMeasure { get; set; }
                public int TotalVolume { get; set; }
                public int TotalVolumeMet { get; set; }
                public byte PointsAchieved { get; set; }
            }
            
            public static Array OverallPerformance(int reportYear, int providerKey)
            {
                if (providerSpec.ToLower() == "adult")
                {
                    return ExtractFactory.ConnectAndQuery<IncentiveMeasures>(connection, OverallPerformanceAdultQuery(reportYear)).Where(x => x.ProviderKey == providerKey).ToArray();
                }
                else
                {
                    return ExtractFactory.ConnectAndQuery<IncentiveMeasures>(connection, OverallPerformancePedsQuery(reportYear)).Where(x => x.ProviderKey == providerKey).ToArray();
                }
            }

            private static string OverallPerformanceAdultQuery(int reportYear)
            {
                return string.Format(@"
                    SELECT T.*,
                           inc.totalpoints,
                           inc.totalmeasuresapplicable,
                           inc.ProviderKey,
                           CASE
                               WHEN t.MeasureKey = -1 THEN 'HbA1C Testing (as part of Comprehensive Diabetes Care) (CDC-A1C)'
                               WHEN t.MeasureKey = -2 THEN 'Retinal Eye Exam (as part of Comprehensive Diabetes Care) (CDC-EYE)'
                               ELSE m.MeasureDescription
                           END AS IncentiveMeasure

                    FROM PHPArchv.dbo.DA02656_Provider_A_{0} inc
                         INNER JOIN

                         (SELECT M.PCPId              AS ProviderId,
                                 m.ProviderKey,
                                 m.reportyear         AS ReportYear,
                                 1                    AS 'order',
                                 m.EEDMeasureKey      AS MeasureKey,
                                 'Members'            AS MeasuredBy,
                                 m.TotalEEDRecords    AS TotalVolume,
                                 m.EligibleEEDRecords AS TotalVolumeMet,
                                 m.EEDPoints          AS PointsAchieved
                          FROM PHPArchv.dbo.DA02656_Provider_A_{0} M
                          WHERE m.isactive = 1
                          UNION
                          (SELECT M.PCPId              AS ProviderId,
                                  m.ProviderKey,
                                  m.reportyear         AS ReportYear,
                                  2                    AS 'order',
                                  m.KEDMeasureKey      AS MeasureKey,
                                  'Members'            AS MeasuredBy,
                                  m.TotalKEDRecords    AS TotalVolume,
                                  m.EligibleKEDRecords AS TotalVolumeMet,
                                  m.KEDPoints          AS PointsAchieved
                           FROM PHPArchv.dbo.DA02656_Provider_A_{0} M
                           WHERE m.isactive = 1)
                          UNION
                          (SELECT M.PCPId              AS ProviderId,
                                  m.ProviderKey,
                                  m.reportyear         AS ReportYear,
                                  3                    AS 'order',
                                  m.CBPMeasureKey      AS MeasureKey,
                                  'Members'            AS MeasuredBy,
                                  m.TotalCBPRecords    AS TotalVolume,
                                  m.EligibleCBPRecords AS TotalVolumeMet,
                                  m.CBPPoints          AS PointsAchieved
                           FROM PHPArchv.dbo.DA02656_Provider_A_{0} M
                           WHERE m.isactive = 1)
                          UNION
                          (SELECT M.PCPId              AS ProviderId,
                                  m.ProviderKey,
                                  m.reportyear         AS ReportYear,
                                  4                    AS 'order',
                                  m.HBDMeasureKey      AS MeasureKey,
                                  'Claims'             AS MeasuredBy,
                                  m.TotalHBDRecords    AS TotalVolume,
                                  m.EligibleHBDRecords AS TotalVolumeMet,
                                  m.HBDPoints          AS PointsAchieved
                           FROM PHPArchv.dbo.DA02656_Provider_A_{0} M
                           WHERE m.isactive = 1)
                          UNION
                          (SELECT M.PCPId               AS ProviderId,
                                  m.ProviderKey,
                                  m.reportyear          AS ReportYear,
                                  5                     AS 'order',
                                  m.BCSEMeasureKey      AS MeasureKey,
                                  'Claims'              AS MeasuredBy,
                                  m.TotalBCSERecords    AS TotalVolume,
                                  m.EligibleBCSERecords AS TotalVolumeMet,
                                  m.BCSEPoints          AS PointsAchieved
                           FROM PHPArchv.dbo.DA02656_Provider_A_{0} M
                           WHERE m.isactive = 1)) T
                         ON t.ProviderKey = inc.ProviderKey

                         LEFT JOIN PHPConfg.dbo.DA02656_DimMeasure_C m
                                   ON t.[MeasureKey] = m.MeasureKey

                    WHERE inc.ProviderSpec NOT IN ('PDIM', 'PEDS')
                      AND inc.IsActive = 1
                    ORDER BY [order]
                ", reportYear);
            }

            private static string OverallPerformancePedsQuery(int reportYear)
            {
                return string.Format(@"
                    SELECT T.*,
                           inc.totalpoints,
                           inc.totalmeasuresapplicable,
                           inc.ProviderKey,
                           m.MeasureDescription AS IncentiveMeasure

                    FROM PHPArchv.dbo.DA02656_Provider_A_{0} inc
                         INNER JOIN

                         (SELECT M.PCPId              AS ProviderId,
                                 m.ProviderKey,
                                 m.reportyear         AS ReportYear,
                                 1                    AS 'order',
                                 m.WCCMeasureKey      AS MeasureKey,
                                 'Members'            AS MeasuredBy,
                                 m.TotalWCCRecords    AS TotalVolume,
                                 m.EligibleWCCRecords AS TotalVolumeMet,
                                 m.WCCPoints          AS PointsAchieved
                          FROM PHPArchv.dbo.DA02656_Provider_A_{0} M
                          WHERE m.isactive = 1

                          UNION
                          (SELECT M.PCPId              AS ProviderId,
                                  m.ProviderKey,
                                  m.reportyear         AS ReportYear,
                                  3                    AS 'order',
                                  m.IMAMeasureKey      AS MeasureKey,
                                  'Members'            AS MeasuredBy,
                                  m.TotalIMARecords    AS TotalVolume,
                                  m.EligibleIMARecords AS TotalVolumeMet,
                                  m.IMAPoints          AS PointsAchieved
                           FROM PHPArchv.dbo.DA02656_Provider_A_{0} M
                           WHERE m.isactive = 1)

                          UNION
                          (SELECT M.PCPId              AS ProviderId,
                                  m.ProviderKey,
                                  m.reportyear         AS ReportYear,
                                  4                    AS 'order',
                                  m.WCVMeasureKey      AS MeasureKey,
                                  'Members'            AS MeasuredBy,
                                  m.TotalWCVRecords    AS TotalVolume,
                                  m.EligibleWCVRecords AS TotalVolumeMet,
                                  m.WCVPoints          AS PointsAchieved
                           FROM PHPArchv.dbo.DA02656_Provider_A_{0} M
                           WHERE m.isactive = 1)

                          UNION
                          (SELECT M.PCPId              AS ProviderId,
                                  m.ProviderKey,
                                  m.reportyear         AS ReportYear,
                                  5                    AS 'order',
                                  m.CISMeasureKey      AS MeasureKey,
                                  'Members'            AS MeasuredBy,
                                  m.TotalCISRecords    AS TotalVolume,
                                  m.EligibleCISRecords AS TotalVolumeMet,
                                  m.CISPoints          AS PointsAchieved
                           FROM PHPArchv.dbo.DA02656_Provider_A_{0} M
                           WHERE m.isactive = 1)

                          UNION
                          (SELECT M.PCPId              AS ProviderId,
                                  m.ProviderKey,
                                  m.reportyear         AS ReportYear,
                                  5                    AS 'order',
                                  m.W30MeasureKey      AS MeasureKey,
                                  'Members'            AS MeasuredBy,
                                  m.TotalW30Records    AS TotalVolume,
                                  m.EligibleW30Records AS TotalVolumeMet,
                                  m.W30Points          AS PointsAchieved
                           FROM PHPArchv.dbo.DA02656_Provider_A_{0} M
                           WHERE m.isactive = 1)) T
                         ON t.ProviderKey = inc.ProviderKey

                         LEFT JOIN PHPConfg.dbo.DA02656_DimMeasure_C m
                                   ON t.[MeasureKey] = m.MeasureKey

                    WHERE inc.ProviderSpec IN ('PDIM', 'PEDS')
                      AND inc.IsActive = 1
                    ORDER BY [order]
                ", reportYear);
            }

            public class RewardBrackets
            {
                [Column]
                public string RewardBracket { get; set; }
                [Column]
                public decimal Reward { get; set; }
            }

            public static Array Points(int reportYear)
            {
                return ExtractFactory.ConnectAndQuery<RewardBrackets>(connection, PointsQuery(reportYear)).ToArray();
            }

            private static string PointsQuery(int reportYear)
            {
                return string.Format(@"
                    SELECT RewardBracket,
                           Reward,
                           RewardYear
                    FROM [PHPConfg].[dbo].[DA02656_DimRewardBracket_C]
                    WHERE IsActive = 1
                      AND rewardyear = {0}
                ", reportYear);
            }

            public class Rewards
            {
                [Column]
                public int ProviderKey { get; set; }
                [Column]
                public decimal PMPMAmount { get; set; }
                [Column]
                public byte TotalPoints { get; set; }
                [Column]
                public int TotalMeasuresMet { get; set; }
                [Column]
                public int TotalMeasuresApplicable { get; set; }
                [Column]
                public float CompositeScore { get; set; }
            }

            public static Object YourReward(int reportYear, int providerKey)
            {
                return ExtractFactory.ConnectAndQuery<Rewards>(connection, YourRewardQuery(reportYear)).Where(x => x.ProviderKey == providerKey).First();
            }

            private static string YourRewardQuery(int reportYear)
            {
                return string.Format(@"
                    SELECT ProviderKey,
                           PMPMAmount,
                           TotalPoints,
                           TotalMeasuresMet,
                           TotalMeasuresApplicable,
                           CompositeScore

                    FROM [PHPArchv].[dbo].[DA02656_Provider_A_{0}]
                    WHERE isactive = 1
                ", reportYear);
            }

            public class Overview
            {
                [Column]
                public int ProviderKey { get; set; }
                [Column]
                public string ProviderId { get; set; }
                [Column]
                public Int16 ReportYear { get; set; }
                [Column]
                public int TotalMembers { get; set; }
                [Column]
                public byte TotalPoints { get; set; }
                [Column]
                public decimal PMPMAmount { get; set; }
                [Column]
                public decimal TotalReward { get; set; }
            }

            public static Object ScoreOverview(int reportYear, int providerKey)
            {
                return ExtractFactory.ConnectAndQuery<Overview>(connection, ScoreOverviewQuery(reportYear)).Where(x => x.ProviderKey == providerKey).First();
            }

            private static string ScoreOverviewQuery(int reportYear)
            {
                return string.Format(@"
                    SELECT ProviderKey,
                           pcpid        AS ProviderId,
                           reportyear   AS ReportYear,
                           totalmembers AS TotalMembers,
                           TotalPoints,
                           PMPMAmount,
                           TotalReward

                    FROM PHPArchv.dbo.DA02656_Provider_A_{0}
                    WHERE IsActive = 1
                ", reportYear);
            }

            public class AdultPatient
            {
                [Column]
                public string ProviderId { get; set; }
                [Column]
                public Int16 ReportYear { get; set; }
                [Column]
                public int ProviderKey { get; set; }
                [Column]
                public string AnnualRun { get; set; }
                [Column]
                public string PatientName { get; set; }
                [Column]
                public string DateOfBirth { get; set; }
                [Column]
                public string GroupNumber { get; set; }
                [Column]
                public string Gender { get; set; }
                [Column]
                public string PatientIDNumber { get; set; }
                [Column]
                public byte Age { get; set; }
                [Column]
                public Int16 IsEEDEligible { get; set; }
                [Column]
                public Int16 EED { get; set; }
                [Column]
                public Int16 IsGSDEligible { get; set; }
                [Column]
                public Int16 GSD { get; set; }
                [Column]
                public Int16 IsCBPEligible { get; set; }
                [Column]
                public Int16 CBP { get; set; }
                [Column]
                public Int16 IsKEDEligible { get; set; }
                [Column]
                public Int16 KED { get; set; }
                [Column]
                public Int16 IsBCSEEligible { get; set; }
                [Column]
                public Int16 BCSE { get; set; }
            }

            public class PedsPatient
            {
                [Column]
                public string ProviderId { get; set; }
                [Column]
                public Int16 ReportYear { get; set; }
                [Column]
                public int ProviderKey { get; set; }
                [Column]
                public string AnnualRun { get; set; }
                [Column]
                public string PatientName { get; set; }
                [Column]
                public string DateOfBirth { get; set; }
                [Column]
                public string GroupNumber { get; set; }
                [Column]
                public string Gender { get; set; }
                [Column]
                public string PatientIDNumber { get; set; }
                [Column]
                public byte Age { get; set; }
                [Column]
                public Int16 IsIMAEligible { get; set; }
                [Column]
                public Int16 IMA { get; set; }
                [Column]
                public Int16 IsWCVEligible { get; set; }
                [Column]
                public Int16 WCV { get; set; }
                [Column]
                public Int16 IsCISEligible { get; set; }
                [Column]
                public Int16 CIS { get; set; }
                [Column]
                public Int16 IsWCCEligible { get; set; }
                [Column]
                public Int16 WCC { get; set; }
                [Column]
                public Int16 IsW30Eligible { get; set; }
                [Column]
                public Int16 W30 { get; set; }
            }

            public static Array RosterOfPatients(int reportYear, int providerKey)
            {
                if (providerSpec.ToLower() == "adult")
                {
                    return ExtractFactory.ConnectAndQuery<AdultPatient>(connection, RosterOfPatientsAdultQuery(reportYear)).Where(x => x.ProviderKey == providerKey).ToArray();
                }
                else
                {
                    return ExtractFactory.ConnectAndQuery<PedsPatient>(connection, RosterOfPatientsPedsQuery(reportYear)).Where(x => x.ProviderKey == providerKey).ToArray();
                }
            }

            private static string RosterOfPatientsAdultQuery(int reportYear)
            {
                return string.Format(@"
                ", reportYear);
            }

            private static string RosterOfPatientsPedsQuery(int reportYear)
            {
                return string.Format(@"
                ", reportYear);
            }
        }

        private class ProvidersToReport
        {
            [Column]
            public int ProviderKey { get; set; }
            [Column]
            public string ProviderId { get; set; }
            [Column]
            public string ProviderTaxID { get; set; }
            [Column]
            public string ProviderLastName { get; set; }
            [Column]
            public string ProviderSpec { get; set; }
            [Column]
            public string AccessListIds { get; set; }
        }

        [Table]
        [InheritanceMapping(Type = typeof(ProviderAdultData), Code = "Adult")]
        [InheritanceMapping(Type = typeof(ProviderPedsData), Code = "Peds")]
        [InheritanceMapping(Type = typeof(ProviderData), Code = "", IsDefault = true)]
        public class ProviderData
        {
            [Column]
            public string ProviderID { get; set; }
            [Column]
            public int ProviderKey { get; set; }
            [Column]
            public string ProviderTaxID { get; set; }
            [Column]
            public string ProviderLastName { get; set; }
            [Column(IsDiscriminator = true)]
            public string ProviderSpec { get; set; }
            [Column]
            public DateTime ReportDate { get; set; }
            [Column]
            public Int16 ReportYear { get; set; }
            [Column]
            public string AnnualRun { get; set; }
            [Column]
            public DateTime PeriodEndDate { get; set; }
            [Column]
            public string ProviderName { get; set; }
            [Column]
            public string AddressGroup { get; set; }
            [Column]
            public string GroupProviderIDSpecialty { get; set; }
            [Column]
            public int EligibleMeasures { get; set; }
            [Column]
            public float CompositeScore { get; set; }
            [Column]
            public int TotalMembers { get; set; }
        }

        public class ProviderAdultData : ProviderData
        {
            [Column]
            public int MetGSD { get; set; }
            [Column]
            public int TotalGSDRecords { get; set; }
            [Column]
            public int DidNotMeetGSD { get; set; }
            [Column]
            public byte GSDPoints { get; set; }
            [Column]
            public int MetKED { get; set; }
            [Column]
            public int TotalKEDRecords { get; set; }
            [Column]
            public int DidNotMeetKED { get; set; }
            [Column]
            public byte KEDPoints { get; set; }
            [Column]
            public int MetBCSE { get; set; }
            [Column]
            public int TotalBCSERecords { get; set; }
            [Column]
            public int DidNotMeetBCSE { get; set; }
            [Column]
            public byte BCSEPoints { get; set; }
            [Column]
            public int MetEED { get; set; }
            [Column]
            public int TotalEEDRecords { get; set; }
            [Column]
            public int DidNotMeetEED { get; set; }
            [Column]
            public byte EEDPoints { get; set; }
            [Column]
            public int MetCBP { get; set; }
            [Column]
            public int TotalCBPRecords { get; set; }
            [Column]
            public int DidNotMeetCBP { get; set; }
            [Column]
            public byte CBPPoints { get; set; }
            [Column]
            public byte TotalPoints { get; set; }
            [Column]
            public int RewardBracketKey { get; set; }
            [Column]
            public decimal PMPMAmount { get; set; }
            [Column]
            public decimal TotalReward { get; set; }
            [Column]
            public int TotReportYear { get; set; }
            [Column]
            public int PHPMetGSD { get; set; }
            [Column]
            public int TotGSD { get; set; }
            [Column]
            public int PHPDidNotMeetGSD { get; set; }
            [Column]
            public int PHPMetEED { get; set; }
            [Column]
            public int TotEED { get; set; }
            [Column]
            public int PHPDidNotMeetEED { get; set; }
            [Column]
            public int PHPMetKED { get; set; }
            [Column]
            public int TotKED { get; set; }
            [Column]
            public int PHPDidNotMeetKED { get; set; }
            [Column]
            public int PHPMetBCSE { get; set; }
            [Column]
            public int TotBCSE { get; set; }
            [Column]
            public int PHPDidNotMeetBCSE { get; set; }
            [Column]
            public int PHPMetCBP { get; set; }
            [Column]
            public int TotCBP { get; set; }
            [Column]
            public int PHPDidNotMeetCBP { get; set; }
            [Column]
            public string GroupName { get; set; }
        }

        public class ProviderPedsData : ProviderData
        {
            [Column]
            public int MetIMA { get; set; }
            [Column]
            public int TotalIMARecords { get; set; }
            [Column]
            public int DidNotMeetIMA { get; set; }
            [Column]
            public byte IMAPoints { get; set; }
            [Column]
            public int MetWCC { get; set; }
            [Column]
            public int TotalWCCRecords { get; set; }
            [Column]
            public int DidNotMeetWCC { get; set; }
            [Column]
            public byte WCCPoints { get; set; }
            [Column]
            public int MetWCV { get; set; }
            [Column]
            public int TotalWCVRecords { get; set; }
            [Column]
            public int DidNotMeetWCV { get; set; }
            [Column]
            public byte WCVPoints { get; set; }
            [Column]
            public int MetCIS { get; set; }
            [Column]
            public int TotalCISRecords { get; set; }
            [Column]
            public int DidNotMeetCIS { get; set; }
            [Column]
            public byte CISPoints { get; set; }
            [Column]
            public int MetW30 { get; set; }
            [Column]
            public int TotalW30Records { get; set; }
            [Column]
            public int DidNotMeetW30 { get; set; }
            [Column]
            public byte W30Points { get; set; }
            [Column]
            public byte TotalPoints { get; set; }
            [Column]
            public int RewardBracketKey { get; set; }
            [Column]
            public decimal PMPMAmount { get; set; }
            [Column]
            public decimal TotalReward { get; set; }
            [Column]
            public Int16 TotReportYear { get; set; }
            [Column]
            public int PHPMetWCV { get; set; }
            [Column]
            public int TotWCV { get; set; }
            [Column]
            public int PHPDidNotMeetWCV { get; set; }
            [Column]
            public int PHPMetIMA { get; set; }
            [Column]
            public int TotIMA { get; set; }
            [Column]
            public int PHPDidNotMeetIMA { get; set; }
            [Column]
            public int PHPMetWCC { get; set; }
            [Column]
            public int TotWCC { get; set; }
            [Column]
            public int PHPDidNotMeetWCC { get; set; }
            [Column]
            public int PHPMetCIS { get; set; }
            [Column]
            public int TotCIS { get; set; }
            [Column]
            public int PHPDidNotMeetCIS { get; set; }
            [Column]
            public int PHPMetW30 { get; set; }
            [Column]
            public int TotW30 { get; set; }
            [Column]
            public int PHPDidNotMeetW30 { get; set; }
            [Column]
            public string GroupName { get; set; }
        }

        public class ManagementData
        {
            [Column]
            public string ProviderID { get; set; }
            [Column]
            public string ProviderName { get; set; }
            [Column]
            public string Specialty { get; set; }
            [Column]
            public string ProviderTIN { get; set; }
            [Column]
            public string GroupID { get; set; }
            [Column]
            public string GroupName { get; set; }
            [Column]
            public string GroupTIN { get; set; }
            [Column]
            public string IPAID { get; set; }
            [Column]
            public string IPAName { get; set; }
            [Column]
            public string IPATIN { get; set; }
            [Column]
            public char Payee { get; set; }
            [Column]
            public byte Score { get; set; }
            [Column]
            public decimal BonusPMPM { get; set; }
            [Column]
            public int MembershipVolume { get; set; }
            [Column]
            public decimal PayoutAmount { get; set; }
            [Column]
            public string AnnualRun { get; set; }
            [Column]
            public DateTime QueryDate { get; set; }
            [Column]
            public decimal ExchangeMemberPayout { get; set; }
            [Column]
            public decimal CommercialMemberPayout { get; set; }
            [Column]
            public int Members { get; set; }
        }
    }
}