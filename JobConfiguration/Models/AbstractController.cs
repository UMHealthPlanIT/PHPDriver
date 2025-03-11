using Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Security.Principal;
using System.DirectoryServices.AccountManagement;
using System.Configuration;

namespace JobConfiguration.Models
{
    public abstract class AbstractController: Controller
    {
        public static String getConfigTables = @"SELECT TABLE_NAME FROM information_schema.tables WHERE TABLE_TYPE='BASE TABLE' and TABLE_NAME like '%_C' order by TABLE_NAME";
        public static Data.AppNames dataSource;
        public static Logger log;
        public new HttpContextBase HttpContext
        {
            get
            {
                HttpContextWrapper context =
                    new HttpContextWrapper(System.Web.HttpContext.Current);
                return (HttpContextBase)context;
            }
        }

        public new IPrincipal User
        {
            get
            {
                return HttpContext.User;
            }
        }


        public ActionResult Index()
        {
            log = GetLog(HttpContext);

            Models.FoundTableDetails foundTabDetails = new Models.FoundTableDetails("Job Configuration", dataSource);

            foundTabDetails.TableDescription = "A Table of Tables";

            DataTable tempDataTable = ExtractFactory.ConnectAndQuery(dataSource, @"SELECT TABLE_NAME as TableName, TableDescription, SupportingInformation, ReadCheck 
                FROM information_schema.tables
                LEFT JOIN JobTableConfigurationDetails_C as JTCD
                ON TABLE_NAME = JTCD.TableName
                WHERE TABLE_TYPE = 'BASE TABLE' and TABLE_NAME like '%_C'
                order by TABLE_NAME");
            DataTable dataTable = tempDataTable.Clone();
            List<string> accTables = Models.Permissions.AccessibleTables(getConfigTables, User.Identity.Name, dataSource, log);

            Session["accessibleTables"] = accTables;
            foreach (System.Data.DataRow item in tempDataTable.Rows)
            {
                if (accTables.Contains(item[0]))
                {
                    dataTable.ImportRow(item);
                }
            }

            foundTabDetails.TableData = dataTable;

            return View(foundTabDetails);
        }

        // GET: JobConfiguration/TableSelect
        [TableRwAuthorize]
        public virtual ActionResult TableSelect(String TableName)
        {
            Models.FoundTableDetails foundTabs = GetTableSelectModel(TableName);

            return View("~/Views/" + TableName + "/TableSelect.cshtml", foundTabs);
        }

        // GET: JobConfiguration/Create
        [TableRwAuthorize]
        public virtual ActionResult Create(String TableName)
        {
            ViewBag.TargetTable = TableName;
            ViewBag.tableSchema = GetTableSchema(TableName);

            Models.FoundTableDetails tableUpdate = GetTableSelectModel(TableName);

            return View("~/Views/" + TableName + "/Create.cshtml", tableUpdate);
        }

        // POST: JobConfiguration/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [TableRwAuthorize]
        public virtual ActionResult Create(Models.TableUpdate tableUpdate)
        {
            DataTable getData = DataWork.GetTableSchema(tableUpdate.Schema + "." + tableUpdate.TableName, dataSource);

            DataRow row = getData.NewRow();

            String logDetail = "";
            foreach (KeyValuePair<object, object> column in tableUpdate.PropertiesValues)
            {
                string[] columnKey = (string[])column.Key;
                string[] value = (string[])column.Value;

                string strVal = string.Join("", value);

                if (strVal != "")
                {
                    String columName = string.Join("", columnKey);
                    row[columName] = strVal;
                    logDetail += columName + ": " + strVal + "; ";
                }

            }

            log.WriteToLog(User.Identity.Name + " Attempting to create record: " + logDetail + " in table " + tableUpdate.TableName);

            getData.Rows.Add(row);

            try
            {
                DataWork.SaveDataTableToDb(tableUpdate.Schema + "." + tableUpdate.TableName, getData, dataSource);
            }
            catch (SqlException e)
            {
                if (e.Message.StartsWith("Violation of PRIMARY KEY"))
                {
                    Models.FoundTableDetails tmpTableUpdate = GetTableSelectModel(tableUpdate.TableName);

                    List<String> PKColNames = new List<string>();
                    foreach (JobConfiguration.Models.ColumnDetails var in tmpTableUpdate.TableColumns)
                    {
                        if (var.PrimaryKey == true)
                        {
                            PKColNames.Add(var.ColumnName);
                        }
                    }

                    Match PKValues = Regex.Match(e.Message, @"The duplicate key value is [(](.*)[)]");

                    TempData.Add("ErrorMessageSimple", "Primary key violation. Duplicate value was entered.");
                    TempData.Add("ErrorMessageSimple2", PKValues.Value + " for columns (" + String.Join(", ", PKColNames) + ")");
                    TempData.Add("ErrorMessage", e.Message);
                    TempData.Add("ErrorStackTrace", e.StackTrace);

                    foreach (KeyValuePair<object, object> var in tableUpdate.PropertiesValues)
                    {
                        tmpTableUpdate.FieldValues.Add(new KeyValuePair<String, String>(var.Key.ToString(), var.Value.ToString()));
                    }

                    return View("Create", tmpTableUpdate);
                }
            }


            log.WriteToLog(User.Identity.Name + " Created Record: " + logDetail + " in table " + tableUpdate.TableName);

            return RedirectToAction("TableSelect", new { TableName = tableUpdate.TableName });
        }

        // GET: JobConfiguration/Edit
        [TableRwAuthorize]
        public virtual ActionResult Edit(String TableName, String KeySelector)
        {
            Models.TableUpdate tableUpdate = GetTableUpdateModel(TableName, KeySelector);

            return View("~/Views/" + TableName + "/Edit.cshtml", tableUpdate);
        }

        // POST: JobConfiguration/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        [TableRwAuthorize]
        public virtual ActionResult Edit(Models.TableUpdate fieldData)
        {
            Models.FoundTableDetails tableSchema = new Models.FoundTableDetails(fieldData.TableName, dataSource);
            String UpdateQuery = "update " + tableSchema.TableSchema + "." + fieldData.TableName + " set ";

            List<SqlParameter> sqlParameters = Models.SqlFactory.GetValuesFromFieldData(fieldData, ref UpdateQuery, tableSchema, true);

            List<SqlParameter> whereSqlParams;
            UpdateQuery += " where " + Models.SqlFactory.WhereClauseFromKey(fieldData.KeySelector, out whereSqlParams, sqlParameters.Count);

            log.WriteToLog(User.Identity.Name + " Attempting to update Record with Key Selector " + fieldData.KeySelector + @" with the following SQL """ + UpdateQuery + @""" where the column values are '" + String.Join(",", sqlParameters.Select(x => x.Value)) + "'");


            try
            {
                DataWork.RunSqlCommand(UpdateQuery, dataSource, sqlParameters.Concat(whereSqlParams).ToList());
            }
            catch (SqlException e)
            {
                if (e.Message.StartsWith("Violation of PRIMARY KEY"))
                {
                    Models.TableUpdate tableUpdate = GetTableUpdateModel(fieldData.TableName, fieldData.KeySelector);

                    List<String> PKColNames = new List<string>();
                    foreach (JobConfiguration.Models.ColumnDetails var in tableUpdate.TableDetails.TableColumns)
                    {
                        if (var.PrimaryKey == true)
                        {
                            PKColNames.Add(var.ColumnName);
                        }
                    }

                    Match PKValues = Regex.Match(e.Message, @"The duplicate key value is [(](.*)[)]");

                    //tableUpdate.PropertiesValues = fieldData.PropertiesValues;
                    TempData.Add("ErrorMessageSimple", "Primary key violation. Duplicate value was entered.");
                    TempData.Add("ErrorMessageSimple2", PKValues.Value + " for columns (" + String.Join(", ", PKColNames) + ")");
                    TempData.Add("ErrorMessage", e.Message);
                    TempData.Add("ErrorStackTrace", e.StackTrace);

                    return View("Edit", tableUpdate);
                }
                else
                {
                    throw e;
                }
            }
            

            log.WriteToLog(User.Identity.Name + " Updated Record with Key Selector " + fieldData.KeySelector + @" with the following SQL """ + UpdateQuery + @""" where the column values are '" + String.Join(",", sqlParameters.Select(x => x.Value)) + "'");

            return RedirectToAction("TableSelect", new { TableName = fieldData.TableName });
        }
        
        // GET: JobConfiguration/Delete
        [TableRwAuthorize]
        public virtual ActionResult Delete(String TableName, String KeySelector)
        {
            Models.TableUpdate tableUpdate = GetTableDeleteModel(TableName, KeySelector);

            return View("~/Views/" + TableName + "/Delete.cshtml", tableUpdate);
        }

        // POST: JobConfiguration/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        [TableRwAuthorize]
        public virtual ActionResult Delete(Models.TableUpdate fieldData)
        {
            String deleteQuery = "delete from " + fieldData.Schema + "." + fieldData.TableName + " where ";

            List<SqlParameter> sqlParams;
            deleteQuery += Models.SqlFactory.WhereClauseFromKey(fieldData.KeySelector, out sqlParams);

            DataWork.RunSqlCommand(deleteQuery, dataSource, sqlParams);

            log.WriteToLog(User.Identity.Name + " Deleted Record with Key Selector " + fieldData.KeySelector + " in table " + fieldData.TableName);

            return RedirectToAction("TableSelect", new { TableName = fieldData.TableName });
        }

        public Models.FoundTableDetails GetTableSelectModel(String TableName)
        {
            Models.FoundTableDetails foundTabs = new Models.FoundTableDetails(TableName, dataSource);

            string tableDesc;
            string tableSupportInfo;

            string tableInfoQry = string.Format("SELECT	TableDescription, SupportingInformation FROM JobTableConfigurationDetails_C WHERE TableName = '{0}'", TableName);
            DataTable tableInfo = ExtractFactory.ConnectAndQuery(dataSource, tableInfoQry);
            if (tableInfo.Rows.Count > 0)
            {
                tableDesc = string.Concat("", tableInfo.Rows[0][0]);
                tableSupportInfo = string.Concat("", tableInfo.Rows[0][1]);
            } else
            {
                tableDesc = "";
                tableSupportInfo = "";
            }
            

            foundTabs.TableDescription = (tableDesc.Length <= 80) ? tableDesc : "";
            foundTabs.SupportingInformation = tableSupportInfo;

            string tableSchema = GetTableSchema(TableName);
            
            //row level permissions logic
            string foundDataQuery = "select * from " + tableSchema + "." + TableName;
            if(Permissions.HasRowLevelPermissions(TableName, dataSource))
            {
                foundDataQuery += " " + Permissions.GetRowLevelPermissionsQuery(TableName, dataSource, log, HttpContext);
            }
            DataTable foundData = ExtractFactory.ConnectAndQuery(dataSource, foundDataQuery);
            //end row level permissions logic

            foundTabs.TableData = foundData;

            foundData.Columns.Add("KeySelectors");

            foreach (DataRow row in foundData.Rows)
            {
                List<String> primaryKeys = foundTabs.TableColumns.Where(x => x.PrimaryKey).Select(x => x.ColumnName).ToList();

                row["KeySelectors"] = Models.KeySelectorWork.GetJSONSelector(row, primaryKeys);

            }

            foundData.AcceptChanges();

            foundTabs.TableData = foundData;

            log.WriteToLog(HttpContext.User.Identity.Name + " selected config table " + TableName);

            return foundTabs;
        }

        public Models.TableUpdate GetTableUpdateModel(String TableName, String KeySelector)
        {
            Models.TableUpdate tableUpdate = new Models.TableUpdate();

            tableUpdate.TableName = TableName;
            tableUpdate.KeySelector = KeySelector;
            tableUpdate.Schema = ExtractFactory.ConnectAndQuery<String>(dataSource, String.Format(@"SELECT TABLE_SCHEMA FROM information_schema.tables WHERE TABLE_TYPE='BASE TABLE' and TABLE_NAME = '{0}'", tableUpdate.TableName)).First();

            tableUpdate.PropertiesValues = Models.KeySelectorWork.GetRecordToEdit(TableName, KeySelector, dataSource, tableUpdate.Schema);
            tableUpdate.TableDetails = new Models.FoundTableDetails(TableName, dataSource);

            return tableUpdate;
        }

        public Models.TableUpdate GetTableDeleteModel(String TableName, String KeySelector)
        {
            Models.TableUpdate tableDelete = new Models.TableUpdate();

            tableDelete.TableName = TableName;
            tableDelete.KeySelector = KeySelector;
            tableDelete.Schema = ExtractFactory.ConnectAndQuery<String>(dataSource, String.Format(@"SELECT TABLE_SCHEMA FROM information_schema.tables WHERE TABLE_TYPE='BASE TABLE' and TABLE_NAME = '{0}'", tableDelete.TableName)).First();
            tableDelete.PropertiesValues = Models.KeySelectorWork.GetRecordToEdit(TableName, KeySelector, dataSource, tableDelete.Schema);

            return tableDelete;
        }

        public static Logger GetLog(HttpContextBase context)
        {
            Logger webLog = context.Session["webLog"] as Logger;

            if (webLog == null)
            {

                if (ConfigurationManager.AppSettings["RunMode"].ToUpper() == "PROD")
                {
                    webLog = new Logger("WEB0003", false);
                    webLog.WriteToLog("Running in Prod Mode");
                }
                else
                {
                    webLog = new Logger("WEB0003", true);
                    webLog.WriteToLog("Running in Test Mode");
                }

                webLog.WriteToLog(context.User.Identity.Name + " has accessed the job configuration tool");

                context.Session["webLog"] = webLog;
            }

            return webLog;
        }

        public static string GetTableSchema(string TableName)
        {
            return ExtractFactory.ConnectAndQuery(dataSource, "SELECT TABLE_SCHEMA FROM information_schema.tables WHERE TABLE_TYPE='BASE TABLE' and TABLE_NAME = @TableName", new List<SqlParameter>() { new SqlParameter() { ParameterName = "TableName", Value = TableName } }).Rows[0][0].ToString();
        }

        public class TableRwAuthorizeAttribute : AuthorizeAttribute
        {

            protected override bool AuthorizeCore(HttpContextBase httpContext)
            {
                bool authorize = false;

                if (Permissions.UserHasAccess(httpContext))
                {
                    authorize = true;
                }

                return authorize;
            }
            protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
            {
                filterContext.Result = new HttpUnauthorizedResult();
            }
        }

        protected override void OnException(ExceptionContext filterContext)
        {
            filterContext.ExceptionHandled = true;

            if (log == null)
            {
                GetLog(HttpContext);
            }
            log.WriteToLog(filterContext.Exception.ToString());
            filterContext.Result = new ViewResult { ViewName = "~/Views/Shared/Error.cshtml", ViewBag = { ErrorMessage = filterContext.Exception.Message, ErrorStackTrace = filterContext.Exception.StackTrace } };
        }
    }
}
