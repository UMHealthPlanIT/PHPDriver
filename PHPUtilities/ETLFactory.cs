using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Linq.Mapping;
using System.Data;
using System.Xml;
using System.Threading;
using System.Threading.Tasks;

namespace Utilities
{

    public class ETLFactory
    {
        Logger msDataLog;
        public ETLFactory(string programCode, Logger caller)
        {
            Logger.LaunchRequest jobIdLaunchRequest = new Logger.LaunchRequest(programCode, caller.TestMode, null, caller.UniqueID);
            Logger jobId = new Logger(jobIdLaunchRequest);
            msDataLog = jobId;
            this.ListOfRequests = ExtractFactory.ConnectAndQuery<ETLRequest>(msDataLog.LoggerPhpConfig, "select * from dbo.MsDataJobs_C where ProgramCode LIKE '" + programCode + "%'").ToList(); //get a, b, c etc versions of job if they exist
            ParallelOptions options = new ParallelOptions();
            if (ListOfRequests.Count == 0)
            {
                throw new Exception("Program Code " + programCode + " Not Found in MrDataJobs_C Table");
            }
            options.MaxDegreeOfParallelism = ListOfRequests.Count();
            Parallel.ForEach(ListOfRequests.AsEnumerable(), options, request =>
            {
                if (request.ActionToTake == "INSERT") //don't need to "pre-load" anything if we're doing a delete
                {
                    msDataLog.WriteToLog("Getting data for " + request.ProgramCode + "...");
                    string query = Integrations.SourceControl.GenerateSqlQueryFromTfs(request.TFSPath, true, null, request.TFSLocation);
                    try
                    {
                        request.SQLData = ExtractFactory.ConnectAndQuery(Data.GetDataSource(request.SourceDB, msDataLog), query);
                    }
                    catch(Exception e)
                    {
                        msDataLog.WriteToLog("Error in " + request.ProgramCode + ": " + e.ToString(), UniversalLogger.LogCategory.ERROR);
                        throw new Exception("Error in " + request.ProgramCode + ", ceasing all queries associated with this Ms. Data job");
                    }
                    msDataLog.WriteToLog("Got data for " + request.ProgramCode + ".");
                }
            });
            foreach (ETLRequest request in ListOfRequests.OrderBy(r => r.RunOrder))
            {
                if (request.ActionToTake == "INSERT")
                {
                    msDataLog.WriteToLog("Inserting for " + request.ProgramCode + "...");
                    DataWork.SaveDataTableToDb(request.DestTable, request.SQLData, Data.GetDataSource(request.DestDB, msDataLog));
                    msDataLog.WriteToLog("Inserted for " + request.ProgramCode + " successfully.");
                }
                else
                {
                    msDataLog.WriteToLog("Deleting for " + request.ProgramCode + "...");
                    DataWork.RunSqlCommand(Integrations.SourceControl.GenerateSqlQueryFromTfs(request.TFSPath, true, null, request.TFSLocation), Data.GetDataSource(request.SourceDB, msDataLog));
                    msDataLog.WriteToLog("Deleted for " + request.ProgramCode + ".");
                }
            }
            msDataLog.WriteToLog("All requests completed");
        }
        public List<ETLRequest> ListOfRequests { get; set; }
    }

    [Table(Name = "MsDataJobs_C")]
    public class ETLRequest
    {
        [Column]
        public string ProgramCode { get; set; }
        [Column]
        public string TFSPath { get; set; }
        [Column]
        public string SourceDB { get; set; }
        [Column]
        public string DestDB { get; set; }
        [Column]
        public string DestTable { get; set; }
        [Column]
        public string ActionToTake { get; set; }
        [Column]
        public int? RunOrder { get; set; }
        public bool overrideTestMode { get; set; }
        public string requestedBy { get; set; }
        public DataTable SQLData { get; set; }
        public string TFSLocation
        {
            get
            {
                return "";
            }
        }
    }

}
