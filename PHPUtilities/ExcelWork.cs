using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Data;


namespace Utilities
{
    public static class ExcelWork
    {

        private static List<string> GetDateColumns(Type T)
        {
            List<string> cols = new List<string>();
            int colNum = 1;
            foreach (PropertyInfo prop in T.GetProperties())
            {
                if (prop.PropertyType == typeof(DateTime) || prop.PropertyType == typeof(DateTime?))
                {
                    cols.Add(GetColumnName(colNum));
                }
                colNum++;
            }

            return cols;
        }

        private static List<string> GetDateColumns(DataColumnCollection columns)
        {
            List<string> cols = new List<string>();
            int colNum = 1;
            foreach (DataColumn column in columns)
            {
                if (column.DataType == typeof(DateTime))
                {
                    cols.Add(GetColumnName(colNum));
                }
                colNum++;
            }

            return cols;
        }


        /// <summary>  
        /// /// Converts number to Excel Column Letter
        /// Killed the StackOverflow version that was here before because it maxed at 702. 
        /// This one goes to infinity and beyond, though Excel maxes at 16384.
        /// </summary>
        /// <param name="index">Number to converter</param>
        /// <returns>Correct Letter</returns>
        public static string GetColumnName(int index)
        {
            if (index <= 26)
            {
                return Convert.ToChar(index + 64).ToString();
            }
            int newIndex = index / 26;
            int modIndex = index % 26;
            if (modIndex == 0)
            {
                modIndex = 26;//if mod 26 = 0, then we're actually dealing with a Z
                newIndex--;
            }
            return GetColumnName(newIndex) + GetColumnName(modIndex);
        }


        /// <summary>
        /// This is the Class you need to call to generate reports. The Main 834 reports take in a list of these
        /// </summary>
        public class ReportView
        {
            /// <summary>
            /// The Constructor for the report list. Each view must have everything in it besides the order by
            /// </summary>
            /// <param name="view">The view name which will be called in the SQL statement.</param>
            /// <param name="dbtarget">The database target needed for the</param>
            /// <param name="sheet">The name of the Excel Sheet.</param>
            /// <param name="header">The header on the first line of a sheet</param>
            /// <param name="orderby">The order by which will be used leave blank if not needed</param>
            /// <param name="istotal">This is a boolean for if the report needs a total column for now must contain a count row</param>
            public ReportView(string view, Data.AppNames dbtarget, string sheet = "", string header = "", string orderby = "", bool istotal = false)
            {
                ViewName = view;
                OrderBy = orderby;
                dbTarget = dbtarget;
                Sheet = sheet;
                Header = header;
                isTotal = istotal;
            }

            public ReportView(DataTable ds, string sheet = "", string header = "", string orderby = "", bool istotal = false)
            {
                OrderBy = orderby;
                DataToOutput = ds;
                Sheet = sheet;
                Header = header;
                isTotal = istotal;
            }

            /// <summary>
            /// This is a very generic select * from view with dynamic order by clause
            /// </summary>
            /// <returns>The table generated with the view</returns>
            public DataTable GenerateTable()
            {
                if (DataToOutput != null)
                {
                    return DataToOutput;
                }
                {
                    if (OrderBy != "")
                    {
                        return ExtractFactory.ConnectAndQuery(dbTarget, ViewName + @"
                Order by " + OrderBy);
                    }
                    else
                    {
                        return ExtractFactory.ConnectAndQuery(dbTarget, ViewName);
                    }
                }


            }
            public String ViewName { get; set; } //The Name of the View to pull from
            public String OrderBy { get; set; } //What to Order by can be blank
            public Data.AppNames dbTarget { get; set; } //The database to pull the view from
            public String Sheet { get; set; } //The Sheet name
            public String Header { get; set; } //The Unique Header per sheet
            public bool isTotal { get; set; } //If the Sheet needs total IS NOT GENERIC
            public DataTable DataToOutput { get; set; }
        }

        /// <summary>
        /// Abstract class used by any ReportList you want to make for the Excel DataTable class
        /// </summary>
        abstract public class ReportList
        {
            /// <summary>
            /// Forces you to create your own  constructor
            /// </summary>
            public ReportList()
            {

            }

