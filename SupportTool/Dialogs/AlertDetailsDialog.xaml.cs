using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SupportTool.Models;
using SupportTool.Services;
using System.Threading.Tasks;
using System.Diagnostics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SupportTool.Dialogs
{
    public sealed partial class AlertDetailsDialog : Page
    {
        public string AppName { get; }
        public string CarrierName { get; }
        public bool HasPrintDurationAlert { get; }
        public bool HasErrorRateAlert { get; }
        public Visibility ShowCreatePrintDurationButton => HasPrintDurationAlert ? Visibility.Collapsed : Visibility.Visible;
        public Visibility ShowCreateErrorRateButton => HasErrorRateAlert ? Visibility.Collapsed : Visibility.Visible;

        public event Action ClosedEvent;
        public event Action ErrorEvent;
        public event Action AlertCreatedEvent;

        private readonly ContentDialog _dialog;
        private readonly AlertService _alertService;
        private readonly string _selectedStack;

        public AlertDetailsDialog(AppCarrierItem item, string selectedStack, AlertService alertService)
        {
            InitializeComponent();

            AppName = item.AppName;
            CarrierName = item.CarrierName;
            HasPrintDurationAlert = item.HasPrintDurationAlert;
            HasErrorRateAlert = item.HasErrorRateAlert;
            _selectedStack = selectedStack;
            _alertService = alertService;

            _dialog = new ContentDialog
            {
                Content = this,
                XamlRoot = App.MainWindow.Content.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                DefaultButton = ContentDialogButton.Close,
                CloseButtonText = "Close"
            };
        }

        public async Task ShowAsync()
        {
            await _dialog.ShowAsync();
        }

        private void CreatePrintDurationAlert_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = (Button)sender;
                // New AppCarrier combination
                var item = new AppCarrierItem { AppName = AppName, CarrierName = CarrierName };
                var alerts = _alertService.GetAlertsForStack(_selectedStack);

                // Add the new generated alert to the alert list and save it to file
                alerts.Add(_alertService.CreateMissingAlertByType(item, AlertType.PrintDuration));
                _alertService.SaveAlertsToFile(_selectedStack, alerts);
                button.IsEnabled = false;
                if (PrintDurationStatusText != null)
                {
                    PrintDurationStatusText.Text = "Alert exists";
                }

            }
            catch (Exception ex)
            {
                ErrorEvent?.Invoke();
                _dialog.Hide();
            }
        }

        private void CreateErrorRateAlert_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var item = new AppCarrierItem { AppName = AppName, CarrierName = CarrierName };
                var alerts = _alertService.GetAlertsForStack(_selectedStack);
                alerts.Add(_alertService.CreateMissingAlertByType(item, AlertType.ErrorRate));
                _alertService.SaveAlertsToFile(_selectedStack, alerts);
            }
            catch (Exception ex)
            {
                ErrorEvent?.Invoke();
                _dialog.Hide();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _dialog.Hide();
        }
    }
}
