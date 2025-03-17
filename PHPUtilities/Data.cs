using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Reflection;
using System.Data.SqlClient;
using System.Data.Odbc;
using System.Data.Linq;
using System.Configuration;
//using Oracle.ManagedDataAccess.Client;
using System.IO;

namespace Utilities
{
    /// <summary>
    /// This class pulls in data access configuration from the DataConfiguration.xml file, uses those details to prepare the connection string and object
    /// and exposes methods for opening the connection based on the driver specified in the configuration file.
    /// </summary>
    public class Data
    {
        public String Authentication;
        String TargetDatabase;
        public String username = "";

        private string _password;

        public String password
        {
            get
            {
                if (_password == null)
                {
                    if(ApplicationName == AppNames.ExampleTest)
                    {
                        //temp log that we're hitting config manager
                        UniversalLogger.WriteToLog(new Logger(new Logger.LaunchRequest("ADMIN", false, null)), "ConfigManager hit for " + ApplicationName.ToString());
                        _password = ConfigurationManager.AppSettings[ApplicationName.ToString()];
                    }
                    else
                    {
                        _password = FileTransfer.GetKeyPassPassword(this.ApplicationName.ToString());
                    }
                    
                }

                return _password;
            }
        }
        public String server = "";
        String MyDriver = "";
        String Port = "";
        private SqlConnection MySqlConnection;
        private OdbcConnection OtherConnection;
        public String connectionString;
        public AppNames ApplicationName;

        public enum AppNames
        {
            ExampleTest, ExampleProd
        }
        /// <summary>
        /// Loads the database access details from the Data.Configuration.xml file
        /// </summary>
        /// <param name="AppName">The name of the application you are connecting to (see DataConfiguration.xml file) and AppNames enum</param>
        public Data(AppNames AppName)
        {
            XmlDocument xmldoc = new XmlDocument();

            ApplicationName = AppName;

            try
            {
                xmldoc.Load(ConfigurationManager.AppSettings["DataConfigPath"]);
            }
            catch (Exception e)
            {
                if (e is System.IO.FileNotFoundException || e is System.IO.DirectoryNotFoundException) 
                {
                    // This is fallback for local development. Since local runs out of the base folder of
                    // the repo, rather than a build location. It'll go up to the parent directory (repo root).
                    DirectoryInfo dataFileBasePath = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                    while (!string.Equals(dataFileBasePath.Name, "JobsApps", StringComparison.OrdinalIgnoreCase))
                    {
                        dataFileBasePath = dataFileBasePath.Parent;
                    }
                    xmldoc.Load(dataFileBasePath.FullName + @"\DataConfiguration.xml");
                }
                else
                {
                    throw;
                }
            }

            XmlNodeList nodeList;
            XmlElement root = xmldoc.DocumentElement;

            nodeList = root.SelectNodes("//Database[@appName='" + AppName.ToString() + "']");
            if (nodeList.Count > 0)
            {
                XmlNodeReader ConfigReader = new XmlNodeReader(nodeList.Item(0));

                ConfigReader.Read(); //Get us into the Database element
                TargetDatabase = ConfigReader.GetAttribute(0);
                do
                {
                    server = (ConfigReader.Name.ToUpper() == "SERVER" ? ConfigReader.ReadString() : server);
                    Port = (ConfigReader.Name.ToUpper() == "PORT" ? ConfigReader.ReadString() : Port);
                    MyDriver = (ConfigReader.Name.ToUpper() == "DRIVER" ? ConfigReader.ReadString() : MyDriver);
                    username = (ConfigReader.Name.ToUpper() == "USERNAME" ? ConfigReader.ReadString() : username);
                } while (ConfigReader.Read());

            }
            else
            {
                throw new Exception("Database or access configuration not found, check spelling and case (the XPath query is case sensitive)");
            }

            if (username != "" && password != "")
            {
                Authentication = String.Format("Uid={0};Pwd={1};", username, password);
            }
            else
            {
                Authentication = "Trusted_Connection=True;";
            }

            if (server != "" && TargetDatabase != "SFTP" && TargetDatabase != "Exchange")
            {
                if (MyDriver == null || MyDriver == "") //Only specify a driver if you are connecting to something other than a SQL DB
                {
                    MySqlConnection = GetSqlConnection(Authentication);
                }
                else
                {
                    OtherConnection = GetOdbcConnection();
                }
            }

        }

