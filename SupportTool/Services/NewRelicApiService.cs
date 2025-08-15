using SupportTool.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using System.Diagnostics;
using System.Threading;
using System.Collections;
using SupportTool.Models;
using System.Globalization;

namespace SupportTool.Services
{
    public class CarrierDurationStatistics
    {
        public float AverageDuration { get; set; }
        public float StandardDeviation { get; set; }
        public bool HasData { get; set; }

        public CarrierDurationStatistics()
        {
            HasData = false;
            AverageDuration = 0f;
            StandardDeviation = 0f;
        }
    }

    public class NewRelicApiService
    {

        private readonly SettingsService _settingsService;

        public NewRelicApiService()
        {
            _settingsService = new SettingsService();
        }

        public async Task<List<string>> FetchCarriers(string stack, CancellationToken cancellationToken = default)
        {
            var carriers = new List<string>();
            try
            {
                string apiKey = _settingsService.GetSetting("NR_API_Key");
                if (string.IsNullOrEmpty(apiKey))
                {
                    throw new Exception("API key not found in settings.");
                }

                if (string.IsNullOrEmpty(stack))
                {
                    throw new Exception("Stack name is required.");
                }

                string url = "https://api.newrelic.com/graphql";
                string query = $@"  
               {{   
                   actor {{   
                       account(id: 400000) {{   
                           nrql(timeout: 120 query: ""SELECT uniques(CarrierName) FROM Transaction WHERE host LIKE '%-{stack}-%' and PrintOperation LIKE '%create%' SINCE 7 days ago"") {{   
                               results   
                           }}   
                       }}   
                   }}   
               }}";

                var requestBody = new { query = query };
                string jsonBody = JsonConvert.SerializeObject(requestBody);

                using (HttpClient client = new HttpClient())
                {
                    var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
                    };
                    requestMessage.Headers.Add("X-Api-Key", apiKey);

                    // Pass the cancellation token to the HTTP request
                    HttpResponseMessage response = await client.SendAsync(requestMessage, cancellationToken);
                    string responseContent = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var json = JsonConvert.DeserializeObject<JObject>(responseContent);
                        var results = json?["data"]?["actor"]?["account"]?["nrql"]?["results"]?.FirstOrDefault();

                        if (results?["uniques.CarrierName"] is JArray carriersArray)
                        {
                            foreach (var carrier in carriersArray)
                            {
                                carriers.Add(carrier.ToString());
                            }
                        }
                        else
                        {
                            throw new Exception("API returned empty or unexpected response.");
                        }

                        // Sort the carriers alphabetically
                        carriers.Sort();
                    }
                    else
                    {
                        throw new Exception($"HTTP Error: {response.StatusCode}, Response: {responseContent}.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation gracefully
                Debug.WriteLine("FetchCarriers was canceled.");
                throw; // Re-throw if you want the caller to handle the cancellation
            }
            catch (Exception ex)
            {
                // Handle or log the exception appropriately
                Debug.WriteLine($"Error in FetchCarriers: {ex.Message}");
                throw; // Re-throw for now
            }

            return carriers;
        }

        /// <summary>
        /// Fetches average duration and standard deviation for a specific carrier's PrintParcel transactions.
        /// </summary>
        /// <param name="carrierName">The name of the carrier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>CarrierDurationStatistics object.</returns>
        public async Task<CarrierDurationStatistics> FetchDurationStatisticsForCarrierAsync(string carrierName, CancellationToken cancellationToken = default)
        {
            var statistics = new CarrierDurationStatistics();
            try
            {
                string apiKey = _settingsService.GetSetting("NR_API_Key");
                if (string.IsNullOrEmpty(apiKey))
                {
                    throw new Exception("API key not found in settings.");
                }
                if (string.IsNullOrEmpty(carrierName))
                {
                    throw new ArgumentException("Carrier name cannot be empty.", nameof(carrierName));
                }
                int samplingDays = AlertTemplates.GetConfigValue<int>("PrintDuration.ProposedValues.SamplingDays");

                string url = "https://api.newrelic.com/graphql";
                string query = $@"{{
                    actor {{
                        account(id: 400000) {{
                            nrql(query: ""SELECT average(duration) AS 'AvgDuration', stddev(duration) AS 'StdDevDuration' FROM Transaction WHERE name LIKE '%.PrintParcel' AND CarrierName = '{carrierName}' AND PrintOperation LIKE '%Create%' SINCE {samplingDays} days ago LIMIT MAX"") {{
                                results
                            }}
                        }}
                    }}
                }}";

                using (HttpClient client = new HttpClient())
                {
                    var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = new StringContent(JsonConvert.SerializeObject(new { query }), Encoding.UTF8, "application/json")
                    };
                    requestMessage.Headers.Add("X-Api-Key", apiKey);

                    HttpResponseMessage response = await client.SendAsync(requestMessage, cancellationToken);
                    string responseContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"FetchDurationStatisticsForCarrierAsync API response for {carrierName}: {responseContent}");

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"HTTP Error: {response.StatusCode}, Response: {responseContent}");
                    }

                    var jsonResult = JsonConvert.DeserializeObject<JObject>(responseContent);

                    if (jsonResult?["errors"] != null && jsonResult["errors"].Any())
                    {
                        var errorMessage = jsonResult["errors"][0]?["message"]?.ToString() ?? "Unknown API error";
                        throw new Exception($"API Error: {errorMessage}");
                    }

                    var resultsArray = jsonResult?["data"]?["actor"]?["account"]?["nrql"]?["results"];
                    if (resultsArray == null || !resultsArray.Any())
                    {
                        Debug.WriteLine($"No results found in the response for carrier {carrierName}.");
                        return statistics;
                    }

                    var firstResult = resultsArray.First();
                    statistics.AverageDuration = firstResult["AvgDuration"]?.ToObject<float>() ?? 0f;
                    statistics.StandardDeviation = firstResult["StdDevDuration"]?.ToObject<float>() ?? 0f;
                    statistics.HasData = true;

                    Debug.WriteLine($"Carrier: {carrierName}, Avg: {statistics.AverageDuration}, StdDev: {statistics.StandardDeviation}");
                    return statistics;
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"FetchDurationStatisticsForCarrierAsync for {carrierName} was canceled.");
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in FetchDurationStatisticsForCarrierAsync for {carrierName}: {ex.Message}");
                return statistics;
            }
        }

        public async Task<Dictionary<string, CarrierDurationStatistics>> FetchDurationStatisticsForCarriersAsync(List<string> carrierNames, CancellationToken cancellationToken = default)
        {
            var statistics = new Dictionary<string, CarrierDurationStatistics>();
            try
            {
                string apiKey = _settingsService.GetSetting("NR_API_Key");
                if (string.IsNullOrEmpty(apiKey))
                {
                    throw new Exception("API key not found in settings.");
                }
                if (carrierNames == null || !carrierNames.Any())
                {
                    throw new ArgumentException("Carrier names list cannot be empty.", nameof(carrierNames));
                }

                int samplingDays = AlertTemplates.GetConfigValue<int>("PrintDuration.ProposedValues.SamplingDays");
                string carriersList = string.Join("', '", carrierNames.Select(c => c.Replace("'", "\\'")));

                string url = "https://api.newrelic.com/graphql";
                string query = $@"{{
                    actor {{
                        account(id: 400000) {{
                            nrql(query: ""SELECT average(duration) AS 'AvgDuration', stddev(duration) AS 'StdDevDuration' FROM Transaction WHERE name LIKE '%.PrintParcel' AND CarrierName in ('{carriersList}') AND PrintOperation LIKE '%Create%' SINCE {samplingDays} days ago FACET CarrierName LIMIT MAX"") {{
                                results
                            }}
                        }}
                    }}
                }}";

                using (HttpClient client = new HttpClient())
                {
                    var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = new StringContent(JsonConvert.SerializeObject(new { query }), Encoding.UTF8, "application/json")
                    };
                    requestMessage.Headers.Add("X-Api-Key", apiKey);

                    HttpResponseMessage response = await client.SendAsync(requestMessage, cancellationToken);
                    string responseContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"FetchDurationStatisticsForCarriersAsync API response: {responseContent}");

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"HTTP Error: {response.StatusCode}, Response: {responseContent}");
                    }

                    var jsonResult = JsonConvert.DeserializeObject<JObject>(responseContent);

                    if (jsonResult?["errors"] != null && jsonResult["errors"].Any())
                    {
                        var errorMessage = jsonResult["errors"][0]?["message"]?.ToString() ?? "Unknown API error";
                        throw new Exception($"API Error: {errorMessage}");
                    }

                    var resultsArray = jsonResult?["data"]?["actor"]?["account"]?["nrql"]?["results"];
                    if (resultsArray == null || !resultsArray.Any())
                    {
                        Debug.WriteLine("No results found in the response for any carrier.");
                        return statistics;
                    }

                    foreach (var resultItem in resultsArray)
                    {
                        string resultCarrierName = resultItem["CarrierName"]?.ToString();
                        if (string.IsNullOrEmpty(resultCarrierName)) continue;

                        var carrierStats = new CarrierDurationStatistics
                        {
                            AverageDuration = resultItem["AvgDuration"]?.ToObject<float>() ?? 0f,
                            StandardDeviation = resultItem["StdDevDuration"]?.ToObject<float>() ?? 0f,
                            HasData = true
                        };

                        Debug.WriteLine($"Carrier: {resultCarrierName}, Avg: {carrierStats.AverageDuration}, StdDev: {carrierStats.StandardDeviation}");
                        statistics[resultCarrierName] = carrierStats;
                    }

                    return statistics;
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("FetchDurationStatisticsForCarriersAsync was canceled.");
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in FetchDurationStatisticsForCarriersAsync: {ex.Message}");
                return statistics;
            }
        }
    }
}