using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Linq;
using System.Data.SqlClient;
using System.Data;
//using Oracle.ManagedDataAccess.Client;
using System.Data.Odbc;
using System.IO;
using System.Reflection;
using System.Data.Common;
using System.ComponentModel;
using System.Data.SqlTypes;

namespace Utilities
{
    public static class DataWork
    {
        //SqlClient seems to incorrectly infer the correct DB type for some C# types. Look:
        //https://github.com/dotnet/SqlClient/blob/cbfa11916d4924ed305c5cb033db6ad3c95d3cdd/src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlParameter.cs#L1879
        //and then look up the definition of GetDefaultMetaType(). What's a byte[] in SQL land? idfk bro just throw an nvarchar in there and let's get to the bar
        //I can't read this as anything other than Microsoft half-assing the connector between *their own language* and *their own database software*. Idiots. -James
        static internal readonly Dictionary<Type, SqlDbType> DbTypeOverrides = new Dictionary<Type, SqlDbType>
        {
            {typeof(byte[]),   SqlDbType.VarBinary },
            {typeof(SqlBytes), SqlDbType.VarBinary },
        };

        /// <summary>
        /// Connects to the given database and deletes all rows from the table
        /// </summary>
        /// <param name="table">Include the schema portion to ensure successful processing</param>
        /// <param name="database">Database to connect and command</param>
        public static void TruncateWorkTable(String table, Data.AppNames database)
        {
            if(ExtractFactory.ConnectAndQuery<int>(database, "SELECT COUNT(*) FROM " + table).First() > 0)
            {
                Data databaseConn = new Data(database);
                DataContext db = databaseConn.OpenConnectionAndGetDatabase();
                db.CommandTimeout = 1200;
                db.ExecuteCommand("delete from " + table);

                db.SubmitChanges();

                databaseConn.CloseConnection();
            }
        }
        
        /// <summary>
        /// Connects to the given database and deletes all rows from the table
        /// </summary>
        /// <param name="table">Include the schema portion to ensure successful processing</param>
        /// <param name="database">Database to connect and command</param>
        [Obsolete("Use TruncateWorkTable instead")]
        public static void DeleteAllRowsFromTable(String table, Data.AppNames database)
        {
            Data databaseConn = new Data(database);
            DataContext db = databaseConn.OpenConnectionAndGetDatabase();
            db.CommandTimeout = 1200;
            db.ExecuteCommand("DELETE FROM " + table);

            db.SubmitChanges();

            databaseConn.CloseConnection();
        }

        /// <summary>
        /// Runs a SQL command against the given database, does not return data.
        /// </summary>
        /// <param name="query">Instruction to pass to the database</param>
        /// <param name="database">Database to connect to</param>
        /// <param name="queryTimeOut">Timeout window before we throw an error</param>
        [Obsolete("Use overloaded version that requires Logger to be passed in for query logging")]
        public static void RunSqlCommand(String query, Data.AppNames database, int queryTimeOut = 600)
        {
            Data JobManConnection = new Data(database);

            DataContext JobMan = JobManConnection.OpenConnectionAndGetDatabase();
            JobMan.CommandTimeout = queryTimeOut;
            JobMan.ExecuteCommand(query);

            JobMan.SubmitChanges();

            JobManConnection.CloseConnection();
        }

        /// <summary>
        /// Runs a SQL command against the given database, does not return data.
        /// </summary>
        /// <param name="query">Instruction to pass to the database</param>
        /// <param name="database">Database to connect to</param>
        /// <param name="queryTimeOut">Timeout window before we throw an error</param>
        public static void RunSqlCommand(Logger logger, String query, Data.AppNames database, int queryTimeOut = 600)
        {
            logger.WriteToLog($"Here is the query that was passed in: {query}");

            RunSqlCommand(query, database, queryTimeOut);
        }

        /// <summary>
        /// Runs a SQL command against the given database, does not return data. Leverages sql parameters to avoid sql injection in params
        /// </summary>
        /// <param name="query">Instruction to pass to the database</param>
        /// <param name="database">Database to connect to</param>
        /// <param name="queryTimeOut">Timeout window before we throw an error</param>
        /// <param name="sqlParams">Parameters to pass into the sql query</param>
        [Obsolete("Use overloaded version that requires Logger to be passed in for query logging")]
        public static void RunSqlCommand(String query, Data.AppNames database, List<SqlParameter> sqlParams)
        {
            Data JobManConnection = new Data(database);

            using (SqlConnection conn = JobManConnection.GetSqlConnection(JobManConnection.Authentication))
            {

                SqlCommand cmd = new SqlCommand(query, conn);

                foreach (SqlParameter param in sqlParams)
                {
                    cmd.Parameters.Add(param);
                }

                conn.Open();

                cmd.ExecuteNonQuery();

                conn.Close();
                conn.Dispose();
            }

        }

        /// <summary>
        /// Runs a SQL command against the given database, does not return data. Leverages sql parameters to avoid sql injection in params
        /// </summary>
        /// <param name="query">Instruction to pass to the database</param>
        /// <param name="database">Database to connect to</param>
        /// <param name="queryTimeOut">Timeout window before we throw an error</param>
        /// <param name="sqlParams">Parameters to pass into the sql query</param>
        public static void RunSqlCommand(Logger logger, String query, Data.AppNames database, List<SqlParameter> sqlParams)
        {
            string queryForLogging = query;

            foreach (SqlParameter param in sqlParams)
            {
                queryForLogging = queryForLogging.Replace($"@{param.ParameterName}", param.Value.ToString());
            }

            logger.WriteToLog($"Here is the query that was passed in: {queryForLogging}");

            RunSqlCommand(query, database, sqlParams);
        }

        /// <summary>
        /// Runs a SQL command against the given database and returns the number of rows affected. Useful for updates
        /// </summary>
        /// <param name="query">Instruction to pass to the database</param>
        /// <param name="database">Database to connect to</param>
        /// <param name="sqlParams">Parameters to pass into the sql query</param>
        [Obsolete("Use overloaded version that requires Logger to be passed in for query logging")]
        public static int RunSqlCommandWithRecordCount(String query, Data.AppNames database)
        {
            int Count = 0;
            Data JobManConnection = new Data(database);
            using (SqlConnection conn = JobManConnection.GetSqlConnection(JobManConnection.Authentication))
            {
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.CommandTimeout = 240;
                conn.Open();

                Count = cmd.ExecuteNonQuery();

                conn.Close();
                conn.Dispose();
            }
            return Count;
        }

