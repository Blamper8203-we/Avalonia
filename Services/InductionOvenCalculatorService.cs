using System;
using System.Collections.Generic;
using System.Linq;

namespace DINBoard.Services;

public enum InductionCableInstallationMethod
{
    B2 = 0,
    C = 1
}

public enum InductionConductorMaterial
{
    Copper = 0,
    Aluminum = 1
}

public enum InductionEarthingSystem
{
    TN = 0,
    TT = 1
}

public enum InductionMcbCurve
{
    B = 0,
    C = 1,
    D = 2
}

public sealed record InductionOvenCalculatorInput(
    double InductionPowerW,
    double CableLengthM,
    double CableCrossSectionMm2,
    double CosPhi = 0.9,
    double PhaseVoltageV = 230.0,
    double PowerManagementLimitW = 0,
    double SimultaneityFactor = 1.0,
    InductionCableInstallationMethod CableInstallationMethod = InductionCableInstallationMethod.B2,
    InductionConductorMaterial ConductorMaterial = InductionConductorMaterial.Copper,
    double AmbientTemperatureC = 30.0,
    double GroupingCorrectionFactor = 1.0,
    bool EnableExtendedVerification = false,
    InductionEarthingSystem EarthingSystem = InductionEarthingSystem.TN,
    InductionMcbCurve McbCurve = InductionMcbCurve.B,
    double FaultLoopImpedanceOhm = 0.0,
    double EarthResistanceOhm = 0.0,
    double RcdResidualCurrentmA = 30.0);

public sealed record InductionOvenScenarioResult(
    string ScenarioName,
    double NominalInductionPowerW,
    double SimultaneousInductionPowerW,
    double EffectiveInductionPowerW,
    bool IsPowerManagementLimiting,
    double InductionCurrentA,
    int? RecommendedInductionBreakerA,
    double L1CurrentA,
    double L2CurrentA,
    double L3CurrentA,
    double PhaseImbalancePercent,
    double InductionVoltageDropPercent,
    InductionCableInstallationMethod CableInstallationMethod,
    InductionConductorMaterial ConductorMaterial,
    double AmbientTemperatureC,
    double BaseCableCapacityA,
    double TemperatureCorrectionFactor,
    double MaterialCorrectionFactor,
    double GroupingCorrectionFactor,
    double CableCapacityA,
    string VoltageDropModelCode,
    bool IsExtendedVerificationEnabled,
    bool? ExtendedVerificationPassed,
    string ExtendedVerificationSummary,
    bool InductionBreakerFitsCable,
    IReadOnlyList<string> Notes);

public sealed record InductionOvenCalculationResult(
    InductionOvenScenarioResult TwoPhaseInductionScenario,
    InductionOvenScenarioResult SinglePhaseInductionScenario);

/// <summary>
/// Kalkulator pomocniczy dla ukladu indukcji.
/// Dobor ma charakter wspomagajacy i nie zastepuje finalnej weryfikacji projektowej.
/// </summary>
public class InductionOvenCalculatorService
{
    private enum VoltageDropModel
    {
        SinglePhaseLn = 0,
        TwoPhaseSplitLn = 1,
        ThreePhase = 2
    }

    private const double CopperResistivity20 = 0.0175; // Ohm*mm2/m
    private const double AluminumResistivity20 = 0.0285; // Ohm*mm2/m
    private const double CopperTempCoefficient = 0.00393; // 1/C
    private const double AluminumTempCoefficient = 0.00403; // 1/C
    private const double DefaultReactanceOhmPerKm = 0.08; // typical low-voltage multicore cable
    private static readonly int[] StandardBreakers = { 6, 10, 12, 13, 16, 20, 25, 32, 40, 50, 63 };
    private static readonly Dictionary<double, double> Pvc70CAmbientCorrectionFactors = new()
    {
        { 25.0, 1.03 },
        { 30.0, 1.00 },
        { 35.0, 0.94 },
        { 40.0, 0.87 },
        { 45.0, 0.79 },
        { 50.0, 0.71 },
        { 55.0, 0.61 },
        { 60.0, 0.50 }
    };

