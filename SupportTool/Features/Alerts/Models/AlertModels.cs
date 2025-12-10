using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace SupportTool.Features.Alerts.Models
{
    // Existing model for app-carrier pair
    public struct AppCarrierItem : IEquatable<AppCarrierItem>
    {
        public string AppName { get; set; }
        public string CarrierName { get; set; }
        public bool HasPrintDurationAlert { get; set; }
        public bool HasErrorRateAlert { get; set; }

        public bool Equals(AppCarrierItem other)
        {
            return AppName == other.AppName && CarrierName == other.CarrierName;
        }

        public override bool Equals(object obj)
        {
            return obj is AppCarrierItem other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(AppName, CarrierName);
        }
    }

    // New model for carrier-only approach
    public class CarrierItem : IEquatable<CarrierItem>, INotifyPropertyChanged
    {
        private string _carrierName;
        private bool _hasPrintDurationAlert;
        private bool _hasErrorRateAlert;
        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public string CarrierName
        {
            get => _carrierName;
            set
            {
                if (_carrierName != value)
                {
                    _carrierName = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasPrintDurationAlert
        {
            get => _hasPrintDurationAlert;
            set
            {
                if (_hasPrintDurationAlert != value)
                {
                    _hasPrintDurationAlert = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasErrorRateAlert
        {
            get => _hasErrorRateAlert;
            set
            {
                if (_hasErrorRateAlert != value)
                {
                    _hasErrorRateAlert = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool Equals(CarrierItem other)
        {
            return CarrierName == other.CarrierName;
        }

        public override bool Equals(object obj)
        {
            return obj is CarrierItem other && Equals(other);
        }

        public override int GetHashCode()
        {
            return CarrierName?.GetHashCode() ?? 0;
        }
    }

    // Other existing models
    public class AppNameItem
    {
        public string AppName { get; set; }
        public List<CarrierItem> Carriers { get; set; } = new List<CarrierItem>();
    }

    public class CarrierMetrics
    {
        public string CarrierName { get; set; }
        public float MedianDuration { get; set; }
        public int TotalCalls { get; set; }
        public float ErrorRate { get; set; }
        public List<string> TopApps { get; set; } = new List<string>();
    }

    // Model for DM alerts using carrier IDs
    public class CarrierIdItem : IEquatable<CarrierIdItem>, INotifyPropertyChanged
    {
        private string _carrierId;
        private bool _hasAverageDurationAlert;
        private bool _hasErrorRateAlert;
        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public string CarrierId
        {
            get => _carrierId;
            set
            {
                if (_carrierId != value)
                {
                    _carrierId = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasAverageDurationAlert
        {
            get => _hasAverageDurationAlert;
            set
            {
                if (_hasAverageDurationAlert != value)
                {
                    _hasAverageDurationAlert = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasErrorRateAlert
        {
            get => _hasErrorRateAlert;
            set
            {
                if (_hasErrorRateAlert != value)
                {
                    _hasErrorRateAlert = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool Equals(CarrierIdItem other)
        {
            return CarrierId == other.CarrierId;
        }

        public override bool Equals(object obj)
        {
            return obj is CarrierIdItem other && Equals(other);
        }

        public override int GetHashCode()
        {
            return CarrierId?.GetHashCode() ?? 0;
        }
    }

    public enum AlertType
    {
        PrintDuration,
        ErrorRate
    }

    public class NewRelicResponse
    {
        [JsonProperty("data")]
        public Data Data { get; set; }
    }

    public class Data
    {
        [JsonProperty("actor")]
        public Actor Actor { get; set; }
    }

    public class Actor
    {
        [JsonProperty("account")]
        public Account Account { get; set; }
    }

    public class Account
    {
        [JsonProperty("nrql")]
        public Nrql Nrql { get; set; }
    }

    public class Nrql
    {
        [JsonProperty("results")]
        public List<Dictionary<string, object>> Results { get; set; }
    }
}