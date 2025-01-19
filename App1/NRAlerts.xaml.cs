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

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace App1
{
    public sealed partial class NRAlerts : Page
    {
        private string? selectedFolderPath;
        private const string stacksPath = "metaform\\mpm\\copies\\production\\prd\\eu-west-1";
        private string[] availableStacks = [];
        private readonly ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        public NRAlerts()
        {
            this.InitializeComponent();
            LoadDirectory();
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
                ValidateAndUpdateUI(selectedFolderPath);
            }
        }

        private void ValidateAndUpdateUI(string folderPath)
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
                ValidateAndUpdateUI(selectedFolderPath);
                GetAlertStacksFromDirectories();
            }
        }

        private void DeleteDirectory()
        {
            localSettings.Values["NRAlertsDir"] = string.Empty;
        }

        private void StackComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (selectedFolderPath == null) return;
            var selectedItem = e.AddedItems[0]?.ToString();
            if (string.IsNullOrEmpty(selectedItem)) return;
            string tfvarsContent = System.IO.File.ReadAllText(Path.Combine(selectedFolderPath, stacksPath, selectedItem, "auto.tfvars"));
            Debug.WriteLine(tfvarsContent);
        }
    }
}
