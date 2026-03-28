using System.Collections.Generic;
using DINBoard.Models;
using DINBoard.Services;
using Xunit;

namespace Avalonia.Tests;

public class RcdOverloadWarningRuleTests
{
    private readonly RcdOverloadWarningRule _rule = new(new ProtectionCurrentParser());

    [Fact]
    public void Evaluate_WhenSumOfMcbCurrentsExceedsRcdAndMainProtectionIsHigher_ShouldReturnWarning()
    {
        var symbols = new List<SymbolItem>
        {
            new() { Id = "rcd1", Type = "RCD", Label = "RCD-1", ProtectionType = "25A" },
            new() { Id = "mcb1", Type = "MCB", RcdSymbolId = "rcd1", ProtectionType = "B16" },
            new() { Id = "mcb2", Type = "MCB", RcdSymbolId = "rcd1", ProtectionType = "B16" }
        };

        var warnings = _rule.Evaluate(symbols, mainProtection: 63);

        Assert.Contains(warnings, w => w.Code == "RCD_OVERLOAD" && w.SymbolId == "rcd1");
    }

    [Fact]
    public void Evaluate_WhenMainProtectionIsNotHigherThanRcd_ShouldNotReturnWarning()
    {
        var symbols = new List<SymbolItem>
        {
            new() { Id = "rcd1", Type = "RCD", Label = "RCD-1", ProtectionType = "25A" },
            new() { Id = "mcb1", Type = "MCB", RcdSymbolId = "rcd1", ProtectionType = "B16" },
            new() { Id = "mcb2", Type = "MCB", RcdSymbolId = "rcd1", ProtectionType = "B16" }
        };

        var warnings = _rule.Evaluate(symbols, mainProtection: 25);

        Assert.DoesNotContain(warnings, w => w.Code == "RCD_OVERLOAD");
    }
}
