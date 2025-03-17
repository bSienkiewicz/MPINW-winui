using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml;
using System.Threading;
using SupportTool.Services;
using System.Linq;
using Microsoft.UI.Xaml.Input;
using SupportTool.Models;
using SupportTool.Dialogs;
using SupportTool.CustomControls;

namespace SupportTool
{
    public sealed partial class Alerting_List : Page
    {
        public ObservableCollection<AppCarrierItem> AppNames { get; } = new();
        private readonly NewRelicApiService _newRelicApiService = new();
        private readonly AlertService _alertService = new();
        private readonly SettingsService _settings = new();
        private string _selectedStack = string.Empty;
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

            if (availableStacks == null || availableStacks.Length == 0)
            {
                // Show toast
                var toast = new CustomToast();
                ToastContainer.Children.Add(toast);
                toast.ShowToast("Error loading alerts", "Alert's repository location is not set properly", InfoBarSeverity.Error, 10);
                return;
            }

            stacksComboBox.ItemsSource = availableStacks;

            // Restore previously selected stack
            _selectedStack = _settings.GetSetting("SelectedStack");
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
            bool hasApiKey = _settings.IsApiKeySet();
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
                _settings.SetSetting("SelectedStack", _selectedStack);

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
                AppNameFetchingProgress.IsActive = true;
                AppNameFetchingProgress.Visibility = Visibility.Visible;

                var appCarrierPairs = await _newRelicApiService.FetchAppNamesAndCarriers(stack, cancellationToken);
                var existingAlerts = _alertService.GetAlertsForStack(stack);

                AppNames.Clear();
                foreach (var app in appCarrierPairs)
                {
                    if (cancellationToken.IsCancellationRequested) return;
                    foreach (var carrier in app.Carriers)
                    {
                        var item = new AppCarrierItem
                        {
                            AppName = app.AppName,
                            CarrierName = carrier.CarrierName
                        };
                        item.HasPrintDurationAlert = _alertService.HasAlert(existingAlerts, item, AlertType.PrintDuration);
                        item.HasErrorRateAlert = _alertService.HasAlert(existingAlerts, item, AlertType.ErrorRate);
                        AppNames.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                var toast = new CustomToast();
                ToastContainer.Children.Add(toast);
                toast.ShowToast("Error loading app-carrier data", $"{ex.Message}", InfoBarSeverity.Error, 10);
            }
            finally
            {
                AppNameFetchingProgress.IsActive = false;
                AppNameFetchingProgress.Visibility = Visibility.Collapsed;
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
            Debug.WriteLine("Refreshed UI");

            var existingAlerts = _alertService.GetAlertsForStack(_selectedStack);

            // Force UI update by re-adding items
            for (int i = 0; i < AppNames.Count; i++)
            {
                var item = AppNames[i];

                bool newPDStatus = _alertService.HasAlert(existingAlerts, item, AlertType.PrintDuration);
                bool newERStatus = _alertService.HasAlert(existingAlerts, item, AlertType.ErrorRate);

                if (item.HasPrintDurationAlert != newPDStatus || item.HasErrorRateAlert != newERStatus)
                {
                    item.HasPrintDurationAlert = newPDStatus;
                    item.HasErrorRateAlert = newERStatus;

                    // This forces the UI to recognize the change
                    AppNames[i] = item;
                }
            }
        }

        private async void AppNamesList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            var selectedApp = (AppCarrierItem)AppNamesList.SelectedItem;
            var dialog = new AlertDetailsDialog(selectedApp, _selectedStack, _alertService);
            dialog.AlertAdded += OnAlertAdded;
            await dialog.ShowAsync();

            RefreshAlertStatus();
        }

        private void OnAlertAdded()
        {
            RefreshAlertStatus();

            var toast = new CustomToast();
            ToastContainer.Children.Add(toast);
            toast.ShowToast("Success", "An alert has been added", InfoBarSeverity.Success, 5);
        }
    }
}