using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;

namespace DINBoard.Converters;

public class RecentProjectTimestampConverter : IValueConverter
{
    private static readonly CultureInfo PolishCulture = CultureInfo.GetCultureInfo("pl-PL");

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return string.Empty;
        }

        var lastWriteTime = File.GetLastWriteTime(path);
        var today = DateTime.Now.Date;

        if (lastWriteTime.Date == today)
        {
            return $"Dzisiaj, {lastWriteTime:HH:mm}";
        }

        if (lastWriteTime.Date == today.AddDays(-1))
        {
            return $"Wczoraj, {lastWriteTime:HH:mm}";
        }

        return lastWriteTime.ToString("dd.MM.yyyy, HH:mm", PolishCulture);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
