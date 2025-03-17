using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Linq;
using System.Data.SqlClient;
using System.Data;
using System.Data.Odbc;
using System.ComponentModel;
using System.Xml;
using System.IO;
using System.Text.RegularExpressions;
using System.ComponentModel.DataAnnotations;

namespace Utilities
{

    public class ExtractFactory
    {

        /// <summary>
        /// New extract to excel method that doesn't require a class container and sends the email based on whether results are found
        /// </summary>
        /// <param name="query">Query to execute against the data source</param>
        /// <param name="dataSource">Data source </param>
        /// <param name="excelSheetName">Name of the sheet inside the excel file</param>
        /// <param name="procLog">Calling program (this)</param>
        /// <param name="fileNameSuccessSubject">Name of the file and the subject line of the email if records are found</param>
        /// <param name="SuccessBody">Body of the email to send if records are found</param>
        /// <param name="zeroSubject">Subject of the email to send if no records are found</param>
        /// <param name="zeroBody">Body of the email to send if records are not found</param>
        /// <param name="zeroExitCode">Exit code to use if no records are found (e.g. 0 if this is acceptable, 6000 if not)</param>
        /// <param name="removeIllegalXMLChars">Scubs the query for illegal XML characters that could break the excel output</param>
        /// <param name="overrideSharepointDeliver">Overrides the built-in behavior to deliver excel reports to PHP's SharePoint site</param>
        public static string RunExcelExtract(String query, Data.AppNames dataSource, String excelSheetName, Logger procLog, String fileNameSuccessSubject, String SuccessBody, String zeroSubject, String zeroBody, int zeroExitCode, bool multipleSheets = false, bool removeIllegalXMLChars = false, bool overrideSharepointDeliver = false, bool outputZeroResults = false)
        {
            procLog.WriteToLog("Query: " + query);

            DataTable fileLoaded = ConnectAndQuery(dataSource, query);

            int rowCount = fileLoaded.Rows.Count;

            procLog.WriteToLog("Records found: " + rowCount);

            FileSystem.ReportYearDir(procLog.LoggerOutputYearDir);

            if (rowCount > 0 || outputZeroResults)
            {
                String outputLoc = procLog.LoggerOutputYearDir + procLog.ProcessId + "_" + fileNameSuccessSubject + "_" + DateTime.Today.ToString("yyyyMMdd") + ".xlsx";

                if (System.IO.File.Exists(outputLoc) && !multipleSheets)
                {
                    System.IO.File.Delete(outputLoc);
                }

                if (removeIllegalXMLChars)
                {
                    EscapeXMLCharactersInDataTable(fileLoaded);
                }

                //ExcelWork.OutputDataTableToExcel(fileLoaded, excelSheetName, outputLoc);

                procLog.WriteToLog("Attaching the report and sending success email");

                if (overrideSharepointDeliver)
                {
                    SendAlerts.Send(procLog.ProcessId, 0, fileNameSuccessSubject, AddStatistics(SuccessBody, outputLoc, rowCount, true), procLog, outputLoc, SendSecure: true);
                }
                else
                {
                    SendAlerts.PublishAndNotify(procLog.ProcessId, 0, fileNameSuccessSubject, AddStatistics(SuccessBody, outputLoc, rowCount, true), procLog, outputLoc);
                }


                return outputLoc;
            }
            else
            {
                procLog.WriteToLog("The extract didn't find any records, sending zero records email");

                if (overrideSharepointDeliver)
                {
                    SendAlerts.Send(procLog.ProcessId, 4, zeroSubject, zeroBody, procLog);
                }
                else
                {
                    String zeroOutPath = procLog.LoggerOutputYearDir + zeroSubject + " " + DateTime.Today.ToString("yyyyMMdd") + ".txt";
                    System.IO.File.WriteAllText(zeroOutPath, zeroBody);

                    SendAlerts.PublishAndNotify(procLog.ProcessId, zeroExitCode, zeroSubject, zeroBody, procLog, zeroOutPath);
                }


                return "";

            }



        }

        public static Boolean RunExcelExtract(String query, Data.AppNames dataSource, String excelSheetName, Logger proclog, String outFile, bool multipleSheets = false, bool removeIllegalXMLChars = false, bool outputZeroResults = false, string dateFormat = "MM/dd/yyyy HH:mm:ss AM/PM")
        {
            proclog.WriteToLog("Query: " + query);

            DataTable fileLoaded = ConnectAndQuery(dataSource, query);

            int rowCount = fileLoaded.Rows.Count;

            proclog.WriteToLog("Records found: " + rowCount);

            if (rowCount > 0 || outputZeroResults)
            {
                if (System.IO.File.Exists(outFile) && !multipleSheets)
                {
                    System.IO.File.Delete(outFile);
                }

                if (removeIllegalXMLChars)
                {
                    EscapeXMLCharactersInDataTable(fileLoaded);
                }
                
                //ExcelWork.OutputDataTableToExcel(fileLoaded, excelSheetName, outFile, dateFormat: dateFormat);

                return true;
            }
            else
            {
                proclog.WriteToLog("The extract didn't find any records, sending zero records email");
                return false;
            }

        }

