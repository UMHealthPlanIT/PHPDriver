using Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;

namespace DataStationApi.Services
{
    public class Validators
    {
        public static string ValidateParameters(string pDbName, string pSpName, string pSchema, Dictionary<string, string> pValidateSpParams)
        {
            bool missingDB = pDbName is null ? true : false;
            bool missingSP = pSpName is null ? true : false;

            if (missingDB && missingSP)
            {
                return "Missing message parameters. DatabaseName and StoredProcedureName required in body.";
            }
            else
            {
                if (missingDB)
                {
                    return "DatabaseName required in body.";
                }
                else if (missingSP)
                {
                    return "StoredProcedureName required in body.";
                }
            }

            if (!Enum.IsDefined(typeof(Data.AppNames), pDbName))
            {
                return $"DatabaseName {pDbName} does not exist in Data.AppNames Enum.";
            }

            Data.AppNames dataSource = (Data.AppNames)Enum.Parse(typeof(Data.AppNames), pDbName);
            string spQuery = $@"
                SELECT 1
                    FROM sys.objects AS SO
                    where SO.name = '{pSpName}'
                    and SCHEMA_NAME(SCHEMA_ID) = '{pSchema}'
                ";

            if (ExtractFactory.ConnectAndQuery(dataSource, spQuery).Rows.Count == 0)
            {
                return $"StoredProcedureName {pSpName} does not exist in Schema {pSchema} and DatabaseName {pDbName}.";
            }

            string checkParametersQuery = $@"
                    select replace(P.name,'@','') AS [ParameterName]
                    from sys.objects as SO
                    inner join sys.parameters as P
                    on SO.OBJECT_ID = P.OBJECT_ID
                    where SO.name = '{pSpName}'
                    and SCHEMA_NAME(SCHEMA_ID) = '{pSchema}'
                ";

            DataTable checkParameters = ExtractFactory.ConnectAndQuery(dataSource, checkParametersQuery);

            if (checkParameters.Rows.Count == 0)
            {
                if (pValidateSpParams != null && pValidateSpParams.Count() > 0)
                {
                    return $"StoredProcedureName {pSpName} does not have any parameters, but parameters were passed in.";
                }
            }
            else
            {
                if (pValidateSpParams is null || pValidateSpParams.Count() == 0)
                {
                    return $"StoredProcedureName {pSpName} requires parameters, but none were passed in.";
                }
                else
                {
                    int i = 0;
                    string msgParamList = "";

                    foreach (DataRow row in checkParameters.Rows)
                    {
                        string paramName = row["ParameterName"].ToString();
                        bool paramExists = pValidateSpParams.ContainsKey(paramName);
                        if (paramExists)
                        {
                            pValidateSpParams.Remove(paramName);
                        }
                        else
                        {
                            string a = i > 0 ? ", " : $"The required parameter(s) for StoredProcedureName {pSpName} were not passed in: ";
                            msgParamList = msgParamList + a + "paramName";
                            i++;
                        }
                    }

                    if (i > 0)
                    {
                        msgParamList = msgParamList + ". ";
                        i = 0;
                    }

                    if (pValidateSpParams.Count > 0)
                    {
                        foreach (KeyValuePair<string, string> item in pValidateSpParams)
                        {
                            {
                                string a = i > 0 ? ", " : $"The following parameter(s) are passed in but not used in StoredProcedureName {pSpName}: ";
                                msgParamList = msgParamList + a + item.Key;
                                i++;
                            }
                        }
                    }

                    if (i > 0)
                    {
                        msgParamList = msgParamList + ".";
                    }
                }
            }

            return "";
        }
    }
}