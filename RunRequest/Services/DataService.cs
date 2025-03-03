using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Configuration;

namespace RunRequest.Services
{
    public class DataService
    {
        /** 
         ** For this service to work correctly, you must have the the logger endpoints setup in your Web.config
         *  In addition, you should have a test/prod variable.
         * 
         * <configuration>
         *      <appSettings>
         *          <add key="CreateLogEndpoint" value="<logger_endpoint>" />
         *          <add key="RunMode" value="<TEST/PROD>" />
         *      </appSettings>
         *  ...
         *  </configuration>
         *
         *  The EndpointUrl string will then dynamically pull the correct test/prod api string for you.
         **/

        private static readonly String DataEndpoint = WebConfigurationManager.AppSettings["GetDataEndpoint"];
        private static readonly bool TestMode = WebConfigurationManager.AppSettings["RunMode"].ToUpper().Equals("TEST");

        public enum DataBase { PhpConfg, PhpStaging, PhpArchv }

        public static DataTable GetDataFromTable(DataBase database, String table, String schema = "dbo")
        {
            return GetDataFromTable<DataTable>(database, table, schema: schema);
        }

        public static T GetDataFromTable<T>(DataBase database, String table, String schema = "dbo")
        {
            String databasePointer = database + (TestMode ? "Test" : "Prod");

            String endpoint = DataEndpoint + String.Format(@"Get/?database={0}&table={1}&schema={2}", databasePointer, table, schema);

            return ApiService.CallApi<T>(endpoint, "GET");
        }

        public static DataTable CallStoredProcedure(DataBase database, String procedure, String schema = "dbo")
        {
            return CallStoredProcedure<DataTable>(database, procedure, schema: schema);
        }

        public static T CallStoredProcedure<T>(DataBase database, String procedure, String schema = "dbo")
        {
            String databasePointer = database + (TestMode ? "Test" : "Prod");

            String endpoint = DataEndpoint + String.Format(@"{0}/SP/{1}?schema={2}", databasePointer, procedure, schema);

            return ApiService.CallApi<T>(endpoint, "POST");
        }

        public static DataTable CallStoredProcedureWithParams<T>(DataBase database, String procedure, T paramObject, String schema = "dbo")
        {
            String databasePointer = database + (TestMode ? "Test" : "Prod");

            String endpoint = DataEndpoint + String.Format(@"{0}/SP/{1}?schema={2}", databasePointer, procedure, schema);

            String payload = JsonConvert.SerializeObject(paramObject);

            return ApiService.CallApi<DataTable>(endpoint, "POST", payload: payload);
        }

        public static TModel CallStoredProcedureWithParams<TParam, TModel>(DataBase database, String procedure, TParam paramObject, String schema = "dbo")
        {
            String databasePointer = database + (TestMode ? "Test" : "Prod");

            String endpoint = DataEndpoint + String.Format(@"{0}/SP/{1}?schema={2}", databasePointer, procedure, schema);

            String payload = JsonConvert.SerializeObject(paramObject);

            return ApiService.CallApi<TModel>(endpoint, "POST", payload: payload);
        }

    }
}