using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.DirectoryServices.AccountManagement;
using Utilities;
using System.Data;

namespace JobConfiguration.Models
{
    public class Permissions
    {
        /// <summary>
        /// This function accepts a list of tables, then returns only those the given user has read access to
        /// </summary>
        /// <param name="rawConfigTables">A raw list of tables</param>
        /// <returns>Only those tables who are listed as having read access to a group the user is a part of</returns>
        public static List<String> AccessibleTables(String query, String userName, Data.AppNames dataSource, Logger procLog)
        {
            List<String> ConfigTables = ExtractFactory.ConnectAndQuery<String>(dataSource, query).ToList();

            PrincipalContext ctx = new PrincipalContext(ContextType.Domain, "");
            UserPrincipal user = UserPrincipal.FindByIdentity(ctx, userName);
            GroupPrincipal DevGroup = GroupPrincipal.FindByIdentity(ctx, @"");

            if (user.IsMemberOf(DevGroup))
            {
                procLog.WriteToLog("Allowed to see all tables, member of DevIntServices");
                return ConfigTables;
            }
            else
            {
                List<string> filteredTables = new List<string>();
                List<JobTableConfiguration_C> jobConfiguration = ExtractFactory.ConnectAndQuery<JobTableConfiguration_C>(dataSource, "select * from JobTableConfiguration_C").ToList();
                List<RowLevelPermissions_C> rowLevelPermissions = ExtractFactory.ConnectAndQuery<RowLevelPermissions_C>(dataSource, "select * from RowLevelPermissions_C").ToList();

                foreach (JobTableConfiguration_C jobTab in jobConfiguration)
                {
                    GroupPrincipal Grp = GroupPrincipal.FindByIdentity(ctx, jobTab.RWPermissions);

                    if (Grp == null)
                    {
                        procLog.WriteToLog("WARNING: " + jobTab.RWPermissions + " was not found in AD, the table " + jobTab.TableName + " will never be displayed");
                        continue;
                    }

                    if (user.IsMemberOf(Grp))
                    {
                        procLog.WriteToLog(userName + " is authorized to see " + jobTab.TableName + " by being a member of " + Grp.Name);

                        if (!filteredTables.Exists(x => x == jobTab.TableName))
                        {
                            filteredTables.Add(jobTab.TableName);
                        }

                    }

                    if (rowLevelPermissions.Where(t => t.TableName == jobTab.TableName).Count() > 0 && !filteredTables.Contains(jobTab.TableName)) //yarr, thar be row level permissions on this table, skipper
                    {
                        foreach (var grpName in rowLevelPermissions.Where(t => t.TableName == jobTab.TableName).Select(t => t.RWPermissions).Distinct()) //todo: refactor this
                        {
                            GroupPrincipal rowLevelGrp = GroupPrincipal.FindByIdentity(ctx, grpName);

                            if((rowLevelGrp != null && user.IsMemberOf(rowLevelGrp)) || userName.Replace("SPARROW\\", "").ToLower() == grpName.ToLower())
                            {
                                procLog.WriteToLog(userName + " is authorized to see part of " + jobTab.TableName + " by being a member of a group/user defined in the row level permissions table");

                                if (!filteredTables.Exists(x => x == jobTab.TableName))
                                {
                                    filteredTables.Add(jobTab.TableName);
                                }
                            }
                        }
                    }
                }
                return filteredTables;
            }
        }

        private class JobTableConfiguration_C
        {
            public string TableName { get; set; }
            public string TableDescription { get; set; }
            public string RWPermissions { get; set; }
            public string AllowBulkUpdate { get; set; }
        }

        private class RowLevelPermissions_C
        {
            public string TableName { get; set; }
            public string ColumnName { get; set; }
            public string ColumnValue { get; set; }
            public string RWPermissions { get; set; }
        }

