using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace JobConfiguration.Models
{
    public class BulkUpdateTable
    {
        public String tableName { get; set; }
        public String tableSchema { get; set; }
        public String errorMessage { get; set; }
        public int currentStep { get; set; }

        public BulkUpdateTable(String tableName, String tableSchema, int currentStep = 1)
        {
            this.tableName = tableName;
            this.tableSchema = tableSchema;
            this.currentStep = currentStep;
        }
    }
}