using Avalonia;
using Avalonia.Controls;

namespace DINBoard.Helpers;

public static class LocalizationHelper
{
    public static string GetString(string key)
    {
        if (Application.Current != null && Application.Current.TryFindResource(key, out var res))
        {
            return res?.ToString() ?? key;
        }
        return key;
    }
}
