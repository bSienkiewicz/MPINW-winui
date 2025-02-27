using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SupportTool.Helpers
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
        public int? AggregationWindow { get; set; }
        public int? AggregationDelay { get; set; }
        public string CriticalOperator { get; set; }
        public double? CriticalThreshold { get; set; }
        public int? CriticalThresholdDuration { get; set; }
        public string CriticalThresholdOccurrences { get; set; }
        public bool ValueChanged { get; set; }

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

            // Find the nr_nrql_alerts array using a greedy regex
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

        public List<NrqlAlert> ParseAlerts2(string content)
        {
            var alerts = new List<NrqlAlert>();

            // Find the nr_nrql_alerts array using a more robust regex
            var arrayMatch = Regex.Match(content, @"nr_nrql_alerts\s*=\s*\[(.*?)\](?=\s*$|\s*#|\s*\w+\s*=)", RegexOptions.Singleline);

            if (!arrayMatch.Success)
            {
                return alerts;
            }

            string alertsContent = arrayMatch.Groups[1].Value;

            // More sophisticated parsing of alert blocks
            var blockPattern = @"(?:(?:#[^\n]*\n)*\s*\{[^{}]*?""name""[^{}]*?\})";
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

            var value = (match.Groups[1].Value + match.Groups[2].Value).Trim().Trim('"');

            // Normalize specific fields
            return key switch
            {
                "severity" => value.ToUpper(),
                "aggregation_method" => value.ToUpper(),
                "critical_operator" => value.ToUpper(),
                "critical_threshold_occurrences" => value.ToUpper(),
                _ => value
            };
        }

        private bool ParseBoolValue(string block, string key)
        {
            var value = ParseStringValue(block, key).ToLower();
            return value == "true";
        }

        private int? ParseIntValue(string block, string key)
        {
            var value = ParseStringValue(block, key);
            return int.TryParse(value, out int result) ? result : (int?)null;
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

                // if not last append comma
                if (!Comparer.ReferenceEquals(alerts.Last(), alert))
                {
                    sb.AppendLine("  },");
                } else
                {
                    sb.AppendLine("  }");
                }
            }

            sb.AppendLine("]");
            return sb.ToString().Trim();
        }

        public void AppendIfNotEmpty(StringBuilder sb, string key, string value, bool ignoreEmptyValues)
        {
            if (!ignoreEmptyValues || !string.IsNullOrWhiteSpace(value))
            {
                // Escape special characters in the value string
                string escapedValue = EscapeHclString(value);
                sb.AppendLine($"    \"{key}\" = \"{escapedValue}\"");
            }
        }

        private string EscapeHclString(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            return input
              .Replace(@"\", @"\\")
              .Replace("\"", "\\\"");
        }

        public string ReplaceNrqlAlertsSection(string originalContent, List<NrqlAlert> alerts)
        {
            // Serialize the updated alerts to HCL format
            var updatedAlertsSection = SerializeAlerts(alerts, true);
            Debug.WriteLine(updatedAlertsSection);

            //var regex = new Regex(@"nr_nrql_alerts\s*=\s*\[((?:[^\[\]]|\[[^\[\]]*\])*)\]", RegexOptions.Singleline); - old regex in case this new one fucks something up
            var regex = new Regex(@"nr_nrql_alerts\s*=\s*\[(.*)\](?=\s*$|\s*\w+\s*=)", RegexOptions.Singleline);

            var match = regex.Match(originalContent);
            if (!match.Success)
            {
                Debug.WriteLine("No match found for nr_nrql_alerts section.");
                return originalContent;
            }
            Debug.WriteLine($"Matched section:\n{match.Value}");


            // Replace the entire content with the updated section
            return originalContent.Replace(match.Value, updatedAlertsSection);

        }
    }
}