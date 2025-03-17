using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Utilities.FTP
{
    public class FTPConnInfo
    {
        public string ConnectionName { get; set; }
        public string SiteAddress { get; set; }
        private string _ChangeDirectory { get; set; }
        public string ChangeDirectory
        {
            get
            {
                if (String.IsNullOrEmpty(_ChangeDirectory))
                {
                    if (_ChangeDirectory.Substring(0, 1) != "/")
                    {
                        _ChangeDirectory = "/" + _ChangeDirectory;
                    }
                    if (_ChangeDirectory.Substring(_ChangeDirectory.Length - 1, 1) != "/")
                    {
                        _ChangeDirectory = _ChangeDirectory + "/";
                    }
                }
                return _ChangeDirectory;
            }
            set
            {
                _ChangeDirectory = value;
            }
        }
        public string UserName { get; set; }
        public string Password { get; set; }
        public bool UseSSHKey { get; set; }
        public string SSHKey { get; set; }
        public string Port { get; set; }
    }
}
