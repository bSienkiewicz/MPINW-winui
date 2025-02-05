using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace App1
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Alerting_Manager : Page
    {

        public ObservableCollection<AlertItem> AlertItems { get; } = new ObservableCollection<AlertItem>();
        private readonly ApplicationDataContainer _localSettings;

        public Alerting_Manager()
        {
            this.InitializeComponent();
            _localSettings = ApplicationData.Current.LocalSettings;
            LoadSampleData();
        }

        private void LoadSampleData()
        {
            AlertItems.Add(new AlertItem 
            { 
                AppName = "App 1", 
                Carrier = "Carrier 1", 
                PrintDuration = true, 
                ErrorRate = false 
            });
            AlertItems.Add(new AlertItem 
            { 
                AppName = "App 2", 
                Carrier = "Carrier 2", 
                PrintDuration = false, 
                ErrorRate = true 
            });
            AlertItems.Add(new AlertItem 
            { 
                AppName = "App 2", 
                Carrier = "Carrier 2", 
                PrintDuration = true, 
                ErrorRate = true 
            });
            AlertItems.Add(new AlertItem 
            { 
                AppName = "App 2", 
                Carrier = "Carrier 2", 
                PrintDuration = true, 
                ErrorRate = true 
            });
            AlertItems.Add(new AlertItem 
            { 
                AppName = "App 2", 
                Carrier = "Carrier 2", 
                PrintDuration = false, 
                ErrorRate = false 
            });
            AlertItems.Add(new AlertItem 
            { 
                AppName = "App 2", 
                Carrier = "Carrier 2", 
                PrintDuration = false, 
                ErrorRate = false 
            });
            AlertItems.Add(new AlertItem 
            { 
                AppName = "App 2", 
                Carrier = "Carrier 2", 
                PrintDuration = true, 
                ErrorRate = true 
            });
            AlertItems.Add(new AlertItem 
            { 
                AppName = "App 2", 
                Carrier = "Carrier 2", 
                PrintDuration = false, 
                ErrorRate = true 
            });
            AlertItems.Add(new AlertItem 
            { 
                AppName = "App 2", 
                Carrier = "Carrier 2", 
                PrintDuration = true, 
                ErrorRate = false 
            });
            AlertItems.Add(new AlertItem 
            { 
                AppName = "App 2", 
                Carrier = "Carrier 2", 
                PrintDuration = false, 
                ErrorRate = true 
            });
            // Add more sample data as needed
        }

        private async void FetchNRButton_Click(object sender, RoutedEventArgs e)
        {
            if (_localSettings.Values.TryGetValue("NR_API_Key", out var value))
            {
                string apiKey = value.ToString();
                Debug.WriteLine($"NR API: {apiKey}");

                // Call the method to fetch the data from New Relic
                await FetchDataFromNewRelic(apiKey);
            }
            else
            {
                Debug.WriteLine("API key not found in local settings.");
            }
        }


        private async Task FetchDataFromNewRelic(string apiKey)
        {
            try
            {
                string url = "https://api.newrelic.com/graphql";
                string query = "{ actor { account(id: 400000) { nrql(query: \"SELECT * FROM Transaction LIMIT 5\") { results } } } }";

                var requestBody = new
                {
                    query = query
                };

                string jsonBody = JsonConvert.SerializeObject(requestBody);

                using (HttpClient client = new HttpClient())
                {
                    var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
                    };

                    requestMessage.Headers.Add("X-Api-Key", apiKey);

                    HttpResponseMessage response = await client.SendAsync(requestMessage);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseContent = await response.Content.ReadAsStringAsync();
                        Debug.WriteLine($"Response from New Relic: {responseContent}");
                    }
                    else
                    {
                        Debug.WriteLine($"Failed to fetch data. Status code: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An error occurred: {ex.Message}");
            }
        }
    }

    public class AlertItem : INotifyPropertyChanged
    {
        private string _appName;
        private string _carrier;
        private bool _printDuration;
        private bool _errorRate;

        public string AppName
        {
            get => _appName;
            set
            {
                _appName = value;
                OnPropertyChanged();
            }
        }

        public string Carrier
        {
            get => _carrier;
            set
            {
                _carrier = value;
                OnPropertyChanged();
            }
        }

        public bool PrintDuration
        {
            get => _printDuration;
            set
            {
                _printDuration = value;
                OnPropertyChanged();
            }
        }

        public bool ErrorRate
        {
            get => _errorRate;
            set
            {
                _errorRate = value;
                OnPropertyChanged();
            }
        }

        public ICommand ConfigureCommand { get; }

        public AlertItem()
        {
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}