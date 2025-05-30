using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using System.Diagnostics;
using SupportTool.Helpers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using SupportTool.Services;
using Windows.ApplicationModel.DataTransfer;
using SupportTool.CustomControls;
using SupportTool.Models;
using Microsoft.UI.Xaml.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SupportTool
{
    /// <summary>
    /// Manages New Relic alerts configuration for different stacks
    /// </summary>
    public sealed partial class Alerting : Page, INotifyPropertyChanged
    {
        // Path to the stacks configuration directory relative to the repository root
        private const string StacksPath = "metaform\\mpm\\copies\\production\\prd\\eu-west-1";

        public ObservableCollection<NrqlAlert> AlertItems { get; } = new();
        private string? _selectedFolderPath;
        private string? _selectedStack;
        private NewRelicApiService _newRelicApiService = new();
        private string[] _availableStacks = [];
        private readonly AlertService _alertService = new();
        private readonly SettingsService _settings = new();
        private int _dragStartIndex = -1;

        public string[] Severities => AlertConstants.Severities;
        public string[] AggregationMethods => AlertConstants.AggregationMethods;
        public string[] CriticalOperators => AlertConstants.CriticalOperators;
        public string[] ThresholdOccurrences => AlertConstants.ThresholdOccurrences;

        private NrqlAlert _originalAlert;
        private NrqlAlert _workingCopy;

        private bool _showFetchDurationButton = false;
        private bool _isFetchDurationEnabled = true;
        private bool _isFetchingDuration = false;

        private int _additionalFieldsCount = 0;
        public int AdditionalFieldsCount
        {
            get => _additionalFieldsCount;
            set
            {
                _additionalFieldsCount = value;
                OnPropertyChanged(nameof(AdditionalFieldsCount));
            }
        }

        public bool ShowFetchDurationButton
        {
            get => _showFetchDurationButton;
            set
            {
                _showFetchDurationButton = value;
                OnPropertyChanged(nameof(ShowFetchDurationButton));
            }
        }

        public bool IsFetchDurationEnabled
        {
            get => _isFetchDurationEnabled;
            set
            {
                _isFetchDurationEnabled = value;
                OnPropertyChanged(nameof(IsFetchDurationEnabled));
            }
        }

        public bool IsFetchingDuration
        {
            get => _isFetchingDuration;
            set
            {
                _isFetchingDuration = value;
                OnPropertyChanged(nameof(IsFetchingDuration));
            }
        }

        public NrqlAlert WorkingCopy
        {
            get => _workingCopy;
            set
            {
                _workingCopy = value;
                UpdateFetchDurationButtonVisibility();
                OnPropertyChanged(nameof(WorkingCopy));
            }
        }

        // This property manages the alert currently selected by the user
        public NrqlAlert SelectedAlert
        {
            get => _originalAlert;
            set
            {
                _originalAlert = value;
                _workingCopy = value != null ? (NrqlAlert)value.Clone() : new NrqlAlert();
                
                // Populate additional fields for UI
                PopulateAdditionalFields();
                
                // Update fetch button visibility
                UpdateFetchDurationButtonVisibility();
                
                OnPropertyChanged(nameof(SelectedAlert));
                OnPropertyChanged(nameof(WorkingCopy));
            }
        }

        public ObservableCollection<AdditionalField> AdditionalFields { get; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public Alerting()
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

        public void RefreshStacks()
        {
            LoadDirectory();
            LoadStack();
        }

        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        #region Directory Management

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFolderPath == null) return;

            var path = Path.Combine(_selectedFolderPath, StacksPath);
            if (Directory.Exists(path))
            {
                Process.Start("explorer.exe", path);
            }
        }

        private void StackComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_selectedFolderPath == null || e.AddedItems.Count == 0) return;

            _selectedStack = e.AddedItems[0]?.ToString();
            _settings.SetSetting("SelectedStack", _selectedStack);
            if (string.IsNullOrEmpty(_selectedStack)) return;

            // Clear the selected alerts section
            SelectedAlert = new NrqlAlert();
            AlertsListView.SelectedItem = null;

            // Load alerts for selected stack and re-set the sources
            LoadAlertsForStack();
            NRAlertSearch.Text = string.Empty;
            AlertsListView.ItemsSource = AlertItems;
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
                AlertItems.Add(alert);
            }
        }

        private void NRAlertSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (AlertItems.Count == 0) return;

            var search = NRAlertSearch.Text.ToLower();
            var filteredAlerts = AlertItems.Where(alert =>
                alert.Name.ToLower().Contains(search) ||
                alert.Description.ToLower().Contains(search) ||
                alert.NrqlQuery.ToLower().Contains(search));

            AlertsListView.ItemsSource = filteredAlerts.OrderBy(alert => alert.Name.ToLower());
        }

        private void AlertsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                // Set the selected alert and create a working copy
                SelectedAlert = (NrqlAlert)e.AddedItems[0];
            }
            else
            {
                SelectedAlert = null;
            }
        }

        private void SaveSelectedAlertButton_Click(object sender, RoutedEventArgs e)
        {
            if (_originalAlert == null || WorkingCopy == null ||
                stacksComboBox.SelectedItem == null ||
                AlertItems == null ||
                _selectedStack == null) return;

            // Sync additional fields before validation
            SyncAdditionalFieldsToWorkingCopy();

            // Validate the working copy
            var alerts = _alertService.GetAlertsForStack(_selectedStack);
            var errors = _alertService.ValidateAlertInputs(WorkingCopy, alerts);
            if (errors.Count > 0)
            {
                // Show validation errors and return
                var errorMessage = string.Join("\n", errors);
                ToastContainer.Children.Add(new CustomToast());
                new CustomToast().ShowToast("Validation error", errorMessage, InfoBarSeverity.Error, 10);
                return;
            }

            // Update all properties including additional fields
            _originalAlert.Name = WorkingCopy.Name;
            _originalAlert.Description = WorkingCopy.Description;
            _originalAlert.NrqlQuery = WorkingCopy.NrqlQuery;
            _originalAlert.RunbookUrl = WorkingCopy.RunbookUrl;
            _originalAlert.Severity = WorkingCopy.Severity;
            _originalAlert.Enabled = WorkingCopy.Enabled;
            _originalAlert.AggregationMethod = WorkingCopy.AggregationMethod;
            _originalAlert.AggregationWindow = WorkingCopy.AggregationWindow;
            _originalAlert.AggregationDelay = WorkingCopy.AggregationDelay;
            _originalAlert.CriticalOperator = WorkingCopy.CriticalOperator;
            _originalAlert.CriticalThreshold = WorkingCopy.CriticalThreshold;
            _originalAlert.CriticalThresholdDuration = WorkingCopy.CriticalThresholdDuration;
            _originalAlert.CriticalThresholdOccurrences = WorkingCopy.CriticalThresholdOccurrences;
            _originalAlert.AdditionalFields = new Dictionary<string, object>(WorkingCopy.AdditionalFields);

            // Save to file
            _alertService.SaveAlertsToFile(_selectedStack, AlertItems.ToList());

            // Show success toast
            var toast = new CustomToast();
            ToastContainer.Children.Add(toast);
            toast.ShowToast("Save Successful", "The alert has been saved.", InfoBarSeverity.Success, 3);

            LoadStack();
        }

        private void CopyAlertButton_Click(object sender, RoutedEventArgs e)
        {
            if (_originalAlert == null || _selectedStack == null) return;

            // Create a new alert based on the original (not the working copy)
            var alertCopy = new NrqlAlert
            {
                Name = $"{_originalAlert.Name} Copy",
                Description = _originalAlert.Description,
                NrqlQuery = _originalAlert.NrqlQuery,
                RunbookUrl = _originalAlert.RunbookUrl,
                Severity = _originalAlert.Severity,
                Enabled = _originalAlert.Enabled,
                AggregationMethod = _originalAlert.AggregationMethod,
                AggregationWindow = _originalAlert.AggregationWindow,
                AggregationDelay = _originalAlert.AggregationDelay,
                CriticalOperator = _originalAlert.CriticalOperator,
                CriticalThreshold = _originalAlert.CriticalThreshold,
                CriticalThresholdDuration = _originalAlert.CriticalThresholdDuration,
                CriticalThresholdOccurrences = _originalAlert.CriticalThresholdOccurrences
            };

            // Add the copy to the collection
            AlertItems.Add(alertCopy);

            // Select the new alert in the ListView
            AlertsListView.SelectedItem = alertCopy;

            // Close the flyout
            cloneButton.Flyout.Hide();

            // Save to file
            _alertService.SaveAlertsToFile(_selectedStack, AlertItems.ToList());

            // Show toast
            var toast = new CustomToast();
            ToastContainer.Children.Add(toast);
            toast.ShowToast("Alert duplicated", "", InfoBarSeverity.Success, 3);
        }

        private void DeleteAlertButton_Click(object sender, RoutedEventArgs e)
        {
            if (_originalAlert == null || _selectedStack == null) return;

            // Remove the original alert from the collection
            AlertItems.Remove(_originalAlert);

            // Clear the selection
            _originalAlert = null;
            WorkingCopy = new NrqlAlert();
            AlertsListView.SelectedItem = null;

            // Save to file
            _alertService.SaveAlertsToFile(_selectedStack, AlertItems.ToList());

            // Close the flyout
            deleteButton.Flyout.Hide();

            // Show toast
            var toast = new CustomToast();
            ToastContainer.Children.Add(toast);
            toast.ShowToast("Alert deleted", "", InfoBarSeverity.Success, 3);
        }

        private void AddNewAlertButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedStack == null) return;

            var newAlert = new NrqlAlert
            {
                Name = "New Alert Name",
                Description = "Alert description",
                NrqlQuery = "SELECT * FROM Transaction",
                RunbookUrl = "",
                Severity = "CRITICAL",
                Enabled = true,
                AggregationMethod = "EVENT_FLOW",
                AggregationDelay = 0,
                AggregationWindow = 0,
                CriticalOperator = "EQUALS",
                CriticalThreshold = 0,
                CriticalThresholdDuration = 0,
                CriticalThresholdOccurrences = "ALL"
            };

            // Add the new alert to the collection
            AlertItems.Add(newAlert);

            // Select the new alert in the ListView
            AlertsListView.SelectedItem = newAlert;

            // Save to file
            _alertService.SaveAlertsToFile(_selectedStack, AlertItems.ToList());

            // Show toast
            var toast = new CustomToast();
            ToastContainer.Children.Add(toast);
            toast.ShowToast("Empty alert created", "", InfoBarSeverity.Success, 3);
        }

        // Used for alert reordering
        private void AlertsListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            if (e.Items.FirstOrDefault() is NrqlAlert draggedAlert)
            {
                _dragStartIndex = AlertItems.IndexOf(draggedAlert); // store the index of initial Alert position on the list
            }
        }

        // Used for alert reordering
        private void AlertsListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            if (args.DropResult == DataPackageOperation.Move && _dragStartIndex != -1)
            {
                if (args.Items.FirstOrDefault() is NrqlAlert draggedAlert)
                {
                    int oldIndex = _dragStartIndex;
                    int newIndex = AlertItems.IndexOf(draggedAlert);

                    if (newIndex >= 0 && newIndex != oldIndex)
                    {
                        AlertItems.Move(oldIndex, newIndex);

                        if (_selectedFolderPath != null && _selectedStack != null)
                        {
                            _alertService.SaveAlertsToFile(_selectedStack, AlertItems.ToList());
                        }
                    }
                }
            }

            _dragStartIndex = -1; // Reset for next drag operation
        }

        #endregion

        #region Storage Operations
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

                // Show message to user
                var toast = new CustomToast();
                ToastContainer.Children.Add(toast);
                toast.ShowToast("No repository selected",
                    "Please select a repository in Settings first",
                    InfoBarSeverity.Warning, 5);
            }
        }
        #endregion

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_originalAlert != null)
            {
                WorkingCopy = (NrqlAlert)_originalAlert.Clone();
                PopulateAdditionalFields(); // Refresh additional fields
            }
            else
            {
                _originalAlert = null;
                WorkingCopy = new NrqlAlert();
                AdditionalFields.Clear();
                AlertsListView.SelectedItem = null;
            }
        }

        private void SortAlertsButton_Click(object sender, RoutedEventArgs e)
        {
            var sortedList = AlertItems.OrderBy(item => item.Name.ToLower()).ToList();
            AlertItems.Clear();
            foreach(var item in sortedList)
            {
                AlertItems.Add(item);
            }


            // Show toast
            var toast = new CustomToast();
            ToastContainer.Children.Add(toast);
            toast.ShowToast("Alerts sorted alphabetically", "Save any alert to confirm the new order", InfoBarSeverity.Success, 5);
        }

        private void refreshAlertsButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedAlert = new NrqlAlert();
            AlertsListView.SelectedItem = null;
            LoadStack();
        }

        private void PopulateAdditionalFields()
        {
            AdditionalFields.Clear();
            if (_workingCopy?.AdditionalFields != null)
            {
                foreach (var kvp in _workingCopy.AdditionalFields)
                {
                    AdditionalFields.Add(new AdditionalField
                    {
                        Key = kvp.Key,
                        Value = FormatValueForDisplay(kvp.Value)
                    });
                }
            }
            AdditionalFieldsCount = AdditionalFields.Count;
        }

        private string FormatValueForDisplay(object value)
        {
            return value switch
            {
                null => "null",
                string str => $"\"{str}\"", // Add quotes around strings
                bool b => b.ToString().ToLowerInvariant(),
                _ => value.ToString()
            };
        }

        private void SyncAdditionalFieldsToWorkingCopy()
        {
            if (_workingCopy == null) return;
            
            _workingCopy.AdditionalFields.Clear();
            foreach (var field in AdditionalFields)
            {
                if (!string.IsNullOrWhiteSpace(field.Key))
                {
                    _workingCopy.AdditionalFields[field.Key] = ParseFieldValue(field.Value);
                }
            }
        }

        private object ParseFieldValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Trim() == "null")
                return null;

            value = value.Trim();

            // If it's quoted, treat as string
            if (value.StartsWith("\"") && value.EndsWith("\""))
            {
                return value.Substring(1, value.Length - 2);
            }

            // Try to parse as bool
            if (value.ToLowerInvariant() == "true" || value.ToLowerInvariant() == "false")
            {
                return bool.Parse(value);
            }

            // Try to parse as int
            if (int.TryParse(value, out int intValue))
            {
                return intValue;
            }

            // Try to parse as double
            if (double.TryParse(value, out double doubleValue))
            {
                return doubleValue;
            }

            // Default to string without quotes
            return value;
        }

        private void AddAdditionalField_Click(object sender, RoutedEventArgs e)
        {
            AdditionalFields.Add(new AdditionalField
            {
                Key = "new_field",
                Value = "\"\""
            });
            AdditionalFieldsCount = AdditionalFields.Count;
        }

        private void RemoveAdditionalField_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is AdditionalField field)
            {
                AdditionalFields.Remove(field);
                AdditionalFieldsCount = AdditionalFields.Count;
            }
        }

        private void UpdateFetchDurationButtonVisibility()
        {
            if (_workingCopy == null)
            {
                ShowFetchDurationButton = false;
                return;
            }

            // Check if NRQL contains 'average(duration)' and title contains carrier name
            bool hasAverageDuration = _workingCopy.NrqlQuery?.ToLower().Contains("average(duration)") == true;
            bool hasCarrierInTitle = !string.IsNullOrEmpty(ExtractCarrierFromTitle(_workingCopy.Name));
            
            ShowFetchDurationButton = hasAverageDuration && hasCarrierInTitle;
        }

        private string ExtractCarrierFromTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
                return string.Empty;

            // Extract carrier name from format "Carrier - Description"
            int dashIndex = title.IndexOf(" - ");
            if (dashIndex > 0)
            {
                return title.Substring(0, dashIndex).Trim();
            }

            return string.Empty;
        }

        private async void FetchDurationButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsFetchingDuration || _workingCopy == null) return;

            string carrierName = ExtractCarrierFromTitle(_workingCopy.Name);
            if (string.IsNullOrEmpty(carrierName))
            {
                var toast = new CustomToast();
                ToastContainer.Children.Add(toast);
                toast.ShowToast("Error", "Could not extract carrier name from title", InfoBarSeverity.Error, 5);
                return;
            }

            try
            {
                IsFetchingDuration = true;
                IsFetchDurationEnabled = false;
                CriticalThresholdNumberBox.Focus(FocusState.Programmatic);

                // Use NewRelicApiService directly
                var statistics = await _newRelicApiService.FetchDurationStatisticsForCarrierAsync(carrierName);
                
                // Use the same calculation logic as other places
                double suggestedThreshold = AlertService.CalculateSuggestedThreshold(statistics);
                
                // Set the suggested threshold
                _workingCopy.CriticalThreshold = suggestedThreshold;
                OnPropertyChanged(nameof(WorkingCopy));

                // Show the suggested value in the UI like AlertDetailsDialog does
                ProposedThresholdText.Text = $"Avg: {statistics.AverageDuration:F2}s, StdDev: {statistics.StandardDeviation:F2}s.\nProposed threshold: {suggestedThreshold:F2}s";
                ProposedThresholdText.Tag = suggestedThreshold;
                ProposedThresholdText.Visibility = Visibility.Visible;

                var toast = new CustomToast();
                ToastContainer.Children.Add(toast);
                toast.ShowToast("Success", 
                    $"Suggested threshold for {carrierName}: {suggestedThreshold:F2}s", 
                    InfoBarSeverity.Success, 5);
            }
            catch (Exception ex)
            {
                var toast = new CustomToast();
                ToastContainer.Children.Add(toast);
                toast.ShowToast("Error", 
                    $"Failed to fetch duration data: {ex.Message}", 
                    InfoBarSeverity.Error, 5);
            }
            finally
            {
                FetchDurationButton.IsEnabled = true;
                IsFetchingDuration = false;
                IsFetchDurationEnabled = true;
            }
        }

        private void ProposedThresholdText_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            // Get the proposed value from the Tag property
            if (ProposedThresholdText.Tag is double proposedValue)
            {
                // Set the value to the NumberBox
                _workingCopy.CriticalThreshold = proposedValue;
                OnPropertyChanged(nameof(_workingCopy));
                CriticalThresholdNumberBox.Focus(FocusState.Programmatic);
            }
        }
    }
}