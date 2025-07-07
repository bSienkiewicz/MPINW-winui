using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using SupportTool.Helpers;
using SupportTool.Models;

namespace SupportTool.Converters
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
            if (value == null || !(value is double d))
                return "–";
            return d.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is string s && double.TryParse(s, out double result))
                return result;
            return null;
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
