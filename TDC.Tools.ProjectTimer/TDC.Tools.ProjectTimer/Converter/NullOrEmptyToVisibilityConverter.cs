using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TDC.Tools.ProjectTimer.Converter
{
    /// <summary>
    /// Behaviour can be inverted by providing a value for parameter.
    /// </summary>
    public class NullOrEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || string.IsNullOrEmpty(value.ToString()))
                return (parameter == null) ?  Visibility.Collapsed : Visibility.Visible;

            return (parameter == null) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
