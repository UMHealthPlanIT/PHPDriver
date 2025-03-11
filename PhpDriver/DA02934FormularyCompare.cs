using ClosedXML.Excel;
using Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

/* https://phpmi.atlassian.net/wiki/spaces/DesignSpecifications/pages/71303269/Formulary+Maintenance+Tool */

namespace Driver
{
    class DA02934FormularyCompare : Logger, IPhp
    {
        DateTime ArchiveDate = DateTime.Today;

        public DA02934FormularyCompare(LaunchRequest Program)
            : base(Program)
        {
            return;
        }

        private enum runModes { ImportOnly, CompareOnly, FullRun, RefreshTest, AcceptEverything, RandoTest, AcceptGap45 }

        public bool Initialize(string[] args)
        {
            string runDate = ArchiveDate.ToString("MM_yyyy"); //For Imports, designates when we put it in
            string refDate = ""; //Empty to use Config's 'master' data or rolled back to declare a new 'master' (newer)
            string compDate = ""; //Empty to use the most recent pulled in data, otherwise date to look back to for compare (older)

            runModes mode = runModes.FullRun;
            if (args.Length == 2)
            {
                try
                {
                    ArchiveDate = DateTime.Parse(args[1]);
                }
                catch
                {
                    try
                    {
                        //Enum.TryParse(args[1], out mode)
                        mode = (runModes)Enum.Parse(typeof(runModes), args[1]);
                    }
                    catch
                    {
                        throw new Exception("Invalid input parameter " + args[1]);
                    }
                }
            }
            else if (args.Length == 3)
            {
                ArchiveDate = DateTime.Parse(args[1]);
                mode = (runModes)Enum.Parse(typeof(runModes), args[2]);
            }
            else if (args.Length == 4)
            {
                mode = (runModes)Enum.Parse(typeof(runModes), args[1]);
                refDate = args[2];
                compDate = args[3];
                if (mode != runModes.CompareOnly)
                {
                    throw new Exception("Invalid input parameter: can only do archival compare on a compare only run.");
                }
                WriteToLog("Running a Compare only");
            }

            if (mode == runModes.FullRun || mode == runModes.ImportOnly)
            {
                importFormularies(runDate);
            }
            if (mode == runModes.FullRun || mode == runModes.CompareOnly)
            {
                compareFormularies(compDate, refDate);
            }
            if(mode == runModes.FullRun || mode == runModes.AcceptGap45) //Pull from full run if Gap45 compare is implemented
            {
                FormularyUtilities.acceptGap45(this);
            }
            if (mode == runModes.RefreshTest)
            {
                FormularyUtilities.refreshTest(this);
            }
            if(mode == runModes.AcceptEverything)
            {
                FormularyUtilities.masterAcceptAll(this);
                compareFormularies(compDate, refDate);
            }
            if (mode == runModes.RandoTest)
            {
                //List<FormularyUtilities.FullItem> x = FormularyUtilities.compareResultByPk("256912", this);
                //List<FormularyUtilities.FullItem> y =  FormularyUtilities.compareResultByPk("276069", this);
                DataTable z = FormularyUtilities.getNDCNames(this);
            }
            return true;
        }

        //List<string> formularies = new List<string>() { "6T", "Sparrow 4T", "Sparrow 3T", "Exchange", "Commercial" };
        Dictionary<string, string> forumlaryMap = new Dictionary<string, string>() { { "6TIER", "6T" }, { "6 TIER", "6T" }, { "4T", "4T" }, { "3T", "3T" }, { "EXC", "Exchange" }, { "COMM", "Commercial" } };
        List<string> tables = new List<string>() { "C17", "C18", "PA", "UM", "NDC", "Gap45", "GPI_Tier", "GPI" };
        //Dictionary<string, string> keyMap = new Dictionary<string, string>() { { "C17", "C17_PK" }, { "C18", "C18_PK" }, { "PA", "PA_PK" }, { "UM", "UM_PK" }, { "NDC", "NDC_PK" }, { "Gap45", "GAP45_PK" }, { "GPI_Tier", "GPI_TIER_PK" }, {"GPI", "GPI_LINE_PK" } };
        List<string> JustGPIMappingExcel = new List<string>() { "FGD Generic Product ID", "Formulary", "ArchiveDate" };
        List<string> JustGPIMappingDB = new List<string>() { "GPI", "Formulary", "ArchiveDate" };

        List<string> GPIMappingsExcel = new List<string>() { "FGL GPI List Name", "FGD Generic Name", "FROMDATE", "THRUDATE", "B/G Ind M", "B/G Ind O", "B/G Ind N", "B/G Ind Y", "GPI_LINE_PK", "ArchiveDate" };
        List<string> GPIMappingsDB = new List<string>() { "Tier", "FGD_Generic_Name", "Effective_Date", "Term_Date", "TIER_M", "TIER_O", "TIER_N", "TIER_Y", "GPI_LINE_PK", "ArchiveDate" };

        List<string> PAandCMappingsExcel = new List<string>() { "BGINDM", "BGINDO", "BGINDN", "BGINDY", "RX", "OTC", "MDD", "GPI_LINE_PK", "PGL GPI List Name", "CUREFFDATE", "CURTRMDATE", "ArchiveDate" };
        List<string> PAMappingsDB = new List<string>() { "PA_M", "PA_O", "PA_N", "PA_Y", "PA_RX", "PA_OTC", "PA_MDD", "GPI_LINE_PK", "PA_TIER", "PA_EFF_DT", "PA_TERM_DT", "ArchiveDate" };
        List<string> C17MappingsDB = new List<string>() { "C17_M", "C17_O", "C17_N", "C17_Y", "C17_RX", "C17_OTC", "C17_MDD", "GPI_LINE_PK", "C17_TIER", "C17_EFF_DT", "C17_TERM_DT", "ArchiveDate" };
        List<string> C18MappingsDB = new List<string>() { "C18_M", "C18_O", "C18_N", "C18_Y", "C18_RX", "C18_OTC", "C18_MDD", "GPI_LINE_PK", "C18_TIER", "C18_EFF_DT", "C18_TERM_DT", "ArchiveDate" };

