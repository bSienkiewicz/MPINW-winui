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

namespace App1
{
    public sealed partial class Alerting_Manager : Page
    {
        public ObservableCollection<AppNameItem> AppNames { get; } = new ObservableCollection<AppNameItem>();
        public ObservableCollection<CarrierItem> Carriers { get; } = new ObservableCollection<CarrierItem>();
        private readonly Dictionary<string, List<string>> _appNameCache = new Dictionary<string, List<string>>();

        private readonly ApplicationDataContainer _localSettings;
        private CancellationTokenSource _cancellationTokenSource;

        public Alerting_Manager()
        {
            this.InitializeComponent();
            _localSettings = ApplicationData.Current.LocalSettings;
            AppNamesList.ItemsSource = AppNames;
            CarriersList.ItemsSource = Carriers;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            CheckApiKey();
        }

        private void CheckApiKey()
        {
            if (!_localSettings.Values.ContainsKey("NR_API_Key"))
            {
                ApiKeyWarningInfoBar.IsOpen = true; // Show the InfoBar if the API key is missing
            }
            else
            {
                ApiKeyWarningInfoBar.IsOpen = false; // Hide the InfoBar if the API key is present
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
                await FetchAppNamesFromNewRelic(apiKey);
            }
            else
            {
                Debug.WriteLine("API key not found in local settings.");
            }
        }

        private async Task FetchAppNamesFromNewRelic(string apiKey)
        {
            try
            {
                string url = "https://api.newrelic.com/graphql";
                string stack = "shd04";
                string query = $@"
                    {{ 
                        actor {{ 
                            account(id: 400000) {{ 
                                nrql(timeout: 120 query: ""SELECT uniques(appName) FROM Transaction WHERE host LIKE '%{stack}%' and PrintOperation like '%create%' SINCE 30 days ago"") {{ 
                                    results 
                                }} 
                            }} 
                        }} 
                    }}";

                var requestBody = new { query = query };
                string jsonBody = JsonConvert.SerializeObject(requestBody);

                // Pokazanie ProgressRing
                AppNames.Clear();
                AppNameFetchingProgress.Visibility = Visibility.Visible;
                AppNameFetchingProgress.IsActive = true;

                using (HttpClient client = new HttpClient())
                {
                    var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
                    };
                    requestMessage.Headers.Add("X-Api-Key", apiKey);

                    HttpResponseMessage response = await client.SendAsync(requestMessage);
                    string responseContent = await response.Content.ReadAsStringAsync();

                    Debug.WriteLine($"🔍 Odpowiedź API: {responseContent}");

                    if (response.IsSuccessStatusCode)
                    {
                        var result = JsonConvert.DeserializeObject<NewRelicResponse>(responseContent);

                        // **Obsługa NULLI - sprawdzamy czy API zwróciło odpowiednie dane**
                        if (result?.Data?.Actor?.Account?.Nrql?.Results == null)
                        {
                            Debug.WriteLine("⚠ API zwróciło pustą lub nieoczekiwaną odpowiedź. Sprawdź poprawność zapytania.");
                            return;
                        }

                        // Czyszczenie i dodanie aplikacji do listy
                        AppNames.Clear();
                        foreach (var appNamesResult in result.Data.Actor.Account.Nrql.Results)
                        {
                            if (appNamesResult.TryGetValue("uniques.appName", out var appNameObj) && appNameObj is JArray appNamesArray)
                            {
                                foreach (var appName in appNamesArray)
                                {
                                    Debug.WriteLine($"✅ APP: {appName}");
                                    AppNames.Add(new AppNameItem { AppName = appName.ToString() });
                                }
                            }
                            else
                            {
                                Debug.WriteLine("⚠ Klucz 'uniques.appName' nie znaleziony w JSON.");
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"❌ Błąd HTTP: {response.StatusCode}, Treść: {responseContent}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"🚨 Wystąpił błąd: {ex}");
            }
            finally
            {
                AppNameFetchingProgress.Visibility = Visibility.Collapsed;
                AppNameFetchingProgress.IsActive = false;
            }
        }



        private async void AppNamesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedAppName = (AppNameItem)AppNamesList.SelectedItem;
            if (selectedAppName != null)
            {
                // Cancel the previous operation if there is one
                _cancellationTokenSource?.Cancel();

                // Create a new CancellationTokenSource for the new operation
                _cancellationTokenSource = new CancellationTokenSource();
                var token = _cancellationTokenSource.Token;

                await FetchCarriersForAppName(selectedAppName.AppName, token);
            }
        }

        private async Task FetchCarriersForAppName(string appName, CancellationToken token)
        {
            // Check cache first
            if (_appNameCache.ContainsKey(appName))
            {
                // Use the cached result
                Debug.WriteLine("Using cached carriers for: " + appName);
                var cachedCarriers = _appNameCache[appName];
                Carriers.Clear();
                foreach (var carrier in cachedCarriers)
                {
                    Carriers.Add(new CarrierItem { CarrierName = carrier });
                }
                CarrierFetchingProgress.Visibility = Visibility.Collapsed;
                return;
            }

            // Proceed with API call if not cached
            if (_localSettings.Values.TryGetValue("NR_API_Key", out var value))
            {
                string apiKey = value.ToString();
                int days = 30;
                if (int.TryParse(DaysTextBox.Text, out int parsedDays))
                {
                    days = parsedDays;
                }

                try
                {
                    string url = "https://api.newrelic.com/graphql";
                    string query = $@"
                {{
                    actor {{
                        account(id: 400000) {{
                            nrql(query: ""SELECT uniques(CarrierName) FROM Transaction WHERE appName = '{appName}' and PrintOperation like '%create%' SINCE {days} days ago"") {{
                                results
                            }}
                        }}
                    }}
                }}";

                    var requestBody = new { query = query };
                    string jsonBody = JsonConvert.SerializeObject(requestBody);
                    CarrierFetchingProgress.Visibility = Visibility.Visible;
                    Carriers.Clear();

                    using (HttpClient client = new HttpClient())
                    {
                        var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
                        {
                            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
                        };
                        requestMessage.Headers.Add("X-Api-Key", apiKey);

                        HttpResponseMessage response = await client.SendAsync(requestMessage, token);
                        string responseContent = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                        {
                            var result = JsonConvert.DeserializeObject<NewRelicResponse>(responseContent);

                            if (result?.Data?.Actor?.Account?.Nrql?.Results == null)
                            {
                                Debug.WriteLine("⚠ API zwróciło pustą lub nieoczekiwaną odpowiedź.");
                                return;
                            }

                            List<string> carriersList = new List<string>();

                            foreach (var carrierResult in result.Data.Actor.Account.Nrql.Results)
                            {
                                string key = "uniques.CarrierName";
                                if (carrierResult.TryGetValue(key, out var carrierObj) && carrierObj is JArray carriersArray)
                                {
                                    foreach (var carrier in carriersArray)
                                    {
                                        carriersList.Add(carrier.ToString());
                                        Carriers.Add(new CarrierItem { CarrierName = carrier.ToString() });
                                    }
                                }
                            }

                            // Cache the result
                            _appNameCache[appName] = carriersList;
                        }
                        else
                        {
                            Debug.WriteLine($"❌ Błąd HTTP: {response.StatusCode}");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine("Async operation was canceled.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"🚨 Wystąpił błąd: {ex}");
                }
                finally
                {
                    CarrierFetchingProgress.Visibility = Visibility.Collapsed;
                }
            }
        }
    }

    public class AppNameItem
    {
        public string AppName { get; set; }
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