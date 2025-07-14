using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using SupportTool.Helpers;
using Microsoft.UI;
using Microsoft.UI.Xaml;

namespace SupportTool.Models
{
    public class NrqlAlert : INotifyPropertyChanged, ICloneable
    {
        private string _name;
        private string _description;
        private string _nrqlQuery;
        private string _runbookUrl;
        private string _severity;
        private bool _enabled;
        private string _aggregationMethod;
        private double _aggregationWindow;
        private double _aggregationDelay;
        private string _criticalOperator;
        private double _criticalThreshold;
        private double _criticalThresholdDuration;
        private string _criticalThresholdOccurrences;
        private double _expirationDuration;
        private bool _closeViolationsOnExpiration;
        private double? _proposedThreshold;
        private bool _isSelectedForUpdate;

        private bool _valueChanged;
        private Dictionary<string, object> _additionalFields = new Dictionary<string, object>();

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public string NrqlQuery
        {
            get => _nrqlQuery;
            set => SetProperty(ref _nrqlQuery, value);
        }

        public string RunbookUrl
        {
            get => _runbookUrl;
            set => SetProperty(ref _runbookUrl, value);
        }

        public string Severity
        {
            get => _severity;
            set => SetProperty(ref _severity, value);
        }

        public bool Enabled
        {
            get => _enabled;
            set => SetProperty(ref _enabled, value);
        }

        public string AggregationMethod
        {
            get => _aggregationMethod;
            set => SetProperty(ref _aggregationMethod, value);
        }

        public double AggregationWindow
        {
            get => _aggregationWindow;
            set => SetProperty(ref _aggregationWindow, value);
        }

        public double AggregationDelay
        {
            get => _aggregationDelay;
            set => SetProperty(ref _aggregationDelay, value);
        }

        public string CriticalOperator
        {
            get => _criticalOperator;
            set => SetProperty(ref _criticalOperator, value);
        }

        public double CriticalThreshold
        {
            get => _criticalThreshold;
            set
            {
                if (SetProperty(ref _criticalThreshold, value))
                {
                    OnPropertyChanged(nameof(NeedsUpdate));
                }
            }
        }

        public double CriticalThresholdDuration
        {
            get => _criticalThresholdDuration;
            set => SetProperty(ref _criticalThresholdDuration, value);
        }

        public string CriticalThresholdOccurrences
        {
            get => _criticalThresholdOccurrences;
            set => SetProperty(ref _criticalThresholdOccurrences, value);
        }

        public double ExpirationDuration
        {
            get => _expirationDuration;
            set => SetProperty(ref _expirationDuration, value);
        }

        public bool CloseViolationsOnExpiration
        {
            get => _closeViolationsOnExpiration;
            set => SetProperty(ref _closeViolationsOnExpiration, value);
        }

        public double? ProposedThreshold
        {
            get => _proposedThreshold;
            set
            {
                if (SetProperty(ref _proposedThreshold, value))
                {
                    OnPropertyChanged(nameof(NeedsUpdate));
                }
            }
        }

        public bool ValueChanged
        {
            get => _valueChanged;
            set
            {
                if (SetProperty(ref _valueChanged, value))
                {
                    OnPropertyChanged(nameof(ValueChanged));
                }
            }
        }

        public Dictionary<string, object> AdditionalFields
        {
            get => _additionalFields;
            set => SetProperty(ref _additionalFields, value ?? new Dictionary<string, object>());
        }

        public bool NeedsUpdate
        {
            get
            {
                if (ProposedThreshold.HasValue)
                {
                    double diff = Math.Abs(CriticalThreshold - ProposedThreshold.Value);
                    double threshold = AlertTemplates.GetThresholdDifference();
                    return diff >= threshold;
                }
                return false;
            }
        }

        public bool IsSelectedForUpdate
        {
            get => _isSelectedForUpdate;
            set => SetProperty(ref _isSelectedForUpdate, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual bool SetProperty<T>(ref T storage, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value)) return false;

            storage = value;

            // Only set ValueChanged to true for non-ValueChanged properties
            if (propertyName != nameof(ValueChanged))
            {
                ValueChanged = true;
            }

            OnPropertyChanged(propertyName);
            return true;
        }

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return $"Alert: {Name} - {Severity}";
        }

        public object Clone()
        {
            return new NrqlAlert
            {
                Name = this.Name,
                Description = this.Description,
                NrqlQuery = this.NrqlQuery,
                RunbookUrl = this.RunbookUrl,
                Severity = this.Severity,
                Enabled = this.Enabled,
                AggregationMethod = this.AggregationMethod,
                AggregationWindow = this.AggregationWindow,
                AggregationDelay = this.AggregationDelay,
                CriticalOperator = this.CriticalOperator,
                CriticalThreshold = this.CriticalThreshold,
                CriticalThresholdDuration = this.CriticalThresholdDuration,
                CriticalThresholdOccurrences = this.CriticalThresholdOccurrences,
                ExpirationDuration = this.ExpirationDuration,
                CloseViolationsOnExpiration = this.CloseViolationsOnExpiration,
                ProposedThreshold = this.ProposedThreshold,
                AdditionalFields = new Dictionary<string, object>(this.AdditionalFields),
                IsSelectedForUpdate = this.IsSelectedForUpdate
            };
        }

        public string GetVerificationNrql(string stack)
        {
            var carrier = SupportTool.Services.AlertService.ExtractCarrierFromTitle(Name);
            var threshold = ProposedThreshold.HasValue ? ProposedThreshold.Value : 0;
            var oldThreshold = CriticalThreshold;
            int samplingDays = AlertTemplates.GetConfigValue<int>("PrintDuration.ProposedValues.SamplingDays");
            // Use the same NRQL as in the template, but fill in the values
            return $"SELECT average(duration), stddev(duration) as 'Deviation', {threshold} as 'New Threshold', {oldThreshold} as 'Old Threshold' FROM Transaction WHERE PrintOperation like '%Create%' AND host like '%-{stack}-%' AND CarrierName = '{carrier}' timeseries max since {samplingDays} days ago";
        }
    }

    public class NRMetricsResult
    {
        public float MedianDuration { get; set; }
    }
}