        List<string> NDCMappingsExcel = new List<string>() { "FNL NDC List Name", "NDC_DRUG", "FROMDATE", "THRUDATE", "FND Current Status", "B/G Ind M", "B/G Ind O",
            "B/G Ind N", "B/G Ind Y", "RX", "OTC", "FND Label Name", "Formulary", "ArchiveDate" };
        List<string> NDCMappingsDB = new List<string>() { "NDC_TIER", "NDC", "NDC_EFF_DT", "NDC_TERM_DT", "FND_CUR_STS", "NDC_M", "NDC_O",
            "NDC_N", "NDC_Y", "NDC_RX", "NDC_OTC", "LABEL_NAME", "Formulary", "ArchiveDate" };

        List<string> UMMappingsExcel = new List<string>() { "DRUG_STS", "REC_STS", "QTY_MIN", "QTY_MAX",
            "DAYS_MIN", "DAYS_MAX", "QTY_VS_DS","AGE_MIN", "AGE_MAX", "DDOSE_MIN", "DDOSE_MAX", "DDOSE_QUAL", "ACUTE_MAX", "MAINT_MAX", "RESTNDC", "RESTNDCLST", "MONY_M", "MONY_O",
            "MONY_N", "MONY_Y", "RX", "OTC", "MDD", "PRDQTYTYPE", "PRDQTYDAYS", "PRDQTYMAX", "PRDDSTYPE", "PRDDSDAYS", "PRD_DS_MAX", "PRD_QTY_DS", "PRDFILLTYP", "PRDFILLDAY",
            "PRDFILLMAX", "AMTDUETYPE", "AMTDUEDAYS", "AMTDUEMAX", "AMTDUEBASS", "PTD_FROM", "DL_PTD_EDT", "DDOSE_MDDL", "PRDDOSEDAY", "PRDDOSEMAX", "CONSEC_DS", "HIST_DATE",
            "HIST_ROLL", "MSG_CODE", "MSG_TYPE", "MESSAGE", "SUPPRS_DEA", "SEX_EXCLUD", "OTC_OR", "FAM_COVER", "PCKG_EXCEP", "USE_RESET", "FDA_EQUIV", "PREF_LABEL", "PREF_PROD",
            "PREF_PCKG", "ROA", "ROA_LIST", "MAINT_STS", "FORMALTGPI", "BRD_GEN", "OR_NONLABL", "ADMIN_FEE", "USE_SMART", "DURATION", "PKG_QTY_MIN", "PKG_QTY_MAX", "COMP_ADMIN", "GPI_LINE_PK", "LIST_ID", "Effective", "Term", "ArchiveDate" };
        List<string> UMMappingsDB = new List<string>() { "UM_DRUG_STS", "UM_REC_STS", "UM_QTY_MIN", "UM_QTY_MAX",
            "UM_DAYS_MIN", "UM_DAYS_MAX", "UM_QTY_VS_DS","UM_AGE_MIN", "UM_AGE_MAX", "UM_DDOSE_MIN", "UM_DDOSE_MAX", "UM_DDOSE_QUAL", "UM_ACUTE_MAX", "UM_MAINT_MAX", "UM_RESTNDC", "UM_RESTNDCLIST", "UM_M", "UM_O",
            "UM_N", "UM_Y", "UM_RX", "UM_OTC", "UM_MDD", "UM_PRDQTYTYPE", "UM_PRDQTYDAYS", "UM_PRDQTYMAX", "UM_PRDDSTYPE", "UM_PRDDSDAYS", "UM_PRD_DS_MAX", "UM_PRD_QTY_DS", "UM_PRDFILLTYP", "UM_PRDFILLDAY",
            "UM_PRDFILLMAX", "UM_AMTDUETYPE", "UM_AMTDUEDAYS", "UM_AMTDUEMAX", "UM_AMTDUEBASS", "UM_PTD_FROM", "UM_DL_PTD_EDT", "UM_DDOSE_MDL", "UM_PRDDOSEDAY", "UM_PRDDOSEMAX", "UM_CONSEC_DS", "UM_HIST_DATE",
            "UM_HIST_ROLL", "UM_MSG_CODE", "UM_MSG_TYPE", "UM_MESSAGE", "UM_SUPPRS_DEA", "UM_SEX_EXCLUD", "UM_OTC_OR", "UM_FAM_COVER", "UM_PCKG_EXCEP", "UM_USE_RESET", "UM_FDA_EQUIV", "UM_PREF_LABEL", "UM_PREF_PROD",
            "UM_PREF_PCKG", "UM_ROA", "UM_ROA_LIST", "UM_MAINT_STS", "UM_FORMALTGPI", "UM_BRD_GEN", "UM_OR_NONLABL", "UM_ADMIN_FEE", "UM_USE_SMART", "UM_DURATION", "UM_PKG_QTY_MIN", "UM_PKG_QTY_MAX", "UM_COMP_ADMIN", "GPI_LINE_PK", "UM_TIER", "UM_EFF_DT", "UM_TERM_DT", "ArchiveDate"};

