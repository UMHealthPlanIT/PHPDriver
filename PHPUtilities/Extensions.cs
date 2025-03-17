using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Reflection;
using System.Globalization;
using System.Data.SqlTypes;
using System.Diagnostics;

namespace Utilities
{
    public static class Extensions
    {
        /// <summary>
        /// Extends String with a truncation method to truncate the string at the specified character 
        /// </summary>
        /// <param name="value">Target String, generaly 'this'</param>
        /// <param name="maxLength">Maximum character length for the string</param>
        /// <returns>Truncated string</returns>
        public static string Truncate(this String value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }


        /// <summary>
        /// Extends string with the ability to convert from "overpunched" values to string representations of decimals (including the decimal).
        /// An 'overpunched' number ends with a letter, which delineates the sign of the numerical value represented
        /// and the right-most character in the field. As an example, +99.95 == 00999E, whereas -99.95 == 00999N
        /// </summary>
        /// <param name="raw">Original overpunched value with 9 chars</param>
        /// <returns>String represented decimal without leading zeroes</returns>
        public static string TranslateOverPunched(this String raw)
        {

            int CharsBeforeDecimal;
            if (raw.Length == 9)
            {
                CharsBeforeDecimal = 8;
            }
            else if (raw.Length == 8)
            {
                CharsBeforeDecimal = 6;
            }
            else if (raw.Length == 10)
            {
                CharsBeforeDecimal = 7;
            }
            else if (raw.Length == 3)
            {
                CharsBeforeDecimal = 0;
            }
            else if (raw.Length == 13)
            {
                CharsBeforeDecimal = 8;
            }
            else if (raw.Length == 11)
            {
                CharsBeforeDecimal = 9;
            }
            else
            {
                throw new Exception("This string length is not supported for overpunch conversion");
            }

            String goodSegment;
            if (CharsBeforeDecimal > 0)
            {
                goodSegment = (raw.Substring(0, CharsBeforeDecimal + 1).Insert(CharsBeforeDecimal, ".") + raw.Substring(CharsBeforeDecimal + 1, raw.Length - (CharsBeforeDecimal + 1))).TrimStart('0');

                if (goodSegment == ".0")
                {
                    goodSegment = "0.0";
                }
            }
            else
            {
                goodSegment = raw.TrimStart('0');
            }

            String lastChar = goodSegment.Substring(goodSegment.Length - 1, 1);

            int Num;
            if (Int32.TryParse(lastChar, out Num))
            {
                return goodSegment;
            }
            else
            {
                String StringMinusLastChar = goodSegment.Substring(0, goodSegment.Length - 1);
                switch (lastChar)
                {
                    case "{":
                        return StringMinusLastChar + "0";
                    case "}":
                        return "-" + StringMinusLastChar + "0";
                    case "A":
                        return StringMinusLastChar + "1";
                    case "B":
                        return StringMinusLastChar + "2";
                    case "C":
                        return StringMinusLastChar + "3";
                    case "D":
                        return StringMinusLastChar + "4";
                    case "E":
                        return StringMinusLastChar + "5";
                    case "F":
                        return StringMinusLastChar + "6";
                    case "G":
                        return StringMinusLastChar + "7";
                    case "H":
                        return StringMinusLastChar + "8";
                    case "I":
                        return StringMinusLastChar + "9";
                    case "J":
                        return "-" + StringMinusLastChar + "1";
                    case "K":
                        return "-" + StringMinusLastChar + "2";
                    case "L":
                        return "-" + StringMinusLastChar + "3";
                    case "M":
                        return "-" + StringMinusLastChar + "4";
                    case "N":
                        return "-" + StringMinusLastChar + "5";
                    case "O":
                        return "-" + StringMinusLastChar + "6";
                    case "P":
                        return "-" + StringMinusLastChar + "7";
                    case "Q":
                        return "-" + StringMinusLastChar + "8";
                    case "R":
                        return "-" + StringMinusLastChar + "9";
                    default:
                        throw new Exception("Could not translate last char of amount field");
                }
            }


        }