        /// <summary>
        /// Runs a SQL command against the given database and returns the number of rows affected. Useful for updates
        /// </summary>
        /// <param name="query">Instruction to pass to the database</param>
        /// <param name="database">Database to connect to</param>
        /// <param name="sqlParams">Parameters to pass into the sql query</param>
        public static int RunSqlCommandWithRecordCount(Logger logger, String query, Data.AppNames database)
        {
            logger.WriteToLog($"Here is the query that was passed in: {query}");

            return RunSqlCommandWithRecordCount(query, database);
        }

        /// <summary>
        /// Runs a SQL command against the given database and returns the number of rows affected. Useful for updates
        /// </summary>
        /// <param name="query">Instruction to pass to the database</param>
        /// <param name="database">Database to connect to</param>
        /// <param name="sqlParams">Parameters to pass into the sql query</param>
        [Obsolete("Use overloaded version that requires Logger to be passed in for query logging")]
        public static int RunSqlCommandWithRecordCount(String query, Data.AppNames database, List<SqlParameter> sqlParams)
        {
            int Count = 0;
            Data JobManConnection = new Data(database);
            using (SqlConnection conn = JobManConnection.GetSqlConnection(JobManConnection.Authentication))
            {
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.CommandTimeout = 240;

                foreach (SqlParameter param in sqlParams)
                {
                    cmd.Parameters.Add(param);
                }
                conn.Open();

                Count = cmd.ExecuteNonQuery();

                conn.Close();
                conn.Dispose();
            }
            return Count;
        }

        /// <summary>
        /// Runs a SQL command against the given database and returns the number of rows affected. Useful for updates
        /// </summary>
        /// <param name="query">Instruction to pass to the database</param>
        /// <param name="database">Database to connect to</param>
        /// <param name="sqlParams">Parameters to pass into the sql query</param>
        public static int RunSqlCommandWithRecordCount(Logger logger, String query, Data.AppNames database, List<SqlParameter> sqlParams)
        {
            string queryForLogging = query;

            foreach (SqlParameter param in sqlParams)
            {
                queryForLogging = queryForLogging.Replace($"@{param.ParameterName}", param.Value.ToString());
            }

            logger.WriteToLog($"Here is the query that was passed in: {queryForLogging}");

            return RunSqlCommandWithRecordCount(query, database, sqlParams);
        }

        /// <summary>
        /// Takes a template-like query string and a list of column names, returns the query configured for use with parameterized query methods.
        /// This method is expected to cover common uses, but feel free to spice it up if needed (here if generally useful, or in your new code if not)
        /// </summary>
        /// <param name="query">query like "insert into Table ({0}) VALUES ({1})"</param>
        /// <param name="columnNames">strings representing the column names you'll be working with</param>
        /// <param name="isUpdateQuery">Whether to format for update queries where column names and parameter references are together</param>
        /// <returns></returns>
        public static string ConfigureDynamicQuery(string query, IEnumerable<string> columnNames, bool isUpdateQuery)
        {
            if (isUpdateQuery)
            {
                query = query.Replace("{0}", String.Join(", ", columnNames.Select(x => $"[{x}] = @{x}"))); //[Column1] = @Column1, [Column2] = @Column2,
            }
            else
            {
                query = query.Replace("{0}", String.Join(", ", columnNames.Select(x => $"[{x}]"))); //[Column1], [Column2]
                query = query.Replace("{1}", String.Join(", ", columnNames.Select(x => $"@{x}"))); //@Column1, @Column2
            }

            return query;
        }

        /// <summary>
        /// Runs a SQL command with an OUTPUT clause and returns the output data, using DbRecordBase subclasses as input
        /// </summary>
        /// <param name="query">Parameterized query</param>
        /// <param name="database">Database to connect to</param>
        /// <param name="parameterData">Parameters to pass into the sql query</param>
        public static object RunSqlCommandWithOutput(Logger logger, String query, Data.AppNames database, DbRecordBase parameterData)
        {
            string queryForLogging = query;

            foreach (KeyValuePair<string, object> param in parameterData.GetDataAsKVP())
            {
                queryForLogging = queryForLogging.Replace($"@{param.Key}", param.Value.ToString());
                //what does Replace do if you replace text with a byte[]? Hope this looks okay
            }
            logger.WriteToLog($"Here is the query that was passed in: {queryForLogging}");

            object response;
            Data JobManConnection = new Data(database);
            using (SqlConnection conn = JobManConnection.GetSqlConnection(JobManConnection.Authentication))
            {
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.CommandTimeout = 240;

                foreach (KeyValuePair<string, object> param in parameterData.GetDataAsKVP())
                {
                    if (DbTypeOverrides.ContainsKey(param.Value.GetType())) //SqlClient will try to send byte[] and some other C# types as NVARCHAR; fix them manually
                    {
                        SqlParameter sqlParam = cmd.Parameters.Add(
                            parameterName: "@"+param.Key,
                            sqlDbType: DbTypeOverrides[param.Value.GetType()]
                        );
                        sqlParam.Value = param.Value; //Couldn't find an Add() overload that accepts everything at once - tack this on separately
                    }
                    else
                    {
                        cmd.Parameters.AddWithValue("@"+param.Key, param.Value);
                    }
                }
                conn.Open();

                response = cmd.ExecuteScalar();

                conn.Close();
                conn.Dispose();
            }
            return response;
        }

        public static void RunOdbcCommand(Logger logger, String query, Data.AppNames database)
        {
            logger.WriteToLog($"Here is the query that was passed in: {query}");
            
            Data JobManConnection = new Data(database);

            using (OdbcConnection conn = JobManConnection.GetOdbcConnection())
            {

                OdbcCommand cmd = new OdbcCommand(query, conn);
                cmd.CommandType = CommandType.StoredProcedure;

                // You have to add “Return_Value” as first param

                cmd.Parameters.Add("Return_Value", OdbcType.Int);

                conn.Open();

                cmd.ExecuteNonQuery();

                conn.Close();
                conn.Dispose();
            }

        }

        /// <summary>
        /// Returns get the server on which the given database is sitting, per PHP conventions this should be the same as the linked server
        /// </summary>
        /// <returns>The configured server on which the database is sitting</returns>
        public static String LinkedServer(Data.AppNames dataSource)
        {
            Data JobMan = new Data(dataSource);

            return JobMan.server;
        }

