using System.Collections.Generic;
using System.IO;
using System;
using System.Reflection;
using System.Text.Json;
using SupportTool.Models;

namespace SupportTool.Helpers
{
    public static class AlertTemplates
    {
        private static Dictionary<string, TemplateData>? templates;

        public static void LoadTemplates()
        {
            if (templates != null) return; // Already loaded

            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "templateConfig.json");
            string resourceName = "SupportTool.Config.templateConfig.json";

            try
            {
                string json;

                if (File.Exists(path))
                {
                    json = File.ReadAllText(path);
                }
                else
                {
                    using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
                    if (stream == null) throw new Exception("Embedded config not found.");
                    using var reader = new StreamReader(stream);
                    json = reader.ReadToEnd();

                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, json); // Save default for editing
                }

                templates = JsonSerializer.Deserialize<Dictionary<string, TemplateData>>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading templates: " + ex.Message);
                templates = new();
            }
        }

        public static NrqlAlert GetTemplate(string templateKey, string appName, string carrierName)
        {
            LoadTemplates();

            if (!templates!.ContainsKey(templateKey))
                throw new ArgumentException($"Template '{templateKey}' not found.");

            var t = templates[templateKey];

            return new NrqlAlert
            {
                Name = ReplaceTokens(t.Name, appName, carrierName),
                Description = ReplaceTokens(t.Description, appName, carrierName),
                Severity = t.Severity,
                NrqlQuery = ReplaceTokens(t.NrqlQuery, appName, carrierName, false),
                RunbookUrl = t.RunbookUrl,
                Enabled = t.Enabled,
                AggregationMethod = t.AggregationMethod,
                AggregationDelay = t.AggregationDelay,
                CriticalOperator = t.CriticalOperator,
                CriticalThreshold = t.CriticalThreshold,
                CriticalThresholdDuration = t.CriticalThresholdDuration,
                CriticalThresholdOccurrences = t.CriticalThresholdOccurrences
            };
        }

        private static string ReplaceTokens(string input, string appName, string carrierName, bool clean = true)
        {
            if (clean) appName = appName.Split('.')[0].ToUpper();
            return input
                .Replace("{appName}", appName)
                .Replace("{carrierName}", carrierName);
        }
    }

    public class TemplateData
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Severity { get; set; }
        public string NrqlQuery { get; set; }
        public string RunbookUrl { get; set; }
        public bool Enabled { get; set; }
        public string AggregationMethod { get; set; }
        public int AggregationDelay { get; set; }
        public string CriticalOperator { get; set; }
        public double CriticalThreshold { get; set; }
        public int CriticalThresholdDuration { get; set; }
        public string CriticalThresholdOccurrences { get; set; }
    }
}