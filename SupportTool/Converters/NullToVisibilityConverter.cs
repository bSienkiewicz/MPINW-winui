using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using SupportTool.Helpers;

namespace SupportTool.Converters
{
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return Visibility.Collapsed;

            // Check if it's a NrqlAlert and if it has any meaningful data
            if (value is NrqlAlert alert)
            {
                // Only show if the alert has a name (meaning it's a real alert, not an empty one)
                return string.IsNullOrWhiteSpace(alert.Name) ? Visibility.Collapsed : Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}