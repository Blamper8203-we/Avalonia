using System.Collections.Generic;
using DINBoard.Models;
using DINBoard.Services;
using Xunit;

namespace Avalonia.Tests;

public class MainOverloadValidationRuleTests
{
    private readonly MainOverloadValidationRule _rule = new();

    [Fact]
    public void Evaluate_WhenInstalledPowerExceedsMainProtectionLimit_ShouldReturnError()
    {
        var project = new Project
        {
            PowerConfig = new PowerSupplyConfig
            {
                Voltage = 230,
                MainProtection = 16,
                Phases = 1
            }
        };

        var symbols = new List<SymbolItem>
        {
            new() { PowerW = 5000 }
        };

        var result = _rule.Evaluate(project, symbols);

        Assert.NotNull(result);
        Assert.Equal("MAIN_OVERLOAD", result.Code);
        Assert.Equal(ValidationSeverity.Error, result.Severity);
    }

    [Fact]
    public void Evaluate_WhenInstalledPowerDoesNotExceedLimit_ShouldReturnNull()
    {
        var project = new Project
        {
            PowerConfig = new PowerSupplyConfig
            {
                Voltage = 400,
                MainProtection = 32,
                Phases = 3
            }
        };

        var symbols = new List<SymbolItem>
        {
            new() { PowerW = 6000 }
        };

        var result = _rule.Evaluate(project, symbols);

        Assert.Null(result);
    }
}