        List<string> Gap45MappingsExcel = new List<string>() {"NDC", "Inactive NDC", "Obsolete Date", "Label Name", "Product Name",
            "Medispan Multi-Source Code", "Rx Indicator", "MEDD Indicator", "Therapy Code", "Therapy Code Description", "Drug Status Effective Date", "PA Flag", "Smart PA Flag",
            "Step Therapy Flag", "Age Flag", "Gender Flag", "QL Flag", "Age Range Flag", "Sex Exclusion", "Qty Min", "Qty Max", "DS Min", "DS Max", "Qty/Ds Comp",
            "Period Qty Type", "Period Qty Days", "Period Qty Max", "Period DS Type", "Period DS Days", "Period DS Max", "Period Qty/Ds Comp", "Period Fills Type",
            "Period Fills Days", "Period Fills Max", "Refill Limit Max Number", "Refill Expire After Days", "Amt Due Type", "Amt Due Days", "Amt Due Max", "Amt Due Basis",
            "Patient Age Min", "Patient Age Max", "DD Min", "DD Max", "Acute Dose Days Max", "Maint Dose Days Max", "Otc Override", "Unit Dose Use", "FDA Therapeutic Equiv",
            "Route Of Admin", "Maint Drug STS", "Brand Generic Edit", "PTD From", "Limit Days Ovr", "Refill Code Ovr", "Days Supply Fill Limit", "Formulary Id",
            "Formulary Source Code","Formulary Flag","Preferred Drug List","Formulary Compliance Code","Tier Number Display","Tier Total Display","Tier Code","Product Type", "GPI_LINE_PK", "ArchiveDate"};
        List<string> Gap45MappingsDB = new List<string>() {"NDC", "Inactive_NDC", "Obsolete_Date", "Label_Name", "Product_Name",
            "Multi_Source_Code", "Rx_Indicator", "MEDD_Indicator", "Therapy_Code", "Therapy_Code_Description", "Drug_Status_Effective_Date", "PA_Flag", "Smart_PA_Flag",
            "Step_Therapy_Flag", "Age_Flag", "Gender_Flag", "QL_Flag", "Age_Range_Flag", "Sex_Exclusion", "Qty_Min", "Qty_Max", "DS_Min", "DS_Max", "Qty_Ds_Comp",
            "Period_Qty_Type", "Period_Qty_Days", "Period_Qty_Max", "Period_DS_Type", "Period_DS_Days", "Period_DS_Max", "Period_Qty_Ds_Comp", "Period_Fills_Type",
            "Period_Fills_Days", "Period_Fills_Max", "Refill_Limit_Max_Number", "Refill_Expire_After_Days", "Amt_Due_Type", "Amt_Due_Days", "Amt_Due_Max", "Amt_Due_Basis",
            "Patient_Age_Min", "Patient_Age_Max", "DD_Min", "DD_Max", "Acute_Dose_Days_Max", "Maint_Dose_Days_Max", "Otc_Override", "Unit_Dose_Use", "FDA_Therapeutic_Equiv",
            "Route_Of_Admin", "Maint_Drug_STS", "Brand_Generic_Edit", "PTD_From", "Limit_Days_Ovr", "Refill_Code_Ovr", "Days_Supply_Fill_Limit", "Formulary_Id",
            "Formulary_Source_Code","Formulary_Flag","Preferred_Drug_List","Formulary_Compliance_Code","Tier_Number_Display","Tier_Total_Display","Tier_Code","Product_Type", "GPI_LINE_PK", "ArchiveDate"};

        public void loadGap45(string fileName)
        {
            this.WriteToLog("Processing Gap45");
            string formulary = getFormulary(fileName).Replace("Gap45", "");
            this.WriteToLog("File: " + fileName);
            this.WriteToLog("Formulary: " + formulary);

            DataTable GPIWithPK = DataWork.QueryToDataTable(string.Format("SELECT * FROM WEB0020.GPI_ WHERE ArchiveDate = '{0}'", ArchiveDate.ToString("yyyy-MM-dd")), this.LoggerPhpArchive);
            DataTable Gap45 = GetFormularyData(fileName, this, "Sheet 1");
            DataTable Gap45ToGo = GetGPILinePK(Gap45, GPIWithPK, "GPI", formulary, fileName);
            DataWork.SaveDataTableToDb("WEB0020.Gap45_", Gap45ToGo, this.LoggerPhpArchive, Gap45MappingsExcel, Gap45MappingsDB);
        }

