using Microsoft.UI.Xaml.Controls;
using SupportTool.Features.Services;

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

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Open the download URL
            UpdateService.OpenDownloadUrl(UpdateInfo.DownloadUrl);
        }
    }
}
