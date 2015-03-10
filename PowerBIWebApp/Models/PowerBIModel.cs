// Copyright Microsoft

using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using PowerBIWebApp.Utils;

namespace PowerBIWebApp.Models
{
    public class PowerBIModel
    {
        static Uri baseAddress = new Uri("https://api.powerbi.com/beta/myorg/");

        /// <summary>
        /// Get resource specific access token for Power BI ("https://analysis.windows.net/powerbi/api")
        /// </summary>
        /// <returns>Access Token string</returns>
        private static async Task<string> getAccessToken()
        {
            // Create auth context (note: token is not cached)
            AuthenticationContext authContext = new AuthenticationContext(Settings.AzureADAuthority);

            // Create client credential
            var clientCredential = new ClientCredential(Settings.ClientId, Settings.Key);
            
            // Get user object id
            var userObjectId = ClaimsPrincipal.Current.FindFirst(Settings.ClaimTypeObjectIdentifier).Value;

            // Get access token for Power BI
            // Call Power BI APIs from Web API on behalf of a user
            return authContext.AcquireToken(Settings.PowerBIResourceId, clientCredential, new UserAssertion(userObjectId, UserIdentifierType.UniqueId.ToString())).AccessToken;
        }

        /// <summary>
        /// Get all datasets for a user
        /// </summary>
        /// <returns>List of PowerBIDataset</returns>
        public static async Task<List<PowerBIDataset>> GetDatasets()
        {
            List<PowerBIDataset> datasets = new List<PowerBIDataset>();
            var token = await getAccessToken();
            
            using (var client = new HttpClient{ BaseAddress = baseAddress })
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                client.DefaultRequestHeaders.Add("Accept", "application/json; odata=verbose");

                using (var response = await client.GetAsync(String.Format("{0}/datasets", baseAddress)))
                {
                    string responseString = await response.Content.ReadAsStringAsync();
                    JObject oResponse = JObject.Parse(responseString);
                    datasets = oResponse.SelectToken("datasets").ToObject<List<PowerBIDataset>>();
                }
            }

            return datasets;
        }

        /// <summary>
        /// Get a specific dataset based on id
        /// </summary>
        /// <param name="id">Guid id of dataset</param>
        /// <returns>PowerBIDataset</returns>
        public static async Task<PowerBIDataset> GetDataset(Guid id)
        {
            PowerBIDataset dataset = null;
            var token = await getAccessToken();
           
            using (var client = new HttpClient { BaseAddress = baseAddress })
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                client.DefaultRequestHeaders.Add("Accept", "application/json; odata=verbose");

                using (var response = await client.GetAsync(String.Format("{0}/datasets/{1}", baseAddress, id.ToString())))
                {
                    string responseString = await response.Content.ReadAsStringAsync();
                    JObject oResponse = JObject.Parse(responseString);
                }
            }

            return dataset;
        }

        /// <summary>
        /// Create a dataset, including tables/columns
        /// </summary>
        /// <param name="dataset">PowerBIDataset</param>
        /// <returns>Guid id of the new dataset</returns>
        public static async Task<Guid> CreateDataset(PowerBIDataset dataset)
        {
            var token = await getAccessToken();
            
            using (var client = new HttpClient{ BaseAddress = baseAddress })
            {
                var content = new StringContent(JsonConvert.SerializeObject(dataset).Replace("\"id\":\"00000000-0000-0000-0000-000000000000\",", ""), System.Text.Encoding.Default, "application/json");
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                client.DefaultRequestHeaders.Add("Accept", "application/json");

                using (var response = await client.PostAsync(String.Format("{0}/datasets", baseAddress), content))
                {
                    string responseString = await response.Content.ReadAsStringAsync();
                    JObject oResponse = JObject.Parse(responseString);
                    dataset.id = new Guid(oResponse.SelectToken("id").ToString());
                }
            }

            return dataset.id;
        }

        /// <summary>
        /// Clear all data our of a given table of a dataset
        /// </summary>
        /// <param name="dataset">Guid dataset igd</param>
        /// <param name="table">string table name</param>
        /// <returns>bool indicating success</returns>
        public static async Task<bool> ClearTable(Guid dataset, string table)
        {
            bool success = false;
            var token = await getAccessToken();
           
            using (var client = new HttpClient { BaseAddress = baseAddress })
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                using (var response = await client.DeleteAsync(String.Format("{0}/datasets/{1}/tables/{2}/rows", baseAddress, dataset.ToString(), table)))
                {
                    string responseString = await response.Content.ReadAsStringAsync();
                    success = true;
                }
            }

            return success;
        }

        /// <summary>
        /// Add rows to a given table and dataset in Power BI
        /// </summary>
        /// <param name="dataset">PowerBIDataset</param>
        /// <param name="table">PowerBITable</param>
        /// <param name="rows">List<Dictionary<string, object>></param>
        /// <returns></returns>
        public static async Task<bool> AddTableRows(Guid dataset, string table, List<Dictionary<string, object>> rows)
        {
            bool success = false;
            var token = await getAccessToken();
            
            using (var client = new HttpClient { BaseAddress = baseAddress })
            {
                //build the json post by looping through the rows and columns for each row
                string json = "{\"rows\": [";
                foreach (var row in rows)
                {
                    //process each column on the row
                    json += "{";
                    foreach (var key in row.Keys)
                    {
                        json += String.Format("\"{0}\":\"{1}\",", key, row[key].ToString());
                    }
                    json = json.Substring(0, json.Length - 1) + "},";
                }

                json = json.Substring(0, json.Length - 1) + "]}";
                var content = new StringContent(json, System.Text.Encoding.Default, "application/json");
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                using (var response = await client.PostAsync(String.Format("{0}/datasets/{1}/tables/{2}/rows", baseAddress, dataset.ToString(), table), content))
                {
                    string responseString = await response.Content.ReadAsStringAsync();
                    success = true;
                }
            }

            return success;
        }
    }

    public class PowerBIDataset
    {
        public Guid id { get; set; }
        public string name { get; set; }
        public List<PowerBITable> tables { get; set; }
    }

    public class PowerBIColumn
    {
        public string name { get; set; }
        public string dataType { get; set; }
    }

    public class PowerBITable
    {
        public string name { get; set; }
        public List<PowerBIColumn> columns { get; set; }
    }

    public class PowerBITableRef
    {
        public Guid datasetId { get; set; }
        public string tableName { get; set; }
    }

    public class PowerBITableRows : PowerBITableRef
    {
        public List<Dictionary<string, object>> rows;
    }
}
