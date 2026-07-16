using System.Globalization;
using System.Windows.Data;
using LumenPomodoro.Models;

namespace LumenPomodoro.Converters;

public class StatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TimerMode mode)
        {
            return mode switch
            {
                TimerMode.Idle => "待开始",
                TimerMode.Focus => "专注中",
                TimerMode.Break => "休息中",
                TimerMode.Paused => "已暂停",
                _ => "待开始"
            };
        }
        return "待开始";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StatusToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TimerMode mode && parameter is string targetMode)
        {
            if (Enum.TryParse<TimerMode>(targetMode, out var parsedMode))
            {
                return mode == parsedMode ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
        }
        return System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class ZeroToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            return intValue > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }
        return System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class NonEmptyToVisibleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string text && !string.IsNullOrEmpty(text))
            return System.Windows.Visibility.Visible;
        return System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
