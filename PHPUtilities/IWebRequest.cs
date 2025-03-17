using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace Utilities
{
    public static class WebRequestUtilities
    {
        public static string parseArgs(string[] args)
        {
            string json = "";
            bool isJson = false;
            foreach (string arg in args)
            {
                if (arg.Contains("{"))
                {
                    isJson = true;
                }
                else if (arg.Contains("}"))
                {
                    json += arg;
                    isJson = false;
                    break;
                }
                if (isJson)
                {
                    json += arg;
                }
            }
            json = json.Replace("~", "\"");
            json = json.Replace("`", " ");
            return json;
        }
    }
    public class JobParameter
    {
        public JobParameter(string name, string description, Type datatype, List<String> dropDownOptions, bool multipleSelect, bool isOptional)
        {
            Name = name;
            SpacedName = Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
            Description = description;
            DataType = datatype;
            DropDownOptions = dropDownOptions;
            MultipleSelect = multipleSelect;
            IsOptional = isOptional;
        }

        public string Name { get; set; }
        public string SpacedName { get; set; }
        public string Description { get; set; }
        public Type DataType { get; set; }
        public List<String> DropDownOptions { get; set; }
        public bool MultipleSelect { get; set; }
        public bool IsOptional { get; set; }
    }

    public class MultiSelectAttribute : Attribute
    {

    }

    public class OptionalParam : Attribute
    {

    }

    public class ListSQLAttribute : Attribute
    {
        public string sql { get; set; }
        public string dataSource { get; set; }
        public ListSQLAttribute(string sql, string dataSource)
        {
            this.sql = sql;
            this.dataSource = dataSource;
        }
    }

    /// <summary>
    /// Interface for jobs with parameters being started off RunRequest
    /// </summary>
    public interface IWebRequest
    {
        /// <summary>
        /// Sets the internal RequestParameters object to the one passed in. Note, all properties defined should be camel case (use a capital letter to denote a new word and begin with a capital letter)
        /// </summary>
        void SetRequestParametersObject(object requestParameters);

        /// <summary>
        /// Sets the internal RequestParameters object to the args passed in
        /// </summary>
        void SetRequestParametersObject(string[] args);

        /// <summary>
        /// Returns an instance of the internal RequestParameters object
        /// </summary>
        object GetRequestParametersObject();

        /// <summary>
        /// Runs the main logic of the job and if it creates a report, return the location as a string
        /// </summary>
        string RunJob(out bool success);
    }
}
