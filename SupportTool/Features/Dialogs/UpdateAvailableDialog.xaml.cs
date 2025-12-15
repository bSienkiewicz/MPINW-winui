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
            CheckForMajorUpdate();
        }

        private void CheckForMajorUpdate()
        {
            bool isMajorUpdate = UpdateInfo.CurrentVersion.Major < UpdateInfo.Version.Major;
            bool isMinorUpdateByTwoOrMore = UpdateInfo.CurrentVersion.Major == UpdateInfo.Version.Major && 
                                            (UpdateInfo.Version.Minor - UpdateInfo.CurrentVersion.Minor >= 2);

            if (isMajorUpdate || isMinorUpdateByTwoOrMore)
            {
                MajorUpdateWarning.IsOpen = true;
            }
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true;

            try
            {
                var appInstallerUrl = UpdateService.GetAppInstallerUrl();
                var uri = new Uri(appInstallerUrl);
                var success = await Launcher.LaunchUriAsync(uri);
                
                if (success)
                {
                    sender.Hide();
                }
                else
                {
                    var msixUri = new Uri(UpdateInfo.DownloadUrl);
                    await Launcher.LaunchUriAsync(msixUri);
                    sender.Hide();
                }
            }
            catch (Exception ex)
            {
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

        private async void ViewReleaseNotes_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                var releaseUrl = $"https://github.com/{UpdateService.GitHubOwner}/{UpdateService.GitHubRepo}/releases/latest";
                var uri = new Uri(releaseUrl);
                await Launcher.LaunchUriAsync(uri);
            }
            catch (Exception ex)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Error",
                    Content = $"Failed to open release page.\n\nError: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }
    }
}