        /// <summary>
        /// Determine if given user has access to access specified table
        /// </summary>
        /// <param name="TableName">Table to be accessed</param>
        /// <returns></returns>
        public static Boolean UserHasAccess(HttpContextBase context)
        {
            string getTable = context.Request.QueryString["TableName"];

            String requestedTable = getTable == null ? context.Request.Params["TableName"] : getTable;

            List<string> userAccessibleTables = context.Session["accessibleTables"] as List<string>;

            Logger proclog = Controllers.HomeController.GetLog(context);
            if (userAccessibleTables == null)
            {
                userAccessibleTables = AccessibleTables(Controllers.HomeController.getConfigTables, context.User.Identity.Name, Controllers.HomeController.dataSource, proclog);
            }
            try
            {

                if (userAccessibleTables.Exists(x => x == requestedTable))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                proclog.WriteToLog(ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// To prevent SQL injection attacks, validate that everything we have in the table parameter is in fact a table
        /// </summary>
        /// <param name="Table">Table to test</param>
        /// <returns>Whether this is a valid table</returns>
        public static Boolean ValidateIsTable(String Table, Data.AppNames dataSource, String query)
        {

            List<String> ConfigTables = ExtractFactory.ConnectAndQuery<String>(dataSource, query).ToList();

            return ConfigTables.Exists(x => x == Table);

        }

        public static Boolean ValidateFieldsAreFields(Dictionary<object, object> dictionary, String tableName, Data.AppNames dataSource)
        {
            Models.FoundTableDetails tableData = new Models.FoundTableDetails(tableName, dataSource);

            foreach (KeyValuePair<object, object> val in dictionary)
            {
                string columnValue = string.Join("", (string[])val.Key);

                if (tableData.TableColumns.Exists(x => x.ColumnName == columnValue))
                {
                    continue;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        public static Boolean AllowsBulkUpdate(String TableName, Data.AppNames dataSource, HttpContextBase context)
        {
            String qry = String.Format("SELECT RWPermissions FROM JobTableConfiguration_C WHERE TableName = '{0}' AND AllowBulkUpdate = 'Y'", TableName);

            PrincipalContext ctx = new PrincipalContext(ContextType.Domain, "");
            UserPrincipal user = UserPrincipal.FindByIdentity(ctx, context.User.Identity.Name);
            DataTable result = ExtractFactory.ConnectAndQuery(dataSource, qry);
            GroupPrincipal DevGroup = GroupPrincipal.FindByIdentity(ctx, @"");

            if (user.IsMemberOf(DevGroup))
            {
                return true;
            }

            if (result.Rows.Count > 0)
            {
                foreach (DataRow row in result.Rows)
                {
                    GroupPrincipal grp = GroupPrincipal.FindByIdentity(ctx, row[0].ToString());
                    if(grp == null)
                    {
                        AbstractController.GetLog(context).WriteToLog(row[0].ToString() + " not found. Will never assign permissions using this group.", UniversalLogger.LogCategory.WARNING);
                        continue;
                    }
                    if (user.IsMemberOf(grp))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static Boolean HasRowLevelPermissions(String TableName, Data.AppNames dataSource)
        {
            string rowLevelPermissionsQry = string.Format("SELECT RowLevelPermissions FROM JobTableConfiguration_C WHERE TableName = '{0}' AND RowLevelPermissions='Y'", TableName);
            var rowLevelPermissions = ExtractFactory.ConnectAndQuery<string>(dataSource, rowLevelPermissionsQry);
            return (rowLevelPermissions.Count() > 0);

        }

        public static string GetRowLevelPermissionsQuery(String TableName, Data.AppNames dataSource, Logger logger, HttpContextBase context)
        {
            string dataWhereClause = "WHERE 1=1";
            string canEditQuery = string.Format("SELECT * FROM RowLevelPermissions_C WHERE TableName = '{0}'", TableName);
            DataTable canEdit = ExtractFactory.ConnectAndQuery(dataSource, canEditQuery);
            PrincipalContext ctx = new PrincipalContext(ContextType.Domain, "");
            UserPrincipal user = UserPrincipal.FindByIdentity(ctx, context.User.Identity.Name);
            GroupPrincipal DevGroup = GroupPrincipal.FindByIdentity(ctx, @"");

            int count = 0;
            foreach (DataRow row in canEdit.Rows)
            {
                GroupPrincipal grp = GroupPrincipal.FindByIdentity(ctx, row["RWPermissions"].ToString());

                if ((grp != null && user.IsMemberOf(grp) && !user.IsMemberOf(DevGroup)) || context.User.Identity.Name.Replace("Domain\\", "").ToLower() == row["RWPermissions"].ToString().ToLower())
                {
                    if (count == 0)
                    {
                        dataWhereClause += " AND (";
                        count++;
                    }
                    dataWhereClause += string.Format(" ({0} = '{1}') OR ", row["ColumnName"].ToString(), row["ColumnValue"].ToString());
                    logger.WriteToLog("Can see records where " + row["ColumnName"].ToString() + " = " + row["ColumnValue"].ToString());
                }
            }
            if (count > 0)
            {
                dataWhereClause = dataWhereClause.Substring(0, dataWhereClause.Length - 3) + ")";
            }
            return dataWhereClause;
        }

        public static string GetTeam(string userName)
        {
            PrincipalContext ctx = new PrincipalContext(ContextType.Domain, "");
            UserPrincipal user = UserPrincipal.FindByIdentity(ctx, userName);
            GroupPrincipal DevGroup = GroupPrincipal.FindByIdentity(ctx, @"");


            if (user.IsMemberOf(DevGroup))
            {
                return "DEVINT";
            }
            else
            {
                return "GENERAL";
            }
        }
    }
}