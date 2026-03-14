using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace DINBoard.Converters;

public class InverseBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }
        return false;
    }
}

public class BoolToZIndexConverter : IValueConverter
{
    public int TrueValue { get; set; } = 5;
    public int FalseValue { get; set; } = 10;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? TrueValue : FalseValue;
        }
        return FalseValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int i)
        {
            return i == TrueValue;
        }
        return false;
    }
}
