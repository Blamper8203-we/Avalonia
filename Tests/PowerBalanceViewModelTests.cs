using System.Collections.ObjectModel;
using Xunit;
using DINBoard.Models;
using DINBoard.ViewModels;

namespace DINBoard.Tests;

public class PowerBalanceViewModelTests
{
    [Fact]
    public void RecalculatePhaseBalance_DistributesLoad()
    {
        // Arrange
        var vm = new PowerBalanceViewModel();
        var symbols = new ObservableCollection<SymbolItem>
        {
            new SymbolItem { Phase = "L1", PowerW = 1000 },
            new SymbolItem { Phase = "L1", PowerW = 500 },
            new SymbolItem { Phase = "L2", PowerW = 800 },
            new SymbolItem { Phase = "L3", PowerW = 700 }
        };
        var project = new Project { PowerConfig = new PowerSupplyConfig { Voltage = 400 } };

        // Act
        vm.RecalculatePhaseBalance(symbols, project);

        // Assert
        Assert.Equal(1500, vm.L1PowerW);
        Assert.Equal(800, vm.L2PowerW);
        Assert.Equal(700, vm.L3PowerW);
        Assert.Equal(3000, vm.TotalInstalledPowerW);
    }

    [Fact]
    public void RecalculatePhaseBalance_CalculatesCurrents()
    {
        // Arrange
        var vm = new PowerBalanceViewModel();
        var symbols = new ObservableCollection<SymbolItem>
        {
            new SymbolItem { Phase = "L1", PowerW = 4000 }
        };
        var project = new Project { PowerConfig = new PowerSupplyConfig { Voltage = 400 } };

        // Act
        vm.RecalculatePhaseBalance(symbols, project);

        // Assert
        Assert.Equal(19.25, vm.L1CurrentA, 2); // 4000W / (400V / sqrt(3) * 0.9) ~= 19.25A
        Assert.Equal(0, vm.L2CurrentA);
        Assert.Equal(0, vm.L3CurrentA);
    }

    [Fact]
    public void RecalculatePhaseBalance_CalculatesAsymmetry()
    {
        // Arrange
        var vm = new PowerBalanceViewModel();
        var symbols = new ObservableCollection<SymbolItem>
        {
            new SymbolItem { Phase = "L1", PowerW = 3000 },
            new SymbolItem { Phase = "L2", PowerW = 2000 },
            new SymbolItem { Phase = "L3", PowerW = 1000 }
        };
        var project = new Project { PowerConfig = new PowerSupplyConfig { Voltage = 400 } };

        // Act
        vm.RecalculatePhaseBalance(symbols, project);

        // Assert - asymmetry should be ~50%
        Assert.True(vm.PhaseImbalancePercent > 40 && vm.PhaseImbalancePercent < 60);
        Assert.False(vm.IsPhaseBalanceOk); // > 15%
    }

    [Fact]
    public void IsPhaseBalanceOk_ReturnsTrueWhenAsymmetryLow()
    {
        // Arrange
        var vm = new PowerBalanceViewModel();
        var symbols = new ObservableCollection<SymbolItem>
        {
            new SymbolItem { Phase = "L1", PowerW = 1000 },
            new SymbolItem { Phase = "L2", PowerW = 1100 },
            new SymbolItem { Phase = "L3", PowerW = 1050 }
        };
        var project = new Project { PowerConfig = new PowerSupplyConfig { Voltage = 400 } };

        // Act
        vm.RecalculatePhaseBalance(symbols, project);

        // Assert
        Assert.True(vm.IsPhaseBalanceOk); // Asymmetry <= 15%
    }

    [Fact]
    public void MaxPhaseCurrentA_ReturnsMaximum()
    {
        // Arrange
        var vm = new PowerBalanceViewModel();
        vm.L1CurrentA = 5;
        vm.L2CurrentA = 15;
        vm.L3CurrentA = 8;

        // Act & Assert
        Assert.Equal(15, vm.MaxPhaseCurrentA);
    }

    [Fact]
    public void CalculatedPowerW_AppliesSimultaneityFactor()
    {
        // Arrange
        var vm = new PowerBalanceViewModel();
        var symbols = new ObservableCollection<SymbolItem>
        {
            new SymbolItem { Phase = "L1", PowerW = 1000 }
        };
        var project = new Project { PowerConfig = new PowerSupplyConfig { Voltage = 400 } };
        vm.SimultaneityFactor = 0.8;

        // Act
        vm.RecalculatePhaseBalance(symbols, project);

        // Assert
        Assert.Equal(1000 * 0.8, vm.CalculatedPowerW);
    }

    [Fact]
    public void RecalculatePhaseBalance_UsesConnectionPowerFromProjectConfig()
    {
        // Arrange
        var vm = new PowerBalanceViewModel();
        var symbols = new ObservableCollection<SymbolItem>
        {
            new SymbolItem { Phase = "L1", PowerW = 4000 },
            new SymbolItem { Phase = "L2", PowerW = 3000 },
            new SymbolItem { Phase = "L3", PowerW = 3000 }
        };
        var project = new Project
        {
            PowerConfig = new PowerSupplyConfig
            {
                Voltage = 400,
                PowerKw = 6
            }
        };
        vm.SimultaneityFactor = 0.8; // 10 kW * 0.8 = 8 kW

        // Act
        vm.RecalculatePhaseBalance(symbols, project);

        // Assert
        Assert.Equal(6000, vm.ConnectionPowerW);
        Assert.Equal(8000, vm.CalculatedPowerW);
        Assert.Equal(133.33, vm.ConnectionPowerUsagePercent, 2);
        Assert.Equal(-2000, vm.ConnectionPowerReserveW);
        Assert.True(vm.IsConnectionPowerExceeded);
        Assert.True(vm.IsConnectionPowerStatusError);
    }
}
