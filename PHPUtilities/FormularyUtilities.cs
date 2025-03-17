using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Linq.Mapping;

namespace Utilities
{
    public class FormularyUtilities
    {
        public static List<string> Tiers = new List<string>() { "Tier", "C17_TIER", "C18_TIER", "UM_Tier", "PA_Tier", "NDC_Tier" };
        private static List<string> ColumnExclusion = new List<string>() { "_PK", "EFF_DT", "EFFECTIVE_DATE", "TERM_DT", "LABEL_NAME", "PRODUCT_NAME", "MEDD_INDICATOR", "THERAPY_CODE", "THERAPY_CODE_DESCRIPTION"
                                                                            ,"DRUG_STATUS_EFFECTIVE_DATE", "FORMULARY_ID", "FORMULARY_SOURCE_CODE", "FORMULARY_COMPLIANCE_CODE", "RX_INDICATOR"};

        /// <summary>
        /// Pulls from either GPI or NDC archive tables for the most recent date less than passed in date, default newest
        /// </summary>
        /// <param name="procLog">Logger Object</param>
        /// <param name="isGpiBased">True for GPI, False for NDC</param>
        /// <param name="compDate">Most recent date before this param, empty is most recent in the table </param>
        /// <returns>String Representing Most Recent Date From Archive Tables</returns>
        public static string getArchvDate(Logger procLog, bool isGpiBased, string compDate = "")
        {
            string whereClause = compDate == "" ? "" : $"WHERE ArchiveDate <= {compDate}";
            if (isGpiBased)
            {
                return ExtractFactory.ConnectAndQuery<DateTime>(procLog, procLog.LoggerPhpArchive, string.Format("SELECT MAX(ArchiveDate) FROM [PHPArchv].WEB0020.GPI_CVS {0}", whereClause)).First().ToString("yyyy-MM-dd");
            }
            else
            {
                return ExtractFactory.ConnectAndQuery<DateTime>(procLog, procLog.LoggerPhpArchive, string.Format("SELECT MAX(ArchiveDate) FROM [PHPArchv].WEB0020.NDC_CVS {0}", whereClause)).First().ToString("yyyy-MM-dd");
            }
        }

        /// <summary>
        /// Takes a singular PK from the compare results table and returns the comparison on that GPI/NDC for that Formulary useful for front end compares
        /// </summary>
        /// <param name="compareResult">Specific record to look at</param>
        /// <param name="procLog">Used for Test or PROD</param>
        /// <returns>List<FullItem> at the GPI/NDC formulary Level</returns>
        public static List<FullItem> compareResultByPk(string compareResult, Logger procLog)
        {
            DataTable results = ExtractFactory.ConnectAndQuery(procLog.LoggerPhpArchive, $"Select * From PHPArchv.WEB0020.CompareResults Where CompareResults_PK = '{compareResult}'");

            if (results.Rows.Count != 1)
            {
                throw new Exception("No Corresponding Record Found or too many");
            }
            else
            {
                DataRow result = results.AsEnumerable().FirstOrDefault();
                if (result["GPI"].ToString() != "")//NDCs will have no GPI
                {
                    return comparePartOfGPI(result["GPI"].ToString(), result["Table"].ToString(), procLog, formularyName: result["Formulary"].ToString());
                }
                else //we are doing an NDC compare
                {
                    return comparePartOfNDC(result["NDC"].ToString(),procLog, formularyName: result["Formulary"].ToString());
                }
            }
        }

        public static List<FormularyItemV2> CompareResultByNDC(string compareResultKey, Logger procLog)
        {
            DataTable results = ExtractFactory.ConnectAndQuery(procLog, procLog.LoggerPhpArchive, $"SELECT * FROM WEB0020.CompareResults WHERE CompareResults_PK = '{compareResultKey}'");

            if (results.Rows.Count == 0)
            {
                throw new Exception($"No corresponding record found for CompareResults_PK {compareResultKey}");
            }
            else if (results.Rows.Count > 1)
            {
                throw new Exception($"Too many records found for CompareResults_PK {compareResultKey}");
            }
            else
            {
                DataRow result = results.AsEnumerable().FirstOrDefault();
                return ComparePartOfNDC(procLog, result["NDC"].ToString()).ToList();
            }
        }

        /// <summary>
        /// Web API will hit this, and we will gather correct Data Tables to pass into the compare
        /// </summary>
        /// <param name="GPI">GPI String to Compare</param>
        /// <param name="table">PA, UM...</param>
        /// <param name="procLog">Logger</param>
        /// <param name="compDate">Lookback date</param>
        /// <param name="refDate">Reference Master date</param>
        /// <param name="cfgAsMaster">Comparing against Config Master</param>
        /// <param name="formularyName">Formulary Workbook</param>
        /// <returns></returns>
        public static List<FullItem> comparePartOfGPI(string GPI, string table, Logger procLog, string compDate = "", string refDate = "", bool cfgAsMaster = true, string formularyName = "")
        {
            Data.AppNames compDataSource = procLog.LoggerPhpArchive;
            Data.AppNames refDataSource = (cfgAsMaster) ? procLog.LoggerPhpConfig : procLog.LoggerPhpArchive;

            compDate = compDate == "" ? getArchvDate(procLog, true) : compDate;
            /*//Handle GAP 45
            List<FullItem> fullGPI = new List<FullItem>();
            if (table.ToUpper() == "GAP45")
            {
                //Not Currently doing GAP45 compares as the business logic for uniqueness needs resolved more
                //fullGPI.AddRange(gap45Compare(GPI, compDataSource, compDate, refDataSource, refDate, cfgAsMaster, formularyName));
            }
            else
            {*/
            string compQuery = genArchvCompGPI(table, compDate, GPI);
            string refQuery = cfgAsMaster ? genConfgGPI(table, GPI) : genArchvCompGPI(table, refDate, GPI);
            DataTable compTable = ExtractFactory.ConnectAndQuery(compDataSource, compQuery);
            DataTable refTable = ExtractFactory.ConnectAndQuery(refDataSource, refQuery);
            
            return comparePartOfGPI(GPI, table, refTable, compTable, formularyName);
        }


        /// <summary>
        /// Compares a single GPI, on a Single Table and/or Forumlary returning a full compare list 
        /// </summary>
        /// <param name="GPI">GPI being compared</param>
        /// <param name="table">PA, C17...</param>
        /// <param name="refTable">Master Data Table</param>
        /// <param name="compTable">Lookback Data Table</param>
        /// <param name="formularyName">Blank if across all, otherwise wich formulary workbook</param>
        /// <returns>FullItem list of comparisons</returns>
        public static List<FullItem> comparePartOfGPI(string GPI, string table, DataTable refTable, DataTable compTable, string formularyName = "")
        {
            List<FullItem> fullGPI = new List<FullItem>();
            List<string> formularies;

            if (formularyName == "")//if not defined gather all
            {
                formularies = refTable.AsEnumerable().Select(x => x.Field<string>("Formulary")).Union(compTable.AsEnumerable().Select(x => x.Field<string>("Formulary"))).ToList();
            }
            else
            {
                formularies = new List<string>() { formularyName };
            }

            foreach (string formulary in formularies)
            {
                List<DataRow> compRows = compTable.Select($"Formulary = '{formulary}'").ToList();
                List<DataRow> refRows = refTable.Select($"Formulary = '{formulary}'").ToList();

                if (table.ToUpper() == "GPI_TIER")
                {
                    List<string> Tiers = refRows.Select(x => x.Field<string>("Tier")).Union(compRows.Select(x => x.Field<string>("Tier"))).ToList();
                    foreach (string Tier in Tiers)
                    {
                        List<DataRow> compRowsTiers = compRows.Where(x => x.Field<string>("Tier") == Tier).ToList();
                        List<DataRow> refRowsTiers = refRows.Where(x => x.Field<string>("Tier") == Tier).ToList();

                        fullGPI.AddRange(dataRowCompares(refRowsTiers, compRowsTiers, GPI, table));
                    }
                }
                else
                {
                    fullGPI.AddRange(dataRowCompares(refRows, compRows, GPI, table));
                }
            }
            return fullGPI.Distinct().ToList();
        }


