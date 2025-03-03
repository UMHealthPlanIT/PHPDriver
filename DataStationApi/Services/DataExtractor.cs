using Newtonsoft.Json;
using Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace DataStationApi.Services
{
    public class DataExtractor
    {

        public static HttpResponseMessage DataGetter(string database, string query, string outputType, HttpRequestMessage Request, System.Security.Principal.IPrincipal user, String sheetName = "Worksheet", List<SqlParameter> parms = null, Boolean IsStoredProc = false)
        {

            Logger log = Services.Log.getLog(user);

            bool successParse = Enum.TryParse(database, out Data.AppNames dataSource);

            if (successParse)
            {
                if (dataSource == Data.AppNames.ExampleProd || dataSource == Data.AppNames.ExampleTest)
                {
                    DataTable outputData;
                    try
                    {

                        if (parms == null)
                        {
                            log.WriteToLog("Connecting to : " + dataSource.ToString() + " with query " + query);
                            outputData = ExtractFactory.ConnectAndQuery(dataSource, query);
                        }
                        else
                        {
                            log.WriteToLog("Connecting to : " + dataSource.ToString() + " with query " + query + " and parameters " + String.Join(",", parms));
                            outputData = ExtractFactory.ConnectAndQuery(dataSource, query, parms, IsStoredProc);
                        }
                    } catch(Exception exc)
                    {
                        log.WriteToLog("Error Accessing Data: " + Environment.NewLine + exc.ToString());
                        throw exc;
                    }
                    


                    if (outputType.ToUpper() == "EXCEL")
                    {
                        return OutputExcel(outputData, sheetName);
                    }
                    else
                    {

                        var lst = outputData.AsEnumerable()
            .Select(r => r.Table.Columns.Cast<DataColumn>()
                    .Select(c => new KeyValuePair<string, object>(c.ColumnName, r[c.Ordinal])
                   ).ToDictionary(z => z.Key, z => z.Value)
            ).ToList();
                        //now serialize it

                        string json = JsonConvert.SerializeObject(lst, Formatting.Indented);

                        HttpResponseMessage resp = Request.CreateResponse(HttpStatusCode.OK);

                        resp.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                        return resp;
                    }
                }
                else
                {
                    log.WriteToLog("Data Source " + database + " Not Found");
                    throw new Exception("Data Source Not Found");
                }

            }
            else
            {
                log.WriteToLog("Data Source " + database + " Not Found");
                throw new Exception("Data Source Not Found");
            }
        }

        public static HttpResponseMessage DataSetGetter(string database, string query, string outputType, HttpRequestMessage Request, System.Security.Principal.IPrincipal user, String workbookName, List<String> worksheetNames = null)
        {

            Logger log = Services.Log.getLog(user);

            bool successParse = Enum.TryParse(database, out Data.AppNames dataSource);

            if (successParse)
            {
                if (dataSource == Data.AppNames.ExampleProd || dataSource == Data.AppNames.ExampleTest)
                {
                    DataSet outputData;
                    try
                    {
                        log.WriteToLog("Connecting to : " + dataSource.ToString() + " with query " + query);
                        outputData = ExtractFactory.ConnectAndQuery_Dataset(dataSource, query);
                    }
                    catch (Exception exc)
                    {
                        log.WriteToLog("Error Accessing Data: " + Environment.NewLine + exc.ToString());
                        throw exc;
                    }

                    if (outputType.ToUpper() == "EXCEL")
                    {
                        return OutputExcel(outputData, worksheetNames, workbookName);
                    }
                    else
                    {
                        List<Object> lst = new List<object>();

                        foreach (DataTable tbl in outputData.Tables)
                        {
                            var tblLst = tbl.AsEnumerable().Select(r => r.Table.Columns.Cast<DataColumn>().Select(c => new KeyValuePair<string, object>(c.ColumnName, r[c.Ordinal])).ToDictionary(z => z.Key, z => z.Value)).ToList();
                            lst.Add(tblLst);
                        }

                        string json = JsonConvert.SerializeObject(lst, Formatting.Indented);

                        HttpResponseMessage resp = Request.CreateResponse(HttpStatusCode.OK);

                        resp.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                        return resp;
                    }
                }
                else
                {
                    log.WriteToLog("Data Source " + database + " Not Found");
                    throw new Exception("Data Source Not Found");
                }

            }
            else
            {
                log.WriteToLog("Data Source " + database + " Not Found");
                throw new Exception("Data Source Not Found");
            }
        }

        private static HttpResponseMessage OutputExcel(DataTable tab, String sheetName)
        {
            string outputLoc = System.Web.Hosting.HostingEnvironment.MapPath(@"\ExcelOutputs\ExcelOutput" + DateTime.Now.ToString("yyyyMMddmmss") + ".xlsx");

            //ExcelWork.OutputDataTableToExcel(tab, sheetName, outputLoc);
            
            return ExcelResponse(outputLoc, sheetName);

        }

        private static HttpResponseMessage OutputExcel(DataSet tableSet, List<String> sheetNames, String workbookName)
        {
            String outputLocation = System.Web.Hosting.HostingEnvironment.MapPath(@"\ExcelOutputs\ExcelOutput" + DateTime.Now.ToString("yyyyMMddmmss") + ".xlsx");

            ExcelWork.OutputDataSetToExcel(tableSet, outputLocation, sheetNames);

            return ExcelResponse(outputLocation, workbookName);
        }

        private static HttpResponseMessage ExcelResponse(String outputLocation, String fileName)
        {
            byte[] stream = System.IO.File.ReadAllBytes(outputLocation);
            // processing the stream.

            var result = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(stream)
            };

            result.Content.Headers.ContentDisposition =
                new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                {
                    FileName = fileName + ".xlsx"
                };

            result.Content.Headers.ContentType =
                new MediaTypeHeaderValue("application/octet-stream");
            
            System.IO.File.Delete(outputLocation);

            return result;
        }
    }
}