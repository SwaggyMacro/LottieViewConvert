using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace LottieViewConvert.Converters;

public class ProgressToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double progress)
        {
            return !(progress is 0 or 100);
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}