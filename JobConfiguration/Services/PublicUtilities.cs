using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json.Linq;

namespace JobConfiguration.Services
{
    public class PublicUtilities
    {
        public static List<string> getDatas(string user)
        {
            string team = JobConfiguration.Models.Permissions.GetTeam(user);
            List<string> littleDatas = new List<string>();
            littleDatas.Add("");
            if (team == "DEVINT")
            {
                JObject result = Utilities.Integrations.SourceControl.CallVisualStudioAPI("items?scopePath=" + "$/PhpAPIGroup/PhpAPI/PhpConfigDb/dbo/Stored Procedures/" + "&includeContent=true&recursionLevel=Full", true, "Development Services");
                littleDatas.AddRange(jObjToStrList(result));


            }

            littleDatas.Sort();
            return littleDatas;
        }

        private static List<string> jObjToStrList(JObject result)
        {
            List<string> littleDatas = new List<string>();

            JToken results = result["value"];

            foreach (JToken sql in results)
            {
                if (sql["isFolder"] == null)
                {
                    string fileName = sql["path"].ToString();
                    if (System.IO.Path.GetExtension(fileName).ToUpper() == ".SQL")
                    {
                        fileName = System.IO.Path.GetFileName(fileName);
                        //fileName = fileName.Substring(fileName.IndexOf("Mr Data/")).Replace("Mr Data/", "");
                        littleDatas.Add(fileName);
                    }
                }
            }
            return littleDatas;
        }
    }
}