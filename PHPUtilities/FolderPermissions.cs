using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using Utilities.Integrations;

using System.Security.Principal;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Runtime.ConstrainedExecution;
using System.Security;
using System.Configuration;

namespace Utilities
{
    public static class FolderPermissions
    {
        /// <summary>
        /// Sets folder permissions depending on department(s) assigned to the job in the job index.
        /// </summary>
        /// <param name="folder">Path of the folder</param>
        /// <param name="jobIndex">Job index number</param>
        /// <param name="additionalPermissions">Additional permission to add for the folder</param>
        /// <param name="setOnBaseJobFolder">If true, this will go back up to the base job folder to set the permissions</param>
        public static void SetFolderPermissions(Logger log, string folder, string additionalPermissions = "", bool setOnBaseJobFolder = false)
        {
            List<string> fakeList = new List<string>();
            if (additionalPermissions != "")
            {
                fakeList.Add(additionalPermissions);
            }
            SetFolderPermissions(log, folder, fakeList, setOnBaseJobFolder);
        }

        /// <summary>
        /// Sets folder permissions depending on department(s) assigned to the job in the job index.
        /// </summary>
        /// <param name="folder">Path of the folder</param>
        /// <param name="jobIndex">Job index number</param>
        /// <param name="additionaPermissions">List of additional permissions to add for the folder</param>
        /// <param name="setOnBaseJobFolder">If true, this will go back up to the base job folder to set the permissions</param>
        public static void SetFolderPermissions(Logger log, string folder, List<string> additionalPermissions, bool setOnBaseJobFolder = false)
        {
            if (setOnBaseJobFolder)
            {
                folder = FindBaseJobFolder(folder);
            }

            additionalPermissions.Add("ITPHP");

            foreach (string additionalUser in additionalPermissions)
            {
                SetPermission(log, folder, additionalUser);
            }
        }


        public sealed class SafeTokenHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            private SafeTokenHandle()
                : base(true)
            {
            }

            [DllImport("kernel32.dll")]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            [SuppressUnmanagedCodeSecurity]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool CloseHandle(IntPtr handle);

            protected override bool ReleaseHandle()
            {
                return CloseHandle(handle);
            }
        }

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool LogonUser(String lpszUsername, String lpszDomain, String lpszPassword,
        int dwLogonType, int dwLogonProvider, out SafeTokenHandle phToken);

        private static void SetPermission(Logger log, string folder, string user)
        {
            //gives read permission only to specified user
            try
            { //System.Security.Principal.


                if (log.TestMode)
                {
                    //THANKS MICOSOFT DOCUMENTATION
                    SafeTokenHandle safeTokenHandle;
                    const int LOGON32_PROVIDER_DEFAULT = 0;
                    const int LOGON32_LOGON_INTERACTIVE = 2;

                    bool returnValue = LogonUser("user", "domain", ConfigurationManager.AppSettings["DriverTest"],
                        LOGON32_LOGON_INTERACTIVE, LOGON32_PROVIDER_DEFAULT,
                        out safeTokenHandle);

                    if (returnValue == false)
                    {
                        throw new System.ComponentModel.Win32Exception();
                    }
                    using (safeTokenHandle)
                    {
                        log.WriteToLog("Before impersonation: "
                        + WindowsIdentity.GetCurrent().Name);
                        using (WindowsIdentity newId = new WindowsIdentity(safeTokenHandle.DangerousGetHandle()))
                        {
                            using (WindowsImpersonationContext impersonatedUser = newId.Impersonate())
                            {
                                log.WriteToLog("After impersonation: "
                                    + WindowsIdentity.GetCurrent().Name);
                                DirectoryInfo directory = new DirectoryInfo(folder);
                                DirectorySecurity security = directory.GetAccessControl();
                                //security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.Read, InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly, AccessControlType.Allow));
                                security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.ReadAndExecute | FileSystemRights.Synchronize, InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit, PropagationFlags.None, AccessControlType.Allow));
                                directory.SetAccessControl(security);
                                UniversalLogger.WriteToLog(log, "Read permissions given to " + user + " for " + folder);
                            }
                        }
                        log.WriteToLog("After closing the context: " + WindowsIdentity.GetCurrent().Name);
                    }
                }
                else
                {
                    DirectoryInfo directory = new DirectoryInfo(folder);
                    DirectorySecurity security = directory.GetAccessControl();
                    //security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.Read, InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly, AccessControlType.Allow));
                    security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.ReadAndExecute | FileSystemRights.Synchronize, InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit, PropagationFlags.None, AccessControlType.Allow));
                    directory.SetAccessControl(security);
                    UniversalLogger.WriteToLog(log, "Read permissions given to " + user + " for " + folder);
                }

            } catch(Exception E)
            {
                UniversalLogger.WriteToLog(log, "Tried to set folder permission and failed (Probably invalid user/group name). " + E.ToString(), category: UniversalLogger.LogCategory.WARNING);
            }
        }

        private static string FindBaseJobFolder(string folder)
        {
            string newFolder = folder;
            string[] splitName = newFolder.Split('\\');

            int jobFolder = Array.IndexOf(splitName, "JobOutput") + 2;

            if (jobFolder != -1)
            {
                newFolder = string.Join("\\", splitName, 0, jobFolder);
            }

            return newFolder;
        }
    }
}