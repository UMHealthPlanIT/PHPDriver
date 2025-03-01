using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data;
using Utilities;

namespace JobConfiguration.Models
{
    public class FoundTableDetails
    {
        public string TableName { get; set; }
        public string TableSchema { get; set; }
        public string TableDescription { get; set; }
        public string SupportingInformation { get; set; }
        public bool ReadCheck { get; set; }
        public DataTable TableData { get; set; }
        public List<ColumnDetails> TableColumns { get; set; }
        private Data.AppNames dataSource;
        public List<String> PrepopulatedFields { get; set; }
        public List<KeyValuePair<String, String>> FieldValues { get; set; }
        public String IdColumn { get; set; }
        public bool bulkUpdateAllowed { get; set; }

        public FoundTableDetails(String tableToUpdate, Data.AppNames datSource)
        {

            TableName = tableToUpdate;

            dataSource = datSource;

            bulkUpdateAllowed = false;

            //TableColumns = GetColumnDetails(tableToUpdate);

            if (tableToUpdate != "Job Configuration")
            {
                TableSchema = ExtractFactory.ConnectAndQuery<String>(dataSource, String.Format(@"SELECT TABLE_SCHEMA FROM information_schema.tables WHERE TABLE_TYPE='BASE TABLE' and TABLE_NAME = '{0}'", tableToUpdate)).First();

                try
                {
                    this.PrepopulatedFields = ExtractFactory.ConnectAndQuery<String>(dataSource, String.Format(@"SELECT LTRIM(RTRIM(FIELDNAME)) FROM dbo.ConfigurationDropDownValues_C WHERE LTRIM(RTRIM(TABLENAME)) = '{0}'", tableToUpdate)).ToList<String>();
                }
                catch (NullReferenceException ex)
                {
                    //No records found
                }

                List<KeyValuePair<string, string>> tempValues = new List<KeyValuePair<string, string>>();

                foreach (String fieldName in this.PrepopulatedFields)
                {
                    List<String> values = ExtractFactory.ConnectAndQuery<String>(dataSource, String.Format(@"SELECT LTRIM(RTRIM(ITEM)) FROM dbo.ConfigurationDropDownValues_C WHERE LTRIM(RTRIM(TABLENAME)) = '{0}' AND LTRIM(RTRIM(FIELDNAME)) = '{1}'", tableToUpdate, fieldName)).ToList();

                    foreach (String value in values)
                    {
                        KeyValuePair<string, string> fValue = new KeyValuePair<string, string>(fieldName, value);
                        tempValues.Add(fValue);
                    }
                }

                this.FieldValues = tempValues.Distinct().ToList();
            }

            TableColumns = GetColumnDetails(tableToUpdate);

        }

        private List<ColumnDetails> GetColumnDetails(String tableToUpdate)
        {

            //todo: set up real calls in stored procedure
            String columnQry = String.Format(@"SELECT COLUMN_NAME as ColumnName, DATA_TYPE, coalesce(CHARACTER_MAXIMUM_LENGTH,'') as CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
                                            FROM INFORMATION_SCHEMA.COLUMNS
                                            WHERE TABLE_NAME = '{0}'", tableToUpdate);

            List<Models.ColumnDetails> Columns = ExtractFactory.ConnectAndQuery<Models.ColumnDetails>(dataSource, columnQry).ToList();

            String keyQry = String.Format(@"SELECT Col.Column_Name from 
                                            INFORMATION_SCHEMA.TABLE_CONSTRAINTS Tab, 
                                            INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE Col 
                                            WHERE 
                                            Col.Constraint_Name = Tab.Constraint_Name
                                            AND Col.Table_Name = Tab.Table_Name
                                            AND Constraint_Type = 'PRIMARY KEY'
                                            AND Col.Table_Name = '{0}'", tableToUpdate);

            List<string> PrimaryKeys = ExtractFactory.ConnectAndQuery<string>(dataSource, keyQry).ToList();

            String idColumnQry = String.Format(@"SELECT COLUMN_NAME   
                                                 FROM INFORMATION_SCHEMA.COLUMNS   
                                                 WHERE TABLE_NAME = '{0}'   
                                                 AND COLUMNPROPERTY(OBJECT_ID(TABLE_SCHEMA + '.' + TABLE_NAME), COLUMN_NAME, 'ISIDENTITY') = 1", tableToUpdate);


            List<String> idList = ExtractFactory.ConnectAndQuery<String>(dataSource, idColumnQry).ToList();
            String idCol = idList.Count > 0 ? idList[0] : "";

            foreach (Models.ColumnDetails column in Columns)
            {

                if (PrimaryKeys.Exists(x => x == column.ColumnName))
                {
                    column.PrimaryKey = true;
                }
                else
                {
                    column.PrimaryKey = false;
                }

                column.Values = this.FieldValues.Where(kvp => kvp.Key == column.ColumnName).Select(kvp => kvp.Value).ToList();  //from this.FieldValues where this.FieldValues.Key == column.ColumnName select this.FieldValues.Value;

                if (column.Values.Count > 0)
                {
                    column.SetList = true;
                }
                else
                {
                    column.SetList = false;
                }

                if (column.ColumnName.Equals(idCol))
                {
                    column.IdColumn = true;
                    this.IdColumn = column.ColumnName;
                }
                else
                {
                    column.IdColumn = false;
                }

            }

            return Columns;
        }
    }

    public class ColumnDetails
    {
        public string ColumnName { get; set; }
        public string DATA_TYPE { get; set; }
        public Int32 CHARACTER_MAXIMUM_LENGTH { get; set; }
        public string IS_NULLABLE { get; set; }
        public Boolean PrimaryKey { get; set; }
        public Boolean SetList { get; set; }
        public List<String> Values { get; set; }
        public Boolean IdColumn { get; set; }
    }


}