        /// <summary>
        /// Uses SqlBulkCopy to load the given DataTable into the provided database table. Note for future, it is possible to use a DataAdapter object to load via the DataTable directly, haven't tried it.
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="targetTable"></param>
        /// <param name="fileToLoad"></param>
        /// <param name="proclog"></param>
        public static void LoadTable(Data.AppNames targetDb, String targetTable, DataTable fileToLoad, Logger proclog, bool mapSourceToTargetColumns = false)
        {
            Data jobManTarget = new Data(targetDb);
            
            using (SqlConnection jobManConn = jobManTarget.GetSqlConnection(jobManTarget.Authentication))
            {
                jobManConn.Open();
                using (SqlBulkCopy bulkCop = new SqlBulkCopy(jobManConn))
                {
                    bulkCop.BulkCopyTimeout = 1800;
                    bulkCop.DestinationTableName = targetTable;
                    
                    if (mapSourceToTargetColumns)
                    {
                        foreach (DataColumn dc in fileToLoad.Columns)
                        {
                            DataTable destination = GetTableSchema(targetTable, targetDb);

                            if (destination.Columns.Contains(dc.ColumnName))
                            {
                                bulkCop.ColumnMappings.Add(destination.Columns[dc.ColumnName].ColumnName, destination.Columns[dc.ColumnName].ColumnName);
                            }
                        }
                    }

                    try
                    {
                        fileToLoad.EndLoadData();
                    }
                    catch //we're going to eat this exception because the WriteToServer will fail anyways
                    {
                        List<DataRow> erroredRows = fileToLoad.GetErrors().ToList();
                        foreach (DataRow errRow in erroredRows)
                        {
                            List<DataColumn> erroredColumns = errRow.GetColumnsInError().ToList();
                            foreach (DataColumn badCol in erroredColumns)
                            {
                                proclog.WriteToLog("The value we placed into " + badCol.ColumnName + " was bad, the value was: " + errRow[badCol].ToString());
                            }
                        }
                    }

                    bulkCop.WriteToServer(fileToLoad);
                }

                jobManConn.Close();
            }
        }

        /// <summary>
        /// Connects to a remote database, pulling those results and then loading them into a given database and table
        /// </summary>
        /// <param name="procLog">Calling process (this)</param>
        /// <param name="sourceDatabase">Database to connect to for source data</param>
        /// <param name="query">Query to submit against source database</param>
        /// <param name="targetDatabase">Database to load results into</param>
        /// <param name="targetTable">Table to load results into (should match the schema of the query)</param>
        [Obsolete("There is a more efficient version of this method called LoadTableFromQuery, we should use that instead")]
        public static void GetDataAndLoadTable(Logger procLog, Data.AppNames sourceDatabase, String query, Data.AppNames targetDatabase, String targetTable, Boolean MapSourceColumnsByName = false)
        {
            DataTable fileLoaded = new DataTable();
            Data sourceCreds = new Data(sourceDatabase);

            procLog.WriteToLog("This is the query that was passed into GetDataAndLoadTable: " + Environment.NewLine + query);
            if (sourceDatabase == Data.AppNames.ExampleTest)
            {

                if (sourceDatabase == Data.AppNames.ExampleTest)
                {
                    //for whatever reason these databases have issues with a diverse set of whitepace characters
                    query = ScrubWhiteSpaceAfterNewLineChar(query);
                }
                OdbcConnection odbcConn = sourceCreds.GetOdbcConnection();

                odbcConn.ConnectionTimeout = 0;

                using (OdbcDataAdapter odbcAdapter = new OdbcDataAdapter(query, odbcConn))
                {
                    odbcAdapter.SelectCommand.CommandTimeout = 0;
                    odbcAdapter.Fill(fileLoaded);
                }

                procLog.WriteToLog("We've completed the query process, now loading into the table...");
                DataWork.LoadTable(targetDatabase, targetTable, fileLoaded, procLog, MapSourceColumnsByName);

                odbcConn.Close();
            }
            else
            {
                SqlConnection JobManSqlConn = sourceCreds.GetSqlConnection(sourceCreds.Authentication);

                using (SqlDataAdapter dAdapter = new SqlDataAdapter(query, JobManSqlConn))
                {
                    dAdapter.SelectCommand.CommandTimeout = 0;
                    dAdapter.Fill(fileLoaded);
                }

                procLog.WriteToLog("We've completed the query process, now loading into the table...");
                DataWork.LoadTable(targetDatabase, targetTable, fileLoaded, procLog, MapSourceColumnsByName);

                JobManSqlConn.Close();
            }

        }

        /// <summary>
        /// This is analagous to GetDataAndLoadTable, except it doesn't load the data to memory. Takes a given query against an ODBC source and loads to a given database and table.
        /// </summary>
        /// <param name="sourceOfData">ODBC Data Source</param>
        /// <param name="query">Query to run against source</param>
        /// <param name="dataTarget">SQL database to load to</param>
        /// <param name="tableToLoad">Table to load query results</param>
        /// <param name="procLog">Calling process (for logging)</param>
        public static void LoadTableFromQuery(Data.AppNames sourceOfData, String query, Data.AppNames dataTarget, String tableToLoad, Logger procLog)
        {

            Data dataSource = new Data(sourceOfData);
            Data targetLoc = new Data(dataTarget);
            procLog.WriteToLog("Starting Data Load from Bulk Loader");
            procLog.WriteToLog("Here is the query we were given : " + query);
            if (IsOdbcDataSource(sourceOfData))
            {
                using (OdbcConnection source = dataSource.GetOdbcConnection())
                {
                    source.Open();
                    SqlConnection targetConn = targetLoc.GetSqlConnection(targetLoc.Authentication);
                    using (SqlBulkCopy bulk = new SqlBulkCopy(targetConn))
                    {
                        bulk.DestinationTableName = tableToLoad;
                        OdbcCommand command = new OdbcCommand(query, source);
                        command.CommandTimeout = 0;
                        OdbcDataReader reader = command.ExecuteReader();
                        targetConn.Open();
                        bulk.BulkCopyTimeout = 0;
                        bulk.WriteToServer(reader);
                    }
                }
            }
            else
            {
                using (SqlConnection source = dataSource.GetSqlConnection(dataSource.Authentication))
                {
                    source.Open();
                    SqlConnection targetConn = targetLoc.GetSqlConnection(targetLoc.Authentication);
                    using (SqlBulkCopy bulk = new SqlBulkCopy(targetConn))
                    {
                        bulk.DestinationTableName = tableToLoad;
                        SqlCommand cmd = new SqlCommand(query, source);
                        //OdbcCommand command = new OdbcCommand(query, SqlConn);
                        cmd.CommandTimeout = 0;
                        SqlDataReader reader = cmd.ExecuteReader();
                        targetConn.Open();
                        bulk.BulkCopyTimeout = 0;
                        bulk.WriteToServer(reader);
                    }
                }
            }

        }
               
