using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace App1
{
    public sealed partial class SettingsPage : Page
    {
        private readonly ApplicationDataContainer _localSettings;

        public SettingsPage()
        {
            this.InitializeComponent();
            _localSettings = ApplicationData.Current.LocalSettings;
            LoadApiKey();
        }
        
        private void LoadApiKey()
        {
            if (_localSettings.Values.TryGetValue("NR_API_Key", out var value))
            {
                NR_API_Key.Text = value?.ToString() ?? string.Empty;
            }
        }

        private void NR_API_Key_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            _localSettings.Values["NR_API_Key"] = NR_API_Key.Text;
        }
    }
}
