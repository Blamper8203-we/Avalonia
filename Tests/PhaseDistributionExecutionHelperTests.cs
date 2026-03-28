using System.Collections.Generic;
using System.Threading.Tasks;
using DINBoard.Models;
using DINBoard.Services;
using Xunit;

namespace Avalonia.Tests;

public class PhaseDistributionExecutionHelperTests
{
    [Fact]
    public async Task ExecuteAnimatedChangeAsync_ShouldApplyChangeAndClearSelection()
    {
        var symbols = new List<SymbolItem>
        {
            new() { Id = "mcb-1", Phase = "L1" },
            new() { Id = "mcb-2", Phase = "L1" }
        };

        await PhaseDistributionExecutionHelper.ExecuteAnimatedChangeAsync(
            symbols,
            () =>
            {
                symbols[0].Phase = "L2";
                symbols[1].Phase = "L2";
            },
            0,
            0);

        Assert.All(symbols, symbol => Assert.False(symbol.IsSelected));
        Assert.All(symbols, symbol => Assert.Equal("L2", symbol.Phase));
    }
}
