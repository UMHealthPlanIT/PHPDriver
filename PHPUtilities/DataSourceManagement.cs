using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;

namespace Utilities
{
    public class DataSourceManagement
    {
        /// <summary>
        /// Obtains status of specified data source
        /// </summary>
        /// <param name="source"></param>
        /// <param name="testMode"></param>
        /// <returns></returns>
        public static APIWork.DataSource GetIndividualDataSourceStatus(string source, bool testMode)
        {
            string endpoint = $"{GetDataStationBaseUrl(testMode)}DataSource/CheckDataSource?parameters={source}";

            string result;

            using (WebClient client = new WebClient())
            {
                client.UseDefaultCredentials = true;
                result = client.DownloadString(endpoint);
            }

            return JsonConvert.DeserializeObject<APIWork.DataSource>(result);
        }

        /// <summary>
        /// Toggle data source between the On and Off states
        /// </summary>
        /// <param name="dataSource"></param>
        /// <param name="isReady"></param>
        /// <param name="overrideInsteadOfReady"></param>
        /// <param name="testMode"></param>
        public static void ToggleSource(string dataSource, bool isReady, bool overrideInsteadOfReady, bool testMode)
        {
            using (WebClient client = new WebClient())
            {
                client.UseDefaultCredentials = true;
                client.DownloadString($"{GetDataStationBaseUrl(testMode)}DataSource/ToggleSource?source=" + dataSource + "&yesOrNo=" + isReady.ToString() + "&overrideInsteadOfReady=" + overrideInsteadOfReady.ToString());
            }
        }

        /// <summary>
        /// Returns false if any data source attributed to this job is unavailable, or the Master switch is set to off.
        /// </summary>
        /// <param name="jobId">Job Index Identifier.</param>
        /// <param name="procLog">Logger object used for determining current environment and logging on behalf of the calling class.</param>
        /// <returns>Dictionary of source ready status</returns>
        public static bool JobSourcesAreReady(String jobId, Logger procLog)
        {
            bool ready;

            JobIndex programJob = new JobIndex(jobId.Length > 7 ? jobId.Substring(0, 7) : jobId);

            ready = JobSourcesAreReady(programJob, procLog);

            return ready;
        }

        /// <summary>
        /// Returns false if any data source attributed to this job is unavailable, or the Master switch is set to off.
        /// </summary>
        /// <param name="jobData">Data pertaining to the given job from the Job Index.</param>
        /// <param name="procLog">Logger object used for determining current environment and logging on behalf of the calling class.</param>
        /// <returns>Dictionary of source ready status</returns>
        public static bool JobSourcesAreReady(JobIndex jobData, Logger procLog)
        {
            bool ready = true;

            Dictionary<string, string> dataSourceReadinessList = SourceStatus(procLog.TestMode);

            //If any data sources are listed as not ready, check those against the list of job sources for this job
            foreach (string dataSource in jobData.DataSourceList)
            {
                if (dataSourceReadinessList.ContainsKey(dataSource)) //Does this job contain an unknown data source
                {
                    if (dataSourceReadinessList["Master"] == "No" || dataSourceReadinessList[dataSource] == "No")
                    {
                        ready = false;
                        procLog.WriteToLog($"{dataSource} was found to be unavailable");
                    }
                }
            }

            return ready;
        }


        /// <summary>
        /// Obtains list of objects of type APIWork.DataSource of data sources and their status from JobCentralControl API
        /// </summary>
        /// <param name="testMode"></param>
        /// <returns></returns>
        public static List<APIWork.DataSource> SourceStatusAsDataSource(bool testMode)
        {
            List<APIWork.DataSource> dataSources = new List<APIWork.DataSource>();

            using (WebClient client = new WebClient())
            {
                client.UseDefaultCredentials = true;
                string result = client.DownloadString($"{GetDataStationBaseUrl(testMode)}DataSource/CheckDataSource");

                JArray reObject = JArray.Parse(result);
                foreach (JValue source in reObject)
                {
                    dataSources.Add(GetIndividualDataSourceStatus(source.ToString(), testMode));
                }
            }

            return dataSources;
        }

        /// <summary>
        /// Obtains dictionary of data sources and their status from JobCentralControl API
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, string> SourceStatus(bool testMode)
        {
            Dictionary<string, string> sourceStatus = new Dictionary<string, string>();

            List<APIWork.DataSource> dataSources = SourceStatusAsDataSource(testMode);

            if (dataSources.Count > 0)
            {
                foreach (APIWork.DataSource dataSource in dataSources)
                {
                    sourceStatus.Add(dataSource.name, dataSource.sourceReady ? "Yes" : "No");
                }
            }
            else //If for some reason the data source readiness API fails to return data, we'll assume things are down and not try to run any jobs
            {
                sourceStatus.Add("Master", "No");
            }

            return sourceStatus;
        }

        public static List<APIWork.DataSource> DataSourceNamesAsDataSource(bool testMode)
        {
             List<APIWork.DataSource> dataSources = new List<APIWork.DataSource>();

            using (WebClient client = new WebClient())
            {
                client.UseDefaultCredentials = true;
                string result = client.DownloadString($"{GetDataStationBaseUrl(testMode)}DataSource/CheckDataSource");

                JArray reObject = JArray.Parse(result);
                foreach (JValue source in reObject)
                {
                    APIWork.DataSource tempSource = new APIWork.DataSource();
                    tempSource.name = source.ToString();
                    dataSources.Add(tempSource);
                }
            }

            return dataSources;
        }

        private static string GetDataStationBaseUrl(bool isTestMode)
        {
            return isTestMode ? 
                "https:///api/DataStation/api/" :
                "https:///api/DataStation/api/";
        }
    }
}