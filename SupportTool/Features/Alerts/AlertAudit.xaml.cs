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

namespace SupportTool
{
    public sealed partial class AlertAudit : Page
    {
        public ObservableCollection<CarrierItem> Carriers { get; } = new();
        private readonly NewRelicApiService _newRelicApiService = new();
        private readonly AlertService _alertService = new();
        private readonly SettingsService _settings = new();
        private string _selectedStack = string.Empty;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isUpdatingHeaderCheckBox = false;

        public AlertAudit()
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
            _selectedStack = _settings.GetSetting("SelectedStack");
            if (!string.IsNullOrEmpty(_selectedStack) && availableStacks.Contains(_selectedStack))
            {
                stacksComboBox.SelectedItem = _selectedStack;
                // Load data for the selected stack
                _ = LoadCarriersForStack(_selectedStack);
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
                _settings.SetSetting("SelectedStack", _selectedStack);

                if (IsApiKeyPresent() && _selectedStack != null)
                {
                    await LoadCarriersForStack(_selectedStack, _cancellationTokenSource.Token);
                }
            }
        }

        private async Task LoadCarriersForStack(string stack, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(stack)) return;

            try
            {
                CarrierFetchingProgressRing.IsActive = true;
                CarrierFetchingProgress.Visibility = Visibility.Visible;

                var uniqueCarriers = await _newRelicApiService.FetchCarriers(stack, cancellationToken);
                var existingAlerts = _alertService.GetAlertsForStack(stack);

                Carriers.Clear();
                foreach (var carrier in uniqueCarriers)
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    var item = new CarrierItem
                    {
                        CarrierName = carrier
                    };
                    item.HasPrintDurationAlert = _alertService.HasCarrierAlert(existingAlerts, item.CarrierName, AlertType.PrintDuration);
                    item.HasErrorRateAlert = _alertService.HasCarrierAlert(existingAlerts, item.CarrierName, AlertType.ErrorRate);
                    
                    // Select carriers that are missing any alerts
                    item.IsSelected = !item.HasPrintDurationAlert || !item.HasErrorRateAlert;
                    
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
                toast.ShowToast("Error loading carrier data", $"{ex.Message}", InfoBarSeverity.Error, 10);
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
                await LoadCarriersForStack(_selectedStack, _cancellationTokenSource.Token);
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

            // Force UI update by re-adding items
            for (int i = 0; i < Carriers.Count; i++)
            {
                var item = Carriers[i];

                bool newPDStatus = _alertService.HasCarrierAlert(existingAlerts, item.CarrierName, AlertType.PrintDuration);
                bool newERStatus = _alertService.HasCarrierAlert(existingAlerts, item.CarrierName, AlertType.ErrorRate);

                if (item.HasPrintDurationAlert != newPDStatus || item.HasErrorRateAlert != newERStatus)
                {
                    item.HasPrintDurationAlert = newPDStatus;
                    item.HasErrorRateAlert = newERStatus;

                    // This forces the UI to recognize the change
                    Carriers[i] = item;
                }
            }
        }

