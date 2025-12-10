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
using SupportTool.Features.Alerts.Models;
using System.Globalization;
using SupportTool.Features.Alerts.Helpers;

namespace SupportTool.Features.Alerts.Services
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

        /// <summary>
        /// Fetch all actively used carriers from a provided stack
        /// </summary>
        /// <param name="stack"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<List<string>> FetchCarriers(string stack, CancellationToken cancellationToken = default)
        {
            var carriers = new List<string>();
            try
            {
                string apiKey = NewRelicApiHelper.GetApiKey(_settingsService);

                if (string.IsNullOrEmpty(stack))
                {
                    throw new Exception("Stack name is required.");
                }

                string nrqlQuery = $"SELECT uniques(CarrierName) FROM Transaction WHERE host LIKE '%-{stack}-%' and PrintOperation LIKE '%create%' SINCE 7 days ago";
                string graphQLQuery = NewRelicApiHelper.BuildNrqlGraphQLQuery(nrqlQuery);

                var jsonResult = await NewRelicApiHelper.ExecuteGraphQLQueryAsync(graphQLQuery, apiKey, cancellationToken);
                var results = NewRelicApiHelper.ExtractFirstResult(jsonResult);

                if (results?["uniques.CarrierName"] is JArray carriersArray)
                {
                    foreach (var carrier in carriersArray)
                    {
                        carriers.Add(carrier.ToString());
                    }
                    carriers.Sort();
                }
                else
                {
                    throw new Exception("API returned empty or unexpected response.");
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("FetchCarriers was canceled.");
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in FetchCarriers: {ex.Message}");
                throw;
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
                string apiKey = NewRelicApiHelper.GetApiKey(_settingsService);
                if (string.IsNullOrEmpty(carrierName))
                {
                    throw new ArgumentException("Carrier name cannot be empty.", nameof(carrierName));
                }
                int samplingDays = AlertTemplates.GetConfigValue<int>("PrintDuration.ProposedValues.SamplingDays");

                string nrqlQuery = $"SELECT average(duration) AS 'AvgDuration', stddev(duration) AS 'StdDevDuration' FROM Transaction WHERE name LIKE '%.PrintParcel' AND CarrierName = '{carrierName.Replace("'", "\\'")}' AND PrintOperation LIKE '%Create%' SINCE {samplingDays} days ago LIMIT MAX";
                string graphQLQuery = NewRelicApiHelper.BuildNrqlGraphQLQuery(nrqlQuery);

                var jsonResult = await NewRelicApiHelper.ExecuteGraphQLQueryAsync(graphQLQuery, apiKey, cancellationToken);
                var resultsArray = NewRelicApiHelper.ExtractResultsArray(jsonResult);

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
                string apiKey = NewRelicApiHelper.GetApiKey(_settingsService);
                if (carrierNames == null || !carrierNames.Any())
                {
                    throw new ArgumentException("Carrier names list cannot be empty.", nameof(carrierNames));
                }

                int samplingDays = AlertTemplates.GetConfigValue<int>("PrintDuration.ProposedValues.SamplingDays");
                string carriersList = string.Join("', '", carrierNames.Select(c => c.Replace("'", "\\'")));

                string nrqlQuery = $"SELECT average(duration) AS 'AvgDuration', stddev(duration) AS 'StdDevDuration' FROM Transaction WHERE name LIKE '%.PrintParcel' AND CarrierName in ('{carriersList}') AND PrintOperation LIKE '%Create%' SINCE {samplingDays} days ago FACET CarrierName LIMIT MAX";
                string graphQLQuery = NewRelicApiHelper.BuildNrqlGraphQLQuery(nrqlQuery);

                var jsonResult = await NewRelicApiHelper.ExecuteGraphQLQueryAsync(graphQLQuery, apiKey, cancellationToken);
                var resultsArray = NewRelicApiHelper.ExtractResultsArray(jsonResult);

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

        /// <summary>
        /// Fetch all actively used carrier IDs from DM allocation transactions
        /// </summary>
        /// <param name="includeAsos">If true, fetches ASOS carriers; if false, excludes ASOS</param>
        /// <param name="cancellationToken"></param>
        /// <returns>List of carrier IDs as strings</returns>
        public async Task<List<string>> FetchCarrierIds(bool includeAsos = false, CancellationToken cancellationToken = default)
        {
            var carrierIds = new List<string>();
            try
            {
                string apiKey = NewRelicApiHelper.GetApiKey(_settingsService);

                string retailerFilter = includeAsos 
                    ? "retailerName = 'ASOS'" 
                    : "retailerName != 'ASOS'";
                
                string nrqlQuery = $"SELECT uniques(carrierId) FROM Transaction WHERE carrierId is not null AND name = 'WebTransaction/SpringController/OctopusApiController/_allocateConsignment' AND {retailerFilter} SINCE 7 DAYS AGO LIMIT MAX";
                string graphQLQuery = NewRelicApiHelper.BuildNrqlGraphQLQuery(nrqlQuery);

                var jsonResult = await NewRelicApiHelper.ExecuteGraphQLQueryAsync(graphQLQuery, apiKey, cancellationToken);
                var results = NewRelicApiHelper.ExtractFirstResult(jsonResult);

                if (results?["uniques.carrierId"] is JArray carrierIdsArray)
                {
                    foreach (var carrierId in carrierIdsArray)
                    {
                        carrierIds.Add(carrierId.ToString());
                    }

                    // Sort the carrier IDs numerically
                    carrierIds.Sort((a, b) => 
                    {
                        if (int.TryParse(a, out int idA) && int.TryParse(b, out int idB))
                            return idA.CompareTo(idB);
                        return string.Compare(a, b, StringComparison.Ordinal);
                    });
                }
                else
                {
                    throw new Exception("API returned empty or unexpected response.");
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("FetchCarrierIds was canceled.");
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in FetchCarrierIds: {ex.Message}");
                throw;
            }

            return carrierIds;
        }

        /// <summary>
        /// Fetches average duration and standard deviation for multiple carrier IDs' DM allocation transactions.
        /// </summary>
        /// <param name="carrierIds">List of carrier IDs to fetch statistics for.</param>
        /// <param name="includeAsos">If true, includes ASOS transactions; if false, excludes ASOS</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Dictionary mapping carrier ID to CarrierDurationStatistics.</returns>
        public async Task<Dictionary<string, CarrierDurationStatistics>> FetchDurationStatisticsForCarrierIdsAsync(List<string> carrierIds, bool includeAsos = false, CancellationToken cancellationToken = default)
        {
            var statistics = new Dictionary<string, CarrierDurationStatistics>();
            try
            {
                string apiKey = NewRelicApiHelper.GetApiKey(_settingsService);
                if (carrierIds == null || !carrierIds.Any())
                {
                    throw new ArgumentException("Carrier IDs list cannot be empty.", nameof(carrierIds));
                }

                int samplingDays = AlertTemplates.GetConfigValue<int>("PrintDuration.ProposedValues.SamplingDays");
                string carrierIdsList = string.Join(", ", carrierIds);
                string retailerFilter = includeAsos 
                    ? "retailerName = 'ASOS'" 
                    : "retailerName != 'ASOS'";

                string nrqlQuery = $"SELECT average(duration) AS 'AvgDuration', stddev(duration) AS 'StdDevDuration' FROM Transaction WHERE name = 'WebTransaction/SpringController/OctopusApiController/_allocateConsignment' AND carrierId in ({carrierIdsList}) AND {retailerFilter} SINCE {samplingDays} days ago FACET carrierId LIMIT MAX";
                string graphQLQuery = NewRelicApiHelper.BuildNrqlGraphQLQuery(nrqlQuery);

                var jsonResult = await NewRelicApiHelper.ExecuteGraphQLQueryAsync(graphQLQuery, apiKey, cancellationToken);
                var resultsArray = NewRelicApiHelper.ExtractResultsArray(jsonResult);

                if (resultsArray == null || !resultsArray.Any())
                {
                    Debug.WriteLine("No results found in the response for any carrier ID.");
                    return statistics;
                }

                foreach (var resultItem in resultsArray)
                {
                    string resultCarrierId = resultItem["carrierId"]?.ToString();
                    if (string.IsNullOrEmpty(resultCarrierId)) continue;

                    var carrierStats = new CarrierDurationStatistics
                    {
                        AverageDuration = resultItem["AvgDuration"]?.ToObject<float>() ?? 0f,
                        StandardDeviation = resultItem["StdDevDuration"]?.ToObject<float>() ?? 0f,
                        HasData = true
                    };

                    Debug.WriteLine($"Carrier ID: {resultCarrierId}, Avg: {carrierStats.AverageDuration}, StdDev: {carrierStats.StandardDeviation}");
                    statistics[resultCarrierId] = carrierStats;
                }

                return statistics;
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("FetchDurationStatisticsForCarrierIdsAsync was canceled.");
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in FetchDurationStatisticsForCarrierIdsAsync: {ex.Message}");
                return statistics;
            }
        }
    }
}