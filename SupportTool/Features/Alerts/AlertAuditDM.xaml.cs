using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml;
using System.Threading;
using System.Linq;
using Microsoft.UI.Xaml.Input;
using SupportTool.Alerts.Dialogs;
using System.Collections.Generic;
using SupportTool.Features.Alerts.Helpers;
using SupportTool.Features.Alerts.Models;
using SupportTool.Features.Alerts.Services;
using SupportTool.Features.Alerts.CustomControls;

namespace SupportTool.Features.Alerts
{
    public sealed partial class AlertAuditDM : Page
    {
        public ObservableCollection<CarrierIdItem> Carriers { get; } = new();
        private readonly NewRelicApiService _newRelicApiService = new();
        private readonly AlertService _alertService = new();
        private readonly SettingsService _settings = new();
        private string _selectedStack = string.Empty;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isUpdatingHeaderCheckBox = false;
        private bool _includeAsos = false;

        public AlertAuditDM()
        {
            InitializeComponent();
            InitializeControls();
        }

        private void InitializeControls()
        {
            CarriersList.ItemsSource = Carriers;

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
            _selectedStack = _settings.GetSetting("SelectedStackDM");
            _includeAsos = _settings.GetSetting("IncludeAsosDM") == "true";
            AsosToggle.IsOn = _includeAsos;
            
            if (!string.IsNullOrEmpty(_selectedStack) && availableStacks.Contains(_selectedStack))
            {
                stacksComboBox.SelectedItem = _selectedStack;
                // Load data for the selected stack
                _ = LoadCarrierIdsForStack(_selectedStack);
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
                Carriers.Clear();

                // Cancel previous fetching operation
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();

                // Update selected stack
                _selectedStack = e.AddedItems[0].ToString();
                _settings.SetSetting("SelectedStackDM", _selectedStack);

                if (IsApiKeyPresent() && _selectedStack != null)
                {
                    await LoadCarrierIdsForStack(_selectedStack, _cancellationTokenSource.Token);
                }
            }
        }

        private async Task LoadCarrierIdsForStack(string stack, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(stack)) return;

            try
            {
                CarrierFetchingProgressRing.IsActive = true;
                CarrierFetchingProgress.Visibility = Visibility.Visible;

                // Fetch carrier IDs based on ASOS toggle
                var uniqueCarrierIds = await _newRelicApiService.FetchCarrierIds(includeAsos: _includeAsos, cancellationToken);
                var existingAlerts = _alertService.GetAlertsForStack(stack);

                Carriers.Clear();
                foreach (var carrierId in uniqueCarrierIds)
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    var item = new CarrierIdItem
                    {
                        CarrierId = carrierId
                    };
                    item.HasAverageDurationAlert = _alertService.HasCarrierIdAlert(existingAlerts, item.CarrierId, AlertType.PrintDuration);
                    item.HasErrorRateAlert = _alertService.HasCarrierIdAlert(existingAlerts, item.CarrierId, AlertType.ErrorRate);
                    
                    // Select carriers that are missing any alerts
                    item.IsSelected = !item.HasAverageDurationAlert || !item.HasErrorRateAlert;
                    
                    Carriers.Add(item);
                }
                
                // Select any carrier that is missing any alert type
                CarriersList.SelectedItems.Clear();
                foreach (var item in Carriers)
                {
                    if (item.IsSelected)
                    {
                        CarriersList.SelectedItems.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                var toast = new CustomToast();
                ToastContainer.Children.Add(toast);
                toast.ShowToast("Error loading carrier ID data", $"{ex.Message}", InfoBarSeverity.Error, 10);
            }
            finally
            {
                CarrierFetchingProgressRing.IsActive = false;
                CarrierFetchingProgress.Visibility = Visibility.Collapsed;
            }
        }

        private async void FetchNRButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsApiKeyPresent())
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();
                await LoadCarrierIdsForStack(_selectedStack, _cancellationTokenSource.Token);
            }
        }

        private void ApiKeyWarningInfoBar_ButtonClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SupportTool.SettingsPage), "ApiKeyTab");
        }

        private async void AsosToggle_Toggled(object sender, RoutedEventArgs e)
        {
            _includeAsos = AsosToggle.IsOn;
            _settings.SetSetting("IncludeAsosDM", _includeAsos ? "true" : "false");
            
            // Reload carrier IDs when toggle changes
            if (!string.IsNullOrEmpty(_selectedStack) && IsApiKeyPresent())
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();
                await LoadCarrierIdsForStack(_selectedStack, _cancellationTokenSource.Token);
            }
        }

        private void RefreshAlertStatus()
        {
            if (string.IsNullOrEmpty(_selectedStack)) return;

            var existingAlerts = _alertService.GetAlertsForStack(_selectedStack);

            // Force UI update by re-adding items
            for (int i = 0; i < Carriers.Count; i++)
            {
                var item = Carriers[i];

                bool newADStatus = _alertService.HasCarrierIdAlert(existingAlerts, item.CarrierId, AlertType.PrintDuration);
                bool newERStatus = _alertService.HasCarrierIdAlert(existingAlerts, item.CarrierId, AlertType.ErrorRate);

                if (item.HasAverageDurationAlert != newADStatus || item.HasErrorRateAlert != newERStatus)
                {
                    item.HasAverageDurationAlert = newADStatus;
                    item.HasErrorRateAlert = newERStatus;

                    // This forces the UI to recognize the change
                    Carriers[i] = item;
                }
            }
        }
    }
}
