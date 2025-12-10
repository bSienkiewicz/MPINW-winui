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

                // Show spinner immediately for new operation
                CarrierFetchingProgressRing.IsActive = true;
                CarrierFetchingProgress.Visibility = Visibility.Visible;

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

                // Fetch carrier IDs based on ASOS toggle state
                var uniqueCarrierIds = await _newRelicApiService.FetchCarrierIds(includeAsos: _includeAsos, cancellationToken);
                var existingAlerts = _alertService.GetAlertsForStack(stack);

                Carriers.Clear();
                foreach (var carrierId in uniqueCarrierIds)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException();
                    }

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
                
                // Initially select any carrier that is missing any alert type
                CarriersList.SelectedItems.Clear();
                foreach (var item in Carriers)
                {
                    if (item.IsSelected)
                    {
                        CarriersList.SelectedItems.Add(item);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Operation was cancelled, don't hide spinner as new operation may be starting
                return;
            }
            catch (Exception ex)
            {
                var toast = new CustomToast();
                ToastContainer.Children.Add(toast);
                toast.ShowToast("Error loading carrier ID data", $"{ex.Message}", InfoBarSeverity.Error, 10);
            }
            finally
            {
                // Only hide spinner if operation completed normally
                if (!cancellationToken.IsCancellationRequested)
                {
                    CarrierFetchingProgressRing.IsActive = false;
                    CarrierFetchingProgress.Visibility = Visibility.Collapsed;
                }
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

        private async void BatchAddButton_Click(object sender, RoutedEventArgs e)
        {
            var (isValid, errorMessage) = BatchAlertService.ValidateBatchPrerequisites(
                IsApiKeyPresent(), 
                _selectedStack, 
                CarriersList.SelectedItems.Count);

            if (!isValid)
            {
                BatchAlertService.ShowToast(ToastContainer, "Error", errorMessage!, InfoBarSeverity.Error, 5);
                return;
            }

            var selectedCarriers = CarriersList.SelectedItems.Cast<CarrierIdItem>().ToList();

            try
            {
                BatchAddButton.IsEnabled = false;
                CarrierFetchingProgressRing.IsActive = true;
                CarrierFetchingProgress.Visibility = Visibility.Visible;

                var existingAlerts = _alertService.GetAlertsForStack(_selectedStack);
                var alertsToAdd = new List<NrqlAlert>();
                int addedCount = 0;
                var skippedCarrierIds = new List<string>();

                // Collect carrier IDs that need Average Duration alerts
                var carrierIdsNeedingAverageDuration = selectedCarriers
                    .Where(c => !c.HasAverageDurationAlert)
                    .Select(c => c.CarrierId)
                    .ToList();

                // Fetch statistics for all carrier IDs needing Average Duration alerts in one request
                Dictionary<string, CarrierDurationStatistics> durationStats = new();
                if (carrierIdsNeedingAverageDuration.Any())
                {
                    durationStats = await _newRelicApiService.FetchDurationStatisticsForCarrierIdsAsync(
                        carrierIdsNeedingAverageDuration, 
                        _includeAsos);
                }

                // Separate ASOS and non-ASOS alerts to ensure ASOS alerts are at the bottom
                var nonAsosAlerts = new List<NrqlAlert>();
                var asosAlerts = new List<NrqlAlert>();

                foreach (var carrier in selectedCarriers)
                {
                    // Check and add Error Rate alert if missing
                    if (!carrier.HasErrorRateAlert)
                    {
                        var errorRateAlert = AlertTemplates.GetDmTemplate("ErrorRate", carrier.CarrierId, null, _includeAsos);
                        if (!_alertService.HasCarrierIdAlert(existingAlerts, carrier.CarrierId, AlertType.ErrorRate))
                        {
                            if (_includeAsos)
                            {
                                asosAlerts.Add(errorRateAlert);
                            }
                            else
                            {
                                nonAsosAlerts.Add(errorRateAlert);
                            }
                            addedCount++;
                        }
                    }

                    // Check and add Average Duration alert if missing
                    if (!carrier.HasAverageDurationAlert)
                    {
                        var averageDurationAlert = AlertTemplates.GetDmTemplate("AverageDuration", carrier.CarrierId, null, _includeAsos);
                        bool thresholdSet = false;

                        // If we have statistics for this carrier ID, calculate the threshold
                        if (durationStats.TryGetValue(carrier.CarrierId, out var stats) && stats.HasData)
                        {
                            try
                            {
                                double suggestedThreshold = AlertService.CalculateSuggestedThreshold(stats);

                                // Validate the threshold is reasonable (at least MinimumAbsoluteThreshold)
                                var minThreshold = AlertTemplates.GetConfigValue<float?>("PrintDuration.ProposedValues.MinimumAbsoluteThreshold");
                                if (suggestedThreshold >= minThreshold)
                                {
                                    averageDurationAlert.CriticalThreshold = suggestedThreshold;
                                    thresholdSet = true;
                                }
                                else
                                {
                                    Debug.WriteLine($"Calculated threshold {suggestedThreshold} for carrier ID {carrier.CarrierId} is too low ({minThreshold}), skipping alert creation");
                                    skippedCarrierIds.Add(carrier.CarrierId);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Could not calculate threshold for carrier ID {carrier.CarrierId}: {ex.Message}");
                                skippedCarrierIds.Add(carrier.CarrierId);
                            }
                        }
                        else
                        {
                            // No statistics available for this carrier ID
                            Debug.WriteLine($"No statistics available for carrier ID {carrier.CarrierId}, skipping Average Duration alert creation");
                            skippedCarrierIds.Add(carrier.CarrierId);
                        }

                        // Only add the alert if we successfully set a valid threshold
                        if (thresholdSet && !_alertService.HasCarrierIdAlert(existingAlerts, carrier.CarrierId, AlertType.PrintDuration))
                        {
                            if (_includeAsos)
                            {
                                asosAlerts.Add(averageDurationAlert);
                            }
                            else
                            {
                                nonAsosAlerts.Add(averageDurationAlert);
                            }
                            addedCount++;
                        }
                    }
                }

                // Combine alerts: non-ASOS first, then ASOS (to ensure ASOS alerts are at the bottom)
                alertsToAdd.AddRange(nonAsosAlerts);
                alertsToAdd.AddRange(asosAlerts);

                if (alertsToAdd.Any())
                {
                    // Separate existing alerts into ASOS and non-ASOS
                    var existingNonAsosAlerts = existingAlerts
                        .Where(a => !a.Name.Contains("ASOS", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    var existingAsosAlerts = existingAlerts
                        .Where(a => a.Name.Contains("ASOS", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    // Combine: existing non-ASOS, new non-ASOS, existing ASOS, new ASOS
                    var allAlerts = new List<NrqlAlert>();
                    allAlerts.AddRange(existingNonAsosAlerts);
                    allAlerts.AddRange(nonAsosAlerts);
                    allAlerts.AddRange(existingAsosAlerts);
                    allAlerts.AddRange(asosAlerts);

                    _alertService.SaveAlertsToFile(_selectedStack, allAlerts);

                    string message = BatchAlertService.CreateSuccessMessage(addedCount, skippedCarrierIds, "carrier ID");
                    BatchAlertService.ShowToast(
                        ToastContainer, 
                        "Success", 
                        message, 
                        InfoBarSeverity.Success, 
                        skippedCarrierIds.Any() ? 10 : 5);

                    // Refresh the alert status display
                    RefreshAlertStatus();
                }
                else
                {
                    var (message, severity) = BatchAlertService.CreateNoAlertsMessage(skippedCarrierIds, "carrier ID");
                    BatchAlertService.ShowToast(ToastContainer, severity == InfoBarSeverity.Warning ? "Warning" : "Information", message, severity, skippedCarrierIds.Any() ? 10 : 5);
                }
            }
            catch (Exception ex)
            {
                BatchAlertService.ShowToast(ToastContainer, "Error", $"Failed to add alerts: {ex.Message}", InfoBarSeverity.Error, 10);
            }
            finally
            {
                CarrierFetchingProgressRing.IsActive = false;
                CarrierFetchingProgress.Visibility = Visibility.Collapsed;
                BatchAddButton.IsEnabled = true;
            }
        }
    }
}
