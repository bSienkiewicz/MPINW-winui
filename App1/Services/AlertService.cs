using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using App1.Helpers;

namespace App1.Services
{
    public class AlertService
    {
        private static AlertService _instance;
        public static AlertService Instance => _instance ??= new AlertService();

        private const string StacksPath = "metaform\\mpm\\copies\\production\\prd\\eu-west-1";

        private AlertService() { }

        public List<NrqlAlert> GetAlertsForStack(string repositoryPath, string stackName)
        {
            var tfvarsPath = Path.Combine(repositoryPath, StacksPath, stackName, "auto.tfvars");
            if (!File.Exists(tfvarsPath))
                return new List<NrqlAlert>();

            var tfvarsContent = File.ReadAllText(tfvarsPath);
            var parser = new HclParser();
            return parser.ParseAlerts(tfvarsContent);
        }

        public bool AlertExistsForCarrier(List<NrqlAlert> alerts, string appName, string carrierName)
        {
            return alerts.Any(alert =>
                alert.NrqlQuery.Contains($"appName = '{appName}") &&
                alert.NrqlQuery.Contains($"CarrierName = '{carrierName}'") &&
                alert.NrqlQuery.Contains("PrintOperation like '%create%'"));
        }

        public NrqlAlert CreatePrintDurationAlert(string appName, string carrierName)
        {
            return new NrqlAlert
            {
                Name = $"{appName.Split(".")[0]} Print duration for {carrierName}",
                Description = $"Alert related to increased {carrierName} print duration",
                NrqlQuery = $"SELECT average(duration) from Transaction where appName = '{appName}.mpm.metapack.com_BlackBox' and CarrierName = '{carrierName}' and PrintOperation like '%create%' FACET BusinessUnit",
                RunbookUrl = "",
                Severity = "CRITICAL",
                Enabled = true,
                AggregationMethod = "event_flow",
                CriticalThresholdOccurrences = "ALL",
                CriticalThresholdDuration = 300,
                CriticalThreshold = 7,
                CriticalOperator = "ABOVE",
                AggregationDelay = 120,
            };
        }

        public void SaveAlertsToFile(string repositoryPath, string stackName, List<NrqlAlert> alerts)
        {
            var filePath = Path.Combine(repositoryPath, StacksPath, stackName, "auto.tfvars");
            var originalContent = File.ReadAllText(filePath);
            var parser = new HclParser();
            var updatedContent = parser.ReplaceNrqlAlertsSection(originalContent, alerts);
            File.WriteAllText(filePath, updatedContent);
        }
    }
}