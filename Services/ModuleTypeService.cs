using System;
using System.Text.RegularExpressions;
using DINBoard.Models;

namespace DINBoard.Services;

/// <summary>
/// Implementacja serwisu rozpoznawania typów modułów elektrycznych.
/// Centralizuje logikę wcześniej rozproszoną w MainViewModel, GroupViewModel, 
/// SchematicDragDropController i PdfExportService.
/// </summary>
public class ModuleTypeService : IModuleTypeService
{
    // Regex do wykrywania liczby biegunów: 1P, 2P, 3P, 4P, 1-P, 2-P, etc.
    private static readonly Regex PoleCountRegex = new(@"(\d)\s*[-]?\s*[Pp]", RegexOptions.Compiled);

    // Regex dla serii S30x (np. S301, S 302, S-303) - popularne oznaczenia
    private static readonly Regex SSeriesRegex = new(@"[Ss]\s*[-]?\s*30(\d)", RegexOptions.Compiled);

    /// <inheritdoc/>
    public ModuleType GetModuleType(SymbolItem? symbol)
    {
        if (symbol == null) return ModuleType.Unknown;

        var path = symbol.VisualPath ?? "";
        var type = symbol.Type ?? "";

        if (ContainsKeyword(path, type, "RCD")) return ModuleType.RCD;
        if (ContainsKeyword(path, type, "MCB")) return ModuleType.MCB;
        if (SSeriesRegex.IsMatch(path) || SSeriesRegex.IsMatch(type)) return ModuleType.MCB;
        if (ContainsKeyword(path, type, "SPD")) return ModuleType.SPD;
        if (ContainsKeyword(path, type, "FR") || ContainsKeyword(path, type, "Switch") || ContainsKeyword(path, type, "Rozłącznik") || ContainsKeyword(path, type, "Isolator")) return ModuleType.Switch;
        if (ContainsKeyword(path, type, "kontrolk") || ContainsKeyword(path, type, "indicator") || ContainsKeyword(path, type, "lampka") || ContainsKeyword(path, type, "sygnalizat") || ContainsKeyword(path, type, "KontrolkiFaz")) return ModuleType.PhaseIndicator;
        if (ContainsKeyword(path, type, "blok") || ContainsKeyword(path, type, "block") || ContainsKeyword(path, type, "rozdz")) return ModuleType.DistributionBlock;

        // Fallback - jeśli Type jest ustawiony ale nie pasuje do znanych
        if (!string.IsNullOrEmpty(type)) return ModuleType.Other;

        return ModuleType.Unknown;
    }

    /// <inheritdoc/>
    public bool IsRcd(SymbolItem? symbol) => GetModuleType(symbol) == ModuleType.RCD;

    /// <inheritdoc/>
    public bool IsMcb(SymbolItem? symbol) => GetModuleType(symbol) == ModuleType.MCB;

    /// <inheritdoc/>
    public bool IsSpd(SymbolItem? symbol) => GetModuleType(symbol) == ModuleType.SPD;

    /// <inheritdoc/>
    public bool IsSwitch(SymbolItem? symbol) => GetModuleType(symbol) == ModuleType.Switch;

    /// <inheritdoc/>
    public bool IsPhaseIndicator(SymbolItem? symbol) => GetModuleType(symbol) == ModuleType.PhaseIndicator;

    /// <inheritdoc/>
    public bool IsDistributionBlock(SymbolItem? symbol) => GetModuleType(symbol) == ModuleType.DistributionBlock;

