using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Utilities
{
    public class ProviderMatcher
    {
        public static List<Provider> loadProviders(DateTime reportDate, string year, bool annualRunFlag, DateTime firstOfYear, DateTime lastOfYear, Logger logger, List<string> measures, string table)
        {
            logger.WriteToLog("Starting Provider Load");
            List<Provider> provsToLoad = new List<Provider>();
            logger.WriteToLog("Initial Provider Query: " + provQOne(year));
            DataTable initProviders = ExtractFactory.ConnectAndQuery(logger.LoggerPhpConfig, provQOne(year));
            DataTable uniqueNPIs = initProviders.DefaultView.ToTable(true, "PRPR_NPI");
            foreach (DataRow NPI in uniqueNPIs.Rows)
            {
                List<DataRow> provIDs = initProviders.Select(string.Format("PRPR_NPI = {0}", NPI["PRPR_NPI"].ToString())).ToList();
                if (provIDs.Count >= 1)
                {
                    DataTable addressTable = ExtractFactory.ConnectAndQuery(logger.LoggerPhpConfig, provQTwo(provIDs, year));
                    DataTable nwprTable = ExtractFactory.ConnectAndQuery(logger.LoggerPhpConfig, provQThree(provIDs, year));
                    if (provIDs.Count == 1)
                    {
                        DataTable data = attemptIndividualMatch(provIDs.First(), addressTable, nwprTable, reportDate, year, annualRunFlag, firstOfYear, lastOfYear, logger, table);
                        if (data.Rows.Count > 0)
                        {
                            provsToLoad.Add(data.Rows[0].ToObject<Provider>());
                        }
                    }
                    else
                    {
                        List<DataRow> backUps = new List<DataRow>();
                        backUps.AddRange(provIDs);
                        for (int i = 0; i < provIDs.Count; i++)
                        {
                            DataTable data = attemptIndividualMatch(provIDs[i], addressTable, nwprTable, reportDate, year, annualRunFlag, firstOfYear, lastOfYear, logger, table);
                            if (data.Rows.Count > 0)
                            {
                                provsToLoad.Add(data.Rows[0].ToObject<Provider>());
                                provIDs.RemoveAt(i);
                                i--;
                            }
                        }
                        if (provIDs.Count > 0)
                        {
                            for (int i = 0; i < provIDs.Count; i++)
                            {
                                List<DataRow> thisProvAddrs = addressTable.Select(string.Format("PRAD_ID = {0}", provIDs[i]["PRPR_ID"].ToString())).ToList();
                                DataTable specificAddresses = addressTable.Clone();
                                DataTable specificNWPRs = nwprTable.Clone();
                                foreach (DataRow thisAddr in thisProvAddrs)
                                {
                                        specificAddresses = addressTable.Select(string.Format(@"PRAD_ADDR1 = '{0}' and PRAD_CITY = '{1}' and PRAD_STATE = '{2}' and PRAD_ZIP = '{3}'"
                                        , thisAddr["PRAD_ADDR1"].ToString().Replace("'", "''"), thisAddr["PRAD_CITY"].ToString(), thisAddr["PRAD_STATE"].ToString()
                                        , thisAddr["PRAD_ZIP"].ToString())).CopyToDataTable();
                                    foreach (DataRow provAddr in specificAddresses.Rows)
                                    {
                                        try
                                        {
                                            List<DataRow> nwpr = nwprTable.Select(string.Format("PRPR_ID = {0}", provAddr["PRAD_ID"].ToString())).ToList();
                                            for (int x = 0; x < nwpr.Count; x++)
                                            {//Don't want to add duplicates
                                                if (specificNWPRs.Select(string.Format("PRPR_ID = '{0}' and NWPR_PFX = '{1}' and NWPR_EFF_DT = '{2}' and NWPR_TERM_DT = '{3}'",
                                                    nwpr[x]["PRPR_ID"].ToString(), nwpr[x]["NWPR_PFX"].ToString(), nwpr[x]["NWPR_EFF_DT"].ToString(), nwpr[x]["NWPR_TERM_DT"].ToString())).Count() == 0)
                                                {
                                                    specificNWPRs.ImportRow(nwpr[x]);
                                                }
                                            }
                                        }
                                        catch
                                        {
                                            //No NWPR for that prov ID, not an issue. Could have put this in an if statement
                                        }
                                    }
                                }//This following line changed from original to account for specialties
                                DataTable data = attemptMultiMatch(backUps.CopyToDataTable().Select(string.Format("PRCF_MCTR_SPEC = '{0}'", provIDs[i]["PRCF_MCTR_SPEC"].ToString())).ToList(), specificAddresses, specificNWPRs, reportDate, year, annualRunFlag, firstOfYear, lastOfYear, logger, table);
                                if (data.Rows.Count > 0)
                                {
                                    logger.WriteToLog(string.Format("Adding provider {0} via Multi-ID Match", data.Rows[0]["PCPId"].ToString()));
                                    provsToLoad.Add(data.Rows[0].ToObject<Provider>());
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {

                }
            }

            foreach(Provider prov in provsToLoad)
            {
                //Dictionary<string, int> t = measures.AsEnumerable().ToDictionary();
                prov.Eligible_Records = createMeasDictionary(measures);
                prov.Total_Records = createMeasDictionary(measures);
                prov._MeasureKey = createMeasDictionary(measures);
                prov._Points = createMeasDictionary(measures);
            }

            logger.WriteToLog("Completed Provider Load");
            return provsToLoad;
        }

        public static Dictionary<string,int> createMeasDictionary(List<string> measures)
        {
            Dictionary<string, int> measDictionary = new Dictionary<string, int>();
            foreach (string measure in measures)
            {
                measDictionary.Add(measure, 0);
            }
            return measDictionary;
        }

        private static DataTable attemptIndividualMatch(DataRow prov, DataTable addressTable, DataTable nwprTable, DateTime reportDate, string year, bool annualRunFlag, DateTime firstOfYear, DateTime lastOfYear, Logger logger, string table)
        {
            DataTable fullProv = initProvTable(logger, table);
            Dictionary<DateTime, DateTime> addressDates = new Dictionary<DateTime, DateTime>();
            foreach (DataRow addr in addressTable.Select(string.Format("PRAD_ID = {0}", prov["PRPR_ID"].ToString())))
            {
                DateTime eff = DateTime.Parse(addr["PRAD_EFF_DT"].ToString());
                DateTime term = DateTime.Parse(addr["PRAD_TERM_DT"].ToString());
                if (eff <= firstOfYear && term >= lastOfYear)
                {
                    addProvRow(new object[] { prov["PRPR_ID"], prov["ProviderLastName"], prov["ProviderName"], addr["PRAD_ADDR1"], addr["PRAD_ADDR2"], addr["PRAD_ADDR3"], addr["PRAD_CITY"], addr["PRAD_STATE"]
                        , addr["PRAD_ZIP"], prov["PRCF_MCTR_SPEC"], prov["ProviderSpecDesc"], prov["PracticeName"], prov["MCTN_ID"],prov["PRPR_MCTR_TYPE"], reportDate/*.ToString("yyyy-MM-dd")*/, year, annualRunFlag.ToString(), true}, ref fullProv);
                    break;
                }
                else
                {
                    if (addressDates.ContainsKey(eff))
                    {
                        if (addressDates[eff] <= term)
                        {

                        }
                        else
                        {
                            addressDates[eff] = term;
                        }
                    }
                    else
                    {
                        addressDates.Add(eff, term);
                    }
                }
            }
            if (fullProv.Rows.Count == 0)
            {
                bool success = compressAndCompareDates(addressDates, firstOfYear, lastOfYear);
                if (success)
                {
                    DataView addrView = addressTable.DefaultView;
                    addrView.Sort = "PRAD_ID";
                    DataRow addr = addrView.ToTable().Rows[addressTable.Rows.Count - 1];
                    addProvRow(new object[] { prov["PRPR_ID"], prov["ProviderLastName"], prov["ProviderName"], addr["PRAD_ADDR1"], addr["PRAD_ADDR2"], addr["PRAD_ADDR3"], addr["PRAD_CITY"], addr["PRAD_STATE"]
                        , addr["PRAD_ZIP"], prov["PRCF_MCTR_SPEC"], prov["ProviderSpecDesc"], prov["PracticeName"], prov["MCTN_ID"],prov["PRPR_MCTR_TYPE"], reportDate/*.ToString("yyyy-MM-dd")*/, year, annualRunFlag.ToString(), true}, ref fullProv);
                }
                else
                {
                    return fullProv;
                }
            }

            bool nwprSuccess = false;
            Dictionary<DateTime, DateTime> nwprDates = new Dictionary<DateTime, DateTime>();
            foreach (DataRow nwpr in nwprTable.Select(string.Format("PRPR_ID = {0}", prov["PRPR_ID"].ToString())))
            {
                DateTime eff = DateTime.Parse(nwpr["NWPR_EFF_DT"].ToString());
                DateTime term = DateTime.Parse(nwpr["NWPR_TERM_DT"].ToString());
                if (eff <= firstOfYear && term >= lastOfYear)
                {
                    nwprSuccess = true;
                    break;
                }
                else
                {
                    if (nwprDates.ContainsKey(eff))
                    {
                        if (nwprDates[eff] <= term)
                        {

                        }
                        else
                        {
                            nwprDates[eff] = term;
                        }
                    }
                    else
                    {
                        nwprDates.Add(eff, term);
                    }
                }
            }
            if (nwprSuccess == false)
            {
                nwprSuccess = compressAndCompareDates(nwprDates, firstOfYear, lastOfYear);
                if (nwprSuccess == false)
                {
                    fullProv.Clear();
                }
            }
            return fullProv;
        }

        private static DataTable attemptMultiMatch(List<DataRow> provs, DataTable addressTable, DataTable nwprTable, DateTime reportDate, string year, bool annualRunFlag, DateTime firstOfYear, DateTime lastOfYear, Logger logger, string table)
        {
            DataTable fullProv = initProvTable(logger, table);
            Dictionary<DateTime, DateTime> addressDates = new Dictionary<DateTime, DateTime>();
            foreach (DataRow addr in addressTable.Rows)
            {
                DateTime eff = DateTime.Parse(addr["PRAD_EFF_DT"].ToString());
                DateTime term = DateTime.Parse(addr["PRAD_TERM_DT"].ToString());
                if (addressDates.ContainsKey(eff))
                {
                    if (addressDates[eff] <= term)
                    {

                    }
                    else
                    {
                        addressDates[eff] = term;
                    }
                }
                else
                {
                    addressDates.Add(eff, term);
                }
            }
            bool success = compressAndCompareDates(addressDates, firstOfYear, lastOfYear);
            if (success)
            {
                DataView addrView = addressTable.DefaultView;
                addrView.Sort = "PRAD_ID";
                DataRow addr = addrView.ToTable().Rows[addressTable.Rows.Count - 1];
                DataRow prov;
                try
                {
                    prov = provs.CopyToDataTable().Select(string.Format("PRPR_ID = {0}", addr["PRAD_ID"].ToString())).First();
                }
                catch
                {
                    return fullProv;//would only error if the PRPR ID doesn't match, should get picked up next iteration.
                }
                addProvRow(new object[] { prov["PRPR_ID"], prov["ProviderLastName"], prov["ProviderName"], addr["PRAD_ADDR1"], addr["PRAD_ADDR2"], addr["PRAD_ADDR3"], addr["PRAD_CITY"], addr["PRAD_STATE"]
                    , addr["PRAD_ZIP"], prov["PRCF_MCTR_SPEC"], prov["ProviderSpecDesc"], prov["PracticeName"], prov["MCTN_ID"],prov["PRPR_MCTR_TYPE"], reportDate/*.ToString("yyyy-MM-dd")*/, year, annualRunFlag.ToString(), true}, ref fullProv);
            }
            else
            {
                return fullProv;
            }

            bool nwprSuccess = false;
            Dictionary<DateTime, DateTime> nwprDates = new Dictionary<DateTime, DateTime>();
            foreach (DataRow nwpr in nwprTable.Rows)
            {
                DateTime eff = DateTime.Parse(nwpr["NWPR_EFF_DT"].ToString());
                DateTime term = DateTime.Parse(nwpr["NWPR_TERM_DT"].ToString());
                if (eff <= firstOfYear && term >= lastOfYear)
                {
                    nwprSuccess = true;
                    break;
                }
                else
                {
                    if (nwprDates.ContainsKey(eff))
                    {
                        if (nwprDates[eff] <= term)
                        {

                        }
                        else
                        {
                            nwprDates[eff] = term;
                        }
                    }
                    else
                    {
                        nwprDates.Add(eff, term);
                    }
                }
            }
            if (nwprSuccess == false)
            {
                nwprSuccess = compressAndCompareDates(nwprDates, firstOfYear, lastOfYear);
                if (nwprSuccess == false)
                {
                    fullProv.Clear();
                }
            }
            return fullProv;
        }

        private static bool compressAndCompareDates(Dictionary<DateTime, DateTime> dates, DateTime firstOfYear, DateTime lastOfYear)
        {
            List<DateTime> eff = dates.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value).Keys.ToList();
            List<DateTime> term = dates.Values.ToList();

            DateTime start = new DateTime();
            DateTime end = new DateTime();
            foreach (DateTime effDT in eff)
            {
                if (start == DateTime.MinValue || start == null)
                {
                    start = effDT;
                    end = dates[effDT];
                    continue;
                }
                if (effDT <= end && effDT >= start && dates[effDT] > end)
                {
                    end = dates[effDT];
                }
                else if (end < DateTime.MaxValue.AddDays(-1))
                {
                    if (effDT <= end.AddDays(1.5) && effDT >= end.AddDays(.5))
                    {
                        end = dates[effDT];
                    }
                }
                else if (effDT < start)
                {
                    start = effDT;
                }
            }

            if (start <= firstOfYear && end >= lastOfYear)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private static void addProvRow(object[] prov, ref DataTable provs)
        {
            provs.Rows.Add();
            provs.Rows[0]["PCPId"] = prov[0];
            provs.Rows[0]["ProviderLastName"] = prov[1];
            provs.Rows[0]["ProviderName"] = prov[2];
            provs.Rows[0]["AddressLine1"] = prov[3];
            provs.Rows[0]["AddressLine2"] = prov[4];
            provs.Rows[0]["AddressLine3"] = prov[5];
            provs.Rows[0]["City"] = prov[6];
            provs.Rows[0]["State"] = prov[7];
            provs.Rows[0]["Zip"] = prov[8];
            provs.Rows[0]["ProviderSpec"] = prov[9];
            provs.Rows[0]["ProviderSpecDesc"] = prov[10];
            provs.Rows[0]["GroupName"] = prov[11];
            provs.Rows[0]["ProviderTaxID"] = prov[12];
            provs.Rows[0]["ProviderType"] = prov[13];
            provs.Rows[0]["ReportDate"] = prov[14];
            provs.Rows[0]["ReportYear"] = prov[15];
            provs.Rows[0]["AnnualRun"] = prov[16];
            provs.Rows[0]["IsActive"] = prov[17];
            provs.Rows[0]["DateCreated"] = DateTime.Now;
            provs.Rows[0]["DateModified"] = DateTime.Now;
            provs.Rows[0]["TotalMembers"] = 0;
            provs.Rows[0]["TotalPoints"] = 0;
            provs.Rows[0]["TotalMeasuresApplicable"] = 0;
            provs.Rows[0]["TotalMeasuresMet"] = 0;
            provs.Rows[0]["CompositeScore"] = 0;
            provs.Rows[0]["PMPMAmount"] = 0;
            provs.Rows[0]["TotalReward"] = 0;

            provs.AcceptChanges();
        }

        private static DataTable initProvTable(Logger logger, string table)
        {
            DataTable provTable = ExtractFactory.ConnectAndQuery(logger.LoggerPhpArchive, @"SELECT TOP(0) ProviderKey,[PCPId],[ProviderLastName],[ProviderName],[AddressLine1],
                    [AddressLine2],[AddressLine3],[City],[State],[Zip],[ProviderSpec],[ProviderSpecDesc],[GroupName],[ProviderTaxID],[ProviderType],[ReportDate],[ReportYear],
                    [TotalMembers],[TotalPoints],
                    [TotalMeasuresApplicable],[TotalMeasuresMet],[CompositeScore],[RewardBracketKey],[PMPMAmount],[TotalReward],[AnnualRun],[IsActive],[DateCreated],
                    [DateModified] FROM [PHPArchv]." + table);
            return provTable;
        }

        private static string provQOne(string year)
        {
            return string.Format(@"", year);
        }
        private static string provQTwo(List<DataRow> provIDs, string year)
        {
            string strProvs = "";
            foreach (DataRow prov in provIDs)
            {
                strProvs += prov["PRPR_ID"].ToString() + ",";
            }
            strProvs = strProvs.Substring(0, strProvs.Length - 1);
            return string.Format(@"", strProvs, year);
        }
        private static string provQThree(List<DataRow> provIDs, string year)
        {
            string strProvs = "";
            foreach (DataRow prov in provIDs)
            {
                strProvs += prov["PRPR_ID"].ToString() + ",";
            }
            strProvs = strProvs.Substring(0, strProvs.Length - 1);
            return string.Format(@")", strProvs, year);
        }

        public class Provider
        {
            public List<Member> Members { get; set; }
            public int ProviderKey { get; set; }
            public string PCPId { get; set; }
            public string ProviderLastName { get; set; }
            public string ProviderName { get; set; }
            public string AddressLine1 { get; set; }
            public string AddressLine2 { get; set; }
            public string AddressLine3 { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public string Zip { get; set; }
            public string ProviderSpec { get; set; }
            public string ProviderSpecDesc { get; set; }
            public string GroupName { get; set; }
            public string ProviderTaxID { get; set; }
            public string ProviderType { get; set; }
            public DateTime ReportDate { get; set; }
            public int ReportYear { get; set; }
            public int TotalMembers { get; set; }
            public Dictionary<string, int> Eligible_Records { get; set; }
            public Dictionary<string, int> Total_Records { get; set; }
            public Dictionary<string, int> _MeasureKey { get; set; }
            public Dictionary<string, int> _Points { get; set; }
            public int TotalPoints { get; set; }
            public int TotalMeasuresApplicable { get; set; }
            public int TotalMeasuresMet { get; set; }
            public double CompositeScore { get; set; }
            public int RewardBracketKey { get; set; }
            public double PMPMAmount { get; set; }
            public double TotalReward { get; set; }
            public string AnnualRun { get; set; }
            public string IsActive { get; set; }
            public DateTime DateCreated { get; set; }
            public DateTime DateModified { get; set; }
        }

        public class Member
        {
            public List<Claim> Claims { get; set; }
            //public List<FacetsClaim> FacetsClaims { get; set; }
            //public List<PharmacyClaim> PharmacyClaims { get; set; }
            public int MemberKey { get; set; }
            public string PCPId { get; set; }
            public int MEM_NBR { get; set; }
            public string PAT_ID { get; set; }
            public DateTime ReportDate { get; set; }
            public int ReportYear { get; set; }
            public DateTime DateOfBirth { get; set; }
            public int RunDateAge { get; set; }
            public string MemberGender { get; set; }
            public int MinAgeDuringReportYear { get; set; }
            public int MaxAgeDuringReportYear { get; set; }
            public Dictionary<string, int> has_Denominator { get; set; }
            public Dictionary<string, int> has_Numerator { get; set; }
            public string AnnualRun { get; set; }
            public int IsActive { get; set; }
            public DateTime DateCreated { get; set; }
            public DateTime DateModified { get; set; }
        }

        public class Claim
        {
            public DateTime QUERY_DATE { get; set; }
            public string REVIEWSETID { get; set; }
            public string MEM_NBR { get; set; }
            public string MEM_LNAME { get; set; }
            public string MEM_FNAME { get; set; }
            public DateTime MEM_DOB { get; set; }
            public string PAT_ID { get; set; }
            public string PROV_NBR { get; set; }
            public string PROV_LNAME { get; set; }
            public string PROV_FNAME { get; set; }
            public string PROV_MNAME { get; set; }
            public string PRODUCT_ROLLUP_ID { get; set; }
            public DateTime SERV_DT { get; set; }
            public string NUM { get; set; }
            public string CLAIM_ID { get; set; }
            public string MEASURE { get; set; }
            public string MEASURE_ABBR { get; set; }
            public int DENOMINATOR { get; set; }
            public int NUMERATOR { get; set; }
            public int YearMonth { get; set; }
            public DateTime Insert_Date { get; set; }
        }
    }
}
