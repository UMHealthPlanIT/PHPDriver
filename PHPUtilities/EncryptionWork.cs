using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.IO;
using System.Data.SqlClient;

namespace Utilities
{
    public class EncryptionWork
    {
        static readonly string PasswordHash = "YourKeyHere";
        static readonly string SaltKey = "YourKeyHere";
        static readonly string VIKey = "@YourKeyHere";

        public static string Encrypt(string plainText)
        {
            byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);

            byte[] keyBytes = new Rfc2898DeriveBytes(PasswordHash, Encoding.ASCII.GetBytes(SaltKey)).GetBytes(256 / 8);
            var symmetricKey = new RijndaelManaged() { Mode = CipherMode.CBC, Padding = PaddingMode.Zeros };
            var encryptor = symmetricKey.CreateEncryptor(keyBytes, Encoding.ASCII.GetBytes(VIKey));

            byte[] cipherTextBytes;

            using (var memoryStream = new MemoryStream())
            {
                using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                {
                    cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
                    cryptoStream.FlushFinalBlock();
                    cipherTextBytes = memoryStream.ToArray();
                    cryptoStream.Close();
                }
                memoryStream.Close();
            }
            return Convert.ToBase64String(cipherTextBytes);
        }

        public static string Decrypt(string encryptedText)
        {
            byte[] cipherTextBytes = Convert.FromBase64String(encryptedText);
            byte[] keyBytes = new Rfc2898DeriveBytes(PasswordHash, Encoding.ASCII.GetBytes(SaltKey)).GetBytes(256 / 8);
            var symmetricKey = new RijndaelManaged() { Mode = CipherMode.CBC, Padding = PaddingMode.None };

            var decryptor = symmetricKey.CreateDecryptor(keyBytes, Encoding.ASCII.GetBytes(VIKey));
            var memoryStream = new MemoryStream(cipherTextBytes);
            var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
            byte[] plainTextBytes = new byte[cipherTextBytes.Length];

            int decryptedByteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);
            memoryStream.Close();
            cryptoStream.Close();
            return Encoding.UTF8.GetString(plainTextBytes, 0, decryptedByteCount).TrimEnd("\0".ToCharArray());
        }


        private static void AddEncryptedKeyToStore(String encryptedText, String Key, Data.AppNames dataStore)
        {
            List<SqlParameter> parameters = new List<SqlParameter>();

            parameters.Add(new SqlParameter("Key", Key));
            parameters.Add(new SqlParameter("Value", encryptedText));

            DataWork.RunSqlCommand(@"INSERT INTO [dbo].[Key_C]
           ([Key]
           ,[Value])
     VALUES
           (@Key
           ,@Value)", dataStore, parameters);
        }

        public static void AddAppNamePasswordToStore(String RawText, Data.AppNames AppName, Data.AppNames keyStore)
        {
            String encryptedPw = Utilities.EncryptionWork.Encrypt(RawText);

            Utilities.EncryptionWork.AddEncryptedKeyToStore(encryptedPw, AppName.ToString(), keyStore);
        }

        public static bool PgpEncrypt(string filePath, string recipient, Logger job)
        {
            string passphrase = "YourPWHere"; //Should put into app.config
            string gpgFolder = string.Format(@"C:\Users\{0}\AppData\Roaming\gnupg", System.Security.Principal.WindowsIdentity.GetCurrent().Name.Split('\\')[1]); //We need to set the folder where the pgp keyring is because of weird things with the job console rerun functionality
            string commandString = string.Format("--batch --passphrase {0} --yes --verbose --always-trust --recipient \"{1}\" --homedir {3} --encrypt-files {2}", passphrase, recipient, filePath, gpgFolder);

            if (GPG4WinInstalled())
            {
                //Make call to encryption command line
                int extCode = FileSystem.ExternalExecutor(@"C:\Program Files (x86)\gnupg\bin\gpg.exe", job, commandString);

                //Exit code 0 is success, anything else is an error
                if (extCode == 0)
                {
                    return true;
                }
                else
                {
                    throw new Exception("Encryption failed. GPG Exit Code: " + extCode.ToString());
                }
            }
            else
            {
                throw new Exception("GPG4Win or one of its components is not installed.");
            }

        }

        public static bool PgpDecrypt(string filePath, Logger job, bool allowCAST5 = false)
        {
            string overrideCast = allowCAST5 ? "--ignore-mdc-error" : "";
            string passphrase = "YourPWHere"; //Should put into app.config

            string path = System.IO.Path.GetDirectoryName(filePath);
            string name = System.IO.Path.GetFileName(filePath);

            string commandString =               $"--batch --try-all-secrets --pinentry-mode loopback {overrideCast} --passphrase {passphrase} --yes --always-trust --decrypt-files \"{path}\"\\{name} ";
            job.WriteToLog($"command string used:  --batch --try-all-secrets --pinentry-mode loopback {overrideCast} --passphrase *omitted* --yes --always-trust --decrypt-files \"{path}\"\\{name} ");

            if (GPG4WinInstalled())
            {
                //Make call to encryption command line
                int extCode = FileSystem.ExternalExecutor(@"C:\Program Files (x86)\gnupg\bin\gpg.exe", job, commandString);

                //Exit code 0 is success, anything else is an error
                if (extCode == 0)
                {
                    return true;
                }
                else
                {
                    throw new Exception("Encryption failed. GPG Exit Code: " + extCode.ToString());
                }
            }
            else
            {
                throw new Exception("GPG4Win or one of its components is not installed.");
            }


        }

        public static bool GPG4WinInstalled()
        {
            //Check if GnuPG is installed
            if (File.Exists(@"C:\Program Files (x86)\gnupg\bin\gpg.exe"))
            {
                //Check if GPG4Win is installed
                if (File.Exists(@"C:\Program Files (x86)\Gpg4win\bin\kleopatra.exe"))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

    }
}
