using System;
using System.Collections.Generic;
using System.Linq;
using DINBoard.Models;

namespace DINBoard.Services;

public interface ICableSafetyValidationRule
{
    CableSafetyValidationResult Evaluate(IEnumerable<SymbolItem> symbols, int phaseVoltage);
}

public sealed class CableSafetyValidationRule : ICableSafetyValidationRule
{
    private readonly ICurrentFromPowerCalculator _currentFromPowerCalculator;
    private readonly ICableSizeValidationCalculator _cableSizeValidationCalculator;
    private readonly ICircuitVoltageDropLimitProvider _circuitVoltageDropLimitProvider;

    public CableSafetyValidationRule(
        ICurrentFromPowerCalculator currentFromPowerCalculator,
        ICableSizeValidationCalculator cableSizeValidationCalculator,
        ICircuitVoltageDropLimitProvider circuitVoltageDropLimitProvider)
    {
        _currentFromPowerCalculator = currentFromPowerCalculator ?? throw new ArgumentNullException(nameof(currentFromPowerCalculator));
        _cableSizeValidationCalculator = cableSizeValidationCalculator ?? throw new ArgumentNullException(nameof(cableSizeValidationCalculator));
        _circuitVoltageDropLimitProvider = circuitVoltageDropLimitProvider ?? throw new ArgumentNullException(nameof(circuitVoltageDropLimitProvider));
    }

    public CableSafetyValidationResult Evaluate(IEnumerable<SymbolItem> symbols, int phaseVoltage)
    {
        if (symbols == null)
        {
            throw new ArgumentNullException(nameof(symbols));
        }

        var result = new CableSafetyValidationResult();

        foreach (var symbol in symbols.Where(s => s.CableCrossSection > 0))
        {
            var current = _currentFromPowerCalculator.Calculate(symbol.PowerW, symbol.Phase, phaseVoltage);
            var isThreePhase = symbol.Phase?.ToUpperInvariant() is "L1+L2+L3" or "3F";
            var cableValidation = _cableSizeValidationCalculator.Validate(current, symbol.CableCrossSection, symbol.CableLength, phaseVoltage, isThreePhase);

            if (!cableValidation.IsValid)
            {
                result.Errors.Add(new ValidationMessage
                {
                    Code = "CABLE_OVERLOAD",
                    Message = $"Przewód {symbol.CableCrossSection}mm² przeciążony w obwodzie '{symbol.Label ?? symbol.Id}'",
                    Severity = ValidationSeverity.Error,
                    Details = $"Prąd: {current:F1}A, Max: {cableValidation.MaxCurrentA:F1}A",
                    SymbolId = symbol.Id
                });
            }

            var limit = _circuitVoltageDropLimitProvider.GetMaxVoltageDrop(symbol.CircuitType);
            if (cableValidation.VoltageDropPercent > limit)
            {
                result.Warnings.Add(new ValidationMessage
                {
                    Code = "VOLTAGE_DROP",
                    Message = $"Przekroczony spadek napięcia w obwodzie '{symbol.Label ?? symbol.Id}'",
                    Severity = ValidationSeverity.Warning,
                    Details = $"Spadek: {cableValidation.VoltageDropPercent:F2}% (max {limit}% dla {symbol.CircuitType})",
                    SymbolId = symbol.Id
                });
            }
        }

        return result;
    }
}

public sealed class CableSafetyValidationResult
{
    public List<ValidationMessage> Errors { get; } = new();
    public List<ValidationMessage> Warnings { get; } = new();
}
