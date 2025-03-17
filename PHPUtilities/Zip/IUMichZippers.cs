using System.Collections.Generic;


namespace Utilities.Zip
{
    public interface IUMichZippers
    {
        Logger Job { get; set; }
        void Zip(string fileOrFolder, string outputFile, string password = null);
        void Zip(IEnumerable<string> files, string outputFile, string password = null);
        void UnZip(string fileOrFolder, string target, string password = null);
        void UnZip(IEnumerable<string> files, string target, string password = null);
    }
}
