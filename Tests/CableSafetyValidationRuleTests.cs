using System.Collections.Generic;
using DINBoard.Models;
using DINBoard.Services;
using Xunit;

namespace Avalonia.Tests;

public class CableSafetyValidationRuleTests
{
    private readonly CableSafetyValidationRule _rule = new(
        new CurrentFromPowerCalculator(),
        new CableSizeValidationCalculator(new CableVoltageDropCalculator()),
        new CircuitVoltageDropLimitProvider());

    [Fact]
    public void Evaluate_WhenCableIsOverloaded_ShouldReturnCableOverloadError()
    {
        var symbols = new List<SymbolItem>
        {
            new() { Id = "c1", Label = "Obwód 1", CableCrossSection = 1.5, PowerW = 5000, Phase = "L1", CableLength = 10 }
        };

        var result = _rule.Evaluate(symbols, phaseVoltage: 230);

        Assert.Contains(result.Errors, e => e.Code == "CABLE_OVERLOAD" && e.SymbolId == "c1");
    }

    [Fact]
    public void Evaluate_WhenVoltageDropExceedsLimit_ShouldReturnVoltageDropWarning()
    {
        var symbols = new List<SymbolItem>
        {
            new() { Id = "c1", Label = "Obwód 1", CableCrossSection = 1.5, PowerW = 3000, Phase = "L1", CableLength = 50, CircuitType = "Gniazda" }
        };

        var result = _rule.Evaluate(symbols, phaseVoltage: 230);

        Assert.Contains(result.Warnings, w => w.Code == "VOLTAGE_DROP" && w.SymbolId == "c1");
    }

    [Fact]
    public void Evaluate_WhenCableIsValidAndVoltageDropIsWithinLimit_ShouldReturnNoMessages()
    {
        var symbols = new List<SymbolItem>
        {
            new() { Id = "c1", Label = "Obwód 1", CableCrossSection = 2.5, PowerW = 1000, Phase = "L1", CableLength = 10 }
        };

        var result = _rule.Evaluate(symbols, phaseVoltage: 230);

        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }
}
