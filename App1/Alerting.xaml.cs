using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.Networking;
using System.Diagnostics;
using System.Text.Json;
using App1.Helpers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using StorageFolder = ABI.Windows.Storage.StorageFolder;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace App1
{
    /// <summary>
    /// Manages New Relic alerts configuration for different stacks
    /// </summary>
    public sealed partial class NRAlerts : Page, INotifyPropertyChanged
    {
        // Path to the stacks configuration directory relative to the repository root
        private const string StacksPath = "metaform\\mpm\\copies\\production\\prd\\eu-west-1";

        private readonly ApplicationDataContainer _localSettings;
        private readonly string[] _requiredFolders = { ".github", "ansible", "metaform", "terraform" };

        private string? _selectedFolderPath;
        private string? _selectedStack;
        private string[] _availableStacks = [];

        // Collection of alerts that supports UI updates when modified
        public ObservableCollection<NrqlAlert> AlertItems { get; } = new();

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

        public NRAlerts()
        {
            InitializeComponent();
            _localSettings = ApplicationData.Current.LocalSettings;
            LoadDirectory();
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
            // open new folder in explorer based on the selected folder path + stacks path 
            if (_selectedFolderPath == null) return;

            var path = Path.Combine(_selectedFolderPath, StacksPath);
            if (Directory.Exists(path))
            {
                Process.Start("explorer.exe", path);
            }
        }

        /// <summary>
        /// Initializes a folder picker with proper Windows handle and settings
        /// </summary>
        private FolderPicker InitializeFolderPicker()
        {
            var folderPicker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };

            // Required for WinUI 3 folder picker initialization
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
                GetAlertStacksFromDirectories();
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

        /// <summary>
        /// Validates if the selected folder contains required repository structure
        /// </summary>
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

        #endregion

        #region Alert Management

        private void GetAlertStacksFromDirectories()
        {
            if (string.IsNullOrEmpty(_selectedFolderPath)) return;

            var path = Path.Combine(_selectedFolderPath, StacksPath);
            if (!Directory.Exists(path))
            {
                Debug.WriteLine($"Directory not found: {path}");
                return;
            }

            _availableStacks = Directory.GetDirectories(path)
                .Select(dir => new DirectoryInfo(dir).Name)
                .ToArray();
            stacksComboBox.ItemsSource = _availableStacks;
        }

        private void StackComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_selectedFolderPath == null || e.AddedItems.Count == 0) return;

            _selectedStack = e.AddedItems[0]?.ToString();
            if (string.IsNullOrEmpty(_selectedStack)) return;

            LoadAlertsForStack();
        }

        /// <summary>
        /// Loads and parses alerts from the tfvars file for the selected stack
        /// </summary>
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

        #endregion

        #region UI Event Handlers

        private void NRAlertSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (AlertItems.Count == 0) return;

            var search = NRAlertSearch.Text.ToLower();
            var filteredAlerts = AlertItems.Where(alert =>
                alert.Name.ToLower().Contains(search) ||
                alert.Description.ToLower().Contains(search));
            AlertsListView.ItemsSource = filteredAlerts;
        }

        private void AlertsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                SelectedAlert = (NrqlAlert)e.AddedItems[0];
            }
        }

        private void SaveSelectedAlertButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAlert == null ||
                stacksComboBox.SelectedItem == null ||
                AlertItems == null ||
                _selectedStack == null) return;

            var index = AlertItems.IndexOf(_selectedAlert);
            if (index != -1)
            {
                AlertItems[index] = _selectedAlert;
                SaveAlertsToFile(_selectedStack, AlertItems.ToList());
            }
        }

        private void CopyAlertButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAlert == null || _selectedStack == null) return;

            // Clone the selected alert with a "Copy" suffix
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
            SaveAlertsToFile(_selectedStack, AlertItems.ToList());
        }

        private void DeleteAlertButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAlert == null || _selectedStack == null) return;

            AlertItems.Remove(_selectedAlert);
            SelectedAlert = null;
            SaveAlertsToFile(_selectedStack, AlertItems.ToList());
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
            SaveAlertsToFile(_selectedStack, AlertItems.ToList());
        }

        private async void GenerateAlertButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedStack == null) return;

            var (appName, carrierName) = await ShowGenerateAlertDialog();
            if (string.IsNullOrEmpty(appName) || string.IsNullOrEmpty(carrierName)) return;

            var newAlert = new NrqlAlert
            {
                Name = $"Print duration for {carrierName}",
                Description = $"Alert related to increased {carrierName} print duration",
                NrqlQuery =
                    $"SELECT average(duration) from Transaction where appName = '{appName}.mpm.metapack.com_BlackBox' and CarrierName = '{carrierName}' and PrintOperation like '%create%' FACET BusinessUnit",
                RunbookUrl = "",
                Severity = "CRITICAL",
                Enabled = true,
                AggregationMethod = "event_flow",
                CriticalThresholdOccurrences = "ALL",
                CriticalThresholdDuration = 300,
                CriticalThreshold = 7,
                CriticalOperator = "ABOVE",
                AggregationDelay = 120,
            };
            AlertItems.Add(newAlert);
            AlertsListView.SelectedItem = newAlert;
            SaveAlertsToFile(_selectedStack, AlertItems.ToList());
        }

        #endregion

        #region Storage Operations

        private void SaveDirectory() =>
            _localSettings.Values["NRAlertsDir"] = _selectedFolderPath;

        private void LoadDirectory()
        {
            _selectedFolderPath = _localSettings.Values["NRAlertsDir"] as string ?? string.Empty;
            if (!string.IsNullOrEmpty(_selectedFolderPath))
            {
                ValidateAndUpdateUi(_selectedFolderPath);
            }
        }

        private void DeleteDirectory() =>
            _localSettings.Values["NRAlertsDir"] = string.Empty;

        /// <summary>
        /// Saves the current alerts configuration back to the tfvars file
        /// </summary>
        private void SaveAlertsToFile(string stackName, List<NrqlAlert> alerts)
        {
            if (_selectedFolderPath == null) return;

            var filePath = Path.Combine(_selectedFolderPath, StacksPath, stackName, "auto.tfvars");
            var originalContent = File.ReadAllText(filePath);
            var parser = new HclParser();
            var updatedContent = parser.ReplaceNrqlAlertsSection(originalContent, alerts);
            File.WriteAllText(filePath, updatedContent);
        }

        /// <summary>
        /// Shows a dialog to collect information for generating a new alert
        /// </summary>
        private async Task<(string AppName, string CarrierName)> ShowGenerateAlertDialog()
        {
            var dialog = new ContentDialog
            {
                Title = "Enter Information",
                XamlRoot = Content.XamlRoot,
                PrimaryButtonText = "OK",
                SecondaryButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            var panel = new StackPanel { Spacing = 10 };
            var appNameBox = new TextBox { Header = "AppName:" };
            var carrierBox = new TextBox { Header = "Carrier Name:" };

            panel.Children.Add(appNameBox);
            panel.Children.Add(carrierBox);
            dialog.Content = panel;

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary
                ? (appNameBox.Text, carrierBox.Text)
                : (string.Empty, string.Empty);
        }

        #endregion
    }
}