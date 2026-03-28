using System.Collections.Generic;
using DINBoard.Models;
using DINBoard.Services;
using Xunit;

namespace Avalonia.Tests;

public class PhaseLoadCalculationServiceTests
{
    private readonly PhaseLoadCalculationService _service = new();

    [Fact]
    public void Calculate_ThreePhaseLoad_ShouldDistributeEvenly()
    {
        var symbols = new List<SymbolItem>
        {
            new() { PowerW = 3000, Phase = "L1+L2+L3" }
        };

        var result = _service.Calculate(symbols);

        Assert.Equal(1000, result.L1PowerW);
        Assert.Equal(1000, result.L2PowerW);
        Assert.Equal(1000, result.L3PowerW);
    }

    [Fact]
    public void Calculate_ImbalancedLoads_ShouldReturnPositiveImbalance()
    {
        var symbols = new List<SymbolItem>
        {
            new() { PowerW = 3000, Phase = "L1" },
            new() { PowerW = 1000, Phase = "L2" },
            new() { PowerW = 1000, Phase = "L3" }
        };

        var result = _service.Calculate(symbols);

        Assert.True(result.ImbalancePercent > 0);
    }
}