        public SqlConnection GetSqlConnection(String Auth)
        {
            connectionString = String.Format("Server={0};Database={1};{2}", server, TargetDatabase, Auth);
            return new SqlConnection(connectionString);
        }

        public OdbcConnection GetOdbcConnection()
        {
            if (ApplicationName == AppNames.ExampleTest)
            {
                connectionString = String.Format("DSN={0};Uid={1};Pwd={2};", TargetDatabase, username, password);
            }
            else
            {
                connectionString = String.Format("Driver={0};Server={1};Port={2};Database={3};Uid={4};Pwd={5};", MyDriver, server, Port, TargetDatabase, username, password);
            }
            return new OdbcConnection(connectionString);
        }

        //public OracleConnection GetOracleConnection()
        //{
        //    string connectionString = $"User Id={username}; password={password}; Data Source={server}/{TargetDatabase};";

        //    return new OracleConnection(connectionString);
        //}

        /// <summary>
        /// Based on the driver defined in DataConfiguration.xml, this opens the appropriate connection and connects to the appropriate DataContext
        /// </summary>
        /// <param name="timeout">Seconds for timeout (default is 1800 seconds, i.e. 30 minutes)</param>
        /// <returns>DataContext for the database defined in the original Data.cs constructor</returns>
        public DataContext OpenConnectionAndGetDatabase(int timeout = 0)
        {
            DataContext DbContext;
            if (MySqlConnection != null)
            {
                MySqlConnection.Open();
                DbContext = new DataContext(MySqlConnection);
                DbContext.CommandTimeout = timeout;
            }
            else
            {
                OtherConnection.Open();
                DbContext = new DataContext(OtherConnection);
                DbContext.CommandTimeout = timeout;
            }

            return DbContext;

        }

        /// <summary>
        /// Based on the driver defined in DataConfiguration.xml, this opens the appropriate connection and connects to the appropriate DataContext. Defaults to 240 second timeout unless using other override.
        /// </summary>
        /// <returns>DataContext for the database defined in the original Data.cs constructor</returns>
        public DataContext OpenConnectionAndGetDatabase()
        {
            return OpenConnectionAndGetDatabase(1000);
        }

        /// <summary>
        /// Closes and disposes of the connections maintained in the Data.cs class, based on the driver defined in DataConfiguration.xml
        /// </summary>
        public void CloseConnection()
        {
            if (MySqlConnection != null)
            {
                MySqlConnection.Close();
                //MySqlConnection.Dispose();
            }
            else
            {
                OtherConnection.Close();
                //OtherConnection.Dispose(); If we dispose this we lose the ability re-query the database connection
            }
        }

        /// <summary>
        /// Write an object to a table in the given database. Table and columns are mapped in the attributes of the class definition.
        /// </summary>
        /// <typeparam name="T">Class of object to be written</typeparam>
        /// <param name="classToWrite">Specific object to be written</param>
        /// <param name="dbName">Name of database</param>
        public static void ObjectToTable<T>(object objectToWrite, Data.AppNames dbName) where T : class
        {
            T convertedObject = (T)objectToWrite;
            Data databaseConn = new Data(dbName);
            DataContext db = databaseConn.OpenConnectionAndGetDatabase();
            Table<T> APILog = db.GetTable<T>();
            APILog.InsertOnSubmit(convertedObject);

            db.SubmitChanges();
            databaseConn.CloseConnection();
            db.Dispose();
        }

        public static Data.AppNames GetDataSource(string dataSourceString, Logger program)
        {
            Data.AppNames dataSource = new AppNames();
            switch (dataSourceString)
            {
                case "Example":
                    if (program.TestMode)
                    {
                        dataSource = Data.AppNames.ExampleTest;
                    }
                    else
                    {
                        dataSource = Data.AppNames.ExampleProd;
                    }
                    break;
                default:
                    dataSource = Data.AppNames.ExampleTest;
                    break;
            }

            return dataSource;
        }
    }
}
