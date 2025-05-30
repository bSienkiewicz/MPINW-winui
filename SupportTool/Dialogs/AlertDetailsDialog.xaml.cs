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
        private CancellationTokenSource? _cancellationTokenSource; // Initialize to null for clarity
        private readonly string _selectedStack;
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
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

            // It's good practice to ensure MainWindow and its Content are not null
            if (App.MainWindow?.Content?.XamlRoot != null)
            {
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
            else
            {
                Debug.WriteLine("Error: Could not create ContentDialog, XamlRoot is null.");
                _dialog = null!;
            }
        }

        public async Task ShowAsync()
        {
            if (_dialog != null)
            {
                await _dialog.ShowAsync();
            }
            else
            {
                Debug.WriteLine("Error: Dialog cannot be shown because it was not initialized properly.");
            }
        }

        private void ApplyPrintDurationTemplate_Click(object sender, RoutedEventArgs e)
        {
            FetchAverageDuration_Button.Visibility = Visibility.Visible;

            NewAlertData = AlertTemplates.GetTemplate("PrintDuration", CarrierName, _selectedStack);

            OnPropertyChanged(nameof(NewAlertData));
        }

        private void ApplyErrorRateTemplate_Click(object sender, RoutedEventArgs e)
        {
            FetchAverageDuration_Button.Visibility = Visibility.Collapsed;
            ProposedThresholdText.Visibility = Visibility.Collapsed;

            NewAlertData = AlertTemplates.GetTemplate("ErrorRate", CarrierName, _selectedStack);
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
                if (errors.Any())
                {
                    var errorMessage = string.Join("\n", errors);
                    var toast = new CustomToast();
                    ToastContainer.Children.Add(toast);
                    toast.ShowToast("Validation error", errorMessage, InfoBarSeverity.Error, 10);
                    args.Cancel = true;
                    return;
                }

                if (!alerts.Contains(NewAlertData)) // Assuming NrqlAlert has proper equality comparison or this is intended reference check
                {
                    alerts.Add(NewAlertData);
                    _alertService.SaveAlertsToFile(_selectedStack, alerts);
                    AlertAdded?.Invoke();

                    _dialog?.Hide(); // Dialog might be null if initialization failed
                }
                else
                {
                    // Handle case where alert might be considered a duplicate (if Contains is not sufficient)
                    Debug.WriteLine("Information: Alert not added as it's considered a duplicate or already exists.");
                    var toast = new CustomToast();
                    if (ToastContainer != null)
                    {
                        ToastContainer.Children.Add(toast);
                        toast.ShowToast("Information", "This alert already exists or is a duplicate.", InfoBarSeverity.Informational, 5);
                    }
                    args.Cancel = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaveButton_Click Error: {ex}");

                var toast = new CustomToast();
                if (ToastContainer != null)
                {
                    ToastContainer.Children.Add(toast);
                    toast.ShowToast("Save Error", $"An unexpected error occurred: {ex.Message}", InfoBarSeverity.Error, 10);
                }
                args.Cancel = true;
            }
        }

        private async void FetchAverageDuration_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            FetchAverageDuration_Button.IsEnabled = false;
            FetchProgressRing.IsActive = true;
            FetchProgressRing.Visibility = Visibility.Visible;

            try
            {
                if (string.IsNullOrWhiteSpace(CarrierName))
                {
                    ProposedThresholdText.Text = "Error: Carrier name is not set.";
                    return;
                }

                CriticalThresholdNumberBox.Focus(FocusState.Programmatic);

                // Fetch statistics
                CarrierDurationStatistics stats = await _newRelicApiService.FetchDurationStatisticsForCarrierAsync(CarrierName, _cancellationTokenSource.Token);


                if (!stats.HasData)
                {
                    Debug.WriteLine(stats.ToString());
                    ProposedThresholdText.Text = $"No performance data found for {CarrierName}. Cannot propose threshold.";
                    return;
                }

                // Use the centralized calculation logic
                double proposedDuration = AlertService.CalculateSuggestedThreshold(stats);
                
                string metricsText = $"Avg: {stats.AverageDuration:F2}s, StdDev: {stats.StandardDeviation:F2}s.\nProposed threshold: {proposedDuration:F2}s";
                ProposedThresholdText.Text = metricsText;
                ProposedThresholdText.Tag = proposedDuration;
                ProposedThresholdText.Visibility = Visibility.Visible;

                ProposedThresholdText.IsTabStop = true;
                ToolTipService.SetToolTip(ProposedThresholdText, "Click to use this value as threshold.");
            }
            catch (OperationCanceledException)
            {
                ProposedThresholdText.Text = "Operation cancelled.";
            }
            catch (Exception ex)
            {
                ProposedThresholdText.Text = $"Error: {ex.Message}";
                Debug.WriteLine($"FetchAverageDuration_Click Error: {ex}");
            }
            finally
            {
                FetchAverageDuration_Button.IsEnabled = true;
                FetchProgressRing.IsActive = false;
                FetchProgressRing.Visibility = Visibility.Collapsed;
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
                CriticalThresholdNumberBox.Focus(FocusState.Programmatic);
            }
        }
    }
}
