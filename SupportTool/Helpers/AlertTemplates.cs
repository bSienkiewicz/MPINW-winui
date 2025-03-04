using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SupportTool.Helpers
{
    public static class AlertTemplates
    {
        public static NrqlAlert PrintDurationTemplate(string appName, string carrierName)
        {
            return new NrqlAlert
            {
                Name = $"Print Duration Alert - {appName} - {carrierName}",
                Description = $"Monitors print duration for {appName} {carrierName}",
                Severity = "CRITICAL",
                NrqlQuery = $"SELECT percentile(duration, 95) FROM Print WHERE appName = '{appName}' AND carrier = '{carrierName}'",
                RunbookUrl = "https://runbook.example.com/print-duration",
                Enabled = true,
                AggregationMethod = "EVENT_FLOW",
                AggregationDelay = 120,
                CriticalOperator = "ABOVE",
                CriticalThreshold = 5000.0, // 5 seconds
                CriticalThresholdDuration = 300,
                CriticalThresholdOccurrences = "ALL"
            };
        }

        public static NrqlAlert ErrorRateTemplate(string appName, string carrierName)
        {
            return new NrqlAlert
            {
                Name = $"Error Rate Alert - {appName} - {carrierName}",
                Description = $"Monitors error rate for {appName} {carrierName}",
                Severity = "CRITICAL",
                NrqlQuery = $"SELECT percentage(count(*), WHERE error IS TRUE) FROM Transaction WHERE appName = '{appName}' AND carrier = '{carrierName}'",
                RunbookUrl = "https://runbook.example.com/error-rate",
                Enabled = true,
                AggregationMethod = "EVENT_FLOW",
                AggregationDelay = 120,
                CriticalOperator = "ABOVE",
                CriticalThreshold = 5.0, // 5% error rate
                CriticalThresholdDuration = 300,
                CriticalThresholdOccurrences = "ALL"
            };
        }
    }
}
