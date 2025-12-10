using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SupportTool.Features.Alerts.CustomControls
{
    public sealed partial class CustomToast : Microsoft.UI.Xaml.Controls.UserControl
    {
        public CustomToast()
        {
            this.InitializeComponent();
        }

        public void ShowToast(string title, string message, InfoBarSeverity severity, int durationInSeconds)
        {
            ToastInfoBar.Title = title;
            ToastInfoBar.Message = message;
            ToastInfoBar.Severity = severity;
            
            // Explicitly set blue background for Informational severity
            if (severity == InfoBarSeverity.Informational)
            {
                // Use system accent color or fallback to a standard blue
                var blueBrush = new SolidColorBrush(Microsoft.UI.Colors.RoyalBlue);
                ToastInfoBar.Background = blueBrush;
            }
            else
            {
                // Clear background for other severities to use default styling
                ToastInfoBar.ClearValue(InfoBar.BackgroundProperty);
            }

            ShowStoryboard.Begin();

            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(durationInSeconds);
            timer.Tick += (s, e) =>
            {
                ToastInfoBar.IsOpen = false;
                (this.Parent as Panel)?.Children.Remove(this);
                timer.Stop();
            };
            timer.Start();
        }

    }
}
