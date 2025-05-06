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

        public NrqlAlert WorkingCopy
        {
            get => _workingCopy;
            set
            {
                _workingCopy = value;
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

                // Create a working copy for editing
                _workingCopy = value != null ? (NrqlAlert)value.Clone() : new NrqlAlert();

                OnPropertyChanged(nameof(SelectedAlert));
                OnPropertyChanged(nameof(WorkingCopy));
            }
        }

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

            // Force update all bindings from UI controls to WorkingCopy
            BindingExpression[] bindingsToUpdate = {
                NameTextBox.GetBindingExpression(TextBox.TextProperty)
            };

            foreach (var binding in bindingsToUpdate)
            {
                if (binding != null)
                    binding.UpdateSource();
            }

            // Validate the working copy
            var alerts = _alertService.GetAlertsForStack(_selectedStack);
            var errors = _alertService.ValidateAlertInputs(WorkingCopy, alerts);
            if (errors.Count > 0)
            {
                // Show validation errors and return
                var errorMessage = string.Join("\n", errors);
                var toast = new CustomToast();
                ToastContainer.Children.Add(toast);
                toast.ShowToast("Validation error", errorMessage, InfoBarSeverity.Error, 10);
                return;
            }

            // Copy the working copy values to the original
            int index = AlertItems.IndexOf(_originalAlert);
            if (index != -1)
            {
                // Update all properties
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

                // Save to file
                _alertService.SaveAlertsToFile(_selectedStack, AlertItems.ToList());

                // Show success toast
                var toast = new CustomToast();
                ToastContainer.Children.Add(toast);
                toast.ShowToast("Save Successful", "The alert has been saved.", InfoBarSeverity.Success, 3);
            }
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
            // Discard changes by recreating the working copy from the original
            if (_originalAlert != null)
            {
                WorkingCopy = (NrqlAlert)_originalAlert.Clone();
            }
            else
            {
                _originalAlert = null;
                WorkingCopy = new NrqlAlert();
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
    }
}