        public static string TranslateShadowOverPunched(this String raw)
        {

            int CharsBeforeDecimal;
            if (raw.Length == 9)
            {
                CharsBeforeDecimal = 7;
            }
            else if (raw.Length == 8)
            {
                CharsBeforeDecimal = 6;
            }
            else if (raw.Length == 10)
            {
                CharsBeforeDecimal = 7;
            }
            else if (raw.Length == 3)
            {
                CharsBeforeDecimal = 0;
            }
            else if (raw.Length == 13)
            {
                CharsBeforeDecimal = 8;
            }
            else if (raw.Length == 11)
            {
                CharsBeforeDecimal = 9;
            }
            else
            {
                throw new Exception("This string length is not supported for overpunch conversion");
            }

            String goodSegment;
            if (CharsBeforeDecimal > 0)
            {
                goodSegment = (raw.Substring(0, CharsBeforeDecimal + 1).Insert(CharsBeforeDecimal, ".") + raw.Substring(CharsBeforeDecimal + 1, raw.Length - (CharsBeforeDecimal + 1))).TrimStart('0');

                if (goodSegment == ".0")
                {
                    goodSegment = "0.0";
                }
            }
            else
            {
                goodSegment = raw.TrimStart('0');
            }

            String lastChar = goodSegment.Substring(goodSegment.Length - 1, 1);

            int Num;
            if (Int32.TryParse(lastChar, out Num))
            {
                return goodSegment;
            }
            else
            {
                String StringMinusLastChar = goodSegment.Substring(0, goodSegment.Length - 1);
                switch (lastChar)
                {
                    case "{":
                        return StringMinusLastChar + "0";
                    case "}":
                        return "-" + StringMinusLastChar + "0";
                    case "A":
                        return StringMinusLastChar + "1";
                    case "B":
                        return StringMinusLastChar + "2";
                    case "C":
                        return StringMinusLastChar + "3";
                    case "D":
                        return StringMinusLastChar + "4";
                    case "E":
                        return StringMinusLastChar + "5";
                    case "F":
                        return StringMinusLastChar + "6";
                    case "G":
                        return StringMinusLastChar + "7";
                    case "H":
                        return StringMinusLastChar + "8";
                    case "I":
                        return StringMinusLastChar + "9";
                    case "J":
                        return "-" + StringMinusLastChar + "1";
                    case "K":
                        return "-" + StringMinusLastChar + "2";
                    case "L":
                        return "-" + StringMinusLastChar + "3";
                    case "M":
                        return "-" + StringMinusLastChar + "4";
                    case "N":
                        return "-" + StringMinusLastChar + "5";
                    case "O":
                        return "-" + StringMinusLastChar + "6";
                    case "P":
                        return "-" + StringMinusLastChar + "7";
                    case "Q":
                        return "-" + StringMinusLastChar + "8";
                    case "R":
                        return "-" + StringMinusLastChar + "9";
                    default:
                        throw new Exception("Could not translate last char of amount field");
                }
            }


        }

        /// <summary>
        /// Converts a datatable row to a readable formatted string.
        /// </summary>
        /// <param name="row">Source row to process</param>
        /// <param name="delimeter">The delimiter used to separate the output columns - "; " by default. </param>
        /// <param name="includeColNames">Whether to incude the column names in the list ie: colName1: colContent1; colName2: colContent2. vs colContent1; colContent2. Defaults to false</param>
        /// <returns>Formatted string with the contents of the row</returns>
        public static string ToContentString(this DataRow row, string delimeter = "; ", bool includeColNames = false)
        {
            try
            {
                string output = "";
                foreach (DataColumn col in row.Table.Columns)
                {
                    output += (includeColNames ? col.ColumnName + ": " : "") + row[col].ToString() + delimeter;
                }
                output = output.Substring(0, output.Length - delimeter.Length);

                return output;
            }
            catch
            {
                return "";
            }
        }


