using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SupportTool.Helpers;
using SupportTool.Models;
using Windows.Storage;

namespace SupportTool.Services
{
    public class AlertService
    {
        private const string StacksPath = "metaform\\mpm\\copies\\production\\prd\\eu-west-1";
        private readonly string[] _requiredFolders = [".github", "ansible", "metaform", "terraform"];
        private readonly ApplicationDataContainer _localSettings;
        private readonly string _repositoryPath;
        private string? _selectedFolderPath;
        private string[] _availableStacks = [];

        public AlertService()
        {
            _localSettings = ApplicationData.Current.LocalSettings;
        }

        public string RepositoryPath
        {
            get => _localSettings.Values["NRAlertsDir"] as string ?? string.Empty;
            private set => _localSettings.Values["NRAlertsDir"] = value;
        }

        public string SelectedStack
        {
            get => _localSettings.Values["SelectedStack"] as string ?? string.Empty;
            set => _localSettings.Values["SelectedStack"] = value;
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

        public void SetRepository(string path)
        {
            if (ValidateRepository(path, out _))
            {
                RepositoryPath = path;
            }
            else
            {
                RepositoryPath = string.Empty;
                throw new InvalidOperationException("Invalid repository structure");
            }
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

        public List<NrqlAlert> GetAlertsForStack(string stackName)
        {
            var tfvarsPath = Path.Combine(RepositoryPath, StacksPath, stackName, "auto.tfvars");
            if (!File.Exists(tfvarsPath))
                return [];

            var parser = new HclParser();
            return parser.ParseAlerts(File.ReadAllText(tfvarsPath));
        }

        public bool HasAlert(List<NrqlAlert> alerts, AppCarrierItem item, AlertType alertType)
        {
            return alertType switch
            {
                AlertType.PrintDuration => alerts.Any(alert =>
                    alert.NrqlQuery.Contains($"appName = '{item.AppName}'") &&
                    alert.NrqlQuery.Contains($"CarrierName = '{item.CarrierName}'") &&
                    alert.NrqlQuery.Contains("PrintOperation like '%create%'")),

                AlertType.ErrorRate => alerts.Any(alert =>
                    alert.NrqlQuery.Contains("WebTransaction/WCF/XLogics.BlackBox.ServiceContracts.IBlackBoxContract.PrintParcel") &&
                    alert.Name.ToLower().Contains(item.ClientName.ToLower())),

                _ => false
            };
        }

        public bool AlertExistsForCarrier(List<NrqlAlert> alerts, string appName, string carrierName)
        {
            return alerts.Any(alert =>
                alert.NrqlQuery.Contains($"appName = '{appName}") &&
                alert.NrqlQuery.Contains($"CarrierName = '{carrierName}'") &&
                alert.NrqlQuery.Contains("PrintOperation like '%create%'"));
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
                    NrqlQuery = $"SELECT filter(count(*), WHERE ExitStatus = 'Error')/ count(*) *100 FROM Transaction WHERE appName like 'pvh.mpm.metapack.%' AND name not like '%.PrintParcel'",
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

        public void SaveAlertsToFile(string stackName, List<NrqlAlert> alerts)
        {
            var filePath = Path.Combine(RepositoryPath, StacksPath, stackName, "auto.tfvars");
            var parser = new HclParser();
            var updatedContent = parser.ReplaceNrqlAlertsSection(
                File.ReadAllText(filePath),
                alerts);
            File.WriteAllText(filePath, updatedContent);
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

        public NrqlAlert CreateEmptyAlert() => new()
        {
            Name = "New Alert",
            Description = "Alert description",
            NrqlQuery = string.Empty,
            RunbookUrl = string.Empty,
            Severity = "CRITICAL",
            Enabled = true,
            AggregationMethod = "event_flow"
        };
    }
}