using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace Utilities.FTP
{
    //Check on connection loss and behavior for that
    //Better error trapping
    //Better logging
    public class UMichFTP : IUMichFTP
    {
        public Logger Proc { get; set; }
        public FTPConnInfo ConnInfo { get; set; }

        public UMichFTP(Logger proc, string jobIDOverride = "")
        {
            Proc = proc;

            ConnInfo = GetConnectionInfo(jobIDOverride == "" ? Proc.ProcessId : jobIDOverride);
        }

        /// <summary>
        /// Download a file or files that match a search pattern
        /// </summary>
        /// <param name="searchString">A single file or search pattern to find and download all files that match the pattern</param>
        /// <param name="destination">Folder to drop the downloaded file(s)</param>
        /// <param name="deleteAfterDownload">Remove from FTP server after downloading</param>
        public void Download(string searchString, string destination, bool deleteAfterDownload = false)
        {
            IEnumerable<string> files = Search(ConnInfo.ChangeDirectory, searchString);
            Download(files, destination, deleteAfterDownload);
        }

        /// <summary>
        /// Download a list of files
        /// </summary>
        /// <param name="fileList">List of files to download</param>
        /// <param name="destination">Folder to drop the downloaded files</param>
        /// <param name="deleteAfterDownload">Remove from FTP server after downloading</param>
        public void Download(IEnumerable<string> fileList, string destination, bool deleteAfterDownload = false)
        {
            Proc.WriteToLog($"File(s) to be downloaded: " + string.Join(",", fileList));
            using (SftpClient sftp = GetFTPConnection())
            {
                sftp.Connect();
                foreach (string file in fileList)
                {
                    string fileandpath = Path.Combine(ConnInfo.ChangeDirectory, Path.GetFileName(file));

                    string localPath = Path.Combine(destination, Path.GetFileName(fileandpath));
                    using (FileStream fs = new FileStream(localPath, FileMode.Create))
                    {
                        try
                        {
                            Proc.WriteToLog($"Attempting to download: {fileandpath}");
                            sftp.DownloadFile(fileandpath, fs);
                        }
                        catch (SshException ex)
                        {
                            string msg = ex.Message;
                            if (msg.ToLower() == "failed to open local file")
                            {
                                throw new Exception("File could not be found");
                            }
                            else
                            {
                                throw ex;
                            }
                        }
                        catch (Exception ex)
                        {
                            Proc.WriteToLog(ex.ToString(), UniversalLogger.LogCategory.ERROR);
                        }


                    }
                    Proc.WriteToLog($"Download complete: {fileandpath} to {localPath}");

                    if (deleteAfterDownload)
                    {
                        sftp.DeleteFile(fileandpath);
                        Proc.WriteToLog($"Deleted: {fileandpath}");
                    }
                }
                sftp.Disconnect();
            }
        }


        /// <summary>
        /// Download a file or files that match a search pattern
        /// </summary>
        /// <param name="searchString">A single file or search pattern to find and download all files that match the pattern</param>
        /// <param name="destination">Folder to drop the downloaded file(s)</param>
        /// <param name="stagingPath">Location where file or files are staged while downloading, and where subsequent recovery runs will continue from</param>
        /// <param name="recover">Recover from prior download attempts</param>
        /// <param name="deleteAfterDownload">Remove from FTP server after downloading</param>
        /// <param name="maxThreads">Maximum number of concurrent downloads</param>
        public void DownloadRecoverable(string searchString, string destination, string stagingPath, bool recover = true, bool deleteAfterDownload = false, int maxThreads = 5)
        {
            IEnumerable<string> files = Search(ConnInfo.ChangeDirectory, searchString);
            DownloadRecoverable(files, destination, stagingPath, recover, deleteAfterDownload, maxThreads);
        }

        /// <summary>
        /// Download a list of files
        /// </summary>
        /// <param name="fileList">List of files to download</param>
        /// <param name="destination">Folder to drop the downloaded file(s)</param>
        /// <param name="stagingPath">Location where file or files are staged while downloading, and where subsequent recovery runs will continue from</param>
        /// <param name="recover">Recover from prior download attempts</param>
        /// <param name="deleteAfterDownload">Remove from FTP server after downloading</param>
        /// <param name="maxThreads">Maximum number of concurrent downloads</param>        
        public void DownloadRecoverable(IEnumerable<string> fileList, string destination, string stagingPath, bool recover = true, bool deleteAfterDownload = false, int maxThreads = 5)
        {
            Proc.WriteToLog($"File(s) to be downloaded: " + string.Join(",", fileList));
            int bufferSize = 8192;
            foreach (var file in fileList)
            {
                string stagingFile = Path.Combine(stagingPath, Path.GetFileName(file));
                string finalFile = Path.Combine(destination, Path.GetFileName(file));
                string fileandpath = Path.Combine(ConnInfo.ChangeDirectory, Path.GetFileName(file));

                if (recover && File.Exists(finalFile))
                {
                    Proc.WriteToLog($"Skipping download of {fileandpath}, already exists at {finalFile}");
                    continue;
                }

                bool downloadSuccessful = false;
                int attempt = 0;

                while (!downloadSuccessful && attempt < 2) // Attempt to connect and download, max 2 attempts
                {
                    try
                    {
                        using (SftpClient sftp = GetFTPConnection())
                        {
                            sftp.Connect();
                            long remoteFileSize = sftp.GetAttributes(fileandpath).Size;

                            // Calculate the chunk size based on the number of threads
                            long chunkSize = remoteFileSize / maxThreads;

                            Proc.WriteToLog($"Attempting to download: {fileandpath}. Attempt " + (attempt + 1).ToString());
                            Parallel.For(0, maxThreads, new ParallelOptions { MaxDegreeOfParallelism = maxThreads }, i =>
                            {
                                long offset = i * chunkSize;
                                long size = (i == maxThreads - 1) ? remoteFileSize - offset : chunkSize;
                                string chunkFile = Path.Combine(stagingPath, $"{Path.GetFileName(file)}.part{i}");

                                long existingChunkSize = 0;
                                if (recover && File.Exists(chunkFile))
                                {
                                    existingChunkSize = new FileInfo(chunkFile).Length;
                                    if (existingChunkSize >= size)
                                    {
                                        Proc.WriteToLog($"Skipping chunk {i + 1}/{maxThreads} of {fileandpath}, already exists at {chunkFile}");
                                        return;
                                    }
                                }

                                using (var fs = sftp.OpenRead(fileandpath))
                                {
                                    fs.Seek(offset + existingChunkSize, SeekOrigin.Begin);

                                    using (var chunkStream = new FileStream(chunkFile, FileMode.Append, FileAccess.Write))
                                    {
                                        byte[] buffer = new byte[bufferSize];
                                        long bytesRemaining = size - existingChunkSize;
                                        int bytesRead;
                                        while (bytesRemaining > 0 && (bytesRead = fs.Read(buffer, 0, (int)Math.Min(bufferSize, bytesRemaining))) > 0)
                                        {
                                            chunkStream.Write(buffer, 0, bytesRead);
                                            bytesRemaining -= bytesRead;
                                        }
                                    }
                                }

                                Proc.WriteToLog($"Downloaded chunk {i + 1}/{maxThreads} of {fileandpath} to {chunkFile}");
                            });

                            // Combine the chunk files into the final file
                            Proc.WriteToLog($"Combining file chunks into file: {finalFile}");
                            using (FileStream fsFinal = new FileStream(stagingFile, FileMode.Create, FileAccess.Write))
                            {
                                for (int i = 0; i < maxThreads; i++)
                                {
                                    string chunkFile = Path.Combine(stagingPath, $"{Path.GetFileName(file)}.part{i}");
                                    using (FileStream fsChunk = new FileStream(chunkFile, FileMode.Open, FileAccess.Read))
                                    {
                                        fsChunk.CopyTo(fsFinal);
                                    }
                                    File.Delete(chunkFile); // Clean up chunk file after combining
                                }
                            }

                            Proc.WriteToLog($"Completed download: {fileandpath} to staging {stagingFile}");

                            File.Move(stagingFile, finalFile);
                            Proc.WriteToLog($"Moved: {stagingFile} to {finalFile}");

                            if (deleteAfterDownload)
                            {
                                sftp.DeleteFile(fileandpath);
                                Proc.WriteToLog($"Deleted from remote host: {fileandpath}");
                            }

                            sftp.Disconnect();
                            downloadSuccessful = true; // Mark as successful if no exception occurs
                        }
                    }
                    catch (Exception ex)
                    {
                        Proc.WriteToLog($"Download failed for {fileandpath}. Attempt {attempt + 1}. Error: {ex.Message}");

                        if (++attempt >= 2)
                        {
                            Proc.WriteToLog($"Failed to download {fileandpath} after {attempt} attempts. Giving up.");
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Upload a single file or entire folder
        /// </summary>
        /// <param name="fileOrFolder">Folder name, or path and filename</param>
        /// <param name="maxThreads">Maximum number of threads for parallel operation</param>
        /// <returns>Boolean indicating success or failure of the operation</returns>
        public bool Upload(string fileOrFolder)
        {
            List<string> files = new List<string>();
            try
            {
                if (File.Exists(fileOrFolder))
                {
                    files.Add(fileOrFolder);
                }
                else
                {
                    files = Directory.GetFiles(fileOrFolder).ToList();
                }

            }
            catch (IOException ex)
            {
                Proc.WriteToLog(ex.Message, UniversalLogger.LogCategory.ERROR);
            }

            return Upload(files);
        }

        /// <summary>
        /// Upload a list of files
        /// </summary>
        /// <param name="fileList">List of path and filenames</param>
        /// <param name="maxThreads">Maximum number of threads for parallel operation</param>
        /// <returns>Boolean indicating success or failure of the operation</returns>
        public bool Upload(IEnumerable<string> fileList)
        {
            Proc.WriteToLog($"File(s) to be uploaded: " + string.Join(",", fileList));
            try
            {
                using (SftpClient sftp = GetFTPConnection())
                {
                    sftp.Connect();
                    foreach (string file in fileList)
                    {
                        Proc.WriteToLog($"Attempting to upload: {file}");
                        using (FileStream fs = new FileStream(file, FileMode.Open))
                        {
                            string targetPath = Path.Combine(ConnInfo.ChangeDirectory, Path.GetFileName(file)).Replace("\\", "/");
                            sftp.UploadFile(fs, targetPath);
                        }
                        Proc.WriteToLog($"Upload complete: {file}");
                    }
                    sftp.Disconnect();
                }
                return true;
            }
            catch (Exception ex)
            {
                Proc.WriteToLog($"Upload failed: {ex.Message}", UniversalLogger.LogCategory.ERROR);
                return false;
            }
        }

        /// <summary>
        /// Delete a file or files that match a search pattern
        /// </summary>
        /// <param name="searchString">A single file or search pattern to find and delete all files that match the pattern</param>
        /// <returns></returns>
        public bool Delete(string searchString)
        {
            IEnumerable<string> files = Search(ConnInfo.ChangeDirectory, searchString);
            return Delete(files);
        }

        /// <summary>
        /// Delete a list of files
        /// </summary>
        /// <param name="fileList">A list of files to be deleted</param>
        /// <returns></returns>
        public bool Delete(IEnumerable<string> fileList)
        {
            Proc.WriteToLog($"File(s) to be deleted: " + string.Join(",", fileList));
            try
            {
                using (SftpClient sftp = GetFTPConnection())
                {
                    sftp.Connect();
                    foreach (string file in fileList)
                    {
                        string fileandpath = Path.Combine(ConnInfo.ChangeDirectory, Path.GetFileName(file));
                        Proc.WriteToLog($"Attempting to delete: {file}");
                        try
                        {
                            sftp.DeleteFile(fileandpath);
                            Proc.WriteToLog($"Successfully deleted: {file}");
                        }
                        catch (Exception ex)
                        {
                            Proc.WriteToLog($"Delete of file ({file}) failed: {ex.Message}");
                            continue;
                        }
                    }
                    sftp.Disconnect();
                }
                return true;
            }
            catch (Exception ex)
            {
                Proc.WriteToLog($"Delete execution failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Search for a file or files using a search pattern in a specified directory
        /// </summary>
        /// <param name="directory">Directory on the FTP server in which to search</param>
        /// <param name="searchString">A single file or search pattern to find all files that match the pattern</param>
        /// <returns></returns>
        public IEnumerable<string> Search(string directory, string searchString)
        {
            IEnumerable<string> files;
            using (SftpClient sftp = GetFTPConnection())
            {
                sftp.Connect();
                files = sftp.ListDirectory(directory)
                            .Where(f => !f.IsDirectory && Regex.IsMatch(f.Name, searchString, RegexOptions.IgnoreCase))
                            .Select(f => f.FullName);
                sftp.Disconnect();
            }
            Proc.WriteToLog($"Search found {files.Count()} files.");
            return files;
        }

        private FTPConnInfo GetConnectionInfo(string JobId)
        {

            FTPConnInfo connectionInfo = ExtractFactory.ConnectAndQuery<FTPConnInfo>(Proc.LoggerPhpConfig, GetConnectionInfoQuery(JobId)).FirstOrDefault();

            if (connectionInfo == null)
            {
                if (Proc.TestMode)
                {
                    connectionInfo = ExtractFactory.ConnectAndQuery<FTPConnInfo>(Proc.LoggerPhpConfig, GetConnectionInfoQuery("TEST")).FirstOrDefault();
                }
                else
                {
                    throw new Exception("No FTP Connection information was found for " + JobId);
                }
            }

            connectionInfo = GetCredentials(connectionInfo);

            return connectionInfo;
        }

        private string GetConnectionInfoQuery(string jobId)
        {
            return $@"SELECT 
                      conn.SiteAddress,
                      conn.ChangeDirectory,
                      xwalk.ConnectionName,
                      conn.UseSSHKey,
                      conn.Port
                      FROM [PHPConfg].[dbo].[UMichFTP_JobToConnectionCrosswalk_C] as xwalk
                      INNER JOIN [PHPConfg].[dbo].[UMichFTP_Connections_C] as conn on xwalk.ConnectionName = conn.ConnectionName
                      WHERE xwalk.JobId = '{jobId}'";
        }

        private FTPConnInfo GetCredentials(FTPConnInfo conInfo)
        {
            conInfo.UserName = FileTransfer.GetElementFromKeyPass("UserName", conInfo.ConnectionName, acceptBlanks: true);

            conInfo.Password = FileTransfer.GetElementFromKeyPass("Password", conInfo.ConnectionName, acceptBlanks: true);

            if (conInfo.UseSSHKey)
            {
                conInfo.SSHKey = FileTransfer.GetElementFromKeyPass("PrivateKey", conInfo.ConnectionName);
            }


            return conInfo;
        }

        private SftpClient GetFTPConnection()
        {
            SftpClient connectionMade = null;
            int port = 22;

            if (!string.IsNullOrWhiteSpace(ConnInfo.Port))
            {
                port = Convert.ToInt16(ConnInfo.Port);
            }

            if (ConnInfo.UseSSHKey)
            {
                PrivateKeyFile[] keyFile = new PrivateKeyFile[] { new PrivateKeyFile(new MemoryStream(Encoding.UTF8.GetBytes(ConnInfo.SSHKey))) };

                if (string.IsNullOrWhiteSpace(ConnInfo.Password) && string.IsNullOrWhiteSpace(ConnInfo.UserName)) // Use SSH key only
                {
                    AuthenticationMethod[] authMethod = new AuthenticationMethod[] { new PrivateKeyAuthenticationMethod(string.Empty, keyFile) };
                    ConnectionInfo conn = new ConnectionInfo(ConnInfo.SiteAddress, port, "", authMethod);
                    connectionMade = new SftpClient(conn);
                }
                else if (string.IsNullOrWhiteSpace(ConnInfo.Password)) //Use username and SSH key
                {
                    AuthenticationMethod[] authMethod = new AuthenticationMethod[] { new PrivateKeyAuthenticationMethod(ConnInfo.UserName, keyFile) };
                    ConnectionInfo conn = new ConnectionInfo(ConnInfo.SiteAddress, port, ConnInfo.UserName, authMethod);
                    connectionMade = new SftpClient(conn);
                }
                else // Use username, password, and SSH key
                {
                    AuthenticationMethod[] authMethod = new AuthenticationMethod[]
                    {
                        new PasswordAuthenticationMethod(ConnInfo.UserName, ConnInfo.Password),
                        new PrivateKeyAuthenticationMethod(ConnInfo.UserName, keyFile)
                    };
                    ConnectionInfo conn = new ConnectionInfo(ConnInfo.SiteAddress, port, ConnInfo.UserName, authMethod);
                    connectionMade = new SftpClient(conn);
                }
            }
            else
            {
                // Use username and password
                connectionMade = new SftpClient(ConnInfo.SiteAddress, port, ConnInfo.UserName, ConnInfo.Password);
            }

            return connectionMade;
        }
    }
}
