using DataStationApi.Models;
using Newtonsoft.Json;
using Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Diagnostics;

namespace DataStationApi.Services
{
    public class ULoggerService
    {
        private readonly Data.AppNames Database;
        private readonly String Table;

        public ULoggerService(String database, String table)
        {
            Enum.TryParse(database, out Database);
            Table = table;
        }

        public void CreateNewLogRecord(ULogEntryModel logEntry)
        {
            String sqlInsert = String.Format(@"INSERT INTO [ULogger].[{0}] (JobIndex, LogDateTime, LogCategory, LoggedByUser, LogContent, UID, Remediated)
                                                VALUES(@param1, @param2, @param3, @param4, @param5, @param6, 0);", Table);

            List<SqlParameter> insertParams = new List<SqlParameter>
            {
                new SqlParameter("param1", logEntry.JobIndex.ToUpper()),
                new SqlParameter("param2", logEntry.LogDateTime),
                new SqlParameter("param3", logEntry.LogCategory.ToUpper()),
                new SqlParameter("param4", logEntry.LoggedByUser.ToUpper()),
                new SqlParameter("param5", logEntry.LogContent),
                new SqlParameter("param6", logEntry.UID ?? "")
            };
            try
            {
                DataWork.RunSqlCommand(sqlInsert, Database, insertParams);
            }catch(Exception e)
            {
                //nom nom nom eat the Exception and send it to the queue to try again later but let the client app keep running
                UloggerQueue.AddToQueue(logEntry, this);
                
                
            }

        }

        public void UpdateLogRemediation(ULogEntryModel logEntry)
        {
            String sqlUpdate = String.Format(@"UPDATE [ULOGGER].[{0}] SET Remediated = @param1, RemediationNote = @param2 WHERE JobIndex = @param3 AND LogDateTime = @param4", Table);

            List<SqlParameter> updateParams = new List<SqlParameter>
            {
                new SqlParameter("param1", logEntry.Remediated),
                new SqlParameter("param2", logEntry.RemediationNote),
                new SqlParameter("param3", logEntry.JobIndex),
                new SqlParameter("param4", logEntry.LogDateTime)
            };

            DataWork.RunSqlCommand(sqlUpdate, Database, updateParams);
        }

        public HttpResponseMessage GetLogsByDate(DateTime date)
        {
            String sql = String.Format(@"SELECT * FROM [ULogger].[{0}] with (nolock) WHERE CONVERT(date, LogDateTime) = CONVERT(date, @param1) ORDER BY LogDateTime DESC", Table);

            List<SqlParameter> sqlParams = new List<SqlParameter>
            {
                new SqlParameter("param1", date.ToString("yyyyMMdd"))
            };

            return ExecuteSqlAndBuildResponseMessage(sql, sqlParams);
        }

        public HttpResponseMessage GetLogsByIndex(String index)
        {
            String sql = String.Format(@"SELECT * FROM [ULogger].[{0}] with (nolock) WHERE JobIndex = @param1 ORDER BY LogDateTime DESC", Table);

            List<SqlParameter> sqlParams = new List<SqlParameter>
            {
                new SqlParameter("param1", index.ToUpper())
            };

            return ExecuteSqlAndBuildResponseMessage(sql, sqlParams);
        }

        public HttpResponseMessage GetNumLogsByIndex(int numLogs, String index)
        {
            String sql = String.Format(@"SELECT TOP @param1 * FROM [ULogger].[{0}] with (nolock) WHERE JobIndex = @param2 ORDER BY LogDateTime DESC", Table);

            List<SqlParameter> sqlParams = new List<SqlParameter>
            {
                new SqlParameter("param1", numLogs),
                new SqlParameter("param2", index.ToUpper())
            };

            return ExecuteSqlAndBuildResponseMessage(sql, sqlParams);
        }

        /// <summary>
        /// Query the LoggerRecord table for the first record where the JobIndex is "CONTROLLER".
        /// </summary>
        /// <returns></returns>
        public bool CheckForSignsOfLife()
        {
            bool lifeSigns = false;

            string sql = @"SELECT TOP 1 * FROM [ULogger].[LoggerRecord] with (nolock) WHERE JobIndex = 'CONTROLLER'";
            DataTable records = ExtractFactory.ConnectAndQuery(Database, sql, timeout:2);
            if(records.Rows.Count > 0)
            {
                if(records.Rows[0]["JobIndex"].ToString() != null)
                {
                    lifeSigns = true;
                }
            }

            return lifeSigns;
        }


        // Don't put the List<ULogEntryModel> in here...just in case we build another model type for a different log in the future.
        // We can just keep this as abstract as possible for now.
        private HttpResponseMessage ExecuteSqlAndBuildResponseMessage(String sql, List<SqlParameter> sqlParams)
        {
            DataTable records = ExtractFactory.ConnectAndQuery(Database, sql, sqlParams);
            
            String jsonContent = JsonConvert.SerializeObject(records, Formatting.Indented);

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            return response;
        }
    }
}