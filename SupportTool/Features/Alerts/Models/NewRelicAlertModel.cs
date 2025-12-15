using SupportTool.Features.Alerts.Helpers;
using SupportTool.Features.Alerts.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace SupportTool.Features.Alerts.Models
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
                Name = Name,
                Description = Description,
                NrqlQuery = NrqlQuery,
                RunbookUrl = RunbookUrl,
                Severity = Severity,
                Enabled = Enabled,
                AggregationMethod = AggregationMethod,
                AggregationWindow = AggregationWindow,
                AggregationDelay = AggregationDelay,
                CriticalOperator = CriticalOperator,
                CriticalThreshold = CriticalThreshold,
                CriticalThresholdDuration = CriticalThresholdDuration,
                CriticalThresholdOccurrences = CriticalThresholdOccurrences,
                ExpirationDuration = ExpirationDuration,
                CloseViolationsOnExpiration = CloseViolationsOnExpiration,
                ProposedThreshold = ProposedThreshold,
                AdditionalFields = new Dictionary<string, object>(AdditionalFields),
                IsSelectedForUpdate = IsSelectedForUpdate
            };
        }

        public string GetVerificationNrql(string stack)
        {
            var carrierName = AlertService.ExtractCarrierFromTitle(Name);
            var carrierId = AlertService.ExtractCarrierIdFromAlert(Name);
            var threshold = ProposedThreshold.HasValue ? ProposedThreshold.Value : 0;
            var oldThreshold = CriticalThreshold;
            int samplingDays = AlertTemplates.GetConfigValue<int>("PrintDuration.ProposedValues.SamplingDays");

            // Check if this is a DM alert (has carrierId)
            bool isDmAlert = Name?.Contains("DM Allocation", StringComparison.OrdinalIgnoreCase) == true;
            bool isAsos = Name?.Contains("ASOS", StringComparison.OrdinalIgnoreCase) == true;
            
            if (isDmAlert && !string.IsNullOrEmpty(carrierId))
            {
                // DM alert: use carrierId and DM allocation transaction
                string retailerFilter = isAsos ? "retailerName = 'ASOS'" : "retailerName != 'ASOS'";
                return $"SELECT average(duration), stddev(duration) as 'Deviation', {threshold} as 'New Threshold', {oldThreshold} as 'Old Threshold' FROM Transaction WHERE name = 'WebTransaction/SpringController/OctopusApiController/_allocateConsignment' AND {retailerFilter} AND carrierId = {carrierId} timeseries max since {samplingDays} days ago";
            }
            else if (!string.IsNullOrEmpty(carrierName))
            {
                // MPM alert: use carrierName and PrintParcel transaction
                return $"SELECT average(duration), stddev(duration) as 'Deviation', {threshold} as 'New Threshold', {oldThreshold} as 'Old Threshold' FROM Transaction WHERE PrintOperation like '%Create%' AND host like '%-{stack}-%' AND CarrierName = '{carrierName.Replace("'", "\\'")}' timeseries max since {samplingDays} days ago";
            }
            else
            {
                // Fallback: return a generic query if neither carrierName nor carrierId is found
                return $"SELECT average(duration), stddev(duration) as 'Deviation', {threshold} as 'New Threshold', {oldThreshold} as 'Old Threshold' FROM Transaction WHERE host like '%-{stack}-%' timeseries max since {samplingDays} days ago";
            }
        }
    }

    public class NRMetricsResult
    {
        public float MedianDuration { get; set; }
    }
}
