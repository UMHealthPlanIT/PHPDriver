using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.SqlClient;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Utilities;
using System.Data;

namespace JobConfiguration.Models
{
    public class SqlFactory
    {

        public static List<SqlParameter> GetValuesFromFieldData(Models.TableUpdate tableUpdate, ref String createQuery, Models.FoundTableDetails tableSchema, bool updateQuery = false)
        {
            int recCounter = 0;

            List<SqlParameter> sqlParameters = new List<SqlParameter>();

            foreach (KeyValuePair<object, object> val in tableUpdate.PropertiesValues)
            {
                if (!string.Join("", (string[])val.Key).Equals(tableSchema.IdColumn))
                {
                    string valValue = string.Join("", (string[])val.Value);
                    string valField = "[" + string.Join("", (string[])val.Key) + "]";
                    String paramVar = "@param" + recCounter.ToString();
                    if (recCounter == 0)
                    {
                        if (updateQuery)
                        {
                            createQuery += valField + " = " + paramVar;
                        }
                        else
                        {
                            createQuery += paramVar;
                        }

                    }
                    else
                    {
                        if (updateQuery)
                        {
                            createQuery += ", " + valField + " = " + paramVar;
                        }
                        else
                        {
                            createQuery += ", " + paramVar;
                        }

                    }

                    String dataType = tableSchema.TableColumns.Where(x => x.ColumnName == valField.Replace("[", "").Replace("]", "")).First().DATA_TYPE;

                    if ((dataType.Contains("date") || dataType == "decimal" || dataType.Contains("int")) && valValue == "")
                    {
                        sqlParameters.Add(new SqlParameter(paramVar, DBNull.Value));
                    }
                    else
                    {
                        sqlParameters.Add(new SqlParameter(paramVar, valValue));
                    }


                    recCounter++;
                }
            }
            return sqlParameters;
        }

        public static string WhereClauseFromKey(String keySelector, out List<SqlParameter> sqlParams, int paramCounterStart = 0)
        {
            sqlParams = new List<SqlParameter>();

            JObject token = JObject.Parse(keySelector);
            String whereClause = "";
            int counter = paramCounterStart;
            foreach (var prop in token)
            {

                if (counter == paramCounterStart)
                {
                    whereClause += "[" + prop.Key + "] = @param" + counter.ToString() + " ";
                }
                else
                {
                    whereClause += ("and [" + prop.Key + "] = @param" + counter.ToString() + " ");
                }

                SqlParameter sparam = new SqlParameter("@param" + counter.ToString(), prop.Value.ToString());
                sqlParams.Add(sparam);

                counter++;
            }

            return whereClause;
        }

        public static bool RunSqlCommand(String query, Data.AppNames database, List<SqlParameter> sqlParameters)
        {
            Data dbConnection = new Data(database);

            using (SqlConnection conn = dbConnection.GetSqlConnection(dbConnection.Authentication))
            {
                SqlCommand cmd = new SqlCommand(query, conn);

                foreach (SqlParameter param in sqlParameters)
                {
                    cmd.Parameters.Add(param);
                }

                conn.Open();

                int rows = cmd.ExecuteNonQuery();

                conn.Close();
                conn.Dispose();

                if (rows == 1)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }            
        }

        public static string GetNextId()
        {
            Logger log = AbstractController.log;
            string id = "";

           string result = ExtractFactory.ConnectAndQuery<string>(log, Data.AppNames.ExampleProd, @"select top (1) JobId from [CONTROLLER].[JobIndex_C]
where jobid like 'DA%'
order by JobId desc").FirstOrDefault();
            if(result == null)
            {
                return "";
            }

            int number = int.Parse(result.Remove(0, 2));
            number++;

            id = $@"DA{number.ToString().PadLeft(5, '0')}";

            return id;
        }
    }
}