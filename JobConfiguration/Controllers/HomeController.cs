using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Utilities;
using System.Data.SqlClient;
using JobConfiguration.Models;
using System.Data;
using System.Reflection;

namespace JobConfiguration.Controllers
{
    public class HomeController : AbstractController
    {
        [TableRwAuthorize]
        public override ActionResult TableSelect(String TableName)
        {
            log = GetLog(HttpContext);

            if (Permissions.ValidateIsTable(TableName, dataSource, getConfigTables))
            {
                Type controller = CheckForCustomController(TableName);
                if (controller != null) //if this table has an associated custom controller
                {
                    AbstractController customController = (AbstractController)Activator.CreateInstance(controller); //we prefer the new default Logger constructor
                    return customController.TableSelect(TableName);
                }
                else
                {
                    Models.FoundTableDetails foundTabs = GetTableSelectModel(TableName);

                    if (Permissions.AllowsBulkUpdate(TableName, dataSource, HttpContext))
                    {
                        foundTabs.bulkUpdateAllowed = true;
                    }

                    return View(foundTabs);
                }

            }
            else
            {
                return HttpNotFound("Page Not Found");
            }
        }

        [TableRwAuthorize]
        public override ActionResult Create(String TableName)
        {

            if (Permissions.ValidateIsTable(TableName, dataSource, getConfigTables))
            {
                Type controller = CheckForCustomController(TableName);
                if (controller != null) //if this table has an associated custom controller
                {
                    AbstractController customController = (AbstractController)Activator.CreateInstance(controller); //we prefer the new default Logger constructor
                    return customController.Create(TableName);
                }
                else
                {
                    ViewBag.TargetTable = TableName;
                    ViewBag.tableSchema = GetTableSchema(TableName);
                    Models.FoundTableDetails foundTableDtls = new Models.FoundTableDetails(TableName, dataSource);
                    return View(foundTableDtls);
                }
            }
            else
            {
                return HttpNotFound("Page Not Found");
            }

        }

        // POST: JobConfiguration/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [TableRwAuthorize]
        public override ActionResult Create(Models.TableUpdate tableUpdate)
        {
            Models.TableUpdate tab = tableUpdate;
            
            if (Permissions.ValidateIsTable(tableUpdate.TableName, dataSource, getConfigTables) && Permissions.ValidateFieldsAreFields(tableUpdate.PropertiesValues, tableUpdate.TableName, dataSource))
            {

                Type controller = CheckForCustomController(tableUpdate.TableName);
                if (controller != null) //if this table has an associated custom controller
                {
                    AbstractController customController = (AbstractController)Activator.CreateInstance(controller); //we prefer the new default Logger constructor
                    return customController.Create(tableUpdate);
                }
                else
                {
                    return base.Create(tableUpdate);
                }
            }
            else
            {
                return HttpNotFound("Page Not Found");
            }

        }

        [TableRwAuthorize]
        public override ActionResult Edit(String TableName, String KeySelector)
        {
            if (KeySelector == "{  }")
            {
                throw new Exception("No Primary Key Defined on the Table, please contact IT");
            }

            if (Permissions.ValidateIsTable(TableName, dataSource, getConfigTables))
            {
                Type controller = CheckForCustomController(TableName);
                if (controller != null) //if this table has an associated custom controller
                {
                    AbstractController customController = (AbstractController)Activator.CreateInstance(controller); //we prefer the new default Logger constructor
                    return customController.Edit(TableName, KeySelector);
                }
                else
                {
                    Models.TableUpdate updateDetails = GetTableUpdateModel(TableName, KeySelector);
                    return View(updateDetails);
                }
            }
            else
            {
                return HttpNotFound("Page Not Found");
            }

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [TableRwAuthorize]
        public override ActionResult Edit(Models.TableUpdate fieldData)
        {
            if (Permissions.ValidateIsTable(fieldData.TableName, dataSource, getConfigTables) && Permissions.ValidateFieldsAreFields(fieldData.PropertiesValues, fieldData.TableName, dataSource))
            {
                Type controller = CheckForCustomController(fieldData.TableName);
                if (controller != null) //if this table has an associated custom controller
                {
                    AbstractController customController = (AbstractController)Activator.CreateInstance(controller); //we prefer the new default Logger constructor
                    return customController.Edit(fieldData);
                }
                else
                {
                    return base.Edit(fieldData);
                }

            }
            else
            {
                return HttpNotFound("Page Not Found");
            }

        }


        [TableRwAuthorize]
        public override ActionResult Delete(String TableName, String KeySelector)
        {
            if (KeySelector == "{  }")
            {
                throw new Exception("No Primary Key Defined on the Table, please contact IT");
            }

            if (Permissions.ValidateIsTable(TableName, dataSource, getConfigTables))
            {
                Type controller = CheckForCustomController(TableName);
                if (controller != null) //if this table has an associated custom controller
                {
                    AbstractController customController = (AbstractController)Activator.CreateInstance(controller); //we prefer the new default Logger constructor
                    return customController.Delete(TableName, KeySelector);
                }
                else
                {
                    Models.TableUpdate updateDetails = GetTableDeleteModel(TableName, KeySelector);

                    return View(updateDetails);
                }
            }
            else
            {
                return HttpNotFound("Page Not Found");
            }

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [TableRwAuthorize]
        public override ActionResult Delete(Models.TableUpdate fieldData)
        {
            if (Permissions.ValidateIsTable(fieldData.TableName, dataSource, getConfigTables)) //todo: validate JSON selector
            {
                Type controller = CheckForCustomController(fieldData.TableName);
                if (controller != null) //if this table has an associated custom controller
                {
                    AbstractController customController = (AbstractController)Activator.CreateInstance(controller); //we prefer the new default Logger constructor
                    return customController.Delete(fieldData);
                }
                else
                {
                    return base.Delete(fieldData);
                }
            }
            else
            {
                return HttpNotFound("Page Not Found");
            }

        }

        private static Type CheckForCustomController(String TableName)
        {
            List<System.Type> controllers = Assembly.GetExecutingAssembly()
                    .GetTypes()
                    .Where(type => typeof(Controller).IsAssignableFrom(type)).ToList();

            return controllers.Where(x => x.Name.StartsWith(TableName)).FirstOrDefault();
        }

    }
}