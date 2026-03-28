using System.Collections.Generic;
using System.Linq;
using DINBoard.Models;
using DINBoard.Services;
using Xunit;

namespace Avalonia.Tests;

public class PhaseDistributionPlannerTests
{
    [Fact]
    public void CreateBalancePlan_PhaseIndicators_ShouldBePlannedWithoutMutatingSourceSymbols()
    {
        var symbols = new List<SymbolItem>
        {
            new() { Id = "ind-1", Type = "KontrolkiFaz", Phase = "PENDING" },
            new() { Id = "ind-2", Type = "KontrolkiFaz", Phase = "PENDING" },
            new() { Id = "ind-3", Type = "KontrolkiFaz", Phase = "PENDING" },
            new() { Id = "ind-4", Type = "KontrolkiFaz", Phase = "PENDING" },
            new() { Id = "mcb-1", Type = "MCB 1P", Phase = "L1", PowerW = 1000, Width = 18, Height = 90 }
        };

        var plan = PhaseDistributionCalculator.CreateBalancePlan(
            symbols,
            BalanceMode.Current,
            BalanceScope.AllSinglePhase,
            230);

        Assert.Equal(new[] { "L1", "L2", "L3", "L1" }, plan.PhaseIndicatorAssignments.Select(a => a.Phase).ToArray());
        Assert.DoesNotContain(plan.WorkingSymbols, symbol => symbol.Id.StartsWith("ind-"));
        Assert.All(symbols.Where(symbol => symbol.Id.StartsWith("ind-")), symbol => Assert.Equal("PENDING", symbol.Phase));
        Assert.Equal(5, plan.Snapshot.Count);
    }

    [Fact]
    public void CreateBalancePlan_LockedSinglePhaseRcdGroup_ShouldMoveWeightToBaseLoads()
    {
        var symbols = new List<SymbolItem>
        {
            new() { Id = "rcd-1", Type = "RCD 2P", Phase = "L3", IsPhaseLocked = true, Width = 36, Height = 90, VisualPath = "rcd_2p" },
            new() { Id = "mcb-1", Type = "MCB 1P", Phase = "L3", PowerW = 1000, RcdSymbolId = "rcd-1", Width = 18, Height = 90 },
            new() { Id = "mcb-2", Type = "MCB 1P", Phase = "L3", PowerW = 500, RcdSymbolId = "rcd-1", Width = 18, Height = 90 }
        };

        var plan = PhaseDistributionCalculator.CreateBalancePlan(
            symbols,
            BalanceMode.Power,
            BalanceScope.OnlyUnlocked,
            230);

        Assert.Empty(plan.Units);
        Assert.Equal(0, plan.Loads[0]);
        Assert.Equal(0, plan.Loads[1]);
        Assert.Equal(1500, plan.Loads[2], 3);
    }

    [Fact]
    public void CreateBalancePlan_UnlockedSinglePhaseRcdGroup_ShouldCreateSingleUnit()
    {
        var symbols = new List<SymbolItem>
        {
            new() { Id = "rcd-1", Type = "RCD 2P", Phase = "L1", Width = 36, Height = 90, VisualPath = "rcd_2p" },
            new() { Id = "mcb-1", Type = "MCB 1P", Phase = "L1", PowerW = 1200, RcdSymbolId = "rcd-1", Width = 18, Height = 90 },
            new() { Id = "mcb-2", Type = "MCB 1P", Phase = "L1", PowerW = 800, RcdSymbolId = "rcd-1", Width = 18, Height = 90 }
        };

        var plan = PhaseDistributionCalculator.CreateBalancePlan(
            symbols,
            BalanceMode.Power,
            BalanceScope.AllSinglePhase,
            230);

        var unit = Assert.Single(plan.Units);
        Assert.Equal(3, unit.Symbols.Count);
        Assert.Equal(2000, unit.TotalWeight, 3);
        Assert.Contains(unit.Symbols, symbol => symbol.Id == "rcd-1");
    }

}
