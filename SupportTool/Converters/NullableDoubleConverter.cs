using System;
using System.Globalization;
using Microsoft.UI.Xaml.Data;

namespace SupportTool.Converters
{
    public class NullableDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value?.ToString() ?? ""; // Convert null to empty string
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (string.IsNullOrWhiteSpace(value?.ToString()))
                return null; // Allow null values when input is empty

            if (double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
                return result;

            return null; // Prevents invalid input from causing an exception
        }
    }
}
