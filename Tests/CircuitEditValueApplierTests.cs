#nullable enable
using System.Collections.Generic;
using DINBoard.Models;
using DINBoard.Services;
using Xunit;

namespace Avalonia.Tests;

public class CircuitEditValueApplierTests
{
    [Fact]
    public void Apply_Phase_ShouldUpdatePhaseAndMarkManual()
    {
        var symbol = new SymbolItem();

        CircuitEditValueApplier.Apply(symbol, "Phase", "L2");

        Assert.Equal("L2", symbol.Phase);
        Assert.NotNull(symbol.Parameters);
        Assert.Equal("true", symbol.Parameters!["ManualPhase"]);
    }

    [Fact]
    public void Apply_CableMetadata_ShouldStoreValueInParameters()
    {
        var symbol = new SymbolItem
        {
            Parameters = new Dictionary<string, string>
            {
                ["Existing"] = "keep"
            }
        };

        CircuitEditValueApplier.Apply(symbol, "CableType", "YDY 3x2.5");

        Assert.NotNull(symbol.Parameters);
        Assert.Equal("keep", symbol.Parameters!["Existing"]);
        Assert.Equal("YDY 3x2.5", symbol.Parameters["CableType"]);
    }

    [Fact]
    public void Apply_RcdPreset_ShouldParseCurrentResidualAndType()
    {
        var symbol = new SymbolItem();

        CircuitEditValueApplier.Apply(symbol, "RcdPreset", "40A/30mA Typ A");

        Assert.Equal(40, symbol.RcdRatedCurrent);
        Assert.Equal(30, symbol.RcdResidualCurrent);
        Assert.Equal("A", symbol.RcdType);
    }

    [Fact]
    public void Apply_EmptyRcdPreset_ShouldRestoreDefaults()
    {
        var symbol = new SymbolItem
        {
            RcdRatedCurrent = 63,
            RcdResidualCurrent = 300,
            RcdType = "B"
        };

        CircuitEditValueApplier.Apply(symbol, "RcdPreset", "");

        Assert.Equal(40, symbol.RcdRatedCurrent);
        Assert.Equal(30, symbol.RcdResidualCurrent);
        Assert.Equal("A", symbol.RcdType);
    }

    [Fact]
    public void Apply_SpdPreset_ShouldParseTypeVoltageAndDischargeCurrent()
    {
        var symbol = new SymbolItem();

        CircuitEditValueApplier.Apply(symbol, "SpdPreset", "T1+T2 275V 25kA");

        Assert.Equal("T1+T2", symbol.SpdType);
        Assert.Equal(275, symbol.SpdVoltage);
        Assert.Equal(25, symbol.SpdDischargeCurrent);
    }

    [Fact]
    public void Apply_EmptySpdPreset_ShouldRestoreDefaults()
    {
        var symbol = new SymbolItem
        {
            SpdType = "T2",
            SpdVoltage = 320,
            SpdDischargeCurrent = 40
        };

        CircuitEditValueApplier.Apply(symbol, "SpdPreset", "");

        Assert.Equal("T1+T2", symbol.SpdType);
        Assert.Equal(275, symbol.SpdVoltage);
        Assert.Equal(25, symbol.SpdDischargeCurrent);
    }

    [Fact]
    public void Apply_InvalidNumericValue_ShouldLeaveExistingPowerUnchanged()
    {
        var symbol = new SymbolItem
        {
            PowerW = 1234
        };

        CircuitEditValueApplier.Apply(symbol, "PowerW", "not-a-number");

        Assert.Equal(1234, symbol.PowerW);
    }

    [Fact]
    public void Apply_CircuitType_ShouldUpdateCircuitType()
    {
        var symbol = new SymbolItem
        {
            CircuitType = "Gniazdo"
        };

        CircuitEditValueApplier.Apply(symbol, "CircuitType", "Siła");

        Assert.Equal("Siła", symbol.CircuitType);
    }
}
