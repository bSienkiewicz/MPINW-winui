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
    public class NewRelicApiService
    {
        private readonly SettingsService _settingsService;

        public NewRelicApiService()
        {
            _settingsService = new SettingsService();
        }

        public async Task<List<AppNameItem>> FetchAppNamesAndCarriers(string stack, CancellationToken cancellationToken = default)
        {
            var appNames = new List<AppNameItem>();
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
                           nrql(timeout: 120 query: ""SELECT uniques(CarrierName) FROM Transaction WHERE host LIKE '%{stack}%' and PrintOperation LIKE '%create%' SINCE 7 days ago FACET appName"") {{   
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
                        var result = JsonConvert.DeserializeObject<NewRelicResponse>(responseContent);

                        if (result?.Data?.Actor?.Account?.Nrql?.Results == null)
                        {
                            throw new Exception("API returned empty or unexpected response.");
                        }

                        var appNameToCarriersMap = new Dictionary<string, AppNameItem>();

                        foreach (var resultData in result.Data.Actor.Account.Nrql.Results)
                        {
                            if (resultData.TryGetValue("facet", out var appNameObj))
                            {
                                string appName = appNameObj.ToString();

                                if (!appNameToCarriersMap.ContainsKey(appName))
                                {
                                    appNameToCarriersMap[appName] = new AppNameItem { AppName = appName };
                                }

                                if (resultData.TryGetValue("uniques.CarrierName", out var carriersObj) && carriersObj is JArray carriersArray)
                                {
                                    foreach (var carrier in carriersArray)
                                    {
                                        appNameToCarriersMap[appName].Carriers.Add(new CarrierItem { CarrierName = carrier.ToString() });
                                    }
                                }
                            }
                        }

                        foreach (var appNameItem in appNameToCarriersMap.Values)
                        {
                            appNameItem.Carriers = appNameItem.Carriers.OrderBy(c => c.CarrierName).ToList();
                            appNames.Add(appNameItem);
                        }

                        appNames = appNames.OrderBy(a => a.AppName).ToList();
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
                Debug.WriteLine("FetchAppNamesAndCarriers was canceled.");
                throw; // Re-throw if you want the caller to handle the cancellation
            }
            catch (Exception ex)
            {
                // Handle or log the exception appropriately
                Debug.WriteLine($"Error in FetchAppNamesAndCarriers: {ex.Message}");
                throw; // Re-throw for now
            }

            return appNames;
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
                           nrql(timeout: 120 query: ""SELECT uniques(CarrierName) FROM Transaction WHERE host LIKE '%{stack}%' and PrintOperation LIKE '%create%' SINCE 7 days ago"") {{   
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


        //public async Task<NRMetricsResult> FetchMetricsForAppNameAndCarrier(string AppName, string CarrierName, CancellationToken cancellationToken = default)
        //{
        //    try
        //    {
        //        string apiKey = _settingsService.GetSetting("NR_API_Key");
        //        if (string.IsNullOrEmpty(apiKey))
        //        {
        //            throw new Exception("API key not found in settings.");
        //        }

        //        if (string.IsNullOrEmpty(AppName) || string.IsNullOrEmpty(CarrierName))
        //        {
        //            throw new Exception("App Name and Carrier Name are required.");
        //        }

        //        string url = "https://api.newrelic.com/graphql";

        //        string query = $@"{{
        //                actor {{
        //                    account(id: 400000) {{
        //                      nrql(query: ""FROM Transaction SELECT percentile(duration, 50) AS 'MedianDuration', count(*) AS 'CreateCalls', (filter(count(*), WHERE CarrierName = '{CarrierName}') * 100.0 / count(*)) AS 'CarrierPercentage' WHERE appName = '{AppName}' AND name = 'WebTransaction/WCF/XLogics.BlackBox.ServiceContracts.IBlackBoxContract.PrintParcel' AND PrintOperation LIKE '%Create%' SINCE 7 days ago"") {{
        //                            results
        //                        }}
        //                    }}
        //                }}
        //            }}";

        //        var requestBody = new { query = query };
        //        string jsonBody = JsonConvert.SerializeObject(requestBody);

        //        using (HttpClient client = new HttpClient())
        //        {
        //            var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
        //            {
        //                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        //            };
        //            requestMessage.Headers.Add("X-Api-Key", apiKey);

        //            HttpResponseMessage response = await client.SendAsync(requestMessage, cancellationToken);
        //            string responseContent = await response.Content.ReadAsStringAsync();

        //            if (!response.IsSuccessStatusCode)
        //            {
        //                throw new Exception($"HTTP Error: {response.StatusCode}, Response: {responseContent}.");
        //            }

        //            var result = JsonConvert.DeserializeObject<JObject>(responseContent);
        //            var data = result?["data"]?["actor"]?["account"]?["nrql"]?["results"]?.FirstOrDefault();

        //            if (data == null)
        //            {
        //                throw new Exception("Invalid response format or missing data.");
        //            }

        //            return new NRMetricsResult
        //            {
        //                MedianDuration = data["MedianDuration"]?["50"]?.Value<float>() ?? 0f,
        //                CreateCalls = data["CreateCalls"]?.Value<int>() ?? 0,
        //                CarrierPercentage = data["CarrierPercentage"]?.Value<float>() ?? 0f
        //            };
        //        }
        //    }
        //    catch (OperationCanceledException)
        //    {
        //        Debug.WriteLine("FetchMetricsForAppNameAndCarrier was canceled.");
        //        throw;
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine($"Error in FetchMetricsForAppNameAndCarrier: {ex.Message}");
        //        throw;
        //    }
        //}

        public async Task<float> FetchDurationForCarrier(string carrierName, CancellationToken cancellationToken = default)
        {
            try
            {
                string apiKey = _settingsService.GetSetting("NR_API_Key");
                if (string.IsNullOrEmpty(apiKey))
                {
                    throw new Exception("API key not found in settings.");
                }

                string url = "https://api.newrelic.com/graphql";
                string query = $@"{{
            actor {{
                account(id: 400000) {{
                    nrql(query: ""SELECT median(webDuration) AS 'MedianDuration' FROM Transaction WHERE name LIKE '%.PrintParcel' AND CarrierName = '{carrierName}' AND PrintOperation LIKE '%Create%' SINCE 7 days ago"") {{
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

                    // For debugging
                    Debug.WriteLine($"API response: {responseContent}");

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"HTTP Error: {response.StatusCode}, Response: {responseContent}");
                    }

                    var result = JsonConvert.DeserializeObject<JObject>(responseContent);

                    // Check for errors
                    if (result["errors"] != null && result["errors"].HasValues)
                    {
                        var errorMessage = result["errors"][0]["message"]?.ToString();
                        throw new Exception($"API Error: {errorMessage}");
                    }

                    // Extract the MedianDuration value from the nested structure
                    var resultsArray = result["data"]["actor"]["account"]["nrql"]["results"];

                    if (resultsArray == null || !resultsArray.Any())
                    {
                        Debug.WriteLine("No results found in the response");
                        return 0f;
                    }

                    var firstResult = resultsArray.First();
                    var medianDuration = firstResult["MedianDuration"];

                    if (medianDuration == null)
                    {
                        Debug.WriteLine("MedianDuration field not found");
                        return 0f;
                    }

                    // Extract the "50" value from MedianDuration object
                    if (medianDuration["50"] != null)
                    {
                        return medianDuration["50"].ToObject<float>();
                    }

                    // Fallback - try direct conversion in case structure changes
                    return medianDuration.ToObject<float>();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in FetchDurationForCarrier: {ex.Message}");
                throw;
            }
        }
    }
}