using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using SupportTool.Services;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SupportTool
{
    public sealed partial class SettingsPage : Page
    {
        private readonly SettingsService _settings = new();

        public SettingsPage()
        {
            this.InitializeComponent();
            LoadApiKey();
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
                        ExpanderSettings_NRAPI.IsExpanded = true;
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

        private void DeleteConfirmation_Click(object sender, RoutedEventArgs e)
        {
            _settings.RemoveAllSettings();
        }
    }
}
