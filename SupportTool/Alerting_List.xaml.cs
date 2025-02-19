using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml;
using System.Threading;
using SupportTool.Services;
using System.Linq;
using Microsoft.UI.Xaml.Input;
using SupportTool.Models;
using SupportTool.Helpers;
using System.Collections.Generic;
using SupportTool.Dialogs;

namespace SupportTool
{
    public sealed partial class Alerting_List : Page
    {
        public ObservableCollection<AppCarrierItem> AppNames { get; } = new();
        private readonly NewRelicApiService _newRelicApiService = new();
        private readonly AlertService _alertService = new();
        private readonly SettingsService _settingsService = new();
        private string _selectedStack = string.Empty;
        private string _repositoryPath;
        private CancellationTokenSource _cancellationTokenSource;

        public Alerting_List()
        {
            InitializeComponent();
            InitializeControls();
        }

        private void InitializeControls()
        {
            AppNamesList.ItemsSource = AppNames;

            // Load available stacks into combo box
            var availableStacks = _alertService.GetAlertStacksFromDirectories();
            stacksComboBox.ItemsSource = availableStacks;

            // Restore previously selected stack
            _selectedStack = _settingsService.GetSetting("SelectedStack");
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
            bool hasApiKey = _settingsService.IsApiKeySet();
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
                _settingsService.SetSetting("SelectedStack", _selectedStack);

                if (IsApiKeyPresent() && _selectedStack != null)
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
                AppNameFetchingProgress.Visibility = Visibility.Visible;
                AppNameFetchingProgress.IsActive = true;

                var appCarrierPairs = await _newRelicApiService.FetchAppNamesAndCarriers(stack, cancellationToken);

                _repositoryPath = _settingsService.GetSetting("NRAlertsDir");
                var existingAlerts = _alertService.GetAlertsForStack(stack);

                AppNames.Clear(); // Clear before adding new items
                CheckForMissingAppNameCarrierPairs(appCarrierPairs, cancellationToken, existingAlerts);
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

        private void CheckForMissingAppNameCarrierPairs(List<AppNameItem> appCarrierPairs, CancellationToken cancellationToken, List<NrqlAlert> existingAlerts)
        {

            foreach (var app in appCarrierPairs)
            {
                if (cancellationToken.IsCancellationRequested) return;
                foreach (var carrier in app.Carriers)
                {
                    var hasPrintDurationAlert = _alertService.AlertExistsForCarrier(
                        existingAlerts,
                        app.AppName,
                        carrier.CarrierName);

                    var hasErrorRateAlert = existingAlerts.Any(alert =>
                        alert.NrqlQuery.Contains($"WebTransaction/WCF/XLogics.BlackBox.ServiceContracts.IBlackBoxContract.PrintParcel") &&
                        alert.Name.ToLower().Contains(app.AppName.ToLower().Split('.')[0]));

                    AppNames.Add(new AppCarrierItem
                    {
                        AppName = app.AppName,
                        CarrierName = carrier.CarrierName,
                        HasPrintDurationAlert = hasPrintDurationAlert,
                        HasErrorRateAlert = hasErrorRateAlert
                    });
                }
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

        private void RefreshAlertStatus()
        {
            if (string.IsNullOrEmpty(_selectedStack)) return;

            var existingAlerts = _alertService.GetAlertsForStack(_selectedStack);

            foreach (var item in AppNames)
            {
                item.HasPrintDurationAlert = _alertService.AlertExistsForCarrier(
                    existingAlerts,
                    item.AppName,
                    item.CarrierName);

                item.HasErrorRateAlert = existingAlerts.Any(alert =>
                    alert.NrqlQuery.Contains($"WebTransaction/WCF/XLogics.BlackBox.ServiceContracts.IBlackBoxContract.PrintParcel") &&
                    alert.Name.ToLower().Contains(item.AppName.ToLower().Split('.')[0]));
            }
        }

        private async void AppNamesList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            var selectedApp = (AppCarrierItem)AppNamesList.SelectedItem;
            //NrqlAlert newAlert = _alertService.CreateMissingAlertByType(selectedApp, AlertType.PrintDuration);
            //List<NrqlAlert> existingAlerts = _alertService.GetAlertsForStack(_selectedStack);

            //existingAlerts.Add(newAlert);
            //_alertService.SaveAlertsToFile(_selectedStack, existingAlerts);

            var dialog = new AlertDetailsDialog(selectedApp, _selectedStack, _alertService);
            await dialog.ShowAsync();

            // Refresh the list after dialog is closed to show updated alert status
            RefreshAlertStatus();

        }
    }
}