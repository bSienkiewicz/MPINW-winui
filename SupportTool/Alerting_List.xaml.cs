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
        public ObservableCollection<AppCarrierItem> AppNames { get; } = new();
        private readonly ApplicationDataContainer _localSettings;
        private readonly NewRelicApiService _newRelicApiService;
        private readonly AlertService _alertService;
        private string _selectedStack = string.Empty;
        private CancellationTokenSource _cancellationTokenSource;

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
                _ = LoadAppNamesForStack(_selectedStack);
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
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
                AppNames.Clear();

                // Cancel previous fetching operation
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();

                // Update selected stack
                _selectedStack = e.AddedItems[0].ToString();
                _localSettings.Values["SelectedStack"] = _selectedStack;

                if (IsApiKeyPresent())
                {
                    await LoadAppNamesForStack(_selectedStack, _cancellationTokenSource.Token);
                }
            }
        }

        private async Task LoadAppNamesForStack(string stack, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(stack)) return;

            try
            {
                AppNames.Clear();
                AppNameFetchingProgress.Visibility = Visibility.Visible;
                AppNameFetchingProgress.IsActive = true;

                var appCarrierPairs = await _newRelicApiService.FetchAppNamesAndCarriers(stack, cancellationToken);

                foreach (var app in appCarrierPairs)
                {
                    if (cancellationToken.IsCancellationRequested) return;
                    foreach (var carrier in app.Carriers)
                    {
                        AppNames.Add(new AppCarrierItem { AppName = app.AppName, CarrierName = carrier.CarrierName });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading app-carrier data: {ex.Message}");
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
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();
                await LoadAppNamesForStack(_selectedStack, _cancellationTokenSource.Token);
            }
        }

        private void ApiKeyWarningInfoBar_ButtonClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage), "ApiKeyTab");
        }

        private void AppNamesList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {

            if (AppNamesList.SelectedItem is not null)
            {
                var selectedApp = (AppCarrierItem)AppNamesList.SelectedItem;
                Debug.WriteLine($"Double-clicked: {selectedApp.AppName}, {selectedApp.CarrierName}");
            }
        }
    }
    public class AppCarrierItem
    {
        public string AppName { get; set; }
        public string CarrierName { get; set; }
    }

}