using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LumenPomodoro.Converters;

public class BarRatioToWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 2 && values[0] is double ratio && values[1] is double maxWidth)
        {
            return Math.Max(4, ratio * maxWidth);
        }
        return 4.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
