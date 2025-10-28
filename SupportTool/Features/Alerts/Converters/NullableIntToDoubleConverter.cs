using System;
using Microsoft.UI.Xaml.Data;

namespace SupportTool.Features.Alerts.Converters
{
    public class NullableIntToDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value == null ? double.NaN : System.Convert.ToDouble(value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is double doubleValue)
            {
                return double.IsNaN(doubleValue) ? (int?)null : System.Convert.ToInt32(doubleValue);
            }
            return null;
        }
    }
}
