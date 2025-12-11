using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage.Pickers;
using SupportTool.Features.Alerts.Services;
using SupportTool.Features.Alerts.CustomControls;
using SupportTool.Features.Services;
using SupportTool.Features.Dialogs;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SupportTool
{
    public sealed partial class SettingsPage : Page
    {
        private readonly SettingsService _settings = new();
        private string? _selectedFolderPath;
        private readonly string[] _requiredFolders = { ".github", "ansible", "metaform", "terraform" };

        public SettingsPage()
        {
            this.InitializeComponent();
            LoadApiKey();
            LoadAutoSelectSetting();
            LoadDMPolicyIdSetting();
            LoadCurrentVersion();
            string repoPath = _settings.GetSetting("NRAlertsDir");
            ValidateAndUpdateUi(repoPath);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Check if a parameter was passed
            if (e.Parameter is string tabName)
            {
                // Select the corresponding tab based on the parameter
                switch (tabName)
                {
                    case "ApiKeyTab":
                        ExpanderSettings_InitialConfig.IsExpanded = true;
                        break;
                    default:
                        break;
                }
            }
        }

        private void LoadApiKey()
        {
            string apiKey = _settings.GetSetting("NR_API_Key");
            if (apiKey is not null)
            {
                NR_API_KeyTextbox.Text = apiKey ?? string.Empty;
            }
        }

        private async void SelectFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var folder = await InitializeFolderPicker().PickSingleFolderAsync();
            if (folder != null)
            {
                _selectedFolderPath = folder.Path;
                ValidateAndUpdateUi(_selectedFolderPath);
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
            if (string.IsNullOrEmpty(folderPath))
            {
                RepoPathTextBlock.Text = "No repository selected";
                return;
            }

            var (isValid, missingFolders) = ValidateFolder(folderPath);

            if (isValid)
            {
                _settings.SetSetting("NRAlertsDir", folderPath); // Use the parameter, not the field  
                RepoPathTextBlock.Text = $"Selected: {folderPath}"; // Display full path instead of folder name  
            }
            else
            {
                var missingFoldersText = string.Join(", ", missingFolders);
                var toast = new CustomToast();
                ToastContainer.Children.Add(toast);
                toast.ShowToast("Invalid repository structure",
                    $"Missing directories: {missingFoldersText}",
                    InfoBarSeverity.Error, 10);
                _settings.SetSetting("NRAlertsDir", string.Empty);
                RepoPathTextBlock.Text = "No valid repository selected";
            }
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

        private void NR_API_Key_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            string apiKey = NR_API_KeyTextbox.Text;
            bool isValid = ValidateAPIKey(apiKey);
            if (isValid)
            {
                _settings.SetSetting("NR_API_Key", apiKey);
                NR_API_KeyTextbox.BorderBrush = null;
            }
            else
            {
                NR_API_KeyTextbox.BorderBrush = new SolidColorBrush(Colors.Red);
            }
        }

        private bool ValidateAPIKey(string apiKey)
        {
            // Define the regular expression pattern
            string pattern = @"^NRAK-[A-Z0-9]{27}$";

            // Use Regex to check if the API key matches the pattern
            return System.Text.RegularExpressions.Regex.IsMatch(apiKey, pattern);
        }

        private void LoadAutoSelectSetting()
        {
            string setting = _settings.GetSetting("AutoSelectMissingAlerts", "Both");
            foreach (ComboBoxItem item in AutoSelectComboBox.Items)
            {
                if (item.Tag?.ToString() == setting)
                {
                    AutoSelectComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void LoadDMPolicyIdSetting()
        {
            string policyId = _settings.GetSetting("DMPolicyId", "6708037");
            DMPolicyIdTextBox.Text = policyId;
        }

        private void LoadCurrentVersion()
        {
            try
            {
                // GetCurrentVersion() now uses a constant fallback, so it should never throw
                var version = UpdateService.GetCurrentVersion();
                if (version != null && version.Major > 0)
                {
                    CurrentVersionTextBlock.Text = $"Current version: v{version.Major}.{version.Minor}.{version.Build}";
                }
                else
                {
                    CurrentVersionTextBlock.Text = "Version unavailable";
                }
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                System.Diagnostics.Debug.WriteLine($"Error loading version: {ex.GetType().Name} - {ex.Message}");
                CurrentVersionTextBlock.Text = "Version unavailable";
            }
        }

        private void DMPolicyIdTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string policyId = DMPolicyIdTextBox.Text;
            if (!string.IsNullOrWhiteSpace(policyId) && int.TryParse(policyId, out _))
            {
                _settings.SetSetting("DMPolicyId", policyId);
                DMPolicyIdTextBox.BorderBrush = null;
            }
            else if (!string.IsNullOrWhiteSpace(policyId))
            {
                DMPolicyIdTextBox.BorderBrush = new SolidColorBrush(Colors.Red);
            }
        }

        private void AutoSelectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AutoSelectComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string tag)
            {
                _settings.SetSetting("AutoSelectMissingAlerts", tag);
            }
        }

        private void DeleteConfirmation_Click(object sender, RoutedEventArgs e)
        {
            _settings.RemoveAllSettings();
        }

        private async void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            CheckForUpdatesButton.IsEnabled = false;
            CheckForUpdatesButton.Content = "Checking...";

            try
            {
                var currentVersion = UpdateService.GetCurrentVersion();
                var updateService = new UpdateService();
                var updateInfo = await updateService.CheckForUpdatesAsync();

                if (updateInfo != null)
                {
                    var dialog = new UpdateAvailableDialog(updateInfo)
                    {
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
                else
                {
                    var dialog = new ContentDialog
                    {
                        Title = "No Updates Available",
                        Content = $"You are running the latest version of the application (v{currentVersion.Major}.{currentVersion.Minor}.{currentVersion.Build}).",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "Error",
                    Content = $"Failed to check for updates: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
            finally
            {
                CheckForUpdatesButton.IsEnabled = true;
                CheckForUpdatesButton.Content = "Check for Updates";
            }
        }
    }
}
