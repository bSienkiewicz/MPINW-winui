using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml;
using System.Threading;
using SupportTool.Services;
using System.Linq;
using Microsoft.UI.Xaml.Input;
using SupportTool.Models;
using SupportTool.Dialogs;
using SupportTool.CustomControls;
using Windows.Storage;

namespace SupportTool
{
    public sealed partial class Alerting_List : Page
    {
        public ObservableCollection<CarrierItem> Carriers { get; } = new();
        private readonly NewRelicApiService _newRelicApiService = new();
        private readonly AlertService _alertService = new();
        private readonly SettingsService _settings = new();
        private string _selectedStack = string.Empty;
        private CancellationTokenSource _cancellationTokenSource;

        public Alerting_List()
        {
            InitializeComponent();
            InitializeControls();
        }

        private void InitializeControls()
        {
            CarriersList.ItemsSource = Carriers;

            // Load available stacks into combo box
            var availableStacks = _alertService.GetAlertStacksFromDirectories();

            if (availableStacks == null || availableStacks.Length == 0)
            {
                // Show toast
                var toast = new CustomToast();
                ToastContainer.Children.Add(toast);
                toast.ShowToast("Error loading alerts", "Alert's repository location is not set properly", InfoBarSeverity.Error, 10);
                return;
            }

            stacksComboBox.ItemsSource = availableStacks;

            // Restore previously selected stack
            _selectedStack = _settings.GetSetting("SelectedStack");
            if (!string.IsNullOrEmpty(_selectedStack) && availableStacks.Contains(_selectedStack))
            {
                stacksComboBox.SelectedItem = _selectedStack;
                // Load data for the selected stack
                _ = LoadCarriersForStack(_selectedStack);
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
        }

        private bool IsApiKeyPresent()
        {
            bool hasApiKey = _settings.IsApiKeySet();
            InfoBar.IsOpen = !hasApiKey;
            return hasApiKey;
        }

        private async void StackComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                Carriers.Clear();

                // Cancel previous fetching operation
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();

                // Update selected stack
                _selectedStack = e.AddedItems[0].ToString();
                _settings.SetSetting("SelectedStack", _selectedStack);

                if (IsApiKeyPresent() && _selectedStack != null)
                {
                    await LoadCarriersForStack(_selectedStack, _cancellationTokenSource.Token);
                }
            }
        }

        private async Task LoadCarriersForStack(string stack, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(stack)) return;

            try
            {
                CarrierFetchingProgress.IsActive = true;
                CarrierFetchingProgress.Visibility = Visibility.Visible;

                var uniqueCarriers = await _newRelicApiService.FetchCarriers(stack, cancellationToken);
                var existingAlerts = _alertService.GetAlertsForStack(stack);

                Carriers.Clear();
                foreach (var carrier in uniqueCarriers)
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    var item = new CarrierItem
                    {
                        CarrierName = carrier
                    };
                    item.HasPrintDurationAlert = _alertService.HasCarrierAlert(existingAlerts, item.CarrierName, AlertType.PrintDuration);
                    item.HasErrorRateAlert = _alertService.HasCarrierAlert(existingAlerts, item.CarrierName, AlertType.ErrorRate);
                    Carriers.Add(item);
                }
            }
            catch (Exception ex)
            {
                var toast = new CustomToast();
                ToastContainer.Children.Add(toast);
                toast.ShowToast("Error loading carrier data", $"{ex.Message}", InfoBarSeverity.Error, 10);
            }
            finally
            {
                CarrierFetchingProgress.IsActive = false;
                CarrierFetchingProgress.Visibility = Visibility.Collapsed;
            }
        }


        private async void FetchNRButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsApiKeyPresent())
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();
                await LoadCarriersForStack(_selectedStack, _cancellationTokenSource.Token);
            }
        }

        private void ApiKeyWarningInfoBar_ButtonClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage), "ApiKeyTab");
        }

        private void RefreshAlertStatus()
        {
            if (string.IsNullOrEmpty(_selectedStack)) return;

            var existingAlerts = _alertService.GetAlertsForStack(_selectedStack);

            // Force UI update by re-adding items
            for (int i = 0; i < Carriers.Count; i++)
            {
                var item = Carriers[i];

                bool newPDStatus = _alertService.HasCarrierAlert(existingAlerts, item.CarrierName, AlertType.PrintDuration);
                bool newERStatus = _alertService.HasCarrierAlert(existingAlerts, item.CarrierName, AlertType.ErrorRate);

                if (item.HasPrintDurationAlert != newPDStatus || item.HasErrorRateAlert != newERStatus)
                {
                    item.HasPrintDurationAlert = newPDStatus;
                    item.HasErrorRateAlert = newERStatus;

                    // This forces the UI to recognize the change
                    Carriers[i] = item;
                }
            }
        }

        private async void CarriersList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (CarriersList.SelectedItem is not CarrierItem selectedCarrier) return;

            var dialog = new AlertDetailsDialog(selectedCarrier, _selectedStack, _alertService);
            dialog.AlertAdded += OnAlertAdded;
            await dialog.ShowAsync();

            RefreshAlertStatus();
        }

        private void OnAlertAdded()
        {
            RefreshAlertStatus();

            var toast = new CustomToast();
            ToastContainer.Children.Add(toast);
            toast.ShowToast("Success", "An alert has been added", InfoBarSeverity.Success, 5);
        }
    }
}