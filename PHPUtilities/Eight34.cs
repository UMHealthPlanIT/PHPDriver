using Utilities;
using Utilities.Eight34Outputs;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Utilities
{

    public class Eight34
    {
        /// <summary>
        /// Loads the provided 834 file to the given database
        /// </summary>
        /// <param name="inputFile">834 file to load</param>
        /// <param name="caller">Calling program</param>
        /// <param name="targetDb">Target database to load to. Note: we will automatically load to the table known as "Output" in the database</param>
        /// <param name="internalGroupId">Internal group identifier for Facets</param>
        /// <param name="fieldDelimiter">834/EDI field delimiter</param>
        /// <param name="vendor">Specifically for HealthTrio/Softheon processor - only used for dual compatability within IT_0346</param>
        /// <returns></returns>
        /// <exception cref="EmptyFileException"></exception>
        public static EightThirtyFourHeader LoadM834TransactionDetailsToDatabase(string inputFile, Logger caller, Data.AppNames targetDb, string internalGroupId, string fieldDelimiter, string vendor = "")
        {
            EightThirtyFourFile eitThirFrFile = Load834InMemory(inputFile, caller, fieldDelimiter, internalGroupId, vendor);

            if (eitThirFrFile.receivedTrans.Count <= 1)
            {
                if (eitThirFrFile.receivedTrans[0].FirstName == null)
                {
                    throw new EmptyFileException(inputFile);
                }
            }

            String outputTable = caller.ProcessId == "IT_0346" ? "IT0346_Output_F" : "Output";

            DataTable getData = DataWork.GetTableSchema(outputTable, targetDb);

            DataWork.TruncateWorkTable(outputTable, targetDb);
            int counter = 1;
            foreach (EightThirtyFourReceived tran in eitThirFrFile.receivedTrans)
            {
                DataRow row = getData.NewRow();

                List<PropertyInfo> props = tran.GetType().GetProperties().ToList();

                foreach (PropertyInfo prop in props)
                {
                    if (internalGroupId == "" || internalGroupId == "")
                    {
                        if (prop.Name == "IsPCPBlock" || prop.Name == "IsLxBlock") //these are flags leveraged only in the read-in of the 834
                        {
                            continue;
                        }
                        if (prop.Name == "ContactInfo")
                        {
                            LoadContactInformation(tran, row, internalGroupId);
                        }
                        else if (prop.Name == "PremiumDtls")
                        {
                            if (tran.PremiumDtls.Exists(x => x.ValueType == "APTC AMT"))
                            {
                                string aptcAmount = tran.PremiumDtls.Where(x => x.ValueType == "APTC AMT").First().Value;
                                row["AptcAmount"] = aptcAmount;

                                if (Convert.ToDecimal(aptcAmount) > 0)
                                {
                                    row["AptcIndicator"] = "Y";
                                }
                                else
                                {
                                    row["AptcIndicator"] = "N";
                                }
                            }
                            else
                            {
                                row["AptcIndicator"] = "N";
                            }

                            if (tran.PremiumDtls.Exists(x => x.ValueType == "CSR AMT"))
                            {
                                row["CsrAmount"] = tran.PremiumDtls.Where(x => x.ValueType == "CSR AMT").First().Value;
                            }

                            //Add premium totals to table
                            if (tran.PremiumDtls.Exists(x => x.ValueType == "PRE AMT TOT"))
                            {
                                row["TotalPremium"] = tran.PremiumDtls.Where(x => x.ValueType == "PRE AMT TOT").First().Value;
                            }
                            if (tran.PremiumDtls.Exists(x => x.ValueType == "PRE AMT 1"))
                            {
                                row["PremiumAmount"] = tran.PremiumDtls.Where(x => x.ValueType == "PRE AMT 1").First().Value;
                            }
                            if (tran.PremiumDtls.Exists(x => x.ValueType == "TOT RES AMT"))
                            {
                                row["TotalResponsible"] = tran.PremiumDtls.Where(x => x.ValueType == "TOT RES AMT").First().Value;
                            }

                            if (tran.PremiumDtls.Exists(x => x.ValueType == "RATING AREA"))
                            {
                                row["SubscriberRatingArea"] = tran.PremiumDtls.Where(x => x.ValueType == "RATING AREA").First().Value;
                            }

                        }
                        else
                        {
                            string propName = prop.Name;

                            if (getData.Columns.IndexOf(propName) != -1)
                            {
                                // Can create new property called Output and ErrCode for checking and load
                                row[prop.Name] = prop.GetValue(tran);
                            }


                        }
                    }

                    else
                    {

                        if (prop.Name == "ContactInfo")
                        {
                            LoadContactInformation(tran, row, internalGroupId);
                        }
                        else
                        {
                            if (getData.Columns.IndexOf(prop.Name) != -1)
                            {
                                row[prop.Name] = prop.GetValue(tran);
                            }
                        }
                    }
                    //do this for 346 and 354
                    if (prop.Name == "EarliestEffDate")
                    {
                        row["EarliestEffDate"] = tran.CovEffDate;
                    }
                    else if (prop.Name == "MemberRecordNumber")
                    {
                        row["MemberRecordNumber"] = tran.MemberRecordNumber;
                    }
                }

                row["GroupNo"] = internalGroupId;
                row["FileDate"] = eitThirFrFile.header.FileDate;

                if (caller.ProcessId == "IT_0354")
                {
                    row["FileTransactionSet"] = eitThirFrFile.header.FileTransactionSet;
                    row["ReferenceID"] = eitThirFrFile.header.ReferenceID;
                    row["FileType"] = eitThirFrFile.header.FileType;

                    //GLAUCH Note: You'll need this when you remove the SQL remove null values procedure from Comm834
                    /* row["SendEligType"] = 0;
                    row["TermSBEL"] = 0;
                    row["OOADeps"] = 0;
                    row["WouldHaveOutput"] = 0;*/
                }

                row["PopulationSET"] = eitThirFrFile.header.PopulationSET;
                row["FileName"] = System.IO.Path.GetFileName(inputFile);
                row["UniqueKey"] = counter++;

                //Set default values for booleans on initial record - this facilitates the read back into memory from the database
                row["SubGroupOut"] = 0;
                row["ClassCodeOut"] = 0;
                row["Output"] = 1;
                row["ErrCode"] = "";

                if(tran.SSN == null)
                {
                    row["SSN"] = "";
                }

                getData.Rows.Add(row);
            }

            DataWork.SaveDataTableToDb(outputTable, getData, targetDb);

            return eitThirFrFile.header;
        }

        public static bool IsFullFile(string fileUrl, out string delimiter)
        {
            char EndOfLine;
            delimiter = FindDelimiter(fileUrl, out EndOfLine);

            using (StreamReader sr = new StreamReader(fileUrl))
            {
                while (!sr.EndOfStream)
                {
                    string inputLine = "";
                    while ((char)sr.Peek() != EndOfLine && !sr.EndOfStream)
                    {
                        inputLine += (char)sr.Read();
                    }

                    if ((char)sr.Peek() == EndOfLine)
                    {
                        sr.Read(); //advance one char farther to skip the end of line

                        List<string> rawfields = inputLine.Split(Convert.ToChar(delimiter)).ToList(); //todo: use field delimiter

                        if (rawfields[0].Trim() == "BGN")
                        {
                            if (rawfields[8] == "RX" || rawfields[8] == "4")
                            {
                                return true;
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }

                }

                throw new Exception("Could not identify if this is a full file or not");
            }

        }

        public static string GetTBOQuery(string linkedServer, string groupId, string excludedClasses = "", string InputFileFilter = "", string callingProcess = "")
        {
            string temptable = "";
            string selectOnlyDepsIfNoSubscriber = "";
            string classCriteria = "";
            string subgroupCriteria = "";
            string useOutputTable = "";

            if (groupId == "")
            {
                temptable = "into #temp";
                selectOnlyDepsIfNoSubscriber = @"select * from #temp o 
                                                where Subscriber = 'Y' or 
                                                (Subscriber = 'N' and not exists (select * from #temp where o.SubscriberID = SubscriberID and Subscriber = 'Y'))";
            }

            if (groupId == "")
            {
                if (InputFileFilter == "") //active
                {
                    subgroupCriteria = @"";
                }
            }

            if (excludedClasses != "")
            {
                classCriteria = " and not in (" + excludedClasses + ") ";
            }

            if (callingProcess != "IT_0346") //breaking 346 away from the Output table - be free little bird
            {
                useOutputTable = "and not exists (select * from Output where . COLLATE Latin1_General_CI_AS =  and MemDep = .)";
            }
            else
            {
                useOutputTable = "";
            }

            return $@"";
        }


        public static string FindDelimiter(string file, out char endOfLine)
        {

            using (System.IO.FileStream stream = new System.IO.FileStream(file, System.IO.FileMode.Open, System.IO.FileAccess.Read))
            {
                int i = -1;
                stream.Seek(i, System.IO.SeekOrigin.End);
                endOfLine = (char)stream.ReadByte();

                if (endOfLine == '\n')
                {
                    i--;
                    stream.Seek(i, System.IO.SeekOrigin.End);
                    endOfLine = (char)stream.ReadByte();
                }

                if (endOfLine == '\r')
                {
                    i--;
                    stream.Seek(i, System.IO.SeekOrigin.End);
                    endOfLine = (char)stream.ReadByte();
                }

                while (endOfLine == ' ')
                {
                    i--;
                    stream.Seek(i, System.IO.SeekOrigin.End);
                    endOfLine = (char)stream.ReadByte();
                }
            }

            string delimiter = "";
            using (System.IO.StreamReader stream = new System.IO.StreamReader(file))
            {
                while (!stream.EndOfStream && delimiter == "")
                {
                    string firstLine = "";
                    while ((char)stream.Peek() != endOfLine && !stream.EndOfStream)
                    {
                        firstLine += (char)stream.Read();
                    }

                    delimiter = firstLine.Substring(firstLine.Length - 2, 1);
                }
            }

            if (delimiter == "")
            {
                throw new Exception("Unable to identify file delimiter in : " + file);
            }
            else
            {
                return delimiter;
            }
        }

        private static void LoadContactInformation(EightThirtyFourReceived tran, DataRow row, string groupID)
        {
            if (tran.ContactInfo.Exists(x => x.ContactMethod == "TE"))
            {
                row["Telephone"] = tran.ContactInfo.Where(x => x.ContactMethod == "TE").First().ContactDetails;
            }
            else if (tran.ContactInfo.Exists(x => x.ContactMethod == "AP"))
            {
                row["Telephone"] = tran.ContactInfo.Where(x => x.ContactMethod == "AP").First().ContactDetails;
            }
            else
            {
                ContactInformation rawTel = tran.ContactInfo.Where(x => x.ContactMethod != "EM").FirstOrDefault();

                if (rawTel != null)
                {
                    row["Telephone"] = rawTel.ContactDetails;
                }

            }

            if (tran.ContactInfo.Exists(x => x.ContactMethod == "EM"))
            {
                if (groupID == "" || groupID == "")
                {
                    row["EmailAddress"] = tran.ContactInfo.Where(x => x.ContactMethod == "EM").First().ContactDetails;
                }

            }
        }

        public static EightThirtyFourFile Load834InMemory(string inputFile, Logger caller, string delimiter, String groupID, String Vendor = "")
        {
            EightThirtyFourHeader header = new EightThirtyFourHeader(inputFile);

            List<EightThirtyFourReceived> receivedTrans = new List<EightThirtyFourReceived>();
            int InsCount = 0;
            EightThirtyFourReceived recTran = new EightThirtyFourReceived();

            bool potentialSSNInN1 = false;
            bool SSNInNM1 = true;
            Exchange834SSNCheck storage = new Exchange834SSNCheck();

            PremiumDetails premDtl = new PremiumDetails();

            List<string> fileLines = SplitFileIntoLines(inputFile);
            string leadingSSNinN1P5 = ""; //for HealthTrio, the SSN comes 2 rows before the INS segment that we use to signify a new record. For this reason we're just going to store a lagging SSN here.

            foreach (string line in fileLines)
            {
                List<string> rawfields = line.Replace(@"\r\n", "").Split(Convert.ToChar(delimiter)).ToList();

                List<string> fields = new List<string>();
                foreach (string fld in rawfields)
                {
                    fields.Add(fld.Trim());
                }

                if (fields[0] == "BGN" && fields[1] == "00")
                {
                    header.FileDate = fields[3];
                    header.FileTransactionSet = fields[1];
                    header.ReferenceID = fields[2];
                    header.FileType = fields[8];
                }
                else if (fields[0] == "N1" && fields[1] == "P5")
                {
                    header.GroupName = fields[2];

                    if (caller.ProcessId == "IT_0346" && Vendor == "")
                    {
                        leadingSSNinN1P5 = fields[4];
                    }


                    //Reset any logic flags for Softheon SSN issue
                    potentialSSNInN1 = false;
                    SSNInNM1 = true;
                    storage = new Exchange834SSNCheck();

                    //check to see if there's a potential SSN in the N1*P5* row. (F1)
                    if (caller.ProcessId == "IT_0346" && fields[4] != "" && Vendor == "")
                    {
                        potentialSSNInN1 = true;
                        storage.POTENTIAL_SSN = fields[4];
                    }

                }
                else if (fields[0] == "INS")
                {
                    if (InsCount == 0)
                    {
                        recTran = new EightThirtyFourReceived();
                    }
                    else
                    {
                        receivedTrans.Add(recTran);
                        recTran = new EightThirtyFourReceived();
                    }

                    InsCount++;
                    recTran.SubscriberFlag = fields[1];
                    recTran.Relationship = fields[2];
                    recTran.MaintenanceCode = fields[3];
                    recTran.MaintenanceRSN = fields[4];
                    recTran.BenefitStatus = fields[5];

                    if (caller.ProcessId == "IT_0346")
                    {
                        if (leadingSSNinN1P5.Length == 9 && Vendor.ToUpper() != "")
                        {
                            recTran.SSN = leadingSSNinN1P5;
                        }
                        else if(Vendor.ToUpper() != "")
                        {

                            recTran.ErrCode = "058,";
                            recTran.Output = false;

                        }

                    }

                    if (fields.Count > 6) 
                    {
                        recTran.MedicarePlanCode = fields[6];
                    }


                    if (fields.Count > 8)
                    {
                        recTran.EmployStatusCD = fields[8];
                    }

                    if (fields.Count > 10)
                    {
                        recTran.HandicapInd = fields[10];
                    }


                }
                else if (fields[0] == "REF" && !recTran.IsLxBlock)
                {
                    string refIdentifier = fields[1];

                    if (refIdentifier == "0F" && Vendor != "") 
                    {
                        recTran.SubNo = fields[2];
                    }
                    else if (refIdentifier == "1L")
                    {

                        if (caller.ProcessId == "IT_0346") //exchange
                        {
                            recTran.PolicyNBR = fields[2];
                        }
                        else
                        {
                            recTran.GroupNo = fields[2];
                        }

                    }
                    else if (refIdentifier == "17")
                    {
                        if (caller.ProcessId == "IT_0346")
                        {
                            recTran.MemberNBR = fields[2]; //exchange issued member ID (key value for CMS)
                        }
                        else
                        {
                            recTran.ClassCode = fields[2]; //class code for IT_0346 is populated via a HIOS & County code mapping
                        }

                    }
                    // Can put the error into the new properties here and in the CE conditional and put comma after the error code (check eightthirtyfourreports file for error codes)
                    else if (refIdentifier == "23")
                    {

                        try
                        {
                            if (caller.ProcessId == "IT_0346" && Vendor.ToUpper() == "")
                            {
                                recTran.SubscriberID = fields[2].Substring(0, 9); 
                                recTran.SubNo = fields[2].Substring(0, 9); //Source matcher needs this so I am putting it in both fields
                                recTran.MemDep = Convert.ToInt32(fields[2].Substring(9, 2)).ToString(); 
                            }
                            else
                            {
                                if (groupID == "")
                                {
                                    recTran.EmployeeID = fields[2];
                                }
                                else
                                {
                                    recTran.SubGroup = fields[2];
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            recTran.ErrCode = "056,";
                            recTran.Output = false;
                        }

                    }
                    else if (refIdentifier == "QQ")
                    {
                        recTran.PriorCoveredMonths = fields[2];
                    }
                    else if (refIdentifier == "38")
                    {
                        if (fields.Count > 2)
                        {
                            header.PopulationSET = fields[2];
                        }

                    }
                    else if (refIdentifier == "CE")
                    {


                        try
                        {
                            recTran.HIOS = fields[2];
                            if (recTran.HIOS.Length == 16 && !recTran.HIOS.Contains("-"))//If  passed us a HIOS ID without a dash in it like expected, add it
                            {
                                recTran.HIOS = recTran.HIOS.Insert(14, "-");
                            }
                        }
                        catch (Exception e)
                        {

                            recTran.ErrCode = "057,";
                            recTran.Output = false;
                        }


                    }
                    else if (refIdentifier == "DX")
                    {
                        recTran.PlanCoverageDesc = fields[2];
                    }
                    else if (refIdentifier == "P5" && groupID == "")
                    {
                        if (fields.Count > 2)
                        {
                            if (string.IsNullOrEmpty(recTran.MemberRecordNumber))
                            {
                                recTran.MemberRecordNumber = fields[2];
                            }
                            else
                            {
                                recTran.MemberRecordNumber = fields[2] + recTran.MemberRecordNumber;
                            }
                        }
                    }
                    else if (refIdentifier == "ZZ" && groupID == "")
                    {
                        if (fields.Count > 2)
                        {
                            if (string.IsNullOrEmpty(recTran.MemberRecordNumber))
                            {
                                recTran.MemberRecordNumber = fields[2];
                            }
                            else
                            {
                                recTran.MemberRecordNumber = recTran.MemberRecordNumber + fields[2];
                            }
                        }
                    }
                    else if (refIdentifier == "F6")
                    {
                        if (fields.Count > 2)
                        {
                            recTran.MedicareBeneficiaryIndicator = fields[2];
                        }

                    }

                }
                else if (fields[0] == "HLH")
                {
                    recTran.SmokingIndicator = fields[1];
                }
                else if (fields[0] == "LUI")
                {
                    if (Vendor.ToUpper() == "")
                    {
                        recTran.Language = fields[2];
                    }
                    else
                    {
                        recTran.Language = fields[1];
                    }
                }
                else if (fields[0] == "DTP" && !recTran.IsLxBlock)
                {
                    string dateIdentifier = fields[1];

                    if (dateIdentifier == "340")
                    {
                        recTran.COBRAStart = fields[3];
                    }
                    else if (dateIdentifier == "341")
                    {
                        recTran.COBRAEnd = fields[3];
                    }
                    else if (dateIdentifier == "344")
                    {
                        recTran.EGWPEligDt = fields[3];
                    }
                    else if (dateIdentifier == "348")
                    {
                        recTran.CovEffDate = fields[3];
                    }
                    else if (dateIdentifier == "349")
                    {
                        recTran.CovEndDate = fields[3].Trim();
                    }
                    else if (dateIdentifier == "356")
                    {
                        recTran.EnrollElig = fields[3].Trim();
                    }
                    else if (dateIdentifier == "303")
                    {
                        recTran.ChangeEffDt = fields[3];
                    }
                    else if (dateIdentifier == "357")
                    {
                        recTran.EnrollTerm = fields[3];
                    }
                    else if (dateIdentifier == "338")
                    {
                        if (fields.Any(x => x.ToUpper() == "D8"))
                        {
                            recTran.MedicareBegin338 = fields[3];
                        }
                        else
                        {
                            recTran.MedicareBegin338 = fields[2];
                        }
                    }
                    else if (dateIdentifier == "339")
                    {
                        if (fields.Count == 4)
                        {
                            recTran.MedicareEnd339 = fields[3];
                        }
                        else
                        {
                            recTran.MedicareEnd339 = fields[2];
                        }
                    }



                }
                else if (fields[0] == "NM1")
                {
                    if (fields[1] == "IL" || fields[1] == "74")
                    {
                        recTran.IsPCPBlock = false;
                        recTran.FirstName = fields[4];
                        recTran.LastName = fields[3];

                        string regexPattern = @"[a-zA-Z'\-]*";


                        //If there's no SSN found in the NM1 row, we flip our second logic flag. (F2)
                        if (caller.ProcessId == "IT_0346" && (fields.Count < 9 || (fields.Count > 9 && fields[9] == "")) && Vendor == "")
                        {
                            SSNInNM1 = false;
                            storage.FIRST_NAME = fields[4];
                            storage.LAST_NAME = fields[3];
                        }

                        if (fields.Count > 5)
                        {
                            recTran.MidInit = fields[5];

                            if (fields.Count > 7)
                            {
                                recTran.NameSuffix = Regex.Match(fields[7], regexPattern).Value.Trim();

                                if (fields.Count > 9)
                                {
                                    if (fields[9].Length == 9)
                                    {
                                        recTran.SSN = fields[9];
                                    }
                                    else
                                    {
                                        recTran.ErrCode = "058,";
                                        recTran.Output = false;
                                    }
                                }
                                else
                                {
                                    recTran.SSN = "999999999";
                                }
                            }
                        }
                    }
                    else if (fields[1] == "P3")
                    {
                        recTran.IsPCPBlock = true;
                        recTran.PCPName = fields[4] + " " + fields[3];
                    }
                    else
                    {
                        recTran.IsPCPBlock = false;
                    }

                    //add else if to process NM1*IN for medicare?

                }
                else if (fields[0] == "PER" && fields[1] == "IP")
                {
                    for (int i = 3; i < fields.Count; i += 2)
                    {
                        ContactInformation contInf = new ContactInformation();
                        contInf.ContactMethod = fields[i];
                        contInf.ContactDetails = fields[i + 1];
                        recTran.ContactInfo.Add(contInf);
                    }
                }
                else if (fields[0] == "N3")
                {
                    if (!recTran.IsPCPBlock)
                    {
                        if (recTran.AddressOne == null)
                        {
                            recTran.AddressOne = fields[1];

                            if (fields.Count > 2) //if address2 is populated
                            {
                                recTran.AddressTwo = fields[2];
                            }
                        }
                        else
                        {
                            recTran.MailingAddressOne = fields[1];

                            if (fields.Count > 2) //if address2 is populated
                            {
                                recTran.MailingAddressTwo = fields[2];
                            }
                        }
                    }
                    else
                    {
                        recTran.PCPAddressOne = fields[1];

                        if (fields.Count > 2)
                        {
                            recTran.PCPAddressTwo = fields[2];
                        }

                    }


                }
                else if (fields[0] == "N4")
                {
                    if (!recTran.IsPCPBlock)
                    {
                        if (recTran.City == null)
                        {
                            recTran.City = fields[1];
                            recTran.State = fields[2];
                            recTran.Zip = fields[3];
                        }
                        else
                        {
                            recTran.MailingCity = fields[1];
                            recTran.MailingState = fields[2];
                            recTran.MailingZip = fields[3];
                        }

                        if (fields.Count > 5)
                        {
                            recTran.County = fields[6];
                        }
                        else if(caller.ProcessId != "IT_0346")
                        {
                            // Added in this logic to reduce Google API calls if we already have a county in Facets
                            string countyQuery = $@"";

                            DataTable county = ExtractFactory.ConnectAndQuery(caller, caller.LoggerExampleDb, countyQuery);
                            if (county.Rows.Count > 0)
                            {
                                recTran.County = county.Rows[0]["COUNTY"].ToString().Trim();
                            }
                            else
                            {
                                //Geo834 test = FindMyLocation.GeocodeAddress(caller, recTran.AddressOne, recTran.City, recTran.State, recTran.Zip);
                                //if (test.administrative_area_level_2 != "")
                                //{
                                //    recTran.County = test.administrative_area_level_2;
                                //}
                            }           
                            
                        }
                        else
                        {

                        }
                    }
                    else
                    {
                        recTran.PCPCity = fields[1];
                        recTran.PCPState = fields[2];
                        recTran.PCPZip = fields[3];
                    }


                }
                else if (fields[0] == "DMG")
                {
                    if (recTran.DOB == null)
                    {
                        recTran.DOB = fields[2];
                    }

                    if (fields.Count > 3)
                    {
                        recTran.Gender = fields[3];

                    }
                    if (fields.Count > 4)
                    {
                        recTran.MaritalStatus = fields[4];
                    }
                    if (fields.Count > 5 && groupID == "")
                    {
                        List<string> rAndEFields = fields[5].Split(':').ToList();
                        if (rAndEFields.Count == 2)
                        {
                            recTran.InputRace = rAndEFields[0];
                            recTran.InputEthnicity = rAndEFields[1];
                        }
                        else
                        {
                            recTran.InputRace = rAndEFields[0];
                        }
                    }


                    //If both logic flags are hit (F1 and !F2)... We go in here to match the potential SSN found on the N1*P5 row with what's in facets.
                    if (potentialSSNInN1 && !SSNInNM1 && caller.ProcessId == "IT_0346" && Vendor == "")
                    {
                        storage.BIRTHDAY = fields[2];
                        //check facets or the warehouse for the member's SSN
                        DataTable dt = ExtractFactory.ConnectAndQuery(Data.AppNames.ExampleProd, string.Format(@""));
                        if (dt.Rows.Count > 0)
                        {
                            string facetsSSN = dt.Rows[0][0].ToString();
                            if (facetsSSN == storage.POTENTIAL_SSN)
                            {
                                recTran.SSN = storage.POTENTIAL_SSN;
                                potentialSSNInN1 = false;
                                SSNInNM1 = true;
                                storage = new Exchange834SSNCheck();
                            }
                        }
                    }

                }
                else if (fields[0] == "LX" && !recTran.IsLxBlock) //if this is a new LX segment we've found
                {
                    recTran.IsLxBlock = true;
                    premDtl = new PremiumDetails();

                }
                else if (recTran.IsLxBlock)
                {
                    if (fields[0] == "LX" || fields[0] == "LE")
                    {
                        recTran.PremiumDtls.Add(premDtl);


                        if (fields[0] == "LX")
                        {
                            premDtl = new PremiumDetails();

                        }
                        else
                        {
                            recTran.IsLxBlock = false;
                        }

                    }
                    else if (fields[0] == "N1" && fields[1] == "75")
                    {
                        premDtl.ValueType = fields[2];
                    }
                    else if (fields[0] == "REF" && (fields[1] == "9X" || fields[1] == "17" || fields[1] == "9V"))
                    {
                        premDtl.Value = fields[2];
                    }
                    else if (fields[0] == "DTP" && fields[1] == "007")
                    {
                        premDtl.ChangeEffectiveDate = fields[3];
                    }

                }
                else if (fields[0] == "HD" && (fields[1] == "030" || fields[1] == "021" || fields[1] == "024" || fields[1] == "001") && fields.Count >= 5)
                {
                    if (fields.Count > 5)
                    {
                        recTran.CoverageLevel = fields[5];
                    }

                    recTran.HealthCovMaintType = Convert.ToInt32(fields[1]).ToString();

                }
                else if (fields[0] == "GE") //we've reached the end of the file, save this member.
                {
                    receivedTrans.Add(recTran);
                }
            }

            return new EightThirtyFourFile(header, receivedTrans);

        }


        private static List<string> SplitFileIntoLines(string inputFile, char endOfLineDelimiter = '~')
        {
            List<string> fileLines = new List<string>();

            using (StreamReader sr = new StreamReader(inputFile, Encoding.Default))
            {
                string rawText = sr.ReadToEnd();
                fileLines = rawText.Split(endOfLineDelimiter).ToList();
            }

            return fileLines;
        }

        public class Exchange834SSNCheck
        {
            public string FIRST_NAME { get; set; }
            public string LAST_NAME { get; set; }
            public string BIRTHDAY { get; set; }
            public string POTENTIAL_SSN { get; set; }
        }
        public class EightThirtyFourFile
        {
            public EightThirtyFourHeader header { get; set; }
            public List<EightThirtyFourReceived> receivedTrans { get; set; }

            public EightThirtyFourFile(EightThirtyFourHeader hdr, List<EightThirtyFourReceived> recTrans)
            {
                header = hdr;
                receivedTrans = recTrans;
            }
        }

        // Add record for output and error code
        public class EightThirtyFourReceived
        {
            public EightThirtyFourReceived()
            {
                ContactInfo = new List<ContactInformation>();
                PremiumDtls = new List<PremiumDetails>();
                Output = true;
            }
            public string SubscriberFlag { get; set; }
            public string Relationship { get; set; }
            public string MaintenanceCode { get; set; }
            public string MaintenanceRSN { get; set; }
            public string BenefitStatus { get; set; }
            public string MedicarePlanCode { get; set; }
            public string EmployStatusCD { get; set; }
            public string EmployeeID { get; set; }
            public string HandicapInd { get; set; }
            public string SubNo { get; set; }
            public string GroupNo { get; set; }
            public string ClassCode { get; set; }
            public string SubGroup { get; set; }
            public string PriorCoveredMonths { get; set; }
            public string EarliestEffDate { get; set; }
            public string LastName { get; set; }
            public string FirstName { get; set; }
            public string MidInit { get; set; }
            public string NameSuffix { get; set; }
            public string SSN { get; set; }
            public List<ContactInformation> ContactInfo { get; set; }
            public string AddressOne { get; set; }
            public string AddressTwo { get; set; }
            public string City { get; set; }
            public string County { get; set; }
            public string State { get; set; }
            public string Zip { get; set; }
            public string DOB { get; set; }
            public string Gender { get; set; }
            public string MaritalStatus { get; set; }
            public string InputRace { get; set; }
            public string InputEthnicity { get; set; }
            public string Race { get; set; }
            public string Ethnicity { get; set; }
            public string ChangeEffDt { get; set; }
            public string CoverageLevel { get; set; }
            public string CovEffDate { get; set; }
            public string LatestFacetsTermDate { get; set; }
            public string CovEndDate { get; set; }
            public string COBRAStart { get; set; }
            public string COBRAEnd { get; set; }
            public string EnrollTerm { get; set; }
            public string EnrollElig { get; set; }
            public string PlanCoverageDesc { get; set; }
            public string Language { get; set; }
            public string SmokingIndicator { get; set; }
            public string HIOS { get; set; }
            public string PolicyNBR { get; set; }
            public string MemberNBR { get; set; }
            public string AptcAmount { get; set; }
            public string CsrAmount { get; set; }
            public string SubscriberRatingArea { get; set; }
            public bool SubGroupOut { get; set; }
            public bool ClassCodeOut { get; set; }
            public string HealthCovMaintType { get; set; }
            public string Action { get; set; }
            public string SubscriberID { get; set; }
            public string MemberRecordNumber { get; set; }
            public string SubscriberMailingAddressType { get; set; }
            public string SubscriberHomeAddressType { get; set; }
            public string CoverageLevelOut { get; set; }
            public string ShortName { get; set; }
            public string MemberRelationship { get; set; }
            public string MemDep { get; set; }
            public string MailingAddressOne { get; set; }
            public string MailingAddressTwo { get; set; }
            public string MailingCity { get; set; }
            public string MailingState { get; set; }
            public string MailingZip { get; set; }
            public string EligAction { get; set; }
            public string ClassPlanIdPharmacy { get; set; }
            public string ClassPlanIdMedical { get; set; }
            public List<PremiumDetails> PremiumDtls { get; set; }
            public bool IsPCPBlock { get; set; }
            public string PCPName { get; set; }
            public string PCPAddressOne { get; set; }
            public string PCPAddressTwo { get; set; }
            public string PCPCity { get; set; }
            public string PCPState { get; set; }
            public string PCPZip { get; set; }
            public Boolean IsLxBlock { get; set; }
            public string MedicareBegin338 { get; set; }
            public string MedicareEnd339 { get; set; }
            public string EGWPEligDt { get; set; }
            public string MedicareBeneficiaryIndicator { get; set; }
            public Boolean Output { get; set; }
            public string ErrCode { get; set; }
        }

        public class ExchangeEightThirtyFourReceived : EightThirtyFourReceived
        {
            public string AptcIndicator { get; set; }
            public string QhpIndicatorNvl { get; set; }
            public string MemberIdNvl { get; set; }

        }

        public class ContactInformation
        {
            public string ContactMethod { get; set; }
            public string ContactDetails { get; set; }
        }

        public class PremiumDetails
        {
            public string ValueType { get; set; }
            public string ChangeEffectiveDate { get; set; }
            public string Value { get; set; }
        }

        public class EightThirtyFourHeader
        {
            public EightThirtyFourHeader(string file)
            {
                FileName = file;
            }
            string FileName { get; set; }
            public string PopulationSET { get; set; }
            public string GroupName { get; set; }
            public string FileDate { get; set; }
            public string FileTransactionSet { get; set; }
            public string ReferenceID { get; set; }
            public string FileType { get; set; }
        }

        public static void AssignMemeSfxForNewMembers(Logger proclog, Data.AppNames CommTarget)
        {
            //Pull members who need a suffix, but were not flagged as orphan dependents for 'new' families
            List<MemberDetails> membersWhoNeedSfx = ExtractFactory.ConnectAndQuery<MemberDetails>(proclog, CommTarget, @"select LastName, FirstName, SSN, DOB, SBSB_ID, SubscriberFlag, SubNo, GroupNo from Output where MemDep = '' and ErrCode not like '%041%'").ToList();

            foreach (MemberDetails mems in membersWhoNeedSfx)
            {
                if (mems.SubscriberFlag == "Y")
                {
                    string updateStatement = string.Format(@"update Output set MemDep = '0' where LastName = '{0}' and FirstName = '{1}' and SSN = '{2}' and DOB = '{3}' and SubNo = '{4}'", mems.LastName.Replace("'", "''"), mems.FirstName, mems.SSN, mems.DOB, mems.SubNo);

                    DataWork.RunSqlCommand(proclog, updateStatement, CommTarget);
                }
                else
                {
                    string getLastUsedSfx = string.Format(@"select Max(MemDep) as NextMemDep
from (
select MemDep
from Admin834.dbo.[EligFromSourceSystem]
WHERE SBSB_ID = '{0}' AND GROUPNO = '{2}' AND BENEFIT_CAT = 'M'
union all
SELECT MemDep
FROM Admin834.dbo.[EligFutureFromSourceSystem] 
WHERE SBSB_ID = '{0}' AND GROUPNO = '{2}' AND BENEFIT_CAT = 'M'
union all
SELECT MemDep
from Admin834.dbo.[EligTermsFromSourceSystem]
WHERE SBSB_ID = '{0}' AND GROUPNO = '{2}' AND BENEFIT_CAT = 'M'
union all
select MemDep
from Output
where SubNo = '{1}' and GroupNo = '{2}') as f", mems.SubscriberId, mems.SubNo, mems.GroupId);

                    int LastUsedSfx = Convert.ToInt32(ExtractFactory.ConnectAndQuery<string>(proclog, CommTarget, getLastUsedSfx).ToList().First());

                    proclog.WriteToLog("Last used MemDep for family SubscriberSSN " + mems.SubNo + "/ SBSB_ID " + mems.SubscriberId + " is " + LastUsedSfx.ToString());
                    string updateStatement = string.Format(@"update Output set MemDep = '{5}', EligAction = '' where LastName = '{0}' and FirstName = '{1}' and SSN = '{2}' and DOB = '{3}' and SubNo = '{4}'", mems.LastName.Replace("'", "''"), mems.FirstName.Replace("'", "''"), mems.SSN, mems.DOB, mems.SubNo, LastUsedSfx + 1);

                    DataWork.RunSqlCommand(proclog, updateStatement, CommTarget);
                }
            }
        }

        class MemberDetails
        {
            public string LastName { get; set; }
            public string FirstName { get; set; }
            public string SSN { get; set; }
            public string DOB { get; set; }
            public string SubscriberId { get; set; }
            public string SubscriberFlag { get; set; }
            public string SubNo { get; set; }
            public string GroupId { get; set; }
        }


        /// <summary>
        /// Given a mm/dd/yyyy or yyyymmdd string, returns a datetime
        /// </summary>
        /// <param name="dateString">mm/dd/yyyy or yyyymmdd string</param>
        /// <returns>DateTime for the given string</returns>
        public static DateTime ConvertToDateTime(string dateString)
        {

            if (dateString.Contains("/"))
            {

                return Convert.ToDateTime(dateString);
            }
            else
            {
                return DateTime.ParseExact(dateString, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
            }

        }

    }

    public class EmptyFileException : Exception
    {
        public EmptyFileException(string fileName)
        {
            this.FileName = fileName;
        }
        public string FileName { get; set; }
    }
    public class WrongPayerFileException : Exception
    {
        public WrongPayerFileException(string fileName)
        {
            this.FileName = fileName;
        }
        public string FileName { get; set; }
    }

    public class TestFileInProdModeException : Exception
    {
        public TestFileInProdModeException(string fileName)
        {
            this.FileName = fileName;
        }
        public string FileName { get; set; }
    }

    public class Geo834
    {
        public string street_number { get; set; }
        public string route { get; set; }
        public string neighborhood { get; set; }
        public string locality { get; set; }
        public string administrative_area_level_3 { get; set; }
        public string administrative_area_level_2 { get; set; }
        public string administrative_area_level_1 { get; set; }
        public string country { get; set; }
        public string postal_code { get; set; }
        public string postal_code_suffix { get; set; }
        public string latitude { get; set; }
        public string longitude { get; set; }
    }
}