        /// <summary>
        /// Compares 1 GPI across all formularies and tables and determines pass or failures for each item
        /// </summary>
        /// <param name="GPI">String GPI to Compare</param>
        /// <param name="procLog">Logger Object</param>
        /// <param name="compDate">Previous to Master always the Archv table Archive Date</param>
        /// <param name="refDate">Master Date of compare</param>
        /// <param name="cfgAsMaster">Normal run true, historical compares false</param>
        /// <returns></returns>
        public static List<FullItem> compareByGPI(string GPI, Logger procLog, string compDate, string refDate = "", bool cfgAsMaster = true, bool onlyFailures = false) 
        {
            //RefDate = newer/master, CompDate = date to compare against
            //TODO return list of failures
            string refQuery = "";
            string compQuery = "";
            List<FullItem> fullGPI = new List<FullItem>();
            List<string> tables = new List<string>(new[] { "C17", "C18", "PA", "UM", "GPI_Tier" }); //, "GAP45" });
                
            if(cfgAsMaster)
            {
                foreach (string table in tables)
                {
                    refQuery = refQuery + ";\n" + genConfgGPI(table, GPI);
                    compQuery = compQuery + ";\n" + genArchvCompGPI(table, compDate, GPI);
                }
            }
            else
            {
                foreach (string table in tables)
                {
                    refQuery = refQuery + ";\n" + genArchvCompGPI(table, refDate, GPI);
                    compQuery = compQuery + ";\n" + genArchvCompGPI(table, compDate, GPI);
                }
            }
            DataSet refDS = ExtractFactory.ConnectAndQuery_Dataset(procLog.LoggerExampleDb, refQuery);
            DataSet compDS = ExtractFactory.ConnectAndQuery_Dataset(procLog.LoggerExampleDb, compQuery);

            int i = 0;
            foreach (string table in tables)
            {
                //maybe a check if we even need to send it in if both tables are 0 counts
                fullGPI.AddRange(comparePartOfGPI(GPI, table, refDS.Tables[i], compDS.Tables[i]));
                i++;
            }
            
            /*if(onlyFailures)
            {
                //return fullGPI.Distinct().ToList().Where(x => x.Passed == false);
            }
            else
            {*/

            return fullGPI.Distinct().ToList();
            
        }


        

        /// <summary>
        /// NOT CURRENTLY TESTED, Cannot get GAP45 summurized yet unique enough to allow one person to maintain
        /// Compares GAP45s at GPI level ignoring NDCs for distinction.  GAP45 does not deal with Tiers but only Formulary Level
        /// </summary>
        /// <param name="GPI">Singular GPI to pull accross all GPIs</param>
        /// <param name="compDS">Archive DB for historical compares (TEST or PROD)</param>
        /// <param name="compDate">Look back date in Archv</param>
        /// <param name="refDS">Config or Archive if doing historical or not (TEST or PROD after that)</param>
        /// <param name="refDate">Master date defaults to empty for config</param>
        /// <param name="cfgAsMaster">True for standard runs, false for archive compares</param>
        /// <returns></returns>
        public static List<FullItem> gap45Compare(string GPI, Data.AppNames compDS, string compDate, Data.AppNames refDS, string refDate = "", bool cfgAsMaster = true, string formularyName = "")
        {
            //Seems like we need to compare at a GPI Formulary level but then if we cannot get it distinct go down to the NDC level?
            List<FullItem> gap45GPIs = new List<FullItem>();

            string compQuery = genGAP45(GPI, compDate);
            string refQuery = cfgAsMaster ? genGAP45(GPI) : genGAP45(GPI, refDate);
            DataTable compTable = ExtractFactory.ConnectAndQuery(compDS, compQuery);
            DataTable refTable = ExtractFactory.ConnectAndQuery(refDS, refQuery);

            List<string> formularies = refTable.AsEnumerable().Select(x => x.Field<string>("Formulary")).Union(compTable.AsEnumerable().Select(x => x.Field<string>("Formulary"))).ToList();

            foreach (string formulary in formularies)
            {
                List<DataRow> compRows = compTable.Select($"Formulary = '{formulary}'").ToList();
                List<DataRow> refRows = refTable.Select($"Formulary = '{formulary}'").ToList();

                List<string> MONYs = refRows.Select(x => x.Field<string>("Multi_Source_Code")).Union(compRows.Select(x => x.Field<string>("Multi_Source_Code"))).ToList();

                foreach (string MONY in MONYs)
                {
                    List<DataRow> compMONYRows = compRows.Where(x => x.Field<string>("Multi_Source_Code") == MONY).ToList();
                    List<DataRow> refMONYRows = refRows.Where(x => x.Field<string>("Multi_Source_Code") == MONY).ToList();

                    //List<DataRow> PTRows = compMONYRows.Select(x => x.Field<string>("Product_Type")).Union(refMONYRows.Select(x => x.Field<string>("Product_Type"))).ToList();

                    gap45GPIs.AddRange(dataRowCompares(refMONYRows, compMONYRows, "GPI", "GAP45"));
                }
            }
            return gap45GPIs;
        }

        /// <summary>
        /// COmpares one NDC across all formularies and tiers returing the results
        /// </summary>
        /// <param name="NDC">What to compare on</param>
        /// <param name="procLog">logger</param>
        /// <param name="compDate">Archive lookback date, if normal run this is the most recent date in archive</param>
        /// <param name="refDate">only overriden if doing a historical to historical compare</param>
        /// <param name="cfgAsMaster">True if normal run using Confg as the master table</param>
        /// <param name="onlyFailures"></param>
        /// <returns></returns>
        public static List<FullItem> compareByNDC(string NDC, Logger procLog, string compDate, string refDate = "", bool cfgAsMaster = true, bool onlyFailures = false)
        {
            //RefDate = newer/master, CompDate = date to compare against
            //TODO return seperate list of failures
            string refQuery = "";
            string compQuery = "";
            List<FullItem> fullNDC = new List<FullItem>();

            if (cfgAsMaster)
            {
                refQuery = genConfgNDC(NDC);
                compQuery = genArchvNDC(NDC, compDate);
            }
            else
            {
                refQuery =  genArchvNDC(NDC, refDate);
                compQuery = genArchvNDC(NDC, compDate);
            }
            DataTable refDT = ExtractFactory.ConnectAndQuery(procLog.LoggerPhpConfig, refQuery);
            DataTable compDT = ExtractFactory.ConnectAndQuery(procLog.LoggerPhpArchive, compQuery);

            fullNDC.AddRange(comparePartOfNDC(NDC, refDT, compDT));
            
            /*if(onlyFailures)
            {
                //return fullGPI.Distinct().ToList().Where(x => x.Passed == false);  need to check the overhead on this call 
            }
            else
            {*/

            return fullNDC.Distinct().ToList();
        }

