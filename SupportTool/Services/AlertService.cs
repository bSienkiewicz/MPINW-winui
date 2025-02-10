using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SupportTool.Helpers;

namespace SupportTool.Services
{
    public class AlertService
    {
        private const string StacksPath = "metaform\\mpm\\copies\\production\\prd\\eu-west-1";

        public AlertService() { }

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
                Name = $"{appName.Split(".")[0].ToUpper()} Print duration for {carrierName}",
                Description = $"Alert related to increased {carrierName} print duration for {appName}",
                NrqlQuery = $"SELECT average(duration) FROM Transaction WHERE name like '%PrintParcel' and appName = '${appName}' where UserName != 'mpmwarmup' and CarrierName = '{carrierName}'",
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
        public NrqlAlert CreateErrorRateAlert(string appName, string carrierName)
        {
            return new NrqlAlert
            {
                Name = $"{appName.Split(".")[0].ToUpper()} Error rate for {carrierName}",
                Description = $"Alert related to {carrierName} error rate for {appName}",
                NrqlQuery = $"SELECT filter(count(*), WHERE ExitStatus = 'Error')/ count(*) *100 FROM Transaction WHERE appName like 'pvh.mpm.metapack.%' AND name not like '%.PrintParcel'",
                RunbookUrl = "",
                Severity = "CRITICAL",
                Enabled = true,
                CriticalThresholdOccurrences = "ALL",
                CriticalThresholdDuration = 600,
                CriticalThreshold = 1,
                CriticalOperator = "ABOVE",
                AggregationDelay = 0,
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