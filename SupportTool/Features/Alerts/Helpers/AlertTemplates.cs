using SupportTool.Features.Alerts.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq; // Required for Enumerable.Take
using System.Reflection;
using System.Text.Json;

namespace SupportTool.Features.Alerts.Helpers
{
    // Represents the "ProposedValues" object in your JSON
    public class ProposedValuesData
    {
        public double FormulaMultiplier { get; set; }
        public double FormulaOffset { get; set; }
    }

    // Represents the main structure of each template in your JSON
    // This is still useful if you want to get a whole template object
    public class TemplateData
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Severity { get; set; } = "";
        public string NrqlQuery { get; set; } = "";
        public string RunbookUrl { get; set; } = "";
        public bool Enabled { get; set; }
        public string AggregationMethod { get; set; } = "";
        public int AggregationDelay { get; set; }
        public string CriticalOperator { get; set; } = "";
        public double CriticalThreshold { get; set; }
        public int CriticalThresholdDuration { get; set; }
        public string CriticalThresholdOccurrences { get; set; } = "";
        public double ExpirationDuration { get; set; }
        public bool CloseViolationsOnExpiration { get; set; }
        public ProposedValuesData? ProposedValues { get; set; } // Nullable if not all templates have it
    }

    public static class AlertTemplates
    {
        // Store templates as a dictionary of template keys to their raw JsonElement representation
        private static Dictionary<string, JsonElement>? rawTemplates;
        private static readonly object loadLock = new object(); // For thread-safe loading

        public static void LoadTemplates()
        {
            // Double-check locking for thread-safe lazy initialization
            if (rawTemplates != null) return;

            lock (loadLock)
            {
                if (rawTemplates != null) return; // Check again inside lock

                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "templateConfig.json");
                string resourceName = "SupportTool.Config.templateConfig.json"; // Make sure this matches your embedded resource path
                string json = "";

                try
                {
                    if (File.Exists(path))
                    {
                        json = File.ReadAllText(path);
                    }
                    else
                    {
                        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
                        if (stream == null)
                        {
                            Console.WriteLine($"Error: Embedded resource '{resourceName}' not found.");
                            rawTemplates = new Dictionary<string, JsonElement>(); // Initialize to empty
                            return;
                        }
                        using var reader = new StreamReader(stream);
                        json = reader.ReadToEnd();

                        // Attempt to save the default config to disk for user modification
                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                            File.WriteAllText(path, json);
                        }
                        catch (Exception exSave)
                        {
                            Console.WriteLine($"Warning: Could not save default config to '{path}': {exSave.Message}");
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        rawTemplates = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                    }
                    else
                    {
                        Console.WriteLine("Error: Template configuration JSON is empty.");
                        rawTemplates = new Dictionary<string, JsonElement>();
                    }
                }
                catch (JsonException jsonEx)
                {
                    Console.WriteLine($"Error deserializing templateConfig.json: {jsonEx.Message}");
                    rawTemplates = new Dictionary<string, JsonElement>();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading templates: {ex.Message}");
                    rawTemplates = new Dictionary<string, JsonElement>();
                }
            }
        }

        /// <summary>
        /// Retrieves a specific value from the loaded JSON configuration using a path.
        /// Example path: "PrintDuration.ProposedValues.FormulaOffset"
        /// </summary>
        /// <typeparam name="T">The expected type of the value.</typeparam>
        /// <param name="jsonPath">The path to the value (e.g., "TemplateKey.Property.NestedProperty").</param>
        /// <returns>The deserialized value, or default(T) if not found or if conversion fails.</returns>
        public static T? GetConfigValue<T>(string jsonPath)
        {
            LoadTemplates(); // Ensure templates are loaded

            if (rawTemplates == null || string.IsNullOrWhiteSpace(jsonPath))
            {
                Console.WriteLine("Warning: Templates not loaded or JSON path is empty.");
                return default;
            }

            string[] segments = jsonPath.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                Console.WriteLine("Warning: JSON path is invalid (no segments).");
                return default;
            }

            // The first segment is the top-level template key (e.g., "PrintDuration")
            if (!rawTemplates.TryGetValue(segments[0], out JsonElement currentElement))
            {
                Console.WriteLine($"Warning: Template key '{segments[0]}' not found in configuration.");
                return default;
            }

            for (int i = 1; i < segments.Length; i++)
            {
                string segment = segments[i];
                if (currentElement.ValueKind != JsonValueKind.Object)
                {
                    Console.WriteLine($"Warning: Path segment '{string.Join(".", segments.Take(i))}' does not resolve to a JSON object. Cannot find property '{segment}'.");
                    return default;
                }

                if (!currentElement.TryGetProperty(segment, out JsonElement nextElement))
                {
                    Console.WriteLine($"Warning: Property '{segment}' not found at path '{string.Join(".", segments.Take(i + 1))}'.");
                    return default;
                }
                currentElement = nextElement;
            }

            try
            {
                return currentElement.Deserialize<T>();
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error deserializing value at path '{jsonPath}' to type '{typeof(T)}': {ex.Message}. ValueKind was: {currentElement.ValueKind}");
                return default;
            }
            catch (NotSupportedException ex)
            {
                Console.WriteLine($"Error: Deserialization of type '{typeof(T)}' not supported for value at path '{jsonPath}': {ex.Message}. ValueKind was: {currentElement.ValueKind}");
                return default;
            }
        }

        public static NrqlAlert GetTemplate(string templateName, string carrierName, string stack, string namepath, string facetBy)
        {
            if (rawTemplates == null)
            {
                Console.WriteLine("Error: Templates not loaded.");
                return new NrqlAlert();
            }

            if (!rawTemplates.TryGetValue(templateName, out JsonElement templateElement))
            {
                Console.WriteLine($"Warning: Template '{templateName}' not found in configuration.");
                return new NrqlAlert();
            }

            var t = JsonSerializer.Deserialize<TemplateData>(templateElement.GetRawText());
            if (t == null) return new NrqlAlert(); // Should not happen if JSON is valid

            return new NrqlAlert
            {
                Name = ReplaceTokens(t.Name, carrierName, stack, namepath, facetBy),
                Description = ReplaceTokens(t.Description, carrierName, stack, namepath, facetBy),
                Severity = t.Severity,
                NrqlQuery = ReplaceTokens(t.NrqlQuery, carrierName, stack, namepath, facetBy, false),
                RunbookUrl = t.RunbookUrl,
                Enabled = t.Enabled,
                AggregationMethod = t.AggregationMethod,
                AggregationDelay = t.AggregationDelay,
                CriticalOperator = t.CriticalOperator,
                CriticalThreshold = t.CriticalThreshold,
                CriticalThresholdDuration = t.CriticalThresholdDuration,
                CriticalThresholdOccurrences = t.CriticalThresholdOccurrences,
                ExpirationDuration = t.ExpirationDuration,
                CloseViolationsOnExpiration = t.CloseViolationsOnExpiration,
            };
        }

        private static string ReplaceTokens(string? input, string carrierName, string stack, string namepath, string facetBy, bool clean = true)
        {
            if (string.IsNullOrEmpty(input)) return "";

            string facet = string.Empty;
            if (!string.IsNullOrEmpty(facetBy) && facetBy != "None")
            {
                facet = $"FACET {facetBy}";
            }

            return input.Replace("{carrierName}", carrierName)
                        .Replace("{stack}", stack)
                        .Replace("{namepath}", namepath)
                        .Replace("{facet}", facet)
                        .TrimEnd();
        }

        /// <summary>
        /// Gets a DM alert template for Error Rate or Average Duration
        /// </summary>
        /// <param name="alertType">"ErrorRate" or "AverageDuration"</param>
        /// <param name="carrierId">The carrier ID</param>
        /// <param name="carrierName">Optional carrier name for display (if not provided, uses "Carrier {carrierId}")</param>
        /// <param name="includeAsos">Whether this is for ASOS retailer</param>
        /// <returns>NrqlAlert template</returns>
        public static NrqlAlert GetDmTemplate(string alertType, string carrierId, string? carrierName = null, bool includeAsos = false)
        {
            string displayName = carrierName ?? $"Carrier {carrierId}";
            string retailerFilter = includeAsos ? "retailerName = 'ASOS'" : "retailerName != 'ASOS'";
            string asosPrefix = includeAsos ? "ASOS " : "";
            
            string name;
            string nrqlQuery;
            string runbookUrl = "https://auctane.atlassian.net/wiki/spaces/GSKB/pages/6384386132/DM+Alerting+Error+Percentage+and+Average+Duration";

            if (alertType == "ErrorRate")
            {
                name = $"DM Allocation {asosPrefix}***Critical*** {displayName} ({carrierId}) Error Percentage";
                nrqlQuery = $"SELECT percentage(count(*),where error is true) FROM Transaction FACET retailerName,appName WHERE name = 'WebTransaction/SpringController/OctopusApiController/_allocateConsignment' AND {retailerFilter} AND carrierId = {carrierId}";
            }
            else if (alertType == "AverageDuration")
            {
                name = $"DM Allocation {asosPrefix}***Critical*** {displayName} ({carrierId}) Average Duration";
                nrqlQuery = $"SELECT average(duration) FROM Transaction WHERE name = 'WebTransaction/SpringController/OctopusApiController/_allocateConsignment' AND {retailerFilter} and carrierId = {carrierId} FACET retailerName,appName,carrierId";
            }
            else
            {
                throw new ArgumentException($"Unknown alert type: {alertType}", nameof(alertType));
            }

            return new NrqlAlert
            {
                Name = name,
                Description = "",
                Severity = "CRITICAL",
                NrqlQuery = nrqlQuery,
                RunbookUrl = runbookUrl,
                Enabled = true,
                AggregationMethod = "event_flow",
                AggregationWindow = 60,
                AggregationDelay = 120,
                CriticalOperator = "above_or_equals",
                CriticalThreshold = alertType == "ErrorRate" ? 5 : 4, // Default thresholds
                CriticalThresholdDuration = 300,
                CriticalThresholdOccurrences = "ALL",
                ExpirationDuration = 900,
                CloseViolationsOnExpiration = true
            };
        }

        public static double GetThresholdDifference()
        {
            try
            {
                var configPath = Path.Combine("Config", "generalConfig.json");
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("Threshold_Difference", out var prop))
                    {
                        return prop.GetDouble();
                    }
                }
            }
            catch { }
            return 3.0;
        }
    }
}