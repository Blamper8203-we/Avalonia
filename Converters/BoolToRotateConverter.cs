using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DINBoard.Converters;

/// <summary>
/// Konwerter bool na RotateTransform - dla animacji obrotu chevron
/// </summary>
public class BoolToRotateConverter : IValueConverter
{
    public static readonly BoolToRotateConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isExpanded)
        {
            // Rozwinięty = obrót 180 stopni (strzałka w górę)
            // Zwinięty = 0 stopni (strzałka w dół)
            return new RotateTransform(isExpanded ? 180 : 0);
        }
        return new RotateTransform(0);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
