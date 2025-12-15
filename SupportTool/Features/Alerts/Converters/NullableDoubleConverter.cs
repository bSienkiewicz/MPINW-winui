using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using SupportTool.Features.Alerts.Helpers;
using SupportTool.Features.Alerts.Models;

namespace SupportTool.Features.Alerts.Converters
{
    public class NullableDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (string.IsNullOrWhiteSpace(value?.ToString()))
                return null;

            string input = value.ToString().Trim().Replace(',', '.');

            if (double.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
                return result;

            return DependencyProperty.UnsetValue;
        }
    }

    public class NullableDoubleToDashConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // Return empty string when null so placeholder shows
            if (value == null || !(value is double d))
                return string.Empty;
            return d.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            // Return null if empty or invalid, so ProposedThreshold becomes null
            if (value is string s)
            {
                s = s.Trim();
                if (string.IsNullOrEmpty(s))
                    return null;
                
                // Try parsing with invariant culture
                if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
                    return result;
            }
            return null;
        }
    }

    public class NullableDoubleToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
                return false;

            if (value is double d && !double.IsNaN(d))
                return true;

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class ThresholdDifferenceToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (parameter is NrqlAlert alert && alert.ProposedThreshold.HasValue)
            {
                double diff = Math.Abs(alert.CriticalThreshold - alert.ProposedThreshold.Value);
                double threshold = AlertTemplates.GetThresholdDifference();
                if (diff >= threshold)
                {
                    return new SolidColorBrush(Microsoft.UI.Colors.Red);
                }
            }
            return new SolidColorBrush(Microsoft.UI.Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
