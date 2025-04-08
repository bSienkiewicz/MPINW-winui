using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SupportTool.Models;

namespace SupportTool.Helpers
{
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
                    AggregationWindow = ParseDoubleValue(cleanBlock, "aggregation_window"),
                    AggregationDelay = ParseDoubleValue(cleanBlock, "aggregation_delay"),
                    CriticalOperator = ParseStringValue(cleanBlock, "critical_operator"),
                    CriticalThreshold = ParseDoubleValue(cleanBlock, "critical_threshold"),
                    CriticalThresholdDuration = ParseDoubleValue(cleanBlock, "critical_threshold_duration"),
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
            value = Regex.Unescape(value);

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

            // Calculate the maximum key length for padding
            int maxKeyLength = 0;
            string[] keys = [
                "name", "description", "nrql_query", "runbook_url", "severity", "enabled",
                "aggregation_method", "aggregation_window", "aggregation_delay",
                "critical_operator", "critical_threshold", "critical_threshold_duration",
                "critical_threshold_occurrences"
            ];

            // get the longest key to determine the space padding
            foreach (var key in keys)
            {
                int keyLength = $"\"{key}\"".Length;
                if (keyLength > maxKeyLength)
                    maxKeyLength = keyLength;
            }

            int padLength = maxKeyLength + 1; // +1 extra space for the longest key

            foreach (var alert in alerts)
            {
                sb.AppendLine("  {");

                AppendIfNotEmpty(sb, "name", alert.Name, ignoreEmptyValues, padLength);
                AppendIfNotEmpty(sb, "description", alert.Description, ignoreEmptyValues, padLength);
                AppendIfNotEmpty(sb, "nrql_query", alert.NrqlQuery, ignoreEmptyValues, padLength);
                AppendIfNotEmpty(sb, "runbook_url", alert.RunbookUrl, ignoreEmptyValues, padLength);
                AppendIfNotEmpty(sb, "severity", alert.Severity, ignoreEmptyValues, padLength);
                AppendIfNotEmpty(sb, "enabled", alert.Enabled.ToString().ToLower(), ignoreEmptyValues, padLength);
                AppendIfNotEmpty(sb, "aggregation_method", alert.AggregationMethod, ignoreEmptyValues, padLength);
                AppendIfNotEmpty(sb, "aggregation_window", alert.AggregationWindow.ToString(), ignoreEmptyValues, padLength);
                AppendIfNotEmpty(sb, "aggregation_delay", alert.AggregationDelay.ToString(), ignoreEmptyValues, padLength);
                AppendIfNotEmpty(sb, "critical_operator", alert.CriticalOperator, ignoreEmptyValues, padLength);
                AppendIfNotEmpty(sb, "critical_threshold", alert.CriticalThreshold.ToString(), ignoreEmptyValues, padLength);
                AppendIfNotEmpty(sb, "critical_threshold_duration", alert.CriticalThresholdDuration.ToString(), ignoreEmptyValues, padLength);
                AppendIfNotEmpty(sb, "critical_threshold_occurrences", alert.CriticalThresholdOccurrences, ignoreEmptyValues, padLength);

                // if not last append comma
                if (!Comparer.ReferenceEquals(alerts.Last(), alert))
                {
                    sb.AppendLine("  },");
                }
                else
                {
                    sb.AppendLine("  }");
                }
            }

            sb.AppendLine("]");
            return sb.ToString().Trim();
        }

        public void AppendIfNotEmpty(StringBuilder sb, string key, string value, bool ignoreEmptyValues, int padLength)
        {
            if (!ignoreEmptyValues || !string.IsNullOrWhiteSpace(value))
            {
                // Escape special characters in the value string
                string escapedValue = EscapeHclString(value);

                // Pad the key with spaces to align the equal signs
                string paddedKey = $"\"{key}\"".PadRight(padLength);

                sb.AppendLine($"    {paddedKey} = \"{escapedValue}\"");
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