        /// <summary>
        /// NDC compare overload for the web calls, based on given info it pulls the needed data tables to compare across all Tiers on that Formulary
        /// </summary>
        /// <param name="NDC">NDC to compare on</param>
        /// <param name="procLog">Used for PROD / Test determination</param>
        /// <param name="compDate">lookback date, default is first in archive</param>
        /// <param name="refDate">master date if not using confg</param>
        /// <param name="cfgAsMaster">default is using the Confg table as the master</param>
        /// <param name="formularyName">Which formulary to compare on</param>
        /// <returns></returns>
        public static List<FullItem> comparePartOfNDC(string NDC, Logger procLog, string compDate = "", string refDate = "", bool cfgAsMaster = true, string formularyName = "")
        {
            Data.AppNames compDataSource = procLog.LoggerPhpArchive;
            Data.AppNames refDataSource = (cfgAsMaster) ? procLog.LoggerPhpConfig : procLog.LoggerPhpArchive;

            compDate = compDate == "" ? getArchvDate(procLog, true) : compDate;
            
            string compQuery = genArchvNDC(NDC, compDate);
            string refQuery = cfgAsMaster ? genConfgNDC(NDC) : genArchvNDC(NDC, refDate);
            DataTable compTable = ExtractFactory.ConnectAndQuery(compDataSource, compQuery);
            DataTable refTable = ExtractFactory.ConnectAndQuery(refDataSource, refQuery);

            return comparePartOfNDC(NDC, refTable, compTable, formularyName);
        }

        /// <summary>
        /// Does an NDC compare based on two tables passed in, can be specific to a formulary
        /// </summary>
        /// <param name="NDC">NDC we are looking at</param>
        /// <param name="refTable">Master table</param>
        /// <param name="compTable">Lookback table</param>
        /// <param name="formularyName">Blank if across all it exists on</param>
        /// <returns></returns>
        public static List<FullItem> comparePartOfNDC(string NDC, DataTable refTable, DataTable compTable, string formularyName = "")
        {
            List<FullItem> fullNDC = new List<FullItem>();
            List<string> formularies;

            if (formularyName == "")
            {
                formularies = refTable.AsEnumerable().Select(x => x.Field<string>("Formulary")).Union(compTable.AsEnumerable().Select(x => x.Field<string>("Formulary"))).ToList();
            }
            else
            {
                formularies = new List<string>() { formularyName };
            }

            foreach (string formulary in formularies)
            {
                List<DataRow> refRows = refTable.Select($"Formulary = '{formulary}'").ToList();
                List<DataRow> compRows = compTable.Select($"Formulary = '{formulary}'").ToList();

                List<string> Tiers = refRows.Select(x => x.Field<string>("NDC_Tier")).Union(compRows.Select(x => x.Field<string>("NDC_Tier"))).ToList();
                foreach (string Tier in Tiers)
                {
                    List<DataRow> refRowsTiers = refRows.Where(x => x.Field<string>("NDC_Tier") == Tier).ToList();
                    List<DataRow> compRowsTiers = compRows.Where(x => x.Field<string>("NDC_Tier") == Tier).ToList();

                    fullNDC.AddRange(dataRowCompares(refRowsTiers, compRowsTiers, NDC, "NDC"));
                }
            }
            return fullNDC.Distinct().ToList();
        }

        public static IEnumerable<FormularyItemV2> ComparePartOfNDC(Logger logger, string ndc, string formularyName = "")
        {
            string query = ndcDiffQuery();
            query += $"\nWHERE NDC.NDC = {ndc}";
            if (formularyName != "")
            {
                query += $"\nAND NDC.Formulary = {formularyName}";
            }

            //return ExtractFactory.ConnectAndQuery<FormularyItemV2>(logger, logger.LoggerPhpConfig, query);
            DataTable dt = ExtractFactory.ConnectAndQuery(logger, logger.LoggerPhpConfig, query);
            return DataWork.DataTableToObject<FormularyItemV2>(dt);
        }

        private static string ndcDiffQuery()
        {
            return $@"
                SELECT DISTINCT NDC.NDC_PK             AS [Key],
                                NDC_TIER               AS Tier,
                                NDC.Formulary          AS Formulary,
                                NDC_ESI.Formulary      AS FormularyESI,
                                CASE
                                    WHEN NDC.Formulary = NDC_ESI.Formulary THEN cast(1 AS bit)
                                    ELSE cast(0 AS bit)
                                END                    AS FormularyIsEqual,
                                'NDC'                  AS Type,
                                NDC.LABEL_NAME         AS LabelName,
                                NDC_ESI.LabelName      AS LabelNameEsi,
                                CASE
                                    WHEN NDC.LABEL_NAME = NDC_ESI.LabelName THEN cast(1 AS bit)
                                    ELSE cast(0 AS bit)
                                END                    AS LabelNameIsEqual,
                                NDC.NDC_EFF_DT         AS EffectiveDate,
                                NDC_ESI.EffectiveDate  AS EffectiveDateEsi,
                                CASE
                                    WHEN NDC.NDC_EFF_DT = NDC_ESI.EffectiveDate THEN cast(1 AS bit)
                                    ELSE cast(0 AS bit)
                                END                    AS EffectiveDateIsEqual,
                                NDC.NDC_TERM_DT        AS TermDate,
                                NDC_ESI.TermDate       AS TermDateEsi,
                                CASE
                                    WHEN NDC.NDC_TERM_DT = NDC_ESI.TermDate THEN cast(1 AS bit)
                                    ELSE cast(0 AS bit)
                                END                    AS TermDateIsEqual,
                                NDC.NDC_M              AS M,
                                NDC_ESI.M              AS MEsi,
                                CASE
                                    WHEN NDC.NDC_M = NDC_ESI.M THEN cast(1 AS bit)
                                    ELSE cast(0 AS bit)
                                END                    AS MIsEqual,
                                NDC.NDC_O              AS O,
                                NDC_ESI.O              AS OEsi,
                                CASE
                                    WHEN NDC.NDC_O = NDC_ESI.O THEN cast(1 AS bit)
                                    ELSE cast(0 AS bit)
                                END                    AS OIsEqual,
                                NDC.NDC_N              AS N,
                                NDC_ESI.N              AS NEsi,
                                CASE
                                    WHEN NDC.NDC_N = NDC_ESI.N THEN cast(1 AS bit)
                                    ELSE cast(0 AS bit)
                                END                    AS NIsEqual,
                                NDC.NDC_Y              AS Y,
                                NDC_ESI.Y              AS YEsi,
                                CASE
                                    WHEN NDC.NDC_Y = NDC_ESI.Y THEN cast(1 AS bit)
                                    ELSE cast(0 AS bit)
                                END                    AS YIsEqual,
                                NDC.FND_CUR_STS        AS FND_CUR_STS,
                                NDC_ESI.FND_CUR_STS    AS FND_CUR_STS_Esi,
                                CASE
                                    WHEN NDC.FND_CUR_STS = NDC_ESI.FND_CUR_STS THEN cast(1 AS bit)
                                    ELSE cast(0 AS bit)
                                END                    AS FND_CUR_STS_IsEqual,
                                NDC.NDC_RX             AS Rx,
                                NDC_ESI.Rx             AS RxEsi,
                                CASE
                                    WHEN NDC.NDC_RX = NDC_ESI.Rx THEN cast(1 AS bit)
                                    ELSE cast(0 AS bit)
                                END                    AS RxIsEqual,
                                NDC.NDC_OTC            AS OverTheCounter,
                                NDC_ESI.OverTheCounter AS OverTheCounterEsi,
                                CASE
                                    WHEN NDC.NDC_OTC = NDC_ESI.OverTheCounter THEN cast(1 AS bit)
                                    ELSE cast(0 AS bit)
                                END                    AS OverTheCounterIsEqual
                FROM [PHPConfg].WEB0020.NDC
                     LEFT JOIN [PHPArchv].[WEB0020].NDC_ESI ON NDC.NDC_PK = NDC_ESI.NDC_PK
            ";
        }