        /// <summary>
        /// Runs an Excel Extract, much like the original extract method; however, this method is meant to accept multiple queries in the @queryList parameter. The queries are ran against 
        /// the specified @dataSource using the same connection to produce a DataSet object. The DataSet object will be extracted into an Excel Spreadsheet named @fileName, each DataTable
        /// being placed in its own sheet in the workbook. The optional parameter @sheetNames allows the caller to specify the name of each sheet.
        /// 
        /// Please note: If the @dataSource is an Oracle DB, you may need to set @isOracleDb to true. The Oracle SQL engine seems to behave differently and requires a different approach.
        /// </summary>
        /// <param name="dataSource">The database to connect to</param>
        /// <param name="queryList">A list of queries to run against the specified database</param>
        /// <param name="proc">The calling job</param>
        /// <param name="fileName">Desired name of the Excel Workbook, excluding the file extension</param>
        /// <param name="successSubject">Subject line of the success email</param>
        /// <param name="successBody">Email body upon successful run</param>
        /// <param name="zeroExitCode">Exit code if zero results are returned</param>
        /// <param name="zeroSubject">Email subject line if zero results are returned</param>
        /// <param name="zeroBody">Email body if zero results are returned</param>
        /// <param name="sheetNames">A list of names (one for each expected DataTable being returned) to rename the Worksheets in the Workbook</param>
        /// <param name="removeIllegalXMLChars">Scrubs the result set(s) for illegal XML characters that could break the excel output</param>
        /// <param name="deliverToSharepoint">Determines whether the report will be delivered to sharepoint upon completion</param>
        /// <returns>Returns the full file path of the generated report. If no results were returned, returns an empty string</returns>
        public static String RunExcelExtractForDataSet(Data.AppNames dataSource, List<String> queryList, Logger proc, String fileName, String successSubject, String successBody, int zeroExitCode,
                            String zeroSubject, String zeroBody, List<String> sheetNames = null, bool removeIllegalXMLChars = false, bool deliverToSharepoint = false, bool isOracleDb = false)
        {
            DataSet dataSet;
            
            if (isOracleDb)
            {
                dataSet = ConnectAndQuery_OracleDataset(dataSource, queryList);
            }
            else
            {
                dataSet = ConnectAndQuery_Dataset(dataSource, String.Join(";", queryList));
            }

            if (removeIllegalXMLChars)
            {
                foreach (DataTable dt in dataSet.Tables)
                {
                    EscapeXMLCharactersInDataTable(dt);
                }
            }

            // Create the directory if it doesn't exist and the base file name.
            FileSystem.ReportYearDir(proc.LoggerOutputYearDir);
            string outputFile = proc.LoggerOutputYearDir + "{0}" + fileName + "_" + DateTime.Today.ToString("yyyyMMdd") + ".xlsx";
            
            // Add the Job Code to the file name if going to sharepoint - otherwise leave it alone.
            if (deliverToSharepoint)
            {
                outputFile = String.Format(outputFile, "_" + proc.ProcessId);
            }
            else
            {
                outputFile = String.Format(outputFile, "");
            }
            
            // Generate the report.
            ExcelWork.OutputDataSetToExcel(dataSet, outputFile, sheetNames: sheetNames);

            if (File.Exists(outputFile)) // If any data was returned.
            {
                if (deliverToSharepoint)
                {
                    proc.WriteToLog("Report successfully created. Uploading to SharePoint and sending success email.");
                    SendAlerts.PublishAndNotify(proc.ProcessId, 0, successSubject, successBody, proc, outputFile);
                }
                else
                {
                    proc.WriteToLog("Report successfully created. Attaching and sending success email.");
                    SendAlerts.Send(proc.ProcessId, 0, successSubject, successBody, proc, outputFile, SendSecure: true);
                }
            }
            else // No data returned from any of the queries.
            {
                proc.WriteToLog("The extract didn't find any records, sending zero records email");
                outputFile = "";
                SendAlerts.Send(proc.ProcessId, zeroExitCode, zeroSubject, zeroBody, proc);
            }

            return outputFile;
        }

        /// <summary>
        /// This is a helper method for the main RunExcelExtractForDataSet(). Allows the caller to pass in a string with multiple queries, separated by a semi-colon (;), instead of
        /// a List<>. For more information on the method, please see the primary method description.
        /// </summary>
        /// <returns>Returns the full file path of the generated report. If no results were returned, returns an empty string</returns>
        public static String RunExcelExtractForDataSet(Data.AppNames dataSource, String queryList, Logger proc, String fileName, String successSubject, String successBody,
                                int zeroExitCode, String zeroSubject, String zeroBody, List<String> sheetNames = null, bool removeIllegalXMLChars = false, bool deliverToSharepoint = false)
        {
            List<String> newList = queryList.Split(';').ToList();

            // If the string ends with a semi-colon(;), the last element produced in the split may be an empty string.
            if (newList.Last().Trim().Equals(""))
            {
                newList.Remove(newList.Last()); // Remove the last string in the list.
            }

            return RunExcelExtractForDataSet(dataSource, newList, proc, fileName, successSubject, successBody, zeroExitCode, zeroSubject, zeroBody,
                                                    sheetNames: sheetNames, removeIllegalXMLChars: removeIllegalXMLChars, deliverToSharepoint: deliverToSharepoint);
        }

        /// <summary>
        /// A simplified version of RunExcelExtractForDataSet that does NOT send email alerts upon finishing.
        /// </summary>
        /// <returns>Returns the full file path of the generated report. If no results were returned, returns an empty string</returns>
        public static String RunExcelExtractForDataSet(Data.AppNames dataSource, List<String> queryList, Logger proc, String fileName, List<String> sheetNames = null, bool removeIllegalXMLChars = false, bool isOracleDb = false)
        {
            DataSet dataSet;

            if (isOracleDb)
            {
                dataSet = ConnectAndQuery_OracleDataset(dataSource, queryList);
            }
            else
            {
                dataSet = ConnectAndQuery_Dataset(dataSource, String.Join(";", queryList));
            }

            if (removeIllegalXMLChars)
            {
                foreach (DataTable dt in dataSet.Tables)
                {
                    EscapeXMLCharactersInDataTable(dt);
                }
            }

            // Create the directory if it doesn't exist and the base file name.
            FileSystem.ReportYearDir(proc.LoggerOutputYearDir);
            string outputFile = proc.LoggerOutputYearDir + fileName + "_" + DateTime.Now.ToString("yyyyMMddhhmmss") + ".xlsx";

            int i = 0;
            foreach(DataTable table in dataSet.Tables)
            {
                proc.WriteToLog(string.Format("Table {0} Record Count: ", i.ToString()) + table.Rows.Count, UniversalLogger.LogCategory.INFO);
                i++;
            }
            // Generate the report.
            ExcelWork.OutputDataSetToExcel(dataSet, outputFile, sheetNames: sheetNames);
            
            if (File.Exists(outputFile))
            {
                return outputFile;
            }
            else
            {
                return "";
            }
        }

        /// <summary>
        /// This is a helper method for the main RunExcelExtractForDataSet(). Allows the caller to pass in a string with multiple queries, separated by a semi-colon (;), instead of
        /// a List<>. For more information on the method, please see the primary method description. This method does NOT send an email upon completion.
        /// </summary>
        /// <returns>Returns the full file path of the generated report. If no results were returned, returns an empty string</returns>
        public static String RunExcelExtractForDataSet(Data.AppNames dataSource, String queryList, Logger proc, String fileName, List<String> sheetNames = null, bool removeIllegalXMLChars = false, bool isOracleDb = false)
        {
            List<String> newList = queryList.Split(';').ToList();

            // If the string ends with a semi-colon(;), the last element produced in the split may be an empty string.
            if (newList.Last().Trim().Equals(""))
            {
                newList.Remove(newList.Last()); // Remove the last string in the list.
            }

            return RunExcelExtractForDataSet(dataSource, newList, proc, fileName, sheetNames: sheetNames, removeIllegalXMLChars: removeIllegalXMLChars, isOracleDb: isOracleDb);
        }


