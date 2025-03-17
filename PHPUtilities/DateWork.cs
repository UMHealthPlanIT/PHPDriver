using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;

namespace Utilities
{
    public static class DateWork
    {
        /// <summary>
        /// Using today, returns the first day of the previous quarter
        /// </summary>
        /// <param name="quarterLookback">Number of quarters in the past, default is none</param>
        /// <returns>First day of the last quarter</returns>
        public static DateTime GetFirstOfQuarter(int quarterLookback = 0)
        {
            DateTime lastQuarter = DateTime.Now.AddMonths(quarterLookback * -3);

            return new DateTime(lastQuarter.Year, (((lastQuarter.Month - 1) / 3 + 1) - 1) * 3 + 1, 1);
        }
        /// <summary>
        /// Using today, returns the last day of the previous quarter
        /// </summary>
        /// <param name="quarterLookback">Number of quarters in the past, default is none</param>
        /// <returns>Last day of the last quarter</returns>
        public static DateTime GetLastOfQuarter(int quarterLookback = 0)
        {
            return GetFirstOfQuarter(quarterLookback).AddMonths(3).AddDays(-1);
        }

        public static DataTable ConvertDeliverytoEasterTime(DataTable dt, string columnName)
        {
                foreach (DataRow row in dt.Rows)
                {
                var delTime = row[columnName];
                    if (!(delTime is System.DBNull))
                    {
                        DateTime utcDateTime = Convert.ToDateTime(row[columnName]);
                        string nzTimeZoneKey = "Eastern Standard Time";
                        TimeZoneInfo nzTimeZone = TimeZoneInfo.FindSystemTimeZoneById(nzTimeZoneKey);
                        DateTime nzDateTime = TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, nzTimeZone);
                        row[columnName] = nzDateTime;
                    }
                }
            dt.AcceptChanges();
            return dt;
        }

    }
}
