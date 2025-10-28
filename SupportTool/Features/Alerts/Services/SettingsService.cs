using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Windows.Storage;

namespace SupportTool.Features.Alerts.Services
{
    public class SettingsService
    {
        private readonly string _settingsPath;
        private readonly string _secureSettingsPath;
        private Dictionary<string, object> _settings;
        private Dictionary<string, string> _secureSettings;
        
        // List of keys that should be stored securely
        private readonly HashSet<string> _sensitiveKeys = new HashSet<string>
        {
            "NR_API_Key"
            // Add any other sensitive keys here
        };

        public SettingsService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var supportToolDir = Path.Combine(appDataPath, "SupportTool");
            
            _settingsPath = Path.Combine(supportToolDir, "settings.json");
            _secureSettingsPath = Path.Combine(supportToolDir, "secure_settings.dat");

            // Ensure directory exists
            Directory.CreateDirectory(supportToolDir);
            
            LoadSettings();
            LoadSecureSettings();
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    _settings = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
                }
                else
                {
                    _settings = new Dictionary<string, object>();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading settings: {ex.Message}");
                _settings = new Dictionary<string, object>();
            }
        }

        private void SaveSettings()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                var json = JsonSerializer.Serialize(_settings, options);
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        private void LoadSecureSettings()
        {
            _secureSettings = new Dictionary<string, string>();
            
            try
            {
                if (File.Exists(_secureSettingsPath))
                {
                    byte[] protectedData = File.ReadAllBytes(_secureSettingsPath);
                    byte[] unprotectedData = ProtectedData.Unprotect(protectedData, null, DataProtectionScope.CurrentUser);
                    string json = Encoding.UTF8.GetString(unprotectedData);
                    _secureSettings = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading secure settings: {ex.Message}");
                // If there's an error, we start with a fresh secure settings dictionary
                _secureSettings = new Dictionary<string, string>();
            }
        }

        private void SaveSecureSettings()
        {
            try
            {
                string json = JsonSerializer.Serialize(_secureSettings);
                byte[] unprotectedData = Encoding.UTF8.GetBytes(json);
                byte[] protectedData = ProtectedData.Protect(unprotectedData, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(_secureSettingsPath, protectedData);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving secure settings: {ex.Message}");
            }
        }

        public string GetSetting(string key, string defaultValue = "")
        {
            // Check if this is a sensitive key that should be retrieved from secure storage
            if (_sensitiveKeys.Contains(key))
            {
                return _secureSettings.TryGetValue(key, out var value) ? value : defaultValue;
            }
            
            // Otherwise retrieve from regular settings
            if (_settings.TryGetValue(key, out var regularValue))
            {
                return regularValue?.ToString() ?? defaultValue;
            }
            
            return defaultValue;
        }

        public void SetSetting(string key, string value)
        {
            // Store sensitive keys in secure storage
            if (_sensitiveKeys.Contains(key))
            {
                _secureSettings[key] = value;
                SaveSecureSettings();
            }
            else
            {
                _settings[key] = value;
                SaveSettings();
            }
        }

        public void RemoveSetting(string key)
        {
            // Remove from appropriate storage
            if (_sensitiveKeys.Contains(key))
            {
                if (_secureSettings.ContainsKey(key))
                {
                    _secureSettings.Remove(key);
                    SaveSecureSettings();
                }
            }
            else if (_settings.ContainsKey(key))
            {
                _settings.Remove(key);
                SaveSettings();
            }
        }

        public bool IsApiKeySet()
        {
            return _secureSettings.ContainsKey("NR_API_Key");
        }

        public Dictionary<string, object> GetAllSettings()
        {
            // Return all non-sensitive settings
            return new Dictionary<string, object>(_settings);
        }

        public void RemoveAllSettings()
        {
            _settings.Clear();
            SaveSettings();
            
            _secureSettings.Clear();
            SaveSecureSettings();
        }
        
        // Migration method to help move from ApplicationDataContainer to file-based storage
        public void MigrateFromApplicationDataContainer()
        {
            try
            {
                var appData = ApplicationData.Current;
                if (appData != null)
                {
                    var localSettings = appData.LocalSettings;
                    foreach (var key in localSettings.Values.Keys)
                    {
                        // Handle sensitive keys appropriately during migration
                        if (_sensitiveKeys.Contains(key) && localSettings.Values[key] is string value)
                        {
                            _secureSettings[key] = value;
                        }
                        else
                        {
                            _settings[key] = localSettings.Values[key];
                        }
                    }
                    
                    SaveSettings();
                    SaveSecureSettings();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to migrate settings: {ex.Message}");
            }
        }
    }
}