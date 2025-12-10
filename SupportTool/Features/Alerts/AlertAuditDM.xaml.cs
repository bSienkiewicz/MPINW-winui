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
using Microsoft.UI.Xaml.Media;
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
        private bool _isAsos = false;

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
            _isAsos = _settings.GetSetting("IncludeAsosDM") == "true";
            AsosToggle.IsOn = _isAsos;
            
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
                var uniqueCarrierIds = await _newRelicApiService.FetchCarrierIds(isAsos: _isAsos, cancellationToken);
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
                    item.HasAverageDurationAlert = _alertService.HasCarrierIdAlert(existingAlerts, item.CarrierId, AlertType.PrintDuration, _isAsos);
                    item.HasErrorRateAlert = _alertService.HasCarrierIdAlert(existingAlerts, item.CarrierId, AlertType.ErrorRate, _isAsos);
                    
                    // Try to extract carrier name from existing alerts
                    var existingAlertForCarrier = existingAlerts.FirstOrDefault(a => 
                        a.Name.Contains("DM Allocation", StringComparison.OrdinalIgnoreCase) &&
                        AlertService.HasExactCarrierIdMatch(a, carrierId));
                    
                    if (existingAlertForCarrier != null)
                    {
                        item.CarrierName = AlertService.ExtractCarrierNameFromDmAlert(existingAlertForCarrier.Name, carrierId);
                    }
                    
                    // Select carriers that are missing any alerts (if auto-select is enabled for DM)
                    bool shouldAutoSelect = ShouldAutoSelectMissingAlerts("DM");
                    item.IsSelected = shouldAutoSelect && (!item.HasAverageDurationAlert || !item.HasErrorRateAlert);
                    
                    Carriers.Add(item);
                }
                
                // Initially select any carrier that is missing any alert type (if auto-select is enabled)
                if (ShouldAutoSelectMissingAlerts("DM"))
                {
                    CarriersList.SelectedItems.Clear();
                    foreach (var item in Carriers)
                    {
                        if (item.IsSelected)
                        {
                            CarriersList.SelectedItems.Add(item);
                        }
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
            _isAsos = AsosToggle.IsOn;
            _settings.SetSetting("IncludeAsosDM", _isAsos ? "true" : "false");
            
            // Reload carrier IDs when toggle changes
            if (!string.IsNullOrEmpty(_selectedStack) && IsApiKeyPresent())
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();
                await LoadCarrierIdsForStack(_selectedStack, _cancellationTokenSource.Token);
            }
        }

        private bool ShouldAutoSelectMissingAlerts(string alertType)
        {
            string setting = _settings.GetSetting("AutoSelectMissingAlerts", "Both");
            return setting switch
            {
                "Both" => true,
                "MPM" => alertType == "MPM",
                "DM" => alertType == "DM",
                "None" => false,
                _ => true // Default to true for backward compatibility
            };
        }

        private void RefreshAlertStatus()
        {
            if (string.IsNullOrEmpty(_selectedStack)) return;

            var existingAlerts = _alertService.GetAlertsForStack(_selectedStack);

            // Force UI update by re-adding items
            for (int i = 0; i < Carriers.Count; i++)
            {
                var item = Carriers[i];

                bool newADStatus = _alertService.HasCarrierIdAlert(existingAlerts, item.CarrierId, AlertType.PrintDuration, _isAsos);
                bool newERStatus = _alertService.HasCarrierIdAlert(existingAlerts, item.CarrierId, AlertType.ErrorRate, _isAsos);

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

            // Validate that all selected carriers have names
            var carriersWithoutNames = selectedCarriers
                .Where(c => string.IsNullOrWhiteSpace(c.CarrierName))
                .ToList();

            if (carriersWithoutNames.Any())
            {
                var carrierIds = string.Join(", ", carriersWithoutNames.Select(c => c.CarrierId));
                BatchAlertService.ShowToast(
                    ToastContainer, 
                    "Error", 
                    $"Please provide carrier names for the following carrier IDs: {carrierIds}", 
                    InfoBarSeverity.Error, 
                    10);
                return;
            }

            // Validate carrier names for invalid characters
            var carriersWithInvalidNames = selectedCarriers
                .Where(c => !string.IsNullOrWhiteSpace(c.CarrierName) && ContainsInvalidCarrierNameCharacters(c.CarrierName))
                .ToList();

            if (carriersWithInvalidNames.Any())
            {
                var invalidCarrierInfo = string.Join(", ", carriersWithInvalidNames.Select(c => $"{c.CarrierId} ({c.CarrierName})"));
                BatchAlertService.ShowToast(
                    ToastContainer, 
                    "Error", 
                    $"Carrier names cannot contain special characters. Invalid names: {invalidCarrierInfo}", 
                    InfoBarSeverity.Error, 
                    10);
                return;
            }

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
                    CarrierFetchingProgressText.Text = "Hold tight - calculating the average duration";
                    durationStats = await _newRelicApiService.FetchDurationStatisticsForCarrierIdsAsync(
                        carrierIdsNeedingAverageDuration, 
                        _isAsos);
                    CarrierFetchingProgressText.Text = "Gathering carrier ID list...";
                }

                // Separate ASOS and non-ASOS alerts to ensure ASOS alerts are at the bottom
                var nonAsosAlerts = new List<NrqlAlert>();
                var asosAlerts = new List<NrqlAlert>();

                foreach (var carrier in selectedCarriers)
                {
                    // Check and add Error Rate alert if missing
                    if (!carrier.HasErrorRateAlert)
                    {
                        var errorRateAlert = AlertTemplates.GetDmTemplate("ErrorRate", carrier.CarrierId, carrier.CarrierName.Trim(), _isAsos);
                        if (!_alertService.HasCarrierIdAlert(existingAlerts, carrier.CarrierId, AlertType.ErrorRate, _isAsos))
                        {
                            if (_isAsos)
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
                        var averageDurationAlert = AlertTemplates.GetDmTemplate("AverageDuration", carrier.CarrierId, carrier.CarrierName.Trim(), _isAsos);
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
                        if (thresholdSet && !_alertService.HasCarrierIdAlert(existingAlerts, carrier.CarrierId, AlertType.PrintDuration, _isAsos))
                        {
                            if (_isAsos)
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

        private static readonly char[] InvalidCarrierNameChars = { '\'', '\"', '.', ',', '<', '>', '(', ')', '[', ']', '{', '}', ':', ';', '/', '?', '\\' };

        /// <summary>
        /// Validates carrier name for invalid characters
        /// </summary>
        /// <param name="carrierName">The carrier name to validate</param>
        /// <returns>True if invalid characters are found, false otherwise</returns>
        private static bool ContainsInvalidCarrierNameCharacters(string carrierName)
        {
            if (string.IsNullOrWhiteSpace(carrierName))
                return false;

            return carrierName.IndexOfAny(InvalidCarrierNameChars) >= 0;
        }

        /// <summary>
        /// Removes invalid characters from carrier name
        /// </summary>
        /// <param name="carrierName">The carrier name to sanitize</param>
        /// <returns>Sanitized carrier name</returns>
        private static string SanitizeCarrierName(string carrierName)
        {
            if (string.IsNullOrWhiteSpace(carrierName))
                return carrierName;

            return new string(carrierName.Where(c => !InvalidCarrierNameChars.Contains(c)).ToArray());
        }

        /// <summary>
        /// Handles TextChanged event for carrier name TextBox to filter invalid characters and auto-select carrier
        /// </summary>
        private void CarrierNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is CarrierIdItem carrierItem)
            {
                string currentText = textBox.Text;
                bool hadInvalidChars = ContainsInvalidCarrierNameCharacters(currentText);
                
                if (hadInvalidChars)
                {
                    // Remove invalid characters
                    string sanitized = SanitizeCarrierName(currentText);
                    
                    // Update the TextBox text (this will trigger another TextChanged, but it will be clean)
                    int caretIndex = textBox.SelectionStart;
                    
                    // Calculate how many characters were removed before the cursor
                    int removedBeforeCursor = currentText.Take(caretIndex).Count(c => InvalidCarrierNameChars.Contains(c));
                    
                    textBox.Text = sanitized;
                    
                    // Restore cursor position (adjust if characters were removed before cursor)
                    textBox.SelectionStart = Math.Max(0, caretIndex - removedBeforeCursor);
                }

                // Auto-select carrier if name is entered and carrier doesn't have both alerts
                // Use the sanitized text if invalid chars were removed, otherwise use currentText
                string finalText = hadInvalidChars ? SanitizeCarrierName(currentText) : currentText;
                
                if (!string.IsNullOrWhiteSpace(finalText))
                {
                    // Select UNLESS it has both alerts already
                    bool hasBothAlerts = carrierItem.HasAverageDurationAlert && carrierItem.HasErrorRateAlert;
                    
                    if (!hasBothAlerts)
                    {
                        // Only select if not already selected to avoid unnecessary UI updates
                        if (!CarriersList.SelectedItems.Contains(carrierItem))
                        {
                            CarriersList.SelectedItems.Add(carrierItem);
                        }
                    }
                    else
                    {
                        // Deselect if it has both alerts (user might have just entered a name but carrier already has both)
                        if (CarriersList.SelectedItems.Contains(carrierItem))
                        {
                            CarriersList.SelectedItems.Remove(carrierItem);
                        }
                    }
                }
                else
                {
                    // If name is cleared, deselect the carrier
                    if (CarriersList.SelectedItems.Contains(carrierItem))
                    {
                        CarriersList.SelectedItems.Remove(carrierItem);
                    }
                }
            }
        }

        /// <summary>
        /// Handles KeyDown event for carrier name TextBox to enable Tab navigation between items
        /// </summary>
        private void CarrierNameTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Tab && sender is TextBox textBox)
            {
                // Find the current item index
                if (textBox.DataContext is CarrierIdItem currentItem)
                {
                    int currentIndex = Carriers.IndexOf(currentItem);
                    
                    if (currentIndex >= 0)
                    {
                        // Determine next index (cycle to beginning if at end)
                        int nextIndex = (currentIndex + 1) % Carriers.Count;
                        
                        // Find the next TextBox in the ListView
                        var container = CarriersList.ContainerFromIndex(nextIndex) as ListViewItem;
                        if (container != null)
                        {
                            var nextTextBox = FindVisualChild<TextBox>(container);
                            if (nextTextBox != null)
                            {
                                nextTextBox.Focus(FocusState.Keyboard);
                                e.Handled = true;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Helper method to find a visual child of a specific type
        /// </summary>
        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                {
                    return result;
                }
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }
    }
}
