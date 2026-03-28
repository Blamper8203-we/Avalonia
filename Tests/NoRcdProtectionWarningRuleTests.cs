using System.Collections.Generic;
using DINBoard.Models;
using DINBoard.Services;
using Xunit;

namespace Avalonia.Tests;

public class NoRcdProtectionWarningRuleTests
{
    private readonly NoRcdProtectionWarningRule _rule = new();

    [Fact]
    public void Evaluate_WhenMcbWithoutRcdExists_ShouldReturnWarning()
    {
        var symbols = new List<SymbolItem>
        {
            new() { Id = "mcb1", Type = "MCB", RcdSymbolId = null },
            new() { Id = "mcb2", Type = "MCB", RcdSymbolId = null }
        };

        var warning = _rule.Evaluate(symbols);

        Assert.NotNull(warning);
        Assert.Equal("NO_RCD_PROTECTION", warning.Code);
        Assert.Equal(ValidationSeverity.Warning, warning.Severity);
    }

    [Fact]
    public void Evaluate_WhenAllMcbsHaveRcd_ShouldReturnNull()
    {
        var symbols = new List<SymbolItem>
        {
            new() { Id = "rcd1", Type = "RCD" },
            new() { Id = "mcb1", Type = "MCB", RcdSymbolId = "rcd1" },
            new() { Id = "mcb2", Type = "MCB", RcdSymbolId = "rcd1" }
        };

        var warning = _rule.Evaluate(symbols);

        Assert.Null(warning);
    }
}
