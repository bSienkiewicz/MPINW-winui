using System;
using Microsoft.UI.Xaml.Data;

namespace SupportTool.Features.Alerts.Converters
{
    public class FloatToString : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if ((float)value == 0f)
            {
                return string.Empty;
            }
            else
            {
                return value.ToString();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            try
            {
                if ((string)value == string.Empty)
                {
                    return 0f;
                }
                return float.Parse(value as string);
            }
            catch (Exception)
            {
                return 0f;
            }

        }
    }
}
