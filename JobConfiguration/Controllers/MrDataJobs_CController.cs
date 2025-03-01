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
    public class MrDataJobs_CController : AbstractController
    {
        [TableRwAuthorize]
        public override ActionResult Edit(String TableName, String KeySelector)
        {
            Models.TableUpdate tableUpdate = GetTableUpdateModel(TableName, KeySelector);

            string team = JobConfiguration.Models.Permissions.GetTeam(HttpContext.User.Identity.Name);

            return View("~/Views/" + TableName + "/Edit.cshtml", tableUpdate);
        }

        [TableRwAuthorize]
        public override ActionResult Create(String TableName)
        {
            ViewBag.TargetTable = TableName;
            ViewBag.tableSchema = GetTableSchema(TableName);

            Models.FoundTableDetails tableUpdate = GetTableSelectModel(TableName);

            string team = JobConfiguration.Models.Permissions.GetTeam(HttpContext.User.Identity.Name);

            return View("~/Views/" + TableName + "/Create.cshtml", tableUpdate);
        }
    }
}