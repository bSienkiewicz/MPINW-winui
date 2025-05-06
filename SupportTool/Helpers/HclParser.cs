using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
            var arrayMatch = Regex.Match(content, @"nr_nrql_alerts\s*=\s*\[(.*?)\](?=\s*(?:$|\w+\s*=))", RegexOptions.Singleline);

            if (!arrayMatch.Success)
            {
                Debug.WriteLine("NRQL alerts array 'nr_nrql_alerts = [...]' not found in HCL content.");
                return alerts; // Return empty list if the main array isn't found
            }
            string alertsContent = arrayMatch.Groups[1].Value;

            // Regex to find individual alert blocks { ... } within the array,
            // allowing for preceding comments (#)
            var blockPattern = @"(?:#[^\n]*\n)*\s*\{(?<blockContent>[^{}]*)\}";
            var alertBlocks = Regex.Matches(alertsContent, blockPattern, RegexOptions.Singleline);

            foreach (Match match in alertBlocks)
            {
                // Using named group "blockContent" if the pattern supports it, otherwise Group 1
                var blockContent = match.Groups["blockContent"].Success ? match.Groups["blockContent"].Value : match.Groups[1].Value;

                if (string.IsNullOrWhiteSpace(blockContent) || !blockContent.Contains("\"name\"")) // Basic check for valid block
                    continue;

                var alert = new NrqlAlert
                {
                    Name = ParseStringValue(blockContent, "name"),
                    Description = ParseStringValue(blockContent, "description"),
                    NrqlQuery = ParseStringValue(blockContent, "nrql_query"),
                    RunbookUrl = ParseStringValue(blockContent, "runbook_url"),
                    Severity = ParseStringValue(blockContent, "severity"),
                    Enabled = ParseBoolValue(blockContent, "enabled"),
                    AggregationMethod = ParseStringValue(blockContent, "aggregation_method"),
                    // Parse numeric values using helper, store as double
                    AggregationWindow = ParseDoubleValue(blockContent, "aggregation_window"),
                    AggregationDelay = ParseDoubleValue(blockContent, "aggregation_delay"),
                    CriticalOperator = ParseStringValue(blockContent, "critical_operator"),
                    CriticalThreshold = ParseDoubleValue(blockContent, "critical_threshold"),
                    CriticalThresholdDuration = ParseDoubleValue(blockContent, "critical_threshold_duration"),
                    CriticalThresholdOccurrences = ParseStringValue(blockContent, "critical_threshold_occurrences")
                };

                // Add only if the alert has a name (basic validation)
                if (!string.IsNullOrWhiteSpace(alert.Name))
                {
                    alerts.Add(alert);
                }
            }
            return alerts;
        }

        private string ParseStringValue(string blockContent, string key)
        {
            // Pattern breakdown:
            // $"\"{key}\""         : Match the key in quotes (e.g., "name")
            // \s*=\s*              : Match equals sign with optional whitespace
            // (?:                  : Start non-capturing group for value types
            //  \"(?<val1>[^\""]*)\" : Match quoted value, capture content in group "val1"
            //  |                   : OR
            //  (?<val2>[^,\s#]+)   : Match unquoted value (stops at comma, whitespace, or #), capture in group "val2"
            // )                    : End non-capturing group
            var pattern = $"\"{key}\"\\s*=\\s*(?:\\\"(?<val1>[^\\\\\"]*)\\\"|(?<val2>[^,\\s#]+))";
            var match = Regex.Match(blockContent, pattern);

            if (!match.Success) return string.Empty;

            // Combine captured groups (only one will have a value) and trim
            var value = (match.Groups["val1"].Value + match.Groups["val2"].Value).Trim();

            // Unescape common sequences like \\ and \"
            value = Regex.Unescape(value); // More robust than manual replaces

            // Apply specific normalization if needed
            return key switch
            {
                "severity" => value.ToUpperInvariant(),
                "aggregation_method" => value.ToUpperInvariant(),
                "critical_operator" => value.ToUpperInvariant(),
                "critical_threshold_occurrences" => value.ToUpperInvariant(),
                _ => value
            };
        }

        private bool ParseBoolValue(string blockContent, string key)
        {
            var value = ParseStringValue(blockContent, key).ToLowerInvariant();
            return value == "true";
        }



        private double ParseDoubleValue(string blockContent, string key)
        {
            var value = ParseStringValue(blockContent, key);
            return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double result) ? result : 0.0;
        }

        public string SerializeAlerts(List<NrqlAlert> alerts, bool ignoreEmptyValues)
        {
            var sb = new StringBuilder();
            sb.AppendLine("nr_nrql_alerts = [");

            int maxKeyLength = CalculateMaxKeyLength();
            int padLength = maxKeyLength + 1; // Padding for alignment

            for (int i = 0; i < alerts.Count; i++)
            {
                var alert = alerts[i];
                sb.AppendLine("  {");

                // Use appropriate Append methods based on required *output* type and model type
                AppendStringIfNotEmpty(sb, "name", alert.Name, ignoreEmptyValues, padLength);
                AppendStringIfNotEmpty(sb, "description", alert.Description, ignoreEmptyValues, padLength);
                AppendStringIfNotEmpty(sb, "nrql_query", alert.NrqlQuery, ignoreEmptyValues, padLength);
                AppendStringIfNotEmpty(sb, "runbook_url", alert.RunbookUrl, ignoreEmptyValues, padLength);
                AppendStringIfNotEmpty(sb, "severity", alert.Severity, ignoreEmptyValues, padLength);
                AppendBooleanIfNotEmpty(sb, "enabled", alert.Enabled, ignoreEmptyValues, padLength); // Handles bool
                AppendStringIfNotEmpty(sb, "aggregation_method", alert.AggregationMethod, ignoreEmptyValues, padLength);

                // aggregation_window: Saved as string (input is double)
                AppendStringIfNotEmpty(sb, "aggregation_window", alert.AggregationWindow.ToString(CultureInfo.InvariantCulture), ignoreEmptyValues, padLength);

                // aggregation_delay: Saved as number (input is double)
                AppendNumericIfNotEmpty(sb, "aggregation_delay", alert.AggregationDelay, ignoreEmptyValues, padLength, "double");

                AppendStringIfNotEmpty(sb, "critical_operator", alert.CriticalOperator, ignoreEmptyValues, padLength);

                // critical_threshold: Saved as int or float (input is double)
                AppendNumericIfNotEmpty(sb, "critical_threshold", alert.CriticalThreshold, ignoreEmptyValues, padLength, "threshold");

                // critical_threshold_duration: Saved as int (input is double)
                AppendNumericIfNotEmpty(sb, "critical_threshold_duration", alert.CriticalThresholdDuration, ignoreEmptyValues, padLength, "duration");

                AppendStringIfNotEmpty(sb, "critical_threshold_occurrences", alert.CriticalThresholdOccurrences, ignoreEmptyValues, padLength);

                sb.Append("  }");
                if (i < alerts.Count - 1)
                {
                    sb.AppendLine(","); // Add comma for all but the last alert
                }
                else
                {
                    sb.AppendLine(); // Newline after the last alert
                }
            }

            sb.AppendLine("]");
            return sb.ToString().Trim(); // Trim trailing whitespace
        }

        private int CalculateMaxKeyLength()
        {
            // List of keys expected in the HCL output
            string[] keys = [
                "name", "description", "nrql_query", "runbook_url", "severity", "enabled",
                 "aggregation_method", "aggregation_window", "aggregation_delay",
                 "critical_operator", "critical_threshold", "critical_threshold_duration",
                 "critical_threshold_occurrences"
            ];
            int maxKeyLength = 0;
            foreach (var key in keys)
            {
                // Length includes the quotes around the key
                int keyLength = $"\"{key}\"".Length;
                if (keyLength > maxKeyLength) maxKeyLength = keyLength;
            }
            return maxKeyLength;
        }

        private void AppendStringIfNotEmpty(StringBuilder sb, string key, string value, bool ignoreEmptyValues, int padLength)
        {
            if (!ignoreEmptyValues || !string.IsNullOrWhiteSpace(value))
            {
                string escapedValue = EscapeHclString(value ?? string.Empty); // Handle potential null
                string paddedKey = $"\"{key}\"".PadRight(padLength);
                sb.AppendLine($"    {paddedKey} = \"{escapedValue}\"");
            }
        }

        private void AppendBooleanIfNotEmpty(StringBuilder sb, string key, bool value, bool ignoreEmptyValues, int padLength)
        {
            // Note: Booleans usually aren't "empty". If 'false' should always be included,
            // remove the 'ignoreEmptyValues' check logic if it were added.
            string stringValue = value.ToString().ToLowerInvariant();
            string paddedKey = $"\"{key}\"".PadRight(padLength);
            sb.AppendLine($"    {paddedKey} = {stringValue}");
        }

        private void AppendNumericIfNotEmpty(StringBuilder sb, string key, object value, bool ignoreEmptyValues, int padLength, string numericTypeHint = "double")
        {
            // Handle null input
            if (value == null)
            {
                if (!ignoreEmptyValues)
                {
                    // Optionally write nulls as comments or skip entirely
                    sb.AppendLine($"    # \"{key}\" was null or non-numeric");
                }
                return; // Skip if null
            }

            // Convert input object to double for processing
            double dblValue;
            try
            {
                dblValue = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is OverflowException)
            {
                // Handle cases where value is not a valid number
                if (!ignoreEmptyValues)
                {
                    Debug.WriteLine($"Warning: Could not convert value for key '{key}' to double. Type: {value.GetType()}. Value: '{value}'. Skipping.");
                    sb.AppendLine($"    # \"{key}\" has non-numeric value: {EscapeHclString(value.ToString())}");
                }
                return; // Skip this key if conversion fails
            }


            string stringValue;

            // Apply formatting based on the hint provided
            switch (numericTypeHint)
            {
                case "threshold":
                    // Use tolerance for float comparison to check if it's effectively a whole number
                    if (Math.Abs(dblValue % 1) <= (Double.Epsilon * 100))
                    {
                        // Output as integer (using long for safety, though int likely ok)
                        stringValue = ((long)dblValue).ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        // Output as float/double using general format specifier "G"
                        stringValue = dblValue.ToString("G", CultureInfo.InvariantCulture);
                    }
                    break;

                case "duration":
                    // Truncate decimal part and output as integer
                    stringValue = ((long)dblValue).ToString(CultureInfo.InvariantCulture);
                    break;

                case "double":
                default:
                    // Output using general format specifier "G"
                    stringValue = dblValue.ToString("G", CultureInfo.InvariantCulture);
                    break;
            }

            // Append the formatted numeric value (no quotes)
            string paddedKey = $"\"{key}\"".PadRight(padLength);
            sb.AppendLine($"    {paddedKey} = {stringValue}");
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

            // Replace backslash with double backslash, then quote with backslash-quote
            return input
              .Replace(@"\", @"\\")
              .Replace("\"", "\\\"");
        }

        public string ReplaceNrqlAlertsSection(string originalContent, List<NrqlAlert> alerts)
        {
            // Serialize the new list of alerts (ignoring empty values for cleaner output)
            var updatedAlertsSection = SerializeAlerts(alerts, true);

            // Regex to find the existing nr_nrql_alerts block. Same pattern as in ParseAlerts.
            var regex = new Regex(@"nr_nrql_alerts\s*=\s*\[.*?\](?=\s*(?:$|\w+\s*=))", RegexOptions.Singleline);

            var match = regex.Match(originalContent);
            if (!match.Success)
            {
                Debug.WriteLine("No match found for nr_nrql_alerts section in original content. Cannot replace.");
                // Return original content if section not found
                return originalContent;
            }

            // Replace the first occurrence of the matched section with the new content
            return regex.Replace(originalContent, updatedAlertsSection, 1);
        }
    }
}