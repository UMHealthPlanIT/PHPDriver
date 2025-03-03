using System;
using System.ComponentModel.DataAnnotations;

namespace RunRequest.Models
{
    public class LogEntryModel
    {
        [Required(AllowEmptyStrings = false)]
        [MaxLength(10)]
        public String JobIndex { get; set; }

        [Required]
        public DateTime LogDateTime { get; set; }

        [Required(AllowEmptyStrings = false)]
        [MaxLength(20)]
        public String LogCategory { get; set; }

        [Required(AllowEmptyStrings = false)]
        [MaxLength(30)]
        public String LoggedByUser { get; set; }

        [Required(AllowEmptyStrings = false)]
        public String LogContent { get; set; }

        public bool Remediated { get; set; }

        [MaxLength(250)]
        public String RemediationNote { get; set; }
    }
}