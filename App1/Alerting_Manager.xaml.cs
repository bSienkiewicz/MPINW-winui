using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Newtonsoft.Json;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Threading;
using App1.Services;
using System.Linq;

namespace App1
{
    public sealed partial class Alerting_Manager : Page
    {
        public ObservableCollection<AppNameItem> AppNames { get; } = new ObservableCollection<AppNameItem>();
        public ObservableCollection<CarrierItem> Carriers { get; } = new ObservableCollection<CarrierItem>();

        private readonly ApplicationDataContainer _localSettings;

        public Alerting_Manager()
        {
            this.InitializeComponent();
            _localSettings = ApplicationData.Current.LocalSettings;
            AppNamesList.ItemsSource = AppNames;
            CarriersList.ItemsSource = Carriers;
            LoadSavedData();
        }


        private void LoadSavedData()
        {
            var savedAppNames = DataService.Instance.GetAppNames();
            if (savedAppNames != null && savedAppNames.Any())
            {
                AppNames.Clear();
                foreach (var app in savedAppNames)
                {
                    AppNames.Add(app);
                }
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // First check if API key exists
            if (_localSettings.Values.TryGetValue("NR_API_Key", out var value))
            {
                string apiKey = value.ToString();

                // Check if we need to fetch data
                if (!AppNames.Any() || !Carriers.Any())
                {
                    // Load from DataService first
                    var savedAppNames = DataService.Instance.GetAppNames();

                    // If no saved data, fetch from API
                    if (savedAppNames == null || !savedAppNames.Any())
                    {
                        await FetchAppNamesAndCarriersFromNewRelic(apiKey);
                    }
                    else
                    {
                        // Use saved data
                        AppNames.Clear();
                        foreach (var app in savedAppNames)
                        {
                            AppNames.Add(app);
                        }
                    }
                }
            }
            else
            {
                Debug.WriteLine("API key not found in local settings.");
                InfoBar.IsOpen = true;
            }

            IsApiKeyPresent();
        }

        private bool IsApiKeyPresent()
        {
            if (!_localSettings.Values.ContainsKey("NR_API_Key"))
            {
                InfoBar.IsOpen = true; // Show the InfoBar if the API key is missing
                return false;
            }
            else
            {
                InfoBar.IsOpen = false; // Hide the InfoBar if the API key is present
                return true;
            }
        }

        private void ApiKeyWarningInfoBar_ButtonClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage), "ApiKeyTab");
        }

        private async void FetchNRButton_Click(object sender, RoutedEventArgs e)
        {
            if (_localSettings.Values.TryGetValue("NR_API_Key", out var value))
            {
                string apiKey = value.ToString();
                await FetchAppNamesAndCarriersFromNewRelic(apiKey);
            }
            else
            {
                Debug.WriteLine("API key not found in local settings.");
            }
        }

        private void AppNamesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AppNamesList.SelectedItem is AppNameItem selectedApp)
            {
                Carriers.Clear();

                foreach (var carrier in selectedApp.Carriers)
                {
                    Carriers.Add(carrier);
                }
            }
        }

        private async Task FetchAppNamesAndCarriersFromNewRelic(string apiKey)
        {
            try
            {
                string url = "https://api.newrelic.com/graphql";
                string stack = "shd04";
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
                InfoBar.IsOpen = false;

                // Show progress indicators
                AppNames.Clear();
                AppNameFetchingProgress.Visibility = Visibility.Visible;
                AppNameFetchingProgress.IsActive = true;
                CarrierFetchingProgress.Visibility = Visibility.Visible;
                CarrierFetchingProgress.IsActive = true;

                using (HttpClient client = new HttpClient())
                {
                    var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
                    };
                    requestMessage.Headers.Add("X-Api-Key", apiKey);

                    HttpResponseMessage response = await client.SendAsync(requestMessage);
                    string responseContent = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var result = JsonConvert.DeserializeObject<NewRelicResponse>(responseContent);

                        if (result?.Data?.Actor?.Account?.Nrql?.Results == null)
                        {
                            InfoBar.Title = "API Error";
                            InfoBar.Severity = InfoBarSeverity.Error;
                            InfoBar.Message = "API returned empty or unexpected response.";
                            InfoBar.IsOpen = true;
                            return;
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
                            AppNames.Add(appNameItem);
                        }
                        DataService.Instance.SaveAppNames(AppNames);
                    }
                    else
                    {
                        InfoBar.Title = "HTTP Error";
                        InfoBar.Severity = InfoBarSeverity.Error;
                        InfoBar.Message = $"HTTP Error: {response.StatusCode}, Response: {responseContent}.";
                        InfoBar.IsOpen = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"🚨 Error occurred: {ex}");
            }
            finally
            {
                AppNameFetchingProgress.Visibility = Visibility.Collapsed;
                AppNameFetchingProgress.IsActive = false;
                CarrierFetchingProgress.Visibility = Visibility.Collapsed;
                CarrierFetchingProgress.IsActive = false;
            }
        }
    }

    public class AppNameItem
    {
        public string AppName { get; set; }
        public List<CarrierItem> Carriers { get; set; } = new List<CarrierItem>();
    }

    public class CarrierItem
    {
        public string CarrierName { get; set; }
    }

    public class NewRelicResponse
    {
        public Data Data { get; set; }
    }

    public class Data
    {
        public Actor Actor { get; set; }
    }

    public class Actor
    {
        public Account Account { get; set; }
    }

    public class Account
    {
        public Nrql Nrql { get; set; }
    }

    public class Nrql
    {
        public List<Dictionary<string, object>> Results { get; set; }
    }

}