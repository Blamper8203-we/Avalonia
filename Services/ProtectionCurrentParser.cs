using System.Text.RegularExpressions;

namespace DINBoard.Services;

public interface IProtectionCurrentParser
{
    int Parse(string? protectionType);
}

public sealed class ProtectionCurrentParser : IProtectionCurrentParser
{
    public int Parse(string? protectionType)
    {
        if (string.IsNullOrEmpty(protectionType))
        {
            return 0;
        }

        // Parse "B16", "C20", "D32" etc. - extract number after characteristic letter.
        var match = Regex.Match(protectionType, @"[BCD](\d{1,3})(?!\d)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var current))
        {
            return current;
        }

        // Fallback: extract any number (for example "16A").
        var numMatch = Regex.Match(protectionType, @"(\d{1,3})A?");
        return numMatch.Success && int.TryParse(numMatch.Groups[1].Value, out var fallback) ? fallback : 0;
    }
}