    public InductionOvenCalculationResult Calculate(InductionOvenCalculatorInput input)
    {
        double voltage = input.PhaseVoltageV > 0 ? input.PhaseVoltageV : 230.0;
        double cosPhi = Math.Clamp(input.CosPhi, 0.6, 1.0);
        double length = Math.Max(0, input.CableLengthM);
        double crossSection = Math.Max(0.5, input.CableCrossSectionMm2);
        double ambientTemperatureC = Math.Clamp(input.AmbientTemperatureC, 10.0, 60.0);
        double groupingFactor = Math.Clamp(input.GroupingCorrectionFactor, 0.5, 1.0);
        var conductorMaterial = input.ConductorMaterial;
        double nominalPower = Math.Max(0, input.InductionPowerW);
        double simultaneityFactor = Math.Clamp(input.SimultaneityFactor, 0.1, 1.0);
        double simultaneousPower = nominalPower * simultaneityFactor;
        double powerManagementLimit = Math.Max(0, input.PowerManagementLimitW);
        bool isPowerManagementLimiting = powerManagementLimit > 0 && simultaneousPower > powerManagementLimit + 0.001;
        double effectivePower = isPowerManagementLimiting ? powerManagementLimit : simultaneousPower;
        var installationMethod = input.CableInstallationMethod;
        double baseCableCapacity = CommonHelpers.GetCableCapacity(crossSection, GetCableCapacityTable(installationMethod));
        double temperatureCorrectionFactor = GetAmbientTemperatureCorrectionFactor(ambientTemperatureC);
        double materialCorrectionFactor = GetMaterialCapacityCorrectionFactor(conductorMaterial);
        double cableCapacity = baseCableCapacity * temperatureCorrectionFactor * materialCorrectionFactor * groupingFactor;

        double inductionTwoPhaseCurrent = CalculateTwoPhaseCurrent(effectivePower, voltage, cosPhi);
        var twoPhase = BuildScenario(
            name: "2F: indukcja na L1+L2",
            evaluateImbalance: true,
            nominalPower: nominalPower,
            simultaneousPower: simultaneousPower,
            effectivePower: effectivePower,
            isPowerManagementLimiting: isPowerManagementLimiting,
            inductionCurrent: inductionTwoPhaseCurrent,
            length: length,
            crossSection: crossSection,
            phaseVoltage: voltage,
            cosPhi: cosPhi,
            installationMethod: installationMethod,
            conductorMaterial: conductorMaterial,
            ambientTemperatureC: ambientTemperatureC,
            baseCableCapacity: baseCableCapacity,
            temperatureCorrectionFactor: temperatureCorrectionFactor,
            materialCorrectionFactor: materialCorrectionFactor,
            groupingCorrectionFactor: groupingFactor,
            cableCapacity: cableCapacity,
            voltageDropModel: VoltageDropModel.TwoPhaseSplitLn,
            enableExtendedVerification: input.EnableExtendedVerification,
            earthingSystem: input.EarthingSystem,
            mcbCurve: input.McbCurve,
            faultLoopImpedanceOhm: input.FaultLoopImpedanceOhm,
            earthResistanceOhm: input.EarthResistanceOhm,
            rcdResidualCurrentmA: input.RcdResidualCurrentmA,
            inductionL1Current: inductionTwoPhaseCurrent,
            inductionL2Current: inductionTwoPhaseCurrent,
            inductionL3Current: 0);

        double inductionSinglePhaseCurrent = CalculateSinglePhaseCurrent(effectivePower, voltage, cosPhi);
        var singlePhase = BuildScenario(
            name: "1F: indukcja na L1",
            evaluateImbalance: false,
            nominalPower: nominalPower,
            simultaneousPower: simultaneousPower,
            effectivePower: effectivePower,
            isPowerManagementLimiting: isPowerManagementLimiting,
            inductionCurrent: inductionSinglePhaseCurrent,
            length: length,
            crossSection: crossSection,
            phaseVoltage: voltage,
            cosPhi: cosPhi,
            installationMethod: installationMethod,
            conductorMaterial: conductorMaterial,
            ambientTemperatureC: ambientTemperatureC,
            baseCableCapacity: baseCableCapacity,
            temperatureCorrectionFactor: temperatureCorrectionFactor,
            materialCorrectionFactor: materialCorrectionFactor,
            groupingCorrectionFactor: groupingFactor,
            cableCapacity: cableCapacity,
            voltageDropModel: VoltageDropModel.SinglePhaseLn,
            enableExtendedVerification: input.EnableExtendedVerification,
            earthingSystem: input.EarthingSystem,
            mcbCurve: input.McbCurve,
            faultLoopImpedanceOhm: input.FaultLoopImpedanceOhm,
            earthResistanceOhm: input.EarthResistanceOhm,
            rcdResidualCurrentmA: input.RcdResidualCurrentmA,
            inductionL1Current: inductionSinglePhaseCurrent,
            inductionL2Current: 0,
            inductionL3Current: 0);

        return new InductionOvenCalculationResult(twoPhase, singlePhase);
    }

