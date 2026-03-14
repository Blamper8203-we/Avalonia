using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DINBoard.Converters;

public class ColorToBrushConverter : IValueConverter
{
    public static readonly ColorToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Color color)
        {
            return new SolidColorBrush(color);
        }
        if (value is string hex && !string.IsNullOrEmpty(hex))
        {
            try { return SolidColorBrush.Parse(hex); } catch { }
        }
        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
