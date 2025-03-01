using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace JobConfiguration.Models
{
    public class EmailParameters
    {
        public string Recipients { get; set; }
        public string CopyRecipients { get; set; } = "";
        public string BlindCopyRecipients { get; set; } = "";
        public string From { get; set; }
        public string Subject { get; set; }
        public string EmailBody { get; set; }
        public bool HtmlEmail { get; set; } = false;
        public List<string> Attachments { get; set; }
        public bool EncryptEmail { get; set; } = true;
        public string WebIndex { get; set; }

        public EmailParameters(string recipients, string subject, string body, bool html)
        {
            Recipients = recipients;
            Subject = subject;
            EmailBody = body;
            HtmlEmail = html;
        }
    }
}