using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace DINBoard.Services;

/// <summary>
/// Optymalizacje stringów używające Span&lt;T&gt; i stackalloc.
/// </summary>
public static class StringOptimizations
{
    /// <summary>
    /// Generuje ID przewodu bez alokacji stringa intermediate.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GenerateWireId(int fromId, int toId)
    {
        Span<char> buffer = stackalloc char[32];
        var pos = 0;
        
        "wire_".AsSpan().CopyTo(buffer[pos..]);
        pos += 5;
        
        if (fromId.TryFormat(buffer[pos..], out var written, provider: CultureInfo.InvariantCulture))
            pos += written;
        
        buffer[pos++] = '_';
        
        if (toId.TryFormat(buffer[pos..], out written, provider: CultureInfo.InvariantCulture))
            pos += written;
        
        return new string(buffer[..pos]);
    }

    /// <summary>
    /// Formatuje etykietę modułu.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string FormatModuleLabel(ReadOnlySpan<char> prefix, int number)
    {
        Span<char> buffer = stackalloc char[64];
        
        prefix.CopyTo(buffer);
        var pos = prefix.Length;
        
        if (number.TryFormat(buffer[pos..], out var written, provider: CultureInfo.InvariantCulture))
            pos += written;
        
        return new string(buffer[..pos]);
    }

    /// <summary>
    /// Formatuje wymiary modułu.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string FormatDimensions(int width, int height)
    {
        Span<char> buffer = stackalloc char[16];
        var pos = 0;
        
        if (width.TryFormat(buffer[pos..], out var written, provider: CultureInfo.InvariantCulture))
            pos += written;
        
        buffer[pos++] = 'x';
        
        if (height.TryFormat(buffer[pos..], out written, provider: CultureInfo.InvariantCulture))
            pos += written;
        
        return new string(buffer[..pos]);
    }

    /// <summary>
    /// Sprawdza czy string zawiera frazę (case-insensitive).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ContainsIgnoreCase(ReadOnlySpan<char> source, ReadOnlySpan<char> value)
    {
        return source.Contains(value, StringComparison.OrdinalIgnoreCase);
    }
}
