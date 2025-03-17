using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RestSharp;
using Newtonsoft.Json.Linq;
using System.Runtime.CompilerServices;
using System.Net;

namespace Utilities
{
    public class NpiValidation
    {
        /// <summary>
        /// Given an NPI, passes that against the CMS NPI web service, and provides details from CMS based on the type of provider passed
        /// </summary>
        /// <param name="NPI">National Provider Identifier to validate</param>
        /// <param name="resp">Empty response object that we will populate on your behalf</param>
        /// <param name="ProviderType">Passing "Facility" or "Group" will have us return organization name details in 'resp', whereas any other value will return individual provider details</param>
        /// <returns>The number of matching records for this NPI, note the resp object will only have information for you if you only matched to one record in the CMS database</returns>
        public static int ValidateNPI(String NPI, out NPPESResponse resp, String ProviderType, string callerName)
        {

            RestRequest req = new RestRequest("?version=2.1&number={number}", Method.GET);

            Parameter NPINum = new Parameter();
            NPINum.Name = "number";
            NPINum.Value = NPI;
            NPINum.Type = ParameterType.UrlSegment;

            req.AddParameter(NPINum);

            int RecordsFound;
            Boolean ErroredRecord;
            dynamic JSON = GetNPPESResponse(req, out RecordsFound, out ErroredRecord);

            if (OnlyOneResponse(RecordsFound) && !ErroredRecord)
            {
                resp = new NPPESResponse();
                if (ProviderType == "Facility" || ProviderType == "Group")
                {
                    resp.Name = JSON.results[0].basic.name;
                    resp.OrgName = JSON.results[0].basic.organization_name;
                    resp.NPIType = JSON.results[0].enumeration_type;

                    if (JSON.results[0].other_names.Count > 0)
                    {
                        resp.OtherName = JSON.results[0].other_names[0].organization_name;
                    }
                }
                else
                {
                    resp.FirstName = JSON.results[0].basic.first_name;
                    resp.LastName = JSON.results[0].basic.last_name;
                    resp.NPIType = JSON.results[0].enumeration_type;
                }


                return RecordsFound;
            }
            else if(ErroredRecord)
            {
                resp = new NPPESResponse();
                try
                {
                    resp.ErrorMessage = JSON.Errors.number;
                }
                catch (Exception ex)
                {
                    string errorText = ex.ToString();

                    Logger caller = new Logger(callerName);
                    UniversalLogger.WriteToLog(caller, errorText);
                    UniversalLogger.WriteToLog(caller, "NPI #" + NPI + " caused the prior error.  PHP Business should be notified to deactivate this NPI.", category:UniversalLogger.LogCategory.WARNING);
                }



                return RecordsFound;
            }
            else
            {
                resp = new NPPESResponse();
                return RecordsFound;
            }

        }

        public class NPPESResponse
        {
            public String Name { get; set; }
            public String OrgName { get; set; }
            public String OtherName { get; set; }
            public String NPIType { get; set; }
            public String FirstName { get; set; }
            public String LastName { get; set; }
            public String ErrorMessage { get; set; }

        }

        private static Boolean OnlyOneResponse(int RecsFound)
        {
            if (RecsFound == 1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private static dynamic GetNPPESResponse(RestRequest req, out int ResultsFound, out Boolean Error)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;
            RestClient client = new RestClient("https://npiregistry.cms.hhs.gov/api/");

            RestSharp.IRestResponse resp = client.Execute(req);

            dynamic JSONObject = JObject.Parse(resp.Content);

            try
            {
                ResultsFound = JSONObject.results.Count;
                Error = false;
            }
            catch
            {
                ResultsFound = 1;
                Error = true;
            }
            
            return JSONObject;

        }

        public static string NPICheckSumValidation(String FieldName, String NPI, ref String message, ref bool BadRecord, string PrescriberLastName, string PrescriberFirstName)
        {
            int Aggregator = 24;
            int checkDigit = 0;
            int npiDigit = 0;

            for (int charPosition = 0; charPosition < NPI.Length; charPosition++)
            {
                //Check if even or odd
                if (charPosition % 2 == 0) //zero is Even in C#
                {
                    npiDigit = (Convert.ToInt32(NPI.Substring(charPosition, 1)) * 2);

                    if (npiDigit.ToString().Length == 2) //doubling 5 to 9 will make a number with two characters
                    {
                        npiDigit = Convert.ToInt32(npiDigit.ToString().Substring(0, 1)) + Convert.ToInt32(npiDigit.ToString().Substring(1, 1));
                    }
                    Aggregator = Aggregator + (npiDigit);
                }
                else //Odd, in C#
                {
                    if (charPosition == 9) //10th position due to zero based array in C#
                    {
                        //This is the checkDigit, grab it!
                        checkDigit = Convert.ToInt32(NPI.Substring(charPosition, 1));
                    }
                    else
                    {
                        Aggregator = Aggregator + Convert.ToInt32(NPI.Substring(charPosition, 1));
                    }
                }
            }

            //Get next highest whole ten number and subtract Aggregator from it
            int nextHighest = (int)Math.Ceiling((decimal)Aggregator / 10) * 10;

            //Then compare to see if the checkDigit is the same as the results
            if ((nextHighest - Aggregator) == checkDigit)
            {
                return NPI;
            }
            else
            {
                BadRecord = true;
                message += " Field " + FieldName + " failed the check digit validation. ";
                return NPI;
            }
        }
    }
}
