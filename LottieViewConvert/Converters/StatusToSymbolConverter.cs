using System;
using System.Globalization;
using Avalonia.Data.Converters;
using LottieViewConvert.Models;

namespace LottieViewConvert.Converters;

public class StatusToSymbolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ConversionStatus status)
        {
            return status switch
            {
                ConversionStatus.Pending => "⏳",
                ConversionStatus.Converting => "",
                ConversionStatus.Success => "✅",
                ConversionStatus.Failed => "❌",
                _ => ""
            };
        }
        return "";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StatusToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ConversionStatus status)
        {
            return status == ConversionStatus.Converting;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}