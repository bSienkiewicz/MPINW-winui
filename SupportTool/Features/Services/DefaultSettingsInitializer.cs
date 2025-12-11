using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using SupportTool.Features.Alerts.Services;

namespace SupportTool.Features.Services
{
    /// <summary>
    /// Initializes default settings values if they don't exist in the system.
    /// Reads default values from Config/defaultSettings.json.
    /// </summary>
    public static class DefaultSettingsInitializer
    {
        /// <summary>
        /// Initializes all default settings that should be set on first application launch.
        /// Only sets values if they don't already exist.
        /// Reads default values from Config/defaultSettings.json.
        /// </summary>
        public static void InitializeDefaults()
        {
            var settingsService = new SettingsService();
            var defaultSettings = LoadDefaultSettings();

            if (defaultSettings == null || defaultSettings.Count == 0)
            {
                Debug.WriteLine("Warning: No default settings found in Config/defaultSettings.json");
                return;
            }

            // Initialize each setting from the JSON file if it doesn't exist
            foreach (var setting in defaultSettings)
            {
                string currentValue = settingsService.GetSetting(setting.Key);
                if (string.IsNullOrEmpty(currentValue))
                {
                    // Extract the string value from JsonElement
                    string defaultValue = setting.Value.ValueKind == JsonValueKind.String
                        ? setting.Value.GetString() ?? string.Empty
                        : setting.Value.GetRawText().Trim('"');
                    
                    if (!string.IsNullOrEmpty(defaultValue))
                    {
                        settingsService.SetSetting(setting.Key, defaultValue);
                        Debug.WriteLine($"Initialized default setting: {setting.Key} = {defaultValue}");
                    }
                }
            }
        }

        /// <summary>
        /// Loads default settings from Config/defaultSettings.json.
        /// Tries to read from file system first, then falls back to embedded resource.
        /// </summary>
        private static Dictionary<string, JsonElement>? LoadDefaultSettings()
        {
            try
            {
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "defaultSettings.json");
                string resourceName = "SupportTool.Config.defaultSettings.json";
                string json = string.Empty;

                // Try to read from file system first
                if (File.Exists(filePath))
                {
                    json = File.ReadAllText(filePath);
                }
                else
                {
                    // Fall back to embedded resource
                    using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
                    if (stream == null)
                    {
                        Debug.WriteLine($"Error: Embedded resource '{resourceName}' not found.");
                        return null;
                    }
                    using var reader = new StreamReader(stream);
                    json = reader.ReadToEnd();

                    // Attempt to save the default config to disk for user modification
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                        File.WriteAllText(filePath, json);
                    }
                    catch (Exception exSave)
                    {
                        Debug.WriteLine($"Warning: Could not save default config to '{filePath}': {exSave.Message}");
                    }
                }

                if (!string.IsNullOrWhiteSpace(json))
                {
                    var settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                    return settings;
                }
                else
                {
                    Debug.WriteLine("Error: Default settings JSON is empty.");
                    return null;
                }
            }
            catch (JsonException jsonEx)
            {
                Debug.WriteLine($"Error deserializing defaultSettings.json: {jsonEx.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading default settings: {ex.Message}");
                return null;
            }
        }
    }
}

