using System;
using System.Globalization;
using Microsoft.UI.Xaml.Data;

namespace SupportTool.Features.Alerts.Converters
{
    public class NullableIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double doubleValue)
            {
                return Math.Round(doubleValue).ToString();
            }
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (string.IsNullOrWhiteSpace(value?.ToString()))
                return null;

            string input = value.ToString().Trim();

            if (int.TryParse(input, out int intResult))
                return intResult;

            if (double.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out double doubleResult))
                return (int)Math.Round(doubleResult);

            return null; // Prevents invalid conversions
        }
    }

}
