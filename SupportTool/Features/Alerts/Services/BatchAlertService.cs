using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SupportTool.Features.Alerts.Helpers;
using SupportTool.Features.Alerts.Models;
using Microsoft.UI.Xaml.Controls;
using SupportTool.Features.Alerts.CustomControls;
using System.Diagnostics;

namespace SupportTool.Features.Alerts.Services
{
    /// <summary>
    /// Service for batch alert operations, extracting common logic
    /// </summary>
    public class BatchAlertService
    {
        private readonly AlertService _alertService;
        private readonly NewRelicApiService _newRelicApiService;

        public BatchAlertService(AlertService alertService, NewRelicApiService newRelicApiService)
        {
            _alertService = alertService;
            _newRelicApiService = newRelicApiService;
        }

        /// <summary>
        /// Shows a toast notification
        /// </summary>
        public static void ShowToast(Canvas toastContainer, string title, string message, InfoBarSeverity severity, int duration = 5)
        {
            var toast = new CustomToast();
            toastContainer.Children.Add(toast);
            toast.ShowToast(title, message, severity, duration);
        }

        /// <summary>
        /// Validates prerequisites for batch operations
        /// </summary>
        public static (bool isValid, string? errorMessage) ValidateBatchPrerequisites(bool hasApiKey, string? selectedStack, int selectedCount)
        {
            if (!hasApiKey || string.IsNullOrEmpty(selectedStack))
            {
                return (false, "Please ensure API key is set and a stack is selected");
            }

            if (selectedCount == 0)
            {
                return (false, "Please select at least one item");
            }

            return (true, null);
        }

        /// <summary>
        /// Creates a success message for batch operations
        /// </summary>
        public static string CreateSuccessMessage(int addedCount, List<string> skippedItems, string itemType = "carrier")
        {
            string message = $"Added {addedCount} missing alert{(addedCount != 1 ? "s" : "")}";
            if (skippedItems.Any())
            {
                message += $". Skipped {skippedItems.Count} {itemType}(s) due to missing or invalid statistics: {string.Join(", ", skippedItems)}. Try running this batch again.";
            }
            return message;
        }

        /// <summary>
        /// Creates a warning/info message when no alerts were added
        /// </summary>
        public static (string message, InfoBarSeverity severity) CreateNoAlertsMessage(List<string> skippedItems, string itemType = "carrier")
        {
            string message = "No missing alerts to add";
            if (skippedItems.Any())
            {
                message += $". {skippedItems.Count} {itemType}(s) skipped due to missing or invalid statistics: {string.Join(", ", skippedItems)}";
                return (message, InfoBarSeverity.Warning);
            }
            return (message, InfoBarSeverity.Informational);
        }
    }
}
