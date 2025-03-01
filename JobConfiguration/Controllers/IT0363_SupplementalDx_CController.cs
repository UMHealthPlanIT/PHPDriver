using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using JobConfiguration.Models;
using Utilities;

namespace JobConfiguration.Controllers
{
    public class IT0363_SupplementalDx_CController : AbstractController
    {
        // POST: JobConfiguration/Create @ IT0363_SupplementalDx_C
        [HttpPost]
        [ValidateAntiForgeryToken]
        [TableRwAuthorize]
        public override ActionResult Create(Models.TableUpdate tableUpdate)
        {
            Models.TableUpdate tab = tableUpdate;

            if (Permissions.ValidateIsTable(tableUpdate.TableName, dataSource, getConfigTables) && Permissions.ValidateFieldsAreFields(tableUpdate.PropertiesValues, tableUpdate.TableName, dataSource))
            {
                DataTable getData = DataWork.GetTableSchema(tableUpdate.Schema + "." + tableUpdate.TableName, dataSource);

                DataRow row = getData.NewRow();

                String logDetail = "";
                foreach (KeyValuePair<object, object> column in tableUpdate.PropertiesValues)
                {
                    string columnName = string.Join("", (string[])column.Key);
                    string strVal = "";

                    if (columnName.Equals("SourceCode"))
                    {
                        strVal = "PHP";
                    }
                    else if (columnName.Equals("InsertDate"))
                    {
                        strVal = System.DateTime.Now.ToString("yyyy-MM-dd");
                    }
                    else
                    {
                        strVal = string.Join("", (string[])column.Value);
                    }

                    if (strVal != "")
                    {
                        row[columnName] = strVal;
                        logDetail += columnName + ": " + strVal + "; ";
                    }

                }

                getData.Rows.Add(row);
                DataWork.SaveDataTableToDb(tableUpdate.Schema + "." + tableUpdate.TableName, getData, dataSource);

                log.WriteToLog(HttpContext.User.Identity.Name + " Created Record: " + logDetail + " in table " + tableUpdate.TableName);

                return RedirectToAction("TableSelect", new { TableName = tableUpdate.TableName });
            }
            else
            {
                return HttpNotFound("Page Not Found");
            }

        }

        // POST: JobConfiguration/Create @ IT0363_SupplementalDx_C
        [HttpPost]
        [ValidateAntiForgeryToken]
        [TableRwAuthorize]
        public override ActionResult Edit(Models.TableUpdate fieldData)
        {
            if (Permissions.ValidateIsTable(fieldData.TableName, dataSource, getConfigTables) && Permissions.ValidateFieldsAreFields(fieldData.PropertiesValues, fieldData.TableName, dataSource))
            {
                Models.FoundTableDetails tableSchema = new Models.FoundTableDetails(fieldData.TableName, dataSource);

                String checkUpdatableQuery = "SELECT * FROM " + tableSchema.TableSchema + "." + fieldData.TableName + " WHERE ";
                String UpdateQuery = "update " + tableSchema.TableSchema + "." + fieldData.TableName + " set ";

                List<SqlParameter> sqlParameters = new List<SqlParameter>();
                List<SqlParameter> whereSqlParams;

                checkUpdatableQuery += Models.SqlFactory.WhereClauseFromKey(fieldData.KeySelector, out whereSqlParams);
                checkUpdatableQuery += " AND SourceCode = 'HDVI'";
                
                DataTable result = ExtractFactory.ConnectAndQuery(dataSource, checkUpdatableQuery, whereSqlParams);
                bool isNotEditable = (result.Rows.Count == 1);

                if (isNotEditable)
                {
                    string parm1 = ((string[]) fieldData.PropertiesValues.Values.ToList()[result.Columns.IndexOf("originalClaimIdentifier")])[0];
                    string parm2 = ((string[]) fieldData.PropertiesValues.Values.ToList()[result.Columns.IndexOf("FreezeFlag")])[0];
                    
                    sqlParameters.Add(new SqlParameter( "param0", parm1 ));
                    sqlParameters.Add(new SqlParameter( "param1", parm2 ));

                    UpdateQuery += "originalClaimIdentifier = @param0, FreezeFlag = @param1 ";
                }
                else
                {
                    sqlParameters = Models.SqlFactory.GetValuesFromFieldData(fieldData, ref UpdateQuery, tableSchema, true);
                }

                UpdateQuery += " where " + Models.SqlFactory.WhereClauseFromKey(fieldData.KeySelector, out whereSqlParams, sqlParameters.Count);

                if (SqlFactory.RunSqlCommand(UpdateQuery, dataSource, sqlParameters.Concat(whereSqlParams).ToList()))
                {
                    log.WriteToLog(HttpContext.User.Identity.Name + " Updated Record with Key Selector " + fieldData.KeySelector + @" with the following SQL """ + UpdateQuery + @""" where the column values are '" + String.Join(",", sqlParameters.Select(x => x.Value)) + "'");
                }

                return RedirectToAction("TableSelect", new { TableName = fieldData.TableName });

            }
            else
            {
                return HttpNotFound("Page Not Found");
            }

        }

        // POST: JobConfiguration/Create @ IT0363_SupplementalDx_C
        [HttpPost]
        [ValidateAntiForgeryToken]
        [TableRwAuthorize]
        public override ActionResult Delete(Models.TableUpdate fieldData)
        {
            if (Permissions.ValidateIsTable(fieldData.TableName, dataSource, getConfigTables)) //todo: validate JSON selector
            {
                String deleteQuery = "delete from " + fieldData.Schema + "." + fieldData.TableName + " where ";

                List<SqlParameter> sqlParams;
                deleteQuery += Models.SqlFactory.WhereClauseFromKey(fieldData.KeySelector, out sqlParams);
                deleteQuery += "AND SourceCode != 'HDVI'";

                if (SqlFactory.RunSqlCommand(deleteQuery, dataSource, sqlParams))
                {
                    log.WriteToLog(HttpContext.User.Identity.Name + " Deleted Record with Key Selector " + fieldData.KeySelector + " in table " + fieldData.TableName);
                }

                return RedirectToAction("TableSelect", new { TableName = fieldData.TableName });
            }
            else
            {
                return HttpNotFound("Page Not Found");
            }

        }
    }
}