    private static InductionOvenScenarioResult BuildScenario(
        string name,
        bool evaluateImbalance,
        double nominalPower,
        double simultaneousPower,
        double effectivePower,
        bool isPowerManagementLimiting,
        double inductionCurrent,
        double length,
        double crossSection,
        double phaseVoltage,
        double cosPhi,
        InductionCableInstallationMethod installationMethod,
        InductionConductorMaterial conductorMaterial,
        double ambientTemperatureC,
        double baseCableCapacity,
        double temperatureCorrectionFactor,
        double materialCorrectionFactor,
        double groupingCorrectionFactor,
        double cableCapacity,
        VoltageDropModel voltageDropModel,
        bool enableExtendedVerification,
        InductionEarthingSystem earthingSystem,
        InductionMcbCurve mcbCurve,
        double faultLoopImpedanceOhm,
        double earthResistanceOhm,
        double rcdResidualCurrentmA,
        double inductionL1Current,
        double inductionL2Current,
        double inductionL3Current)
    {
        const double epsilon = 0.001;
        int? inductionBreaker = SelectBreaker(inductionCurrent, cableCapacity);

        double inductionDrop = CalculateVoltageDropPercent(
            inductionCurrent,
            crossSection,
            length,
            phaseVoltage,
            cosPhi,
            conductorMaterial,
            ambientTemperatureC,
            voltageDropModel);
        double scenarioImbalance = CalculateScenarioImbalancePercent(inductionL1Current, inductionL2Current, inductionL3Current);
        double imbalance = evaluateImbalance ? scenarioImbalance : 0.0;

        bool hasLoad = inductionCurrent > epsilon;
        bool inductionFitsCable = !hasLoad || inductionBreaker.HasValue;

        var notes = new List<string>();
        if (hasLoad && !inductionBreaker.HasValue)
        {
            notes.Add($"Brak standardowego MCB 6-63A spelniajacego warunek IB <= In <= Iz (IB={inductionCurrent:F1}A, Iz={cableCapacity:F1}A).");
        }

        var extendedVerification = EvaluateExtendedVerification(
            enableExtendedVerification,
            earthingSystem,
            mcbCurve,
            phaseVoltage,
            inductionBreaker,
            faultLoopImpedanceOhm,
            earthResistanceOhm,
            rcdResidualCurrentmA);
        if (enableExtendedVerification && extendedVerification.Notes.Count > 0)
        {
            notes.AddRange(extendedVerification.Notes);
        }

        if (Math.Abs(temperatureCorrectionFactor - 1.0) > epsilon
            || Math.Abs(materialCorrectionFactor - 1.0) > epsilon
            || Math.Abs(groupingCorrectionFactor - 1.0) > epsilon)
        {
            notes.Add(
                $"Korekta Iz: bazowe {baseCableCapacity:F1}A * kT {temperatureCorrectionFactor:F2} * kM {materialCorrectionFactor:F2} * kG {groupingCorrectionFactor:F2} = {cableCapacity:F1}A.");
        }

        if (isPowerManagementLimiting)
        {
            notes.Add($"PowerManagement ogranicza moc do {effectivePower:F0}W.");
        }

        if (evaluateImbalance && imbalance > 20.0)
        {
            bool isTwoPhase = inductionL1Current > epsilon && inductionL2Current > epsilon && inductionL3Current <= epsilon;
            if (isTwoPhase)
            {
                notes.Add($"Nierownowaga faz aktywnych L1/L2 {imbalance:F1}% przekracza prog projektowy 20%.");
            }
            else
            {
                notes.Add($"Asymetria obciazenia faz {imbalance:F1}% przekracza prog projektowy 20%.");
            }
        }

        return new InductionOvenScenarioResult(
            name,
            nominalPower,
            simultaneousPower,
            effectivePower,
            isPowerManagementLimiting,
            inductionCurrent,
            inductionBreaker,
            inductionL1Current,
            inductionL2Current,
            inductionL3Current,
            imbalance,
            inductionDrop,
            installationMethod,
            conductorMaterial,
            ambientTemperatureC,
            baseCableCapacity,
            temperatureCorrectionFactor,
            materialCorrectionFactor,
            groupingCorrectionFactor,
            cableCapacity,
            GetVoltageDropModelCode(voltageDropModel),
            enableExtendedVerification,
            extendedVerification.Passed,
            extendedVerification.Summary,
            inductionFitsCable,
            notes);
    }

