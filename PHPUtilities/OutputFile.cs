using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.IO.Compression;
using System.Data.SqlClient;
using System.Data.Odbc; //For Sybase
using System.Data.Common; //For DBdataReader
//using System.Web.Configuration;
using System.Runtime.Serialization.Json;
using System.Data;
namespace Utilities
{
    public class OutputFile
    {
        /// <summary>
        /// Outputs your enumerable to a flat-file, leveraging the provided field separator
        /// </summary>
        /// <typeparam name="T">Type of the objects to output</typeparam>
        /// <param name="path">Where to output the file</param>
        /// <param name="outputSet">IEnumerable to output</param>
        /// <param name="separator">Field separator</param>
        /// <param name="zipCompress">Compress the output after it is written</param>
        /// <param name="AddHeaders">Add headers from the given class</param>
        /// <param name="trailerString">A string to be placed at the end of the file. Passing this parameter requires that proclog also be passed.</param>
        /// <returns>The outputfile - will be the same as the path, except in the case of zipping</returns>
        public static string WriteSeparated<T>(String path, IEnumerable<T> outputSet, String separator, Boolean zipCompress, Boolean AddHeaders = false, Boolean endWithSeparator = true, Boolean quoteQualify = false, String headerString = "", String trailerString = "", Logger proclog = null)
        {
            List<PropertyInfo> propList = new List<PropertyInfo>();
            propList.AddRange(typeof(T).GetProperties().ToList());

            String outputFile;

            if (zipCompress)
            {
                outputFile = path.Replace(@"\Output\" + DateTime.Today.Year + @"\", @"\Staging\");
                FileSystem.ReportYearDir(System.IO.Path.GetDirectoryName(outputFile));
            }
            else
            {
                outputFile = path;
                FileSystem.ReportYearDir(System.IO.Path.GetDirectoryName(outputFile));
            }


            using (var w = new StreamWriter(outputFile))
            {
                w.AutoFlush = true;

                if (AddHeaders && headerString != "")
                {
                    throw new ArgumentException("You gave us both a header string value, and said to add headers - you can't have both");
                }

                if (AddHeaders)
                {
                    String headerRow = "";
                    foreach (PropertyInfo col in propList)
                    {
                        if (quoteQualify)
                        {
                            headerRow += @"""" + col.Name + @"""" + separator.ToString();
                        }
                        else
                        {
                            headerRow += col.Name + separator.ToString();
                        }

                    }

                    w.WriteLine(EndSeparator(endWithSeparator, headerRow, separator));

                }

                int recCount = 0;
                String outputText = "";
                if (headerString != "")
                {
                    w.WriteLine(EndSeparator(endWithSeparator, headerString, separator));
                }
                
                foreach (T rec in outputSet)
                {
                    String row = "";
                    foreach (PropertyInfo col in propList)
                    {
                        if (rec.GetType().GetProperty(col.Name).GetValue(rec) != null)
                        {
                            if (quoteQualify)
                            {
                                row += @"""" + rec.GetType().GetProperty(col.Name).GetValue(rec).ToString().Replace("\n", " ") + @"""" + separator.ToString();
                            }
                            else
                            {
                                row += rec.GetType().GetProperty(col.Name).GetValue(rec).ToString().Replace("\n", " ") + separator.ToString();
                            }

                        }
                        else
                        {
                            if (quoteQualify)
                            {
                                row += @"""""" + separator.ToString();
                            }
                            else
                            {
                                row += separator.ToString();
                            }

                        }

                    }
                    outputText += EndSeparator(endWithSeparator, row, separator) + Environment.NewLine;
                    recCount++;

                    if (recCount % 1000 == 0) //every thousandth record, write to file
                    {
                        w.Write(outputText);
                        outputText = "";
                    }
                }

                if (outputText != "")
                {
                    w.Write(outputText);
                }

                if (trailerString != "")
                {
                    WriteTrailer(separator, proclog, endWithSeparator, trailerString, recCount, w);
                }
            }

            if (zipCompress)
            {
                String zipOutput = System.IO.Path.ChangeExtension(path, ".zip");

                if (System.IO.File.Exists(zipOutput))
                {
                    System.IO.File.Delete(zipOutput);
                }
                System.IO.Compression.ZipFile.CreateFromDirectory(System.IO.Path.GetDirectoryName(outputFile), zipOutput);

                List<String> dirtyFiles = System.IO.Directory.GetFiles(System.IO.Path.GetDirectoryName(outputFile)).ToList();

                foreach (String file in dirtyFiles)
                {
                    System.IO.File.Delete(file);
                }
                System.IO.Directory.Delete(System.IO.Path.GetDirectoryName(outputFile));

                return zipOutput;
            }

            return path;
        }

        public static string WriteSeparated(String fileName, DataTable outputSet, String separator, Logger proclog, Boolean AddHeaders = false, Boolean endWithSeparator = true, Boolean quoteQualify = false, String headerString = "", String trailerString = "", Boolean isFileLocation = false, Boolean quoteQualifyHeader = false)
        {
            string outputFile;
            if (isFileLocation)
            {
                outputFile = fileName;
            }
            else
            {
                outputFile = proclog.LoggerOutputYearDir + fileName;
            }
            proclog.WriteToLog("Writing file to " + outputFile);
            StringBuilder stringBuilder = new StringBuilder();

            FileSystem.ReportYearDir(System.IO.Path.GetDirectoryName(outputFile));

            string qualifier = quoteQualify ? "\"" : "";
            using (var w = new StreamWriter(outputFile))
            {

                int rowCount = 0;
                if (AddHeaders)
                {
                    rowCount++;
                    List<string> cols = new List<string>();
                    foreach (DataColumn header in outputSet.Columns)
                    {
                        cols.Add(quoteQualifyHeader ? "\"" + header.ColumnName.ToString() + "\"" : header.ColumnName.ToString());
                    }
                    stringBuilder.AppendLine(endWithSeparator ? string.Join(separator, cols) + separator : string.Join(separator, cols));
                }
                if (headerString != "")
                {
                    rowCount++;
                    stringBuilder.AppendLine(EndSeparator(endWithSeparator, headerString, separator));
                }
                IEnumerable<string> fields;
                foreach (DataRow row in outputSet.Rows)
                {
                    rowCount++;
                    fields = row.ItemArray.Select(field => qualifier + field.ToString() + qualifier);
                    stringBuilder.AppendLine(endWithSeparator ? string.Join(separator, fields) + separator : string.Join(separator, fields));
                    if (rowCount % 1000 == 0)
                    {
                        w.Write(stringBuilder);
                        stringBuilder.Clear();
                    }
                }
                if (rowCount > 0 && rowCount % 1000 != 0)
                {
                    w.Write(stringBuilder);
                    stringBuilder.Clear();
                }
                if (trailerString != "")
                {
                    WriteTrailer(separator, proclog, endWithSeparator, trailerString, outputSet.Rows.Count, w);
                }
            }

            return outputFile;
        }

        /// <summary>
        /// Scrubs the written line to end with or without the separator
        /// </summary>
        /// <param name="endWithSeparator">If the row should end with its separator</param>
        /// <param name="sourceRow">Constructed row to be scrubbed</param>
        /// <param name="separator">Value to check doesn't exist on the end</param>
        /// <returns></returns>
        private static String EndSeparator(Boolean endWithSeparator, String sourceRow, String separator)
        {
            if (!endWithSeparator && sourceRow.Substring(sourceRow.Length - 1) == separator && separator != "")
            {
                return sourceRow.Substring(0, sourceRow.Length - 1);
            }
            else
            {
                return sourceRow;
            }
        }

        /// <summary>
        /// This is an optimized extract method that doesn't leverage any generics or custom objects
        /// </summary>
        /// <param name="DataSource">Utilities.Data enum to connect to and query</param>
        /// <param name="Query">Query to hit against the database</param>
        /// <param name="separator">A value to insert between each field in the results</param>
        /// <param name="processLog">Calling object - we'll log the query here</param>
        /// <param name="outputLoc">The full path to the output file</param>
        /// <param name="TimeOut">Timeout for the sql connection, defaults to 4 minutes</param>
        /// <param name="AddHeaders">Pull headers from the SQL results and output them on the first row of the output</param>
        /// <param name="endWithSeparator">Whether the rows should end with a separator or not</param>
        /// <param name="logging">Whether you want the extract to log the query and other details on your behalf</param>
        /// <param name="headerString">String to insert in the first row of the outputfile (i.e. as a header record)</param>
        /// <param name="trailerString">String to insert in the last row of the outputfile (i.e. as a trailer record) </param>
        /// <param name="trailerStringHandler">Delegate for inserting the record count into the trailer</param>
        /// <param name="compressOutput">Should the output be compressed after being written</param>
        public static int OutputBulkText(Data.AppNames DataSource, String Query, String separator, Logger processLog, String outputLoc, int TimeOut = 240, Boolean AddHeaders = false, Boolean endWithSeparator = true, Boolean logging = true, String headerString = "", String trailerString = "", String recordBrace = "")
        {
            if (logging)
            {
                processLog.WriteToLog("This is the query that was passed into the method :" + Query);
            }

            Data dataConnection = new Data(DataSource);
            int recCount = 0;
            if (DataSource == Data.AppNames.ExampleProd)
            {

                using (OdbcConnection cn = dataConnection.GetOdbcConnection())
                {
                    // cn.ConnectionString = "Driver={Microsoft Access Driver (*.mdb)};DBQ="+DataSource+".mdb;"+"";
                    cn.Open();
                    OdbcCommand cmd = new OdbcCommand();
                    cmd.Connection = cn;
                    cmd.CommandType = System.Data.CommandType.Text;
                    cmd.CommandTimeout = TimeOut;
                    cmd.CommandText = Query;

                    OdbcDataReader dataRecord = cmd.ExecuteReader();
                    recCount = DataRecordFun(dataRecord, separator, processLog, outputLoc, TimeOut, AddHeaders, endWithSeparator, logging, headerString, trailerString, recordBrace);

                }
            }
            else
            {
                using (SqlConnection cn = dataConnection.GetSqlConnection(dataConnection.Authentication))
                {

                    cn.Open();
                    SqlCommand cmd = new SqlCommand();
                    cmd.Connection = cn;
                    cmd.CommandType = System.Data.CommandType.Text;
                    cmd.CommandTimeout = TimeOut;
                    cmd.CommandText = Query;
                    SqlDataReader dataRecord = cmd.ExecuteReader();
                    recCount = DataRecordFun(dataRecord, separator, processLog, outputLoc, TimeOut, AddHeaders, endWithSeparator, logging, headerString, trailerString, recordBrace);
                }
            }
            return recCount;

        }
        public static int DataRecordFun(DbDataReader dataRecord, String separator, Logger processLog, String outputLoc, int TimeOut, Boolean AddHeaders, Boolean endWithSeparator, Boolean logging, String headerString, String trailerString, String recordBrace = "")
        {
            int recCount = 0;
            int colCount = dataRecord.FieldCount;

            FileSystem.ReportYearDir(System.IO.Path.GetDirectoryName(outputLoc), processLog);

            using (StreamWriter w = new StreamWriter(outputLoc))
            {
                String headerRow = "";
                w.AutoFlush = true;

                if (AddHeaders && headerString != "")
                {
                    throw new ArgumentException("You gave us both a header string value, and said to add headers - you can't have both");
                }
                else if (AddHeaders)
                {
                    processLog.WriteToLog("You told us to add headers from the query");
                    for (int i = 0; i < colCount; i++)
                    {
                        headerRow += dataRecord.GetName(i) + separator;
                    }
                    w.WriteLine(EndSeparator(endWithSeparator, headerRow, separator));
                }
                else if (headerString != "")
                {
                    processLog.WriteToLog("You gave us a header string, it was: " + headerString);
                    w.WriteLine(EndSeparator(endWithSeparator, headerString, separator));
                }

                String outputText = "";

                while (dataRecord.Read())
                {


                    String row = "";
                    for (int i = 0; i < colCount; i++)
                    {
                        row += recordBrace + dataRecord[i] + recordBrace + separator;
                    }
                    outputText += EndSeparator(endWithSeparator, row, separator).Replace("\n", "") + Environment.NewLine;
                    recCount++;
                    if (recCount % 1000 == 0) //every thousandth record, write to file
                    {
                        w.Write(outputText);
                        outputText = "";
                    }
                }

                if (outputText != "")
                {
                    w.Write(outputText);
                }

                if (trailerString != "")
                {
                    WriteTrailer(separator, processLog, endWithSeparator, trailerString, recCount, w);
                }
            }

            return recCount;
        }

        private static void WriteTrailer(String separator, Logger processLog, Boolean endWithSeparator, String trailerString, int recCount, StreamWriter w)
        {
            processLog.WriteToLog("You gave us a trailer string :" + trailerString);

            String formattedTrailerString;
            //if (trailerStringHandler != null)
            //{
            //    formattedTrailerString = trailerStringHandler(trailerString, recCount);
            //}
            //else
            //{
                formattedTrailerString = trailerString;
            //}

            w.WriteLine(EndSeparator(endWithSeparator, formattedTrailerString, separator));
        }

        public static string CompressOutputFile(Logger processLog, String outputFile)
        {
            return "";

        }


        public static void WriteJson(Object passedType, String outputLoc, Logger proclog)
        {
            DataContractJsonSerializerSettings settings = new DataContractJsonSerializerSettings();
            settings.UseSimpleDictionaryFormat = true;
            settings.EmitTypeInformation = System.Runtime.Serialization.EmitTypeInformation.Never;

            DataContractJsonSerializer serializer = new DataContractJsonSerializer(passedType.GetType(), settings);
            String jsonString;
            using (MemoryStream s = new MemoryStream())
            {
                serializer.WriteObject(s, passedType);
                jsonString = Encoding.UTF8.GetString(s.ToArray());
            }

            String prettifiedJson = jsonString.Replace("[", "[" + Environment.NewLine).Replace(",", "," + Environment.NewLine).Replace("]", "]" + Environment.NewLine).Replace("}", "}" + Environment.NewLine);

            //fix backslash issue
            prettifiedJson = prettifiedJson.Replace(@"\/", @"/");

            if (System.IO.File.Exists(outputLoc))
            {
                System.IO.File.Delete(outputLoc);
            }
            System.IO.File.WriteAllText(outputLoc, prettifiedJson);

        }

    }
}
