using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace App1.Helpers
{
    public class NrqlAlert
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string NrqlQuery { get; set; }
        public string RunbookUrl { get; set; }
        public string Severity { get; set; }
        public bool Enabled { get; set; }
        public string AggregationMethod { get; set; }
        public int AggregationWindow { get; set; }
        public int AggregationDelay { get; set; }
        public string CriticalOperator { get; set; }
        public double CriticalThreshold { get; set; }
        public int CriticalThresholdDuration { get; set; }
        public string CriticalThresholdOccurrences { get; set; }

        public override string ToString()
        {
            return $"Alert: {Name} - {Severity}";
        }
    }

    public class HclParser
    {
        public List<NrqlAlert> ParseAlerts(string content)
        {
            var alerts = new List<NrqlAlert>();

            // Extract everything between the square brackets after nr_nrql_alerts =
            var arrayMatch = Regex.Match(content, @"nr_nrql_alerts\s*=\s*\[(.*)\]", RegexOptions.Singleline);
            if (!arrayMatch.Success)
            {
                return alerts;
            }

            string alertsContent = arrayMatch.Groups[1].Value;

            // Find all alert blocks including those with preceding comments
            var blockPattern = @"(?:#[^\n]*\n)*\s*\{[^{]*?""name"".*?\}";
            var alertBlocks = Regex.Matches(alertsContent, blockPattern, RegexOptions.Singleline);

            foreach (Match match in alertBlocks)
            {
                var block = match.Value;

                // Extract just the JSON-like portion between curly braces
                var jsonBlock = Regex.Match(block, @"\{.*\}", RegexOptions.Singleline);
                if (!jsonBlock.Success) continue;

                var cleanBlock = jsonBlock.Value
                    .Trim()
                    .TrimStart('{')
                    .TrimEnd('}')
                    .TrimEnd(',');

                if (string.IsNullOrWhiteSpace(cleanBlock))
                    continue;

                var alert = new NrqlAlert
                {
                    Name = ParseStringValue(cleanBlock, "name"),
                    Description = ParseStringValue(cleanBlock, "description"),
                    NrqlQuery = ParseStringValue(cleanBlock, "nrql_query"),
                    RunbookUrl = ParseStringValue(cleanBlock, "runbook_url"),
                    Severity = ParseStringValue(cleanBlock, "severity"),
                    Enabled = ParseBoolValue(cleanBlock, "enabled"),
                    AggregationMethod = ParseStringValue(cleanBlock, "aggregation_method"),
                    AggregationWindow = ParseIntValue(cleanBlock, "aggregation_window"),
                    AggregationDelay = ParseIntValue(cleanBlock, "aggregation_delay"),
                    CriticalOperator = ParseStringValue(cleanBlock, "critical_operator"),
                    CriticalThreshold = ParseDoubleValue(cleanBlock, "critical_threshold"),
                    CriticalThresholdDuration = ParseIntValue(cleanBlock, "critical_threshold_duration"),
                    CriticalThresholdOccurrences = ParseStringValue(cleanBlock, "critical_threshold_occurrences")
                };

                if (!string.IsNullOrWhiteSpace(alert.Name))
                {
                    alerts.Add(alert);
                }
            }

            return alerts;
        }

        private string ParseStringValue(string block, string key)
        {
            var pattern = $@"""{key}""\s*=\s*""([^""]*)""|""{key}""\s*=\s*([^,\r\n#]*?)(?=\s*(?:$|,|\r|\n|#))";
            var match = Regex.Match(block, pattern);

            if (!match.Success) return string.Empty;

            return (match.Groups[1].Value + match.Groups[2].Value).Trim().Trim('"');
        }

        private bool ParseBoolValue(string block, string key)
        {
            var value = ParseStringValue(block, key).ToLower();
            return value == "true";
        }

        private int ParseIntValue(string block, string key)
        {
            var value = ParseStringValue(block, key);
            return int.TryParse(value, out int result) ? result : 0;
        }

        private double ParseDoubleValue(string block, string key)
        {
            var value = ParseStringValue(block, key);
            return double.TryParse(value, out double result) ? result : 0.0;
        }

        public string SerializeAlerts(List<NrqlAlert> alerts, bool ignoreEmptyValues)
        {
            var sb = new StringBuilder();
            sb.AppendLine("nr_nrql_alerts = [");

            foreach (var alert in alerts)
            {
                sb.AppendLine("  {");

                AppendIfNotEmpty(sb, "name", alert.Name, ignoreEmptyValues);
                AppendIfNotEmpty(sb, "description", alert.Description, ignoreEmptyValues);
                AppendIfNotEmpty(sb, "nrql_query", alert.NrqlQuery, ignoreEmptyValues);
                AppendIfNotEmpty(sb, "runbook_url", alert.RunbookUrl, ignoreEmptyValues);
                AppendIfNotEmpty(sb, "severity", alert.Severity, ignoreEmptyValues);
                AppendIfNotEmpty(sb, "enabled", alert.Enabled.ToString().ToLower(), ignoreEmptyValues);
                AppendIfNotEmpty(sb, "aggregation_method", alert.AggregationMethod, ignoreEmptyValues);
                AppendIfNotEmpty(sb, "aggregation_window", alert.AggregationWindow.ToString(), ignoreEmptyValues);
                AppendIfNotEmpty(sb, "aggregation_delay", alert.AggregationDelay.ToString(), ignoreEmptyValues);
                AppendIfNotEmpty(sb, "critical_operator", alert.CriticalOperator, ignoreEmptyValues);
                AppendIfNotEmpty(sb, "critical_threshold", alert.CriticalThreshold.ToString(), ignoreEmptyValues);
                AppendIfNotEmpty(sb, "critical_threshold_duration", alert.CriticalThresholdDuration.ToString(), ignoreEmptyValues);
                AppendIfNotEmpty(sb, "critical_threshold_occurrences", alert.CriticalThresholdOccurrences, ignoreEmptyValues);

                sb.AppendLine("  },");
            }

            sb.AppendLine("]");
            return sb.ToString();
        }

        private void AppendIfNotEmpty(StringBuilder sb, string key, string value, bool ignoreEmptyValues)
        {
            if (!ignoreEmptyValues || !string.IsNullOrWhiteSpace(value))
            {
                sb.AppendLine($"    \"{key}\" = \"{value}\"");
            }
        }

        public string ReplaceNrqlAlertsSection(string originalContent, List<NrqlAlert> alerts)
        {
            // Serialize the updated alerts to HCL format
            var updatedAlertsSection = SerializeAlerts(alerts, true);

            // Use regex to find and replace the nr_nrql_alerts section
            var regex = new Regex(@"nr_nrql_alerts\s*=\s*\[.*?\]", RegexOptions.Singleline);
            return regex.Replace(originalContent, updatedAlertsSection);
        }
    }
}