using System;
using System.Collections.Generic;
using System.Linq;
using DINBoard.Models;

namespace DINBoard.Services;

/// <summary>
/// Serwis walidacji elektrycznej - sprawdza obciążenie faz, przekroje przewodów, spadki napięcia.
/// </summary>
public interface IElectricalValidationService
{
    ValidationResult ValidateProject(Project project, IEnumerable<SymbolItem> symbols);
    PhaseLoadResult CalculatePhaseLoads(IEnumerable<SymbolItem> symbols, int voltage = 230);
    CableSizeValidation ValidateCableSize(double currentA, double crossSectionMm2, double lengthM, int voltage = 230, bool isThreePhase = false);
    double CalculateVoltageDrop(double currentA, double crossSectionMm2, double lengthM, int voltage = 230, bool isThreePhase = false);
}

public class ElectricalValidationService : IElectricalValidationService
{
    // Rezystywność miedzi w Ohm*mm²/m
    private const double CopperResistivity = 0.0175;

    // Maksymalny dopuszczalny spadek napięcia (%)
    private const double MaxVoltageDropPercent = 3.0;

    // Maksymalna dopuszczalna asymetria faz (%)
    private const double MaxPhaseImbalancePercent = 15.0;

    public ValidationResult ValidateProject(Project project, IEnumerable<SymbolItem> symbols)
    {
        var result = new ValidationResult();
        var symbolList = symbols.ToList();

        // 1. Walidacja obciążenia faz
        // Napięcie fazowe z konfiguracji projektu
        var lineVoltage = project?.PowerConfig?.Voltage ?? 400;
        var phaseVoltage = (int)(lineVoltage / Math.Sqrt(3));

        var phaseLoads = CalculatePhaseLoads(symbolList, phaseVoltage);
        if (phaseLoads.ImbalancePercent > MaxPhaseImbalancePercent)
        {
            result.Warnings.Add(new ValidationMessage
            {
                Code = "PHASE_IMBALANCE",
                Message = $"Asymetria obciążenia faz: {phaseLoads.ImbalancePercent:F1}% (max {MaxPhaseImbalancePercent}%)",
                Severity = ValidationSeverity.Warning,
                Details = $"L1: {phaseLoads.L1CurrentA:F1}A, L2: {phaseLoads.L2CurrentA:F1}A, L3: {phaseLoads.L3CurrentA:F1}A"
            });
        }

        // 2. Walidacja przekrojów przewodów
        foreach (var symbol in symbolList.Where(s => s.CableCrossSection > 0))
        {
            var current = CalculateCurrentFromPower(symbol.PowerW, symbol.Phase, phaseVoltage);
            var isThreePhase = symbol.Phase?.ToUpperInvariant() is "L1+L2+L3" or "3F";
            var cableValidation = ValidateCableSize(current, symbol.CableCrossSection, symbol.CableLength, phaseVoltage, isThreePhase);

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

            if (cableValidation.VoltageDropPercent > GetMaxVoltageDrop(symbol.CircuitType))
            {
                var limit = GetMaxVoltageDrop(symbol.CircuitType);
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

        // 3. Walidacja zabezpieczeń vs przekrój
        foreach (var symbol in symbolList.Where(s => !string.IsNullOrEmpty(s.ProtectionType)))
        {
            var protectionCurrent = ParseProtectionCurrent(symbol.ProtectionType);
            if (protectionCurrent > 0 && symbol.CableCrossSection > 0)
            {
                var maxCableCurrent = CommonHelpers.GetCableCapacity(symbol.CableCrossSection, CommonHelpers.CableAmpacityMethodB2);
                if (protectionCurrent > maxCableCurrent)
                {
                    result.Errors.Add(new ValidationMessage
                    {
                        Code = "PROTECTION_MISMATCH",
                        Message = $"Zabezpieczenie {symbol.ProtectionType} za duże dla przewodu {symbol.CableCrossSection}mm²",
                        Severity = ValidationSeverity.Error,
                        Details = $"Zabezpieczenie: {protectionCurrent}A, Max dla przewodu: {maxCableCurrent:F1}A",
                        SymbolId = symbol.Id
                    });
                }
            }
        }

        // 4. Sprawdzenie czy RCD chroni wszystkie obwody
        var circuitsWithoutRcd = symbolList
            .Where(s => s.Type?.Contains("MCB", StringComparison.OrdinalIgnoreCase) == true)
            .Where(s => string.IsNullOrEmpty(s.RcdSymbolId))
            .ToList();

        if (circuitsWithoutRcd.Count != 0)
        {
            result.Warnings.Add(new ValidationMessage
            {
                Code = "NO_RCD_PROTECTION",
                Message = $"{circuitsWithoutRcd.Count} obwodów bez ochrony różnicowoprądowej",
                Severity = ValidationSeverity.Warning,
                Details = string.Join(", ", circuitsWithoutRcd.Select(s => s.Label ?? s.Id).Take(5))
            });
        }

        // 5. Walidacja mocy vs zabezpieczenie główne
        if (project?.PowerConfig != null)
        {
            var totalPowerW = symbolList.Sum(s => s.PowerW);
            const double cosPhi = 0.9;

            // Moc czynna dopuszczalna przez zabezpieczenie główne: P = U · I · cosφ · (√3 dla 3F)
            var maxPowerW = project.PowerConfig.Voltage * project.PowerConfig.MainProtection * cosPhi
                           * (project.PowerConfig.Phases == 3 ? Math.Sqrt(3) : 1);

            if (totalPowerW > maxPowerW)
            {
                result.Errors.Add(new ValidationMessage
                {
                    Code = "MAIN_OVERLOAD",
                    Message = "Przekroczona moc zabezpieczenia głównego",
                    Severity = ValidationSeverity.Error,
                    Details = $"Moc zainstalowana: {totalPowerW / 1000:F1}kW, Max: {maxPowerW / 1000:F1}kW"
                });
            }
        }

        // 6. Walidacja brakującego rozłącznika głównego
        bool hasMainBreaker = symbolList.Any(s => 
            (s.Type ?? "").Contains("Rozłącznik", StringComparison.OrdinalIgnoreCase) ||
            (s.Type ?? "").Contains("Switch", StringComparison.OrdinalIgnoreCase) ||
            (s.Label ?? "").StartsWith("FR", StringComparison.OrdinalIgnoreCase) ||
            (s.VisualPath ?? "").Contains("switch_disconnector", StringComparison.OrdinalIgnoreCase));

        if (!hasMainBreaker)
        {
            result.Warnings.Add(new ValidationMessage
            {
                Code = "NO_MAIN_BREAKER",
                Message = "Brak rozłącznika głównego (FR) w rozdzielnicy",
                Severity = ValidationSeverity.Warning,
                Details = "Zalecane jest zainstalowanie głównego aparatu odcinającego zasilanie przed zabezpieczeniami instalacji."
            });
        }

        // 7. Walidacja przeciążenia wyłączników różnicowoprądowych (RCD)
        var rcds = symbolList.Where(s => 
            (s.Type ?? "").Contains("RCD", StringComparison.OrdinalIgnoreCase) || 
            (s.Type ?? "").Contains("Różnicow", StringComparison.OrdinalIgnoreCase)).ToList();

        var mainProtection = project?.PowerConfig?.MainProtection ?? 0;

        foreach (var rcd in rcds)
        {
            var rcdCurrent = ParseProtectionCurrent(rcd.ProtectionType); 
            if (rcdCurrent == 0) continue; 
            
            var mcbsUnderRcd = symbolList.Where(s => s.RcdSymbolId == rcd.Id).ToList();
            var sumCurrents = mcbsUnderRcd.Sum(m => ParseProtectionCurrent(m.ProtectionType));
            
            // Ostrzegamy o potencjalnym przeciążeniu jeśli główny bezpiecznik obiektu nie zabezpiecza prądu znamionowego RCD,
            // A suma "podrzędnych" esów przekracza znamionowy prąd RCD.
            if (sumCurrents > rcdCurrent && (mainProtection == 0 || mainProtection > rcdCurrent))
            {
                result.Warnings.Add(new ValidationMessage
                {
                    Code = "RCD_OVERLOAD",
                    Message = $"Suma prądów MCB ({sumCurrents}A) za RCD '{rcd.Label}' przekracza jego prąd znamionowy ({rcdCurrent}A)",
                    Severity = ValidationSeverity.Warning,
                    Details = $"Ryzyko przeciążenia styków RCD w skrajnym przypadku. Zastosuj zabezpieczenie przedlicznikowe max {rcdCurrent}A, RCD o wyższym In lub zmniejsz wartość zabezpieczeń za RCD.",
                    SymbolId = rcd.Id
                });
            }
        }

        result.IsValid = result.Errors.Count == 0;
        return result;
    }

    public PhaseLoadResult CalculatePhaseLoads(IEnumerable<SymbolItem> symbols, int voltage = 230)
    {
        var dist = PhaseDistributionCalculator.CalculateTotalDistribution(symbols);

        var result = new PhaseLoadResult
        {
            L1PowerW = dist.L1PowerW,
            L2PowerW = dist.L2PowerW,
            L3PowerW = dist.L3PowerW
        };

        result.L1CurrentA = PhaseDistributionCalculator.CalculateCurrent(result.L1PowerW, "L1", voltage);
        result.L2CurrentA = PhaseDistributionCalculator.CalculateCurrent(result.L2PowerW, "L2", voltage);
        result.L3CurrentA = PhaseDistributionCalculator.CalculateCurrent(result.L3PowerW, "L3", voltage);

        result.ImbalancePercent = PhaseDistributionCalculator.CalculateImbalancePercent(
            result.L1CurrentA, result.L2CurrentA, result.L3CurrentA);

        return result;
    }

    public CableSizeValidation ValidateCableSize(double currentA, double crossSectionMm2, double lengthM, int voltage = 230, bool isThreePhase = false)
    {
        var maxCurrent = CommonHelpers.GetCableCapacity(crossSectionMm2, CommonHelpers.CableAmpacityMethodB2);
        var voltageDrop = CalculateVoltageDrop(currentA, crossSectionMm2, lengthM, voltage, isThreePhase);

        return new CableSizeValidation
        {
            CrossSectionMm2 = crossSectionMm2,
            CurrentA = currentA,
            MaxCurrentA = maxCurrent,
            LengthM = lengthM,
            VoltageDropV = voltageDrop * voltage / 100.0,
            VoltageDropPercent = voltageDrop,
            IsValid = currentA <= maxCurrent,
            IsVoltageDropOk = voltageDrop <= MaxVoltageDropPercent
        };
    }

    public double CalculateVoltageDrop(double currentA, double crossSectionMm2, double lengthM, int voltage = 230, bool isThreePhase = false)
    {
        if (crossSectionMm2 <= 0 || voltage <= 0) return 0;

        // Spadek napięcia wg PN-HD 60364-5-52:
        // Jednofazowy: ΔU = 2 · I · L · ρ / S  (tam i z powrotem)
        // Trójfazowy:  ΔU = √3 · I · L · ρ / S
        var factor = isThreePhase ? Math.Sqrt(3) : 2.0;
        var resistanceOhm = factor * lengthM * CopperResistivity / crossSectionMm2;
        var voltageDropV = currentA * resistanceOhm;
        var voltageDropPercent = (voltageDropV / voltage) * 100.0;

        return voltageDropPercent;
    }

    private double CalculateCurrentFromPower(double powerW, string? phase, int voltage = 230)
    {
        if (powerW <= 0) return 0;

        // Zakładamy cos(phi) = 0.9
        const double cosPhi = 0.9;

        return phase?.ToUpperInvariant() switch
        {
            // Dla 3-faz (230V p-n, 400V p-p): P = sqrt(3) * 400 * I * cosPhi = 3 * 230 * I * cosPhi
            "L1+L2+L3" or "3F" => powerW / (3.0 * voltage * cosPhi),
            // 2-fazowe (indukcja): moc podzielona na 2 fazy
            "L1+L2" or "L1+L3" or "L2+L3" => powerW / (2.0 * voltage * cosPhi),
            _ => powerW / (voltage * cosPhi)
        };
    }

    /// <summary>
    /// Zwraca maksymalny dopuszczalny spadek napięcia wg PN-HD 60364-5-52:
    /// - Oświetlenie: 3%
    /// - Gniazda/Siła/Inne: 5%
    /// </summary>
    private double GetMaxVoltageDrop(string? circuitType)
    {
        return circuitType?.ToLowerInvariant() switch
        {
            "oświetlenie" => 3.0,
            _ => 5.0
        };
    }



    private int ParseProtectionCurrent(string? protectionType)
    {
        if (string.IsNullOrEmpty(protectionType)) return 0;

        // Parse "B16", "C20", "D32" etc. — wyciągnij liczbę po literze charakterystyki
        var match = System.Text.RegularExpressions.Regex.Match(protectionType, @"[BCD](\d{1,3})(?!\d)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var current))
            return current;

        // Fallback: spróbuj wyciągnąć samą liczbę (np. "16A")
        var numMatch = System.Text.RegularExpressions.Regex.Match(protectionType, @"(\d{1,3})A?");
        return numMatch.Success && int.TryParse(numMatch.Groups[1].Value, out var fallback) ? fallback : 0;
    }
}

#region Result Models

public class ValidationResult
{
    public bool IsValid { get; set; } = true;
    public System.Collections.ObjectModel.Collection<ValidationMessage> Errors { get; } = new();
    public System.Collections.ObjectModel.Collection<ValidationMessage> Warnings { get; } = new();
    public System.Collections.ObjectModel.Collection<ValidationMessage> Info { get; } = new();

    public IEnumerable<ValidationMessage> AllMessages => Errors.Concat(Warnings).Concat(Info);
}

public class ValidationMessage
{
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Details { get; set; }
    public ValidationSeverity Severity { get; set; }
    public string? SymbolId { get; set; }
}

public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}

public class PhaseLoadResult
{
    public double L1PowerW { get; set; }
    public double L2PowerW { get; set; }
    public double L3PowerW { get; set; }
    public double L1CurrentA { get; set; }
    public double L2CurrentA { get; set; }
    public double L3CurrentA { get; set; }
    public double ImbalancePercent { get; set; }

    public double TotalPowerW => L1PowerW + L2PowerW + L3PowerW;
    public double TotalCurrentA => L1CurrentA + L2CurrentA + L3CurrentA;
}

public class CableSizeValidation
{
    public double CrossSectionMm2 { get; set; }
    public double CurrentA { get; set; }
    public double MaxCurrentA { get; set; }
    public double LengthM { get; set; }
    public double VoltageDropV { get; set; }
    public double VoltageDropPercent { get; set; }
    public bool IsValid { get; set; }
    public bool IsVoltageDropOk { get; set; }
}

#endregion
