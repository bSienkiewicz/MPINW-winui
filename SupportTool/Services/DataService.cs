using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SupportTool.Services
{
    // <summary>
    // This class is used to store and retrieve data across the app.
    // </summary>

    public class DataService
    {
        private static readonly DataService _instance = new DataService();
        private Dictionary<string, object> _data = new Dictionary<string, object>();

        private DataService() { }

        public static DataService Instance
        {
            get { return _instance; }
        }

        public void SetData<T>(string key, T value)
        {
            if (_data.ContainsKey(key))
                _data[key] = value;
            else
                _data.Add(key, value);
        }

        public T GetData<T>(string key)
        {
            if (_data.ContainsKey(key))
                return (T)_data[key];
            return default(T);
        }

        // Helper methods specific to your app
        public void SaveAppNames(ObservableCollection<AppNameItem> appNames)
        {
            SetData("AppNames", appNames);
        }

        public void SaveStacks(string[] stacks)
        {
            SetData("Stacks", stacks);
        }

        public ObservableCollection<AppNameItem> GetAppNames()
        {
            return GetData<ObservableCollection<AppNameItem>>("AppNames") ?? new ObservableCollection<AppNameItem>();
        }
    }
}
