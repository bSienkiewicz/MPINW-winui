using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SupportTool.Models;
using SupportTool.Services;
using System.Threading.Tasks;
using System.Threading;
using SupportTool.Helpers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Linq;
using System.Globalization;
using SupportTool.CustomControls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SupportTool.Dialogs
{
    public sealed partial class AlertDetailsDialog : Page, INotifyPropertyChanged
    {
        public event Action? AlertAdded;
        public string AppName { get; }
        public string CarrierName { get; }
        public bool HasPrintDurationAlert { get; }
        public bool HasErrorRateAlert { get; }

        public NrqlAlert NewAlertData { get; set; } = new NrqlAlert();
        public string[] Severities => AlertConstants.Severities;
        public string[] AggregationMethods => AlertConstants.AggregationMethods;
        public string[] CriticalOperators => AlertConstants.CriticalOperators;
        public string[] ThresholdOccurrences => AlertConstants.ThresholdOccurrences;


        private readonly ContentDialog _dialog;
        private readonly AlertService _alertService;
        private readonly NewRelicApiService _newRelicApiService = new();
        private CancellationTokenSource _cancellationTokenSource;
        private readonly string _selectedStack; 
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

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
                Width = 1000,
                CloseButtonText = "Close",
                PrimaryButtonText = "Save",
                DefaultButton = ContentDialogButton.Primary
            };

            _dialog.PrimaryButtonClick += SaveButton_Click;
        }

        public async Task ShowAsync()
        {
            await _dialog.ShowAsync();
        }

        private void ApplyPrintDurationTemplate_Click(object sender, RoutedEventArgs e)
        {
            NewAlertData = AlertTemplates.GetTemplate("PrintDuration", AppName, CarrierName);

            foreach (var property in NewAlertData.GetType().GetProperties())
            {
                var value = property.GetValue(NewAlertData);
                Debug.WriteLine($"Key: {property.Name}, Value: {value}, Type: {value?.GetType()}");
            }

            OnPropertyChanged(nameof(NewAlertData));
        }

        private void ApplyErrorRateTemplate_Click(object sender, RoutedEventArgs e)
        {
            NewAlertData = AlertTemplates.GetTemplate("ErrorRate", AppName, CarrierName);
            OnPropertyChanged(nameof(NewAlertData));
        }

        private async void FetchMetricsButton_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();

            FetchMetricsButton.Visibility = Visibility.Collapsed;
            MetricsFetchProgress.Visibility = Visibility.Visible;
            NRMetricsResult metrics = await _newRelicApiService.FetchMetricsForAppNameAndCarrier(AppName, CarrierName, _cancellationTokenSource.Token);
            double roundedDuration = Math.Round(metrics.MedianDuration, 3);
            double calls = metrics.CreateCalls;
            double carrierPercentage = metrics.CarrierPercentage;

            string metricsText = $"{calls} calls\n{Math.Round(carrierPercentage, 1)}% of all carrier calls.\nMedian call duration - {roundedDuration}s";

            NRMetrics.Text = metricsText.ToString(CultureInfo.InvariantCulture);
            MetricsFetchProgress.Visibility = Visibility.Collapsed;
        }

        private void SaveButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            try
            {
                var alerts = _alertService.GetAlertsForStack(_selectedStack);
                var errors = _alertService.ValidateAlertInputs(NewAlertData, alerts, checkForDuplicates: true);
                if (errors.Count > 0)
                {
                    var errorMessage = string.Join("\n", errors);
                    var toast = new CustomToast();
                    ToastContainer.Children.Add(toast);
                    toast.ShowToast("Validation error", errorMessage, InfoBarSeverity.Error, 10);
                    args.Cancel = true;
                    return;
                }

                if (!alerts.Contains(NewAlertData))
                {
                    alerts.Add(NewAlertData);
                    _alertService.SaveAlertsToFile(_selectedStack, alerts);
                    AlertAdded?.Invoke();

                    _dialog.Hide();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);

                args.Cancel = true;
            }
        }
    }
}