        /// <summary>
        /// A pared down version of the RunTextExtract method that doesn't send emails or do FTPs, but does return the IEnumerable from the database
        /// </summary>
        /// <typeparam name="T">Type of the records to load/export</typeparam>
        /// <param name="DataSource">Database to query</param>
        /// <param name="Query">Query to hit against the database</param>
        /// <param name="separator">Field separator in the output. "" means there are no separators, in which case your SQL query should have the appropriate widths built-in</param>
        /// <param name="processLog">Calling program (this)</param>
        /// <param name="outputLoc">Full URI for the desired output file</param>
        /// <param name="zipOutput">If the output file should be zipped after it is output</param>
        /// <param name="TimeOut">Time-out counter for the extract, default is 240 seconds (4 minutes)</param>
        /// <param name="AddHeaders">Add headers from the given class T to the output file</param>
        /// <returns>Whether or not the extract found any records</returns>
        public static IEnumerable<T> RunTextExtract<T>(Data.AppNames DataSource, String Query, String separator, Logger processLog, String outputLoc, Boolean zipOutput = false, int TimeOut = 240, Boolean AddHeaders = false, Boolean endWithSeparator = true)
        {
            return RunTextExtract<T>(DataSource, Query, separator, processLog, "", "", "", "", outputLoc, zipOutput, TimeOut, "", "", "", AddHeaders: AddHeaders, endWithSeparator: endWithSeparator);
        }


        /// <summary>
        /// Extract factory using the bulktext output method, that doesn't require classes. This will ftp and send emails according to the parameters.
        /// </summary>
        /// <param name="datasource">Data source to query against</param>
        /// <param name="query">Query to find results</param>
        /// <param name="separator">Field separator to write, note if this is "" then lengths should be built into the SQL statement</param>
        /// <param name="proclog">Calling process (this)</param>
        /// <param name="outputLoc">Where to output the file</param>
        /// <param name="ftpSite">Site to push the file to</param>
        /// <param name="changeDir">Directory on the ftp site to push the file to</param>
        /// <param name="successSubject">Subject of the email to send if records are found and the file is successfully ftp'd</param>
        /// <param name="successBody">Body of the email to send if records are found - note we will also add the record count and ftp site to the email body</param>
        /// <param name="zeroSubject">Subject of the email to send if records are not found by the query (note, if left blank we will not send an email)</param>
        /// <param name="zeroBody">Body of the email to send if records are not found by the query</param>
        /// <param name="zeroExitCode">Exit code to place on the email if records are not found (e.g. 6000 if this is a problem)</param>
        /// <param name="TimeOut">Timeout for query</param>
        /// <param name="AddHeaders">If headers from the SQL statement should be output (note this cannot be used in conjunction with headerString</param>
        /// <param name="endWithSep">If the rows of the file should end with the given separator</param>
        /// <param name="headerString">String to insert in the first row of the file (note this cannot be used in conjunction with AddHeaders)</param>
        /// <param name="trailerString">String to insert in the last row of the file (note, string formats are supported for record counts)</param>
        /// <param name="compressOutput">Uses 7-zip to compress the output before sending it off</param>
        /// <param name="ftpRouteOverride">Overrides the FTP route that we should drop to, if left blank we use the calling process id</param>
        /// <param name="IncludeRecCountInEmail">If true, we will insert the record count into the body of the email generated by Rhapsody (via the rhapsody config table)</param>
        /// <param name="PushFile">If true, will drop the file to the Rhapsody share to allow it to deliver the file to the remote server</param>
        /// <param name="recordBrace">Brace to implement around each value (e.g. double quotes)</param>
        /// <param name="trailerStringHandler">Delegate to build the trailer row leveraging the record count</param>
        public static int RunTextExtract(Data.AppNames datasource, String query, String separator, Logger proclog, String outputLoc, String zeroSubject = "", String zeroBody = "", int zeroExitCode = 0, int TimeOut = 240, Boolean AddHeaders = false, Boolean endWithSep = true, String headerString = "", String trailerString = "", Boolean compressOutput = false, String recordBrace = "", String ftpRouteOverride = "", Boolean IncludeRecCountInEmail = false, Boolean PushFile = true)
        {
            int recordCount = OutputFile.OutputBulkText(datasource, query, separator, proclog, outputLoc, TimeOut, AddHeaders, endWithSep, true, headerString, trailerString, recordBrace);

            if (compressOutput && recordCount > 0)
            {

                OutputFile.CompressOutputFile(proclog, outputLoc);
                outputLoc = System.IO.Path.ChangeExtension(outputLoc, ".zip"); //we want to send the compressed file out, not the original
            }

            if (recordCount > 0)
            {


                proclog.WriteToLog("There were " + recordCount.ToString() + " found by your query.");

                if (IncludeRecCountInEmail)
                {
                    SendAlerts.UpdateRhapsodyBodyCount(proclog, recordCount);
                }

                if (PushFile) //allows us to run delta files through without pushing to 
                {
                    proclog.WriteToLog("Now ftping the output file");

                    if (ftpRouteOverride == "")
                    {
                        FileTransfer.DropToPhpDoorStep(proclog, outputLoc);
                    }
                    else
                    {
                        FileTransfer.DropToPhpDoorStep(proclog, outputLoc, progCodeOverride: ftpRouteOverride);
                    }
                }
            }
            else if (zeroSubject != "")
            {
                SendAlerts.Send(proclog.ProcessId, zeroExitCode, zeroSubject, zeroBody, proclog);
            }

            return recordCount;
        }


        /// <summary>
        /// A full version of RunText Extract that allows you to push to an FTP site and send emails (if the subject lines are not ""), and returns the result set back to the caller
        /// </summary>
        /// <typeparam name="T">Type of the result</typeparam>
        /// <param name="DataSource">Database to query</param>
        /// <param name="Query">SQL query to hit against the database</param>
        /// <param name="separator">Field separator, can be "" in which case your widths should be built into the SQL statement</param>
        /// <param name="processLog">The calling program (this)</param>
        /// <param name="SuccessSubject">Subject of the email if records are found</param>
        /// <param name="SuccessBody">Body of the email if records are found</param>
        /// <param name="ZeroSubject">Subject of the email if records are NOT found</param>
        /// <param name="ZeroBody">Body of the email if records are NOT found</param>
        /// <param name="outputLoc">Location to output the file to (we will do a ReportYearDir call for you)</param>
        /// <param name="zipOutput">Compress the file before pushing to an FTP site</param>
        /// <param name="TimeOut">Override timeout for the query</param>
        /// <param name="ftpSite">Site to push the file to after it is written</param>
        /// <param name="ftpChangeDir">Change dir call after connecting to FTP site</param>
        /// <param name="destRename">Rename the file on writing</param>
        /// <param name="AddHeaders">Add headers to the output file, these are from the properties of the class T</param>
        /// <returns></returns>
        public static IEnumerable<T> RunTextExtract<T>(Data.AppNames DataSource, String Query, String separator, Logger processLog, String SuccessSubject, String SuccessBody, String ZeroSubject, String ZeroBody, String outputLoc, Boolean zipOutput = false, int TimeOut = 240, String ftpSite = "", String ftpChangeDir = "", String destRename = "", Boolean AddHeaders = false, Boolean endWithSeparator = true, Boolean QuoteQualify = false)
        {

            processLog.WriteToLog("Query: " + Query);
            IEnumerable<T> results = ConnectAndQuery<T>(DataSource, Query, TimeOut);

            int recordCount = results.Count();
            processLog.WriteToLog("Number of records found: " + recordCount.ToString());

            FileSystem.ReportYearDir(System.IO.Path.GetDirectoryName(outputLoc));

            ResponseToExtract(recordCount, results, outputLoc, separator, zipOutput, processLog, ftpSite, ftpChangeDir, destRename, SuccessSubject, SuccessBody, ZeroSubject, ZeroBody, zipOutput, addHeaders: AddHeaders, endWithSeparator: endWithSeparator, quoteQualify: QuoteQualify);

            return results;

        }

