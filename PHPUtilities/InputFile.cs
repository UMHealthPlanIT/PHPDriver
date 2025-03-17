using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Utilities
{
    public class InputFile
    {
        /// <summary>
        /// Fast import method to load a separated file into a table inside the specified database. Assumes the ordinal positions of the columns match that of the input file
        /// </summary>
        /// <param name="url">Location of the file to import</param>
        /// <param name="delimiter">Delimiter of the file</param>
        /// <param name="hasHeaders">If the first row should be skipped because there are embedded headers</param>
        /// <param name="targetTable">Table to insert the rows into</param>
        /// <param name="proclog">Calling process (this)</param>
        /// <param name="workDatabase">Database to load the table into</param>
        /// <returns></returns>
        public static DataTable ReadInTextFile(string url, string delimiter, bool hasHeaders, string targetTable, Logger proclog, Data.AppNames workDatabase, bool AddRowCount = false, char endOfLine = ' ', bool hasTrailerRecord = false)
        {
            proclog.WriteToLog("Requested to Load File " + url + " to database " + workDatabase.ToString() + " and table " + targetTable);
            DataTable fileLoaded = new DataTable();
            Data jobManConf = new Data(workDatabase);
            SqlConnection JobManSqlConn = jobManConf.GetSqlConnection(jobManConf.Authentication);

            using (SqlDataAdapter dAdapter = new SqlDataAdapter("select top 0 * from " + targetTable, JobManSqlConn))
            {
                dAdapter.FillSchema(fileLoaded, SchemaType.Mapped);
            }

            fileLoaded = ReadInTextFile(url, delimiter, hasHeaders, proclog, AddRowCount: AddRowCount, endOfLine: endOfLine, mappedTable: fileLoaded, hasTrailerRecord: hasTrailerRecord);

            DataWork.LoadTable(workDatabase, targetTable, fileLoaded, proclog);

            return fileLoaded;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="url">Location of the file to import</param>
        /// <param name="delimiter">Delimiter of the file</param>
        /// <param name="hasHeaders">If the first row should be skipped because there are embedded headers</param>
        /// <param name="proclog">Calling process (this)</param>
        /// <param name="AddRowCount">Add the row count to each row</param>
        /// <param name="endOfLine">End of line character override</param>
        /// <param name="mappedTable">An empty table with the columns mapped to the file to be read in</param>
        /// <param name="hasTrailerRecord">Whether the text file has a trailer record that needs to be removed</param>
        /// <returns></returns>
        public static DataTable ReadInTextFile(string url, string delimiter, bool hasHeaders, Logger proclog, bool AddRowCount = false, char endOfLine = ' ', DataTable mappedTable = null, bool hasTrailerRecord = false)
        {
            DataTable fileLoaded;
            if (mappedTable != null)
            {
                fileLoaded = mappedTable.Clone();
            }
            else
            {
                fileLoaded = new DataTable();
            }

            string inputLine;
            int recordCount = 0;

            try
            {
                using (StreamReader sr = new StreamReader(url))
                {
                    while (!sr.EndOfStream)
                    {
                        if (endOfLine == ' ')
                        {
                            inputLine = sr.ReadLine();
                        }
                        else
                        {
                            inputLine = "";
                            while ((char)sr.Peek() != endOfLine && !sr.EndOfStream)
                            {
                                inputLine += (char)sr.Read();
                            }

                            if ((char)sr.Peek() == endOfLine)
                            {
                                sr.Read(); //advance one char farther to skip the end of line
                            }
                        }

                        recordCount++;

                        fileLoaded.BeginLoadData();
                        if (recordCount == 1 && hasHeaders)
                        {
                            if (mappedTable == null)//if mapping isn't provided, we need to build out the columns
                            {
                                foreach (string col in inputLine.Split(new[] { delimiter }, StringSplitOptions.None))
                                {
                                    fileLoaded.Columns.Add(col);
                                }
                            }
                            //skip this row;
                        }
                        else
                        {
                            if (recordCount == 1 && mappedTable == null)//if mapping isn't provided, we need to build out the columns
                            {
                                int colCount = 0;
                                foreach (string col in inputLine.Split(new[] { delimiter }, StringSplitOptions.None))
                                {
                                    fileLoaded.Columns.Add("Column" + colCount.ToString());
                                    colCount++;
                                }
                            }
                            if (AddRowCount)
                            {
                                inputLine = recordCount + delimiter + inputLine.Trim();
                            }
                            InsertNewRow(fileLoaded, inputLine, delimiter, proclog);
                        }
                    }
                    sr.Close();
                }

            }
            catch (Exception E)
            {
                proclog.WriteToLog(E.ToString(), UniversalLogger.LogCategory.ERROR);
            }

            if (hasTrailerRecord)
            {
                fileLoaded.Rows.Remove(fileLoaded.Rows[fileLoaded.Rows.Count - 1]);
            }

            return fileLoaded;
        }

        public static void InsertNewRow(DataTable table, string readIn, string delimiter, Logger proclog)
        {
            int marker = 0;
            DataRow rowList = table.NewRow();
            int rowColumnCounter = 0;
            while (readIn.IndexOf(delimiter, marker) != -1)
            {
                string fieldvalue;

                if (readIn.Substring(marker, 1) == @"""")
                {
                    fieldvalue = readIn.Substring(marker + 1, readIn.IndexOf("\"" + delimiter, marker + 1) - marker - 1);
                    marker += 2; //to offset for the open and closed double quotes
                }
                else
                {
                    fieldvalue = readIn.Substring(marker, readIn.IndexOf(delimiter, marker) - marker);
                }

                int doubleQuoteSkipper = 0; //we'll use this to skip the position for the closing double quotes

                if (fieldvalue.Contains(@"""" + delimiter))
                {
                    int endOfDoubleQuote = readIn.IndexOf(@""" + delimiter", marker + 1);
                    fieldvalue = readIn.Substring(marker, endOfDoubleQuote - marker);
                    doubleQuoteSkipper = 1;
                }

                if (fieldvalue == "")
                {
                    rowList[rowColumnCounter] = DBNull.Value;
                }
                else
                {
                    if (rowList.Table.Columns[rowColumnCounter].DataType == typeof(bool))
                    {
                        if (fieldvalue == "1" || fieldvalue == "0")
                        {
                            rowList[rowColumnCounter] = fieldvalue == "1" ? 1 : 0;
                        }

                        if (fieldvalue.ToUpper() == "TRUE" || fieldvalue.ToUpper() == "FALSE")
                        {
                            rowList[rowColumnCounter] = fieldvalue.ToUpper() == "TRUE" ? 1 : 0;
                        }
                    }
                    else
                    {
                        rowList[rowColumnCounter] = fieldvalue.Replace(@"""", "");
                    }
                }

                marker = marker + fieldvalue.Length + 1 + doubleQuoteSkipper;
                rowColumnCounter++;
            }

            if (marker < readIn.Length)
            {
                string lastFieldValue = readIn.Substring(marker, readIn.Length - marker);
                //Added this very specific second condition because my last field was potentially "     " in the file, but exists as an int in the db, so "     " could not be added.
                if (lastFieldValue == "" || (string.IsNullOrWhiteSpace(lastFieldValue) && rowList.Table.Columns[rowColumnCounter].DataType != typeof(string) && rowList.Table.Columns[rowColumnCounter].AllowDBNull))
                {

                    rowList[rowColumnCounter] = DBNull.Value;

                }
                else if (lastFieldValue.Contains(@"""")) //there are double quotes around this field
                {
                    rowList[rowColumnCounter] = lastFieldValue.Substring(1, lastFieldValue.Length - 2);
                }
                else
                {
                    rowList[rowColumnCounter] = lastFieldValue;
                }
            }
            try
            {
                table.Rows.Add(rowList);
            }
            catch (Exception exc)
            {
                proclog.WriteToLog("This row could not be imported: " + Environment.NewLine + readIn + Environment.NewLine + exc.ToString()); //will only work if the DataTable's constraints are on
                throw exc;
            }
        }

        /// <summary>
        /// Loads an excel file into the given table
        /// </summary>
        /// <param name="url">Path to the input file</param>
        /// <param name="HasHeaders">Whether the first row should be skipped because it contains headers</param>
        /// <param name="targetTable">Table to load the excel file into</param>
        /// <param name="proclog">Calling program (this)</param>
        /// <param name="workDatabase">Database containing the table to load</param>
        public static void ReadInExcelFile(string url, bool HasHeaders, string targetTable, Logger proclog, Data.AppNames workDatabase)
        {
            DataTable fileLoaded = new DataTable();
            Data jobManConf = new Data(workDatabase);
            SqlConnection JobManSqlConn = jobManConf.GetSqlConnection(jobManConf.Authentication);

            using (SqlDataAdapter dAdapter = new SqlDataAdapter("select top 0 * from " + targetTable, JobManSqlConn))
            {
                dAdapter.FillSchema(fileLoaded, SchemaType.Mapped);
            }

            DataWork.SaveDataTableToDb(targetTable, fileLoaded, workDatabase);
        }

        /// <summary>
        /// This function allows us to import a fixed width file into a table in a database. Given the target database/table, the file path and an array of integers
        /// we can import the file to a DataTable and utilize DataWork to push the DataTable to the databse. The array of integers should contain an entry for each 
        /// column that exists in the file, each individual element representing the size (width) of that column.
        /// </summary>
        /// <param name="filePath">Full path where the file is located.</param>
        /// <param name="rowWidths">A single integer per column, representing the column width. (Read from left to right)</param>
        /// <param name="hasHeader">Whether the file has a header row. If true, the first line will be removed.</param>
        /// <param name="hasFooter">Whether the file has a footer/trailer row. If true, the last line will be removed.</param>
        /// <param name="targetTable">The target table in which the data will be loaded into.</param>
        /// <param name="targetDb">The database where the target table is located.</param>
        /// <param name="logger">The logger, likely your job class.</param>
        public static void ImportFixedWidthFile(string filePath, int[] rowWidths, bool hasHeader, bool hasFooter, string targetTable, Data.AppNames targetDb, Logger logger)
        {
            // Convert the fixed width file to a DataTable.
            DataTable fwFile = ImportFixedWidthFile(filePath, rowWidths, hasHeader, hasFooter, logger);

            // Store the DataTable to the target table & database.
            try
            {
                UniversalLogger.WriteToLog(logger, string.Format("Writing DataTable to target: {0}.{1}", targetDb, targetTable));
                DataWork.SaveDataTableToDb(targetTable, fwFile, targetDb);
            }
            catch (Exception e)
            {
                UniversalLogger.WriteToLog(logger, String.Format("An error occurred trying to import file into the database. \n{0} \n{1}", e.Message, e.StackTrace));
                throw e;
            }
        }

        /// <summary>
        /// This function allows us to import a fixed width file into a C# DataTable. The array of integers should contain an entry for each 
        /// column that exists in the file, each individual element representing the size (width) of that column.
        /// </summary>
        /// <param name="filePath">Full path where the file is located.</param>
        /// <param name="rowWidths">A single integer per column, representing the column width. (Read from left to right)</param>
        /// <param name="hasHeader">Whether the file has a header row. If true, the first line will be removed.</param>
        /// <param name="hasFooter">Whether the file has a footer/trailer row. If true, the last line will be removed.</param>
        /// <param name="logger">The logger, likely your job class.</param>
        /// <returns>A DataTable object containing the data from the provided fixed-width file.</returns>
        public static DataTable ImportFixedWidthFile(string filePath, int[] rowWidths, bool hasHeader, bool hasFooter, Logger logger)
        {
            // Create a new DataTable to hold return data.
            DataTable dt = new DataTable();

            // Create a column for each rowWidth.
            for (int i = 0; i < rowWidths.Length; i++)
            {
                dt.Columns.Add();
            }

            // Read in each line of the file and add it to the datatable.
            using (StreamReader stream = new StreamReader(filePath))
            {
                if (hasHeader)
                {
                    stream.ReadLine();
                }

                string currentLine; // Content of the current line being read.
                DataRow currentRow; // Current row being built from the string.
                int currentLinePosition = 0; // Keep track of the index of the current line.

                while (!stream.EndOfStream)
                {
                    currentLine = stream.ReadLine();

                    if (!String.IsNullOrWhiteSpace(currentLine))
                    {
                        currentRow = dt.NewRow();

                        for (int j = 0; j < rowWidths.Length; j++)
                        {
                            currentRow[j] = currentLine.Substring(currentLinePosition, rowWidths[j]);
                            currentLinePosition += rowWidths[j];
                        }

                        dt.Rows.Add(currentRow);
                        currentLinePosition = 0;
                    }
                }
            }

            if (hasFooter) // Remove the last of the DataTable, because it's just a silly footer :/
            {
                dt.Rows.Remove(dt.Rows[dt.Rows.Count - 1]);
            }

            return dt;
        }

        /// <summary>
        /// This function allows us to import a fixed width file into a C# DataTable with named columns. The Dictionary should contain a KeyValuePair 
        /// object for each column contained in the file; the String being the name of the column and the integer being the size (width) of the column.
        /// </summary>
        /// <param name="filePath">Full path where the file is located.</param>
        /// <param name="rowDefs">KeyValuePair sets representing the name and size of a column.</param>
        /// <param name="hasHeader">Whether the file has a header row. If true, the first line will be removed.</param>
        /// <param name="hasFooter">Whether the file has a footer/trailer row. If true, the last line will be removed.</param>
        /// <param name="logger">The logger, likely your job class.</param>
        /// <returns>A DataTable object containing the data from the provided fixed-width file.</returns>
        public static DataTable ImportFixedWidthFile(string filePath, Dictionary<string, int> rowDefs, bool hasHeader, bool hasFooter, Logger logger)
        {
            // Get a list of the row widths from the Dictionary
            int[] rowWidths = rowDefs.Values.ToArray();

            // Build the DataTable using the overloaded import function
            DataTable dt = ImportFixedWidthFile(filePath, rowWidths, hasHeader, hasFooter, logger);

            // Get a list of the keys and associate them to the DataTable columns.
            string[] keys = rowDefs.Keys.ToArray();
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                dt.Columns[i].ColumnName = keys[i];
            }

            return dt;
        }

        /// <summary>
        /// This function uses C# Generics to allow us to import a fixed width file into a custom C# object with named properties. The Dictionary should contain a KeyValuePair 
        /// object for each column contained in the file; the String being the name of the column (and thus the name of the property in the custom object) and the integer being 
        /// the size (width) of the column in the file.
        /// </summary>
        /// <typeparam name="T">The desired object type to map the file to.</typeparam>
        /// <param name="filePath">Full path where the file is located.</param>
        /// <param name="rowDefs">KeyValuePair sets representing the name and size of a column.</param>
        /// <param name="hasHeader">Whether the file has a header row. If true, the first line will be removed.</param>
        /// <param name="hasFooter">Whether the file has a footer/trailer row. If true, the last line will be removed.</param>
        /// <param name="logger">The logger, likely your job class.</param>
        /// <returns>A List<> object containing the data from the provided fixed-width file mapped into a custom object, defined by T.</returns>
        public static List<T> ImportFixedWidthFile<T>(string filePath, Dictionary<string, int> rowDefs, bool hasHeader, bool hasFooter, Logger logger)
        {
            DataTable dt = ImportFixedWidthFile(filePath, rowDefs, hasHeader, hasFooter, logger);
            List<string> colNames = rowDefs.Keys.ToList();

            List<T> returnList = new List<T>();
            T currentObj;

            foreach (DataRow row in dt.Rows)
            {
                // Create new T
                currentObj = (T)Activator.CreateInstance(typeof(T));

                // For each property in the object, get the value from the current row/column and assign it to the object.
                PropertyInfo prop;
                foreach (string colName in colNames)
                {
                    prop = currentObj.GetType().GetProperty(colName);
                    prop.SetValue(currentObj, row[colName].ToString());
                }

                // Add the object to the list and move on to the next row.
                returnList.Add(currentObj);
            }

            return returnList;
        }

        /// <summary>
        /// This function uses C# Generics to allow us to import a fixed width file into a custom C# object with
        /// named properties. It uses a List of KeyValuePairs to retain the original ordering of elements. 
        /// The List should contain an element for each column contained in the file; the String being the name of
        /// the column (and thus the name of the property in the custom object) and the integer being the size
        /// (width) of the column in the file.
        /// </summary>
        /// <typeparam name="T">The desired object type to map the file to.</typeparam>
        /// <param name="filePath">Full path where the file is located.</param>
        /// <param name="rowDefs">KeyValuePair sets representing the name and size of a column.</param>
        /// <param name="hasHeader">Whether the file has a header row. If true, the first line will be removed.</param>
        /// <param name="hasFooter">Whether the file has a footer/trailer row. If true, the last line will be removed.</param>
        /// <param name="logger">The logger, likely your job class.</param>
        /// <returns>A List<> object containing the data from the provided fixed-width file mapped into a custom object, defined by T.</returns>
        public static IEnumerable<T> ImportFixedWidthFile<T>(string filePath, List<KeyValuePair<string, int>> rowDefs, bool hasHeader, bool hasFooter, Logger logger)
        {
            IEnumerable<int> lengths = from row in rowDefs
                                       select row.Value;
            IEnumerable<string> colNames = from row in rowDefs
                                           select row.Key;
            DataTable dt = ImportFixedWidthFile(filePath, lengths.ToArray(), hasHeader, hasFooter, logger);

            // Associate the keys to the DataTable columns.
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                dt.Columns[i].ColumnName = colNames.ElementAt(i);
            }

            List<T> returnList = new List<T>();
            T currentObj;

            foreach (DataRow row in dt.Rows)
            {
                // Create new T
                currentObj = (T)Activator.CreateInstance(typeof(T));

                // For each property in the object, get the value from the current row/column and assign it to the object.
                PropertyInfo prop;
                foreach (string colName in colNames)
                {
                    prop = currentObj.GetType().GetProperty(colName);
                    // If the type is nullable, get the underlying type (non-nullable version)
                    Type propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                    // Convert it to the proper source type
                    prop.SetValue(currentObj, Convert.ChangeType(row[colName].ToString().Trim(), propType));
                }

                // Add the object to the list and move on to the next row.
                returnList.Add(currentObj);
            }

            return returnList;
        }

        /// <summary>
        /// Does one pass of a file pulling sections out that start or contain (your choice) a listing of search values.  
        /// Can return text lines before and after a search value block. Great for EDI files or others that loop
        /// </summary>
        /// <param name="log">Logger</param>
        /// <param name="sourceFile">File to Pull from</param>
        /// <param name="searchValues">List of values to find, make sure they are unique enough you dont get false positives</param>
        /// <param name="startChunk">Optional if you need lines before and after a search value, each appearance of the start chunk will begin and end a block of text, only returns if the search value is found</param>
        /// <param name="endOfSearch">Optional if you want to stop looking at some point</param>
        /// <param name="anywhere">False if the line must start with the search values, true if it just needs to contain them</param>
        /// <param name="findFirst">Optional if you only need the first instance of a chunk</param>
        /// <returns>List of strings starting at a search value until another if encountered </returns>
        public static List<string> GetChunksOfTextFileNearValue(Logger log, string sourceFile, List<string> searchValues, string startChunk = "", string endOfSearch = "", bool anywhere = false, bool findFirst = false)
        {
            List<string> chunks = new List<string>();
            log.WriteToLog($"Starting to pull string chunks out of {System.IO.Path.GetFileName(sourceFile)}.");
            string chunk = "";
            bool foundVal = false;

            //If a certain text string is used to indicate a unique block of text that you want to retain but look inside of for search values
            if (startChunk != "")
            {
                foreach (string line in File.ReadLines(sourceFile))
                {
                    // Console.WriteLine(line); //Testing only
                    if (line.StartsWith(startChunk) || (anywhere && line.Contains(startChunk))) //New chunk starts
                    {
                        if (foundVal) //did we find what we were looking for already
                        {
                            chunks.Add(chunk);//add what we had
                            if (findFirst)
                            {
                                break;
                            }
                        }
                        chunk = ""; //starting over regardless of findings 
                        foundVal = false;
                    }

                    if (!foundVal)
                    {
                        foundVal = anywhere ? searchValues.Any(s => line.Contains(s)) : searchValues.Any(s => line.StartsWith(s));
                    }

                    if (endOfSearch != "" && (line.StartsWith(endOfSearch) || (anywhere && line.Contains(endOfSearch))))
                    {
                        if (foundVal)
                        {
                            chunks.Add(chunk);//add what we had
                        }
                        break;//dont look after end of search
                    }

                    chunk += line + Environment.NewLine;
                }
            }

            //Otherwise we treat each found value as a chunk of text
            else
            {
                foreach (string line in File.ReadLines(sourceFile))
                {
                    //Console.WriteLine(line); //testing only
                    if (foundVal = anywhere ? searchValues.Any(s => line.Contains(s)) : searchValues.Any(s => line.StartsWith(s)))
                    {
                        if (foundVal)
                        {
                            chunks.Add(chunk);
                            if (findFirst)
                            {
                                break;
                            }
                            chunk = "";//start over
                        }
                    }

                    if (endOfSearch != "" && (line.StartsWith(endOfSearch) || (anywhere && line.Contains(endOfSearch))))
                    {
                        if (foundVal)
                        {
                            chunks.Add(chunk);//add what we had
                        }
                        break;//dont look after end of search
                    }

                    chunk += line + Environment.NewLine;
                }
            }
            log.WriteToLog($"Found {chunks.Count} Chunks.");
            return chunks;
        }

        /// <summary>
        /// Takes a delimited file and streams the contents into the specified database table in small batches.
        /// The stream is encapsulated in a single transaction. If there are any errors, the entire transaction
        /// will be rolled back resulting in no change to the destination table.
        /// </summary>
        /// <param name="log">The calling process.</param>
        /// <param name="inputFilePath">Path of the file to be processed.</param>
        /// <param name="hasHeader">Whether the file has a header record.</param>
        /// <param name="hasTrailer">Whether the file has a trailer record.</param>
        /// <param name="columnDelimiter">Delimiter character separating fields in the file.</param>
        /// <param name="targetDatabase">The desired database to insert the data.</param>
        /// <param name="targetTable">The desired table to insert the data.</param>
        /// <param name="endOfLine">Optional argument to override the end of line character.</param>
        /// <param name="mappedTable">Used to map the file columns when there isn't a header row.</param>
        /// <param name="batchSize">Number of records pulled into memory before attempting to insert into the database.</param>
        /// <returns><see langword="true"/> if the operation was successful and the transaction was committed; otherwise <see langword="false"/></returns>
        public static bool StreamFileToDatabaseTable(Logger log, string inputFilePath, bool hasHeader, bool hasTrailer, char columnDelimiter, Data.AppNames targetDatabase, string targetTable, char endOfLine = ' ', DataTable mappedTable = null, int batchSize = 10000)
        {
            log.WriteToLog($"Start stream of {inputFilePath}");

            string GetNextLine(StreamReader reader)
            {
                string nextLine;

                if (endOfLine == ' ')
                {
                    nextLine = reader.ReadLine();
                }
                else
                {
                    nextLine = "";
                    while ((char)reader.Peek() != endOfLine && !reader.EndOfStream)
                    {
                        nextLine += (char)reader.Read();
                    }

                    if ((char)reader.Peek() == endOfLine)
                    {
                        reader.Read(); //advance one char farther to skip the end of line
                    }
                }

                return nextLine;
            }

            Data dataTarget = new Data(targetDatabase);

            using (StreamReader streamReader = new StreamReader(inputFilePath))
            using (SqlConnection connection = dataTarget.GetSqlConnection(dataTarget.Authentication))
            {
                connection.Open();
                SqlTransaction transaction = connection.BeginTransaction();
                
                string inputLine;
                DataTable batchRecords;

                try
                {
                    using (SqlBulkCopy bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, transaction))
                    {
                        bulkCopy.BulkCopyTimeout = 1800;
                        bulkCopy.DestinationTableName = targetTable;

                        // Build the DataTable definition
                        if (mappedTable != null)
                        {
                            // Explicit mapping was provided, use that.
                            batchRecords = mappedTable.Clone();
                        }
                        else if (hasHeader)
                        {
                            // No mapping was provided, build out the table structure based on the header row.
                            inputLine = GetNextLine(streamReader);
                            batchRecords = new DataTable();

                            foreach (string col in inputLine.Split(columnDelimiter))
                            {
                                batchRecords.Columns.Add(col);
                            }
                        }
                        else
                        {
                            // We got nothin' to map to! Default to target table schema.
                            batchRecords = DataWork.GetTableSchema(targetTable, targetDatabase);
                        }

                        batchRecords.BeginLoadData();

                        int totalRecords = 0;

                        // Read file in batches until end of file
                        while (!streamReader.EndOfStream)
                        {
                            inputLine = GetNextLine(streamReader);

                            if (string.IsNullOrWhiteSpace(inputLine))
                            {
                                // We don't want this junk...
                                log.WriteToLog("We found a blank line in your file, that's gross!", UniversalLogger.LogCategory.WARNING);
                                continue;
                            }

                            if (!(hasTrailer && streamReader.EndOfStream))
                            {
                                InsertNewRow(batchRecords, inputLine, columnDelimiter.ToString(), log);
                            }

                            // When each batch size is met, or we're at the end of the file, dump the records to the database
                            if ((batchRecords.Rows.Count == batchSize) || streamReader.EndOfStream)
                            {
                                // Send this batch to the database
                                try
                                {
                                    batchRecords.EndLoadData();
                                }
                                catch // We're going to eat this exception because WriteToServer will fail anyways
                                {
                                    List<DataRow> erroredRows = batchRecords.GetErrors().ToList();
                                    foreach (DataRow errRow in erroredRows)
                                    {
                                        List<DataColumn> erroredColumns = errRow.GetColumnsInError().ToList();
                                        foreach (DataColumn badCol in erroredColumns)
                                        {
                                            log.WriteToLog("The value we placed into " + badCol.ColumnName + " was bad, the value was: " + errRow[badCol].ToString(), UniversalLogger.LogCategory.WARNING);
                                        }
                                    }
                                }

                                bulkCopy.WriteToServer(batchRecords);
                                totalRecords += batchRecords.Rows.Count;
                                log.WriteToLog($"Progress - Total Records Inserted: {totalRecords}");

                                // Reset data
                                batchRecords = batchRecords.Clone();
                                batchRecords.BeginLoadData();
                            }
                        }

                        transaction.Commit();
                    }
                }
                catch (Exception ex)
                {
                    log.WriteToLog($"An error occurred when attempting to stream the file to the database. Rolling back the transaction...\n{ex}", UniversalLogger.LogCategory.ERROR);

                    try
                    {
                        transaction.Rollback();
                    }
                    catch (Exception ex2)
                    {
                        log.WriteToLog($"An error occurred when attempting to roll back the transaction.\n{ex2}", UniversalLogger.LogCategory.ERROR);
                    }

                    return false;
                }
            }

            log.WriteToLog($"Completed streaming {inputFilePath}");

            return true;
        }
    }
}
