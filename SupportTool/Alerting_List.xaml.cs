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
using SupportTool.Services;
using System.Linq;
using SupportTool.Helpers;
using System.IO;

namespace SupportTool
{
    public sealed partial class Alerting_List : Page
    {
        private ObservableCollection<AppNameItem> AppNames { get; } = new ObservableCollection<AppNameItem>();
        private readonly ApplicationDataContainer _localSettings;
        private readonly NewRelicApiService _newRelicApiService;
        private readonly AlertService _alertService;
        private string _selectedStack = string.Empty;

        public Alerting_List()
        {
            InitializeComponent();
            _localSettings = ApplicationData.Current.LocalSettings;
            _newRelicApiService = new NewRelicApiService();
            _alertService = new AlertService();

            InitializeControls();
        }

        private void InitializeControls()
        {
            AppNamesList.ItemsSource = AppNames;

            // Load available stacks into combo box
            var availableStacks = _alertService.GetAlertStacksFromDirectories();
            stacksComboBox.ItemsSource = availableStacks;

            // Restore previously selected stack
            _selectedStack = _localSettings.Values["SelectedStack"] as string ?? string.Empty;
            if (!string.IsNullOrEmpty(_selectedStack) && availableStacks.Contains(_selectedStack))
            {
                stacksComboBox.SelectedItem = _selectedStack;
                // Load data for the selected stack
                _ = LoadAppNamesForStack();
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (IsApiKeyPresent() && !AppNames.Any() && !string.IsNullOrEmpty(_selectedStack))
            {
                await LoadAppNamesForStack();
            }
        }

        private bool IsApiKeyPresent()
        {
            bool hasApiKey = _localSettings.Values.ContainsKey("NR_API_Key");
            InfoBar.IsOpen = !hasApiKey;
            return hasApiKey;
        }

        private async void StackComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                // Clear existing data
                AppNames.Clear();

                // Update selected stack
                _selectedStack = e.AddedItems[0].ToString();

                // Save to local storage
                _localSettings.Values["SelectedStack"] = _selectedStack;

                // Load new data if API key is present
                if (IsApiKeyPresent())
                {
                    await LoadAppNamesForStack();
                }
            }
        }

        private async Task LoadAppNamesForStack()
        {
            if (string.IsNullOrEmpty(_selectedStack))
            {
                return;
            }

            try
            {
                AppNameFetchingProgress.Visibility = Visibility.Visible;
                AppNameFetchingProgress.IsActive = true;

                // Always fetch fresh data from API when stack changes
                var fetchedAppNames = await _newRelicApiService.FetchAppNamesAndCarriers(_selectedStack);

                AppNames.Clear();
                foreach (var app in fetchedAppNames)
                {
                    AppNames.Add(app);
                }

                // Cache the new data
                DataService.Instance.SaveAppNames(AppNames);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading app names: {ex.Message}");
                // Consider showing an error message to the user
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "Error",
                    Content = "Failed to load app names. Please try again.",
                    CloseButtonText = "OK"
                };
                await errorDialog.ShowAsync();
            }
            finally
            {
                AppNameFetchingProgress.Visibility = Visibility.Collapsed;
                AppNameFetchingProgress.IsActive = false;
            }
        }

        private async void FetchNRButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsApiKeyPresent())
            {
                await LoadAppNamesForStack();
            }
        }

        private void ApiKeyWarningInfoBar_ButtonClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage), "ApiKeyTab");
        }
    }

}