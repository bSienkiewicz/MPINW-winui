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

namespace SupportTool
{
    public sealed partial class Alerting_Manager : Page
    {
        public ObservableCollection<AppNameItem> AppNames { get; } = new ObservableCollection<AppNameItem>();
        public ObservableCollection<CarrierItem> Carriers { get; } = new ObservableCollection<CarrierItem>();

        private readonly ApplicationDataContainer _localSettings;
        private AppNameItem _selectedApp;
        private CarrierItem _selectedCarrier;
        private readonly NewRelicApiService _newRelicApiService;
        private readonly AlertService _alertService;

        private string _selectedStack = "shd03"; 
        private string _repositoryPath;

        public Alerting_Manager()
        {
            this.InitializeComponent();
            _localSettings = ApplicationData.Current.LocalSettings;
            _repositoryPath = _localSettings.Values["NRAlertsDir"] as string ?? string.Empty;
            AppNamesList.ItemsSource = AppNames;
            CarriersList.ItemsSource = Carriers;
            _newRelicApiService = new NewRelicApiService();
            _alertService = new AlertService();
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

            if (IsApiKeyPresent())
            {
                if (!AppNames.Any() || !Carriers.Any())
                {
                    await LoadAppNamesAndCarriers();
                }
            }
        }

        private bool IsApiKeyPresent()
        {
            if (!_localSettings.Values.ContainsKey("NR_API_Key"))
            {
                InfoBar.IsOpen = true;
                return false;
            }
            else
            {
                InfoBar.IsOpen = false;
                return true;
            }
        }

        private void ApiKeyWarningInfoBar_ButtonClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage), "ApiKeyTab");
        }

        private async Task LoadAppNamesAndCarriers()
        {
            try
            {
                // Show progress indicators
                AppNames.Clear();
                AppNameFetchingProgress.Visibility = Visibility.Visible;
                AppNameFetchingProgress.IsActive = true;
                CarrierFetchingProgress.Visibility = Visibility.Visible;
                CarrierFetchingProgress.IsActive = true;

                // Load from DataService first
                var savedAppNames = DataService.Instance.GetAppNames();

                // If no saved data, fetch from API
                if (savedAppNames == null || !savedAppNames.Any())
                {
                    var appNames = await _newRelicApiService.FetchAppNamesAndCarriers(_selectedStack);
                    foreach (var app in appNames)
                    {
                        AppNames.Add(app);
                    }
                    DataService.Instance.SaveAppNames(AppNames);
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
            catch (Exception ex)
            {
                Debug.WriteLine($"🚨 Error occurred: {ex}");
                // Handle/log the exception appropriately
            }
            finally
            {
                AppNameFetchingProgress.Visibility = Visibility.Collapsed;
                AppNameFetchingProgress.IsActive = false;
                CarrierFetchingProgress.Visibility = Visibility.Collapsed;
                CarrierFetchingProgress.IsActive = false;
            }
        }

        private async void FetchNRButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsApiKeyPresent())
            {
                await LoadAppNamesAndCarriers();
            }
        }

        private void AppNamesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AppNamesList.SelectedItem is AppNameItem selectedApp)
            {
                _selectedApp = selectedApp;
                Carriers.Clear();

                foreach (var carrier in selectedApp.Carriers)
                {
                    Carriers.Add(carrier);
                }

                UpdateAlertButtonsState();
            }
        }

        private void CarriersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CarriersList.SelectedItem is CarrierItem selectedCarrier)
            {
                _selectedCarrier = selectedCarrier;
                UpdateAlertButtonsState();
            }
        }

        private void UpdateAlertButtonsState()
        {
            if (_selectedApp != null && _selectedCarrier != null && !string.IsNullOrEmpty(_repositoryPath))
            {
                var existingAlerts = _alertService.GetAlertsForStack(_repositoryPath, _selectedStack);
                bool printDurationExists = _alertService.AlertExistsForCarrier(existingAlerts, _selectedApp.AppName, _selectedCarrier.CarrierName);
                AddPrintDurationButton.IsEnabled = !printDurationExists;
            }
            else
            {
                AddPrintDurationButton.IsEnabled = false;
            }
        }

        private void AddPrintDurationButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedApp == null || _selectedCarrier == null || string.IsNullOrEmpty(_repositoryPath))
            {
                return;
            }

            try
            {
                var existingAlerts = _alertService.GetAlertsForStack(_repositoryPath, _selectedStack);
                var newAlert = _alertService.CreatePrintDurationAlert(_selectedApp.AppName, _selectedCarrier.CarrierName);

                existingAlerts.Add(newAlert);
                _alertService.SaveAlertsToFile(_repositoryPath, _selectedStack, existingAlerts);

                // Show success message
                InfoBar.Title = "Success";
                InfoBar.Severity = InfoBarSeverity.Success;
                InfoBar.Message = "Print Duration alert added successfully.";
                InfoBar.IsOpen = true;

                // Update button states
                UpdateAlertButtonsState();
            }
            catch (Exception ex)
            {
                InfoBar.Title = "Error";
                InfoBar.Severity = InfoBarSeverity.Error;
                InfoBar.Message = $"Failed to add alert: {ex.Message}";
                InfoBar.IsOpen = true;
            }
        }
        private void AddErrorRateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedApp == null || _selectedCarrier == null || string.IsNullOrEmpty(_repositoryPath))
            {
                return;
            }

            try
            {
                var existingAlerts = _alertService.GetAlertsForStack(_repositoryPath, _selectedStack);
                var newAlert = _alertService.CreateErrorRateAlert(_selectedApp.AppName, _selectedCarrier.CarrierName);

                existingAlerts.Add(newAlert);
                _alertService.SaveAlertsToFile(_repositoryPath, _selectedStack, existingAlerts);

                // Show success message
                InfoBar.Title = "Success";
                InfoBar.Severity = InfoBarSeverity.Success;
                InfoBar.Message = "Print Duration alert added successfully.";
                InfoBar.IsOpen = true;

                // Update button states
                UpdateAlertButtonsState();
            }
            catch (Exception ex)
            {
                InfoBar.Title = "Error";
                InfoBar.Severity = InfoBarSeverity.Error;
                InfoBar.Message = $"Failed to add alert: {ex.Message}";
                InfoBar.IsOpen = true;
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