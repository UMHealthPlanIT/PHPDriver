using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web;
using Utilities;
using System.IO;

namespace RunRequest.Controllers
{
    [Authorize]
    public class RequestRunController : ApiController
    {
        // GET api/RequestRun
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/RequestRun/5
        public string Get(int id)
        {
            return "value";
        }

        //// POST api/RequestRun
        //public void Post([FromBody]string value)
        //{

        //}

        // POST api/RequestRun/5
        public HttpResponseMessage Post(string id)
        {
            //For testing with SOAP UI: https://turreta.com/2015/12/11/upload-file-using-soap-ui/

            try
            {
                var httpRequest = HttpContext.Current.Request;
                if (httpRequest.Files.Count < 1)
                {
                    Services.LoggingService.WriteToLog("WEB0004", DateTime.Now, User.Identity.Name, "No Files Found, bad Request", "ERROR");
                    return Request.CreateResponse(HttpStatusCode.BadRequest);
                }

                Services.LoggingService.WriteToLog("WEB0004", DateTime.Now, User.Identity.Name, "User is posting files through Run Request to job " + id);

                foreach (string file in httpRequest.Files)
                {
                    var postedFile = httpRequest.Files[file];


                    String shareLoc;
                    if (HomeController.testMode)
                    {
                        shareLoc = @"\\.org\dfs\\JobOutput\";
                    }
                    else
                    {
                        shareLoc = @"\\.org\dfs\\JobOutput\";
                    }

                    string dropLoc = shareLoc + id + @"\FromFTP\Staging\";
                    FileSystem.ReportYearDir(dropLoc);
                    string filePath = dropLoc + new FileInfo(postedFile.FileName).Name;
                    try
                    {
                        postedFile.SaveAs(filePath);
                        Services.LoggingService.WriteToLog("WEB0004", DateTime.Now, User.Identity.Name, "Saved File at " + filePath);
                    } catch(Exception exc)
                    {
                        Services.LoggingService.WriteToLog("WEB0004", DateTime.Now, User.Identity.Name, "Error Uploading " + filePath + Environment.NewLine + exc.ToString(), "ERROR"); ;
                    }
                    


                    
                }
               

            } catch(Exception exc)
            {
                Services.LoggingService.WriteToLog("WEB0004", DateTime.Now, User.Identity.Name, "Error Posting File For Processing" + Environment.NewLine + exc.ToString(), "ERROR");
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc);
            }
            

            return Request.CreateResponse(HttpStatusCode.Created);
        }

        public void Put(string id)
        {
            //Models.StageRunRequest.LoadRunRequestToController(id, User.Identity.Name, "T");
        }

        // DELETE api/RequestRun/5
        public void Delete(int id)
        {
        }

    }
}
