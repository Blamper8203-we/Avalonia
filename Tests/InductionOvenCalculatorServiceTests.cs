using System;
using DINBoard.Services;
using Xunit;

namespace Avalonia.Tests;

public class InductionOvenCalculatorServiceTests
{
    private readonly InductionOvenCalculatorService _service = new();

    [Fact]
    public void Calculate_ForTypicalInputs_ShouldRecommendExpectedBreakers()
    {
        var input = new InductionOvenCalculatorInput(
            InductionPowerW: 7360,
            CableLengthM: 18,
            CableCrossSectionMm2: 4.0,
            CosPhi: 0.9);

        var result = _service.Calculate(input);

        Assert.Equal(7360, result.TwoPhaseInductionScenario.NominalInductionPowerW);
        Assert.Equal(7360, result.TwoPhaseInductionScenario.SimultaneousInductionPowerW);
        Assert.Equal(7360, result.TwoPhaseInductionScenario.EffectiveInductionPowerW);
        Assert.False(result.TwoPhaseInductionScenario.IsPowerManagementLimiting);
        Assert.Equal(20, result.TwoPhaseInductionScenario.RecommendedInductionBreakerA);
        Assert.Null(result.SinglePhaseInductionScenario.RecommendedInductionBreakerA);
        Assert.False(result.SinglePhaseInductionScenario.InductionBreakerFitsCable);
        Assert.Equal(0, result.TwoPhaseInductionScenario.PhaseImbalancePercent, 1);
        Assert.Equal(0, result.SinglePhaseInductionScenario.PhaseImbalancePercent, 1);
        Assert.Equal(0, result.TwoPhaseInductionScenario.L3CurrentA);
        Assert.Equal(0, result.SinglePhaseInductionScenario.L2CurrentA);
        Assert.Equal(0, result.SinglePhaseInductionScenario.L3CurrentA);
    }

    [Fact]
    public void Calculate_ForTooSmallCable_ShouldFlagBreakerCapacityMismatch()
    {
        var input = new InductionOvenCalculatorInput(
            InductionPowerW: 7360,
            CableLengthM: 25,
            CableCrossSectionMm2: 1.5,
            CosPhi: 0.9);

        var result = _service.Calculate(input);

        Assert.False(result.TwoPhaseInductionScenario.InductionBreakerFitsCable);
        Assert.Null(result.TwoPhaseInductionScenario.RecommendedInductionBreakerA);
        Assert.NotEmpty(result.TwoPhaseInductionScenario.Notes);
    }

    [Fact]
    public void Calculate_WithPowerManagementAndSimultaneity_ShouldUseEffectivePower()
    {
        var input = new InductionOvenCalculatorInput(
            InductionPowerW: 7400,
            CableLengthM: 18,
            CableCrossSectionMm2: 4.0,
            CosPhi: 0.9,
            PhaseVoltageV: 230,
            PowerManagementLimitW: 4500,
            SimultaneityFactor: 0.9);

        var result = _service.Calculate(input);

        Assert.Equal(7400, result.TwoPhaseInductionScenario.NominalInductionPowerW);
        Assert.Equal(6660, result.TwoPhaseInductionScenario.SimultaneousInductionPowerW, 0);
        Assert.Equal(4500, result.TwoPhaseInductionScenario.EffectiveInductionPowerW, 0);
        Assert.True(result.TwoPhaseInductionScenario.IsPowerManagementLimiting);
        Assert.Equal(12, result.TwoPhaseInductionScenario.RecommendedInductionBreakerA);
        Assert.Equal(25, result.SinglePhaseInductionScenario.RecommendedInductionBreakerA);
    }

    [Fact]
    public void Calculate_ForZeroLoad_ShouldReturnZeroCurrentsAndNoBreakers()
    {
        var input = new InductionOvenCalculatorInput(
            InductionPowerW: 0,
            CableLengthM: 10,
            CableCrossSectionMm2: 2.5,
            CosPhi: 0.9);

        var result = _service.Calculate(input);

        Assert.Equal(0, result.TwoPhaseInductionScenario.InductionCurrentA);
        Assert.Equal(0, result.TwoPhaseInductionScenario.L1CurrentA);
        Assert.Equal(0, result.TwoPhaseInductionScenario.L2CurrentA);
        Assert.Equal(0, result.TwoPhaseInductionScenario.L3CurrentA);
        Assert.Null(result.TwoPhaseInductionScenario.RecommendedInductionBreakerA);
    }

    [Fact]
    public void Calculate_ForSinglePhaseScenario_ShouldNotEvaluatePhaseImbalance()
    {
        var input = new InductionOvenCalculatorInput(
            InductionPowerW: 7400,
            CableLengthM: 12,
            CableCrossSectionMm2: 4.0,
            CosPhi: 0.9);

        var result = _service.Calculate(input);

        Assert.Equal(0, result.SinglePhaseInductionScenario.PhaseImbalancePercent, 1);
        Assert.DoesNotContain(
            result.SinglePhaseInductionScenario.Notes,
            note => note.Contains("prog projektowy 20%", StringComparison.Ordinal));
    }