            //:/ no deconstructor 
            /// <summary>
            /// Used so you know how many reports
            /// </summary>
            /// <returns>The Count of reports</returns>
            public int GetCount()
            {
                return ReportViews.Count;
            }

            /// <summary>
            /// Gets ones View at a certain index
            /// This way you don't expose the data structure but it's still public :/
            /// </summary>
            /// <param name="index">the index number of the view</param>
            /// <returns>An individual view</returns>
            public ReportView GetReport(int index)
            {
                return ReportViews[index];
            }

            /// <summary>
            /// Whether the current instance of ReportList has a SourceFile associated with it
            /// </summary>
            public bool HasSourceFile()
            {
                return !String.IsNullOrWhiteSpace(SourceFile);
            }

            public List<ReportView> ReportViews { get; set; } // The List of View objects
            public string Date { get; set; } // The Date going to be used if not Today
            public string OutputLocation { get; set; } // The Directory of where the file is going
            public string ReportTitle { get; set; } // The Title of the Report that will be used on each sheet
            public string SourceFile { get; set; } // If applicable, the source file the report is based on
        }

        /// <summary>
        /// The Custom 834 Report 
        /// </summary>
        public class ReportList834 : ReportList
        {
            /// <summary>
            /// The Constructor of 834
            /// </summary>
            /// <param name="reportlist">The list of reports</param>
            /// <param name="directory">the 834 directories</param>
            /// <param name="title">the title</param>
            /// <param name="date">the file date </param>
            public ReportList834(List<ReportView> reportlist, string outputLoc, string title, string date, string sourceFile = null)
            {
                ReportViews = reportlist;
                OutputLocation = outputLoc;
                ReportTitle = title;
                Date = date;
                SourceFile = sourceFile;
            }

        }

        /// <summary>
        /// This method leverages the OutputDataTableToExcelEpp() method to help write DataSet objects to excel spreadsheets and is EPP complient. Each DataTable
        /// contained in the DataSet is written to separate worksheet within the specified workbook. This method automatically
        /// handles the 'multipleSheets' parameter that is normally leveraged to prevent overwriting workbooks.
        /// </summary>
        /// <param name="dataSet">The DataSet object containing the DataTables to be written to the workbook</param>
        /// <param name="outputLocation">The full file path of the desired workbook (including file name and extension)</param>
        /// <param name="sheetNames">A list containing the desired names of each worksheet within the excel workbook. If the optional
        ///                          parameter is left empty, or given the incorrect number of sheet names, the default sheet names
        ///                          will be used (ie sheet1, sheet2, ...) </param>
        public static void OutputDataSetToExcel(DataSet dataSet, String outputLocation, List<String> sheetNames = null, bool useTableNames = false)
        {
            //Need to kill off any jobs using EPP already and push them back to this
            // If sheet names were not designated, or too few sheet names were provided, resort to default names.
            if (sheetNames == null || sheetNames.Count < dataSet.Tables.Count)
            {
                sheetNames = new List<String>();

                for (int i = 0; i < dataSet.Tables.Count; i++)
                {
                    sheetNames.Add(String.Format("sheet{0}", i));
                }
            }

            // Write each of the DataTables contained in the DataSet to a new WorkSheet in the WorkBook at the speicied location.
            for (int j = 0; j < dataSet.Tables.Count; j++)
            {
                string sheetName = "";

                if (useTableNames)
                {
                    sheetName = dataSet.Tables[j].TableName;
                }
                else
                {
                    sheetName = sheetNames[j];
                }

                //OutputDataTableToExcel(dataSet.Tables[j], sheetName, outputLocation, Overwrite: false);
            }
        }



        /// <summary>
        /// Determines if a type is numeric.  Nullable numeric types are considered numeric. https://stackoverflow.com/questions/124411/using-net-how-can-i-determine-if-a-type-is-a-numeric-valuetype
        /// </summary>
        /// <remarks>
        /// Boolean is not considered numeric.
        /// </remarks>
        public static bool IsNumericType(Type type)
        {
            if (type == null)
            {
                return false;
            }

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.SByte:
                case TypeCode.Single:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return true;
                case TypeCode.Object:
                    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        return IsNumericType(Nullable.GetUnderlyingType(type));
                    }
                    return false;
            }
            return false;
        }


    }
}