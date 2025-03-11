using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data;
using System.Data.SqlClient;
using Utilities;

namespace JobConfiguration.Models
{
    public class KeySelectorWork
    {
        public static string GetJSONSelector(DataRow row, List<String> primaryKeys)
        {
            String keySelector = "{ ";
            int Counter = 0;
            foreach (string col in primaryKeys)
            {
                if (Counter == 0)
                {
                    keySelector += "'" + col + "': '" + row[col].ToString() + "'";
                }
                else
                {
                    keySelector += ",'" + col + "': '" + row[col].ToString() + "'";
                }

                Counter++;

            }

            keySelector += " }";

            return keySelector;
        }

        public static Dictionary<object, object> GetRecordToEdit(String tableName, String keySelector, Data.AppNames dataSource, String schema)
        {

            String queryForItem = String.Format(@"select * from {0}.{1} where ", schema,tableName);

            List<SqlParameter> sParams;
            queryForItem += Models.SqlFactory.WhereClauseFromKey(keySelector, out sParams);

            DataRow foundRec = ExtractFactory.ConnectAndQuery(dataSource, queryForItem, sParams).Rows[0];

            Dictionary<object, object> result = new Dictionary<object, object>();

            foreach (DataColumn col in foundRec.Table.Columns)
            {
                result.Add(col.ColumnName, foundRec[col.ColumnName]);
            }

            return result;

        }

    }
}