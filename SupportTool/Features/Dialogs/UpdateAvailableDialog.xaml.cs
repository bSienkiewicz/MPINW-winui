using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using SupportTool.Features.Services;
using Windows.System;

namespace SupportTool.Features.Dialogs
{
    public sealed partial class UpdateAvailableDialog : ContentDialog
    {
        public UpdateInfo UpdateInfo { get; set; }

        public UpdateAvailableDialog(UpdateInfo updateInfo)
        {
            InitializeComponent();
            UpdateInfo = updateInfo;
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Defer closing the dialog until we've launched the update
            args.Cancel = true;

            try
            {
                // Use AppInstaller for automatic updates (recommended)
                // This will automatically download and install the update
                var appInstallerUrl = UpdateService.GetAppInstallerUrl();
                
                // Use Launcher for URLs in WinUI apps
                var uri = new Uri(appInstallerUrl);
                var success = await Launcher.LaunchUriAsync(uri);
                
                if (success)
                {
                    // Close the dialog after successful launch
                    sender.Hide();
                }
                else
                {
                    // Fallback: Try opening the MSIX download URL
                    var msixUri = new Uri(UpdateInfo.DownloadUrl);
                    await Launcher.LaunchUriAsync(msixUri);
                    sender.Hide();
                }
            }
            catch (Exception ex)
            {
                // If Launcher fails, try Process.Start as fallback
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = UpdateInfo.DownloadUrl,
                        UseShellExecute = true
                    });
                    sender.Hide();
                }
                catch
                {
                    // Show error if all methods fail
                    var errorDialog = new ContentDialog
                    {
                        Title = "Error",
                        Content = $"Failed to open update URL.\n\nError: {ex.Message}\n\nPlease download manually from:\n{UpdateInfo.DownloadUrl}",
                        CloseButtonText = "OK",
                        XamlRoot = sender.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                    sender.Hide();
                }
            }
        }
    }
}
