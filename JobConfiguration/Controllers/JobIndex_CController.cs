using JobConfiguration.Models;
using Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;



namespace JobConfiguration.Controllers
{
    public class JobIndex_CController : AbstractController
    {
        public static readonly bool testMode = (WebConfigurationManager.AppSettings["RunMode"] == "Test");

        [TableRwAuthorize]
        public override ActionResult TableSelect(string TableName)
        {
            log = GetLog(HttpContext);

            if (Permissions.ValidateIsTable(TableName, dataSource, getConfigTables))
            {
                FoundTableDetails foundTabs = GetTableSelectModel(TableName);

                string[] fieldsToShow = new[]
                {
                    "JobId",
                    "Title",
                    "Tool",
                    "Job Coordinator",
                    "Recovery Type",
                    "Recovery Details",
                    "Business Owner",
                    "Department",
                    "Technical Notes",
                    "Status",
                    "Run Type",
                    "Frequency",
                    "Responsible Team",
                    "KeySelectors"
                };

                string[] colNames = foundTabs.TableData.Columns.Cast<DataColumn>().Select(x => x.ColumnName).ToArray();

                foreach(string colName in colNames)
                {
                    int index = Array.IndexOf(fieldsToShow, colName);

                    if (index == -1)
                    {
                        foundTabs.TableData.Columns.Remove(colName);
                    }
                    else
                    {
                        foundTabs.TableData.Columns[colName].SetOrdinal(index);
                    }
                }

                if (Permissions.AllowsBulkUpdate(TableName, dataSource, HttpContext))
                {
                    foundTabs.bulkUpdateAllowed = true;
                }

                return View("~/Views/" + TableName + "/TableSelect.cshtml", foundTabs);
            }
            else
            {
                return HttpNotFound("Page Not Found");
            }
        }

        [TableRwAuthorize]
        public override ActionResult Edit(string TableName, string KeySelector)
        {
            TableUpdate tableUpdate = GetTableUpdateModel(TableName, KeySelector);

            return View("~/Views/" + TableName + "/Edit.cshtml", tableUpdate);
        }

        [TableRwAuthorize]
        public override ActionResult Create(string TableName)
        {
            ViewBag.TargetTable = TableName;
            ViewBag.tableSchema = GetTableSchema(TableName);

            var nextId = SqlFactory.GetNextId();
            ViewBag.nextId = nextId;

            FoundTableDetails tableUpdate = GetTableSelectModel(TableName);

            return View("~/Views/" + TableName + "/Create.cshtml", tableUpdate);
        }

        // POST: JobConfiguration/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [TableRwAuthorize]
        public override ActionResult Create(Models.TableUpdate tableUpdate)
        {
            DataTable getData = DataWork.GetTableSchema(tableUpdate.Schema + "." + tableUpdate.TableName, dataSource);

            string[] boolFields = new[]
            {
                "Outbound Data",
                "Consumes Uploaded File",
                "Standard Package",
                "Mission Critical Job",
                "Page On Error",
                "On Hold",
                "Contains PHI",
                "Contains Sensitive Data"
            };

            string[] multiSelectFields = new[]
            {
                "Data Sources",
                "Data Domain"
            };

            DataRow row = getData.NewRow();
            string jobId = "";
            string logDetail = "";

            foreach (KeyValuePair<object, object> column in tableUpdate.PropertiesValues)
            {
                try
                {
                    string[] columnKey = (string[])column.Key;
                    string[] value = (string[])column.Value;

                    string strVal = "";
                    String columName = string.Join("", columnKey);

                    
                    if (columnKey[0] == "JobId")
                    {
                        jobId = value[0];
                    }

                    if (boolFields.Contains(columnKey[0])) //Checkbox values that equate to columns with data type bit in the database
                    {
                        strVal = value[0];
                    }
                    else if (multiSelectFields.Contains(columnKey[0]))
                    {
                        List<string> dsList = value.ToList();
                        strVal = String.Join(",", dsList);
                    }
                    else
                    {
                        strVal = string.Join("", value);
                    }

                    if (columName == "Last Modified Date")
                    {
                        row[columName] = DateTime.Now.ToString("MM/dd/yyyy");
                    }
                    else if (columName == "Last Modified By")
                    {
                        row[columName] = System.Web.HttpContext.Current.User.Identity.Name;
                    }
                    else if (strVal != "")
                    {

                        row[columName] = strVal;
                        logDetail += columName + ": " + strVal + "; ";
                    }
                }
                catch (InvalidCastException) //Catching failed attempt to shove a binary file into a string array
                {
                    HttpRequest httpRequest = System.Web.HttpContext.Current.Request;
                    SaveAttachedFile(httpRequest, jobId);
                    row["Attachment"] = true;
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
                    FoundTableDetails tmpTableUpdate = GetTableSelectModel(tableUpdate.TableName);

                    List<string> PKColNames = new List<string>();
                    foreach (ColumnDetails var in tmpTableUpdate.TableColumns)
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
                        tmpTableUpdate.FieldValues.Add(new KeyValuePair<string, string>(var.Key.ToString(), var.Value.ToString()));
                    }

                    return View("Create", tmpTableUpdate);
                }
            }