    public bool IsPowerBusbar(SymbolItem? symbol)
    {
        if (symbol == null) return false;
        var label = symbol.Label ?? "";
        var type = symbol.Type ?? "";
        var path = symbol.VisualPath ?? "";
        if (label.Contains("Szyna prądowa", StringComparison.OrdinalIgnoreCase)) return true;
        if (type.Contains("Szyna", StringComparison.OrdinalIgnoreCase)) return true;
        if (path.Contains("szyna prądowa", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <inheritdoc/>
    public string GetModuleTypeName(SymbolItem? symbol)
    {
        return GetModuleType(symbol) switch
        {
            ModuleType.RCD => "RCD",
            ModuleType.MCB => "MCB",
            ModuleType.SPD => "SPD",
            ModuleType.Switch => "FR",
            ModuleType.PhaseIndicator => "KontrolkiFaz",
            ModuleType.DistributionBlock => "Blok",
            ModuleType.Other => symbol?.Type ?? "?",
            _ => "?"
        };
    }

    /// <inheritdoc/>
    public ModulePoleCount GetPoleCount(SymbolItem? symbol)
    {
        if (symbol == null) return ModulePoleCount.Unknown;

        // 1. Try string based detection first
        var poleCount = GetPoleCount(symbol.VisualPath, symbol.Type);
        if (poleCount != ModulePoleCount.Unknown) return poleCount;

        // 2. Fallback: Aspect Ratio (Width / Height)
        // Standard Module: Height ~90 units
        // 1P ~18 units (Ratio 0.2)
        // 2P ~36 units (Ratio 0.4)
        // 3P ~54 units (Ratio 0.6)
        // 4P ~72 units (Ratio 0.8)

        if (symbol.Height > 0)
        {
            double ratio = symbol.Width / symbol.Height;

            // Direct Width check (assuming standard ~18mm per module)
            // Use width ranges relative to height or raw units if scale is known? 
            // Better to rely on Ratio as it handles Zoom/Scale, but Width ratio is robust.

            if (ratio < 0.30) return ModulePoleCount.P1;     // e.g. 18/90 = 0.2
            if (ratio < 0.55) return ModulePoleCount.P2;     // e.g. 36/90 = 0.4 (Increased threshold slightly to catch 0.5 boundary cases)
            if (ratio < 0.75) return ModulePoleCount.P3;     // e.g. 54/90 = 0.6
            return ModulePoleCount.P4;                       // e.g. 72/90 = 0.8
        }

        return ModulePoleCount.Unknown;
    }

    /// <inheritdoc/>
    public ModulePoleCount GetPoleCount(string? visualPath, string? type)
    {
        var path = visualPath ?? "";
        var typeName = type ?? "";
        var combined = $"{path} {typeName}";

        // Szukaj wzorca np. "3P", "3-P", "3 P", "MCB_3P", "RCD-4P"
        var match = PoleCountRegex.Match(combined);
        if (match.Success && int.TryParse(match.Groups[1].Value, out int poles))
        {
            return poles switch
            {
                1 => ModulePoleCount.P1,
                2 => ModulePoleCount.P2,
                3 => ModulePoleCount.P3,
                4 => ModulePoleCount.P4,
                _ => ModulePoleCount.Unknown
            };
        }

        // Sprawdź serię S30x (S301...S304)
        var sMatch = SSeriesRegex.Match(combined);
        if (sMatch.Success && int.TryParse(sMatch.Groups[1].Value, out int sPoles))
        {
            return sPoles switch
            {
                1 => ModulePoleCount.P1,
                2 => ModulePoleCount.P2,
                3 => ModulePoleCount.P3,
                4 => ModulePoleCount.P4,
                _ => ModulePoleCount.Unknown
            };
        }

        // Fallback: Jeśli nazwa zawiera "4P" bez spacji
        if (combined.Contains("4P", StringComparison.OrdinalIgnoreCase)) return ModulePoleCount.P4;
        if (combined.Contains("3P", StringComparison.OrdinalIgnoreCase)) return ModulePoleCount.P3;
        if (combined.Contains("2P", StringComparison.OrdinalIgnoreCase)) return ModulePoleCount.P2;
        if (combined.Contains("1P", StringComparison.OrdinalIgnoreCase)) return ModulePoleCount.P1;

        return ModulePoleCount.Unknown;
    }

    /// <inheritdoc/>
    public string GetDefaultPhaseForPoleCount(ModulePoleCount poleCount)
    {
        return poleCount switch
        {
            ModulePoleCount.P3 => "L1+L2+L3",  // 3-biegunowy = 3 fazy
            ModulePoleCount.P4 => "L1+L2+L3",  // 4-biegunowy = 3 fazy + N
            ModulePoleCount.P2 => "L1+L2",      // 2-biegunowy = 2 fazy
            ModulePoleCount.P1 => "L1",         // 1-biegunowy = 1 faza (domyślnie L1)
            _ => "L1"                           // Domyślnie L1
        };
    }

    /// <inheritdoc/>
    public bool IsThreePhase(SymbolItem? symbol)
    {
        var poleCount = GetPoleCount(symbol);
        return poleCount == ModulePoleCount.P3 || poleCount == ModulePoleCount.P4;
    }

    /// <summary>
    /// Sprawdza czy ścieżka lub typ zawiera podane słowo kluczowe (case-insensitive)
    /// </summary>
    private static bool ContainsKeyword(string path, string type, string keyword)
    {
        // Dla bardzo krótkich słów kluczowych (np. "FR") wymagamy dopasowania słowa lub separatora,
        // aby uniknąć błędów typu "TRANSFORMER" pasujący do "FR".
        if (keyword.Length <= 2)
        {
            var pattern = $@"\b{Regex.Escape(keyword)}\b|[{Regex.Escape(keyword)}]_|_{Regex.Escape(keyword)}";
            return Regex.IsMatch(path, pattern, RegexOptions.IgnoreCase)
                || Regex.IsMatch(type, pattern, RegexOptions.IgnoreCase);
        }

        return path.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            || type.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }
}
