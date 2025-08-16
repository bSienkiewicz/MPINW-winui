using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SupportTool.Features.Alerts.Helpers
{
    public static class ConfigLoader
    {
        private static Dictionary<string, object> _config;
        static ConfigLoader()
        {
            var configPath = Path.Combine("Config", "generalConfig.json");
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                _config = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            }
            else
            {
                _config = new Dictionary<string, object>();
            }
        }

        public static T Get<T>(string key, T defaultValue = default)
        {
            if (_config != null && _config.TryGetValue(key, out var value))
            {
                try
                {
                    if (value is JsonElement elem)
                    {
                        return JsonSerializer.Deserialize<T>(elem.GetRawText());
                    }
                    return (T)System.Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    // fallback to default if conversion fails
                }
            }
            return defaultValue;
        }
    }
} 