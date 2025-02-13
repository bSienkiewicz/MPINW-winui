using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SupportTool.Models
{
    public class AppCarrierItem
    {
        public string AppName { get; set; }
        public string CarrierName { get; set; }
        public bool HasPrintDurationAlert { get; set; }
        public bool HasErrorRateAlert { get; set; }
        public string ClientName => AppName.Split('.')[0].ToUpper();
    }
    public enum AlertType
    {
        PrintDuration,
        ErrorRate
    }

    public class AppCarrierGroup
    {
        public string AppName { get; set; }
        public List<CarrierInfo> Carriers { get; set; } = new();
    }

    public class CarrierInfo
    {
        public string CarrierName { get; set; }
        public bool HasPrintDurationAlert { get; set; }
        public bool HasErrorRateAlert { get; set; }
    }
}
