using System;
using DINBoard.Models;

namespace DINBoard.Services;

/// <summary>
/// Centralizuje logikę aktualizacji parametrów symbolu na podstawie jego typu
/// i właściwości domenowych. Wcześniej była rozproszona w oknie głównym.
/// </summary>
public class SymbolParameterService
{
    private readonly IModuleTypeService _moduleTypeService;

    public SymbolParameterService(IModuleTypeService moduleTypeService)
    {
        _moduleTypeService = moduleTypeService ?? throw new ArgumentNullException(nameof(moduleTypeService));
    }

    /// <summary>
    /// Uaktualnia słownik <see cref="SymbolItem.Parameters"/> tak, aby odzwierciedlał
    /// aktualne właściwości symbolu (etykieta, zabezpieczenie, moc).
    /// </summary>
    public void UpdateSymbolParameters(SymbolItem symbol)
    {
        if (symbol == null)
        {
            return;
        }

        var moduleType = _moduleTypeService.GetModuleType(symbol);

        string label;
        if (moduleType == ModuleType.Switch)
        {
            label = symbol.FrType ?? symbol.Label ?? string.Empty;
        }
        else if (moduleType == ModuleType.PhaseIndicator)
        {
            label = symbol.PhaseIndicatorModel ?? symbol.Label ?? string.Empty;
        }
        else
        {
            label = symbol.ProtectionType ?? symbol.Label ?? string.Empty;
        }

        SetOrAdd(symbol, "LABEL", label);

        string protect = symbol.ProtectionType ?? string.Empty;

        // Zachowujemy dotychczasowe zachowanie oparte na nazwie typu.
        if (symbol.Type?.Contains("RCD", StringComparison.OrdinalIgnoreCase) == true)
        {
            protect = $"{symbol.RcdRatedCurrent}A";
        }

        SetOrAdd(symbol, "CURRENT", protect);

        string power = symbol.PowerW.ToString();
        SetOrAdd(symbol, "POWER", power);
    }

    private static void SetOrAdd(SymbolItem symbol, string key, string value)
    {
        if (symbol.Parameters == null)
        {
            return;
        }

        if (symbol.Parameters.ContainsKey(key))
        {
            symbol.Parameters[key] = value;
        }
        else
        {
            symbol.Parameters.Add(key, value);
        }
    }
}

