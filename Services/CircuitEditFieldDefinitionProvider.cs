using System;
using System.Collections.Generic;
using DINBoard.Constants;
using DINBoard.Models;

namespace DINBoard.Services;

internal enum CircuitEditFieldKind
{
    Text,
    Number,
    Combo
}

internal sealed record CircuitEditFieldDefinition(
    string Key,
    string Label,
    CircuitEditFieldKind Kind,
    string TextValue,
    double NumberValue,
    string[] Options)
{
    public static CircuitEditFieldDefinition Text(string key, string label, string value)
        => new(key, label, CircuitEditFieldKind.Text, value, 0, Array.Empty<string>());

    public static CircuitEditFieldDefinition Number(string key, string label, double value)
        => new(key, label, CircuitEditFieldKind.Number, string.Empty, value, Array.Empty<string>());

    public static CircuitEditFieldDefinition Combo(string key, string label, string value, string[] options)
        => new(key, label, CircuitEditFieldKind.Combo, value, 0, options);
}

internal static class CircuitEditFieldDefinitionProvider
{
    public static IReadOnlyList<CircuitEditFieldDefinition> GetFields(SymbolItem symbol, IModuleTypeService moduleTypeService)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(moduleTypeService);

        return moduleTypeService.GetModuleType(symbol) switch
        {
            ModuleType.Switch => CreateFrFields(symbol),
            ModuleType.PhaseIndicator => CreatePhaseIndicatorFields(symbol),
            ModuleType.RCD => CreateRcdFields(symbol),
            ModuleType.SPD => CreateSpdFields(symbol),
            _ => CreateMcbFields(symbol, moduleTypeService.GetPoleCount(symbol))
        };
    }

    private static IReadOnlyList<CircuitEditFieldDefinition> CreateFrFields(SymbolItem symbol)
        => new[]
        {
            CircuitEditFieldDefinition.Text("ReferenceDesignation", "Oznaczenie", symbol.ReferenceDesignation ?? string.Empty),
            CircuitEditFieldDefinition.Text("Label", "Etykieta", symbol.Label ?? string.Empty),
            CircuitEditFieldDefinition.Combo("FrType", "Typ FR", symbol.FrType ?? "63", DialogConstants.FrPresets),
            CircuitEditFieldDefinition.Text("FrRatedCurrent", "Prad znamionowy", symbol.FrRatedCurrent ?? "63A")
        };

    private static IReadOnlyList<CircuitEditFieldDefinition> CreatePhaseIndicatorFields(SymbolItem symbol)
        => new[]
        {
            CircuitEditFieldDefinition.Text("ReferenceDesignation", "Oznaczenie", symbol.ReferenceDesignation ?? string.Empty),
            CircuitEditFieldDefinition.Text("Label", "Etykieta", symbol.Label ?? string.Empty),
            CircuitEditFieldDefinition.Combo(
                "PhaseIndicatorModel",
                "Model",
                symbol.PhaseIndicatorModel ?? "3 lampki z bezpiecznikiem",
                DialogConstants.PhaseIndicatorModelPresets),
            CircuitEditFieldDefinition.Combo(
                "PhaseIndicatorFuseRating",
                "Bezpiecznik",
                symbol.PhaseIndicatorFuseRating ?? "2A gG",
                DialogConstants.PhaseIndicatorFusePresets)
        };

    private static IReadOnlyList<CircuitEditFieldDefinition> CreateRcdFields(SymbolItem symbol)
    {
        string currentRcdValue = $"{symbol.RcdRatedCurrent}A/{symbol.RcdResidualCurrent}mA Typ {symbol.RcdType}";
        return new[]
        {
            CircuitEditFieldDefinition.Text("ReferenceDesignation", "Oznaczenie", symbol.ReferenceDesignation ?? string.Empty),
            CircuitEditFieldDefinition.Combo("RcdPreset", "Typ RCD", currentRcdValue, DialogConstants.RcdPresets)
        };
    }

    private static IReadOnlyList<CircuitEditFieldDefinition> CreateSpdFields(SymbolItem symbol)
    {
        string currentSpdValue = $"{symbol.SpdType} {symbol.SpdVoltage}V {symbol.SpdDischargeCurrent}kA";
        return new[]
        {
            CircuitEditFieldDefinition.Text("ReferenceDesignation", "Oznaczenie", symbol.ReferenceDesignation ?? string.Empty),
            CircuitEditFieldDefinition.Combo("SpdPreset", "Typ SPD", currentSpdValue, DialogConstants.SpdPresets)
        };
    }

    private static IReadOnlyList<CircuitEditFieldDefinition> CreateMcbFields(SymbolItem symbol, ModulePoleCount poleCount)
        => new[]
        {
            CircuitEditFieldDefinition.Text("ReferenceDesignation", "Oznaczenie", symbol.ReferenceDesignation ?? string.Empty),
            CircuitEditFieldDefinition.Text("CircuitName", "Nazwa obwodu", symbol.CircuitName ?? string.Empty),
            CircuitEditFieldDefinition.Text("Location", "Lokalizacja", symbol.Location ?? string.Empty),
            CircuitEditFieldDefinition.Combo("CircuitType", "Typ obwodu", symbol.CircuitType ?? "Gniazdo", DialogConstants.CircuitTypePresets),
            CircuitEditFieldDefinition.Combo("ProtectionType", "Zabezpieczenie", symbol.ProtectionType ?? "B16", DialogConstants.ProtectionPresets),
            CircuitEditFieldDefinition.Number("PowerW", "Moc (W)", symbol.PowerW),
            CircuitEditFieldDefinition.Combo("Phase", "Faza", GetDisplayPhase(symbol.Phase), GetPhaseOptions(poleCount)),
            CircuitEditFieldDefinition.Text("CableDesig", "Oznaczenie kabla", symbol.Parameters?.GetValueOrDefault("CableDesig", string.Empty) ?? string.Empty),
            CircuitEditFieldDefinition.Text("CableType", "Typ kabla", symbol.Parameters?.GetValueOrDefault("CableType", string.Empty) ?? string.Empty),
            CircuitEditFieldDefinition.Number("CableLength", "Dlugosc kabla (m)", symbol.CableLength),
            CircuitEditFieldDefinition.Number("CableCrossSection", "Przekroj (mm2)", symbol.CableCrossSection)
        };

    private static string GetDisplayPhase(string? phase)
        => phase is null or "" or "pending" or "PENDING" ? "L1" : phase;

    private static string[] GetPhaseOptions(ModulePoleCount poleCount)
        => poleCount switch
        {
            ModulePoleCount.P3 or ModulePoleCount.P4 => new[] { "L1+L2+L3", "L1", "L2", "L3", "L1+L2", "L2+L3", "L3+L1" },
            ModulePoleCount.P2 => new[] { "L1+L2", "L2+L3", "L3+L1", "L1", "L2", "L3", "L1+L2+L3" },
            _ => new[] { "L1", "L2", "L3", "L1+L2", "L2+L3", "L3+L1", "L1+L2+L3" }
        };
}