        private static void ResponseToExtract<T>(int recordCount, IEnumerable<T> results, String outputLoc, String separator, Boolean ZipOutput, Logger processLog, String ftpSite,
                String ftpChangeDir, String destRename, String SuccessSubject, String SuccessBody, String ZeroSubject, String ZeroBody, Boolean zipOutput, Boolean addHeaders, Boolean endWithSeparator, Boolean quoteQualify)
        {
            if (recordCount > 0)
            {
                outputLoc = OutputFile.WriteSeparated<T>(outputLoc, results, separator, zipOutput, addHeaders, endWithSeparator, quoteQualify);
                processLog.WriteToLog("Output results to separated file");
                processLog.WriteToLog("Output to: " + outputLoc);

                if (ftpSite != "")
                {
                    processLog.WriteToLog("An FTP site was specified, passing control to Utilities.FileTransfer");

                    SendAlerts.UpdateRhapsodyBodyCount(processLog, recordCount);
                    FileTransfer.DropToPhpDoorStep(processLog, outputLoc);

                    processLog.WriteToLog("File transfer complete");
                }

            }
            else
            {
                if (ZeroSubject != "")
                {
                    SendAlerts.Send(processLog.ProcessId, 4, ZeroSubject, ZeroBody, processLog);
                    processLog.WriteToLog("Sent no results email");
                }
                else
                {
                    processLog.WriteToLog("Not sending an email notifying the user that no records were output because the Zero subject line is blank");
                }

            }
        }

        /// <summary>
        /// Connects to the given database and returns a datatable object with the results
        /// </summary>
        /// <param name="dataSource">Data source to connect and query</param>
        /// <param name="query">Query to hit against database</param>
        /// <param name="timeout">Time-out for entire command to complete, default is 0 (infinite)</param>
        /// <returns>Loaded data table object</returns>
        [Obsolete("Use overloaded version that requires Logger to be passed in for query logging")]
        public static DataTable ConnectAndQuery(Data.AppNames dataSource, String query, int timeout = 0)
        {
            DataTable fileLoaded = new DataTable();
            Data sourceCreds = new Data(dataSource);
            return ConnectAndQuery(sourceCreds, query, timeout);
        }

        /// <summary>
        /// Connects to the given database and returns a datatable object with the results
        /// </summary>
        /// <param name="dataSource">Data source to connect and query</param>
        /// <param name="query">Query to hit against database</param>
        /// <param name="timeout">Time-out for entire command to complete, default is 0 (infinite)</param>
        /// <returns>Loaded data table object</returns>
        public static DataTable ConnectAndQuery(Logger logger, Data.AppNames dataSource, String query, int timeout = 0)
        {
            logger.WriteToLog($"Here is the query that was passed in: {query}");
            
            return ConnectAndQuery(dataSource, query, timeout);
        }

        /// <summary>
        /// Connects to the given database and returns a datatable object with the results, but by requesting a Data object it 
        /// will have improved runtime because it retains DataConfiguration.xml info
        /// </summary>
        /// <param name="source">Data source to connect and query</param>
        /// <param name="query">Query to hit against database</param>
        /// <param name="timeout">Time-out for entire command to complete, default is 0 (infinite)</param>
        /// <returns>Loaded data table object</returns>
        [Obsolete("Use overloaded version that requires Logger to be passed in for query logging")]
        public static DataTable ConnectAndQuery(Data source, String query, int timeout = 0, int tries = 2)
        {
            try
            {
                DataTable fileLoaded = new DataTable();

                Data.AppNames dataSource = source.ApplicationName;

                if (DataWork.IsOdbcDataSource(dataSource))
                {
                    using (OdbcConnection OdbcConn = source.GetOdbcConnection())
                    {
                        OdbcCommand cmd = new OdbcCommand(query, OdbcConn);
                        cmd.CommandTimeout = timeout;
                        using (OdbcDataAdapter dAdapter = new OdbcDataAdapter(cmd))
                        {
                            dAdapter.Fill(fileLoaded);
                        }
                    }

                }
                else
                {
                    using (SqlConnection SqlConn = source.GetSqlConnection(source.Authentication))
                    {
                        SqlConn.Open();
                        SqlCommand sqlCmd = new SqlCommand(query, SqlConn);
                        sqlCmd.CommandTimeout = timeout;
                        using (SqlDataAdapter sqlAdapter = new SqlDataAdapter(sqlCmd))
                        {
                            sqlAdapter.Fill(fileLoaded);
                        }
                    }

                }

                return fileLoaded;
            }
            catch(Exception Ex)
            {
                tries--;
                if(tries > 0)
                {
                    System.Threading.Thread.Sleep(1 * 60 * 1000);//Wait for 1 minute before trying again
                    return ConnectAndQuery(source, query, timeout, tries);
                }
                else
                {
                    throw Ex;
                }
            }
        }

        public static Dictionary<string, DataTable> ConnectAndQueryTables(Data source, String query, List<string> tableNames, Logger procLog = null, int timeout = 0, int tries = 2)
        {
            Dictionary<string, DataTable> tables = new Dictionary<string, DataTable>();
            Data.AppNames dataSource = source.ApplicationName;
            try
            {
                using (SqlConnection SqlConn = source.GetSqlConnection(source.Authentication))
                {
                    SqlConn.Open();
                    SqlCommand sqlCmd = new SqlCommand(query, SqlConn);
                    sqlCmd.CommandTimeout = timeout;
                    using (SqlDataAdapter sqlAdapter = new SqlDataAdapter(sqlCmd))
                    {
                        DataTable fileLoaded = new DataTable();
                        sqlAdapter.Fill(fileLoaded);
                        int i = 0;
                        foreach (string tableName in tableNames)
                        {
                            tables.Add(tableName, fileLoaded);
                            if (++i < tableNames.Count())
                            {

                            }
                        }
                    }
                }
            }
                
                
            catch (Exception Ex)
            {
               
            }
            return tables;
        }
        /// <summary>
        /// Connects to the given database and returns a datatable object with the results, but by requesting a Data object it 
        /// will have improved runtime because it retains DataConfiguration.xml info
        /// </summary>
        /// <param name="source">Data source to connect and query</param>
        /// <param name="query">Query to hit against database</param>
        /// <param name="timeout">Time-out for entire command to complete, default is 0 (infinite)</param>
        /// <returns>Loaded data table object</returns>
        public static DataTable ConnectAndQuery(Logger logger, Data source, string query, int timeout = 0, int tries = 2)
        {
            logger.WriteToLog($"Here is the query that was passed in: {query}");

            return ConnectAndQuery(source, query, timeout, tries);
        }

