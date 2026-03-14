using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using SkiaSharp;

namespace DINBoard.Constants;

/// <summary>
/// Wspólne stałe i presety używane w dialogach edycji
/// </summary>
public static class DialogConstants
{
    /// <summary>
    /// Presety zabezpieczeń MCB (B, C, D charakterystyka)
    /// </summary>
    public static readonly string[] ProtectionPresets = new[]
    {
        "B6", "B10", "B13", "B16", "B20", "B25", "B32", "B40", "B50", "B63",
        "C6", "C10", "C13", "C16", "C20", "C25", "C32", "C40", "C50", "C63",
        "D6", "D10", "D13", "D16", "D20", "D25", "D32", "D40", "D50", "D63"
    };

    /// <summary>
    /// Presety RCD (wyłączników różnicowoprądowych)
    /// </summary>
    public static readonly string[] RcdPresets = new[]
    {
        "25A/30mA Typ A",
        "40A/30mA Typ A",
        "40A/30mA Typ AC",
        "63A/30mA Typ A",
        "63A/30mA Typ AC",
        "40A/100mA Typ A",
        "63A/100mA Typ A",
        "40A/300mA Typ S",
        "63A/300mA Typ S",
        "40A/30mA Typ B",
        "25A/30mA Typ F"
    };

    /// <summary>
    /// Presety SPD (ograniczników przepięć)
    /// </summary>
    public static readonly string[] SpdPresets = new[]
    {
        "T1+T2 275V 25kA",
        "T1+T2 275V 12.5kA",
        "T1 320V 25kA",
        "T1 320V 50kA",
        "T2 275V 20kA",
        "T2 275V 40kA",
        "T2+T3 275V 10kA",
        "T1+T2 385V 25kA",
        "T1+T2 385V 50kA"
    };

    /// <summary>
    /// Presety typów FR (rozłączników głównych)
    /// </summary>
    public static readonly string[] FrPresets = new[]
    {
        "32",
        "40",
        "63",
        "100"
    };

    /// <summary>
    /// Presety modeli kontrolek faz
    /// </summary>
    public static readonly string[] PhaseIndicatorModelPresets = new[]
    {
        "3 lampki z bezpiecznikiem",
        "3 lampki bez bezpiecznika",
        "Lampka pojedyncza L1",
        "Lampka pojedyncza L2",
        "Lampka pojedyncza L3"
    };

    /// <summary>
    /// Presety bezpieczników kontrolek faz
    /// </summary>
    public static readonly string[] PhaseIndicatorFusePresets = new[]
    {
        "2A gG",
        "4A gG",
        "6A gG",
        "10A gG"
    };

    /// <summary>
    /// Typy obwodów — wpływa na limity spadku napięcia
    /// </summary>
    public static readonly string[] CircuitTypePresets = new[]
    {
        "Oświetlenie",
        "Gniazdo",
        "Siła",
        "Inne"
    };

    /// <summary>
    /// Zwraca placeholder dla danego pola
    /// </summary>
    public static string GetPlaceholder(string key) => key switch
    {
        "CircuitName" => "np. Oświetlenie salon",
        "Location" => "np. Piętro 1, Kuchnia",
        "ProtectionType" => "np. B10, B16, C16, C20, C32",
        "PowerW" => "np. 100, 500, 1000, 2000",
        "CableLength" => "np. 5, 10, 15, 20",
        "CableCrossSection" => "np. 1.5, 2.5, 4.0, 6.0",
        _ => ""
    };

    /// <summary>
    /// Mapuje typ modułu na jego nazwę wyświetlaną
    /// </summary>
    public static string GetModuleTypeName(string? moduleType) => moduleType?.ToUpperInvariant() switch
    {
        "MCB" => "Wyłącznik nadprądowy (MCB)",
        "RCD" => "Wyłącznik różnicowoprądowy (RCD)",
        "SPD" => "Ogranicznik przepięć (SPD)",
        "FR" => "Rozłącznik główny (FR)",
        "KONTROLKIFAZ" => "Kontrolki faz",
        "TERMINAL" => "Zaciski",
        _ => moduleType ?? "Nieznany"
    };

    /// <summary>
    /// Kolory faz elektrycznych
    /// </summary>
    public static class PhaseColors
    {
        public static string L1 => ResolveColor("AccentBlue", "#3B82F6");
        public static string L2 => ResolveColor("AccentOrange", "#D97706");
        public static string L3 => ResolveColor("AccentRed", "#EF4444");
        public static string Neutral => ResolveColor("AccentBluePressed", "#1D4ED8");
        public static string Ground => ResolveColor("TextSecondary", "#6B7280");

        private static string ResolveColor(string key, string fallbackHex)
        {
            if (Application.Current?.Resources.TryGetValue(key, out var resource) == true
                && resource is SolidColorBrush brush)
            {
                return brush.Color.ToString();
            }

            return fallbackHex;
        }
    }


    /// <summary>
    /// Typy pól edycyjnych
    /// </summary>
    public enum FieldType
    {
        Text,
        Number,
        ComboBox,
        Boolean
    }

    /// <summary>
    /// Informacje o polu edycyjnym
    /// </summary>
    public class FieldDefinition
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public FieldType Type { get; set; }
        public object? DefaultValue { get; set; }
        public string[]? Options { get; set; }
        public string Placeholder { get; set; } = "";

        public FieldDefinition(string key, string label, FieldType type)
        {
            Key = key;
            Label = label;
            Type = type;
            Placeholder = GetPlaceholder(key);
        }
    }
}

public static class WirePalette
{
    public const string L1Hex = "#8B4513";
    public const string L2Hex = "#4a4a4a";
    public const string L3Hex = "#808080";
    public const string NHex = "#0066CC";
    public const string PEHex = "#FFCC00";

    public static string GetHex(string? phase)
    {
        return phase?.ToUpperInvariant() switch
        {
            "L1" => L1Hex,
            "L2" => L2Hex,
            "L3" => L3Hex,
            "N" => NHex,
            "PE" => PEHex,
            "L1+L2+L3" or "3F" => L1Hex,
            _ => L1Hex
        };
    }

    public static Color GetAvalonia(string? phase)
    {
        return Color.Parse(GetHex(phase));
    }

    public static SKColor GetSkia(string? phase)
    {
        return SKColor.Parse(GetHex(phase));
    }
}

public static class AppDefaults
{
    public const double DinRailScale = 0.20;
    public const double DinRailMaxScale = 0.25;
    public const double DinRailSnapDistance = 80.0;
    public const double DinRailSameRailTolerance = 100.0;
    public const double ModuleUnitWidth = 18.0;
    public const double SnapGapUnit = 0.0555555556;
    public const double SnapThreshold = 30.0;
    public const double SnapOverlapTolerance = 2.0;
}