    private static double CalculateSinglePhaseCurrent(double powerW, double voltage, double cosPhi)
    {
        if (powerW <= 0 || voltage <= 0 || cosPhi <= 0)
        {
            return 0;
        }

        return powerW / (voltage * cosPhi);
    }

    private static double CalculateTwoPhaseCurrent(double powerW, double voltage, double cosPhi)
    {
        if (powerW <= 0 || voltage <= 0 || cosPhi <= 0)
        {
            return 0;
        }

        return powerW / (2.0 * voltage * cosPhi);
    }

    private static double CalculateScenarioImbalancePercent(double l1, double l2, double l3)
    {
        const double epsilon = 0.001;
        bool hasL1 = l1 > epsilon;
        bool hasL2 = l2 > epsilon;
        bool hasL3 = l3 > epsilon;

        // Dla wariantu 2F oceniamy tylko balans miedzy aktywnymi fazami L1/L2.
        if (hasL1 && hasL2 && !hasL3)
        {
            double averageTwoPhase = (l1 + l2) / 2.0;
            if (averageTwoPhase <= 0)
            {
                return 0;
            }

            return (Math.Abs(l1 - l2) / averageTwoPhase) * 100.0;
        }

        return PhaseDistributionCalculator.CalculateImbalancePercent(l1, l2, l3);
    }

    private static int? SelectBreaker(double currentA, double cableCapacityA)
    {
        if (currentA <= 0 || cableCapacityA <= 0)
        {
            return null;
        }

        foreach (var breaker in StandardBreakers)
        {
            if (breaker >= currentA && breaker <= cableCapacityA + 0.001)
            {
                return breaker;
            }
        }

        return null;
    }

    private static Dictionary<double, double> GetCableCapacityTable(InductionCableInstallationMethod method)
    {
        return method == InductionCableInstallationMethod.C
            ? CommonHelpers.CableAmpacityMethodC
            : CommonHelpers.CableAmpacityMethodB2;
    }

