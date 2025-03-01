using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using JobConfiguration.Models;
using Utilities;
using System.ComponentModel.DataAnnotations;

namespace JobConfiguration.Models
{
    public class MrDataJobs_C
    {
        [Required(ErrorMessage = "Program Code Number is Required.")]
        public string PROGRAMCODE { get; set; }

        public string OUTPUTFILENAME { get; set; }

        public string OUTPUTFILETYPE { get; set; }

        public string OUTPUTINTERFACE { get; set; }

        public string OUTPUTLOCATION { get; set; }

        [StringLength(1,ErrorMessage = "The delimiter can only be one character long. ")]
        public string DELIMITER { get; set; }

        public string CODE { get; set; }

        public string STOREDPROCEDURE { get; set; }

        public string TFSCODE { get; set; }

        public string DATASOURCE { get; set; }

        [Required(ErrorMessage = "Success email Subject is Required.")]
        public string SUCCESSSUBJECT { get; set; }

        [Required(ErrorMessage = "Success email Body is Required.")]
        public string SUCCESSBODY { get; set; }

        [Required(ErrorMessage = "Zero records found email Subject is Required.")]
        public string ZEROSUBJECT { get; set; }

        [Required(ErrorMessage = "Zero records found email body is Required.")]
        public string ZEROBODY { get; set; }

        [Required(ErrorMessage = "Zero exit code is Required.")]
        public int ZEROEXITCODE { get; set; }

        public bool NoHeaders { get; set; }
        public List<SelectListItem> FileTypes { get; set; }

        public List<SelectListItem> OutputInterface { get; set; }

        public List<SelectListItem> DataSource { get; set; }

        public List<SelectListItem> ZeroExitCode { get; set; }


        public MrDataJobs_C() { }

            public MrDataJobs_C(
                                string PROGRAMCODE,
                                string OUTPUTFILENAME,
                                string OUTPUTFILETYPE,
                                string OUTPUTINTERFACE,
                                string OUTPUTLOCATION,
                                string DELIMITER,
                                string CODE,
                                string STOREDPROCEDURE,
                                string TFSCODE,
                                string DATASOURCE,
                                string SUCCESSSUBJECT,
                                string SUCCESSBODY,
                                string ZEROSUBJECT,
                                string ZEROBODY,
                                int ZEROEXITCODE,
                                bool NoHeaders
                               )
        {
            this.PROGRAMCODE = PROGRAMCODE;
            this.OUTPUTFILENAME = OUTPUTFILENAME;
            this.OUTPUTFILETYPE = OUTPUTFILETYPE;
            this.OUTPUTINTERFACE = OUTPUTINTERFACE;
            this.OUTPUTLOCATION = OUTPUTLOCATION;
            this.DELIMITER = DELIMITER;
            this.CODE = CODE;
            this.STOREDPROCEDURE = STOREDPROCEDURE;
            this.TFSCODE = TFSCODE;
            this.DATASOURCE = DATASOURCE;
            this.SUCCESSSUBJECT = SUCCESSSUBJECT;
            this.SUCCESSBODY = SUCCESSBODY;
            this.ZEROSUBJECT = ZEROSUBJECT;
            this.ZEROBODY = ZEROBODY;
            this.ZEROEXITCODE = ZEROEXITCODE;
            this.NoHeaders = NoHeaders;
        }
    }
}