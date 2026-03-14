using Xunit;
using DINBoard.Services;
using DINBoard.Models;

namespace Avalonia.Tests;

public class ModuleTypeServiceTests
{
    private readonly ModuleTypeService _service = new();

    [Theory]
    [InlineData("RCD_2P_40A.svg", "", ModuleType.RCD)]
    [InlineData("rcd_4p.svg", "", ModuleType.RCD)]
    [InlineData("", "RCD", ModuleType.RCD)]
    [InlineData("MCB_1P_B16.svg", "", ModuleType.MCB)]
    [InlineData("mcb_3p.svg", "", ModuleType.MCB)]
    [InlineData("", "MCB", ModuleType.MCB)]
    [InlineData("SPD_T2.svg", "", ModuleType.SPD)]
    [InlineData("", "SPD", ModuleType.SPD)]
    [InlineData("unknown.svg", "", ModuleType.Unknown)]
    [InlineData("", "CustomType", ModuleType.Other)]
    public void GetModuleType_ShouldReturnCorrectType(string visualPath, string type, ModuleType expected)
    {
        var symbol = new SymbolItem { VisualPath = visualPath, Type = type };

        var result = _service.GetModuleType(symbol);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetModuleType_WithNullSymbol_ShouldReturnUnknown()
    {
        var result = _service.GetModuleType(null);

        Assert.Equal(ModuleType.Unknown, result);
    }

    [Theory]
    [InlineData("RCD_2P.svg", "", true)]
    [InlineData("MCB_1P.svg", "", false)]
    [InlineData("", "RCD", true)]
    public void IsRcd_ShouldReturnCorrectValue(string visualPath, string type, bool expected)
    {
        var symbol = new SymbolItem { VisualPath = visualPath, Type = type };

        var result = _service.IsRcd(symbol);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("MCB_1P.svg", "", true)]
    [InlineData("RCD_2P.svg", "", false)]
    [InlineData("", "MCB", true)]
    public void IsMcb_ShouldReturnCorrectValue(string visualPath, string type, bool expected)
    {
        var symbol = new SymbolItem { VisualPath = visualPath, Type = type };

        var result = _service.IsMcb(symbol);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("SPD_T2.svg", "", true)]
    [InlineData("MCB_1P.svg", "", false)]
    public void IsSpd_ShouldReturnCorrectValue(string visualPath, string type, bool expected)
    {
        var symbol = new SymbolItem { VisualPath = visualPath, Type = type };

        var result = _service.IsSpd(symbol);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("RCD_2P.svg", "", "RCD")]
    [InlineData("MCB_1P.svg", "", "MCB")]
    [InlineData("SPD_T2.svg", "", "SPD")]
    [InlineData("", "CustomType", "CustomType")]
    [InlineData("unknown.svg", "", "?")]
    public void GetModuleTypeName_ShouldReturnCorrectName(string visualPath, string type, string expected)
    {
        var symbol = new SymbolItem { VisualPath = visualPath, Type = type };

        var result = _service.GetModuleTypeName(symbol);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CaseInsensitive_ShouldWork()
    {
        var lowerCase = new SymbolItem { VisualPath = "rcd_2p.svg" };
        var upperCase = new SymbolItem { VisualPath = "RCD_2P.svg" };
        var mixedCase = new SymbolItem { VisualPath = "Rcd_2P.svg" };

        Assert.True(_service.IsRcd(lowerCase));
        Assert.True(_service.IsRcd(upperCase));
        Assert.True(_service.IsRcd(mixedCase));
    }

    [Theory]
    [InlineData(18, 90, ModulePoleCount.P1)]
    [InlineData(36, 90, ModulePoleCount.P2)]
    [InlineData(54, 90, ModulePoleCount.P3)]
    [InlineData(72, 90, ModulePoleCount.P4)]
    public void GetPoleCount_ShouldUseAspectRatioFallback_WhenNoTextHints(double width, double height, ModulePoleCount expected)
    {
        var symbol = new SymbolItem
        {
            VisualPath = "unknown.svg",
            Type = "Unknown",
            Width = width,
            Height = height
        };

        var result = _service.GetPoleCount(symbol);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(ModulePoleCount.P1, "L1")]
    [InlineData(ModulePoleCount.P2, "L1+L2")]
    [InlineData(ModulePoleCount.P3, "L1+L2+L3")]
    [InlineData(ModulePoleCount.P4, "L1+L2+L3")]
    [InlineData(ModulePoleCount.Unknown, "L1")]
    public void GetDefaultPhaseForPoleCount_ShouldReturnExpectedDefault(ModulePoleCount poles, string expected)
    {
        var result = _service.GetDefaultPhaseForPoleCount(poles);

        Assert.Equal(expected, result);
    }
}
