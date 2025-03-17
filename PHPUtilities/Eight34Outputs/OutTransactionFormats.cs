using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Utilities.Eight34Outputs
{
    public class OutTransactionFormats
    {

        private readonly Logger ProcLog;

        public OutTransactionFormats(Logger callingProcess)
        {
            ProcLog = callingProcess;
        }

        /// <summary>
        /// Leveraging reflection, interrogates the trans records in this object and writes out the key (property) value (data) pairs in the keyword structure.
        /// </summary>
        /// <param name="outputLoc">Where to write out the file to</param>
        /// <param name"Recs">Inbound keyword records</param>
        public void SerializeKeyWord(string outputLoc, object Recs)
        {

        }

        private static void SerializeSingleTableRecord(StreamWriter file, object tableRec)
        {

        }

        private static void SerializeListTableRecords(StreamWriter file, IList tableRecArray, int m)
        {

        }

        public static DataTable ReadFileIntoMemory(string filePath, string recordType)
        {
            DataTable table = new DataTable();
            table.Columns.Add("LineNumber", typeof(int));
            string[] lines = File.ReadAllLines(filePath);

            //foreach (string line in lines)
            for (int i = 0; i < lines.Length; i++)
            {
                string[] lineItems = lines[i].Split(',');

                if (lineItems[0].Split('=')[1].Replace("\"", string.Empty) == recordType)
                {
                    foreach (string lineItem in lineItems) //go through and add any missing columns first
                    {
                        string fieldName = lineItem.Split('=')[0].Substring(2);

                        if (table.Columns.Contains(fieldName) == false)
                        {
                            table.Columns.Add(fieldName, typeof(string));
                        }
                    }

                    DataRow newRow = table.NewRow();
                    newRow["LineNumber"] = i + 1;
                    foreach (string lineItem in lineItems) //loop through again to insert values
                    {
                        string[] item = lineItem.Split('=');
                        newRow[item[0].Substring(2)] = item[1].Replace("\"", string.Empty);
                    }

                    table.Rows.Add(newRow);
                }
            }

            return table;
        }
    }
}
