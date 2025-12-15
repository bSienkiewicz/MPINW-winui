using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using SupportTool.Features.Alerts.Helpers;
using SupportTool.Features.Alerts.Models;
using SupportTool.Features.Alerts.Services;
using System.Collections.Generic;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SupportTool
{
    public sealed partial class ThresholdManager : Page
    {
        private static readonly string StacksPath = Features.Alerts.Helpers.ConfigLoader.Get<string>("Alert_Directory_Path", "metaform\\mpm\\copies\\production\\prd\\eu-west-1");
        public ObservableCollection<NrqlAlert> AlertItems { get; } = new();
        private readonly AlertService _alertService = new();
        private readonly SettingsService _settings = new();
        private string? _selectedFolderPath;
        private string? _selectedStack;
        private string[] _availableStacks = [];

        public ThresholdManager()
        {
            InitializeComponent();
            _availableStacks = _alertService.GetAlertStacksFromDirectories();
            LoadDirectory();
            LoadStack();
        }

        private void LoadStack()
        {
            _selectedStack = _settings.GetSetting("SelectedStack");
            if (!string.IsNullOrEmpty(_selectedStack))
            {
                if (_availableStacks.Contains(_selectedStack))
                {
                    stacksComboBox.SelectedItem = _selectedStack;
                    LoadAlertsForStack();
                }
                else
                {
                    Debug.WriteLine($"Saved stack '{_selectedStack}' not found.");
                }
            }
        }
        private void LoadDirectory()
        {
            _selectedFolderPath = _settings.GetSetting("NRAlertsDir");

            if (!string.IsNullOrEmpty(_selectedFolderPath))
            {
                // Refresh available stacks when directory is loaded
                _availableStacks = _alertService.GetAlertStacksFromDirectories();
                stacksComboBox.ItemsSource = _availableStacks;

                // Check if we have a previously selected stack
                _selectedStack = _settings.GetSetting("SelectedStack");
                if (!string.IsNullOrEmpty(_selectedStack) && _availableStacks.Contains(_selectedStack))
                {
                    stacksComboBox.SelectedItem = _selectedStack;
                }
                else if (_availableStacks.Length > 0)
                {
                    // Select first stack if no previous selection
                    stacksComboBox.SelectedItem = _availableStacks[0];
                }
            }
            else
            {
                // Clear the combobox if no directory is selected
                _availableStacks = Array.Empty<string>();
                stacksComboBox.ItemsSource = _availableStacks;
            }
        }

        private void LoadAlertsForStack()
        {
            var tfvarsPath = Path.Combine(_selectedFolderPath, StacksPath, _selectedStack, "auto.tfvars");
            var tfvarsContent = File.ReadAllText(tfvarsPath);
            var parser = new HclParser();
            var parsedAlerts = parser.ParseAlerts(tfvarsContent);

            AlertItems.Clear();
            foreach (var alert in parsedAlerts)
            {
                if (AlertService.IsAlertPrintDuration(alert))
                {
                    alert.ProposedThreshold = null;
                    AlertItems.Add(alert);
                }
            }
        }

        private void StacksComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_selectedFolderPath == null || e.AddedItems.Count == 0) return;

            _selectedStack = e.AddedItems[0]?.ToString();
            _settings.SetSetting("SelectedStack", _selectedStack);
            if (string.IsNullOrEmpty(_selectedStack)) return;

            // Load PrintDuration alerts for the selected stack
            LoadAlertsForStack();
        }

        private async void CalculateTimesButton_Click(object sender, RoutedEventArgs e)
        {
            if (AlertItems.Count == 0) return;

            CalculateTimesButton.IsEnabled = false;
            AlertFetchingProgressRing.IsActive = true;
            AlertFetchingProgress.Visibility = Visibility.Visible;
            AlertFetchingOverlay.Visibility = Visibility.Visible;
            try
            {
                var apiService = new NewRelicApiService();
                
                // Separate alerts by type: MPM (carrierName) and DM (carrierId)
                var mpmAlerts = new List<NrqlAlert>();
                var dmAlerts = new List<NrqlAlert>();
                
                foreach (var alert in AlertItems)
                {
                    bool isDmAlert = alert.Name?.Contains("DM Allocation", StringComparison.OrdinalIgnoreCase) == true;
                    if (isDmAlert)
                    {
                        dmAlerts.Add(alert);
                    }
                    else
                    {
                        mpmAlerts.Add(alert);
                    }
                }

                // Fetch statistics for MPM alerts (by carrier name)
                Dictionary<string, CarrierDurationStatistics> mpmStatsDict = new();
                var carriers = mpmAlerts
                    .Select(alert => AlertService.ExtractCarrierFromTitle(alert.Name))
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Distinct()
                    .ToList();

                if (carriers.Any())
                {
                    mpmStatsDict = await apiService.FetchDurationStatisticsForCarriersAsync(carriers);
                }

                // Fetch statistics for DM alerts (by carrier ID)
                // Group by ASOS/non-ASOS since they need separate queries
                var dmAlertsByAsos = dmAlerts.GroupBy(alert => 
                    alert.Name?.Contains("ASOS", StringComparison.OrdinalIgnoreCase) == true).ToList();
                
                Dictionary<string, CarrierDurationStatistics> dmStatsDict = new();
                foreach (var group in dmAlertsByAsos)
                {
                    bool isAsos = group.Key;
                    var carrierIds = group
                        .Select(alert => AlertService.ExtractCarrierIdFromAlert(alert.Name))
                        .Where(id => !string.IsNullOrEmpty(id))
                        .Distinct()
                        .ToList();

                    if (carrierIds.Any())
                    {
                        var groupStats = await apiService.FetchDurationStatisticsForCarrierIdsAsync(carrierIds, isAsos);
                        foreach (var kvp in groupStats)
                        {
                            dmStatsDict[kvp.Key] = kvp.Value;
                        }
                    }
                }

                // Process all alerts and assign statistics
                foreach (var alert in AlertItems)
                {
                    bool isDmAlert = alert.Name?.Contains("DM Allocation", StringComparison.OrdinalIgnoreCase) == true;
                    CarrierDurationStatistics? stats = null;

                    if (isDmAlert)
                    {
                        // For DM alerts, use carrierId
                        var carrierId = AlertService.ExtractCarrierIdFromAlert(alert.Name);
                        if (!string.IsNullOrEmpty(carrierId) && dmStatsDict.TryGetValue(carrierId, out var dmStats) && dmStats.HasData)
                        {
                            stats = dmStats;
                        }
                    }
                    else
                    {
                        // For MPM alerts, use carrierName
                        var carrier = AlertService.ExtractCarrierFromTitle(alert.Name);
                        if (!string.IsNullOrEmpty(carrier) && mpmStatsDict.TryGetValue(carrier, out var mpmStats) && mpmStats.HasData)
                        {
                            stats = mpmStats;
                        }
                    }

                    if (stats != null)
                    {
                        alert.ProposedThreshold = AlertService.CalculateSuggestedThreshold(stats);
                    }
                    else
                    {
                        alert.ProposedThreshold = null;
                    }

                    // Auto-select if difference is >= threshold
                    if (alert.ProposedThreshold.HasValue)
                    {
                        double diff = Math.Abs(alert.CriticalThreshold - alert.ProposedThreshold.Value);
                        double threshold = AlertTemplates.GetThresholdDifference();
                        if (diff >= threshold)
                        {
                            AlertsListView.SelectedItems.Add(alert);
                        }
                    }
                }

                int totalFetched = AlertItems.Count(a => a.ProposedThreshold.HasValue);
                ShowToast("Success", $"Proposed times fetched from New Relic for {totalFetched} alert(s).", InfoBarSeverity.Success, 4);
            }
            catch (Exception ex)
            {
                ShowToast("Error", $"Failed to fetch times: {ex.Message}", InfoBarSeverity.Error, 6);
            }
            finally
            {
                CalculateTimesButton.IsEnabled = true;
                AlertFetchingProgressRing.IsActive = false;
                AlertFetchingProgress.Visibility = Visibility.Collapsed;
                AlertFetchingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void CopyNrqlButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is NrqlAlert alert && !string.IsNullOrEmpty(_selectedStack))
            {
                var nrql = alert.GetVerificationNrql(_selectedStack);
                var dataPackage = new DataPackage();
                dataPackage.SetText(nrql);
                Clipboard.SetContent(dataPackage);
                ShowToast("Copied the New Relic Query", "Paste it in the New Relic Query Builder to confirm the change.", InfoBarSeverity.Informational, 4);            }
        }

        private void ApplySelectedThresholdsButton_Click(object sender, RoutedEventArgs e)
        {
            AlertFetchingProgressRing.IsActive = true;
            AlertFetchingProgress.Visibility = Visibility.Visible;
            AlertFetchingOverlay.Visibility = Visibility.Visible;
            if (!string.IsNullOrEmpty(_selectedStack))
            {
                // Load all alerts for the stack
                var allAlerts = _alertService.GetAlertsForStack(_selectedStack);

                // For each selected alert in the UI, update the corresponding alert in allAlerts
                var selectedAlerts = AlertsListView.SelectedItems.Cast<NrqlAlert>().Where(a => a.ProposedThreshold.HasValue).ToList();
                foreach (var updatedAlert in selectedAlerts)
                {
                    var match = allAlerts.FirstOrDefault(a => a.Name == updatedAlert.Name);
                    if (match != null)
                    {
                        match.CriticalThreshold = updatedAlert.ProposedThreshold.Value;
                    }
                }

                _alertService.SaveAlertsToFile(_selectedStack, allAlerts);
                ShowToast("Thresholds Applied", "Selected thresholds have been updated and saved.", InfoBarSeverity.Success, 4);
                LoadAlertsForStack();
            }
            AlertFetchingProgressRing.IsActive = false;
            AlertFetchingProgress.Visibility = Visibility.Collapsed;
            AlertFetchingOverlay.Visibility = Visibility.Collapsed;
        }

        private void ShowToast(string title, string message, InfoBarSeverity severity, int durationSeconds)
        {
            var toast = new Features.Alerts.CustomControls.CustomToast();
            ToastContainer.Children.Add(toast);
            toast.ShowToast(title, message, severity, durationSeconds);
        }
    }
}
