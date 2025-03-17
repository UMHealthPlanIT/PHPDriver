using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utilities.Eight34Outputs
{
    public class Messages
    {
        public static List<String> ParseMessagesFromBlock(String largeText, String Separator, String End)
        {
            List<String> DistinctMessages = new List<String>();

            int marker = (largeText.IndexOf(Separator, 0) == -1 ? 0 : largeText.IndexOf(Separator, 0));
            int end = (largeText.IndexOf(End) == -1 ? largeText.IndexOf("A severe error occurred ") : largeText.IndexOf(End));
            while (largeText.IndexOf(Separator, marker) != -1)
            {
                int endOrNextKey = largeText.IndexOf(Separator, marker + 1) == -1 ? end : largeText.IndexOf(Separator, marker + 1);
                int startOfKey = largeText.IndexOf(Separator, marker);
                String errorMsg = largeText.Substring(startOfKey, endOrNextKey - startOfKey);
                marker = largeText.IndexOf(errorMsg, marker) + errorMsg.Length;
                DistinctMessages.Add(errorMsg);
            }
            return DistinctMessages;
        }

        public static void SendMessages(List<String> messages, String logType, String logDate, String logLink, Logger proclog, int ErrorCode, String errorDistribution = "")
        {
            if (messages.Count > 0)
            {
                String allMessages = "";

                foreach (String message in messages)
                {
                    allMessages = allMessages + message.ToString() + "\r\n";
                }

                String Subject = "Error messages from " + logType + " " + logDate + " report";

                String distribution = (errorDistribution == "" ? proclog.ProcessId : errorDistribution);

                SendAlerts.Send(distribution, ErrorCode, Subject, allMessages + "\r\n" + logLink, proclog);
            }
            else
            {
                String Subject = "There were no error messages in the " + logType + " Log for " + logDate;

                SendAlerts.Send(proclog.ProcessId + "a", 0, Subject, "The log we processed is here:" + System.Environment.NewLine + logLink, proclog);
            }
        }
    }
}
