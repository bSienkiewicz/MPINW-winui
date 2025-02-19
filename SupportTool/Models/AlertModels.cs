using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SupportTool.Models
{
    public class AppCarrierItem : INotifyPropertyChanged
    {
        private string _appName;
        private string _carrierName;
        private bool _hasPrintDurationAlert = false;
        private bool _hasErrorRateAlert = false;

        public string AppName
        {
            get => _appName;
            set
            {
                _appName = value;
                OnPropertyChanged();
                OnPropertyChanged();
            }
        }

        public string CarrierName
        {
            get => _carrierName;
            set
            {
                _carrierName = value;
                OnPropertyChanged();
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
                _hasErrorRateAlert = value;
                OnPropertyChanged();
            }
        }

        public string ClientName => AppName?.Split('.')[0].ToUpper() ?? string.Empty;


        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum AlertType
    {
        PrintDuration,
        ErrorRate
    }
}