        /// <summary>
        /// Converts a datatable row to a readable formatted string.
        /// </summary>
        /// <param name="row">Source row to process</param>
        /// <param name="delimeter">The delimiter used to separate the output columns - "; " by default. </param>
        /// <param name="includeColNames">Whether to incude the column names in the list ie: colName1: colContent1; colName2: colContent2. vs colContent1; colContent2. Defaults to false</param>
        /// <returns>Formatted string with the contents of the row</returns>
        public static string ToDelimitedString<T>(this List<T> list, string delimeter = ", ")
        {
            try
            {
                string output = "";
                foreach (T row in list)
                {
                    output += row.ToString() + delimeter;
                }
                output = output.Substring(0, output.Length - delimeter.Length);

                return output;
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Converts a datatable row to a readable formatted string.
        /// </summary>
        /// <param name="row">Source row to process</param>
        /// <param name="delimeter">The delimiter used to separate the output columns - "; " by default. </param>
        /// <param name="includeColNames">Whether to incude the column names in the list ie: colName1: colContent1; colName2: colContent2. vs colContent1; colContent2. Defaults to false</param>
        /// <returns>Formatted string with the contents of the row</returns>
        public static string ToSelectString(this DataRow row, List<string> keys = null)
        {
            try
            {
                string output = "";
                foreach (DataColumn col in row.Table.Columns)
                {
                    if (keys == null || keys.Count == 0 || (keys != null && keys.Any(x => x.ToUpper() == col.ColumnName.ToUpper())))
                    {
                        if (string.IsNullOrWhiteSpace(row[col].ToString()))
                        {

                        }
                        if (!string.IsNullOrWhiteSpace(row[col].ToString()) || !(keys == null || keys.Count == 0))
                        {
                            if (!(keys.Count == 0 && col.DataType == typeof(DateTime)))
                            {
                                output += col.ColumnName + " = '" + row[col].ToString().Replace("'", "''") + "' AND ";
                            }
                        }
                    }
                }
                output = output.Substring(0, output.Length - 4);

                return output;
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Converts a DataTable to a typed-list. Thanks to https://stackoverflow.com/questions/11270999/how-can-i-map-the-results-of-a-sql-query-onto-objects for the code.
        /// </summary>
        /// <typeparam name="T">Type to convert each row to</typeparam>
        /// <param name="table">Source table to process</param>
        /// <returns>List of the specified type</returns>
        public static List<T> ToList<T>(this DataTable table) where T : class, new()
        {
            try
            {
                List<T> list = new List<T>();

                foreach (var row in table.AsEnumerable())
                {
                    T obj = new T();

                    foreach (var prop in obj.GetType().GetProperties())
                    {
                        try
                        {
                            PropertyInfo propertyInfo = obj.GetType().GetProperty(prop.Name);
                            propertyInfo.SetValue(obj, Convert.ChangeType(row[prop.Name], propertyInfo.PropertyType), null);
                        }
                        catch
                        {
                            continue;
                        }
                    }

                    list.Add(obj);
                }

                return list;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Converts a DataTable to a typed-list. Uses a passed in dictionary to map columns to properties. Only tries to assign if the column exists in th map.
        /// </summary>
        /// <typeparam name="T">Type to convert each row to</typeparam>
        /// <param name="table">Source table to process</param>
        /// <returns>List of the specified type</returns>
        public static List<T> ToList<T>(this DataTable table, Dictionary<string, string> colMap) where T : class, new()
        {
            try
            {
                List<T> list = new List<T>();

                foreach (var row in table.AsEnumerable())
                {
                    T obj = new T();

                    foreach (var prop in obj.GetType().GetProperties())
                    {
                        if (colMap.ContainsKey(prop.Name))
                        {
                            try
                            {
                                PropertyInfo propertyInfo = obj.GetType().GetProperty(prop.Name);
                                if((propertyInfo.PropertyType == typeof(DateTime) || row[colMap[prop.Name]].GetType() == typeof(DBNull)) && row[colMap[prop.Name]].ToString() == "")
                                {
                                    propertyInfo.SetValue(obj, DateTime.MinValue, null);
                                }
                                else
                                {
                                    propertyInfo.SetValue(obj, Convert.ChangeType(row[colMap[prop.Name]], propertyInfo.PropertyType), null);
                                }
                            }
                            catch (Exception E)
                            {
                                continue;
                            }
                        }
                    }

                    list.Add(obj);
                }

                return list;
            }
            catch
            {
                return null;
            }
        }

        public static T ToObject<T>(this DataRow dataRow)
 where T : new()
        {
            T item = new T();
            foreach (DataColumn column in dataRow.Table.Columns)
            {
                PropertyInfo property = item.GetType().GetProperty(column.ColumnName);

                if (property != null && dataRow[column] != DBNull.Value)
                {
                    object result = Convert.ChangeType(dataRow[column], property.PropertyType);
                    property.SetValue(item, result, null);
                }
            }

            return item;
        }


        /// <summary>
        /// Extends DataTable to attempt to convert each column to an int (or decimal if there's a ".") if all of the values in the column can convert
        /// </summary>
        /// <param name="origTable">DataTable to try to convert</param>
        /// <param name="colName">Optional parameter to just attempt to convert a specific column</param>
        /// <param name="exceptColName">Optional parameter to exclude listed columns from conversion. Useful when there are columns you know could convert, but you don't want them to.</param>
        /// <returns>DataTable with all columns converted if possible</returns>
        public static DataTable ConvertToNumeric(this DataTable origTable, string colName = null, List<string> exceptColName = null)
        {
            DataTable newTable = new DataTable();
            try
            {
                foreach (DataColumn col in origTable.Columns)
                {
                    //skip column if it's already a decimal or int
                    if (DataWork.IsNumeric(col.DataType) || (exceptColName != null && exceptColName.Any(x => x == col.ColumnName)))
                    {
                        newTable.Columns.Add(col.ColumnName, col.DataType);
                        continue;
                    }

                    if (colName == null || col.ColumnName == colName)
                    {
                        bool canConvert = true;
                        bool isDecimal = false;

                        if (origTable.AsEnumerable().All(x => string.IsNullOrWhiteSpace(x[col.ColumnName].ToString())))
                        {
                            canConvert = false;
                        }
                        else
                        {
                            foreach (DataRow row in origTable.Rows)
                            {
                                string value = row[col.ColumnName].ToString();
                                if (!string.IsNullOrWhiteSpace(value))
                                {
                                    if (decimal.TryParse(value, out decimal temp) == false)
                                    {
                                        canConvert = false;
                                        break;
                                    }
                                    else if (isDecimal == false && value.Contains("."))
                                    {
                                        isDecimal = true;
                                    }
                                }
                            }
                        }

                        if (canConvert == false)
                        {
                            newTable.Columns.Add(col.ColumnName, col.DataType);
                        }
                        else if (isDecimal)
                        {
                            newTable.Columns.Add(col.ColumnName, typeof(decimal));
                        }
                        else
                        {
                            newTable.Columns.Add(col.ColumnName, typeof(long));
                        }
                    }
                    else
                    {
                        newTable.Columns.Add(col.ColumnName, col.DataType);
                    }
                }

                foreach (DataRow row in origTable.Rows)
                {
                    newTable.ImportRow(row);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return newTable;
        }
        public static Dictionary<TKey, TValue> AddRange<TKey, TValue>(this Dictionary<TKey, TValue> value, IEnumerable<KeyValuePair<TKey, TValue>> range)
        {
            foreach(KeyValuePair<TKey, TValue> pair in range)
            {
                value.Add(pair.Key, pair.Value);
            }
            return value;
        }
        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> value)
        {
            Dictionary<TKey, TValue> outDic = new Dictionary<TKey, TValue>();
            foreach (KeyValuePair<TKey, TValue> pair in value)
            {
                outDic.Add(pair.Key, pair.Value);
            }
            return outDic;
        }

        public static string GetEnumStringAttribute<T>(this Enum enumValue, string attributeString)
        {
            FieldInfo field = enumValue.GetType().GetField(enumValue.ToString());
            if (Attribute.GetCustomAttribute(field, typeof(T)) is T attribute)
            {
                return (string)attribute.GetType().GetProperty(attributeString).GetValue(attribute);
            }
            throw new ArgumentException("Item not found.", nameof(enumValue));
        }

        public static string Left(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            maxLength = Math.Abs(maxLength);

            return (value.Length <= maxLength
                   ? value
                   : value.Substring(0, maxLength)
                   );
        }

        public static string Right(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            maxLength = Math.Abs(maxLength);

            if(value.Length <= maxLength)
            {
                return value;
            }
            else
            {
                value = new string(value.Reverse().ToArray());
                value = value.Substring(0, maxLength);
                return new string(value.Reverse().ToArray());
            }
        }

        public static IEnumerable<DataTable> AsEnumerable(this DataSet data)
        {
            if (data.Tables.Count == 0)
            {
                throw new ArgumentOutOfRangeException("DataSet cannot be empty");
            }

            DataTable[] dataTables = new DataTable[data.Tables.Count];
            /*for(int i = 0; i < data.Tables.Count; i++)
            {
                dataTables[i] = data.Tables[i];
            }*/

            data.Tables.CopyTo(dataTables, 0);
            return dataTables;
        }

        /// <summary>
        /// Merges two DataTables by ordinal position (ignoring column naming), or by a map (for merging dissimilar tables)
        /// </summary>
        /// <param name="table1">The table to merge into</param>
        /// <param name="table2">The table to merge from</param>
        /// <param name="colMap">A column name mapping between the two tables, to use when merging tables that don't have identical schemas</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Throws an exception when the column counts between the two tables are different, unless colMap is populated</exception>
        public static DataTable CustomMerge(this DataTable table1, DataTable table2, Dictionary<string, string> colMap = null)
        {
            if (table1.Columns.Count != table2.Columns.Count && colMap == null)
            {
                throw new ArgumentException("Table Column Counts do not match. Either correct the column counts or populate a mapping dictionary");
            }
            foreach (DataRow row in table2.Rows)
            {
                DataRow addRow = table1.NewRow();
                if (colMap == null)
                {
                    for (int x = 0; x < table1.Columns.Count; x++)
                    {
                        addRow[x] = row[x];
                    }
                    try
                    {
                        table1.Rows.Add(addRow); //And then we try to add it to the destination again for some reason? NewRow() on :590 doesn't add it, but why do we try multiple times? Does :596 actually modify the data in the destination row?
                    }
                    catch (Exception E)
                    {
                        if (E.Message != "This row already belongs to this table.") //Code written in meeting TODO check if this is necessary
                        {
                            throw E;
                        }
                        continue; // Duplicate row
                    }
                }
                else
                {
                    foreach (KeyValuePair<string, string> map in colMap)
                    {
                        addRow[map.Key] = row[map.Value];
                    }
                    table1.Rows.Add(addRow);
                }
            }
            return table1;
        }

        /// <summary>
        /// Merges two columns from within a DataTable in the form col1 + separator + col2
        /// </summary>
        /// <param name="table1">The table being worked upon</param>
        /// <param name="col1">The column to be kept</param>
        /// <param name="col2">The column to be deleted</param>
        /// <param name="separator">optional param to insert a separator between the two</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Throws an exception when the column counts between the two tables are different, unless colMap is populated</exception>
        public static DataTable Merge(this DataTable table1, DataColumn col1, DataColumn col2, string separator = "", bool trimSpaces = false)
        {
            if (trimSpaces)
            {
                foreach (DataRow row in table1.Rows)
                {
                    row[col1] = row[col1].ToString().Trim() + separator + row[col2].ToString().Trim();
                }
            }
            else
            {
                foreach (DataRow row in table1.Rows)
                {
                    row[col1] = row[col1] + separator + row[col2];
                }
            }

            table1.Columns.Remove(col2);
            table1.AcceptChanges();

            return table1;
        }

        public static DataTable ChangeDataType(this DataTable table, DataColumn col, Type newType, string format = "")
        {
            table.Columns.Add(col.ColumnName + "2", newType);
            table.Columns[col.ColumnName + "2"].SetOrdinal(table.Columns[col.ColumnName].Ordinal);


            foreach (DataRow row in table.Rows)
            {
                if (newType == typeof(DateTime))
                {
                    try
                    {
                        row[col.ColumnName + "2"] = DateTime.ParseExact(row[col.ColumnName].ToString().Trim(), format, CultureInfo.InvariantCulture);
                    }
                    catch (Exception E)
                    {
                        row[col.ColumnName + "2"] = DBNull.Value;
                    }
                }
                else if (newType == typeof(SqlMoney))
                {
                    try
                    {
                        row[col.ColumnName + "2"] = SqlMoney.Parse(row[col.ColumnName].ToString());
                    }
                    catch (Exception E)
                    {
                        row[col.ColumnName + "2"] = DBNull.Value;
                    }
                }
                else
                {
                    try
                    {
                        if (string.IsNullOrEmpty(row[col.ColumnName].ToString()))
                        {
                            row[col.ColumnName + "2"] = DBNull.Value;
                        }
                        else
                            row[col.ColumnName + "2"] = Convert.ChangeType(row[col.ColumnName].ToString(), Type.GetTypeCode(newType));
                    }
                    catch (Exception E)
                    {
                        row[col.ColumnName + "2"] = DBNull.Value;
                    }
                }
            }

            table.Columns.Remove(col);
            table.Columns[col.ColumnName + "2"].ColumnName = col.ColumnName;
            table.AcceptChanges();

            return table;
        }
    }

    [AttributeUsage(AttributeTargets.All)]
    public class DoNotWrite : Attribute
    {

    }
}