        /// <summary>
        /// For inserts to Config Master from CVS archive, Looks up the GPI or NDC pk provided from archv to see if it exists in confg, adds to master if not returning the new pk
        /// </summary>
        /// <param name="log">Logger</param>
        /// <param name="archvPk">Archive Tables Pk to look up NDC/GPI and Formulary combo</param>
        /// <param name="table">Which Extract to look against</param>
        /// <returns>Matching Config Pk (at GPI/NDC + Formulary level) or newly inserted one</returns>
        public static string getConfgPk(Logger log, string archvPk, string table)
        {
            DataTable pkInfo = new DataTable();
            string confgPk;

            string NDCQuery = @"Select  C.*
                                  From PHPArchv.WEB0020.NDC_CVS A
                                  Inner Join PHPConfg.WEB0020.NDC C
                                  On A.NDC = C.NDC And A.Formulary = C.Formulary And A.NDC_TIER = C.NDC_TIER
                                  Where A.NDC_PK = '{0}'"; //only need to know it doesnt exist for that NDC/Formulary/Tier combo for NDCs

            string GPIQuery = @"Select Top 1 C.GPI_LINE_PK, A.GPI, A.Formulary
                                  From PHPArchv.WEB0020.{1}_CVS tbl
                                  Inner Join PHPArchv.WEB0020.GPI_CVS A
                                  On tbl.GPI_LINE_PK = A.GPI_LINE_PK
                                  Left Join PHPConfg.WEB0020.GPI C
                                  On A.GPI = C.GPI and A.Formulary = C.Formulary
                                  Where tbl.{1}_PK = '{0}'";

            string updateQuery = @"INSERT INTO PHPConfg.WEB0020.GPI (GPI, Formulary, ArchiveDate)
                                    OUTPUT inserted.GPI_LINE_PK
                                    VALUES('{0}', '{1}', GETDATE())";
            

            if (table == "NDC")
            {
                pkInfo = ExtractFactory.ConnectAndQuery(log, log.LoggerPhpArchive, string.Format(NDCQuery,archvPk));
                if(pkInfo.Rows.Count > 0)
                {
                    throw new Exception("NDC, Formulary, Tier combo already exists in NDC Master Table");
                }
                return "NDC Ok";
            }
            else if (table == "Gap45")
            {
                //NotToday Broskie
            }
            else //All other tables
            {
                pkInfo = ExtractFactory.ConnectAndQuery(log, log.LoggerPhpArchive, string.Format(GPIQuery, archvPk, table));
                if (pkInfo.Rows.Count == 0)
                {
                    throw new Exception("Did not find the proper Pk information");
                }
            }

            if (pkInfo.Rows[0][0] == DBNull.Value) //Need to create a PK for this Formualry
            {
                log.WriteToLog("This is a brand new GPI/NDC being added");
                confgPk = ExtractFactory.ConnectAndQuery(log, log.LoggerPhpConfig, string.Format(updateQuery, pkInfo.Rows[0][1], pkInfo.Rows[0][2]), 1).Rows[0][0].ToString();
                if (confgPk == "")
                {
                    throw new Exception("Failure to insert a New PK!");
                }
            }
            else //One Exists
            {
                confgPk = pkInfo.Rows[0][0].ToString();
            }
            return confgPk;
        }

        /// <summary>
        /// Inserts an entire Archive Row into Config using archive Pk, creates new GPI/NDC as needed
        /// </summary>
        /// <param name="log">Logger</param>
        /// <param name="archvPk">What to look up</param>
        /// <param name="table">What table is being updated</param>
        /// <returns>True if it completes</returns>
        public static bool InsertPk(Logger log, string archvPk, string table)
        {
            string confgPk = getConfgPk(log, archvPk, table); //Get the master config pk or make one

            DataTable archiveDt = ExtractFactory.ConnectAndQuery(log, log.LoggerPhpArchive, $"Select * From PHPArchv.WEB0020.{table}_CVS Where {table}_PK = '{archvPk}'");

            if(archiveDt.Rows.Count != 1)
            {
                throw new Exception($"Could not find Archive Table {table} pk value of {archvPk}");
            }

            archiveDt.Columns.RemoveAt(0);//Take out the identity column so it can auto fill

            int AD = archiveDt.Columns.IndexOf("ArchiveDate");
            if (AD != -1) //DateStamp when we update it
            {
                archiveDt.Rows[0][AD] = System.DateTime.Now.ToString();
            }

            if(table != "NDC")//NDC pk is the identity column no reference table like GPIs
            {
                archiveDt.Rows[0][0] = confgPk;
            }

            int i = DataWork.DataTableInsertToSQL(log, log.LoggerPhpConfig, $"PHPConfg.WEB0020.{table}", archiveDt);//insert into Config Master what we have in Archive
            if(i == 0)
            {
                throw new Exception("No rows inserted to config");
            }
            return true;
        }


