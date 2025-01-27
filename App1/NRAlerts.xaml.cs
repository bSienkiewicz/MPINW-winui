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

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace App1
{
    public sealed partial class NRAlerts : Page, INotifyPropertyChanged
    {
        private string? selectedFolderPath;
        private string? selectedStack;
        private const string stacksPath = "metaform\\mpm\\copies\\production\\prd\\eu-west-1";
        private string[] availableStacks = [];
        private readonly ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        private ObservableCollection<NrqlAlert> AlertItems { get; set; } = new ObservableCollection<NrqlAlert>();
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

        public NRAlerts()
        {
            this.InitializeComponent();
            LoadDirectory();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async void SelectFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();

            // Get the window handle from the current window
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

            folderPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            folderPicker.FileTypeFilter.Add("*");

            StorageFolder folder = await folderPicker.PickSingleFolderAsync();

            if (folder != null)
            {
                selectedFolderPath = folder.Path;
                ValidateAndUpdateUi(selectedFolderPath);
            }
        }

        private void ValidateAndUpdateUi(string folderPath)
        {
            var validationResult = ValidateFolder(folderPath);

            //pathTextBlock.Text = folderPath;

            if (validationResult.IsValid)
            {
                infoBar.IsOpen = false;
                //continueButton.IsEnabled = true;
                SaveDirectory();
            }
            else
            {
                infoBar.Title = $"Invalid repository structure.";
                infoBar.Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error;
                infoBar.Message = "PLease select a correct folder.";
                infoBar.IsOpen = true;
                //continueButton.IsEnabled = false;
                DeleteDirectory();
            }
        }

        private (bool IsValid, string[] MissingFolders) ValidateFolder(string folderPath)
        {
            var requiredFolders = new[] { ".github", "ansible", "metaform", "terraform" };

            try
            {
                var existingFolders = Directory.GetDirectories(folderPath)
                    .Select(path => new DirectoryInfo(path).Name)
                    .ToArray();

                var missingFolders = requiredFolders
                    .Where(required => !existingFolders.Contains(required))
                    .ToArray();

                return (
                    IsValid: !missingFolders.Any(),
                    MissingFolders: missingFolders
                );
            }
            catch (Exception)
            {
                return (
                    IsValid: false,
                    MissingFolders: requiredFolders
                );
            }
        }

        private void GetAlertStacksFromDirectories()
        {
            if (selectedFolderPath == null) return;

            var path = Path.Combine(selectedFolderPath, stacksPath);
            if (Directory.Exists(path))
            {
                availableStacks = Directory.GetDirectories(path)
                    .Select(dir => new DirectoryInfo(dir).Name)
                    .ToArray();
                stacksComboBox.ItemsSource = availableStacks;
            }
            else
            {
                Debug.WriteLine($"Directory not found: {path}");
            }
        }


        private void SaveDirectory()
        {
            localSettings.Values["NRAlertsDir"] = selectedFolderPath;
        }

        private void LoadDirectory()
        {
            selectedFolderPath = localSettings.Values["NRAlertsDir"] as string ?? string.Empty;
            if (selectedFolderPath != string.Empty)
            {
                ValidateAndUpdateUi(selectedFolderPath);
                GetAlertStacksFromDirectories();
            }
        }
        private void openFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedFolderPath == null) return;

            var folderPath = Path.Combine(selectedFolderPath, stacksPath);
            if (Directory.Exists(folderPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = folderPath,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            else
            {
                Debug.WriteLine($"Directory not found: {folderPath}");
            }
        }

        private void DeleteDirectory()
        {
            localSettings.Values["NRAlertsDir"] = string.Empty;
        }

        private void StackComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (selectedFolderPath == null) return;

            selectedStack = e.AddedItems[0]?.ToString();
            if (string.IsNullOrEmpty(selectedStack)) return;

            string tfvarsContent = File.ReadAllText(Path.Combine(selectedFolderPath, stacksPath, selectedStack, "auto.tfvars"));
            var parser = new HclParser();
            var parsedAlerts = parser.ParseAlerts(tfvarsContent);

            AlertItems.Clear();
            foreach (var alert in parsedAlerts)
            {
                AlertItems.Add(alert);
            }

            Debug.WriteLine($"** Found {AlertItems.Count} alerts for {selectedStack}");
        }

        private void NRAlertSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (AlertItems.Count == 0) return;
            var search = NRAlertSearch.Text.ToLower();
            var filteredAlerts = AlertItems.Where(alert => alert.Name.ToLower().Contains(search) || alert.Description.ToLower().Contains(search));
            AlertsListView.ItemsSource = filteredAlerts;
        }

        private void AlertsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {

                // Switch to the new alert
                SelectedAlert = (NrqlAlert)e.AddedItems[0];
            }
        }

        private void AddNewAlertButton_Click(object sender, RoutedEventArgs e)
        {
            // Create a new empty alert
            if (selectedStack == null) return;

            var newAlert = new NrqlAlert
            {
                Name = "New Alert",
                Description = "",
                NrqlQuery = "",
                RunbookUrl = "",
                Severity = "CRITICAL",
                Enabled = true,
                AggregationMethod = "",
                AggregationWindow = 0,
                AggregationDelay = 0,
                CriticalOperator = "",
                CriticalThreshold = 0.0,
                CriticalThresholdDuration = 0,
                CriticalThresholdOccurrences = ""
            };

            // Add the new alert to the AlertItems collection
            AlertItems.Add(newAlert);

            // Select the new alert in the ListView
            AlertsListView.SelectedItem = newAlert;

            // Set focus on the Name text box in the right column
            DispatcherQueue.TryEnqueue(() =>
            {
                var nameTextBox = FindName("NameTextBox") as TextBox;
                nameTextBox?.Focus(FocusState.Programmatic);
            });
        }



        private void SaveAlertsToFile(string stackName, List<NrqlAlert> alerts)
        {
            if (selectedFolderPath == null) return;

            var filePath = Path.Combine(selectedFolderPath, stacksPath, stackName, "auto.tfvars");

            // Read the original file content
            var originalContent = File.ReadAllText(filePath);

            // Replace the nr_nrql_alerts section with the updated alerts
            var parser = new HclParser();
            var updatedContent = parser.ReplaceNrqlAlertsSection(originalContent, alerts);

            // Write the updated content back to the file
            File.WriteAllText(filePath, updatedContent);
        }

        private void SaveSelectedAlertButton_Click(object sender, RoutedEventArgs e)
        {
            // Save changes to the current alert before switching
            if (_selectedAlert != null)
            {
                var index = AlertItems.IndexOf(_selectedAlert);
                if (index != -1)
                {
                    AlertItems[index] = _selectedAlert;
                }
                if (stacksComboBox.SelectedItem == null || AlertItems == null || selectedStack == null) return;

                SaveAlertsToFile(selectedStack, AlertItems.ToList());
            }
        }
        private void CopyAlertButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAlert == null || selectedStack == null) return;

            // Create a new alert and copy all properties from the selected alert
            var alertToAdd = new NrqlAlert
            {
                Name = _selectedAlert.Name + " Copy",
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

            AlertItems.Add(alertToAdd);
            AlertsListView.SelectedItem = alertToAdd;
            cloneButton.Flyout.Hide();
            SaveAlertsToFile(selectedStack, AlertItems.ToList());
        }

        private void DeleteAlertButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAlert == null || selectedStack == null) return;
            AlertItems.Remove(_selectedAlert);
            SelectedAlert = null;
            SaveAlertsToFile(selectedStack, AlertItems.ToList());
            deleteButton.Flyout.Hide();
        }

    }
}