        /// <summary>
        /// Connects to the given database and returns a dataset object with the results
        /// </summary>
        /// <param name="dataSource">Data source to connect and query</param>
        /// <param name="query">Query to hit against database</param>
        /// <param name="timeout">Time-out for entire command to complete, default is 0 (infinite)</param>
        /// <returns>Loaded data set object</returns>
        [Obsolete("Use overloaded version that requires Logger to be passed in for query logging")]
        public static DataSet ConnectAndQuery_Dataset(Data.AppNames dataSource, String query, int timeout = 0, int tries = 2)
        {
            try
            {
                DataSet fileLoaded = new DataSet();
                Data sourceCreds = new Data(dataSource);

                if (DataWork.IsOdbcDataSource(dataSource))
                {
                    using (OdbcConnection OdbcConn = sourceCreds.GetOdbcConnection())
                    {
                        OdbcCommand cmd = new OdbcCommand(query, OdbcConn);
                        cmd.CommandTimeout = timeout;
                        using (OdbcDataAdapter dAdapter = new OdbcDataAdapter(cmd))
                        {
                            dAdapter.Fill(fileLoaded);
                        }
                    }
                }
                else
                {
                    using (SqlConnection SqlConn = sourceCreds.GetSqlConnection(sourceCreds.Authentication))
                    {
                        SqlConn.Open();
                        SqlCommand sqlCmd = new SqlCommand(query, SqlConn);
                        sqlCmd.CommandTimeout = timeout;
                        using (SqlDataAdapter sqlAdapter = new SqlDataAdapter(sqlCmd))
                        {
                            sqlAdapter.Fill(fileLoaded);
                        }
                    }

                }
                return fileLoaded;
            }
            catch (Exception Ex)
            {
                tries--;
                if (tries > 0)
                {
                    System.Threading.Thread.Sleep(1 * 60 * 1000);//Wait for 1 minute before trying again
                    return ConnectAndQuery_Dataset(dataSource, query, timeout, tries);
                }
                else
                {
                    throw Ex;
                }
            }
        }

        /// <summary>
        /// Connects to the given database and returns a dataset object with the results
        /// </summary>
        /// <param name="dataSource">Data source to connect and query</param>
        /// <param name="query">Query to hit against database</param>
        /// <param name="timeout">Time-out for entire command to complete, default is 0 (infinite)</param>
        /// <returns>Loaded data set object</returns>
        public static DataSet ConnectAndQuery_Dataset(Logger logger, Data.AppNames dataSource, string query, int timeout = 0, int tries = 2)
        {
            logger.WriteToLog($"Here is the query that was passed in: {query}");

            return ConnectAndQuery_Dataset(dataSource, query, timeout, tries);
        }

        /// <summary>
        /// Connects to the given database and returns a DataSet object with the resulting DataTables. This method allows you to run multiple queries in a list against the 
        /// database under the same connection, resulting in less overhead. For whatever reason, passing a raw string with multiple queries to Oracle DBs results in errors,
        /// so this method solves that issue.
        /// </summary>
        /// <param name="dataSource">Data source to connect to and query against</param>
        /// <param name="queryList">A List of independent queries (strings) to run against the database</param>
        /// <param name="timeout">Time-out for entire command to complete, default is 0 (infinite)</param>
        /// <returns>Loaded DataSet onject</returns>
        [Obsolete("Use overloaded version that requires Logger to be passed in for query logging")]
        public static DataSet ConnectAndQuery_OracleDataset(Data.AppNames dataSource, List<String> queryList, int timeout = 0, int tries = 2)
        {
            try
            {
                DataSet resultSets = new DataSet();
                Data sourceCreds = new Data(dataSource);

                if (queryList.Count > 0) // Query list is not empty.
                {
                    int i = 1;

                    using (OdbcConnection connection = sourceCreds.GetOdbcConnection()) // Connect to specified db.
                    {
                        foreach(String query in queryList) // Execute each query under the connection.
                        {
                            OdbcCommand command = new OdbcCommand(query, connection);
                            command.CommandTimeout = timeout;

                            using(OdbcDataAdapter adapter = new OdbcDataAdapter(command))
                            {
                                adapter.Fill(resultSets, i.ToString()); // Adds each DataTable to the DataSet. The second String param guarantees each query results in a new DataTable.
                            }

                            if (resultSets.Tables.Count < i)
                            {
                                resultSets.Tables.Add(new DataTable(i.ToString()));
                            }

                            i++;
                        }
                    }
                }

                return resultSets;
            }
            catch (Exception Ex)
            {
                tries--;
                if (tries > 0)
                {
                    System.Threading.Thread.Sleep(1 * 60 * 1000);//Wait for 1 minute before trying again
                    return ConnectAndQuery_OracleDataset(dataSource, queryList, timeout, tries);
                }
                else
                {
                    throw Ex;
                }
            }
        }

        /// <summary>
        /// Connects to the given database and returns a DataSet object with the resulting DataTables. This method allows you to run multiple queries in a list against the 
        /// database under the same connection, resulting in less overhead. For whatever reason, passing a raw string with multiple queries to Oracle DBs results in errors,
        /// so this method solves that issue.
        /// </summary>
        /// <param name="dataSource">Data source to connect to and query against</param>
        /// <param name="queryList">A List of independent queries (strings) to run against the database</param>
        /// <param name="timeout">Time-out for entire command to complete, default is 0 (infinite)</param>
        /// <returns>Loaded DataSet onject</returns>
        public static DataSet ConnectAndQuery_OracleDataset(Logger logger, Data.AppNames dataSource, List<string> queryList, int timeout = 0, int tries = 2)
        {
            foreach (string query in queryList)
            {
                logger.WriteToLog($"Here is the query that was passed in: {query}");
            }

            return ConnectAndQuery_OracleDataset(dataSource, queryList, timeout, tries);
        }

        /// <summary>
        /// This is a helper method to ConnectAndQuery_OracleDataset(Data.AppNames, List<String>), accepting a String parameter instead of a List of strings. The string
        /// should contain multiple queries, separated by a semi-colon (;).
        /// Connects to the given database and returns a DataSet object with the resulting DataTables. This method allows you to run multiple queries in a list against the 
        /// database under the same connection, resulting in less overhead. For whatever reason, passing a raw string with multiple queries to Oracle DBs results in errors,
        /// so this method solves that issue.
        /// </summary>
        /// <param name="dataSource">Data source to connect to and query against</param>
        /// <param name="queries">A string of independent queries to run against the database. Queries must be separated by a semi-colon (;)</param>
        /// <param name="timeout">Time-out for entire command to complete, default is 0 (infinite)</param>
        /// <returns>Loaded DataSet onject</returns>
        public static DataSet ConnectAndQuery_OracleDataset(Logger logger, Data.AppNames datasource, String queryList, int timeout = 0)
        {
            List<String> newList = queryList.Split(';').ToList();

            // If the string ends with a semi-colon(;), the last element produced in the split may be an empty string.
            if (newList.Last().Trim().Equals(""))
            {
                newList.Remove(newList.Last()); // Remove the last string in the list.
            }

            return ConnectAndQuery_OracleDataset(logger, datasource, newList, timeout);
        }

