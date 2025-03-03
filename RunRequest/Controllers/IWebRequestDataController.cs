using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Utilities;
using Newtonsoft.Json;
using System.Data;
using System.Web.Configuration;

namespace RunRequest.Controllers
{
    public class IWebRequestDataController : Controller
    {
        private static readonly bool testMode = Convert.ToBoolean(WebConfigurationManager.AppSettings["testMode"]);
        public ContentResult GetListData(int page, string listIdentifier, string term = "")
        {
            FinalJSON finalGroup = new FinalJSON();

            Data.AppNames db = testMode ? Data.AppNames.ExampleTest : Data.AppNames.ExampleProd;
            LargeListData list = ExtractFactory.ConnectAndQuery<LargeListData>(db, string.Format("SELECT * FROM [dbo].[WEB0004_LargeListData_C] WHERE ListIdentifier = '{0}'", listIdentifier)).First();
            finalGroup.results = ExtractFactory.ConnectAndQuery<JSONInfo>(db, string.Format(list.ListSQL, term, (page - 1) * 100)).ToList();
            finalGroup.totalCount = ExtractFactory.ConnectAndQuery<int>(db, string.Format(list.CountSQL, term)).First();
            return Content(JsonConvert.SerializeObject(finalGroup), "application/json");
        }

        private class FinalJSON
        {
            public int totalCount { get; set; }
            public List<JSONInfo> results { get; set; }
        }

        private class JSONInfo
        {
            public String id { get; set; }
            public String text { get; set; }
        }
        
        private class LargeListData
        {
            public string ListIdentifier { get; set; }
            public string ListSQL { get; set; }
            public string CountSQL { get; set; }
        }
    }
}