        public void loadFormulary(string fileName)
        {
            string formulary = getFormulary(fileName);

            List<string> GPISheets = new List<string>();
            List<string> NDCSheets = new List<string>();

            this.WriteToLog("File: " + fileName);
            this.WriteToLog("Formulary: " + formulary);
            this.WriteToLog("GPIs: " + string.Join(" ", GPISheets));
            this.WriteToLog("NDCs: " + string.Join(" ", NDCSheets));

            //get just GPI + formulary for DB insert
            DataTable allGPIs = new DataTable();
            int count = 0;
            foreach (string sheet in GPISheets)
            {
                if (count == 0)
                {
                    allGPIs = GetGPIData(fileName, sheet, formulary);
                    allGPIs.AcceptChanges();
                }
                else
                {
                    allGPIs.Merge(GetGPIData(fileName, sheet, formulary));
                    allGPIs.AcceptChanges();
                }
                this.WriteToLog("Loaded sheet " + sheet + " for base GPI");
                count++;
            }
            DataTable distinctGPIs = allGPIs.AsEnumerable().GroupBy(x => x.Field<string>("FGD Generic Product ID")).Select(y => y.First()).CopyToDataTable();//maybe optimize here?
            distinctGPIs.AcceptChanges();
            Debug.WriteLine(distinctGPIs.Rows.Count);
            DataWork.SaveDataTableToDb("WEB0020.GPI_", distinctGPIs, this.LoggerPhpArchive, JustGPIMappingExcel, JustGPIMappingDB);

            //get back GPI + PK so you can update other tables
            DataTable GPIWithPK = DataWork.QueryToDataTable(string.Format("SELECT * FROM WEB0020.GPI_ WHERE Formulary = '{0}' AND ArchiveDate = '{1}'", formulary, ArchiveDate.ToString("yyyy-MM-dd")), this.LoggerPhpArchive);

            GPISheets.Remove("C17");
            GPISheets.Remove("C18");
            GPISheets.Remove("UM");
            GPISheets.Remove("PA");

            //🎵go back, Jack, and do it again🎵
            //load the tiers of GPIs now
            foreach (string sheet in GPISheets)
            {
                this.WriteToLog("Loading sheet " + sheet + " for tiers");
                DataTable GPIWithTier = GetFormularyData(fileName, this, sheet);
                DataTable GPIWithTierToGo = GetGPILinePK(GPIWithTier, GPIWithPK, "FGD Generic Product ID", formulary, fileName);
                for (int i = 0; i < GPIWithTierToGo.Rows.Count; i++)
                {
                    DataRow row = GPIWithTierToGo.Rows[i];
                    row["FROMDATE"] = OtherDateToDateTime(row["FROMDATE"].ToString()).ToString();
                    row["THRUDATE"] = OtherDateToDateTime(row["THRUDATE"].ToString()).ToString();
                }
                GPIWithTierToGo.AcceptChanges();
                DataWork.SaveDataTableToDb("WEB0020.GPI_Tier_", GPIWithTierToGo, this.LoggerPhpArchive, GPIMappingsExcel, GPIMappingsDB);
            }

            loadSatelliteTable(fileName, "PA", "PGO Generic Product ID", GPIWithPK, formulary, PAandCMappingsExcel, PAMappingsDB);
            loadSatelliteTable(fileName, "C17", "PGO Generic Product ID", GPIWithPK, formulary, PAandCMappingsExcel, C17MappingsDB);
            loadSatelliteTable(fileName, "C18", "PGO Generic Product ID", GPIWithPK, formulary, PAandCMappingsExcel, C18MappingsDB);

            this.WriteToLog("Loading NDCs");
            foreach (string sheet in NDCSheets)
            {
                this.WriteToLog("Loading " + sheet);
                DataTable NDC = GetFormularyData(fileName, this, sheet, formulary);
                if(NDC.Rows.Count > 1) //As long as the sheet isnt headers only process
                {
                    NDC = ChangeColumnDataTypeWithData(NDC, "FROMDATE", typeof(DateTime));
                    NDC = ChangeColumnDataTypeWithData(NDC, "THRUDATE", typeof(DateTime));
                    DataWork.SaveDataTableToDb("WEB0020.NDC_", NDC, this.LoggerPhpArchive, NDCMappingsExcel, NDCMappingsDB);
                    this.WriteToLog("Loaded " + sheet);
                }
                else
                {
                    this.WriteToLog("Empty NDC Sheet for: " + sheet);
                }
            }
            this.WriteToLog("Loaded NDCS");

            this.WriteToLog("Loading UM");
            DataTable UM = GetFormularyData(fileName, this, "UM");
            UM = ChangeColumnDataTypeIfEmpty(UM, "ADMIN_FEE", typeof(decimal));
            UM = ChangeColumnDataTypeIfEmpty(UM, "QTY_VS_DS", typeof(decimal));
            UM = ChangeColumnDataTypeIfEmpty(UM, "AMTDUETYPE", typeof(decimal));
            UM = ChangeColumnDataTypeIfEmpty(UM, "AMTDUEBASS", typeof(decimal));
            UM = ChangeColumnDataTypeIfEmpty(UM, "PRD_QTY_DS", typeof(int));
            DataTable UMReadToGo = GetGPILinePK(UM, GPIWithPK, "GPI", formulary, fileName);
            DataWork.SaveDataTableToDb("WEB0020.UM_", UMReadToGo, this.LoggerPhpArchive, UMMappingsExcel, UMMappingsDB);
            this.WriteToLog("Loaded UM");
            this.WriteToLog("Loaded " + formulary);
        }

        public void loadSatelliteTable(string fileName, string tableName, string gpiName, DataTable GPIWithPK, string formulary, List<string> sourceMappings, List<string> destMappings)
        {
            this.WriteToLog("Loading " + tableName);
            DataTable table = GetFormularyData(fileName, this, tableName);
            DataTable tableToGo = GetGPILinePK(table, GPIWithPK, gpiName, formulary, fileName);
            DataWork.SaveDataTableToDb("WEB0020." + tableName + "_", tableToGo, this.LoggerPhpArchive, sourceMappings, destMappings);
            this.WriteToLog("Loaded " + tableName);
        }

        public string getFormulary(string fileName)
        {
            string formulary = "";
            foreach (string key in forumlaryMap.Keys)
            {
                if (fileName.ToUpper().Contains(key))
                {
                    formulary = forumlaryMap[key];
                    return formulary;
                }
            }
            return formulary;
        }

        public void importFormularies(string runDate)
        {

            //List<string> files = System.IO.Directory.GetFiles(this.LoggerStagingDir, "*.xlsx").OrderByDescending(f => f).ToList();
            List<string> files = FileSystem.GetInputFiles(this, "", runDate, "/Formulary_Sheets", deleteAfterDownload: TestMode ? false : true);
            List<string> gap45Files = files.Where(x => x.Contains("Gap45")).ToList();
            if (files.Count == 0)
            {
                SendAlerts.Send(ProcessId, 6000, "Formulary Files Not Found", "Formularies either weren't found on time or weren't in .xlsx format.", this);
                throw new Exception("Formularies either weren't found on time or weren't in .xlsx format.");
                //Kari, Denise
            }
            else if(gap45Files.Count == 0)
            {
                SendAlerts.Send(ProcessId, 6000, "Gap45 Formulary Files Not Found", "Gap45 formularies either weren't found on time or weren't in .xlsx format.", this);
            }
            files.RemoveAll(x => x.Contains("Gap45"));

            foreach (string file in files)
            {
                loadFormulary(file);
                FtpFactory.ArchiveFile(this, file);
            }
            foreach (string file in gap45Files)
            {
                loadGap45(file);
                FtpFactory.ArchiveFile(this, file);
            }

        }

