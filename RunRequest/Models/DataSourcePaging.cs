using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace RunRequest.Models
{
    public class DataSourcePaging
    {
        public string Name { get; set; }
        public string ReadyTimeStart { get; set; }
        public string ReadyTimeEnd { get; set; }
        public int ConsecutiveNotReady { get; set; }
        public int NumberOfNotReadyUntilPage { get; set; }
        public int TimesPagedOncall { get; set; }
        public string LastPaged { get; set; }
        public int MaxPages { get; set; }
        public bool Pageable { get; set; }
    }
}