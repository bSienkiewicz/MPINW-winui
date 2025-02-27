using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using Windows.Storage;
using System.Diagnostics;
using SupportTool.Helpers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using SupportTool.Services;
using Windows.ApplicationModel.DataTransfer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

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
        private readonly string[] _requiredFolders = { ".github", "ansible", "metaform", "terraform" };
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

        private NrqlAlert _selectedAlert;
        public NrqlAlert SelectedAlert
        {
            get => _selectedAlert;
            set
            {
                if (_selectedAlert != value)
                {
                    _selectedAlert = value;
                    OnPropertyChanged(nameof(SelectedAlert));
                }
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

        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        #region Directory Management

        private async void SelectFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var folder = await InitializeFolderPicker().PickSingleFolderAsync();
            if (folder != null)
            {
                _selectedFolderPath = folder.Path;
                ValidateAndUpdateUi(_selectedFolderPath);
            }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFolderPath == null) return;

            var path = Path.Combine(_selectedFolderPath, StacksPath);
            if (Directory.Exists(path))
            {
                Process.Start("explorer.exe", path);
            }
        }

        private FolderPicker InitializeFolderPicker()
        {
            var folderPicker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
            folderPicker.FileTypeFilter.Add("*");

            return folderPicker;
        }

        private void ValidateAndUpdateUi(string folderPath)
        {
            var (isValid, _) = ValidateFolder(folderPath);

            if (isValid)
            {
                infoBar.IsOpen = false;
                SaveDirectory();
                stacksComboBox.ItemsSource = _alertService.GetAlertStacksFromDirectories();
            }
            else
            {
                ShowErrorInfoBar("Invalid repository structure.", "Please select a correct folder.");
                DeleteDirectory();
            }
        }

        private void ShowErrorInfoBar(string title, string message)
        {
            infoBar.Title = title;
            infoBar.Severity = InfoBarSeverity.Error;
            infoBar.Message = message;
            infoBar.IsOpen = true;
        }

        private (bool IsValid, string[] MissingFolders) ValidateFolder(string folderPath)
        {
            try
            {
                var existingFolders = Directory.GetDirectories(folderPath)
                    .Select(path => new DirectoryInfo(path).Name)
                    .ToArray();

                var missingFolders = _requiredFolders.Except(existingFolders).ToArray();
                return (IsValid: !missingFolders.Any(), MissingFolders: missingFolders);
            }
            catch (Exception)
            {
                return (IsValid: false, MissingFolders: _requiredFolders);
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
                // Set the selected alerts to display its details
                SelectedAlert = (NrqlAlert)e.AddedItems[0];
            }
        }

        private void SaveSelectedAlertButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAlert == null ||
                stacksComboBox.SelectedItem == null ||
                AlertItems == null ||
                _selectedStack == null) return;

            if (!_alertService.ValidateAlertInputs(_selectedAlert))
            {
                return;
            }

            var index = AlertItems.IndexOf(_selectedAlert);
            if (index != -1)
            {
                AlertItems[index] = _selectedAlert;
                _alertService.SaveAlertsToFile(_selectedStack, AlertItems.ToList());
            }
        }

        private void CopyAlertButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAlert == null || _selectedStack == null) return;

            var alertCopy = new NrqlAlert
            {
                Name = $"{_selectedAlert.Name} Copy",
                Description = _selectedAlert.Description,
                NrqlQuery = _selectedAlert.NrqlQuery,
                RunbookUrl = _selectedAlert.RunbookUrl,
                Severity = _selectedAlert.Severity,
                Enabled = _selectedAlert.Enabled,
                AggregationMethod = _selectedAlert.AggregationMethod,
                AggregationWindow = _selectedAlert.AggregationWindow,
                AggregationDelay = _selectedAlert.AggregationDelay,
                CriticalOperator = _selectedAlert.CriticalOperator,
                CriticalThreshold = _selectedAlert.CriticalThreshold,
                CriticalThresholdDuration = _selectedAlert.CriticalThresholdDuration,
                CriticalThresholdOccurrences = _selectedAlert.CriticalThresholdOccurrences
            };

            AlertItems.Add(alertCopy);
            AlertsListView.SelectedItem = alertCopy;
            cloneButton.Flyout.Hide();
            _alertService.SaveAlertsToFile(_selectedStack, AlertItems.ToList());
        }

        private void DeleteAlertButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAlert == null || _selectedStack == null) return;

            AlertItems.Remove(_selectedAlert);
            SelectedAlert = new NrqlAlert();
            _alertService.SaveAlertsToFile(_selectedStack, AlertItems.ToList());
            deleteButton.Flyout.Hide();
        }

        private void AddNewAlertButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedStack == null) return;

            var newAlert = new NrqlAlert
            {
                Name = "New Alert", Description = "Alert description", NrqlQuery = "",
                RunbookUrl = "", Severity = "CRITICAL", Enabled = true, AggregationMethod = "event_flow"
            };
            AlertItems.Add(newAlert);
            AlertsListView.SelectedItem = newAlert;
            _alertService.SaveAlertsToFile(_selectedStack, AlertItems.ToList());
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

        private void SaveDirectory() =>
            _settings.SetSetting("NRAlertsDir", _selectedFolderPath);


        private void DeleteDirectory() =>
            _settings.SetSetting("NRAlertsDir", string.Empty);

        private void LoadDirectory()
        {
            _selectedFolderPath = _settings.GetSetting("NRAlertsDir");
            if (!string.IsNullOrEmpty(_selectedFolderPath))
            {
                ValidateAndUpdateUi(_selectedFolderPath);
            }
        }
        #endregion

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedAlert = new NrqlAlert();
            AlertsListView.SelectedItem = null;
        }

        private void SortAlertsButton_Click(object sender, RoutedEventArgs e)
        {
            var sortedList = AlertItems.OrderBy(item => item.Name.ToLower()).ToList();
            AlertItems.Clear();
            foreach(var item in sortedList)
            {
                AlertItems.Add(item);
            }
        }
    }
}