        /// <summary>
        /// Compares two formularies based on dates provided and generates error reports and populates WEB0020.CompareResults for front end consumption.
        /// </summary>
        /// <param name="olderDate">"yyyy-mm-dd" look back to this date for compare</param>
        /// <param name="newerDate">"yyyy-mm-dd" blank then use master config tables otherwise declare a new master</param>
        private void compareFormularies(string olderDate = "", string newerDate = "")
        {
            bool cfgAsMaster = (newerDate == ""); //if passing in newer date then we are only dealing with historical compares, this changes which DB we look at

            FormularyUtilities.Formulary form = new FormularyUtilities.Formulary
            {
                GPIs = new ConcurrentBag<FormularyUtilities.FullItem>(),
                NDCs = new ConcurrentBag<FormularyUtilities.FullItem>()
            };

            if (cfgAsMaster)
            {
                //newer date stays blank
                olderDate = FormularyUtilities.getArchvDate(this, true);//newest archived data
            }
            else
            {
                newerDate = FormularyUtilities.getArchvDate(this, true, $"'{newerDate}'");
                olderDate = FormularyUtilities.getArchvDate(this, true, olderDate == "" ? $"DATEADD(DD, -1, '{newerDate}')" : $"'{olderDate}'");//if no older date given get first one before newer date provided  
            }
            WriteToLog($"Config DB set as our Master? {cfgAsMaster}");
            WriteToLog($"Master/New Date Set As: {newerDate} \n Compare/Old Date Set As: {olderDate}");

            //GPIs - Source of truth of distinct list of GPIs in our Master or designated master compare
            List<string> masterGPIs = ExtractFactory.ConnectAndQuery(this, LoggerPhpConfig, cfgAsMaster ? $"SELECT GPI FROM WEB0020.GPI Union Select GPI From PHPArchv.WEB0020.GPI_ Where ArchiveDate = '{olderDate}'" : //Need to pull from both sides, master and archive for adds/drops
                        $"SELECT GPI FROM PHPArchv.WEB0020.GPI_ WHERE ArchiveDate = '{newerDate}' Union SELECT GPI FROM PHPArchv.WEB0020.GPI_ WHERE ArchiveDate = '{olderDate}'").AsEnumerable().Select(x => x.Field<string>("GPI")).ToList();//need to take top 100 out


            ParallelOptions opt = new ParallelOptions();
            opt.MaxDegreeOfParallelism = 12;//Put this back when done with testing
            //opt.MaxDegreeOfParallelism = 1;//Pull this out when done with testing

            WriteToLog($"Starting full GPI compare on {masterGPIs.Count} GPIs");
            int count = 0;
            
            Parallel.ForEach(masterGPIs, opt, masterGPI => //This is for each unique GPI
            {
                Parallel.ForEach(FormularyUtilities.compareByGPI(masterGPI, this, olderDate, newerDate, cfgAsMaster), fGPI => //This is for each full item within that GPI
                {
                    form.GPIs.Add(fGPI);
                });
                count++;
                if (count % 250 == 0)
                {
                    WriteToLog($"{count} GPIs Complete Out of {masterGPIs.Count}");
                }
            });
            WriteToLog("Complete with GPI Compares");

            
            ////////////////////////////Starting NDC Compares now!!!////////////////////////////////////////////
            List<string> masterNDCs = ExtractFactory.ConnectAndQuery(this, LoggerPhpConfig, cfgAsMaster ? $"SELECT NDC FROM WEB0020.NDC Union Select NDC From PHPArchv.WEB0020.NDC_ Where ArchiveDate = '{olderDate}'" : //Need to pull from both sides, master and archive for adds/drops
                        $"SELECT NDC FROM PHPArchv.WEB0020.NDC_ WHERE ArchiveDate = '{newerDate}' Union SELECT NDC FROM PHPArchv.WEB0020.NDC_ WHERE ArchiveDate = '{olderDate}'").AsEnumerable().Select(x => x.Field<string>("NDC")).ToList();

            count = 0;
            WriteToLog($"Starting full NDC compare on {masterNDCs.Count} NDCs");
            Parallel.ForEach(masterNDCs, opt, NDC =>
             {
                 Parallel.ForEach(FormularyUtilities.compareByNDC(NDC, this, olderDate, newerDate,cfgAsMaster), fNDC =>
                 {
                     form.NDCs.Add(fNDC);
                 });
                 count++;
                 if (count % 250 == 0)
                 {
                     WriteToLog($"{count} NDCs Complete Out of {masterNDCs.Count}");
                 }
             });


            WriteToLog("Compare complete.");

            if (form.GPIs.Where(x => x.Passed == false).Count() > 0 || form.NDCs.Where(x => x.Passed == false).Count() > 0)
                form.Passed = false;
            else
                form.Passed = true;

            WriteToLog("Formulary passed = " + form.Passed.ToString());

            List<FormularyUtilities.CompareItem> erroredItems = new List<FormularyUtilities.CompareItem>();
            List<FormularyUtilities.ItemCompareFlattened> errorsFlat = new List<FormularyUtilities.ItemCompareFlattened>();

            WriteToLog("Finding errored items.");
            WriteToLog("Starting GPI Search.");

            foreach (FormularyUtilities.FullItem erroredItem in form.GPIs.Where(x => x.Passed == false).AsEnumerable())
            {
                string othFormulary = erroredItem.OtherItems.Where(x => x.Column.ToUpper() == "FORMULARY").Select(x => x.Item).FirstOrDefault().ToString();

                string phpFormulary = erroredItem.PhpItems.Where(x => x.Column.ToUpper() == "FORMULARY").Select(x => x.Item).FirstOrDefault().ToString();

                string otherTier = erroredItem.OtherItems.Where(x => FormularyUtilities.Tiers.Any(y => y.ToUpper() == x.Column.ToUpper())).Select(x => x.Item).FirstOrDefault() == null
                    ? "" : erroredItem.OtherItems.Where(x => FormularyUtilities.Tiers.Any(y => y.ToUpper() == x.Column.ToUpper())).Select(x => x.Item).FirstOrDefault().ToString();

                string phpTier = erroredItem.PhpItems.Where(x => FormularyUtilities.Tiers.Any(y => y.ToUpper() == x.Column.ToUpper())).Select(x => x.Item).FirstOrDefault() == null
                    ? "" : erroredItem.PhpItems.Where(x => FormularyUtilities.Tiers.Any(y => y.ToUpper() == x.Column.ToUpper())).Select(x => x.Item).FirstOrDefault().ToString();

                string otherNdc = erroredItem.OtherItems.Where(x => x.Column.ToUpper() == "NDC").Select(x => x.Item).FirstOrDefault() == null
                ? "" : erroredItem.OtherItems.Where(x => x.Column.ToUpper() == "NDC").Select(x => x.Item).First().ToString();
                string phpNdc = erroredItem.PhpItems.Where(x => x.Column.ToUpper() == "NDC").Select(x => x.Item).FirstOrDefault() == null
                ? "" : erroredItem.PhpItems.Where(x => x.Column.ToUpper() == "NDC").Select(x => x.Item).First().ToString();

                FormularyUtilities.CompareItem item = new FormularyUtilities.CompareItem
                {
                    Table = erroredItem.Type,
                    GPI = erroredItem.Key,
                    Formulary = string.IsNullOrEmpty(othFormulary) ? phpFormulary : othFormulary,  
                    Resolved = false,
                    ArchiveDate = ArchiveDate,
                    Tier = string.IsNullOrEmpty(otherTier) ? phpTier : otherTier,   
                    NDC = string.IsNullOrEmpty(otherNdc) ? phpNdc : otherNdc,
                };

                erroredItems.Add(item);
                errorsFlat.AddRange(erroredItem.ItemCompareFlattened());
            }
            WriteToLog("Starting NDC Search.");
            foreach (FormularyUtilities.FullItem erroredItem in form.NDCs.Where(x => x.Passed == false).AsEnumerable())
            {
                FormularyUtilities.CompareItem item = new FormularyUtilities.CompareItem
                {
                    Table = erroredItem.Type,
                    NDC = erroredItem.Key,
                    Formulary = erroredItem.OtherItems.Where(x => x.Column.ToUpper() == "FORMULARY").Select(x => x.Item).FirstOrDefault()
                        ?? erroredItem.PhpItems.Where(x => x.Column.ToUpper() == "FORMULARY").Select(x => x.Item).FirstOrDefault(),
                    Tier = erroredItem.OtherItems.Where(x => FormularyUtilities.Tiers.Any(y => y.ToUpper() == x.Column.ToUpper())).Select(x => x.Item).FirstOrDefault()
                        ?? erroredItem.PhpItems.Where(x => FormularyUtilities.Tiers.Any(y => y.ToUpper() == x.Column.ToUpper())).Select(x => x.Item).FirstOrDefault() ?? "",
                    Resolved = false,
                    ArchiveDate = ArchiveDate
                };
                erroredItems.Add(item);
                errorsFlat.AddRange(erroredItem.ItemCompareFlattened());
            }

            /////////////////////////WRAPPING UP AND Committing to DB What we found////////////////////////////
            if (cfgAsMaster)
            {
                WriteToLog("Loading results table.");
                if (erroredItems.Count == 0)
                {
                    WriteToLog("ALL GOOD HERE, MOVE ALONG!"); //NO actual compare issues, insanity I tell you
                    ExtractFactory.ConnectAndQuery(this, this.LoggerPhpArchive, $"INSERT INTO PHPArchv.WEB0020.CompareResults ([Table], Resolved, ArchiveDate) VALUES('All', 1, '{ArchiveDate}')");//still need to log something though so the archive date is updated
                }
                else
                {
                    DataWork.LoadTable(LoggerPhpArchive, "WEB0020.CompareResults", DataWork.ObjectToDataTable(erroredItems), this, true);
                }
            }

            WriteToLog("Starting output file.");
            string errorFile = LoggerOutputYearDir + "erroredItems" + (newerDate == "" ? "Master" : newerDate) + "_v_" + olderDate +".xlsx";

            DataTable errs = DataWork.ObjectToDataTable(errorsFlat);
            errs.Columns.Add("Name", typeof(string));
            errs.Columns["Name"].SetOrdinal(1);
            DataTable GPInames = FormularyUtilities.getGPINames(this);
            DataTable NDCnames = FormularyUtilities.getNDCNames(this);
            foreach (DataRow e in errs.Rows)
            {
                string name = GPInames.AsEnumerable().Where(n => n.Field<string>("GPI") == e.Field<string>("ItemKey")).Select(n => n.Field<string>("GPI_Name")).FirstOrDefault()
                    ?? NDCnames.AsEnumerable().Where(n => n.Field<string>("NDC") == e.Field<string>("ItemKey")).Select(n => n.Field<string>("NDC_Name")).FirstOrDefault();
                e.SetField("Name", name);
            }

            //ExcelWork.Output(erroredItems, "CompareResults", errorFile, Overwrite: false);
            //ExcelWork.OutputDataTableToExcel(errs, "ErrorDetails", errorFile, false);

            FileTransfer.PushToSharepoint("ITReports", this.ProcessId, errorFile, this);
        }

