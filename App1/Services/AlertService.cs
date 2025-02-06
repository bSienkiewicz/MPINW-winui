using System.Collections.Generic;
using System.IO;
using System.Linq;
using App1.Helpers;

namespace App1.Services
{
    public class AlertService
    {
        private const string StacksPath = "metaform\\mpm\\copies\\production\\prd\\eu-west-1";

        public List<NrqlAlert> LoadAlertsForStack(string folderPath, string stackName)
        {
            var tfvarsPath = Path.Combine(folderPath, StacksPath, stackName, "auto.tfvars");
            var tfvarsContent = File.ReadAllText(tfvarsPath);
            var parser = new HclParser();
            return parser.ParseAlerts(tfvarsContent);
        }

        public void SaveAlertsToFile(string folderPath, string stackName, List<NrqlAlert> alerts)
        {
            var filePath = Path.Combine(folderPath, StacksPath, stackName, "auto.tfvars");
            var originalContent = File.ReadAllText(filePath);
            var parser = new HclParser();
            var updatedContent = parser.ReplaceNrqlAlertsSection(originalContent, alerts);
            File.WriteAllText(filePath, updatedContent);
        }

        public bool AlertExists(List<NrqlAlert> alerts, string appName, string carrierName)
        {
            return alerts.Any(alert =>
                alert.Name.Contains(carrierName) &&
                alert.NrqlQuery.Contains(appName));
        }

        public void AddAlert(List<NrqlAlert> alerts, string appName, string carrierName)
        {
            var newAlert = new NrqlAlert
            {
                Name = $"Print duration for {carrierName}",
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
            alerts.Add(newAlert);
        }
    }
}