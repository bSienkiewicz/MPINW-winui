using SupportTool.Models;

namespace SupportTool.Helpers
{
    public static class AlertTemplates
    {
        public static NrqlAlert PrintDurationTemplate(string appName, string carrierName)
        {
            return new NrqlAlert
            {
                Name = $"{appName.Split('.')[0].ToUpper()} PrintParcel duration - {carrierName}",
                Description = $"PrintParcel duration for {appName.Split('.')[0].ToUpper()} ({carrierName})",
                Severity = "CRITICAL",
                NrqlQuery = $"SELECT average(duration) FROM Transaction where appName = '{appName}' and name = 'WebTransaction/WCF/XLogics.BlackBox.ServiceContracts.IBlackBoxContract.PrintParcel' and PrintOperation like '%Create%' and CarrierName = '{carrierName}'",
                RunbookUrl = "https://runbook.example.com/print-duration",
                Enabled = true,
                AggregationMethod = "EVENT_FLOW",
                AggregationDelay = 120,
                CriticalOperator = "ABOVE",
                CriticalThreshold = 0.5,
                CriticalThresholdDuration = 600,
                CriticalThresholdOccurrences = "ALL"
            };
        }

        public static NrqlAlert ErrorRateTemplate(string appName, string carrierName)
        {
            return new NrqlAlert
            { 
                Name = $"{appName.Split('.')[0].ToUpper()} Error rate - {carrierName}",
                Description = $"Error rate for {appName.Split('.')[0].ToUpper()} ({carrierName})",
                Severity = "CRITICAL",
                NrqlQuery = $"SELECT filter(count(*), WHERE ExitStatus = 'Error')/ count(*) * 100 FROM Transaction WHERE appName = '{appName}' AND name not like '%.PrintParcel'",
                RunbookUrl = "https://runbook.example.com/error-rate",
                Enabled = true,
                AggregationMethod = "EVENT_FLOW",
                AggregationDelay = 120,
                CriticalOperator = "ABOVE",
                CriticalThreshold = 5, // 5% error rate
                CriticalThresholdDuration = 300,
                CriticalThresholdOccurrences = "ALL"
            };
        }
    }
}