        public void exportSatelliteTable(string table, string GPIName, DataTable GPIWithPK, string formulary, string[] columnsToDelete, List<string> sourceMappings, List<string> destMappings, bool isFirst = false)
        {
            string justThisFormularyQuery = string.Format(@"SELECT {0}.* from WEB0020.{0}_
                                                            INNER JOIN WEB0020.GPI_ GPI
                                                            ON GPI.GPI_LINE_PK = {0}.GPI_LINE_PK
                                                            where GPI.Formulary = '{1}'", table, formulary);
            DataTable export = DataWork.QueryToDataTable(justThisFormularyQuery, this.LoggerPhpConfig, sourceMappings, destMappings);
            DataTable exportWithGPI = GetGPIInfoFromPK(export, GPIWithPK, GPIName, columnsToDelete);
            ExcelWork.OutputDataTableToExcel(exportWithGPI, table, this.LoggerOutputYearDir + formulary + "_" + ArchiveDate.ToString("yyyyMMdd") + ".xlsx", !isFirst);
        }

        /// <summary>
        /// Reads an excel sheet from a specific file and returns 3 columns to be inserted into GPI_
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="sheetName"></param>
        /// <param name="formularyName"></param>
        /// <returns>Data table formated to Tiered GPI Naming convention</returns>
        public DataTable GetGPIData(string fileName, string sheetName, string formularyName)
        {
            DataTable GPI = GetFormularyData(fileName, this, sheetName, formularyName: formularyName);
            //GPIs "FGD Generic Product ID"
            //PA "PGO Generic Product ID"
            //UM "GPI"

            if(!sheetName.Contains("GPI"))//Lovingly changes what a GPI is named on a per sheet basis so we must standardize
            {
                if(sheetName.Contains("UM"))
                {
                    GPI.Columns["GPI"].ColumnName = "FGD Generic Product ID";
                }
                else
                {
                    GPI.Columns["PGO Generic Product ID"].ColumnName = "FGD Generic Product ID";
                }
            }

            //Sheets have different formats so must only pull usable column names, mark anything not white listed for deletion
            List<string> deletions = new List<string>();
            foreach (DataColumn col in GPI.Columns)
            {
                if(col.ColumnName == "FGD Generic Product ID" || col.ColumnName == "Formulary" || col.ColumnName == "ArchiveDate")
                {
                    continue;
                }
                else
                {
                    deletions.Add(col.ColumnName);
                }
            }
            foreach (string colName in deletions)//actually remove from DataTable
            {
                GPI.Columns.Remove(colName);
            }

            GPI.AcceptChanges();
            return GPI;
        }

        public DataTable ChangeColumnDataTypeIfEmpty(DataTable input, string column, Type dataType)
        {
            if (input.Columns[column].DataType == typeof(string))
            {
                input.Columns.Remove(column);
                input.Columns.Add(column, dataType);
                foreach (DataRow row in input.Rows)
                {
                    row[column] = 0;
                }
            }
            input.AcceptChanges();
            return input;
        }

        public DataTable ChangeColumnDataTypeWithData(DataTable input, string column, Type dataType)
        {
                if (input.Columns[column].DataType == typeof(string))
                {
                    input.Columns.Add(column + "temp", dataType);
                    foreach (DataRow row in input.Rows)
                    {
                        if (dataType == typeof(int))
                        {
                            row[column + "temp"] = int.Parse(row.Field<string>(column));
                        }
                        else if (dataType == typeof(double))
                        {
                            row[column + "temp"] = double.Parse(row.Field<string>(column));
                        }
                        else if (dataType == typeof(long))
                        {
                            row[column + "temp"] = long.Parse(row.Field<string>(column));
                        }
                        else if (dataType == typeof(short))
                        {
                            row[column + "temp"] = short.Parse(row.Field<string>(column));
                        }
                        else if (dataType == typeof(float))
                        {
                            row[column + "temp"] = float.Parse(row.Field<string>(column));
                        }
                        else if (dataType == typeof(byte))
                        {
                            row[column + "temp"] = byte.Parse(row.Field<string>(column));
                        }
                        else if (dataType == typeof(uint))
                        {
                            row[column + "temp"] = uint.Parse(row.Field<string>(column));
                        }
                        else if (dataType == typeof(ushort))
                        {
                            row[column + "temp"] = ushort.Parse(row.Field<string>(column));
                        }
                        else if (dataType == typeof(ulong))
                        {
                            row[column + "temp"] = ulong.Parse(row.Field<string>(column));
                        }
                        else if (dataType == typeof(sbyte))
                        {
                            row[column + "temp"] = sbyte.Parse(row.Field<string>(column));
                        }
                        else if (dataType == typeof(bool))
                        {
                            row[column + "temp"] = bool.Parse(row.Field<string>(column));
                        }
                        else if (dataType == typeof(decimal))
                        {
                            row[column + "temp"] = decimal.Parse(row.Field<string>(column));
                        }
                        else if (dataType == typeof(DateTime))
                        {
                            row[column + "temp"] = DateTime.Parse(row.Field<string>(column));
                        }
                        else if (dataType == typeof(char))
                        {
                            row[column + "temp"] = char.Parse(row.Field<string>(column));
                        }
                    }
                    input.Columns.Remove(column);
                    input.Columns[column + "temp"].ColumnName = column;
                }
                input.AcceptChanges();
            return input;
        }

        public DataTable GetGPILinePK(DataTable input, DataTable GPIWithPK, string GPIField, string formulary, string file)
        {
            input = input.AsEnumerable().OrderBy(x => x[GPIField].ToString().Contains("*")).CopyToDataTable();
            input.Columns.Add("GPI_LINE_PK", typeof(int));
            DataTable GPIWithPKFormulary = GPIWithPK.Clone();
            try
            {
                GPIWithPKFormulary = GPIWithPK.Select("Formulary = '" + formulary + "'").CopyToDataTable();
            }
            catch
            {
                WriteToLog("No GPIs for this formulary");
            }
            for (int i = 0; i < input.Rows.Count; i++)
            {
                DataRow row = input.Rows[i];
                if (row[GPIField].ToString() == "" || row[GPIField] == null)
                {
                    row.Delete();
                }
                else if (row["GPI_LINE_PK"].ToString() != "")
                {
                    continue;
                }
                else if (row[GPIField].ToString().Contains("*"))
                {
                    List<string> matches = GPIWithPKFormulary.Select("GPI LIKE '" + row[GPIField].ToString().Replace("*", "%") + "'").Select(x => x["GPI_LINE_PK"].ToString()).ToList();
                    if (matches.Count() > 0)
                    {
                        bool uniqMatch = false;
                        for (int x = 0; x < matches.Count(); x++)
                        {
                            if (input.AsEnumerable().Where(z => z.RowState != DataRowState.Deleted && z["GPI_LINE_PK"].ToString() == matches[x]).Count() == 0)
                            {
                                if (x == 0)
                                {
                                    row["GPI_LINE_PK"] = matches[x];
                                    uniqMatch = true;
                                }
                                else
                                {
                                    DataRow newRow = input.NewRow();
                                    foreach (DataColumn col in input.Columns)
                                    {
                                        newRow[col] = row[col];
                                    }
                                    newRow["GPI_LINE_PK"] = matches[x];
                                    input.Rows.Add(newRow);
                                }
                            }
                        }
                        if (!uniqMatch)
                        {
                            this.WriteToLog("no match for generic (or all matches overridden) - " + row[GPIField].ToString());
                            row.Delete();
                        }
                    }
                    else
                    {
                        this.WriteToLog("no match - " + row[GPIField].ToString());
                        row.Delete();
                    }
                }
                else
                {
                    //Debug.WriteLine("searching for " + row[GPIField].ToString());
                    //THIS IS RESPONSIBLE FOR THE WEIRD RECORDS
                    //PROBABLY NEED TO SORT THE ASTERISK RECORDS TO THE BOTTOM, THEN ONLY INCLUDE THEM ON MATCHES THAT DON'T HAVE AN EXPLICIT MATCH
                    IEnumerable<string> matches = GPIWithPKFormulary.Select("GPI LIKE '" + row[GPIField].ToString() + "'").Select(x => x["GPI_LINE_PK"].ToString());

                    if (matches.Count() == 1)
                    {
                        row["GPI_LINE_PK"] = matches.First();
                    }
                    else if (matches.Count() > 1)
                    {
                        List<DataRow> matches2 = GPIWithPKFormulary.Select("GPI LIKE '" + row[GPIField].ToString().Replace("*", "%") + "'").ToList();

                        row["GPI_LINE_PK"] = matches.First();
                    }
                    else
                    {
                        this.WriteToLog("no match - " + row[GPIField].ToString());
                        row.Delete();
                    }
                }
            }
            input.AcceptChanges();
            return input;
        }

        public DataTable GetGPIInfoFromPK(DataTable input, DataTable GPIWithPK, string GPIField, string[] columnsToDrop)
        {
            input.Columns.Add(GPIField, typeof(string)).SetOrdinal(0);
            for (int i = 0; i < input.Rows.Count; i++)
            {
                DataRow row = input.Rows[i];
                if (row["GPI_LINE_PK"].ToString() == "" || row["GPI_LINE_PK"] == null)
                {
                    input.Rows.Remove(row);
                    continue;
                }
                IEnumerable<string> matches = GPIWithPK.Select("GPI_LINE_PK = " + row["GPI_LINE_PK"].ToString()).Select(x => x["GPI"].ToString());
                if (matches.Count() > 0)
                {
                    row[GPIField] = matches.First();
                }
                else
                {
                    row[GPIField] = 0;
                }
            }
            foreach (string column in columnsToDrop)
            {
                input.Columns.Remove(column);
            }
            input.AcceptChanges();
            return input;
        }

        public class GPIDTO
        {
            public string GPI { get; set; }
            public string Name { get; set; }
            public DateTime EffectiveDate { get; set; }
            public DateTime TermDate { get; set; }
        }

        //Incoming date format: CYYMMDD. Default for C (0) = 19XX, 1 = 20XX, 2 = 21XX, etc.
        public DateTime OtherDateToDateTime(string dt)
        {
            string year, month, day;
            int century = 19; //1900s by default
            if (dt.Length == 7)
            {
                century = century + Convert.ToInt32(dt.Substring(0, 1));
                dt = dt.Substring(1, 6);
            }
            year = dt.Substring(0, 2);
            month = dt.Substring(2, 2);
            day = dt.Substring(4, 2);
            return new DateTime(Convert.ToInt32(string.Concat(century, year)), Convert.ToInt32(month), Convert.ToInt32(day));
        }

        public string DateTimeToOtherDate(DateTime dateTime)
        {
            string century, year, month, day;
            int distanceFrom1900 = dateTime.Year - 1900;
            century = (distanceFrom1900 / 100).ToString();
            if (century == "0")
            {
                century = "";
            }
            year = dateTime.ToString("yy");
            month = dateTime.ToString("MM");
            day = dateTime.ToString("dd");
            return century + year + month + day;
        }

        DataTable GetFormularyData(string fileName, Logger logger, string sheetName, string formularyName = "", string tierName = "")
        {
            DataTable formularyData = ExcelWork.ImportXlsx(fileName, true, sheetName, this);
            if (formularyName != "")
            {
                formularyData.Columns.Add("Formulary", typeof(string));
                formularyData.Columns.Add("ArchiveDate", typeof(DateTime));
                foreach (DataRow row in formularyData.Rows)
                {
                    row["Formulary"] = formularyName;
                    row["ArchiveDate"] = ArchiveDate;
                }
            }
            else
            {
                formularyData.Columns.Add("ArchiveDate", typeof(DateTime));
                foreach (DataRow row in formularyData.Rows)
                {
                    row["ArchiveDate"] = ArchiveDate;
                }
            }
            /*
            if(tierName != "")
            {
                formularyData.Columns.Add("Tier", typeof(string));
                foreach (DataRow row in formularyData.Rows)
                {
                    row["Tier"] = formularyName;
                }
            }*/
            formularyData.AcceptChanges();
            return formularyData;
        }
    }
}