            log.WriteToLog(User.Identity.Name + " Created Record: " + logDetail + " in table " + tableUpdate.TableName);

            return RedirectToAction("TableSelect", new { TableName = tableUpdate.TableName });
        }

        // POST: JobConfiguration/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        [TableRwAuthorize]
        public override ActionResult Edit(TableUpdate fieldData)
        {
            int ind = 0;
            string jobId = "";
            bool hasAttachment = fieldData.Attachment;
            

            Dictionary<object, object> enumDic = new Dictionary<object, object>(fieldData.PropertiesValues);

            foreach (KeyValuePair<object, object> field in enumDic)
            {
                foreach (string key in (string[])field.Key)
                {
                    if (key == "JobId")
                    {
                        string[] val = (string[])fieldData.PropertiesValues[fieldData.PropertiesValues.ElementAt(ind).Key];
                        jobId = val[0];
                    }
                    if (key == "Last Modified Date")
                    {
                        fieldData.PropertiesValues[fieldData.PropertiesValues.ElementAt(ind).Key] = new string[1] { DateTime.Now.ToString() };
                    }
                    else if (key == "Last Modified By")
                    {
                        fieldData.PropertiesValues[fieldData.PropertiesValues.ElementAt(ind).Key] = new string[1] { System.Web.HttpContext.Current.User.Identity.Name };
                    }
                    else if (key == "Data Sources" || key == "Data Domain")
                    {
                        List<string> dsList = ((string[])fieldData.PropertiesValues[fieldData.PropertiesValues.ElementAt(ind).Key]).ToList();
                        fieldData.PropertiesValues[fieldData.PropertiesValues.ElementAt(ind).Key] = new string[1] { String.Join(",", dsList) };
                    }
                }
                ind++;
            }
            fieldData.KeySelector = "{ 'JobId': '" + jobId + "' }";
            HttpRequest httpRequest = System.Web.HttpContext.Current.Request;
            if (httpRequest.Files[0].FileName != "")
            {
                SaveAttachedFile(httpRequest, jobId);
                hasAttachment = true;
            }

            FoundTableDetails tableSchema = new FoundTableDetails(fieldData.TableName, dataSource);
            string UpdateQuery = "update " + tableSchema.TableSchema + "." + fieldData.TableName + " set ";

            List<SqlParameter> sqlParameters = GetValues(fieldData, ref UpdateQuery, tableSchema, true, hasAttachment);

            List<SqlParameter> whereSqlParams;
            
            UpdateQuery += " where " + SqlFactory.WhereClauseFromKey(fieldData.KeySelector, out whereSqlParams, sqlParameters.Count);

            log.WriteToLog(User.Identity.Name + " Attempting to update Record with Key Selector " + fieldData.KeySelector + @" with the following SQL """ + UpdateQuery + @""" where the column values are '" + String.Join(",", sqlParameters.Select(x => x.Value)) + "'");


            try
            {
                DataWork.RunSqlCommand(UpdateQuery, dataSource, sqlParameters.Concat(whereSqlParams).ToList());
            }
            catch (SqlException e)
            {
                if (e.Message.StartsWith("Violation of PRIMARY KEY"))
                {
                    TableUpdate tableUpdate = GetTableUpdateModel(fieldData.TableName, fieldData.KeySelector);

                    List<string> PKColNames = new List<string>();
                    foreach (ColumnDetails var in tableUpdate.TableDetails.TableColumns)
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

        public static List<SqlParameter> GetValues(TableUpdate tableUpdate, ref String createQuery, FoundTableDetails tableSchema, bool updateQuery = false, bool hasAttachment = false)
        {
            int recCounter = 0;

            List<SqlParameter> sqlParameters = new List<SqlParameter>();

            foreach (KeyValuePair<object, object> val in tableUpdate.PropertiesValues)
            {
                if (!string.Join("", (string[])val.Key).Equals(tableSchema.IdColumn))
                {
                    string[] columnKey = (string[])val.Key;
                    string[] value;
                    string valValue = "";
                    string valField = "[" + columnKey[0] + "]";
                    String paramVar = "@param" + recCounter.ToString();
                    try
                    {
                        value = (string[])val.Value;
                        valValue = value[0];
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
                    }
                    catch (InvalidCastException ex)
                    {
                        string attach = "0";
                        if (hasAttachment)
                        {
                            attach = "1";
                        }
                        if (updateQuery)
                        {
                            createQuery += ", [Attachment] = " + attach;
                        }
                        else
                        {
                            createQuery += ", " + attach;
                        }

                        sqlParameters.Add(new SqlParameter("@param" + recCounter.ToString(), hasAttachment));
                    }
                    

                    recCounter++;
                }
            }
            return sqlParameters;
        }
      
        private static void SaveAttachedFile(HttpRequest httpRequest, string jobId)
        {
            Data.AppNames db; 
            if (!testMode)
            {
                db = Data.AppNames.ExampleProd;
            }
            else
            {
                db = Data.AppNames.ExampleTest;
            }

            byte[] fileData = null;

            using (BinaryReader binaryReader = new BinaryReader(httpRequest.Files[0].InputStream))
            {
                fileData = binaryReader.ReadBytes(httpRequest.Files[0].ContentLength);
            }

            //Query will attempt to update the record first, and if there are no records to update, it will insert a new one.
            string query = string.Format(@"UPDATE [CONTROLLER].[JobIndex_Attachements_C] SET [File] = @file, [FileName] = @filename, [JobId] = @jobid, [ContentType] = @contenttype WHERE JobId = '{0}'
                                                    IF @@ROWCOUNT = 0
                                                    INSERT into[CONTROLLER].[JobIndex_Attachements_C]([File], [FileName], [ContentType], [JobId]) values(@file, @filename, @contenttype, '{0}'); ", jobId);
            List<SqlParameter> sqlParams = new List<SqlParameter>();
            SqlParameter fileParam = new SqlParameter("@file", fileData);
            sqlParams.Add(fileParam);
            SqlParameter fileNameParam = new SqlParameter("@filename", httpRequest.Files[0].FileName);
            sqlParams.Add(fileNameParam);
            SqlParameter jobIdParam = new SqlParameter("@jobid", jobId);
            sqlParams.Add(jobIdParam);
            SqlParameter contentTypeParam = new SqlParameter("@contenttype", httpRequest.Files[0].ContentType);
            sqlParams.Add(contentTypeParam);

            DataWork.RunSqlCommand(query, db, sqlParams);
        }

        [HttpGet]
        public ActionResult Attachment(string jobId)
        {
            Data.AppNames db;
            if (!testMode)
            {
                db = Data.AppNames.ExampleProd;
            }
            else
            {
                db = Data.AppNames.ExampleTest;
            }

            DataTable fileRecords = ExtractFactory.ConnectAndQuery(db, $"SELECT TOP (1) [JobId], [FileName], [ContentType], [File] FROM[CONTROLLER].[JobIndex_Attachements_C] WHERE JobId = '{jobId}'");

            if (fileRecords.Rows.Count > 0)
            {
                Byte[] fileBytes = (Byte[])fileRecords.Rows[0]["File"];
                string contentType = fileRecords.Rows[0]["ContentType"].ToString();
                string fileName = fileRecords.Rows[0]["FileName"].ToString();
                return File(fileBytes, contentType, fileName);
            }
            else
            {
                return new EmptyResult();
            }
        }
    }
}