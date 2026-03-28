using System.Collections.Generic;
using DINBoard.Models;
using DINBoard.Services;
using Xunit;

namespace Avalonia.Tests;

public class MainBreakerWarningRuleTests
{
    private readonly MainBreakerWarningRule _rule = new();

    [Fact]
    public void Evaluate_WhenMainBreakerIsMissing_ShouldReturnWarning()
    {
        var symbols = new List<SymbolItem>
        {
            new() { Id = "mcb1", Type = "MCB" }
        };

        var warning = _rule.Evaluate(symbols);

        Assert.NotNull(warning);
        Assert.Equal("NO_MAIN_BREAKER", warning.Code);
        Assert.Equal(ValidationSeverity.Warning, warning.Severity);
    }

    [Fact]
    public void Evaluate_WhenSwitchBreakerExists_ShouldReturnNull()
    {
        var symbols = new List<SymbolItem>
        {
            new() { Id = "main", Type = "Switch Disconnector" },
            new() { Id = "mcb1", Type = "MCB" }
        };

        var warning = _rule.Evaluate(symbols);

        Assert.Null(warning);
    }
}
