using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DINBoard.Models;
using DINBoard.Services;
using Xunit;

namespace Avalonia.Tests;

public class PhaseDistributionCalculatorTests
{
    [Fact]
    public async Task BalancePhasesAsync_PendingSinglePhaseByRatio_AssignsPhase()
    {
        var symbols = new List<SymbolItem>
        {
            new()
            {
                Id = "mcb-1",
                Type = "MCB",
                Phase = "PENDING",
                PowerW = 2300,
                Width = 18,
                Height = 90
            }
        };

        var snapshot = await PhaseDistributionCalculator.BalancePhasesAsync(
            symbols,
            BalanceMode.Current,
            BalanceScope.AllSinglePhase,
            230);

        Assert.Equal("PENDING", snapshot["mcb-1"]);
        Assert.Contains(symbols[0].Phase, new[] { "L1", "L2", "L3" });
    }

    [Fact]
    public async Task BalancePhasesAsync_PhaseIndicators_AssignsRoundRobinAndKeepsSnapshot()
    {
        var symbols = new List<SymbolItem>
        {
            new() { Id = "ind-1", Type = "KontrolkiFaz", Phase = "PENDING" },
            new() { Id = "ind-2", Type = "KontrolkiFaz", Phase = "PENDING" },
            new() { Id = "ind-3", Type = "KontrolkiFaz", Phase = "PENDING" },
            new() { Id = "mcb-1", Type = "MCB 1P", Phase = "L1", PowerW = 2000, Width = 18, Height = 90 }
        };

        var snapshot = await PhaseDistributionCalculator.BalancePhasesAsync(
            symbols,
            BalanceMode.Current,
            BalanceScope.AllSinglePhase,
            230);

        Assert.Equal("PENDING", snapshot["ind-1"]);
        Assert.Equal("PENDING", snapshot["ind-2"]);
        Assert.Equal("PENDING", snapshot["ind-3"]);
        Assert.Equal("L1", symbols[0].Phase);
        Assert.Equal("L2", symbols[1].Phase);
        Assert.Equal("L3", symbols[2].Phase);
    }

    [Fact]
    public async Task BalancePhasesAsync_EqualPowerCircuits_ZeroImbalance()
    {
        // 6 obwodów po 2000W — powinno rozłożyć idealnie 2×2000 na każdą fazę
        var symbols = Enumerable.Range(1, 6).Select(i => new SymbolItem
        {
            Id = $"mcb-{i}",
            Type = "MCB 1P",
            Phase = "L1",
            PowerW = 2000,
            Width = 18,
            Height = 90
        }).ToList();

        await PhaseDistributionCalculator.BalancePhasesAsync(
            symbols, BalanceMode.Current, BalanceScope.AllSinglePhase, 230);

        var dist = PhaseDistributionCalculator.CalculateTotalDistribution(symbols);
        var imbalance = PhaseDistributionCalculator.CalculateImbalancePercent(
            dist.L1PowerW, dist.L2PowerW, dist.L3PowerW);

        Assert.True(imbalance < 1.0, $"Oczekiwano asymetrii < 1%, otrzymano {imbalance:F1}%");
    }

    [Fact]
    public async Task BalancePhasesAsync_VariedPowerCircuits_LowImbalance()
    {
        // Realistyczny rozkład mocy w typowej rozdzielnicy
        var powers = new[] { 500, 800, 1200, 2000, 2300, 3500, 500, 1000, 1500 };
        var symbols = powers.Select((p, i) => new SymbolItem
        {
            Id = $"mcb-{i}",
            Type = "MCB 1P",
            Phase = "L1",
            PowerW = p,
            Width = 18,
            Height = 90
        }).ToList();

        await PhaseDistributionCalculator.BalancePhasesAsync(
            symbols, BalanceMode.Current, BalanceScope.AllSinglePhase, 230);

        var dist = PhaseDistributionCalculator.CalculateTotalDistribution(symbols);
        var imbalance = PhaseDistributionCalculator.CalculateImbalancePercent(
            dist.L1PowerW, dist.L2PowerW, dist.L3PowerW);

        Assert.True(imbalance <= 10.0, $"Oczekiwano asymetrii <= 10%, otrzymano {imbalance:F1}%");
    }

