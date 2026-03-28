using System;
using System.Collections.Generic;
using DINBoard.Models;

namespace DINBoard.Services;

/// <summary>
/// Wspolne mapowanie wartosci formularza edycji na model symbolu.
/// Pozwala trzymac logike poza code-behind widokow.
/// </summary>
internal static class CircuitEditValueApplier
{
    public static void Apply(SymbolItem symbol, string key, string value)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        switch (key)
        {
            case "CircuitName":
                symbol.CircuitName = value;
                break;
            case "Location":
                symbol.Location = value;
                break;
            case "CircuitType":
                symbol.CircuitType = value;
                break;
            case "ProtectionType":
                symbol.ProtectionType = value;
                break;
            case "PowerW":
                if (double.TryParse(value, out var power))
                {
                    symbol.PowerW = power;
                }

                break;
            case "Phase":
                symbol.Phase = value;
                GetOrCreateParameters(symbol)["ManualPhase"] = "true";
                break;
            case "CableLength":
                if (double.TryParse(value, out var length))
                {
                    symbol.CableLength = length;
                }

                break;
            case "CableCrossSection":
                if (double.TryParse(value, out var cross))
                {
                    symbol.CableCrossSection = cross;
                }

                break;
            case "RcdPreset":
                ParseAndApplyRcdPreset(symbol, value);
                break;
            case "SpdPreset":
                ParseAndApplySpdPreset(symbol, value);
                break;
            case "Label":
                symbol.Label = value;
                break;
            case "FrType":
                symbol.FrType = value;
                break;
            case "FrRatedCurrent":
                symbol.FrRatedCurrent = value;
                break;
            case "PhaseIndicatorModel":
                symbol.PhaseIndicatorModel = value;
                break;
            case "PhaseIndicatorFuseRating":
                symbol.PhaseIndicatorFuseRating = value;
                break;
            case "ReferenceDesignation":
                symbol.ReferenceDesignation = value;
                break;
            case "CableDesig":
            case "CableType":
                GetOrCreateParameters(symbol)[key] = value;
                break;
        }
    }

    private static Dictionary<string, string> GetOrCreateParameters(SymbolItem symbol)
        => symbol.Parameters ??= new Dictionary<string, string>();

    private static void ParseAndApplyRcdPreset(SymbolItem symbol, string preset)
    {
        if (string.IsNullOrWhiteSpace(preset))
        {
            symbol.RcdRatedCurrent = 40;
            symbol.RcdResidualCurrent = 30;
            symbol.RcdType = "A";
            return;
        }

        var parts = preset.Split(' ');
        if (parts.Length < 3)
        {
            return;
        }

        var ampParts = parts[0].Split('/');
        if (ampParts.Length == 2)
        {
            if (int.TryParse(ampParts[0].Replace("A", "", StringComparison.Ordinal), out var rated))
            {
                symbol.RcdRatedCurrent = rated;
            }

            if (int.TryParse(ampParts[1].Replace("mA", "", StringComparison.Ordinal), out var residual))
            {
                symbol.RcdResidualCurrent = residual;
            }
        }

        symbol.RcdType = parts[2];
    }

    private static void ParseAndApplySpdPreset(SymbolItem symbol, string preset)
    {
        if (string.IsNullOrWhiteSpace(preset))
        {
            symbol.SpdType = "T1+T2";
            symbol.SpdVoltage = 275;
            symbol.SpdDischargeCurrent = 25;
            return;
        }

        var parts = preset.Split(' ');
        if (parts.Length < 3)
        {
            return;
        }

        symbol.SpdType = parts[0];
        if (int.TryParse(parts[1].Replace("V", "", StringComparison.Ordinal), out var voltage))
        {
            symbol.SpdVoltage = voltage;
        }

        if (int.TryParse(parts[2].Replace("kA", "", StringComparison.Ordinal).Replace(".5", "", StringComparison.Ordinal), out var discharge))
        {
            symbol.SpdDischargeCurrent = discharge;
        }
    }
}