        public static bool IsOdbcDataSource(Data.AppNames dataSource)
        {
            if (true)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private static string ScrubWhiteSpaceAfterNewLineChar(String line)
        {
            return line.Replace(Convert.ToChar(160), ' ').Replace(Convert.ToChar(32), ' ').Replace('\r', ' ').Replace('\n', ' ');


        }

        /// <summary>
        /// Given a query with a single column and a target database, returns a single string with a comma (and single-quote qualified) list of values in that table.
        /// Use this to quickly extract data from a config table and build that list into your query via an 'in' statement.
        /// </summary>
        /// <param name="proclog">Calling program (this)</param>
        /// <param name="sourceDatabase">Database to query</param>
        /// <param name="query">Single-column query whose results will be transformed into the 'in' list</param>
        /// <returns></returns>
        public static String GetValuesInCommaSeparatedList(Logger proclog, Data.AppNames sourceDatabase, String query)
        {
            List<string> ConfigValues = ExtractFactory.ConnectAndQuery<String>(sourceDatabase, query).ToList();

            String inString = "";
            foreach (string s in ConfigValues)
            {
                inString += "'" + s + "',";
            }

            return inString.Substring(0, inString.Length - 1); //drop the last comma
        }

        public static String GetValuesInCommaSeparatedList(List<String> values)
        {
            String inString = "";

            foreach (string s in values)
            {
                inString += "'" + s + "',";
            }

            return inString.Substring(0, inString.Length - 1); //drop the last comma
        }

        public static String QueryView(String viewName)
        {
            return @"select * from " + viewName;
        }

        /// <summary>
        /// Gets the schema for a given table for loading
        /// </summary>
        /// <param name="proclog">Calling process - we'll use this to get PHP_Config's definition</param>
        /// <param name="targetTable">Table to load</param>
        /// <returns>Loaded data table for the appropriate SQL table</returns>
        public static DataTable GetTableSchema(String targetTable, Data.AppNames targetDb)
        {
            DataTable fileLoaded = new DataTable();
            Data jobManConf = new Data(targetDb);
            SqlConnection JobManSqlConn = jobManConf.GetSqlConnection(jobManConf.Authentication);

            using (SqlDataAdapter dAdapter = new SqlDataAdapter("select top 0 * from " + targetTable, JobManSqlConn))
            {
                dAdapter.FillSchema(fileLoaded, SchemaType.Mapped);
            }
            return fileLoaded;
        }

        /// <summary>
        /// Gets the schema and data for a given table and returns as DataTable
        /// </summary>
        /// <param name="targetTable">Table to load</param>
        /// <param name="targetDb">Db the table is in</param>
        /// <returns>Loaded data table for the appropriate SQL table</returns>
        public static DataTable GetTable(String targetTable, Data.AppNames targetDb, List<string> sourceMappings = default(List<string>), List<string> destMappings = default(List<string>))
        {
            DataTable fileLoaded = new DataTable("Table1");
            Data jobManConf = new Data(targetDb);
            SqlConnection JobManSqlConn = jobManConf.GetSqlConnection(jobManConf.Authentication);

            using (SqlDataAdapter dAdapter = new SqlDataAdapter("select * from " + targetTable, JobManSqlConn))
            {
                if (sourceMappings != default(List<string>) && destMappings != default(List<string>))
                {
                    DataTableMapping mapping = dAdapter.TableMappings.Add("Table", "Table1");
                    for (int i = 0; i < sourceMappings.Count; i++)
                    {
                        mapping.ColumnMappings.Add(sourceMappings[i], destMappings[i]);
                    }
                }
                dAdapter.Fill(fileLoaded);
            }
            return fileLoaded;
        }

        /// <summary>
        /// Queries a given database and returns a DataTable with results
        /// </summary>
        /// <param name="query">The query</param>
        /// <param name="targetDb">Db the table is in</param>
        /// <returns>Loaded data table for the appropriate SQL table</returns>
        [Obsolete("Use overloaded version that requires Logger to be passed in for query logging")]
        public static DataTable QueryToDataTable(String query, Data.AppNames targetDb, List<string> sourceMappings = default(List<string>), List<string> destMappings = default(List<string>))
        {
            DataTable fileLoaded = new DataTable("Table1");
            Data jobManConf = new Data(targetDb);
            SqlConnection JobManSqlConn = jobManConf.GetSqlConnection(jobManConf.Authentication);

            using (SqlDataAdapter dAdapter = new SqlDataAdapter(query, JobManSqlConn))
            {
                if (sourceMappings != default(List<string>) && destMappings != default(List<string>))
                {
                    DataTableMapping mapping = dAdapter.TableMappings.Add("Table", "Table1");
                    for (int i = 0; i < sourceMappings.Count; i++)
                    {
                        mapping.ColumnMappings.Add(sourceMappings[i], destMappings[i]);
                    }
                }
                dAdapter.SelectCommand.CommandTimeout = 50000;
                dAdapter.Fill(fileLoaded);
            }
            return fileLoaded;
        }

        /// <summary>
        /// Queries a given database and returns a DataTable with results
        /// </summary>
        /// <param name="query">The query</param>
        /// <param name="targetDb">Db the table is in</param>
        /// <returns>Loaded data table for the appropriate SQL table</returns>
        public static DataTable QueryToDataTable(Logger logger, String query, Data.AppNames targetDb, List<string> sourceMappings = default(List<string>), List<string> destMappings = default(List<string>))
        {
            logger.WriteToLog($"Here is the query that was passed in: {query}");

            return QueryToDataTable(query, targetDb, sourceMappings, destMappings);
        }

        [Obsolete("Use overloaded version that requires Logger to be passed in for query logging")]
        public static DataTable QueryToDataTableODBC(String query, Data.AppNames targetDB)
        {
            DataTable dataTableToLoad = new DataTable();
            Data database = new Data(targetDB);
            using (OdbcConnection odbcConnection = database.GetOdbcConnection())
            {
                odbcConnection.Open();
                OdbcCommand odbcCommand = new OdbcCommand(query, odbcConnection);
                odbcCommand.CommandTimeout = 50000;
                OdbcDataAdapter adapter = new OdbcDataAdapter(odbcCommand);
                adapter.Fill(dataTableToLoad);
            }
            return dataTableToLoad;
        }

        public static DataTable QueryToDataTableODBC(Logger logger, String query, Data.AppNames targetDB)
        {
            logger.WriteToLog($"Here is the query that was passed in: {query}");

            return QueryToDataTableODBC(query, targetDB);
        }

        /// <summary>
        /// Saves a given data table to a given database
        /// </summary>
        /// <param name="targetTable">Table to insert values in</param>
        /// <param name="fileLoaded">DataTable containing the values to insert</param>
        /// <param name="targetDatabase">Database to insert into</param>
        /// <param name="sourceMappings">(Optional) column names from source</param>
        /// <param name="destMappings">Column names at destination</param>
        public static void SaveDataTableToDb(String targetTable, DataTable fileLoaded, Data.AppNames targetDatabase, List<string> sourceMappings = default(List<string>), List<string> destMappings = default(List<string>))
        {

            Data targetDb = new Data(targetDatabase);

            using (SqlConnection dbConn = targetDb.GetSqlConnection(targetDb.Authentication))
            {
                dbConn.Open();
                using (SqlBulkCopy bulkCop = new SqlBulkCopy(dbConn))
                {
                    bulkCop.DestinationTableName = targetTable;
                    bulkCop.BulkCopyTimeout = 0;
                    if (sourceMappings != default(List<string>) && destMappings != default(List<string>))
                    {
                        for (int i = 0; i < sourceMappings.Count; i++)
                        {
                            bulkCop.ColumnMappings.Add(sourceMappings[i], destMappings[i]);
                        }
                    }
                    bulkCop.WriteToServer(fileLoaded);
                }

                dbConn.Close();
            }
        }


        /// <summary>
        /// Saves a given Excel file into a given table
        /// </summary>
        /// <param name="FileName">File to replace current table with</param>
        /// <param name="TableName">DataTable containing the values to insert</param>
        /// <param name="Database">Database to insert into</param>
        /// <param name="ProcLog">Logger class for filenames, database references, and passing to other functions</param>
        public static void LoadExcelToTable(string FileName, string TableName, Data.AppNames Database, Logger ProcLog, bool guessType = false)
        {
            DataTable DataToLoad = new DataTable();
            DataTable TableSchema = DataWork.GetTableSchema(TableName, ProcLog.LoggerPhpConfig);
            DataTable OldTableContents = DataWork.GetTable(TableName, ProcLog.LoggerPhpConfig);

            //check if columns match
            foreach (DataColumn column in TableSchema.Columns)
            {
                Console.WriteLine(column.MaxLength);
                if (DataToLoad.Columns.Contains(column.ToString()))
                {
                    //Console.WriteLine("Matched on " + column);
                }
                else
                {
                    throw new Exception("Error: Column mismatch: Spreadsheet does not have column " + column + " required for loading in to table");
                }
            }

            foreach (DataColumn column in DataToLoad.Columns)
            {
                if (TableSchema.Columns.Contains(column.ToString()))
                {
                    //Console.WriteLine("Matched on " + column);
                }
                else
                {
                    throw new Exception("Error: Column mismatch: Database does not have column " + column + " contained in spreadsheet");
                }
            }

            //check for primary key error, type mismatch, and length mismatch
            //hack hack HACK!!! this is some vile programming but the more proper methods weren't working
            DataColumn[] PrimaryKeys = TableSchema.PrimaryKey;
            List<string> AllPrimaryKeys = new List<string>();
            string ThisOne = "";
            for (int i = 0; i < DataToLoad.Rows.Count; i++)
            {
                for (int x = 0; x < TableSchema.Columns.Count; x++)
                {
                    if (DataToLoad.Rows[i][TableSchema.Columns[x].ColumnName].ToString() == "NULL")
                    {
                        DataToLoad.Rows[i][TableSchema.Columns[x].ColumnName] = DBNull.Value;
                    }
                    //get whether nullable or not for possible error message
                    string IsNullable = "";
                    if (TableSchema.Columns[x].AllowDBNull)
                    {
                        IsNullable = "nullable";
                    }
                    else
                    {
                        IsNullable = "non-nullable";
                    }

                    //check for nulls in non-nullable fields
                    if (TableSchema.Columns[x].AllowDBNull == false && DataToLoad.Rows[i][TableSchema.Columns[x].ColumnName] == DBNull.Value)
                    {
                        throw new Exception("Error: Data type mismatch on row " + (i + 2) + ", column " + TableSchema.Columns[x].ColumnName + ", value " + DataToLoad.Rows[i][TableSchema.Columns[x].ColumnName] + " . Should be a " + TableSchema.Columns[x].DataType + " " + IsNullable);
                    }

                    //try to convert to what type it's supposed to be
                    try
                    {
                        Convert.ChangeType(DataToLoad.Rows[i][TableSchema.Columns[x].ColumnName], TableSchema.Columns[x].DataType);
                    }
                    //convert didn't work, possibly because column allows nulls and it was a null
                    //if it's not that, thrown an exception
                    //if column is numeric, nullable, and value is blank or spaces, turn it into a null
                    catch
                    {
                        var value = DataToLoad.Rows[i][TableSchema.Columns[x].ColumnName];
                        if (TableSchema.Columns[x].AllowDBNull == true && value == DBNull.Value)
                        {
                        }
                        if (TableSchema.Columns[x].AllowDBNull == true && value.ToString().Trim() == "" && IsNumeric(TableSchema.Columns[x].DataType))
                        {
                            DataToLoad.Rows[i][TableSchema.Columns[x].ColumnName] = DBNull.Value;
                        }
                        else
                        {
                            throw new Exception("Error: Data type mismatch on row " + (i + 2) + ", column " + TableSchema.Columns[x].ColumnName + ", value " + DataToLoad.Rows[i][TableSchema.Columns[x].ColumnName] + " . Should be a " + TableSchema.Columns[x].DataType + " " + IsNullable);
                        }
                    }

                    //validate string isn't too long
                    if (TableSchema.Columns[x].DataType == typeof(String))
                    {
                        
                        if(DataToLoad.Rows[i][TableSchema.Columns[x].ColumnName] == DBNull.Value)
                        {
                            DataToLoad.Rows[i][TableSchema.Columns[x].ColumnName] = "";
                        }
                        
                        String AsString = (String)DataToLoad.Rows[i][TableSchema.Columns[x].ColumnName];
                        if (AsString.Length > TableSchema.Columns[x].MaxLength)
                        {
                            throw new Exception("Error: String too long in row " + (i + 2) + ", column " + TableSchema.Columns[x].ColumnName + ". Your value: " + AsString + ". This field should be less than " + TableSchema.Columns[x].MaxLength + " characters.");
                        }
                    }
                }

                for (int j = 0; j < PrimaryKeys.Length; j++) //get all primary keys for each row, compress into string for comparison
                {
                    ThisOne += DataToLoad.Rows[i][PrimaryKeys[j].ColumnName] + "~";
                }

                int Count = AllPrimaryKeys.Where(x => x == ThisOne).Count(); //see if we have any matches

                if (Count > 0) //we do
                {
                    string ExceptionText = "You have a primary key violation on row " + (i + 2) + ". Columns";

                    foreach (DataColumn column in PrimaryKeys)
                    {
                        ExceptionText += " " + column.ColumnName + ",";
                    }
                    ExceptionText = ExceptionText.Remove(ExceptionText.Length - 1);
                    int Index = AllPrimaryKeys.IndexOf(ThisOne);
                    ExceptionText += " must be unique for each record. The primary key is already used on row " + (Index + 2) + ". Please fix this error and rerun.";
                    throw new Exception(ExceptionText);
                }
                AllPrimaryKeys.Add(ThisOne);
                ThisOne = "";
            }

            try
            {

                DataWork.DeleteAllRowsFromTable(TableName, Database);
                DataWork.SaveDataTableToDb(TableName, DataToLoad, Database);
            }
            catch (Exception e)
            {
                DataWork.DeleteAllRowsFromTable(TableName, Database);
                DataWork.SaveDataTableToDb(TableName, OldTableContents, Database);
                throw new Exception("Something went wrong. Data load has been cancelled and original data has been reloaded. Message below: " + e.Message + "\n" + e.InnerException);
            }

            string ExcelSheetName = TableName.Substring(0, Math.Min(22, TableName.Length));
            //ExcelWork.OutputDataTableToExcel(OldTableContents, ExcelSheetName, ProcLog.LoggerOutputYearDir + TableName + "\\" + TableName + DateTime.Now.ToString("yyyyMMdd-hhmm") + ".xlsx");

            // Clean archive directory
            String dir = ProcLog.LoggerOutputYearDir + TableName;

            System.IO.DirectoryInfo di = new DirectoryInfo(dir);

            if (di.GetFiles().Length > 12)
            {
                FileInfo[] files = di.GetFiles().OrderByDescending(p => p.CreationTime).ToArray();

                for (int i = files.Length - 1; i >= 12; i--)
                {
                    files[i].Delete();
                }
            }
        }

        public static DataTable RemoveLastRowIfEmpty(DataTable table)
        {
            DataRow lastRow = table.Rows[table.Rows.Count - 1];

            if (lastRow == null)
            {
                table.Rows.Remove(lastRow);
            }
            else
            {
                bool isEmpty = true;

                foreach (Object dataItem in lastRow.ItemArray)
                {
                    if (!String.IsNullOrWhiteSpace(dataItem.ToString()))
                    {
                        isEmpty = false;
                    }
                }

                if (isEmpty)
                {
                    table.Rows.Remove(lastRow);
                }
            }

            return table;
        }

        /// <summary>
        /// Write a DataTable object out to a StringBuilder object
        /// </summary>
        /// <param name="inboundDataTable"></param>
        /// <param name="delimiter"></param>
        /// <returns></returns>
        public static StringBuilder DataTableToString(DataTable inboundDataTable, string delimiter = "", bool fixedWidth = false, bool endLineWithDelimiter = true)
        {
            StringBuilder outBoundSB = new StringBuilder();

            if (fixedWidth)
            {
                int[] columnsWidths = new int[inboundDataTable.Columns.Count];

                // Get column widths
                foreach (DataRow row in inboundDataTable.Rows)
                {
                    for (int i = 0; i < inboundDataTable.Columns.Count; i++)
                    {
                        var length = row[i].ToString().Length;
                        if (columnsWidths[i] < length)
                            columnsWidths[i] = length;
                    }
                }

                // Write Rows
                foreach (DataRow row in inboundDataTable.Rows)
                {
                    for (int i = 0; i < inboundDataTable.Columns.Count; i++)
                    {
                        var text = row[i].ToString();
                        outBoundSB.Append(text.PadLeft(columnsWidths[i]));
                    }
                    outBoundSB.Append("\n");

                }
            }
            else
            {
                foreach (DataRow row in inboundDataTable.Rows)
                {
                    foreach (DataColumn column in inboundDataTable.Columns)
                    {
                        outBoundSB.Append(row[column.ColumnName].ToString() + delimiter);
                    }
                    if(!endLineWithDelimiter)
                    {
                        outBoundSB.Remove(outBoundSB.Length - 1, 1);
                    }
                    outBoundSB.Append("\n");
                }
            }
            return outBoundSB;
        }

        /// <summary>
        /// This method takes a generic IEnumerable of any object T and converts it to a data table with columns named as the variables in the object
        /// </summary>
        /// <typeparam name="T">The type of the object used in the enumerable passed in</typeparam>
        /// <param name="obj">The enumerable of objects to be converted</param>
        /// <param name="handleNullable">Converts nullable datatypes in the objects to not-nullables since DataTable can't handle nullables</param>
        /// <returns></returns>
        public static DataTable ObjectToDataTable<T>(IEnumerable<T> obj, bool handleNullable = false)
        {
            DataTable dt = new DataTable();
            foreach (PropertyInfo prop in typeof(T).GetProperties())
            {
                Type dataType = prop.PropertyType;
                if (handleNullable)
                {
                    dataType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                }
                dt.Columns.Add(prop.Name, dataType);
            }

            foreach (T item in obj)
            {
                PropertyInfo[] props = item.GetType().GetProperties();
                object[] row = new object[props.Length];
                for (int i = 0; i < props.Length; i++)
                {
                    var value = props[i].GetValue(item);
                    if (handleNullable)
                    {
                        value = props[i].GetValue(item) ?? DBNull.Value;
                    }
                    row[i] = value;
                }
                dt.Rows.Add(row);
            }
            return dt;
        }

        /// <summary>
        /// This method takes a generic IEnumerable of any object T and converts it to a data table with columns named as the variables in the object
        /// </summary>
        /// <typeparam name="T">The type of the object used in the enumerable passed in</typeparam>
        /// <param name="obj">The enumerable of objects to be converted</param>
        /// <param name="handleNullable">Converts nullable datatypes in the objects to not-nullables since DataTable can't handle nullables</param>
        /// <returns></returns>
        public static DataTable ObjectToDataTable<T,Z>(IEnumerable<T> obj, bool handleNullable = false)
        {
            DataTable dt = new DataTable();
            foreach (PropertyInfo prop in typeof(Z).GetProperties())
            {
                Type dataType = prop.PropertyType;
                if (handleNullable)
                {
                    dataType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                }
                dt.Columns.Add(prop.Name, dataType);
            }

            foreach (T item in obj)
            {
                PropertyInfo[] props = item.GetType().GetProperties();
                object[] row = new object[props.Length];
                for (int i = 0; i < props.Length; i++)
                {
                    var value = props[i].GetValue(item);
                    if (handleNullable)
                    {
                        value = props[i].GetValue(item) ?? DBNull.Value;
                    }
                    row[i] = value;
                }
                dt.Rows.Add(row);
            }
            return dt;
        }

        /// <summary>
        /// Takes a data table and maps the fields to an object. Supports nullable types.
        /// </summary>
        /// <typeparam name="T">The type of object to convert to</typeparam>
        /// <param name="table">A data table of data to map to an object</param>
        /// <param name="columnMapping">When provided, used to associate column names with object properties</param>
        /// <returns></returns>
        public static List<T> DataTableToObject<T>(DataTable table, Dictionary<string, string> columnMapping = null)
        {
            List<T> list = new List<T>();
            foreach (DataRow row in table.Rows)
            {
                T item = Activator.CreateInstance<T>();  //default(T);
                PropertyInfo[] props = typeof(T).GetProperties();
                TypeConverter converter = new TypeConverter();

                foreach (PropertyInfo prop in props)
                {
                    if (table.Columns.Contains(prop.Name))
                    {
                        if (row[prop.Name].GetType() == prop.PropertyType)
                        {
                            prop.SetValue(item, row[prop.Name]);
                        }
                        else if (row[prop.Name].GetType() == Nullable.GetUnderlyingType(prop.PropertyType))
                        {
                            prop.SetValue(item, row[prop.Name]);
                        }
                        else if (row[prop.Name].GetType() == typeof(DBNull))
                        {
                            prop.SetValue(item, null);
                        }
                        else
                        {
                            prop.SetValue(item, converter.ConvertTo(row[prop.Name], prop.PropertyType));
                        }
                    }
                    else if(columnMapping != null && columnMapping.Keys.Contains(prop.Name))
                    {
                        if (row[columnMapping[prop.Name]].GetType() == prop.PropertyType)
                        {
                            prop.SetValue(item, row[columnMapping[prop.Name]]);
                        }
                        else if (row[columnMapping[prop.Name]].GetType() == Nullable.GetUnderlyingType(prop.PropertyType))
                        {
                            prop.SetValue(item, row[columnMapping[prop.Name]]);
                        }
                        else if (row[columnMapping[prop.Name]].GetType() == typeof(DBNull))
                        {
                            prop.SetValue(item, null);
                        }
                        else
                        {
                            
                            prop.SetValue(item, converter.ConvertTo(row[columnMapping[prop.Name]], prop.PropertyType));
                        }
                    }
                }
                list.Add(item);
            }

            return list;
        }

        /// <summary>
        /// Takes a Data Table and formats an insert statment for SQL, table headers must match SQL fields
        /// Does not support byte[] or SqlBytes
        /// </summary>
        /// <param name="procLog">Logger</param>
        /// <param name="tableName">Table Name to insert into, can include schema and table if needed</param>
        /// <param name="dt">Data Table to convert into a SQL insert statement</param>
        /// <param name="from">If Provided, Ignores dt row values and instead leverages the From statement passed in to point to another table</param>
        /// <returns>SQL Insert Statement String, empty string if issue</returns>
        public static string DataTableToInsertQuery(Logger procLog, string tableName, DataTable dt, string selectFrom = "")
        {
            /*Column can be excluded from data table to insert IF IT
                Has an IDENTITY property. The next incremental identity value is used.
                Has a default. The default value for the column is used.
                Has a timestamp data type. The current timestamp value is used.
                Is nullable. A null value is used.
                Is a computed column. The calculated value is used.*/
            if(dt.Rows.Count == 0 && selectFrom == "")
            {
                procLog.WriteToLog("Not attempting insert no values provided", UniversalLogger.LogCategory.WARNING);
                return "";
            }
            procLog.WriteToLog($"Attempting to Insert {dt.Rows.Count} Rows into {tableName}.");

            string query = $"INSERT INTO {tableName}(";

            foreach (DataColumn col in dt.Columns) //Name the values
            {
                if(col != dt.Columns[0]) //comma before next value if not the first one
                {
                    query += ", ";
                }
                query += col.ColumnName;
            }
            if(selectFrom == "")
            {
                query += ")\n VALUES";

                foreach (DataRow row in dt.Rows) //If multiple rows of values loop through them
                {
                    if (row != dt.Rows[0])
                    {
                        query += ", ";
                    }
                    query += "(";
                    foreach (DataColumn col in dt.Columns) //Set your values
                    {
                        if (col != dt.Columns[0])
                        {
                            query += ", ";
                        }
                        query += $"'{row[col.ColumnName]}'";
                    }
                    query += ")";
                }
            }
            else
            {
                query += ")\n" + selectFrom;
            }

            return query;
        }

        /// <summary>
        /// Takes a Data Table and formats an insert statment into a SQL Table, table headers must match SQL fields, will complete insert if true
        /// </summary>
        /// <param name="procLog">Logger</param>
        /// <param name="tableName">Table Name to insert into, can include schema and table if needed</param>
        /// <param name="dt">Data Table to convert into a SQL insert statement</param>
        /// <param name="db">Which DB to input into, be sure to pass in test/prod</param>
        /// <returns>Number of rows inserted</returns>
        /// TODO could throw another utility out here that sandwhiches an output statement into the query and returns a data table
        public static int DataTableInsertToSQL(Logger procLog, Data.AppNames db, string tableName, DataTable dt)
        {
            /*Column can be excluded from data table to insert IF IT
                Has an IDENTITY property. The next incremental identity value is used.
                Has a default. The default value for the column is used.
                Has a timestamp data type. The current timestamp value is used.
                Is nullable. A null value is used.
                Is a computed column. The calculated value is used.*/
            string query = DataTableToInsertQuery(procLog, tableName, dt);
            int x = RunSqlCommandWithRecordCount(procLog, query, db);
            procLog.WriteToLog($"Inserted {x} rows");
            return x;
        }
        /// <summary>
        /// Checks the status of a SQL Server Agent job to see if it has started/completed within a specified timeout.
        /// </summary>
        /// <param name="procLog">Logger</param>
        /// <param name="db">Database to check for running job</param>
        /// <param name="jobName">SQL Server Agent job name to look for</param>
        /// <param name="timeout">timeout period in seconds. defaults to 600 seconds/10 minutes</param>
        /// <returns>True if job successfuly ran and False if job didn't launch or failed during run</returns>
        public static bool CheckJobStatus(Logger procLog, Data.AppNames db, string jobName, int timeout = 600)
        {
            // Grabs newest run information for specified job from specified DB
            DateTime DAStartTime = DateTime.Now.AddSeconds(-10);
            DataTable status = ExtractFactory.ConnectAndQuery(procLog, db, JobStatusQuery(jobName));

            // Checks to make sure SQL job launched after the DA job did 
            if (status.Rows.Count == 0 || ((DateTime)status.Rows[0]["run_requested_date"]) < DAStartTime)
            {
                procLog.WriteToLog("Job did not start. Check SSMS job activity monitor for details", UniversalLogger.LogCategory.ERROR);
                return false;
            }

            // Loop checks job status until error or success or timeout period is hit
            DateTime sqlStartTime = (DateTime)status.Rows[0]["run_requested_date"];
            procLog.WriteToLog($@"{jobName} started at {sqlStartTime}");
            while ((int)status.Rows[0]["Elapsed"] < timeout)
            {
                // If run status is 1, program completed successfully
                if (status.Rows[0]["run_status"].ToString() == "1")
                {
                    procLog.WriteToLog($@"{jobName} finished running at {status.Rows[0]["stop_execution_date"]}");
                    return true;
                }
                // If run status is not 1 and the program finished executing, something went wrong during execution.
                else if(!string.IsNullOrWhiteSpace(status.Rows[0]["stop_execution_date"].ToString()) && status.Rows[0]["run_status"].ToString() != "1")
                {
                    procLog.WriteToLog($@"{jobName} has errored out. Check the status in SSMS.", UniversalLogger.LogCategory.ERROR);
                    return false;
                }
                System.Threading.Thread.Sleep(1000 * 60);
                status = ExtractFactory.ConnectAndQuery(procLog, db, JobStatusQuery(jobName));
            }
            
            // Only gets to this point if job doesn't complete within timeout period
            procLog.WriteToLog($@"{jobName} has been running for too long. Check the status in SSMS.", UniversalLogger.LogCategory.ERROR);
            return false;
        }

        private static string JobStatusQuery(string jobName)
        {
            return $@"SELECT TOP(1) 
                        job.name
                        ,activity.run_requested_date
                        ,activity.stop_execution_date
                        ,activity.last_executed_step_id
                        ,history.run_status
                        ,DATEDIFF( SECOND, activity.run_requested_date, GETDATE() ) as Elapsed 
                        FROM 
	                        msdb.dbo.sysjobs_view job 
                        INNER JOIN msdb.dbo.sysjobactivity activity 
	                        ON job.job_id = activity.job_id 
                        INNER JOIN msdb.dbo.syssessions sess 
	                        ON sess.session_id = activity.session_id 
                        INNER JOIN msdb.dbo.sysjobhistory history
	                        ON job.job_id = history.job_id
                        WHERE 
	                        job.name = '{jobName}'
                        ORDER BY
	                        activity.run_requested_date DESC";
        }


        /// <summary>
        /// This method returns whether the type passed in is any numeric type
        /// </summary>
        /// <param name="dataType">The type to check</param>
        /// <returns></returns>
        public static bool IsNumeric(Type dataType)
        {
            bool isNumeric = (dataType == typeof(int) ||
                              dataType == typeof(double) ||
                              dataType == typeof(long) ||
                              dataType == typeof(short) ||
                              dataType == typeof(float) ||
                              dataType == typeof(byte) ||
                              dataType == typeof(uint) ||
                              dataType == typeof(ushort) ||
                              dataType == typeof(ulong) ||
                              dataType == typeof(sbyte) ||
                              dataType == typeof(bool) ||
                              dataType == typeof(decimal) ||
                              dataType == typeof(DateTime));

            return isNumeric;
        }

        public static int GetCount(string query, Data.AppNames sourceDb)
        {
            string countQuery = "SELECT count(*) FROM (" + query + ") t";
            Data sourceCreds = new Data(sourceDb);
            OdbcConnection odbcConn = sourceCreds.GetOdbcConnection();
            OdbcCommand cmd = new OdbcCommand(countQuery);
            cmd.Connection = odbcConn;
            cmd.CommandTimeout = 0;
            odbcConn.Open();
            int count = (int)cmd.ExecuteScalar();
            odbcConn.Close();
            return count;
        }
    }