    [Fact]
    public async Task BalancePhasesAsync_PhaseLockedCircuits_MaintainPhase()
    {
        var symbols = new List<SymbolItem>
        {
            new() { Id = "locked-1", Type = "MCB 1P", Phase = "L2", PowerW = 5000,
                     IsPhaseLocked = true, Width = 18, Height = 90 },
            new() { Id = "free-1", Type = "MCB 1P", Phase = "L1", PowerW = 2000,
                     Width = 18, Height = 90 },
            new() { Id = "free-2", Type = "MCB 1P", Phase = "L1", PowerW = 2000,
                     Width = 18, Height = 90 },
            new() { Id = "free-3", Type = "MCB 1P", Phase = "L1", PowerW = 2000,
                     Width = 18, Height = 90 },
        };

        await PhaseDistributionCalculator.BalancePhasesAsync(
            symbols, BalanceMode.Current, BalanceScope.OnlyUnlocked, 230);

        // Locked circuit must remain on L2
        Assert.Equal("L2", symbols[0].Phase);
    }

    [Fact]
    public async Task BalancePhasesAsync_ZeroPowerCircuits_NoException()
    {
        var symbols = new List<SymbolItem>
        {
            new() { Id = "mcb-1", Type = "MCB 1P", Phase = "L1", PowerW = 0,
                     Width = 18, Height = 90 },
            new() { Id = "mcb-2", Type = "MCB 1P", Phase = "L1", PowerW = 0,
                     Width = 18, Height = 90 },
            new() { Id = "mcb-3", Type = "MCB 1P", Phase = "L1", PowerW = 1000,
                     Width = 18, Height = 90 },
        };

        var snapshot = await PhaseDistributionCalculator.BalancePhasesAsync(
            symbols, BalanceMode.Current, BalanceScope.AllSinglePhase, 230);

        // Nie powinien wyrzucić wyjątku i powinien zwrócić snapshot
        Assert.NotNull(snapshot);
        Assert.Equal(3, snapshot.Count);
    }

    [Fact]
    public async Task BalancePhasesAsync_RcdGroup_AllMcbsSamePhase()
    {
        var rcdId = "rcd-1";
        var symbols = new List<SymbolItem>
        {
            new() { Id = rcdId, Type = "RCD 2P", Phase = "L1", PowerW = 0,
                     Width = 36, Height = 90, VisualPath = "rcd_2p" },
            new() { Id = "mcb-1", Type = "MCB 1P", Phase = "L1", PowerW = 2000,
                     RcdSymbolId = rcdId, Width = 18, Height = 90 },
            new() { Id = "mcb-2", Type = "MCB 1P", Phase = "L1", PowerW = 1500,
                     RcdSymbolId = rcdId, Width = 18, Height = 90 },
            // Standalone MCBs na inne fazy
            new() { Id = "mcb-3", Type = "MCB 1P", Phase = "L1", PowerW = 3500,
                     Width = 18, Height = 90 },
            new() { Id = "mcb-4", Type = "MCB 1P", Phase = "L1", PowerW = 3500,
                     Width = 18, Height = 90 },
        };

        await PhaseDistributionCalculator.BalancePhasesAsync(
            symbols, BalanceMode.Current, BalanceScope.AllSinglePhase, 230);

        // MCB-1 i MCB-2 pod RCD 2P muszą mieć tę samą fazę co RCD
        var rcdPhase = symbols.First(s => s.Id == rcdId).Phase;
        Assert.Equal(rcdPhase, symbols.First(s => s.Id == "mcb-1").Phase);
        Assert.Equal(rcdPhase, symbols.First(s => s.Id == "mcb-2").Phase);
    }

    [Fact]
    public async Task BalancePhasesAsync_LargeImbalance_RefinementReducesIt()
    {
        // Symulacja dużej nierównowagi: 10 obwodów, większość na L1
        var symbols = new List<SymbolItem>();
        var powers = new[] { 5000, 4000, 3000, 2500, 2000, 1500, 1000, 800, 600, 400 };
        for (int i = 0; i < powers.Length; i++)
        {
            symbols.Add(new SymbolItem
            {
                Id = $"mcb-{i}",
                Type = "MCB 1P",
                Phase = "L1",
                PowerW = powers[i],
                Width = 18,
                Height = 90
            });
        }

        await PhaseDistributionCalculator.BalancePhasesAsync(
            symbols, BalanceMode.Current, BalanceScope.AllSinglePhase, 230);

        var dist = PhaseDistributionCalculator.CalculateTotalDistribution(symbols);
        var imbalance = PhaseDistributionCalculator.CalculateImbalancePercent(
            dist.L1PowerW, dist.L2PowerW, dist.L3PowerW);

        // Po greedy + refinement + swap asymetria powinna być < 10%
        Assert.True(imbalance <= 10.0,
            $"Oczekiwano asymetrii <= 10%, otrzymano {imbalance:F1}%. " +
            $"L1={dist.L1PowerW}W, L2={dist.L2PowerW}W, L3={dist.L3PowerW}W");
    }