        public static void acceptGap45(Logger log)
        {
            log.WriteToLog("Moving archive Gap45 To Master");
            
            string insertMissingGPIs = @"Insert Into PHPConfg.WEB0020.GPI
Select Distinct Arch.Formulary, Arch.GPI, GETDATE()
From
	PHPArchv.WEB0020.GPI_CVS Arch
Left Join
	PHPConfg.WEB0020.GPI Confg
	On Arch.GPI = Confg.GPI
	And Arch.Formulary = Confg.Formulary
Where
	Arch.ArchiveDate = (Select Max(ArchiveDate) From PHPArchv.WEB0020.GPI_CVS)
	And Arch.GPI_LINE_PK IN (Select Distinct GPI_LINE_PK From PHPArchv.WEB0020.Gap45_CVS Where ArchiveDate = (Select MAX(ArchiveDate) From PHPArchv.WEB0020.Gap45_CVS))
	And Confg.GPI_LINE_PK IS NULL";

            string insertGAP45 = @"Insert Into PHPConfg.WEB0020.Gap45
SELECT Gap45.[NDC]      ,Gap45.[Generic_Name_Description]      ,Gap45.[Inactive_NDC]      ,Gap45.[Obsolete_Date]
      ,Gap45.[Label_Name]      ,Gap45.[Product_Name]      ,Gap45.[Multi_Source_Code]      ,Gap45.[Rx_Indicator]
      ,Gap45.[MEDD_Indicator]      ,Gap45.[Therapy_Code]      ,Gap45.[Therapy_Code_Description]      ,Gap45.[Drug_Status_Effective_Date]
      ,Gap45.[PA_Flag]      ,Gap45.[Smart_PA_Flag]      ,Gap45.[Step_Therapy_Flag]      ,Gap45.[Age_Flag]
      ,Gap45.[Gender_Flag]      ,Gap45.[QL_Flag]      ,Gap45.[Age_Range_Flag]      ,Gap45.[Sex_Exclusion]
      ,Gap45.[Qty_Min]      ,Gap45.[Qty_Max]      ,Gap45.[DS_Min]      ,Gap45.[DS_Max]      ,Gap45.[Qty_Ds_Comp]
      ,Gap45.[Period_Qty_Type]      ,Gap45.[Period_Qty_Days]      ,Gap45.[Period_Qty_Max]      ,Gap45.[Period_DS_Type]
      ,Gap45.[Period_DS_Days]      ,Gap45.[Period_DS_Max]      ,Gap45.[Period_Qty_Ds_Comp]      ,Gap45.[Period_Fills_Type]      
	  ,Gap45.[Period_Fills_Days]      ,Gap45.[Period_Fills_Max]      ,Gap45.[Refill_Limit_Max_Number]      ,Gap45.[Refill_Expire_After_Days]
      ,Gap45.[Amt_Due_Type]      ,Gap45.[Amt_Due_Days]      ,Gap45.[Amt_Due_Max]      ,Gap45.[Amt_Due_Basis]
      ,Gap45.[Patient_Age_Min]      ,Gap45.[Patient_Age_Max]      ,Gap45.[DD_Min]      ,Gap45.[DD_Max]      ,Gap45.[Acute_Dose_Days_Max]
      ,Gap45.[Maint_Dose_Days_Max]      ,Gap45.[Otc_Override]      ,Gap45.[Unit_Dose_Use]      ,Gap45.[FDA_Therapeutic_Equiv]
      ,Gap45.[Route_Of_Admin]      ,Gap45.[Maint_Drug_STS]      ,Gap45.[Brand_Generic_Edit]      ,Gap45.[PTD_From]
	  ,Gap45.[Limit_Days_Ovr]      ,Gap45.[Refill_Code_Ovr]      ,Gap45.[Days_Supply_Fill_Limit]      ,Gap45.[Formulary_Id]
      ,Gap45.[Formulary_Source_Code]      ,Gap45.[Formulary_Flag]      ,Gap45.[Preferred_Drug_List]      ,Gap45.[Formulary_Compliance_Code]
      ,Gap45.[Tier_Number_Display]      ,Gap45.[Tier_Total_Display]      ,Gap45.[Tier_Code]      ,Gap45.[Product_Type]
    ,GPI.[GPI_LINE_PK] --FROM CONFG!!      
	  ,Gap45.[ArchiveDate]
  FROM [PHPArchv].[WEB0020].[Gap45_CVS] as Gap45
  Left Join PHPArchv.WEB0020.GPI_CVS GPI_CVS
	On Gap45.GPI_LINE_PK = GPI_CVS.GPI_LINE_PK --GPI_LINE_PKs already eliminate need to care about Archive Dates
  Left Join PHPConfg.WEB0020.GPI GPI 
	On GPI_CVS.GPI = GPI.GPI And GPI_CVS.Formulary = GPI.Formulary
  Where 
	Gap45.ArchiveDate = (Select Max(ArchiveDate) From PHPArchv.WEB0020.Gap45_CVS)";

            //backup gap45 master
            string backupStamp = "_BAK_" + System.DateTime.Now.ToString("yyMMdd_HHmmss");
            string backupQuery = $"Select * Into WEB0020.Gap45{backupStamp} From WEB0020.Gap45";
            int x = DataWork.RunSqlCommandWithRecordCount(log, backupQuery, log.LoggerPhpConfig);
            log.WriteToLog($"Master Table Gap45 backed up with {x} records");
            try
            {
                //truncate master
                DataWork.RunSqlCommandWithRecordCount(log, $"Truncate Table WEB0020.Gap45", log.LoggerPhpConfig);

                //Insert any missing GPI/Formularies to the GPI table in Config
                x = DataWork.RunSqlCommandWithRecordCount(log, insertMissingGPIs, log.LoggerPhpConfig);
                log.WriteToLog($"Inserted {x} records to the master GPI table.");

                //Insert Latest Gap45 From Archive to Master
                x = DataWork.RunSqlCommandWithRecordCount(log, insertGAP45, log.LoggerPhpConfig);
                log.WriteToLog($"Inserted {x} records to the master Gap45 table.");

                log.WriteToLog("Success Dropping Backup Gap45 Table.");
                DataWork.RunSqlCommand(log, $"DROP TABLE WEB0020.Gap45{backupStamp}", log.LoggerPhpConfig);
            }
            catch (Exception e)
            {
                log.WriteToLog($"Error in the rebuild of Master Gap45 from Archive!  Recover Gap45 from Backups stamped {backupStamp}.", UniversalLogger.LogCategory.ERROR);
                log.WriteToLog(e.ToString(), UniversalLogger.LogCategory.ERROR);
            }
        }

