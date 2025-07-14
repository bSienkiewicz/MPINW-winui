using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SupportTool.CustomControls;
using SupportTool.Helpers;
using SupportTool.Models;
using SupportTool.Services;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.ApplicationModel.DataTransfer;
using System.ComponentModel;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SupportTool
{
    public sealed partial class ThresholdManager : Page
    {
        private const string StacksPath = "metaform\\mpm\\copies\\production\\prd\\eu-west-1";
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
            AlertItems.CollectionChanged += AlertItems_CollectionChanged;
        }

        private void AlertItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is NrqlAlert alert)
                        alert.PropertyChanged += Alert_PropertyChanged;
                }
            }
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is NrqlAlert alert)
                        alert.PropertyChanged -= Alert_PropertyChanged;
                }
            }
            UpdateHeaderCheckBox();
        }

        private void Alert_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is NrqlAlert alert)
            {
                if (e.PropertyName == nameof(NrqlAlert.IsSelectedForUpdate))
                {
                    UpdateHeaderCheckBox();
                }
                else if (e.PropertyName == nameof(NrqlAlert.ProposedThreshold))
                {
                    if (!alert.IsSelectedForUpdate)
                        alert.IsSelectedForUpdate = true;
                }
            }
        }

        private void HeaderCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (HeaderCheckBox.IsChecked == true)
            {
                foreach (var alert in AlertItems)
                    alert.IsSelectedForUpdate = true;
            }
            else if (HeaderCheckBox.IsChecked == false)
            {
                foreach (var alert in AlertItems)
                    alert.IsSelectedForUpdate = false;
            }
            // If indeterminate, do nothing
        }

        private void UpdateHeaderCheckBox()
        {
            if (AlertItems.Count == 0)
            {
                HeaderCheckBox.IsChecked = false;
                return;
            }
            int selected = AlertItems.Count(a => a.IsSelectedForUpdate);
            if (selected == 0)
                HeaderCheckBox.IsChecked = false;
            else if (selected == AlertItems.Count)
                HeaderCheckBox.IsChecked = true;
            else
                HeaderCheckBox.IsChecked = null; // Indeterminate
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
            AlertFetchingProgress.IsActive = true;
            AlertFetchingProgress.Visibility = Visibility.Visible;
            try
            {
                var carriers = AlertItems
                    .Select(alert => AlertService.ExtractCarrierFromTitle(alert.Name))
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Distinct()
                    .ToList();

                if (carriers.Count == 0)
                {
                    ShowToast("No carriers found", "There are no carriers to fetch.", InfoBarSeverity.Warning, 5);
                    CalculateTimesButton.IsEnabled = true;
                    AlertFetchingProgress.IsActive = false;
                    AlertFetchingProgress.Visibility = Visibility.Collapsed;
                    return;
                }

                var apiService = new NewRelicApiService();
                var statsDict = await apiService.FetchDurationStatisticsForCarriersAsync(carriers);

                foreach (var alert in AlertItems)
                {
                    var carrier = AlertService.ExtractCarrierFromTitle(alert.Name);
                    if (!string.IsNullOrEmpty(carrier) && statsDict.TryGetValue(carrier, out var stats) && stats.HasData)
                    {
                        alert.ProposedThreshold = AlertService.CalculateSuggestedThreshold(stats);
                    }
                    else
                    {
                        alert.ProposedThreshold = null;
                    }

                    // Auto-check if difference is >= threshold
                    if (alert.ProposedThreshold.HasValue)
                    {
                        double diff = Math.Abs(alert.CriticalThreshold - alert.ProposedThreshold.Value);
                        double threshold = AlertTemplates.GetThresholdDifference();
                        alert.IsSelectedForUpdate = diff >= threshold;
                    }
                    else
                    {
                        alert.IsSelectedForUpdate = false;
                    }
                }
                ShowToast("Success", "Proposed times fetched from New Relic.", InfoBarSeverity.Success, 4);
            }
            catch (Exception ex)
            {
                ShowToast("Error", $"Failed to fetch times: {ex.Message}", InfoBarSeverity.Error, 6);
            }
            finally
            {
                CalculateTimesButton.IsEnabled = true;
                AlertFetchingProgress.IsActive = false;
                AlertFetchingProgress.Visibility = Visibility.Collapsed;
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
                // Optionally show a toast or InfoBar here
            }
        }

        private void ApplySelectedThresholdsButton_Click(object sender, RoutedEventArgs e)
        {
            AlertFetchingProgress.IsActive = true;
            AlertFetchingProgress.Visibility = Visibility.Visible;
            if (!string.IsNullOrEmpty(_selectedStack))
            {
                // Load all alerts for the stack
                var allAlerts = _alertService.GetAlertsForStack(_selectedStack);

                // For each PrintDuration alert in the UI, update the corresponding alert in allAlerts
                foreach (var updatedAlert in AlertItems.Where(a => a.IsSelectedForUpdate && a.ProposedThreshold.HasValue))
                {
                    var match = allAlerts.FirstOrDefault(a => a.Name == updatedAlert.Name);
                    if (match != null)
                    {
                        match.CriticalThreshold = updatedAlert.ProposedThreshold.Value;
                        updatedAlert.IsSelectedForUpdate = false;
                    }
                }

                _alertService.SaveAlertsToFile(_selectedStack, allAlerts);
                ShowToast("Thresholds Applied", "Selected thresholds have been updated and saved.", InfoBarSeverity.Success, 4);
                LoadAlertsForStack();
            }
            AlertFetchingProgress.IsActive = false;
            AlertFetchingProgress.Visibility = Visibility.Collapsed;
        }

        private void ShowToast(string title, string message, InfoBarSeverity severity, int durationSeconds)
        {
            var toast = new CustomControls.CustomToast();
            ToastContainer.Children.Add(toast);
            toast.ShowToast(title, message, severity, durationSeconds);
        }
    }
}
