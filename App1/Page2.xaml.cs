using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using Microsoft.Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace App1
{
    public sealed partial class Page2 : Page, INotifyPropertyChanged
    {
        private string responseData = string.Empty;
        private bool isFetchingData;
        private readonly ApplicationDataContainer localSettings = ApplicationData.GetDefault().LocalSettings;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Page2()
        {
            this.InitializeComponent();
            DataContext = this; // Set DataContext for bindings
            LocationServices = new ObservableCollection<LS>
            {
                new() { Title = "Location 1", Description = "Description 1" },
                new() { Title = "Location 2", Description = "Description 2" }
            };
            LoadConnectionDetails();
        }

        public ObservableCollection<LS> LocationServices { get; }



        /*
         * Fetching functionality
         */
        public string ResponseData
        {
            get => responseData;
            set
            {
                if (responseData != value)
                {
                    responseData = value;
                    OnPropertyChanged(nameof(ResponseData));
                }
            }
        }
        public bool IsFetchingData
        {
            get => isFetchingData;
            set
            {
                if (isFetchingData != value)
                {
                    isFetchingData = value;
                    OnPropertyChanged(nameof(IsFetchingData));
                }
            }
        }
        private async void OnFetchDataClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            string providerId = ProviderID.Text;
            if (!string.IsNullOrWhiteSpace(providerId))
            {
                await MakeLSRequestAsync(providerId);
            }
        }

        public async Task MakeLSRequestAsync(string providerId)
        {
            using HttpClient client = new();

            try
            {
                IsFetchingData = true;

                string url = $"https://jsonplaceholder.typicode.com/posts/{providerId}";
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<Response>(responseBody);

                if (result != null)
                {
                    Debug.WriteLine($"Deserialized body: {result.Body}");
                    Debug.WriteLine($"Deserialized title: {result.Title}");
                    Debug.WriteLine($"Deserialized id: {result.Id}");

                    // Update the UI with the fetched data
                    LocationServices.Clear();
                    LocationServices.Add(new LS
                    {
                        Title = $"Location {providerId}",
                        Description = string.IsNullOrWhiteSpace(result.Body) ? "No description" : result.Body
                    });
                }
                else
                {
                    Debug.WriteLine("Failed to deserialize the response.");
                    DisplayDialog("Error", "Failed to deserialize the response!");
                }
            }
            catch (Exception ex)
            {
                DisplayDialog("Error", ex.Message);
                ResponseData = $"Error: {ex.Message}";
            }
            finally
            {
                IsFetchingData = false;
            }
        }

        //TODO: Make Oauth request, check how the oauth is made

        /*
         * Saving and reading the localSettings for connection details
         */
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Auto-save whenever the text changes
            SaveTextBoxValues();
        }

        private void SaveTextBoxValues()
        {
            // Save the TextBox values to local settings
            localSettings.Values["HostnameValue"] = Hostname.Text;
            localSettings.Values["UsernameValue"] = Username.Text;
            localSettings.Values["PasswordValue"] = Password.Text;
        }

        private void LoadConnectionDetails()
        {
            // Load saved values if they exist
            Hostname.Text = localSettings.Values["HostnameValue"] as string ?? string.Empty;
            Username.Text = localSettings.Values["UsernameValue"] as string ?? string.Empty;
            Password.Text = localSettings.Values["PasswordValue"] as string ?? string.Empty;
        }

        /*
         * Additional helper functions
         */
        private void DisplayDialog(string Title, string Content)
        {
            ContentDialog dialog = new()
            {
                Title = Title,
                Content = Content,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };
            _ = dialog.ShowAsync();
        }
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class LS
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class Response
    {
        [JsonPropertyName("userId")]
        public int UserId { get; set; }

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;
    }
}
