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
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SupportTool
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // Initialize default settings if they don't exist
            InitializeDefaultSettings();
            
            MainWindow = new MainWindow();
            MainWindow.Activate();

            // Check for updates in the background (don't block startup)
            _ = CheckForUpdatesInBackground();
        }

        private async Task CheckForUpdatesInBackground()
        {
            try
            {
                // Wait a bit after startup to not interfere with initial load
                await Task.Delay(3000);

                var updateService = new Features.Services.UpdateService();
                var updateInfo = await updateService.CheckForUpdatesAsync();

                if (updateInfo != null && MainWindow != null)
                {
                    // Show update notification on the main window
                    var dialog = new Features.Dialogs.UpdateAvailableDialog(updateInfo)
                    {
                        XamlRoot = MainWindow.Content.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
            }
            catch
            {
                // Silently fail - don't interrupt user experience
            }
        }

        private void InitializeDefaultSettings()
        {
            var settingsService = new Features.Alerts.Services.SettingsService();
            
            // Initialize DM Policy ID if not set
            string dmPolicyId = settingsService.GetSetting("DMPolicyId");
            if (string.IsNullOrEmpty(dmPolicyId))
            {
                settingsService.SetSetting("DMPolicyId", "6708037");
            }
        }

        public static Window? MainWindow { get; set; }
    }
}