    public static string GetInstallationMethodCode(InductionCableInstallationMethod method)
    {
        return method == InductionCableInstallationMethod.C ? "C" : "B2";
    }

    public static string GetConductorMaterialCode(InductionConductorMaterial material)
    {
        return material == InductionConductorMaterial.Aluminum ? "Al" : "Cu";
    }

    private static double GetAmbientTemperatureCorrectionFactor(double ambientTemperatureC)
    {
        return InterpolateFactor(Pvc70CAmbientCorrectionFactors, ambientTemperatureC);
    }

    private static double GetMaterialCapacityCorrectionFactor(InductionConductorMaterial material)
    {
        return material == InductionConductorMaterial.Aluminum ? 0.80 : 1.0;
    }

    private static double InterpolateFactor(IReadOnlyDictionary<double, double> factorTable, double value)
    {
        if (factorTable.Count == 0)
        {
            return 1.0;
        }

        var points = factorTable
            .OrderBy(pair => pair.Key)
            .ToArray();

        if (value <= points[0].Key)
        {
            return points[0].Value;
        }

        if (value >= points[^1].Key)
        {
            return points[^1].Value;
        }

        for (int i = 0; i < points.Length - 1; i++)
        {
            var left = points[i];
            var right = points[i + 1];
            if (value < left.Key || value > right.Key)
            {
                continue;
            }

            double span = right.Key - left.Key;
            if (span <= 0.0)
            {
                return left.Value;
            }

            double ratio = (value - left.Key) / span;
            return left.Value + ((right.Value - left.Value) * ratio);
        }

        return points[^1].Value;
    }

    private static string GetVoltageDropModelCode(VoltageDropModel model)
    {
        return model switch
        {
            VoltageDropModel.SinglePhaseLn => "1F (L-N)",
            VoltageDropModel.TwoPhaseSplitLn => "2x230V (L-N + L-N)",
            VoltageDropModel.ThreePhase => "3F (L-L)",
            _ => "1F (L-N)"
        };
    }

    private static (bool? Passed, string Summary, IReadOnlyList<string> Notes) EvaluateExtendedVerification(
        bool enabled,
        InductionEarthingSystem earthingSystem,
        InductionMcbCurve mcbCurve,
        double phaseVoltage,
        int? breakerA,
        double faultLoopImpedanceOhm,
        double earthResistanceOhm,
        double rcdResidualCurrentmA)
    {
        if (!enabled)
        {
            return (null, "Weryfikacja rozszerzona: wylaczona.", Array.Empty<string>());
        }

        var notes = new List<string>();
        bool allChecksAvailable = true;
        bool allChecksPassed = true;

        if (earthingSystem == InductionEarthingSystem.TN)
        {
            if (breakerA.HasValue && breakerA.Value > 0 && faultLoopImpedanceOhm > 0.0 && phaseVoltage > 0.0)
            {
                double magneticFactor = GetMcbMagneticFactor(mcbCurve);
                double maxZs = phaseVoltage / (magneticFactor * breakerA.Value);
                bool tnSwzPass = faultLoopImpedanceOhm <= maxZs + 0.001;
                allChecksPassed &= tnSwzPass;
                notes.Add(
                    $"TN/SWZ (uproszcz.): Zs={faultLoopImpedanceOhm:F2}ohm, Zs_max~{maxZs:F2}ohm dla {GetMcbCurveCode(mcbCurve)}{breakerA.Value} -> {(tnSwzPass ? "OK" : "NIE")}");
            }
            else
            {
                allChecksAvailable = false;
                notes.Add("TN/SWZ: brak pelnych danych (Zs, In MCB lub U0).");
            }

            if (rcdResidualCurrentmA > 0.0)
            {
                notes.Add($"RCD-checklist: IΔn={rcdResidualCurrentmA:F0}mA (sprawdz typ, selektywnosc i koordynacje).");
            }
        }
        else
        {
            if (earthResistanceOhm > 0.0 && rcdResidualCurrentmA > 0.0)
            {
                double residualCurrentA = rcdResidualCurrentmA / 1000.0;
                double touchVoltage = earthResistanceOhm * residualCurrentA;
                bool ttPass = touchVoltage <= 50.0 + 0.001;
                allChecksPassed &= ttPass;
                notes.Add($"TT/RCD: RA*IΔn={touchVoltage:F1}V <= 50V -> {(ttPass ? "OK" : "NIE")}");
            }
            else
            {
                allChecksAvailable = false;
                notes.Add("TT/RCD: brak pelnych danych (RA lub IΔn).");
            }

            if (faultLoopImpedanceOhm > 0.0)
            {
                notes.Add($"TT info: Zs={faultLoopImpedanceOhm:F2}ohm (w TT decyzje SWZ zwykle opieraj na RCD + RA).");
            }
        }

        bool? passed = allChecksAvailable ? allChecksPassed : null;
        string summary = passed switch
        {
            true => "Weryfikacja rozszerzona: OK (orientacyjnie).",
            false => "Weryfikacja rozszerzona: do poprawy.",
            null => "Weryfikacja rozszerzona: niepelne dane."
        };

        return (passed, summary, notes);
    }

