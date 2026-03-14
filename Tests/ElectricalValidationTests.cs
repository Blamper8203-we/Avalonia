using System.Collections.Generic;
using Xunit;
using DINBoard.Services;
using DINBoard.Models;

namespace Avalonia.Tests;

public class ElectricalValidationTests
{
    private readonly ElectricalValidationService _service = new();

    #region Phase Load Tests

    [Fact]
    public void CalculatePhaseLoads_SinglePhaseL1_ShouldAddToL1()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { PowerW = 1000, Phase = "L1" }
        };

        var result = _service.CalculatePhaseLoads(symbols);

        Assert.Equal(1000, result.L1PowerW);
        Assert.Equal(0, result.L2PowerW);
        Assert.Equal(0, result.L3PowerW);
        Assert.True(result.L1CurrentA > 0);
    }

    [Fact]
    public void CalculatePhaseLoads_ThreePhase_ShouldDistributeEvenly()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { PowerW = 3000, Phase = "L1+L2+L3" }
        };

        var result = _service.CalculatePhaseLoads(symbols);

        Assert.Equal(1000, result.L1PowerW);
        Assert.Equal(1000, result.L2PowerW);
        Assert.Equal(1000, result.L3PowerW);
    }

    [Fact]
    public void CalculatePhaseLoads_Imbalanced_ShouldCalculateImbalance()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { PowerW = 3000, Phase = "L1" },
            new SymbolItem { PowerW = 1000, Phase = "L2" },
            new SymbolItem { PowerW = 1000, Phase = "L3" }
        };

        var result = _service.CalculatePhaseLoads(symbols);

        Assert.True(result.ImbalancePercent > 0);
    }

    [Fact]
    public void CalculatePhaseLoads_Balanced_ShouldHaveZeroImbalance()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { PowerW = 1000, Phase = "L1" },
            new SymbolItem { PowerW = 1000, Phase = "L2" },
            new SymbolItem { PowerW = 1000, Phase = "L3" }
        };

        var result = _service.CalculatePhaseLoads(symbols);

        Assert.Equal(0, result.ImbalancePercent, 1);
    }

    #endregion

    #region Cable Validation Tests

    [Fact]
    public void ValidateCableSize_1_5mm_Under15A_ShouldBeValid()
    {
        var result = _service.ValidateCableSize(currentA: 10, crossSectionMm2: 1.5, lengthM: 10);

        Assert.True(result.IsValid);
        Assert.Equal(15.5, result.MaxCurrentA);
    }

    [Fact]
    public void ValidateCableSize_1_5mm_Over15A_ShouldBeInvalid()
    {
        var result = _service.ValidateCableSize(currentA: 20, crossSectionMm2: 1.5, lengthM: 10);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateCableSize_2_5mm_ShouldHave21ACapacity()
    {
        var result = _service.ValidateCableSize(currentA: 16, crossSectionMm2: 2.5, lengthM: 10);

        Assert.True(result.IsValid);
        Assert.Equal(21.0, result.MaxCurrentA);
    }

    [Fact]
    public void ValidateCableSize_LongCable_ShouldHaveHighVoltageDrop()
    {
        var result = _service.ValidateCableSize(currentA: 16, crossSectionMm2: 1.5, lengthM: 50);

        Assert.True(result.VoltageDropPercent > 3.0);
        Assert.False(result.IsVoltageDropOk);
    }

    [Fact]
    public void ValidateCableSize_ShortCable_ShouldHaveLowVoltageDrop()
    {
        var result = _service.ValidateCableSize(currentA: 10, crossSectionMm2: 2.5, lengthM: 10);

        Assert.True(result.VoltageDropPercent < 3.0);
        Assert.True(result.IsVoltageDropOk);
    }

    #endregion

    #region Voltage Drop Tests

    [Fact]
    public void CalculateVoltageDrop_ShouldBeProportionalToLength()
    {
        var drop10m = _service.CalculateVoltageDrop(10, 2.5, 10);
        var drop20m = _service.CalculateVoltageDrop(10, 2.5, 20);

        Assert.Equal(drop10m * 2, drop20m, 2);
    }

    [Fact]
    public void CalculateVoltageDrop_ShouldBeInverselyProportionalToCrossSection()
    {
        var drop2_5mm = _service.CalculateVoltageDrop(10, 2.5, 20);
        var drop5mm = _service.CalculateVoltageDrop(10, 5.0, 20);

        Assert.Equal(drop2_5mm / 2, drop5mm, 2);
    }

    [Fact]
    public void CalculateVoltageDrop_ZeroCrossSection_ShouldReturnZero()
    {
        var drop = _service.CalculateVoltageDrop(10, 0, 20);

        Assert.Equal(0, drop);
    }

    #endregion

    #region Project Validation Tests

    [Fact]
    public void ValidateProject_OverloadedCable_ShouldReturnError()
    {
        var project = new Project();
        var symbols = new List<SymbolItem>
        {
            new SymbolItem 
            { 
                Id = "test",
                Label = "Obwód 1",
                PowerW = 5000, 
                Phase = "L1", 
                CableCrossSection = 1.5, 
                CableLength = 10 
            }
        };

        var result = _service.ValidateProject(project, symbols);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "CABLE_OVERLOAD");
    }

    [Fact]
    public void ValidateProject_ProtectionTooHighForCable_ShouldReturnError()
    {
        var project = new Project();
        var symbols = new List<SymbolItem>
        {
            new SymbolItem 
            { 
                Id = "test",
                ProtectionType = "B32", // 32A protection
                CableCrossSection = 1.5, // Max 15.5A
                PowerW = 100 // Low power, cable not overloaded by current
            }
        };

        var result = _service.ValidateProject(project, symbols);

        Assert.Contains(result.Errors, e => e.Code == "PROTECTION_MISMATCH");
    }

    [Fact]
    public void ValidateProject_HighPhaseImbalance_ShouldReturnWarning()
    {
        var project = new Project();
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { PowerW = 5000, Phase = "L1" },
            new SymbolItem { PowerW = 500, Phase = "L2" },
            new SymbolItem { PowerW = 500, Phase = "L3" }
        };

        var result = _service.ValidateProject(project, symbols);

        Assert.Contains(result.Warnings, w => w.Code == "PHASE_IMBALANCE");
    }

    [Fact]
    public void ValidateProject_McbWithoutRcd_ShouldReturnWarning()
    {
        var project = new Project();
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { Id = "mcb1", Type = "MCB", RcdSymbolId = null },
            new SymbolItem { Id = "mcb2", Type = "MCB", RcdSymbolId = null }
        };

        var result = _service.ValidateProject(project, symbols);

        Assert.Contains(result.Warnings, w => w.Code == "NO_RCD_PROTECTION");
    }

    [Fact]
    public void ValidateProject_McbWithRcd_ShouldNotWarn()
    {
        var project = new Project();
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { Id = "rcd1", Type = "RCD" },
            new SymbolItem { Id = "mcb1", Type = "MCB", RcdSymbolId = "rcd1" },
            new SymbolItem { Id = "mcb2", Type = "MCB", RcdSymbolId = "rcd1" }
        };

        var result = _service.ValidateProject(project, symbols);

        Assert.DoesNotContain(result.Warnings, w => w.Code == "NO_RCD_PROTECTION");
    }

    [Fact]
    public void ValidateProject_MainOverload_ShouldReturnError()
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
            new SymbolItem { PowerW = 5000 } // 5kW on 16A (3.68kW max)
        };

        var result = _service.ValidateProject(project, symbols);

        Assert.Contains(result.Errors, e => e.Code == "MAIN_OVERLOAD");
    }

    [Fact]
    public void ValidateProject_ValidProject_ShouldBeValid()
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
            new SymbolItem 
            { 
                Id = "rcd1", 
                Type = "RCD" 
            },
            new SymbolItem 
            { 
                Id = "mcb1", 
                Type = "MCB", 
                PowerW = 2000, 
                Phase = "L1",
                ProtectionType = "B16",
                CableCrossSection = 2.5,
                CableLength = 15,
                RcdSymbolId = "rcd1"
            },
            new SymbolItem 
            { 
                Id = "mcb2", 
                Type = "MCB", 
                PowerW = 2000, 
                Phase = "L2",
                ProtectionType = "B16",
                CableCrossSection = 2.5,
                CableLength = 15,
                RcdSymbolId = "rcd1"
            },
            new SymbolItem 
            { 
                Id = "mcb3", 
                Type = "MCB", 
                PowerW = 2000, 
                Phase = "L3",
                ProtectionType = "B16",
                CableCrossSection = 2.5,
                CableLength = 15,
                RcdSymbolId = "rcd1"
            }
        };

        var result = _service.ValidateProject(project, symbols);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    #endregion
}
