using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SupportTool.Features.Alerts.Services;

namespace SupportTool.Features.Alerts.Helpers
{
    /// <summary>
    /// Helper class for common New Relic API operations
    /// </summary>
    public static class NewRelicApiHelper
    {
        private const string GraphQLEndpoint = "https://api.newrelic.com/graphql";
        private const int AccountId = 400000;

        /// <summary>
        /// Validates and retrieves the API key from settings
        /// </summary>
        public static string GetApiKey(SettingsService settingsService)
        {
            string apiKey = settingsService.GetSetting("NR_API_Key");
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new Exception("API key not found in settings.");
            }
            return apiKey;
        }

        /// <summary>
        /// Executes a GraphQL query against New Relic API
        /// </summary>
        /// <param name="query">The GraphQL query string</param>
        /// <param name="apiKey">The API key for authentication</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The JSON response as a JObject</returns>
        public static async Task<JObject> ExecuteGraphQLQueryAsync(string query, string apiKey, CancellationToken cancellationToken = default)
        {
            var requestBody = new { query };
            string jsonBody = JsonConvert.SerializeObject(requestBody);

            using (HttpClient client = new HttpClient())
            {
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, GraphQLEndpoint)
                {
                    Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
                };
                requestMessage.Headers.Add("X-Api-Key", apiKey);

                HttpResponseMessage response = await client.SendAsync(requestMessage, cancellationToken);
                string responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"HTTP Error: {response.StatusCode}, Response: {responseContent}");
                }

                var jsonResult = JsonConvert.DeserializeObject<JObject>(responseContent);

                // Check for GraphQL errors
                if (jsonResult?["errors"] != null && jsonResult["errors"].Any())
                {
                    var errorMessage = jsonResult["errors"][0]?["message"]?.ToString() ?? "Unknown API error";
                    throw new Exception($"API Error: {errorMessage}");
                }

                return jsonResult;
            }
        }

        /// <summary>
        /// Builds a GraphQL query for NRQL execution
        /// </summary>
        /// <param name="nrqlQuery">The NRQL query string</param>
        /// <param name="timeout">Query timeout in seconds (default: 120)</param>
        /// <returns>The formatted GraphQL query</returns>
        public static string BuildNrqlGraphQLQuery(string nrqlQuery, int timeout = 120)
        {
            return $@"{{
                actor {{
                    account(id: {AccountId}) {{
                        nrql(timeout: {timeout} query: ""{nrqlQuery}"") {{
                            results
                        }}
                    }}
                }}
            }}";
        }

        /// <summary>
        /// Extracts the results array from a GraphQL response
        /// </summary>
        /// <param name="jsonResult">The JSON response object</param>
        /// <returns>The results array or null if not found</returns>
        public static JArray ExtractResultsArray(JObject jsonResult)
        {
            return jsonResult?["data"]?["actor"]?["account"]?["nrql"]?["results"] as JArray;
        }

        /// <summary>
        /// Extracts the first result object from a GraphQL response
        /// </summary>
        /// <param name="jsonResult">The JSON response object</param>
        /// <returns>The first result object or null if not found</returns>
        public static JObject ExtractFirstResult(JObject jsonResult)
        {
            var results = jsonResult?["data"]?["actor"]?["account"]?["nrql"]?["results"];
            return results?.FirstOrDefault() as JObject;
        }
    }
}
