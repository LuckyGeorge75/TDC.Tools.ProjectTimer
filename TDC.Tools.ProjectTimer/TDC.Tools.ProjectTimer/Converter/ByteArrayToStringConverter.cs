using System;
using System.Windows.Data;

namespace TDC.Tools.ProjectTimer.Converter
{
    public class ByteArrayToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var retVal = String.Empty;
            var bytes = value as byte[];
            if (bytes != null)
            {
                foreach (var b in bytes)
                {
                    retVal += $"{b:X2}";
                }
            }
            return retVal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return !(bool)value;
        }
    }
}
