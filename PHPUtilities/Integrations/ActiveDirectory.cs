using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.DirectoryServices;
using System.Data;
using System.DirectoryServices.AccountManagement;

namespace Utilities.Integrations
{
    public class ActiveDirectory
    {
        public static SearchResult GetPersonFromActiveDirectory(string adLogin)
        {
            
            DirectoryEntry entry = new DirectoryEntry("LDAP://DC=,DC=");
            DirectorySearcher mySearcher = new DirectorySearcher(entry);
            mySearcher.Filter = "(&(objectClass=user)(|(cn=" + adLogin + ")(sAMAccountName=" + adLogin + ")))";
            return mySearcher.FindOne();
        }

        /// <summary>
        /// Method to pull email address from Active Directory given a user's AD name. Returns null if user is not found or if they don't have an email listed.
        /// </summary>
        /// <param name="adName">The AD name of the user</param>
        /// <returns>The user's email address in Active Directory, or null if one is not found.</returns>
        public static string GetEmailFromAdName(string adName)
        {
            string splitName = adName.Split('\\').Last();
            
            try
            {
                return GetPersonFromActiveDirectory(splitName).Properties["mail"][0].ToString();
            } 
            catch (NullReferenceException)
            {
                return null;
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }
        public static SearchResult GetPersonFromActiveDirectoryByEmail(string email)
        {

            DirectoryEntry entry = new DirectoryEntry("LDAP://DC=,DC=");
            DirectorySearcher mySearcher = new DirectorySearcher(entry);
            mySearcher.Filter = ("mail=" + email);
            return mySearcher.FindOne();

        }

        /// <summary>
        /// This will return all active directory users for shs.org
        /// </summary>
        /// <param name="pSearchParameters"></param> Optional parameter on DistinguishedName, such as "OU=Sparrow Users"
        /// <returns></returns>
        public static List<Principal> GetAllActiveDirectoryUsers(string pSearchParameters = "")
        {
            List<UserPrincipal> results = new List<UserPrincipal>();
            using (PrincipalContext context = new PrincipalContext(ContextType.Domain, ""))
            {
                using (PrincipalSearcher searcher = new PrincipalSearcher(new UserPrincipal(context)))
                {
                    List<Principal> searchResults = searcher.FindAll().Where(p => p.DistinguishedName.Contains(pSearchParameters)).ToList();
                    return searchResults;
                }
            }
        }

        /// <summary>
        /// This will update the phone number in the active directory.
        /// </summary>
        /// <param name="pUserPrincipal"></param>
        /// <param name="pPhoneNumber"></param>
        public static void UpdateActiveDirectoryPhoneNumber(UserPrincipal pUserPrincipal, string pPhoneNumber)
        {
            Logger log = new Logger(new Logger.LaunchRequest("UpdateActiveDirectoryPhoneNumber", false, null));

            if (log.TestMode)
            {
                Console.WriteLine("UserID: " + pUserPrincipal.SamAccountName);
                Console.WriteLine("Phone number passed in: " + pPhoneNumber);
                Console.WriteLine("AD phone number: " + pUserPrincipal.VoiceTelephoneNumber);
                Console.WriteLine("");
            }
            else
            {
                pUserPrincipal.VoiceTelephoneNumber = pPhoneNumber;
                pUserPrincipal.Save();
            }
        }
    }
}
