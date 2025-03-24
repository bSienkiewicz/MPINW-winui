using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;

namespace SupportTool.Services
{
    public class SettingsService
    {
        private readonly ApplicationDataContainer _localSettings;

        public SettingsService()
        {
            _localSettings = ApplicationData.Current.LocalSettings;
        }

        public string GetSetting(string key, string defaultValue = "")
        {
            return _localSettings.Values[key] as string ?? defaultValue;
        }

        public void SetSetting(string key, string value)
        {
            _localSettings.Values[key] = value;
        }

        public void RemoveSetting(string key)
        {
            _localSettings.Values.Remove(key);
        }

        public bool IsApiKeySet()
        {
            return _localSettings.Values.ContainsKey("NR_API_Key");
        }


        public Dictionary<string, object> GetAllSettings()
        {
            var allSettings = new Dictionary<string, object>();
            foreach (var key in _localSettings.Values.Keys)
            {
                allSettings.Add(key, _localSettings.Values[key]);
            }
            return allSettings;
        }
        public void RemoveAllSettings()
        {
            _localSettings.Values.Clear();
        }
    }
}
