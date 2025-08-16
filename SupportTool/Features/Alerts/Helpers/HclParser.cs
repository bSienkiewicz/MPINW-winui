using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SupportTool.Features.Alerts.Models;

namespace SupportTool.Features.Alerts.Helpers
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
                return alerts;
            }
            string alertsContent = arrayMatch.Groups[1].Value;

            var blockPattern = @"(?:#[^\n]*\n)*\s*\{(?<blockContent>[^{}]*)\}";
            var alertBlocks = Regex.Matches(alertsContent, blockPattern, RegexOptions.Singleline);

            foreach (Match match in alertBlocks)
            {
                var blockContent = match.Groups["blockContent"].Success ? match.Groups["blockContent"].Value : match.Groups[1].Value;

                if (string.IsNullOrWhiteSpace(blockContent) || !blockContent.Contains("\"name\""))
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
                    AggregationWindow = ParseDoubleValue(blockContent, "aggregation_window"),
                    AggregationDelay = ParseDoubleValue(blockContent, "aggregation_delay"),
                    CriticalOperator = ParseStringValue(blockContent, "critical_operator"),
                    CriticalThreshold = ParseDoubleValue(blockContent, "critical_threshold"),
                    CriticalThresholdDuration = ParseDoubleValue(blockContent, "critical_threshold_duration"),
                    CriticalThresholdOccurrences = ParseStringValue(blockContent, "critical_threshold_occurrences"),
                    ExpirationDuration = ParseDoubleValue(blockContent, "expiration_duration"),
                    CloseViolationsOnExpiration = ParseBoolValue(blockContent, "close_violations_on_expiration"),
                };

                // Parse additional fields
                ParseAdditionalFields(blockContent, alert);

                if (!string.IsNullOrWhiteSpace(alert.Name))
                {
                    alerts.Add(alert);
                }
            }
            return alerts;
        }

        private void ParseAdditionalFields(string blockContent, NrqlAlert alert)
        {
            // Known fields that are already parsed
            var knownFields = new HashSet<string>
            {
                "name", "description", "nrql_query", "runbook_url", "severity", "enabled",
                "aggregation_method", "aggregation_window", "aggregation_delay",
                "critical_operator", "critical_threshold", "critical_threshold_duration",
                "critical_threshold_occurrences", "expiration_duration", "close_violations_on_expiration"
            };

            // Find all key-value pairs in the block
            var pattern = @"""([^""]+)""\s*=\s*([^,\n]+)";
            var matches = Regex.Matches(blockContent, pattern);

            foreach (Match match in matches)
            {
                var key = match.Groups[1].Value;
                var rawValue = match.Groups[2].Value.Trim();

                // Skip known fields
                if (knownFields.Contains(key))
                    continue;

                // Parse the value based on its format
                object value = ParseAdditionalFieldValue(rawValue);
                alert.AdditionalFields[key] = value;
            }
        }

        private object ParseAdditionalFieldValue(string rawValue)
        {
            // Remove quotes if present
            if (rawValue.StartsWith("\"") && rawValue.EndsWith("\""))
            {
                return rawValue.Substring(1, rawValue.Length - 2);
            }

            // Try to parse as boolean
            if (rawValue.ToLowerInvariant() == "true" || rawValue.ToLowerInvariant() == "false")
            {
                return bool.Parse(rawValue);
            }

            // Try to parse as number
            if (double.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double doubleValue))
            {
                return doubleValue;
            }

            // Try to parse as integer
            if (int.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out int intValue))
            {
                return intValue;
            }

            // Handle null
            if (rawValue.ToLowerInvariant() == "null")
            {
                return null;
            }

            // Return as string if nothing else matches
            return rawValue;
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

        /// <summary>
        /// Serializes a list of alerts to HCL format.
        /// </summary>
        /// <param name="alerts">The list of alerts to serialize</param>
        /// <param name="ignoreEmptyValues">If true, fields with empty/null values will be omitted from the output.
        /// Note: This does not affect the special handling of Loss of Signal fields, which are omitted when:
        /// - close_violations_on_expiration is false
        /// - expiration_duration is 0</param>
        /// <returns>The serialized HCL string</returns>
        public string SerializeAlerts(List<NrqlAlert> alerts, bool ignoreEmptyValues)
        {
            var sb = new StringBuilder();
            sb.AppendLine("nr_nrql_alerts = [");

            int maxKeyLength = CalculateMaxKeyLength(alerts);
            int padLength = maxKeyLength + 1; // Padding for alignment

            for (int i = 0; i < alerts.Count; i++)
            {
                var alert = alerts[i];
                sb.AppendLine("  {");

                // Serialize known fields
                AppendStringIfNotEmpty(sb, "name", alert.Name, ignoreEmptyValues, padLength);
                AppendStringIfNotEmpty(sb, "description", alert.Description, ignoreEmptyValues, padLength);
                AppendStringIfNotEmpty(sb, "nrql_query", alert.NrqlQuery, ignoreEmptyValues, padLength);
                AppendStringIfNotEmpty(sb, "runbook_url", alert.RunbookUrl, ignoreEmptyValues, padLength);
                AppendStringIfNotEmpty(sb, "severity", alert.Severity, ignoreEmptyValues, padLength);
                AppendBooleanIfNotEmpty(sb, "enabled", alert.Enabled, ignoreEmptyValues, padLength);
                AppendStringIfNotEmpty(sb, "aggregation_method", alert.AggregationMethod, ignoreEmptyValues, padLength);
                AppendNumericIfNotEmpty(sb, "aggregation_window", alert.AggregationWindow, ignoreEmptyValues, padLength);
                AppendNumericIfNotEmpty(sb, "aggregation_delay", alert.AggregationDelay, ignoreEmptyValues, padLength, "double");
                AppendStringIfNotEmpty(sb, "critical_operator", alert.CriticalOperator, ignoreEmptyValues, padLength);
                AppendNumericIfNotEmpty(sb, "critical_threshold", alert.CriticalThreshold, ignoreEmptyValues, padLength, "threshold");
                AppendNumericIfNotEmpty(sb, "critical_threshold_duration", alert.CriticalThresholdDuration, ignoreEmptyValues, padLength, "duration");
                AppendStringIfNotEmpty(sb, "critical_threshold_occurrences", alert.CriticalThresholdOccurrences, ignoreEmptyValues, padLength);
                AppendNumericIfNotEmpty(sb, "expiration_duration", alert.ExpirationDuration, ignoreEmptyValues, padLength, "duration");
                AppendBooleanIfNotEmpty(sb, "close_violations_on_expiration", alert.CloseViolationsOnExpiration, ignoreEmptyValues, padLength);

                // Serialize additional fields
                SerializeAdditionalFields(sb, alert.AdditionalFields, ignoreEmptyValues = false, padLength);

                sb.Append("  }");
                if (i < alerts.Count - 1)
                {
                    sb.AppendLine(",");
                }
                else
                {
                    sb.AppendLine();
                }
            }

            sb.AppendLine("]");
            return sb.ToString().Trim();
        }

        private void SerializeAdditionalFields(StringBuilder sb, Dictionary<string, object> additionalFields, bool ignoreEmptyValues, int padLength)
        {
            foreach (var kvp in additionalFields)
            {
                var key = kvp.Key;
                var value = kvp.Value;

                if (value == null)
                {
                    if (!ignoreEmptyValues)
                    {
                        string paddedKey = $"\"{key}\"".PadRight(padLength);
                        sb.AppendLine($"    {paddedKey} = null");
                    }
                }
                else if (value is string stringValue)
                {
                    AppendStringIfNotEmpty(sb, key, stringValue, ignoreEmptyValues, padLength);
                }
                else if (value is bool boolValue)
                {
                    AppendBooleanIfNotEmpty(sb, key, boolValue, ignoreEmptyValues, padLength);
                }
                else if (value is double || value is float || value is int || value is long)
                {
                    AppendNumericIfNotEmpty(sb, key, value, ignoreEmptyValues, padLength);
                }
                else
                {
                    // Fallback: convert to string
                    AppendStringIfNotEmpty(sb, key, value.ToString(), ignoreEmptyValues, padLength);
                }
            }
        }

        private int CalculateMaxKeyLength(List<NrqlAlert> alerts)
        {
            // Include known keys
            string[] knownKeys = [
                "name", "description", "nrql_query", "runbook_url", "severity", "enabled",
                "aggregation_method", "aggregation_window", "aggregation_delay",
                "critical_operator", "critical_threshold", "critical_threshold_duration",
                "critical_threshold_occurrences", "expiration_duration", "close_violations_on_expiration"
            ];

            int maxKeyLength = 0;
            
            // Check known keys
            foreach (var key in knownKeys)
            {
                int keyLength = $"\"{key}\"".Length;
                if (keyLength > maxKeyLength) maxKeyLength = keyLength;
            }

            // Check additional fields
            foreach (var alert in alerts)
            {
                foreach (var key in alert.AdditionalFields.Keys)
                {
                    int keyLength = $"\"{key}\"".Length;
                    if (keyLength > maxKeyLength) maxKeyLength = keyLength;
                }
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

        /// <summary>
        /// Appends a boolean value to the HCL output if it should be included.
        /// </summary>
        /// <param name="sb">The StringBuilder to append to</param>
        /// <param name="key">The field name</param>
        /// <param name="value">The boolean value</param>
        /// <param name="ignoreEmptyValues">If true, null values will be omitted (not applicable for booleans)</param>
        /// <param name="padLength">The length to pad the key for alignment</param>
        /// <remarks>
        /// Special handling: The close_violations_on_expiration field is omitted when its value is false,
        /// regardless of the ignoreEmptyValues parameter.
        /// </remarks>
        private void AppendBooleanIfNotEmpty(StringBuilder sb, string key, bool value, bool ignoreEmptyValues, int padLength)
        {
            // Skip writing close_violations_on_expiration if it's false
            if (key == "close_violations_on_expiration" && !value)
            {
                return;
            }

            string stringValue = value.ToString().ToLowerInvariant();
            string paddedKey = $"\"{key}\"".PadRight(padLength);
            sb.AppendLine($"    {paddedKey} = {stringValue}");
        }

        /// <summary>
        /// Appends a numeric value to the HCL output if it should be included.
        /// </summary>
        /// <param name="sb">The StringBuilder to append to</param>
        /// <param name="key">The field name</param>
        /// <param name="value">The numeric value</param>
        /// <param name="ignoreEmptyValues">If true, null values will be omitted</param>
        /// <param name="padLength">The length to pad the key for alignment</param>
        /// <param name="numericTypeHint">A hint about the type of number being written (threshold, duration, or double)</param>
        /// <remarks>
        /// Special handling: The expiration_duration field is omitted when its value is 0,
        /// regardless of the ignoreEmptyValues parameter.
        /// </remarks>
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

            // Skip writing expiration_duration if it's 0
            if (key == "expiration_duration" && dblValue == 0)
            {
                return;
            }

            string stringValue;

            // Apply formatting based on the hint provided
            switch (numericTypeHint)
            {
                case "threshold":
                    // Use tolerance for float comparison to check if it's effectively a whole number
                    if (Math.Abs(dblValue % 1) <= double.Epsilon * 100)
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
            var updatedAlertsSection = SerializeAlerts(alerts, false);

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