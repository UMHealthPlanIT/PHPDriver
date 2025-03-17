using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Linq.Mapping;
using System.Data;
using System.Xml;
using System.Threading;

namespace Utilities
{

    public class ExtractFactory2
    {
        System.Threading.CountdownEvent complete;
        int extractCountDown;
        Logger mrDataLog;
        public ExtractFactory2(string programCode, Logger caller)
        {
            Logger.LaunchRequest jobIdLaunchRequest = new Logger.LaunchRequest(programCode, caller.TestMode, null, uid: caller.UniqueID);
            Logger jobId = new Logger(jobIdLaunchRequest); jobId.UniqueID = caller.UniqueID;
            mrDataLog = jobId;
            //NOTE: This hard-coded reference to PHPCONFG prod is intentional, we want to pull prod job configuration regardless
            this.ListOfReports = ExtractFactory.ConnectAndQuery<GenericExtractGenerator>(Data.AppNames.ExampleProd, "select * from dbo.MrDataJobs_C where ProgramCode LIKE '" + programCode + "%'").ToList(); //get a, b, c etc versions of job if they exist

            if (ListOfReports.Count == 0)
            {
                throw new Exception("Program Code " + programCode + " Not Found in MrDataJobs_C Table");
            }

            extractCountDown = this.ListOfReports.Count;

            complete = new CountdownEvent(extractCountDown);

            Console.WriteLine("ExtractCountDown Starts At: " + complete.CurrentCount);

            //Execute with given parameters and appropriate outputs
            foreach (GenericExtractGenerator extract in this.ListOfReports)
            {
                extract.overrideTestMode = caller.TestMode;
                extract.requestedBy = caller.requestedBy;
                mrDataLog.WriteToLog("Mr Data Record:" + extract.ProgramCode + ", " + extract.OutputFileName + " thread started");
                ThreadPool.QueueUserWorkItem(ThreadDoer, extract);
                Thread.Sleep(10000); //wait for 10 seconds to avoid conflict with log file creation (since it uses a timestamp in filename)
            }

            complete.Wait();
            mrDataLog.WriteToLog("All extracts completed");
        }
        public List<GenericExtractGenerator> ListOfReports { get; set; }

        public void ThreadDoer(Object threadContext)
        {
            GenericExtractGenerator mrDataExtract = (GenericExtractGenerator)threadContext;

            try
            {
                mrDataExtract.GenerateReport(mrDataLog.UniqueID);
            }
            catch (Exception exc)
            {
                mrDataLog.WriteToLog("There was an error generating file " + mrDataExtract.OutputFileName + Environment.NewLine + exc.ToString(), UniversalLogger.LogCategory.ERROR);
            }

            complete.Signal();
        }

    }