    private static string GetMcbCurveCode(InductionMcbCurve curve)
    {
        return curve switch
        {
            InductionMcbCurve.B => "B",
            InductionMcbCurve.C => "C",
            InductionMcbCurve.D => "D",
            _ => "B"
        };
    }

    private static double GetMcbMagneticFactor(InductionMcbCurve curve)
    {
        return curve switch
        {
            InductionMcbCurve.B => 5.0,
            InductionMcbCurve.C => 10.0,
            InductionMcbCurve.D => 20.0,
            _ => 5.0
        };
    }

    private static double CalculateVoltageDropPercent(
        double currentA,
        double crossSectionMm2,
        double lengthM,
        double voltage,
        double cosPhi,
        InductionConductorMaterial material,
        double ambientTemperatureC,
        VoltageDropModel model)
    {
        if (currentA <= 0 || crossSectionMm2 <= 0 || lengthM <= 0 || voltage <= 0 || cosPhi <= 0)
        {
            return 0;
        }

        double resistivity20 = material == InductionConductorMaterial.Aluminum
            ? AluminumResistivity20
            : CopperResistivity20;
        double tempCoefficient = material == InductionConductorMaterial.Aluminum
            ? AluminumTempCoefficient
            : CopperTempCoefficient;

        // Approximation: conductor operating temperature elevated over ambient.
        double conductorTemperatureC = Math.Clamp(ambientTemperatureC + 20.0, 20.0, 90.0);
        double resistivity = resistivity20 * (1.0 + (tempCoefficient * (conductorTemperatureC - 20.0)));
        double resistancePerMeter = resistivity / crossSectionMm2;
        double reactancePerMeter = DefaultReactanceOhmPerKm / 1000.0;

        double boundedCosPhi = Math.Clamp(cosPhi, 0.0, 1.0);
        double sinPhi = Math.Sqrt(Math.Max(0.0, 1.0 - (boundedCosPhi * boundedCosPhi)));
        double impedanceComponent = (resistancePerMeter * boundedCosPhi) + (reactancePerMeter * sinPhi);

        double pathFactor = model switch
        {
            VoltageDropModel.SinglePhaseLn => 2.0,
            VoltageDropModel.TwoPhaseSplitLn => 2.0,
            VoltageDropModel.ThreePhase => Math.Sqrt(3.0),
            _ => 2.0
        };

        double voltageDrop = currentA * pathFactor * lengthM * impedanceComponent;
        return (voltageDrop / voltage) * 100.0;
    }
}
