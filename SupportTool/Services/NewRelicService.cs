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

namespace SupportTool.Services
{
    public class NewRelicApiService
    {
        private readonly ApplicationDataContainer _localSettings;

        public NewRelicApiService()
        {
            _localSettings = ApplicationData.Current.LocalSettings;
        }

        public async Task<List<AppNameItem>> FetchAppNamesAndCarriers(string stack, CancellationToken cancellationToken = default)
        {
            var appNames = new List<AppNameItem>();
            try
            {
                if (!_localSettings.Values.TryGetValue("NR_API_Key", out var value))
                {
                    throw new Exception("API key not found in local settings.");
                }

                if (string.IsNullOrEmpty(stack))
                {
                    throw new Exception("Stack name is required.");
                }

                string apiKey = value.ToString();
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
    }
}