        /// <summary>
        /// Moves the most recent Archive import straight into Config to align the two
        /// </summary>
        /// <param name="log">Logger</param>
        public static void masterAcceptAll(Logger log)
        {
            log.WriteToLog("Rolling all archive tables to Config Master.");

            string backupStamp = "_BAK_" + System.DateTime.Now.ToString("yyMMdd_HHmmss");
            List<string> allTables = new List<string>(new[] { "C17", "C18", "PA", "UM", "GPI_Tier", "NDC", "Gap45", "GPI"});


            log.WriteToLog("Backing up all Config Tables.");
            foreach(string table in allTables)
            {
                string backupQuery = $"Select * Into WEB0020.{table}{backupStamp} from WEB0020.{table}";
                int x = DataWork.RunSqlCommandWithRecordCount(log, backupQuery, log.LoggerPhpConfig);
                log.WriteToLog($"Table {table} backed up with {x} records.");
            }

            try
            {
                int x;
                log.WriteToLog("Trunacting Tables."); //Need to do extracts first so pk Violation doesnt happen
                foreach (string table in allTables)
                {
                    if (table != "GPI")
                    {
                        x = DataWork.RunSqlCommandWithRecordCount(log, $"Truncate Table WEB0020.{table}", log.LoggerPhpConfig);
                    }
                }
                
                x = DataWork.RunSqlCommandWithRecordCount(log, $"Delete FROM WEB0020.GPI", log.LoggerPhpConfig); //Cant truncate if fks are known to exist
                log.WriteToLog($"Table GPI DELETED {x} records.");

                allTables.Reverse(); //need to populate PK tables first
                foreach (string table in allTables)
                {
                    string insertQuery = $"SET IDENTITY_INSERT [PHPConfg].WEB0020.{table} ON;\n";
                    DataTable schemaDT = DataWork.GetTableSchema($"WEB0020.{table}", log.LoggerPhpConfig);
                    insertQuery += DataWork.DataTableToInsertQuery(log, $"WEB0020.{table}", schemaDT, $"Select * FROM PHPArchv.WEB0020.{table}_CVS Where ArchiveDate = (Select Top 1 ArchiveDate From PHPArchv.WEB0020.GPI_CVS Order By ArchiveDate Desc);\n");
                    insertQuery += $"SET IDENTITY_INSERT [PHPConfg].WEB0020.{table} OFF;";
                    x = DataWork.RunSqlCommandWithRecordCount(log, insertQuery, log.LoggerPhpConfig);
                    log.WriteToLog($"Table {table} populated from Archive with {x} records.");
                }

                log.WriteToLog("Success, Dropping BAK tables for cleanup");
                foreach(string table in allTables)
                {
                    DataWork.RunSqlCommand(log, $"DROP TABLE WEB0020.{table}{backupStamp}", log.LoggerPhpConfig);
                }
            }
            catch (Exception e)
            {
                log.WriteToLog($"Error in the rebuild of Config from Archive!  Recover Config Tables from Backups stamped {backupStamp}.", UniversalLogger.LogCategory.ERROR);
                log.WriteToLog(e.ToString(), UniversalLogger.LogCategory.ERROR);
            }
            

        }
        /// <summary>
        /// Truncates Test and Pulls from PROD tables (config and archive) over to test environment for all WEB0020 tables in PROD
        /// </summary>
        /// <param name="log"></param>
        public static void refreshTest(Logger log)
        {

            log.WriteToLog("Starting Config Overwrites!");
            //Truncate all Tables that do not have a PK
            List<string> prodConfgTables = ExtractFactory.ConnectAndQuery<string>(log, log.LoggerExampleDb, "SELECT DISTINCT TABLE_SCHEMA + '.' + TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA LIKE 'WEB0020' and TABLE_NAME Not Like 'GPI'").ToList();
            Parallel.ForEach(prodConfgTables, table =>
            {
                log.WriteToLog($"Config Starting Truncate: {table}");
                DataWork.TruncateWorkTable(table, log.LoggerExampleDb);
                log.WriteToLog($"Config Finished truncate for: {table}");
            });
            //Wipe and restore the PK table
            log.WriteToLog($"Config Starting Truncate and rebuild of: WEB0020.GPI due to FKs must do it last");
            DataWork.TruncateWorkTable("WEB0020.GPI", log.LoggerExampleDb);
            ExtractFactory.ConnectAndQuery(log, log.LoggerExampleDb, @"SET IDENTITY_INSERT [PHPConfg].WEB0020.GPI ON; 
                                                                              INSERT Into DRIVERTVDB01.[PHPConfg].WEB0020.GPI ([GPI_LINE_PK], [Formulary], [GPI], [ArchiveDate]) Select [GPI_LINE_PK], [Formulary], [GPI], [ArchiveDate] From DRIVERPVDB01.PHPConfg.WEB0020.GPI; 
                                                                              SET IDENTITY_INSERT [PHPConfg].WEB0020.GPI OFF;");//need to carry over the PKs as is
            log.WriteToLog($"Config Finished Restore: WEB0020.GPI");
            //Relaod all FK tables
            Parallel.ForEach(prodConfgTables, table =>
            {
                log.WriteToLog($"Config Starting Restore: {table}");
                DataWork.LoadTableFromQuery(log.LoggerExampleDb, $"SELECT * FROM {table}", log.LoggerExampleDb, table, log);
                log.WriteToLog($"Config Finished Restore: {table}");
            });

            //STARTING ARCHIVES NOW

            //Truncate all Tables that do not have a PK
            log.WriteToLog("Starting Archive Overwrites!");
            List<string> prodArchiveTables = ExtractFactory.ConnectAndQuery<string>(log, log.LoggerExampleDb, "SELECT DISTINCT TABLE_SCHEMA + '.' + TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA LIKE 'WEB0020' and TABLE_NAME Not Like 'GPI_CVS'").ToList();
            Parallel.ForEach(prodArchiveTables, table =>
            {
                log.WriteToLog($"Archive Starting Truncate for: {table}");
                DataWork.TruncateWorkTable(table, log.LoggerExampleDb);
                log.WriteToLog($"Archive Finished Truncate for: {table}");
            });

            //Wipe and restore the PK table
            log.WriteToLog($"Archive Starting Truncate and rebuild of: WEB0020.GPI_CVS due to FKs must do it last");
            DataWork.TruncateWorkTable("WEB0020.GPI_CVS", log.LoggerExampleDb);
            ExtractFactory.ConnectAndQuery(log, log.LoggerExampleDb, @"SET IDENTITY_INSERT [PHPArchv].WEB0020.GPI_CVS ON;
                                                                              INSERT Into DRIVERTVDB01.[PHPArchv].WEB0020.GPI_CVS ([GPI_LINE_PK], [Formulary], [GPI], [ArchiveDate]) Select [GPI_LINE_PK], [Formulary], [GPI], [ArchiveDate] From DRIVERPVDB01.PHPArchv.WEB0020.GPI_CVS; 
                                                                              SET IDENTITY_INSERT [PHPArchv].WEB0020.GPI_CVS OFF; ");//need to carry over the PKs as is
            log.WriteToLog($"Archive Finished Restore: WEB0020.GPI_CVS");

            //Truncate all Tables that do not have a PK
            Parallel.ForEach(prodArchiveTables, table =>
            {
                log.WriteToLog($"Archive Starting Rebuild for: {table}");
                DataWork.LoadTableFromQuery(log.LoggerExampleDb, $"SELECT * FROM {table}", log.LoggerExampleDb, table, log);
                log.WriteToLog($"Archive Finished Rebuild for: {table}");
            });
        }


        /// <summary>
        /// Given two list of data rows will compare the larger list to the smaller returning the full results of the compare and empty rows as needed
        /// </summary>
        /// <param name="refRows">Master rows</param>
        /// <param name="compRows">Look back rows</param>
        /// <param name="keyName">GPI or NDC</param>
        /// <param name="type">Table (PA, C17...)</param>
        /// <returns>List of full Item results between those data rows</returns>
        public static List<FullItem> dataRowCompares(List<DataRow> refRows, List<DataRow> compRows, string keyName, string type)
        {
            List<FullItem> compared = new List<FullItem>();

            if (refRows.Count >= compRows.Count) //which list is longer 
            {
                foreach (DataRow refRow in refRows)
                {
                    for (int i = 0; i < refRows.Count; i++)
                    {
                        if (i >= compRows.Count)
                        {
                            compared.Add(formularyCompare(refRow, keyName, type, false));//Adds data to missing compares
                        }
                        else
                        {
                            compared.Add(formularyCompare(refRow, compRows[i], keyName, type));//Does compare
                        }
                    }
                }
            }
            else
            {
                foreach (DataRow compRow in compRows)
                {
                    for (int i = 0; i < compRows.Count; i++)
                    {
                        if (i >= refRows.Count)
                        {
                            compared.Add(formularyCompare(compRow, keyName, type, true));//Adds data to missing compares
                        }
                        else
                        {
                            compared.Add(formularyCompare(refRows[i], compRow, keyName, type));//Does Compare
                        }
                    }
                }
            }
            return compared;
        }

        /// <summary>
        /// Adds an extra column to the Formualy compare and only returns one failure due to too many keys of one type, addToMaster True if we are importing new, false if it was orphaned and needs deleted
        /// </summary>
        /// <param name="existing">The Row you do have to compare with, brings in these column names</param>
        /// <param name="key">GPI/NDC</param>
        /// <param name="table">Gap45, C17...</param>
        /// <param name="addToMaster">True if we are importing new, false if it was orphaned and needs deleted</param>
        /// <returns></returns>
        public static FullItem formularyCompare(DataRow existing, string key, string table, bool addToMaster)
        {
            FullItem form = new FullItem
            {
                Type = table,
                OtherItems = new List<FormularyItem>(),
                PhpItems = new List<FormularyItem>(),
                Key = key,
                Passed = false
            };

            if(addToMaster)
            {
                FormularyItem addFromCVS = new FormularyItem
                {
                    Column = "Add/Delete",
                    Item = "New Key Added accept record?",
                    Passed = false
                };
                form.OtherItems.Add(addFromCVS);
                FormularyItem addMaster = new FormularyItem
                {
                    Column = "Add/Delete",
                    Item = "Import to Master?",
                    Passed = false
                };
                form.PhpItems.Add(addMaster);

                foreach (DataColumn col in existing.Table.Columns)
                {
                    FormularyItem cvsItem = new FormularyItem
                    {
                        Column = col.ColumnName,
                        Item = existing[col.ColumnName],
                        Passed = true
                    };
                    form.OtherItems.Add(cvsItem);
                    FormularyItem phpItem = new FormularyItem
                    {
                        Column = col.ColumnName,
                        Item = "",
                        Passed = true
                    };
                    form.PhpItems.Add(phpItem);
                }
            }
            else
            {
                FormularyItem deleteFromMaster = new FormularyItem
                {
                    Column = "Add/Delete",
                    Item = "Key Not Found, Delete Record?",
                    Passed = false
                };
                form.OtherItems.Add(deleteFromMaster);

                FormularyItem deleteMaster = new FormularyItem
                {
                    Column = "Add/Delete",
                    Item = "Delete From Master?",
                    Passed = false
                };
                form.PhpItems.Add(deleteMaster);

                foreach (DataColumn col in existing.Table.Columns)
                {
                    FormularyItem cvsItem = new FormularyItem
                    {
                        Column = col.ColumnName,
                        Item = "",
                        Passed = true
                    };
                    form.OtherItems.Add(cvsItem);

                    FormularyItem phpItem = new FormularyItem
                    {
                        Column = col.ColumnName,
                        Item = existing[col.ColumnName],
                        Passed = true
                    };
                    form.PhpItems.Add(phpItem);
                }
            }
            return form;
        }

        /// <summary>
        /// Does a comparison on two data rows passed in and provides status for each column pass or fail
        /// </summary>
        /// <param name="phpForm">Master Row Record</param>
        /// <param name="cvsForm">Comparing to Row Record</param>
        /// <param name="Key">GPI or NDC Uniquey Key</param>
        /// <param name="Table">C17, C18...</param>
        /// <returns></returns>
        public static FullItem formularyCompare(DataRow phpForm, DataRow cvsForm, string Key, string Table)
        {
            bool success = false;
            FullItem form = new FullItem
            {
                Type = Table,
                OtherItems = new List<FormularyItem>(),
                PhpItems = new List<FormularyItem>(),
                Key = Key,
                Passed = true
            };

            foreach (DataColumn col in phpForm.Table.Columns)
            {
                if (!col.ColumnName.ToUpper().Contains("ARCHIVEDATE"))
                {
                    if (phpForm[col.ColumnName].ToString().ToUpper() != cvsForm[col.ColumnName].ToString().ToUpper() && !ColumnExclusion.Any(x => col.ColumnName.ToUpper().Contains(x))) //want to do a test on if it is faster to swap these
                    {
                        success = false;
                        form.Passed = false;
                    }
                    else
                    {
                        success = true;
                    }

                    FormularyItem cvsItem = new FormularyItem
                    {
                        Column = col.ColumnName,
                        Item = cvsForm[col.ColumnName],
                        Passed = success
                    };
                    form.OtherItems.Add(cvsItem);
                    FormularyItem phpItem = new FormularyItem
                    {
                        Column = col.ColumnName,
                        Item = phpForm[col.ColumnName],
                        Passed = success
                    };
                    form.PhpItems.Add(phpItem);
                }
            }
            return form;
        }


        public static DataTable getGPINames(Logger procLog)
        {
            string gpiQuery = @"Select GPI, Min(FGD_Generic_Name) GPI_Name From
                                (
	                            Select Distinct GPI.GPI, Tier.FGD_Generic_Name
	                            From
		                            PHPConfg.WEB0020.GPI_Tier Tier
		                            Left Join
		                            PHPConfg.WEB0020.GPI GPI on GPI.GPI_LINE_PK = Tier.GPI_LINE_PK
                                Union
	                            Select Distinct GPI.GPI, Tier.FGD_Generic_Name
	                            From
		                            PHPArchv.WEB0020.GPI_Tier_CVS Tier
		                            Left Join
		                            PHPArchv.WEB0020.GPI_CVS GPI on GPI.GPI_LINE_PK = Tier.GPI_LINE_PK
		                            Where GPI.ArchiveDate = (SELECT TOP 1 ArchiveDate FROM [PHPArchv].[WEB0020].GPI_CVS ORDER BY ArchiveDate DESC)
                                ) GPIs
                                Group By GPI";
            return ExtractFactory.ConnectAndQuery(procLog.LoggerPhpConfig, gpiQuery);
        }

        public static DataTable getNDCNames(Logger procLog)
        {
            string ndcQuery = @"Select NDC, Min(LABEL_NAME) NDC_Name From
                                (
	                                Select Distinct NDC.NDC, NDC.LABEL_NAME
	                                From
		                                PHPConfg.WEB0020.NDC NDC
	                                Union
	                                Select Distinct NDC.NDC, NDC.LABEL_NAME
	                                From
		                                PHPArchv.WEB0020.NDC_CVS NDC
		                                Where NDC.ArchiveDate = (SELECT TOP 1 ArchiveDate FROM [PHPArchv].[WEB0020].NDC_CVS ORDER BY ArchiveDate DESC)
                                ) NDCs
                                Group By NDC";
            return ExtractFactory.ConnectAndQuery(procLog.LoggerPhpConfig, ndcQuery);
        }

        /// <summary>
        /// Base Query to pull all distinct GPI lines for a table from current master formulary, optinal only 1 GPI at a time
        /// </summary>
        /// <param name="table">Which Table to work with (C17, C18...)</param>
        /// <param name="GPI">>Which singular GPI to pull for</param>
        /// <returns></returns>
        private static string genConfgGPI(string table, string GPI = "*")
        {
            return string.Format(@"SELECT distinct *
  FROM [PHPConfg].[WEB0020].{0} {0}
  inner join [PHPConfg].WEB0020.GPI GPI on GPI.GPI_LINE_PK = {0}.GPI_LINE_PK
    WHERE GPI = '{1}'", table, GPI);
        }

        /// <summary>
        /// Base Query to pull all distinct GPI lines for a table from a historical file, optional only 1 GPI at a time
        /// </summary>
        /// <param name="table">Which Table to work with (C17, C18...)</param>
        /// <param name="archvDate">Date for compare</param>
        /// <param name="GPI">Which singular GPI to pull for</param>
        /// <returns>Query String</returns>
        private static string genArchvCompGPI(string table, string archvDate, string GPI = "*")
        {
            return string.Format(@"SELECT distinct *
  FROM [PHPArchv].[WEB0020].[{0}_CVS] {0}
  inner join [PHPArchv].WEB0020.GPI_CVS GPI on GPI.GPI_LINE_PK = {0}.GPI_LINE_PK AND GPI.ArchiveDate = {0}.ArchiveDate
    WHERE GPI = '{2}' AND GPI.ArchiveDate = '{1}'
    ", table, archvDate, GPI);
        }

        private static string genConfgNDC(string NDC)
        {
            string query = string.Format(@"SELECT distinct *
              FROM [PHPConfg].[WEB0020].NDC NDC
              WHERE NDC.NDC = '{0}'", NDC);
            return query;
        }

        private static string genArchvNDC(string NDC, string archvDate)
        {
            return string.Format(@"SELECT distinct *
                FROM [PHPArchv].[WEB0020].[NDC_CVS] NDC
                WHERE NDC = '{0}' AND NDC.ArchiveDate = '{1}'", NDC, archvDate);
        }


        /// <summary>
        /// Parsed down query of the GAP45 table from master to remove NDC duplication, Multiple formularies returned
        /// </summary>
        /// <param name="GPI">GPI being pulled across formularies</param>
        /// <returns>Query String</returns>
        private static string genGAP45(string GPI, string archvDate = "")
        {
            string db = "PHPArchv";
            string underscore = "_CVS";
            string archive = $" and GAP45.ArchiveDate = '{archvDate}'";
            if(archvDate == "")
            {
                db = "PHPConfg";
                underscore = "";
                archive = "";
            }
            string query = string.Format(@"SELECT Distinct
	                                       GPI.GPI_LINE_PK
	                                      ,GPI.Formulary
	                                      ,GPI.GPI
	                                      ,[Multi_Source_Code]
	                                      ,[Product_Type]
                                          ,[PA_Flag]
                                          ,[Smart_PA_Flag]
                                          ,[Step_Therapy_Flag]
                                          ,[Age_Flag]
                                          ,[Gender_Flag]
                                          ,[QL_Flag]
                                          ,[Age_Range_Flag]
                                          ,[Sex_Exclusion]
                                          ,[Qty_Min]
                                          ,[Qty_Max]
                                          ,[DS_Min]
                                          ,[DS_Max]
                                          ,[Qty_Ds_Comp]
                                          ,[Period_Qty_Type]
                                          ,[Period_Qty_Days]
                                          ,[Period_Qty_Max]
                                          ,[Period_DS_Type]
                                          ,[Period_DS_Days]
                                          ,[Period_DS_Max]
                                          ,[Period_Qty_Ds_Comp]
                                          ,[Period_Fills_Type]
                                          ,[Period_Fills_Days]
                                          ,[Period_Fills_Max]
                                          ,[Refill_Limit_Max_Number]
                                          ,[Refill_Expire_After_Days]
                                          ,[Amt_Due_Type]
                                          ,[Amt_Due_Days]
                                          ,[Amt_Due_Max]
                                          ,[Amt_Due_Basis]
                                          ,[Patient_Age_Min]
                                          ,[Patient_Age_Max]
                                          ,[DD_Min]
                                          ,[DD_Max]
                                          ,[Acute_Dose_Days_Max]
                                          ,[Maint_Dose_Days_Max]
                                          ,[Otc_Override]
                                          ,[Unit_Dose_Use]
                                          ,[FDA_Therapeutic_Equiv]
                                          ,[Route_Of_Admin]
                                          ,[Maint_Drug_STS]
                                          ,[Brand_Generic_Edit]
                                          ,[PTD_From]
                                          ,[Limit_Days_Ovr]
                                          ,[Refill_Code_Ovr]
                                          ,[Days_Supply_Fill_Limit]
                                          ,[Formulary_Id]
                                          ,[Formulary_Source_Code]
                                          ,[Formulary_Flag]
                                          ,[Preferred_Drug_List]
                                          ,[Formulary_Compliance_Code]
                                          ,[Tier_Number_Display]
                                          ,[Tier_Total_Display]
                                          ,[Tier_Code]
	                                      ,Gap45.ArchiveDate
                                      FROM {0}.WEB0020.GPI{1} GPI Inner Join
                                      {0}.WEB0020.Gap45{1} Gap45 on GPI.GPI_LINE_PK = Gap45.GPI_LINE_PK{2}
                                      Where GPI.GPI = '{3}'",db, underscore, archive, GPI);
            return query;
        }

        public class Formulary
        {
            public bool Passed { get; set; }
            public ConcurrentBag<FullItem> GPIs { get; set; }
            public ConcurrentBag<FullItem> NDCs { get; set; }
        }

        public class FullItem
        {
            public string Key { get; set; }
            public string Type { get; set; }
            public bool Passed { get; set; }
            public List<FormularyItem> PhpItems { get; set; }
            public List<FormularyItem> OtherItems { get; set; }
            public List<ItemCompareFlattened> ItemCompareFlattened()
            {
                List<ItemCompareFlattened> flats = new List<ItemCompareFlattened>();
                int i = 0;
                foreach(FormularyItem item in PhpItems)
                {
                    ItemCompareFlattened temp = new ItemCompareFlattened(this, i);
                    if (!temp.ColPassed)
                    {
                        flats.Add(temp);
                    }
                    i++;
                }
                return flats;
            }
        }

        public class FormularyItemV2
        {
            public string Formulary { get; set; }
            public string? FormularyEsi { get; set; }
            public bool FormularyIsEqual { get; set; }
            public string LabelName { get; set; }
            public string? LabelNameEsi { get; set; }
            public bool LabelNameIsEqual { get; set; }
            public DateTime EffectiveDate { get; set; }
            public DateTime? EffectiveDateEsi { get; set; }
            public bool EffectiveDateIsEqual { get; set; }
            public DateTime TermDate { get; set; }
            public DateTime? TermDateEsi { get; set; }
            public bool TermDateIsEqual { get; set; }
            public string M { get; set; }
            public string? MEsi { get; set; }
            public bool MIsEqual { get; set; }
            public string O { get; set; }
            public string? OEsi { get; set; }
            public bool OIsEqual { get; set; }
            public string N { get; set; }
            public string? NEsi { get; set; }
            public bool NIsEqual { get; set; }
            public string Y { get; set; }
            public string? YEsi { get; set; }
            public bool YIsEqual { get; set; }
            public string FND_CUR_STS { get; set; }
            public string? FND_CUR_STS_Esi { get; set; }
            public bool FND_CUR_STS_IsEqual { get; set; }
            public string Rx { get; set; }
            public string? RxEsi { get; set; }
            public bool RxIsEqual { get; set; }
            public string OverTheCounter { get; set; }
            public string? OverTheCounterEsi { get; set; }
            public bool OverTheCounterIsEqual { get; set; }
            public string Key { get; set; }
            public string Tier { get; set; }
            public string Type { get; set; }
            public bool Passed {
                get
                {
                    return FormularyIsEqual &&
                        EffectiveDateIsEqual &&
                        TermDateIsEqual &&
                        MIsEqual &&
                        OIsEqual &&
                        NIsEqual &&
                        YIsEqual &&
                        FND_CUR_STS_IsEqual &&
                        RxIsEqual &&
                        OverTheCounterIsEqual;
                }
            }

            public FormularyCompareIssue Issue
            {
                get
                {
                    if (FormularyEsi == null &&
                        EffectiveDateEsi == null &&
                        TermDateEsi == null &&
                        MEsi == null &&
                        OEsi == null &&
                        NEsi == null &&
                        YEsi == null &&
                        FND_CUR_STS_Esi == null &&
                        RxEsi == null &&
                        OverTheCounterEsi == null)
                    {
                        return FormularyCompareIssue.RecordNotInPBM;
                    }
                    else if (Formulary == null &&
                        EffectiveDate == null &&
                        TermDate == null &&
                        M == null &&
                        O == null &&
                        N == null &&
                        Y == null &&
                        FND_CUR_STS == null &&
                        Rx == null &&
                        OverTheCounter == null)
                    {
                        return FormularyCompareIssue.RecordNotInLocal;
                    }
                    else
                    {
                        return FormularyCompareIssue.None;
                    }
                }
            }
        }

        public enum FormularyCompareIssue
        {
            None,
            RecordNotInPBM,
            RecordNotInLocal
        }

        public class FormularyItem
        {
            public string Column { get; set; }
            public dynamic Item { get; set; }
            public bool Passed { get; set; }
        }

        public class ItemCompareFlattened
        {
            public string ItemKey { get; set; }
            public string ItemType { get; set; }
            public bool ItemPassed { get; set; }
            public bool ColPassed { get; set; }
            public string MasterColumn { get; set; }
            public string MasterItem { get; set; }
            public string Column { get; set; }
            public string Item { get; set; }
            public ItemCompareFlattened(FullItem FI, int i)
            {
                ItemKey = FI.Key;
                ItemType = FI.Type;
                ItemPassed = FI.Passed;
                ColPassed = FI.OtherItems[i].Passed && FI.PhpItems[i].Passed;
                MasterColumn = FI.PhpItems[i].Column;
                MasterItem = Convert.ToString(FI.PhpItems[i].Item);
                Column = FI.OtherItems[i].Column;
                Item = Convert.ToString(FI.OtherItems[i].Item);
            }
        }

        public class CompareItem
        {
            public string Table { get; set; }
            public string GPI { get; set; }
            public string Formulary { get; set; }
            public string Tier { get; set; }
            public string NDC { get; set; }
            public bool Resolved { get; set; }
            public DateTime ArchiveDate { get; set; }
        }
    }
}