    [Table(Name = "MrDataJobs_C")]
    public class GenericExtractGenerator
    {
        Logger _program;
        public void GenerateReport(string uId)
        {

            Logger.LaunchRequest launchRequest = new Logger.LaunchRequest(this.ProgramCode, this.overrideTestMode, null, this.requestedBy, uid: uId);
            Logger program = new Logger(launchRequest);
            _program = program;
            string outFile = program.LoggerOutputYearDir + this.OutputFileName;
            DataTable fileLoaded = new DataTable();
            string tfsQuery = "";

            if (this.Code == null || this.Code == "")
            {
                if (this.StoredProcedure == null || this.StoredProcedure == "")
                {

                    try
                    {

                        tfsQuery = Utilities.Integrations.SourceControl.GenerateSqlQueryFromTfs("$/SQLQueries/MrDataQueries/" + this.TFSCode, true, null, "IT-Lab");
                    }
                    catch
                    {
                        throw new Exception("TFS Query not found. Expected query: " + this.TFSCode);
                    }


                    fileLoaded = ExtractFactory.ConnectAndQuery(Data.GetDataSource(this.DataSource, this._program), tfsQuery);
                }
                else
                {
                    fileLoaded = ExtractFactory.ConnectAndQuery(Data.GetDataSource(this.DataSource, this._program), "EXEC " + this.StoredProcedure);
                }
            }
            else
            {
                fileLoaded = ExtractFactory.ConnectAndQuery(Data.GetDataSource(this.DataSource, this._program), this.Code);
            }

            if (fileLoaded.Rows.Count > 0 || AlwaysCreateFile == true)
            {

                //Output file type
                if (this.OutputFileType.ToUpper() == "EXCEL")
                {

                    fileLoaded = FixXMLCharacters(fileLoaded);

                    //ExcelWork.OutputDataTableToExcel(fileLoaded, this._OutputFileName.Truncate(31), outFile);

                }
                else if (this.OutputFileType.ToUpper() == "TEXT" || this.OutputFileType.ToUpper() == "CSV")
                {

                    Boolean textQualifier = false;
                    if (this.Delimiter == "\\t")
                    {
                        this.Delimiter = this.Delimiter.Replace("\\t", "\t");
                    }
                    else if (this.Delimiter == @""",")
                    {
                        textQualifier = true;
                        this.Delimiter = ",";
                    }


                    Boolean Headers = (this.NoHeaders == "Y" ? false : true);
                    //Want to pull RunTextExtract out of original Extract factory to facilitate a single query design

                    OutputFile.WriteSeparated(System.IO.Path.GetFileName(outFile), fileLoaded, this.Delimiter, program, Headers, false, textQualifier);


                }


                //Output interface
                if (this.OutputInterface.ToUpper() == "SHAREPOINT")
                {
                    String sharepointUrl = FileTransfer.PushToSharepoint("ITReports", this.ProgramCode, outFile, program);
                    SendAlerts.Send(this.ProgramCode, 0, this.SuccessSubject, @"Report available at """ + sharepointUrl + @"""", program);
                }
                else if (this.OutputInterface.ToUpper() == "FTP")
                {
                    List<string> outFiles = new List<string>();
                    outFiles.Add(outFile);
                    FileTransfer.DropToPhpDoorStep(program, outFiles);
                }
                else if (this.OutputInterface.ToUpper() == "EMAIL")
                {
                    SendAlerts.Send(this.ProgramCode, 0, this.SuccessSubject, this.SuccessBody, program, outFile, SendSecure: true);
                }
                else if (this.OutputInterface.ToUpper() == "NETWORKSHARE")
                {
                    if (_program.TestMode)
                    {
                        program.WriteToLog("Not outputting to share b/c we're in test mode");
                    }
                    else
                    {
                        program.WriteToLog("Writing out to " + this.OutputLocation + " because we're in prod mode");
                        FileSystem.ReportYearDir(this.OutputLocation);

                        System.IO.File.Copy(outFile, this.OutputLocation + @"\" + System.IO.Path.GetFileName(outFile), true);
                    }

                    SendAlerts.Send(this.ProgramCode, 0, this.SuccessSubject, this.SuccessBody + Environment.NewLine + " File Output To " + this.OutputLocation, program, SendSecure: true);
                }
                else if (this.OutputInterface.ToUpper() == "WEB")
                {
                    UniversalLogger.WriteToLog(_program, outFile, requestedBy, UniversalLogger.LogCategory.AUDIT);
                }
            }
            else
            {
                int ZeroExitCodeModifiedForEnv;

                if (this.overrideTestMode && this.ZeroExitCode == "6000")
                {
                    ZeroExitCodeModifiedForEnv = 6004;
                }
                else
                {
                    ZeroExitCodeModifiedForEnv = Convert.ToInt16(this.ZeroExitCode);
                }
                SendAlerts.Send(this.ProgramCode, ZeroExitCodeModifiedForEnv, this.ZeroSubject, this.ZeroBody, program);
            }
        }

        private static DataTable FixXMLCharacters(DataTable tableToFix)
        {

            foreach (DataRow row in tableToFix.Rows)
            {
                foreach (DataColumn col in tableToFix.Columns)
                {
                    if (col.DataType == typeof(string))
                    {
                        row[col.ColumnName] = RemoveTroublesomeCharacters(row[col.ColumnName].ToString());
                    }
                }
            }

            return tableToFix;
        }

        private static string RemoveTroublesomeCharacters(string inString)
        {
            if (inString == null) return null;

            StringBuilder newString = new StringBuilder();
            char ch;

            for (int i = 0; i < inString.Length; i++)
            {
                ch = inString[i];
                if (XmlConvert.IsXmlChar(ch))
                {
                    newString.Append(ch);
                }
            }
            return newString.ToString();

        }

        [Column]
        public string ProgramCode { get; set; }
        private string _OutputFileName { get; set; }
        [Column]
        public string OutputFileName
        {
            get
            {
                String extension = "";

                if (this.OutputFileType == "EXCEL")
                {
                    extension = ".xlsx";
                    return this.ProgramCode + "_" + this._OutputFileName + "_" + DateTime.Today.ToString("yyyyMMdd") + extension;
                }
                else if (this.OutputFileType == "TEXT")
                {
                    extension = ".txt";

                    return this._OutputFileName + "_" + DateTime.Today.ToString("yyyyMMdd") + extension;
                }
                else if (this.OutputFileType == "CSV")
                {
                    extension = ".csv";

                    return this._OutputFileName + "_" + DateTime.Today.ToString("yyyyMMdd") + extension;
                }
                else
                {
                    return this.ProgramCode + "_" + this._OutputFileName + "_" + DateTime.Today.ToString("yyyyMMdd") + extension;
                }


            }

            set
            {
                this._OutputFileName = value;
            }
        }
        [Column]
        public string OutputFileType { get; set; }
        [Column]
        public string OutputInterface { get; set; }
        [Column]
        public string OutputLocation { get; set; }
        [Column]
        public string Delimiter { get; set; }
        [Column]
        public string Code { get; set; }
        [Column]
        public string StoredProcedure { get; set; }
        [Column]
        public string TFSCode { get; set; }
        [Column]
        public string SuccessSubject { get; set; }
        [Column]
        public string SuccessBody { get; set; }
        [Column]
        public string ZeroSubject { get; set; }
        [Column]
        public string ZeroBody { get; set; }
        [Column]
        public string ZeroExitCode { get; set; }
        [Column]
        public string DataSource { get; set; }
        [Column]
        public string NoHeaders { get; set; }
        [Column]
        public bool AlwaysCreateFile { get; set; }
        public bool overrideTestMode { get; set; }
        public string requestedBy { get; set; }
    }

}
