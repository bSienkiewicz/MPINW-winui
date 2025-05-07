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
using Microsoft.UI.Xaml.Media;
using static System.Runtime.InteropServices.JavaScript.JSType;

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

        public AlertDetailsDialog(CarrierItem item, string selectedStack, AlertService alertService)
        {
            InitializeComponent();

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
            FetchAverageDuration_Button.Visibility = Visibility.Visible;

            NewAlertData = AlertTemplates.GetTemplate("PrintDuration", CarrierName);

            foreach (var property in NewAlertData.GetType().GetProperties())
            {
                var value = property.GetValue(NewAlertData);
            }

            OnPropertyChanged(nameof(NewAlertData));
        }

        private void ApplyErrorRateTemplate_Click(object sender, RoutedEventArgs e)
        {
            FetchAverageDuration_Button.Visibility = Visibility.Collapsed;
            ProposedThresholdText.Visibility = Visibility.Collapsed;

            NewAlertData = AlertTemplates.GetTemplate("ErrorRate", CarrierName);
            OnPropertyChanged(nameof(NewAlertData));
        }

        //private async void FetchMetricsButton_Click(object sender, RoutedEventArgs e)
        //{
        //    _cancellationTokenSource?.Cancel();
        //    _cancellationTokenSource = new CancellationTokenSource();

        //    FetchMetricsButton.Visibility = Visibility.Collapsed;
        //    MetricsFetchProgress.Visibility = Visibility.Visible;
        //    NRMetricsResult metrics = await _newRelicApiService.FetchMetricsForAppNameAndCarrier(AppName, CarrierName, _cancellationTokenSource.Token);
        //    double roundedDuration = Math.Round(metrics.MedianDuration, 3);
        //    double calls = metrics.CreateCalls;
        //    double carrierPercentage = metrics.CarrierPercentage;

        //    string metricsText = $"{calls} calls\n{Math.Round(carrierPercentage, 1)}% of all carrier calls.\nMedian call duration - {roundedDuration}s";

        //    NRMetrics.Text = metricsText.ToString(CultureInfo.InvariantCulture);
        //    MetricsFetchProgress.Visibility = Visibility.Collapsed;
        //}

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

        private async void FetchAverageDuration_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            FetchAverageDuration_Button.IsEnabled = false;
            ProposedThresholdText.Visibility = Visibility.Visible;
            ProposedThresholdText.Text = "Fetching data...";

            try
            {
                float duration = await _newRelicApiService.FetchDurationForCarrier(CarrierName, _cancellationTokenSource.Token);

                double proposedDuration = Math.Round((duration * 1.5 + 3) * 2.0) / 2.0;

                string metricsText = $"Median call duration - {duration:F2}s. Proposed threshold - {proposedDuration:F2}";
                ProposedThresholdText.Text = metricsText;

                // Store proposed value for later use
                ProposedThresholdText.Tag = proposedDuration;

                // Make ProposedThresholdText clickable and add tooltip
                ProposedThresholdText.IsTabStop = true;
                ToolTipService.SetToolTip(ProposedThresholdText, "Click to use this value as threshold");
            }
            catch (Exception ex)
            {
                ProposedThresholdText.Text = $"Error: {ex.Message}";
            }
            finally
            {
                FetchAverageDuration_Button.IsEnabled = true;
            }
        }

        private void ProposedThresholdText_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            // Get the proposed value from the Tag property
            if (ProposedThresholdText.Tag is double proposedValue)
            {
                // Set the value to the NumberBox
                NewAlertData.CriticalThreshold = proposedValue;
                OnPropertyChanged(nameof(NewAlertData));
            }
        }
    }
}