        private async void BatchAddButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsApiKeyPresent() || string.IsNullOrEmpty(_selectedStack))
            {
                var toast = new CustomToast();
                ToastContainer.Children.Add(toast);
                toast.ShowToast("Error", "Please ensure API key is set and a stack is selected", InfoBarSeverity.Error, 5);
                return;
            }

            var selectedCarriers = CarriersList.SelectedItems.Cast<CarrierItem>().ToList();
            if (!selectedCarriers.Any())
            {
                var toast = new CustomToast();
                ToastContainer.Children.Add(toast);
                toast.ShowToast("Information", "Please select at least one carrier", InfoBarSeverity.Informational, 5);
                return;
            }

            var optionsDialog = new BatchAddOptionsDialog(_selectedStack)
            {
                XamlRoot = RootGrid.XamlRoot
            };

            var optionsResult = await optionsDialog.ShowAsync();
            if (optionsResult != ContentDialogResult.Primary)
            {
                return;
            }

            string namePrefix = optionsDialog.NamePrefix;
            string facetBy = optionsDialog.FacetBy;

            try
            {
                BatchAddButton.IsEnabled = false;
                CarrierFetchingProgressRing.IsActive = true;
                CarrierFetchingProgress.Visibility = Visibility.Visible;

                var existingAlerts = _alertService.GetAlertsForStack(_selectedStack);
                var alertsToAdd = new List<NrqlAlert>();
                int addedCount = 0;
                var skippedCarriers = new List<string>();

                // Collect carriers that need PrintDuration alerts
                var carriersNeedingPrintDuration = selectedCarriers
                    .Where(c => !c.HasPrintDurationAlert)
                    .Select(c => c.CarrierName)
                    .ToList();

                // Fetch statistics for all carriers needing PrintDuration alerts in one request
                Dictionary<string, CarrierDurationStatistics> durationStats = new();
                if (carriersNeedingPrintDuration.Any())
                {
                    durationStats = await _newRelicApiService.FetchDurationStatisticsForCarriersAsync(carriersNeedingPrintDuration);
                }

                foreach (var carrier in selectedCarriers)
                {
                    // Check and add Error Rate alert if missing
                    if (!carrier.HasErrorRateAlert)
                    {
                        var errorRateAlert = AlertTemplates.GetTemplate("ErrorRate", carrier.CarrierName, _selectedStack, namePrefix, facetBy);
                        if (!_alertService.HasCarrierAlert(existingAlerts, carrier.CarrierName, AlertType.ErrorRate))
                        {
                            alertsToAdd.Add(errorRateAlert);
                            addedCount++;
                        }
                    }

                    // Check and add Print Duration alert if missing
                    if (!carrier.HasPrintDurationAlert)
                    {
                        var printDurationAlert = AlertTemplates.GetTemplate("PrintDuration", carrier.CarrierName, _selectedStack, namePrefix, facetBy);
                        bool thresholdSet = false;

                        // If we have statistics for this carrier, calculate the threshold
                        if (durationStats.TryGetValue(carrier.CarrierName, out var stats) && stats.HasData)
                        {
                            try
                            {
                                double suggestedThreshold = AlertService.CalculateSuggestedThreshold(stats);

                                // Validate the threshold is reasonable (at least MinimumAbsoluteThreshold)
                                var minThreshold = AlertTemplates.GetConfigValue<float?>("PrintDuration.ProposedValues.MinimumAbsoluteThreshold");
                                if (suggestedThreshold >= minThreshold)
                                {
                                    printDurationAlert.CriticalThreshold = suggestedThreshold;
                                    thresholdSet = true;
                                }
                                else
                                {
                                    Debug.WriteLine($"Calculated threshold {suggestedThreshold} for {carrier.CarrierName} is too low ({minThreshold}), skipping alert creation");
                                    skippedCarriers.Add(carrier.CarrierName);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Could not calculate threshold for {carrier.CarrierName}: {ex.Message}");
                                // Don't add alert if calculation fails
                                skippedCarriers.Add(carrier.CarrierName);
                            }
                        }
                        else
                        {
                            // No statistics available for this carrier
                            Debug.WriteLine($"No statistics available for {carrier.CarrierName}, skipping PrintDuration alert creation");
                            skippedCarriers.Add(carrier.CarrierName);
                        }

                        // Only add the alert if we successfully set a valid threshold
                        if (thresholdSet && !_alertService.HasCarrierAlert(existingAlerts, carrier.CarrierName, AlertType.PrintDuration))
                        {
                            alertsToAdd.Add(printDurationAlert);
                            addedCount++;
                        }
                    }
                }

                if (alertsToAdd.Any())
                {
                    // Add all new alerts to existing ones
                    existingAlerts.AddRange(alertsToAdd);
                    _alertService.SaveAlertsToFile(_selectedStack, existingAlerts);

                    var toast = new CustomToast();
                    ToastContainer.Children.Add(toast);
                    string message = $"Added {addedCount} missing alerts";
                    if (skippedCarriers.Any())
                    {
                        message += $". Skipped {skippedCarriers.Count} carrier(s) due to missing or invalid statistics: {string.Join(", ", skippedCarriers)}. Try running this batch again.";
                    }
                    toast.ShowToast("Success", message, InfoBarSeverity.Success, skippedCarriers.Any() ? 10 : 5);

                    // Refresh the alert status display
                    RefreshAlertStatus();
                }
                else
                {
                    var toast = new CustomToast();
                    ToastContainer.Children.Add(toast);
                    string message = "No missing alerts to add";
                    if (skippedCarriers.Any())
                    {
                        message += $". {skippedCarriers.Count} carrier(s) skipped due to missing or invalid statistics: {string.Join(", ", skippedCarriers)}";
                        toast.ShowToast("Warning", message, InfoBarSeverity.Warning, 10);
                    }
                    else
                    {
                        toast.ShowToast("Information", message, InfoBarSeverity.Informational, 5);
                    }
                }
            }
            catch (Exception ex)
            {
                var toast = new CustomToast();
                ToastContainer.Children.Add(toast);
                toast.ShowToast("Error", $"Failed to add alerts: {ex.Message}", InfoBarSeverity.Error, 10);
            }
            finally
            {
                CarrierFetchingProgressRing.IsActive = false;
                CarrierFetchingProgress.Visibility = Visibility.Collapsed;
                BatchAddButton.IsEnabled = true;
            }
        }

        private async void CarrierItem_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is CarrierItem selectedCarrier)
            {
                var dialog = new AlertDetailsDialog(selectedCarrier, _selectedStack, _alertService);
                dialog.AlertAdded += OnAlertAdded;
                await dialog.ShowAsync();

                RefreshAlertStatus();
            }
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