using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SupportTool.Helpers;
using SupportTool.Models;
using Windows.Storage;

namespace SupportTool.Services
{
    public class AlertService
    {
        private const string StacksPath = "metaform\\mpm\\copies\\production\\prd\\eu-west-1";
        private readonly string[] _requiredFolders = [".github", "ansible", "metaform", "terraform"];
        private readonly SettingsService _settings = new();

        public AlertService()
        {
        }

        public string RepositoryPath
        {
            get => _settings.GetSetting("NRAlertsDir");
            private set => _settings.SetSetting("NRAlertsDir", value);
        }

        public string SelectedStack
        {
            get => _settings.GetSetting("SelectedStack");
            set => _settings.SetSetting("SelectedStack", value);
        }

        public List<NrqlAlert> GetAlertsForStack(string stackName)
        {
            var tfvarsPath = Path.Combine(RepositoryPath, StacksPath, stackName, "auto.tfvars");
            if (!File.Exists(tfvarsPath))
                return [];

            var parser = new HclParser();
            return parser.ParseAlerts(File.ReadAllText(tfvarsPath));
        }

        public void SaveAlertsToFile(string stackName, List<NrqlAlert> alerts)
        {
            try
            {
                var filePath = Path.Combine(RepositoryPath, StacksPath, stackName, "auto.tfvars");
                var parser = new HclParser();
                var updatedContent = parser.ReplaceNrqlAlertsSection(
                    File.ReadAllText(filePath),
                    alerts);
                File.WriteAllText(filePath, updatedContent);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        public bool ValidateRepository(string folderPath, out string[] missingFolders)
        {
            try
            {
                var existingFolders = Directory.GetDirectories(folderPath)
                    .Select(path => new DirectoryInfo(path).Name)
                    .ToArray();

                missingFolders = _requiredFolders.Except(existingFolders).ToArray();
                return !missingFolders.Any();
            }
            catch (Exception)
            {
                missingFolders = _requiredFolders;
                return false;
            }
        }

        public List<string> ValidateAlertInputs(NrqlAlert alert)
        {
            var errors = new List<string>();

            if (alert == null)
            {
                errors.Add("Alert cannot be null.");
                return errors;
            }

            // Required fields with character validation
            var fieldsToValidate = new Dictionary<string, string>
                {
                    { "Name", alert.Name },
                    { "NrqlQuery", alert.NrqlQuery },
                    { "Severity", alert.Severity },
                    { "AggregationMethod", alert.AggregationMethod },
                    { "CriticalOperator", alert.CriticalOperator },
                    { "CriticalThresholdOccurrences", alert.CriticalThresholdOccurrences }
                };

            foreach (var field in fieldsToValidate)
            {
                if (string.IsNullOrWhiteSpace(field.Value))
                    errors.Add($"{field.Key} cannot be empty.");
                else if (ContainsInvalidCharacters(field.Value))
                    errors.Add($"{field.Key} contains invalid characters.");
            }

            // Name and NRQL Query length validation
            if (!string.IsNullOrWhiteSpace(alert.Name) && alert.Name.Length < 10)
                errors.Add("Name must be at least 10 characters long.");

            if (!string.IsNullOrWhiteSpace(alert.NrqlQuery) && alert.NrqlQuery.Length < 10)
                errors.Add("NRQL Query must be at least 10 characters long.");

            // Numeric field validations
            if (alert.CriticalThreshold.HasValue)
            {
                if (!double.TryParse(alert.CriticalThreshold.ToString(), out double criticalThreshold))
                    errors.Add("Critical Threshold must be a valid number.");
                else if (criticalThreshold < 0)
                    errors.Add("Critical Threshold must be a non-negative number.");
            }

            if (alert.CriticalThresholdDuration.HasValue)
            {
                if (!int.TryParse(alert.CriticalThresholdDuration.ToString(), out int criticalThresholdDuration))
                    errors.Add("Critical Threshold Duration must be a valid integer.");
                else if (criticalThresholdDuration < 0)
                    errors.Add("Critical Threshold Duration must be a non-negative integer.");
            }

            if (alert.AggregationDelay.HasValue)
            {
                if (!int.TryParse(alert.AggregationDelay.ToString(), out int aggregationDelay))
                    errors.Add("Aggregation Delay must be a valid integer.");
                else if (aggregationDelay < 0)
                    errors.Add("Aggregation Delay must be a non-negative integer.");
            }

            return errors;
        }

        private bool ContainsInvalidCharacters(string input)
        {
            if (input == null || input == string.Empty)
                return false;
            // List of invalid characters
            char[] invalidChars = {'[', ']', '{', '}' };

            // Check if the input contains any invalid characters
            return input.IndexOfAny(invalidChars) >= 0;
        }

        public string[] GetAlertStacksFromDirectories()
        {
            if (string.IsNullOrEmpty(RepositoryPath)) return [];

            var path = Path.Combine(RepositoryPath, StacksPath);
            return !Directory.Exists(path)
                ? []
                : Directory.GetDirectories(path)
                    .Select(dir => new DirectoryInfo(dir).Name)
                    .ToArray();
        }


        public bool HasAlert(List<NrqlAlert> alerts, AppCarrierItem item, AlertType alertType)
        {
            return alertType switch
            {
                AlertType.PrintDuration => alerts.Any(alert =>
                    alert.NrqlQuery.Contains($"appName = '{item.AppName}'") &&
                    alert.NrqlQuery.Contains($"CarrierName = '{item.CarrierName}'") &&
                    alert.NrqlQuery.Contains($"average(duration)")),

                AlertType.ErrorRate => alerts.Any(alert =>
                    alert.NrqlQuery.Contains($"appName = '{item.AppName}'") &&
                    alert.NrqlQuery.Contains("SELECT filter(count(*), WHERE ExitStatus = 'Error')/ count(*) * 100") &&
                    alert.NrqlQuery.Contains($"CarrierName = '{item.CarrierName}'")),

                _ => false
            };
        }

        public NrqlAlert CreateMissingAlertByType(AppCarrierItem item, AlertType alertType)
        {
            return alertType switch
            {
                AlertType.PrintDuration => new NrqlAlert
                {
                    Name = $"{item.ClientName} Print duration for {item.CarrierName}",
                    Description = $"Alert related to increased {item.CarrierName} print duration for {item.AppName}",
                    NrqlQuery = $"SELECT average(duration) FROM Transaction WHERE name like '%PrintParcel' and appName = '{item.AppName}' where UserName != 'mpmwarmup' and CarrierName = '{item.CarrierName}'",
                    Severity = "CRITICAL",
                    Enabled = true,
                    AggregationMethod = "event_flow",
                    CriticalThresholdOccurrences = "ALL",
                    CriticalThresholdDuration = 300,
                    CriticalThreshold = 7,
                    CriticalOperator = "ABOVE",
                    AggregationDelay = 120,
                },

                AlertType.ErrorRate => new NrqlAlert
                {
                    Name = $"{item.ClientName} Error rate for {item.CarrierName}",
                    Description = $"Alert related to {item.CarrierName} error rate for {item.AppName}",
                    NrqlQuery = $"SELECT filter(count(*), WHERE ExitStatus = 'Error')/ count(*) * 100 FROM Transaction WHERE appName = '{item.AppName}' AND name not like '%.PrintParcel'",
                    Severity = "CRITICAL",
                    Enabled = true,
                    CriticalThresholdOccurrences = "ALL",
                    CriticalThresholdDuration = 600,
                    CriticalThreshold = 1,
                    CriticalOperator = "ABOVE",
                    AggregationDelay = 0,
                },

                _ => throw new ArgumentException("Invalid alert type")
            };
        }

        public NrqlAlert CloneAlert(NrqlAlert alert) => new()
        {
            Name = $"{alert.Name} Copy",
            Description = alert.Description,
            NrqlQuery = alert.NrqlQuery,
            RunbookUrl = alert.RunbookUrl,
            Severity = alert.Severity,
            Enabled = alert.Enabled,
            AggregationMethod = alert.AggregationMethod,
            AggregationWindow = alert.AggregationWindow,
            AggregationDelay = alert.AggregationDelay,
            CriticalOperator = alert.CriticalOperator,
            CriticalThreshold = alert.CriticalThreshold,
            CriticalThresholdDuration = alert.CriticalThresholdDuration,
            CriticalThresholdOccurrences = alert.CriticalThresholdOccurrences
        };
    }
}