using System;
using System.ComponentModel.DataAnnotations;

namespace DataStationApi.Models
{
    /*
     *      If this model gets updated, be sure to update the corresponding model and API Service in Plumage!!
     */

    public class ULogEntryModel
    {
        [Required(AllowEmptyStrings = false)]
        [MaxLength(50)]
        public String JobIndex { get; set; }
        
        [Required]
        public DateTime LogDateTime { get; set; }

        [Required(AllowEmptyStrings = false)]
        [MaxLength(20)]
        public String LogCategory { get; set; }

        [Required(AllowEmptyStrings = false)]
        [MaxLength(30)]
        public String LoggedByUser { get; set; }

        private String _LogContent { get; set; }
        [Required(AllowEmptyStrings = false)]
        public String LogContent
        {
            get
            {
                if(this._LogContent == null || this._LogContent == "")
                {
                    return "This space left blank.";
                }
                else
                {
                    return this._LogContent;
                }
            }
            set
            {
                this._LogContent = value;
            }
        }

        [MaxLength(36)]
        public String UID { get; set; }
        
        public bool Remediated { get; set; }

        [MaxLength(250)]
        public String RemediationNote { get; set; }
    }
}