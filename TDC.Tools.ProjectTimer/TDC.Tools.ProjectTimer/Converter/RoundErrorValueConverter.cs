using System;
using System.Globalization;
using System.Windows.Data;

namespace TDC.Tools.ProjectTimer.Converter
{
    public class RoundErrorValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double)
            {
                var retVal = ((double)value).ToString("F2");
                if ((double)value > 0) retVal = $"+{retVal}";
                return retVal;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}