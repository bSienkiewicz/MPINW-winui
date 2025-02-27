using System;
using Microsoft.UI.Xaml.Data;

namespace SupportTool.Converters
{
    public class NullableIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (string.IsNullOrWhiteSpace(value?.ToString()))
                return null;

            if (int.TryParse(value.ToString(), out int result))
                return result;

            return null; // Prevents invalid conversions
        }
    }

}