    /// <summary>
    /// This class and its descendents are useful for making easy to use objects representing a row in
    /// a DB without the overhead of working with DataTables. To use it, subclass it and add public
    /// properties matching your table. Users of your object can instantiate it, fill the properties
    /// relevant to their usecase, and pass it to one of the DataWork methods that supports it.
    /// 
    /// All properties of value types (like DateTime, int, etc) should be defined as nullable, otherwise
    /// their default values will be sent to the DB even if the user does not specify them.
    /// 
    /// Example implementation: MRDRecord in MRDClient
    /// Example usage: DA03772
    /// </summary>
    public class DbRecordBase
    {
        public IList<string> GetSpecifiedFieldNames()
        {
            return GetSpecifiedFields().Select(f => f.Name).ToList();
        }

        public IList<FieldInfo> GetSpecifiedFields()
        {
            //https://stackoverflow.com/a/4144817/432690
            Type inputType = this.GetType();
            IList<FieldInfo> fields = new List<FieldInfo>(inputType.GetFields().Where(x => x.GetValue(this) != null));

            return fields;
        }

        /// <summary>
        /// Returns all specified data as a key-value pair of the column name and data for that column
        /// </summary>
        /// <returns></returns>
        public IList<KeyValuePair<string, object>> GetDataAsKVP() //If I knew how to extend this to just be a dictionary I would
        {
            IList<FieldInfo> foo = this.GetSpecifiedFields();
            List<KeyValuePair<string, object>> response = new List<KeyValuePair<string, object>>(foo.Count);

            foreach (FieldInfo field in foo)
            {
                response.Add(new KeyValuePair<string, object>(field.Name, field.GetValue(this)));
            }

            return response;
        }
    }
}
