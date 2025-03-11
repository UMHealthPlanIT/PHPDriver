using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace JobConfiguration.Models
{
    public class TableUpdate
    {
        public string TableName { get; set; }
        public string Schema { get; set; }
        public bool Attachment { get; set; } //This is exclusively for the Job Index custom view
        public Dictionary<object, object> PropertiesValues { get; set; }
        public String KeySelector { get; set; }
        public FoundTableDetails TableDetails { get; set; }
    }
}