    [Fact]
    public void Calculate_ForMethodC_ShouldUseHigherAmpacityThanMethodB2()
    {
        var baseInput = new InductionOvenCalculatorInput(
            InductionPowerW: 7360,
            CableLengthM: 12,
            CableCrossSectionMm2: 4.0,
            CosPhi: 0.9);

        var b2Result = _service.Calculate(baseInput with
        {
            CableInstallationMethod = InductionCableInstallationMethod.B2
        });
        var cResult = _service.Calculate(baseInput with
        {
            CableInstallationMethod = InductionCableInstallationMethod.C
        });

        Assert.Equal(InductionCableInstallationMethod.B2, b2Result.TwoPhaseInductionScenario.CableInstallationMethod);
        Assert.Equal(InductionCableInstallationMethod.C, cResult.TwoPhaseInductionScenario.CableInstallationMethod);
        Assert.True(cResult.TwoPhaseInductionScenario.CableCapacityA > b2Result.TwoPhaseInductionScenario.CableCapacityA);
    }

    [Fact]
    public void Calculate_WithMaterialTemperatureAndGrouping_ShouldReduceCorrectedIz()
    {
        var baseInput = new InductionOvenCalculatorInput(
            InductionPowerW: 7360,
            CableLengthM: 10,
            CableCrossSectionMm2: 4.0,
            CosPhi: 0.9,
            CableInstallationMethod: InductionCableInstallationMethod.B2);

        var correctedInput = baseInput with
        {
            ConductorMaterial = InductionConductorMaterial.Aluminum,
            AmbientTemperatureC = 40.0,
            GroupingCorrectionFactor = 0.8
        };

        var baseResult = _service.Calculate(baseInput);
        var correctedResult = _service.Calculate(correctedInput);

        Assert.Equal(28.0, baseResult.TwoPhaseInductionScenario.BaseCableCapacityA, 1);
        Assert.Equal(1.0, baseResult.TwoPhaseInductionScenario.TemperatureCorrectionFactor, 2);
        Assert.Equal(1.0, baseResult.TwoPhaseInductionScenario.MaterialCorrectionFactor, 2);
        Assert.Equal(1.0, baseResult.TwoPhaseInductionScenario.GroupingCorrectionFactor, 2);

        Assert.Equal(0.87, correctedResult.TwoPhaseInductionScenario.TemperatureCorrectionFactor, 2);
        Assert.Equal(0.80, correctedResult.TwoPhaseInductionScenario.MaterialCorrectionFactor, 2);
        Assert.Equal(0.80, correctedResult.TwoPhaseInductionScenario.GroupingCorrectionFactor, 2);
        Assert.True(correctedResult.TwoPhaseInductionScenario.CableCapacityA < baseResult.TwoPhaseInductionScenario.CableCapacityA);
    }

    [Fact]
    public void Calculate_ShouldExposeVoltageDropModelCodesForScenarios()
    {
        var input = new InductionOvenCalculatorInput(
            InductionPowerW: 7360,
            CableLengthM: 10,
            CableCrossSectionMm2: 4.0,
            CosPhi: 0.9);

        var result = _service.Calculate(input);

        Assert.Contains("2x230V", result.TwoPhaseInductionScenario.VoltageDropModelCode, StringComparison.Ordinal);
        Assert.Contains("1F", result.SinglePhaseInductionScenario.VoltageDropModelCode, StringComparison.Ordinal);
    }

    [Fact]
    public void Calculate_WithExtendedVerification_TnShouldPassForLowZs()
    {
        var input = new InductionOvenCalculatorInput(
            InductionPowerW: 7360,
            CableLengthM: 10,
            CableCrossSectionMm2: 4.0,
            CosPhi: 0.9,
            EnableExtendedVerification: true,
            EarthingSystem: InductionEarthingSystem.TN,
            McbCurve: InductionMcbCurve.B,
            FaultLoopImpedanceOhm: 1.0,
            RcdResidualCurrentmA: 30.0);

        var result = _service.Calculate(input);

        Assert.True(result.TwoPhaseInductionScenario.IsExtendedVerificationEnabled);
        Assert.True(result.TwoPhaseInductionScenario.ExtendedVerificationPassed);
        Assert.Contains("OK", result.TwoPhaseInductionScenario.ExtendedVerificationSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void Calculate_WithExtendedVerification_TtShouldFailForHighRa()
    {
        var input = new InductionOvenCalculatorInput(
            InductionPowerW: 7360,
            CableLengthM: 10,
            CableCrossSectionMm2: 4.0,
            CosPhi: 0.9,
            EnableExtendedVerification: true,
            EarthingSystem: InductionEarthingSystem.TT,
            EarthResistanceOhm: 300.0,
            RcdResidualCurrentmA: 300.0);

        var result = _service.Calculate(input);

        Assert.True(result.TwoPhaseInductionScenario.IsExtendedVerificationEnabled);
        Assert.False(result.TwoPhaseInductionScenario.ExtendedVerificationPassed);
        Assert.Contains("do poprawy", result.TwoPhaseInductionScenario.ExtendedVerificationSummary, StringComparison.Ordinal);
        Assert.Contains(
            result.TwoPhaseInductionScenario.Notes,
            note => note.Contains("TT/RCD", StringComparison.Ordinal));
    }

    [Fact]
    public void Calculate_WhenCurrentExceedsStandardRange_ShouldNotForce63ASelection()
    {
        var input = new InductionOvenCalculatorInput(
            InductionPowerW: 22000,
            CableLengthM: 10,
            CableCrossSectionMm2: 16.0,
            CosPhi: 0.9,
            PowerManagementLimitW: 22000,
            SimultaneityFactor: 1.0);

        var result = _service.Calculate(input);

        Assert.Null(result.SinglePhaseInductionScenario.RecommendedInductionBreakerA);
        Assert.False(result.SinglePhaseInductionScenario.InductionBreakerFitsCable);
        Assert.Contains(
            result.SinglePhaseInductionScenario.Notes,
            note => note.Contains("Brak standardowego MCB 6-63A", StringComparison.Ordinal));
    }
}