        /// <summary>
        /// Connects to the given database and returns a datatable object with the results using SQL commands & parms parameters to avoid SQL injection (use this for our web applications). I'm pretty 
        /// sure we have to use the ? notation instead of {} in the raw SQL.
        /// </summary>
        /// <param name="dataSource">Data source to connect and query</param>
        /// <param name="query">Query to hit against database</param>
        /// <param name="queryParms">Query parameters to pass into the database</param>
        /// <param name="timeout">Time-out for entire command to complete, default is 0 (infinite)</param>
        /// <returns>Loaded data table object</returns>
        [Obsolete("Use overloaded version that requires Logger to be passed in for query logging")]
        public static DataTable ConnectAndQuery(Data.AppNames dataSource, String query, List<SqlParameter> queryParms, Boolean IsStoredProcedure = false, int timeout = 0)
        {
            DataTable fileLoaded = new DataTable();
            Data sourceCreds = new Data(dataSource);

            SqlConnection SqlConn = sourceCreds.GetSqlConnection(sourceCreds.Authentication);
            SqlCommand sqlCmd = new SqlCommand(query, SqlConn);

            if (IsStoredProcedure)
            {
                sqlCmd.CommandType = CommandType.StoredProcedure;
            }
            
            foreach (SqlParameter param in queryParms)
            {
                sqlCmd.Parameters.Add(param);
            }

            sqlCmd.CommandTimeout = timeout;
            using (SqlDataAdapter sqlAdapter = new SqlDataAdapter(sqlCmd))
            {
                sqlAdapter.Fill(fileLoaded);
            }

            return fileLoaded;
        }

        /// <summary>
        /// Connects to the given database and returns a datatable object with the results using SQL commands & parms parameters to avoid SQL injection (use this for our web applications). I'm pretty 
        /// sure we have to use the ? notation instead of {} in the raw SQL.
        /// </summary>
        /// <param name="dataSource">Data source to connect and query</param>
        /// <param name="query">Query to hit against database</param>
        /// <param name="queryParms">Query parameters to pass into the database</param>
        /// <param name="timeout">Time-out for entire command to complete, default is 0 (infinite)</param>
        /// <returns>Loaded data table object</returns>
        public static DataTable ConnectAndQuery(Logger logger, Data.AppNames dataSource, string query, List<SqlParameter> queryParms, bool isStoredProcedure = false, int timeout = 0)
        {
            string queryForLogging = query;

            foreach (SqlParameter param in queryParms)
            {
                queryForLogging = queryForLogging.Replace($"@{param.ParameterName}", param.Value.ToString());
            }

            logger.WriteToLog($"Here is the query that was passed in: {queryForLogging}");

            return ConnectAndQuery(dataSource, query, queryParms, isStoredProcedure, timeout);
        }

        /// <summary>
        /// Connects to the given database and returns a datatable object with the results using ODBC SQL commands & parms parameters to avoid SQL injection (use this for our web applications). I'm pretty 
        /// sure we have to use the ? notation instead of {} in the raw SQL.
        /// </summary>
        /// <param name="dataSource">Data source to connect and query</param>
        /// <param name="query">Query to hit against database</param>
        /// <param name="queryParms">Query parameters to pass into the database</param>
        /// <param name="timeout">Time-out for entire command to complete, default is 0 (infinite)</param>
        /// <returns>Loaded data table object</returns>
        [Obsolete("Use overloaded version that requires Logger to be passed in for query logging")]
        public static DataTable ConnectAndQuery_Odbc(Data.AppNames dataSource, String query, List<OdbcParameter> queryParms, int timeout = 0)
        {
            DataTable fileLoaded = new DataTable();
            Data sourceCreds = new Data(dataSource);

            OdbcConnection odbcConn = sourceCreds.GetOdbcConnection();
            OdbcCommand odbccomm = new OdbcCommand(query, odbcConn);

            foreach (OdbcParameter param in queryParms)
            {
                odbccomm.Parameters.Add(param);
            }

            odbccomm.CommandTimeout = timeout;
            using (OdbcDataAdapter sqlAdapter = new OdbcDataAdapter(odbccomm))
            {
                sqlAdapter.Fill(fileLoaded);
            }

            return fileLoaded;
        }

        /// <summary>
        /// Connects to the given database and returns a datatable object with the results using ODBC SQL commands & parms parameters to avoid SQL injection (use this for our web applications). I'm pretty 
        /// sure we have to use the ? notation instead of {} in the raw SQL.
        /// </summary>
        /// <param name="dataSource">Data source to connect and query</param>
        /// <param name="query">Query to hit against database</param>
        /// <param name="queryParms">Query parameters to pass into the database</param>
        /// <param name="timeout">Time-out for entire command to complete, default is 0 (infinite)</param>
        /// <returns>Loaded data table object</returns>
        public static DataTable ConnectAndQuery_Odbc(Logger logger, Data.AppNames dataSource, string query, List<OdbcParameter> queryParms, int timeout = 0)
        {
            string queryForLogging = query;
            Regex regex = new Regex(Regex.Escape("?"));

            foreach (OdbcParameter param in queryParms)
            {
                queryForLogging = regex.Replace(queryForLogging, param.Value.ToString(), 1);
            }

            logger.WriteToLog($"Here is the query that was passed in: {queryForLogging}");

            return ConnectAndQuery_Odbc(dataSource, query, queryParms, timeout);
        }

        /// <summary>
        /// Connects and queries a given database with a given query, returning the typed result set
        /// </summary>
        /// <typeparam name="T">The type to load the records returned from the database into</typeparam>
        /// <param name="source">Database to hit the query with - this should be configured in DataConfiguration.xml and as an enumeration option</param>
        /// <param name="query">SQL query to hit the database with</param>
        /// <param name="Timer">Time-out counter for the query, default is 240 seconds (4 minutes)</param>
        /// <param name="timeout">Time-out for entire command to complete, default is 0 (infinite)</param>
        /// <returns>Typed data set containing the results of the query</returns>
        [Obsolete("Use overloaded version that requires Logger to be passed in for query logging")]
        public static IEnumerable<T> ConnectAndQuery<T>(Data.AppNames source, String query, int Timer = 240, int timeout = 0, int tries = 2)
        {
            Data TargetConn = new Data(source);

            return ConnectAndQuery<T>(TargetConn, query, Timer, timeout, tries);
        }

