using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Mail;
using System.Data.SqlClient;
using System.Data.Linq;
using Outlook = Microsoft.Office.Interop.Outlook;
using Microsoft.Win32;
using System.DirectoryServices;
using System.IO;


namespace Utilities
{
    public static class SendAlerts
    {

        /// <summary>
        /// In the future this will be the method that pushes attachments to sharepoint for DotNet programs, for now it just passes control over to .Send
        /// </summary>
        /// <param name="ProcId">Process Id to Send Emails for</param>
        /// <param name="Scenario">4) Program completed with warnings; 1012) Program had an error; 0) Program completed successfully </param>
        /// <param name="emailSubject">Subject of the email message</param>
        /// <param name="message">Body of the email message</param>
        /// <param name="AttachmentUrl">URL of the item to attach to the email</param>
        /// <param name="SendToOverride">Override email for the send-to attribute (optional)</param>
        /// <param name="SendSecure"></param>
        public static void PublishAndNotify(String ProcId, int Scenario, String emailSubject, String message, Logger proclog, string AttachmentUrl = "", String SendToOverride = "", Boolean SendSecure = false)
        {

            try
            {
                String sharepointUrl = FileTransfer.PushToSharepoint("ITReports", ProcId, AttachmentUrl, proclog);
                Send(ProcId, Scenario, emailSubject, message + Environment.NewLine + @"Report available at """ + sharepointUrl + @"""", proclog);
            }
            catch (Exception e)
            {
                System.Threading.Thread.Sleep(30000);
                try
                {
                    String sharepointUrl = FileTransfer.PushToSharepoint("ITReports", ProcId, AttachmentUrl, proclog);
                    Send(ProcId, Scenario, emailSubject, message + Environment.NewLine + @"Report available at """ + sharepointUrl + @"""", proclog);
                }
                catch (Exception exc)
                {
                    Send(ProcId, 1012, "We errored trying to push to SharePoint", exc.ToString(), proclog);
                }

            }
        }

        public static void PublishAndNotify(String ProcId, int Scenario, String emailSubject, String message, Logger proclog, List<string> Attachments, String SendToOverride = "", Boolean SendSecure = false)
        {
            List<string> sharepointLocations = new List<string>();
            foreach (String s in Attachments)
            {
                sharepointLocations.Add(FileTransfer.PushToSharepoint("ITReports", ProcId, s, proclog));
            }

            Send(ProcId, Scenario, emailSubject, message + Environment.NewLine + FileTransfer.BuildSharepointBody(sharepointLocations), proclog, SendToOverride: SendToOverride, SendSecure: SendSecure);
        }

        /// <summary>
        /// Send an email with a single attachment to the recipients defined in Job Manager
        /// </summary>
        /// <param name="ProcId">Process Id to Send Emails for</param>
        /// <param name="Scenario">4) Program completed with warnings; 1012) Program had an error; 0) Program completed successfully </param>
        /// <param name="emailSubject">Subject of the email message</param>
        /// <param name="message">Body of the email message</param>
        /// <param name="AttachmentUrl">URL of the item to attach to the email</param>
        /// <param name="SendToOverride">Override email for the send-to attribute (optional)</param>
        /// <param name="SendSecure">Will input the 'shsencrypt' keyword in the subject of the email, implementing secure email functionality in exchange</param>
        /// <param name="BCC">Email address to BCC (will only send in production)</param>
        public static void Send(String ProcId, int Scenario, String emailSubject, String message, Logger proclog, string AttachmentUrl = "", String SendToOverride = "", Boolean SendSecure = false, Boolean htmlEmail = false, Boolean overrideRecipientInTest = false, string fromOverride = "", string BCC = "", Boolean includeJobCode = true, string fromDisplay = "", bool hideFooter = false)
        {
            List<String> attachmentsToSend = new List<String>();

            if (AttachmentUrl != "")
            {
                attachmentsToSend.Add(AttachmentUrl);
            }

            Send(ProcId, Scenario, emailSubject, message, proclog, attachmentsToSend, SendToOverride, SendSecure, htmlEmail, overrideRecipientInTest, fromOverride, BCC, includeJobCode, fromDisplay, hideFooter);
        }

        /// <summary>
        /// Send an email with multiple attachments to the recipients defined in Job Manager
        /// </summary>
        /// <param name="ProcId">Process Id to Send Emails for</param>
        /// <param name="Scenario">4) Program completed with warnings; 6000) Program had an error, still send email; 1012) Program had an error, no email; 0) Program completed successfully </param>
        /// <param name="emailSubject">Subject of the Email Message</param>
        /// <param name="message">Body of the email message</param>
        /// <param name="AttachmentUrl">URL of the item to attach to the email</param>
        /// <param name="SendToOverride">Override email address for the send-to attribute (optional)</param>
        /// <param name="SendSecure">Deprecated. All e-mails will be encrypted</param>
        /// <param name="htmlEmail">Are email contents HTML?</param>
        /// <param name="overrideRecipientInTest">Should SendToOverride work in test? (defaults to no)</param>
        /// <param name="fromOverride">Send from a different address (only works via SMTP)</param>
        /// <param name="fromDisplay">Set the display for the sender</param>
        /// <param name="hideFooter">Allows emails to send without adding the footer of the emails</param>
        /// <param name="includeJobCode">Send Email without the leading jobid in the subject</param>
        public static void Send(String ProcId, int Scenario, String emailSubject, String message, Logger proclog, List<String> AttachmentUrl, String SendToOverride = "", Boolean SendSecure = false, Boolean htmlEmail = false, Boolean overrideRecipientInTest = false, string fromOverride = "", string BCC = "", Boolean includeJobCode = true, string fromDisplay = "", bool hideFooter = false)
        {
            if (Scenario == 1012)
            {
                UniversalLogger.WriteToLog(proclog, emailSubject + Environment.NewLine + message, category: UniversalLogger.LogCategory.ERROR);
            }
            else
            {
                if (Scenario == 4 || Scenario == 6004) //6004 is the test mode version of a 6000 missing file error
                {
                    UniversalLogger.WriteToLog(proclog, emailSubject + Environment.NewLine + message, category: UniversalLogger.LogCategory.WARNING);

                }
                else if (Scenario == 6000)
                {
                    if (proclog.TestMode)
                    {
                        UniversalLogger.WriteToLog(proclog, emailSubject + Environment.NewLine + message, category: UniversalLogger.LogCategory.WARNING);
                    }
                    else
                    {
                        UniversalLogger.WriteToLog(proclog, emailSubject + Environment.NewLine + message, category: UniversalLogger.LogCategory.ERROR);
                    }
                    
                }


                //Determine if Outlook is installed
                Type officeType = Type.GetTypeFromProgID("Outlook.Application");

                String eSubject;
                if (includeJobCode)
                {
                    eSubject = ProcId + " " + emailSubject + " " + "shsencrypt" + " (" + Scenario.ToString() + ")";
                }
                else
                {
                    eSubject = emailSubject;
                }
                try
                {
                    if (officeType != null && Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName", "").ToString() == "Windows 10 Pro" && System.IO.Directory.GetCurrentDirectory().ToUpper() != @"C:\WINDOWS\SYSTEM32")
                    {
                        SendOutlook(ProcId, Scenario, eSubject, message, proclog, AttachmentUrl, SendToOverride, SendSecure = false, htmlEmail, overrideRecipientInTest, BCC);
                    }
                    else
                    {
                        SendSMTP(ProcId, Scenario, eSubject, message, proclog, AttachmentUrl, SendToOverride, SendSecure = false, htmlEmail, overrideRecipientInTest, fromOverride, fromDisplay, BCC, hideFooter);
                    }
                }
                catch (Exception exc)
                {
                    proclog.WriteToLog(exc.ToString());
                    String emailMessage = String.Format(@"Errored Trying to Send Email. Here is what we tried to tell you: 
                    Process ID: {0}
                    Exit Code: {1}
                    Subject Line: {2}
                    Body: {3}
                    Attachments: {4}", ProcId, Scenario, eSubject, message, String.Join(",", AttachmentUrl));
                    proclog.WriteToLog(emailMessage);
                }

            }
        }

        /// <summary>
        /// SendOutlook is used for Testing on local machines.  The permission to access the Exchange server to send SMTP messages is based on IP. Which is fine for the non-moving, 
        /// rarely-changing servers. To remove the need to have each laptop's IP added to the allowed list every time we get a new laptop or a new employee, 
        /// this was added so we have the ability for Driver to make use of a locally installed Outlook client.
        /// </summary>
        /// <param name="ProcId"></param>
        /// <param name="Scenario"></param>
        /// <param name="eSubject"></param>
        /// <param name="message"></param>
        /// <param name="proclog"></param>
        /// <param name="AttachmentUrl"></param>
        /// <param name="SendToOverride"></param>
        /// <param name="SendSecure"></param>
        /// <param name="htmlEmail"></param>
        /// <param name="overrideRecipientInTest"></param>
        /// <param name="BCC"></param>
        public static void SendOutlook(String ProcId, int Scenario, String eSubject, String message, Logger proclog, List<String> AttachmentUrl, String SendToOverride = "", Boolean SendSecure = false, Boolean htmlEmail = false, Boolean overrideRecipientInTest = false, string BCC = "")
        {
            Outlook.Application outlookApp = new Outlook.Application();
            Outlook.MailItem email = (Outlook.MailItem)outlookApp.CreateItem(Outlook.OlItemType.olMailItem);
            email.Subject = eSubject;

            proclog.WriteToLog("Email Subject: " + email.Subject + Environment.NewLine + "Email Body: " + message);

            if (proclog.TestMode && proclog.requestedBy != null)
            {
                proclog.WriteToLog("Requested User: " + proclog.requestedBy.Split('\\').Last());
            }
            else
            {
                proclog.WriteToLog("Requested User: " + System.Security.Principal.WindowsIdentity.GetCurrent().Name.Split('\\').Last());
            }

            Boolean Recipients = LoadRecipients(ProcId, Scenario, proclog, SendToOverride, overrideRecipientInTest, BCC, email);

            if (Recipients)
            {
                if (AttachmentUrl.Count > 0)
                {
                    proclog.WriteToLog("Attachments:");
                    foreach (String toAttach in AttachmentUrl)
                    {
                        proclog.WriteToLog(toAttach);
                        try
                        {
                            email.Attachments.Add(toAttach, Outlook.OlAttachmentType.olByValue, Type.Missing, Type.Missing);
                        }
                        catch (Exception exc)
                        {
                            try
                            {
                                email.Attachments.Add(toAttach, Outlook.OlAttachmentType.olByValue, Type.Missing, Type.Missing);
                            }
                            catch (System.IO.IOException ioExc)
                            {
                                proclog.WriteToLog("Could not access the file at : " + toAttach + Environment.NewLine + ioExc.ToString());

                                SendAlerts.Send(ProcId, 1012, "File Not Found Error", exc.ToString() + "\n" + ioExc.ToString(), proclog);
                            }
                            catch
                            {
                                throw exc;
                            }

                        }

                    }

                }

                if (htmlEmail)
                {
                    email.HTMLBody = message;
                }
                else
                {
                    email.Body = message;
                }

                email.Send();


            }


        }

        private static Boolean LoadRecipients(string ProcId, int Scenario, Logger proclog, string SendToOverride, bool overrideRecipientInTest, String BCC, dynamic email)
        {
            List<String> toNotify = GetRecipients(ProcId, Scenario, proclog, SendToOverride, overrideRecipientInTest);
            String emailRecipients = "";

            if (toNotify.Count == 0 && BCC == "")
            {
                proclog.WriteToLog("No Recipients Found");

                return false;
            }
            else
            {
                foreach (String nots in toNotify)
                {
                    emailRecipients += (nots + Environment.NewLine);
                    try
                    {
                        if (email is MailMessage)
                        {
                            email.To.Add(nots);
                        }
                        else
                        {
                            email.Recipients.Add(nots);
                        }
                    }
                    catch (Exception exc)
                    {
                        SendAlerts.Send(ProcId, 1012, "There was a problem with the email '" + nots + "' defined for this program", exc.ToString(), proclog);
                    }
                }


                proclog.WriteToLog("Email Recipients:" + emailRecipients + Environment.NewLine);

                if (BCC != "" && !proclog.TestMode)
                {
                    if (email is MailMessage)
                    {
                        email.Bcc.Add(BCC);
                    }
                    else
                    {
                        email.BCC = BCC;
                    }
                    proclog.WriteToLog("BCC: " + BCC);
                }

                return true;
            }


        }

        public static void SendSMTP(String ProcId, int Scenario, String eSubject, String message, Logger proclog, List<String> AttachmentUrl, String SendToOverride = "", Boolean SendSecure = false, Boolean htmlEmail = false, Boolean overrideRecipientInTest = false, string fromOverride = "", string fromDisplay = "", string BCC = "", bool hideFooter = false)
        {

            MailMessage email = new MailMessage();
            SmtpClient smtpClnt = new SmtpClient("shsmailgate.sparrow.org");
            email.Subject = eSubject;

            proclog.WriteToLog("Email Subject: " + email.Subject + Environment.NewLine + "Email Body: " + message);
            if (proclog.TestMode && proclog.requestedBy != null)
            {
                proclog.WriteToLog("Requested User: " + proclog.requestedBy.Split('\\').Last());
            }
            else
            {
                proclog.WriteToLog("Requested User: " + System.Security.Principal.WindowsIdentity.GetCurrent().Name.Split('\\').Last());
            }

            Boolean Recipients = LoadRecipients(ProcId, Scenario, proclog, SendToOverride, overrideRecipientInTest, BCC, email);

            if (Recipients)
            {
                if (AttachmentUrl.Count > 0)
                {
                    proclog.WriteToLog("Attachments:");
                    foreach (String toAttach in AttachmentUrl)
                    {
                        proclog.WriteToLog(toAttach);
                        System.Net.Mail.Attachment attachment;
                        try
                        {
                            attachment = new System.Net.Mail.Attachment(toAttach);
                            email.Attachments.Add(attachment);
                        }
                        catch (Exception exc)
                        {
                            try
                            {
                                attachment = new System.Net.Mail.Attachment(toAttach + ".xlsx");
                                email.Attachments.Add(attachment);
                                attachment.Dispose();
                            }
                            catch (System.IO.IOException ioExc)
                            {
                                proclog.WriteToLog("Could not access the file at : " + toAttach + Environment.NewLine + ioExc.ToString());
                            }
                            catch
                            {
                                throw exc;
                            }

                        }

                    }

                }

                if (fromDisplay != "")
                {
                    if (fromOverride != "")
                    {
                        email.From = new MailAddress(fromOverride, fromDisplay);
                    }
                    else
                    {
                        email.From = new MailAddress("DoNotReply@.org", "DONOTREPLY");

                    }
                }
                else
                {
                    if (fromOverride != "")
                    {
                        email.From = new MailAddress(fromOverride);
                    }
                    else
                    {
                        email.From = new MailAddress("DoNotReply@.org", "DONOTREPLY");

                    }
                }
                

                
                if (!hideFooter)
                {
                    if (htmlEmail)
                    {
                        email.Body = message + "<br><br><br><br>If you have a request related to this email, please submit that through the "
                            +"<a href=\"https://.-.com/\">Service Portal</a> and reference the JobId: " + ProcId + ". <br><br>RefId: " + proclog.UniqueID;
                    }
                    else
                    {
                        email.Body = message + "\n\n\n\nIf you have a request related to this email, please submit that through the "
                            + "Service Portal https://.-.com/ and reference the JobId: " + ProcId + ". \n\nRefId: " + proclog.UniqueID;
                    }
                }
                else
                {
                    email.Body = message;
                }

                //I think that there is a scenario where we do not have shsencrypt to the subject 

                //email.Body = email.Body + "\n\n\n\nshsencrypt";



                if (htmlEmail)
                {
                    email.IsBodyHtml = true;
                }

                email.Headers.Add("X-Auto-Response-Suppress", "All");

                smtpClnt.EnableSsl = false;

                smtpClnt.Send(email);
                smtpClnt.Dispose();
                email.Dispose();

            }


        }

        public static void SendAdminEmail(String ProcId, int ErrorCode, String emailSubject, String message, Boolean TestMode)
        {
            //Determine if Outlook is installed
            Type officeType = Type.GetTypeFromProgID("Outlook.Application");
            String eSubject = ProcId + " " + emailSubject + " " + "shsencrypt" + " (" + ErrorCode.ToString() + ")";
            List<String> AttachmentUrl = new List<String>();

            if (officeType != null && System.Environment.OSVersion.ToString() != "Microsoft Windows NT 6.2.9200.0" && System.IO.Directory.GetCurrentDirectory().ToUpper() != @"C:\WINDOWS\SYSTEM32")
            {
                SendOutlook(ProcId, ErrorCode, eSubject, message, new Logger("Admin", TestMode), AttachmentUrl);
            }
            else
            {
                SendSMTP(ProcId, ErrorCode, eSubject, message, new Logger("Admin", TestMode), AttachmentUrl);
            }

        }

        private static List<String> GetRecipients(String ProcId, int Scenario, Logger procLog, String SendToOverride = "", Boolean overrideRecipientInTest = false)
        {

            //Get current user's e-mail address from AD
            List<String> toNotify = new List<String>();
            string userName = System.Security.Principal.WindowsIdentity.GetCurrent().Name.Split('\\').Last();
            if (procLog.TestMode && procLog.requestedBy != null && procLog.requestedBy.ToUpper() != "LSFUSERSCHEDULE")
            {
                userName = procLog.requestedBy.Split('\\').Last();
            }


            //if you've manually told us to override the recipient, and this is being run by LSFUser
            //new! if you have the override in test flag, it'll do that now too
            if (SendToOverride != "" && (!procLog.TestMode || overrideRecipientInTest))
            {
                toNotify.Add(SendToOverride);
            }
            //if this isn't being run by LSFUser or the sql service account
            else if (!userName.ToLower().Contains("lsf") && !userName.ToUpper().StartsWith("SVC-"))
            {
                SearchResult result = Integrations.ActiveDirectory.GetPersonFromActiveDirectory(userName);
                try
                {
                    toNotify.Add(result.Properties["mail"][0].ToString());
                }
                catch (Exception exc)
                {
                    throw new Exception("There isn't an email address defined on this user");
                }

            }
            else if (!procLog.TestMode || overrideRecipientInTest)
            {
                toNotify = ExtractFactory.ConnectAndQuery<string>(Data.AppNames.ExampleProd, @"Select Recipient from dbo.EmailDistribution_C where ProgramCode = '" + ProcId + @"'").ToList();
            }

            if (toNotify.Count() == 0)
            {
                //throw new Exception("failed to find an address to send to, check job manager record for this process or your active directory settings");
                UniversalLogger.WriteToLog(procLog, "No recipients were found for this e-mail", category: UniversalLogger.LogCategory.WARNING);
            }

            return toNotify;
        }

        /// <summary>
        /// The call made to update the Rhapsody email body dynamically.
        /// </summary>
        /// <param name="proclog"></param>
        /// <param name="recordcount">Total amount of records found</param>
        public static void UpdateRhapsodyBodyCount(Logger proclog, int recordcount)
        {
            string Message = "There were " + recordcount + " records in the extract pushed to the ftp site.";
            Message += Environment.NewLine + "Please contact .@sparrow.org if you have any questions.";
            string UpdateQuery = string.Format(@"UPDATE [dbo].[]
                    SET [Body] = '{0}'
                    WHERE ProgramCode='{1}'", Message, proclog.ProcessId);
            DataWork.RunSqlCommand(UpdateQuery, proclog.LoggerPhpConfig);
        }

        /// <summary>
        /// The call made to update the Rhapsody email subject dynamically.
        /// </summary>
        /// <param name="ProgramCode">The program that ran this.</param>
        /// <param name="proclog"></param>
        /// <param name="Subject">Dynamic text for the Subject</param>
        public static void UpdateRhapsodySubjectCount(string ProgramCode, Logger proclog, string Subject)
        {
            string UpdateQuery = string.Format(@"UPDATE [dbo].[]
                    SET [] = '{0}'
                    WHERE ProgramCode='{1}'", Subject, ProgramCode);
            DataWork.RunSqlCommand(UpdateQuery, proclog.LoggerPhpConfig);
            proclog.WriteToLog("Updated Rhapsody email configuration");
        }

        private static string RedirectAttachements(Logger log, List<string> attachements, string email)
        {
            string emailBodyLinks = "";
            List<string> updatedAttachments = new List<string>();

            if (IsInternalAddress(email))
            {
                string recipientUserId = GetUserFromEmail(email);

                if (recipientUserId != "")
                {
                    emailBodyLinks = "The attached file(s) have been moved to a secure location and are directly downloadable by clicking on the links provided below.\r\n\r\n";

                    foreach (string filePath in attachements)
                    {
                        emailBodyLinks += Path.GetFileName(filePath) + " : " + ProcessAttachment(log, filePath, recipientUserId) + "\r\n";
                    }
                }
                else
                {
                    log.WriteToLog("No user ID was found for the e-mail address " + email + ". As a consequence attachements will be left in the e-mail instead of redirected.", UniversalLogger.LogCategory.WARNING);
                }
            }

            return emailBodyLinks;
        }

        /// <summary>
        /// Save files in short-term storage, give user permission to download the file via the API and return the URL that will be used for download
        /// </summary>
        /// <param name="log">Logger process accessing this method</param>
        /// <param name="filePath">Path to file to be converted</param>
        /// <param name="user">User who will be granted access</param>
        /// <param name="weeksToLive">Number of weeks until file will expire</param>
        /// <returns></returns>
        private static string ProcessAttachment(Logger log, string filePath, string user, int weeksToLive = 1)
        {
            string server = "https://..org/api//api/";
            string shortTermStorage = @"\\.org\dfs\\JobOutput\FileAttachmentRetrievalStorage\";
            string fileName = Path.GetFileName(filePath);
            int daysToLive = weeksToLive * 7;

            if (!log.TestMode)
            {
                server = "https://..org/api//api/";
                shortTermStorage = @"\\.org\dfs\\JobOutput\FileAttachmentRetrievalStorage\";
            }

            Guid newFileId = Guid.NewGuid();
            string downloadURL = server + "FileAttachmentRetrieval/DownloadFile?GUID=" + newFileId.ToString();

            //Copy file to Short-term storage
            File.Copy(filePath, shortTermStorage + newFileId.ToString() + ".");

            //Create record giving user access
            ExtractFactory.ConnectAndQuery(log, log.LoggerPhpConfig, InsertRecordQuery(newFileId.ToString(), fileName, user, weeksToLive));

            return downloadURL;
        }

        /// <summary>
        /// Determine if e-mail address is internal to Sparrow
        /// </summary>
        /// <param name="email">E-mail address to evaluate</param>
        /// <returns>true or false</returns>
        private static bool IsInternalAddress(string email)
        {
            bool isInternal = false;

            int atIndex = email.IndexOf("@") + 1;
            string domain = email.Substring(atIndex, email.Length - atIndex);
            List<string> internalDomains = new List<string>
    {
        "Example@ex.biz"
    };

            isInternal = internalDomains.Contains(domain.ToLower());

            return isInternal;
        }

        /// <summary>
        /// Create a record in the database table that will allow user to retrieve file from the API link
        /// </summary>
        /// <param name="guid">Unique identifier for this instance of this file</param>
        /// <param name="filename">Name of the file being saved</param>
        /// <param name="user">User who will be granted access</param>
        /// <param name="weeksToLive">Number of weeks until file will expire</param>
        /// <returns></returns>
        private static string InsertRecordQuery(string guid, string filename, string user, int weeksToLive)
        {
            return $@"INSERT INTO [PHPConfg].[dbo].[WEB0068_FileAttachmentRetrieval_C]
              VALUES(
             '{guid}', 
             '{filename}', 
             '{user}', 
              DATEADD(week, {weeksToLive}, GETDATE()))";
        }

        /// <summary>
        /// Find the AD user ID associated with this e-mail address
        /// </summary>
        /// <param name="email">E-mail address to evaluate</param>
        /// <returns>AD User ID</returns>
        private static string GetUserFromEmail(string email)
        {
            string userID = "";

            string convertedEmail = ConvertEmail(email);

            using (DirectoryEntry entry = new DirectoryEntry($"LDAP://OU= Users,DC=,DC=org"))
            {
                var search = new DirectorySearcher(entry)
                {
                    Filter = $"(&(objectCategory=person)(objectClass=user)(memberOf=*)(mail={convertedEmail}))"
                };

                search.PropertiesToLoad.Add("SamAccountName");

                SearchResult results = search.FindOne();

                try
                {
                    userID = @"\" + results.Properties["SamAccountName"][0].ToString();
                }
                catch (NullReferenceException ex)
                {
                    //We didn't find an AD account with this e-mail, and will handle this higher in the process
                }

            }

            return userID;
        }

        /// <summary>
        /// Check if domain in address is .org and convert to .org if it is
        /// </summary>
        /// <param name="email">E-mail address to evaluate</param>
        /// <returns>Proper, non-sparrow.org, e-mail address</returns>
        private static string ConvertEmail(string email)
        {
            string convertedEmail = email;

            int atIndex = email.IndexOf("@") + 1;
            string domain = email.Substring(atIndex, email.Length - atIndex);
            if (domain == ".org")
            {
                string name = email.Substring(0, email.IndexOf("@"));
                convertedEmail = name + "@.org";
            }

            return convertedEmail;
        }
    }


}