    [Fact]
    public async Task BalancePhasesAsync_ThreeEqualCircuits_OnePerPhase()
    {
        var symbols = new List<SymbolItem>
        {
            new() { Id = "mcb-1", Type = "MCB 1P", Phase = "L1", PowerW = 1000,
                     Width = 18, Height = 90 },
            new() { Id = "mcb-2", Type = "MCB 1P", Phase = "L1", PowerW = 1000,
                     Width = 18, Height = 90 },
            new() { Id = "mcb-3", Type = "MCB 1P", Phase = "L1", PowerW = 1000,
                     Width = 18, Height = 90 },
        };

        await PhaseDistributionCalculator.BalancePhasesAsync(
            symbols, BalanceMode.Current, BalanceScope.AllSinglePhase, 230);

        var phases = symbols.Select(s => s.Phase).OrderBy(p => p).ToList();
        Assert.Contains("L1", phases);
        Assert.Contains("L2", phases);
        Assert.Contains("L3", phases);
    }

    [Fact]
    public async Task BalancePhasesAsync_SnapshotRestoresOriginalPhases()
    {
        var symbols = new List<SymbolItem>
        {
            new() { Id = "mcb-1", Type = "MCB 1P", Phase = "L1", PowerW = 3000,
                     Width = 18, Height = 90 },
            new() { Id = "mcb-2", Type = "MCB 1P", Phase = "L2", PowerW = 1000,
                     Width = 18, Height = 90 },
            new() { Id = "mcb-3", Type = "MCB 1P", Phase = "L3", PowerW = 500,
                     Width = 18, Height = 90 },
        };

        var snapshot = await PhaseDistributionCalculator.BalancePhasesAsync(
            symbols, BalanceMode.Current, BalanceScope.AllSinglePhase, 230);

        // Snapshot powinien mieć oryginalne fazy
        Assert.Equal("L1", snapshot["mcb-1"]);
        Assert.Equal("L2", snapshot["mcb-2"]);
        Assert.Equal("L3", snapshot["mcb-3"]);
    }

    [Fact]
    public void CalculateImbalancePercent_EqualLoads_ZeroPercent()
    {
        var imbalance = PhaseDistributionCalculator.CalculateImbalancePercent(10, 10, 10);
        Assert.Equal(0, imbalance);
    }

    [Fact]
    public void CalculateImbalancePercent_AllZero_ZeroPercent()
    {
        var imbalance = PhaseDistributionCalculator.CalculateImbalancePercent(0, 0, 0);
        Assert.Equal(0, imbalance);
    }

    [Fact]
    public void DistributePower_ThreePhase_SplitsEvenly()
    {
        var dist = PhaseDistributionCalculator.DistributePower(3000, "L1+L2+L3");
        Assert.Equal(1000, dist.L1PowerW);
        Assert.Equal(1000, dist.L2PowerW);
        Assert.Equal(1000, dist.L3PowerW);
    }

    [Fact]
    public void DistributePower_TwoPhase_SplitsHalf()
    {
        var dist = PhaseDistributionCalculator.DistributePower(2000, "L1+L3");
        Assert.Equal(1000, dist.L1PowerW);
        Assert.Equal(0, dist.L2PowerW);
        Assert.Equal(1000, dist.L3PowerW);
    }

    [Fact]
    public async Task BalancePhasesAsync_ManySmallCircuits_DistributesEvenly()
    {
        // 15 obwodów oświetleniowych po 200W
        var symbols = Enumerable.Range(1, 15).Select(i => new SymbolItem
        {
            Id = $"mcb-{i}",
            Type = "MCB 1P",
            Phase = "L1",
            PowerW = 200,
            Width = 18,
            Height = 90
        }).ToList();

        await PhaseDistributionCalculator.BalancePhasesAsync(
            symbols, BalanceMode.Current, BalanceScope.AllSinglePhase, 230);

        var dist = PhaseDistributionCalculator.CalculateTotalDistribution(symbols);
        var imbalance = PhaseDistributionCalculator.CalculateImbalancePercent(
            dist.L1PowerW, dist.L2PowerW, dist.L3PowerW);

        // 15 = 5+5+5, powinno być idealnie
        Assert.True(imbalance < 1.0, $"Oczekiwano asymetrii < 1%, otrzymano {imbalance:F1}%");
    }
}