        /// <summary>
        /// Connects and queries a given database with a given query, returning the typed result set
        /// </summary>
        /// <typeparam name="T">The type to load the records returned from the database into</typeparam>
        /// <param name="source">Database to hit the query with - this should be configured in DataConfiguration.xml and as an enumeration option</param>
        /// <param name="query">SQL query to hit the database with</param>
        /// <param name="Timer">Time-out counter for the query, default is 240 seconds (4 minutes)</param>
        /// <param name="timeout">Time-out for entire command to complete, default is 0 (infinite)</param>
        /// <returns>Typed data set containing the results of the query</returns>
        public static IEnumerable<T> ConnectAndQuery<T>(Logger logger, Data.AppNames source, string query, int Timer = 240, int timeout = 0, int tries = 2)
        { 
            //Logging can be removed once the new ConnectAndQuery method is being used here
            logger.WriteToLog($"Here is the query that was passed in: {query}");

            return ConnectAndQuery<T>(source, query, Timer, timeout, tries);
        }

        /// <summary>
        /// Faster connects and query that leverages your preconfigured (and thus pre-queried data configuration details)
        /// </summary>
        /// <typeparam name="T">The type to load the records returned from the database into</typeparam>
        /// <param name="source">Database to hit the query with - this should be configured in DataConfiguration.xml and as an enumeration option</param>
        /// <param name="query">SQL query to hit the database with</param>
        /// <param name="Timer">Time-out counter for the query, default is 240 seconds (4 minutes)</param>
        /// <param name="timeout">Time-out for entire command to complete, default is 0 (infinite)</param>
        /// <returns>Typed data set containing the results of the query</returns>
        [Obsolete("Use overloaded version that requires Logger to be passed in for query logging")]
        public static IEnumerable<T> ConnectAndQuery<T>(Data TargetConn, String query, int Timer = 240, int timeout = 0, int tries = 2)
        {
            try
            { 
                DataContext TargetDatabase;
                try
                {
                    TargetDatabase = TargetConn.OpenConnectionAndGetDatabase(Timer);
                }
                catch (Exception exc)
                {
                    throw new Exception("Connection string failed to connect: " + TargetConn.connectionString + Environment.NewLine + exc.ToString());
                }
                TargetDatabase.CommandTimeout = timeout;
                IEnumerable<T> ResultSet = TargetDatabase.ExecuteQuery<T>(query).ToArray();

                TargetConn.CloseConnection();

                return ResultSet;
            }
            catch (Exception Ex)
            {
                tries--;
                TargetConn.CloseConnection();
                if (tries > 0)
                {
                    System.Threading.Thread.Sleep(1 * 60 * 1000);//Wait for 1 minute before trying again
                    return ConnectAndQuery<T>(TargetConn, query, Timer, timeout, tries);
                }
                else
                {
                    throw Ex;
                }
            }
        }

        /// <summary>
        /// Faster connects and query that leverages your preconfigured (and thus pre-queried data configuration details)
        /// </summary>
        /// <typeparam name="T">The type to load the records returned from the database into</typeparam>
        /// <param name="source">Database to hit the query with - this should be configured in DataConfiguration.xml and as an enumeration option</param>
        /// <param name="query">SQL query to hit the database with</param>
        /// <param name="Timer">Time-out counter for the query, default is 240 seconds (4 minutes)</param>
        /// <param name="timeout">Time-out for entire command to complete, default is 0 (infinite)</param>
        /// <returns>Typed data set containing the results of the query</returns>
        public static IEnumerable<T> ConnectAndQuery<T>(Logger logger, Data targetConn, string query, int timer = 240, int timeout = 0, int tries = 2)
        {
            logger.WriteToLog($"Here is the query that was passed in: {query}");

            return ConnectAndQuery<T>(targetConn, query, timer, timeout, tries);
        }

        private static String AddStatistics(String unformatedBody, String link, int count, bool attached = false)
        {
            if (attached)
            {
                return unformatedBody + Environment.NewLine + Environment.NewLine + String.Format("There are {0} records in the report.", count.ToString());
            }
            else
            {
                return unformatedBody + Environment.NewLine + Environment.NewLine + "Please find the report here: " + Environment.NewLine + @"""" + link + @"""" + Environment.NewLine + Environment.NewLine +
                    String.Format("There are {0} records in the report.", count.ToString());
            }
        }

        /// <summary>
        /// Handles querying and output to an excel table, does not generate emails
        /// </summary>
        /// <typeparam name="T">Type of the results of the query</typeparam>
        /// <param name="source"></param>
        /// <param name="query"></param>
        /// <param name="sheetName"></param>
        /// <param name="outputLoc"></param>
        /// <returns></returns>
        public static IEnumerable<T> RunTableOutput<T>(Data.AppNames source, String query, String sheetName, String outputLoc, Logger processLog)
        {
            processLog.WriteToLog("RunTableOutput utility called");
            processLog.WriteToLog("Provided query :" + query);
            IEnumerable<T> resultSet = ConnectAndQuery<T>(source, query);

            int numberOfRecords = resultSet.Count();
            if (numberOfRecords == 0)
            {
                processLog.WriteToLog("The provided query didn't return any results");
            }
            else
            {
                processLog.WriteToLog("Number of records found: " + numberOfRecords);
                //ExcelWork.OutputTableToExcel<T>(resultSet, sheetName, outputLoc);
                processLog.WriteToLog("Output the excel table here: " + outputLoc);
            }

            return resultSet;
        }

        /// <summary>
        /// Writes an object to a string using reflection
        /// </summary>
        /// <param name="obj">Object to write</param>
        /// <returns>All values in the object to the string</returns>
        public static String ObjectToText(object obj, string delimiter = "", string valueBracket = "", bool endWithDelimiter = true, bool includePropertyName = false)
        {
            String combinedString = "";
            Type objType = obj.GetType();

            foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(obj))
            {
                System.Reflection.PropertyInfo pi = objType.GetProperty(descriptor.Name);
                bool hasIt = Attribute.IsDefined(pi, typeof(DoNotWrite));

                if (includePropertyName && !Attribute.IsDefined(pi, typeof(DoNotWrite)))
                {
                    combinedString += descriptor.DisplayName + ": ";
                }

                if (!Attribute.IsDefined(pi, typeof(DoNotWrite)))
                {
                    object value = descriptor.GetValue(obj);
                    if (Attribute.IsDefined(pi, typeof(DisplayFormatAttribute)))
                    {
                        DisplayFormatAttribute attr = (DisplayFormatAttribute)pi.GetCustomAttributes(typeof(DisplayFormatAttribute), true).FirstOrDefault();
                        value = string.Format(attr.DataFormatString, value);
                    }
                    combinedString += valueBracket + value + valueBracket + delimiter;
                }

            }
            if(endWithDelimiter)
            {
                return combinedString;
            }
            else
            {
                return combinedString.Remove(combinedString.Length - 1);
            }
        }

