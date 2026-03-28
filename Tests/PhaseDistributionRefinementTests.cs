using System.Collections.Generic;
using DINBoard.Models;
using DINBoard.Services;
using Xunit;

namespace Avalonia.Tests;

public class PhaseDistributionRefinementTests
{
    [Fact]
    public void FindBestMoveCandidate_ShouldPreferClosestWeightCandidate()
    {
        var heavyRcdUnit = CreateRcdUnit("rcd-heavy", "L1", 1400, 400);
        var targetRcdUnit = CreateRcdUnit("rcd-light", "L2", 200);
        var standaloneUnit = CreateStandaloneUnit("mcb-standalone", "L1", 900);
        var units = new List<PhaseDistributionCalculator.BalanceUnit>
        {
            heavyRcdUnit,
            standaloneUnit,
            targetRcdUnit
        };

        var candidate = PhaseDistributionCalculator.FindBestMoveCandidate(
            units,
            "L1",
            "L2",
            2200,
            1100,
            BalanceMode.Power,
            230);

        Assert.NotNull(candidate);
        Assert.True(candidate!.IsStandalone);
        Assert.Equal("mcb-standalone", candidate.Mcb.Id);
        Assert.Equal(900, candidate.Weight, 3);
    }

    [Fact]
    public void FindBestSwapCandidate_ShouldPickMostImprovingPair()
    {
        var heavyA = CreateStandaloneUnit("heavy-a", "L1", 2000);
        var heavyB = CreateStandaloneUnit("heavy-b", "L1", 1500);
        var lightA = CreateStandaloneUnit("light-a", "L2", 500);
        var lightB = CreateStandaloneUnit("light-b", "L2", 1200);
        var units = new List<PhaseDistributionCalculator.BalanceUnit>
        {
            heavyA,
            heavyB,
            lightA,
            lightB
        };

        double[] loads = { 5000, 1000, 1000 };
        double currentImbalance = PhaseDistributionCalculator.CalculateImbalancePercent(loads[0], loads[1], loads[2]);

        var candidate = PhaseDistributionCalculator.FindBestSwapCandidate(
            units,
            loads,
            "L1",
            "L2",
            0,
            1,
            currentImbalance);

        Assert.NotNull(candidate);
        Assert.Equal("heavy-a", candidate!.HeavyUnit.Symbols[0].Id);
        Assert.Equal("light-a", candidate.LightUnit.Symbols[0].Id);
        Assert.True(candidate.ResultingImbalance < currentImbalance);
    }

    private static PhaseDistributionCalculator.BalanceUnit CreateStandaloneUnit(string id, string phase, double powerW)
    {
        var unit = new PhaseDistributionCalculator.BalanceUnit();
        unit.Symbols.Add(new SymbolItem
        {
            Id = id,
            Type = "MCB 1P",
            Phase = phase,
            PowerW = powerW,
            Width = 18,
            Height = 90
        });
        unit.TotalWeight = powerW;
        return unit;
    }

    private static PhaseDistributionCalculator.BalanceUnit CreateRcdUnit(string id, string phase, params double[] childPowers)
    {
        var unit = new PhaseDistributionCalculator.BalanceUnit();
        unit.Symbols.Add(new SymbolItem
        {
            Id = id,
            Type = "RCD 2P",
            VisualPath = "rcd_2p",
            Phase = phase,
            Width = 36,
            Height = 90
        });

        for (int i = 0; i < childPowers.Length; i++)
        {
            unit.Symbols.Add(new SymbolItem
            {
                Id = $"{id}-mcb-{i + 1}",
                Type = "MCB 1P",
                Phase = phase,
                PowerW = childPowers[i],
                RcdSymbolId = id,
                Width = 18,
                Height = 90
            });
        }

        foreach (var power in childPowers)
        {
            unit.TotalWeight += power;
        }

        return unit;
    }
}
