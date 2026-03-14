using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DINBoard.Converters;

public class BoolToBrushConverter : IValueConverter
{
    public IBrush TrueBrush { get; set; } = Brushes.Red;
    public IBrush FalseBrush { get; set; } = Brushes.Blue;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b)
        {
            return TrueBrush;
        }
        return FalseBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