        public static DataTable TextToTable(string inputString, FileFormats fileFormat, dynamic delimiter, bool useFirstLineAsHeader = false, int linesToSkip = 0, bool quoteQualified = false)
        {
            DataTable returnTable = new DataTable();

            if (fileFormat == FileFormats.delimited)
            {
                string[] lines = System.IO.File.ReadAllLines(inputString);
                char[] delim = delimiter.ToCharArray();
                string[] strDelim = new string[] { "\"" + delimiter.ToString() + "\"" };

                int colCount;
                if (quoteQualified)
                {
                    colCount = lines[linesToSkip].Split(strDelim, StringSplitOptions.None).Length;
                }
                else
                {
                    colCount = lines[linesToSkip].Split(delim).Length;
                }                

                if(!useFirstLineAsHeader)
                {
                    for (int col = 0; col < colCount; col++)
                    {
                        returnTable.Columns.Add(new DataColumn("column" + (col + 1).ToString()));
                    }
                }

                int lineCounter = 1;

                foreach (string line in lines.Skip(linesToSkip))
                {

                    var cols = quoteQualified ? line.Split(strDelim, StringSplitOptions.None) : line.Split(delim[0]);

                    //If it's the first line and we're using it as a header, create columns using those values
                    if (useFirstLineAsHeader && lineCounter == 1)
                    {
                        for (int col = 0; col < colCount; col++)
                        {
                            if(col == 0 && quoteQualified)
                            {
                                cols[col] = cols[col].Substring(1);
                            }
                            else if(col == colCount-1 && quoteQualified)
                            {
                                cols[col] = cols[col].Substring(0,cols[col].Length-1);
                            }
                            //string stuff = cols[col];
                                returnTable.Columns.Add(new DataColumn(cols[col]));
                            }
                        //}
                        lineCounter++;
                    }
                    else
                    {
                        //var cols = line.Split(delim[0]);
                        DataRow dr = returnTable.NewRow();
                        for (int cIndex = 0; cIndex < colCount; cIndex++)
                        {
                            try
                            {
                                if (cIndex == 0 && quoteQualified)
                                {
                                    cols[cIndex] = cols[cIndex].Substring(1);
                                }
                                else if (cIndex == colCount - 1 && quoteQualified)
                                {
                                    cols[cIndex] = cols[cIndex].Substring(0, cols[cIndex].Length - 1);
                                }
                                dr[cIndex] = cols[cIndex];
                            }
                            catch(IndexOutOfRangeException ex)
                            {

                            }
                        }
                        returnTable.Rows.Add(dr);
                        lineCounter++;
                    }

                }

            }
            else if (fileFormat == FileFormats.fixedWidth)
            {
                for (int col = 0; col < delimiter.Count; col++)
                {
                    returnTable.Columns.Add(new DataColumn("column" + (col + 1).ToString()));
                }

                var parts = new string[delimiter.Count];

                foreach (string line in System.IO.File.ReadLines(inputString))
                {
                    int start = 0;
                    DataRow dr = returnTable.NewRow();

                    for (int i = 0; i < delimiter.Count; i++)
                    {
                        if (i == delimiter.Count - 1)
                        {
                            dr[i] = line.Substring(delimiter[i], line.Length - delimiter[i]);
                        }
                        else
                        {
                            dr[i] = line.Substring(delimiter[i], (delimiter[i + 1] - delimiter[i]));
                        }
                    }

                    returnTable.Rows.Add(dr);


                }
            }

            return returnTable;
        }

        public enum FileFormats
        {
            delimited,
            fixedWidth
        }

        /// <summary>
        /// Given a large list of data, and a specified number of batches, this will break the data set out for multithreading
        /// </summary>
        /// <typeparam name="T">The record type to be processed</typeparam>
        /// <param name="extractedData">The list of data to be split into batches</param>
        /// <param name="NumberOfBatches">How many batches to split the data into</param>
        /// <param name="ProcLog">Calling process</param>
        /// <param name="loadBatches">Whether we should actually load the batches with the subset of data, or just identify the record start/ends for each batch without actually manipulating the main data set list</param>
        /// <returns></returns>
        public static List<MultiBatch<T>> GenerateBatches<T>(List<T> extractedData, int NumberOfBatches, Logger ProcLog, Boolean loadBatches = true)
        {
            int fullRowCount = extractedData.Count;
            int batchRemainder = fullRowCount % NumberOfBatches;

            int divisibleRowCount = fullRowCount - batchRemainder;

            int normalBatchSize = divisibleRowCount / NumberOfBatches;

            List<Task> threadsToManage = new List<Task>();
            List<MultiBatch<T>> batchesToManage = new List<MultiBatch<T>>();

            int lastBatchEnd = 0;
            for (int i = 0; i < NumberOfBatches; i++)
            {
                MultiBatch<T> batch;
                
                if (i == 0)
                {
                    batch = new MultiBatch<T>() { StartRecord = lastBatchEnd, EndRecord = normalBatchSize + batchRemainder };

                    if (loadBatches)
                    {
                        batch.DataToProcess = new Stack<T>(extractedData.Take(batch.EndRecord));
                    }

                }
                else
                {
                    int batchStart = lastBatchEnd;
                    batch = new MultiBatch<T>() { StartRecord = batchStart, EndRecord = batchStart + normalBatchSize };

                    if (loadBatches)
                    {
                        batch.DataToProcess = new Stack<T>(extractedData.Take(normalBatchSize));
                    }
                    
                }

                if (loadBatches)
                {
                    extractedData.RemoveRange(0, batch.DataToProcess.Count);
                }
                

                lastBatchEnd = batch.EndRecord;

                //ExcelWork.OutputTableToExcel<T>(batch.DataToProcess, i.ToString(), ProcLog.LoggerOutputYearDir + "batchOutput" + i + ".xlsx");
                batchesToManage.Add(batch);
            }

            return batchesToManage;
        }

        public class MultiBatch<T>
        {
            public int StartRecord { get; set; }
            public int EndRecord { get; set; }
            public Stack<T> DataToProcess { get; set; }

        }

        private static void EscapeXMLCharactersInDataTable(DataTable dataSet)
        {
            foreach (DataRow row in dataSet.Rows)
            {
                foreach (DataColumn col in dataSet.Columns)
                {
                    if (col.DataType == typeof(string))
                    {
                        row[col.ColumnName] = RemoveTroublesomeCharacters(row[col.ColumnName].ToString());
                    }
                }
            }
        }

        private static string RemoveTroublesomeCharacters(string inString)
        {
            if (inString == null) return null;

            StringBuilder newString = new StringBuilder();
            char ch;

            for (int i = 0; i < inString.Length; i++)
            {
                ch = inString[i];
                if (XmlConvert.IsXmlChar(ch))
                {
                    newString.Append(ch);
                }
            }
            return newString.ToString();

        }

    }
}
