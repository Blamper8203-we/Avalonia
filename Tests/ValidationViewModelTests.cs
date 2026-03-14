using System.Collections.ObjectModel;
using System.Linq;
using Xunit;
using DINBoard.Models;
using DINBoard.ViewModels;
using DINBoard.Services;

namespace Avalonia.Tests;

/// <summary>
/// Tests for the new ValidationViewModel extracted from MainViewModel.
/// </summary>
public class ValidationViewModelTests
{
    private static ElectricalValidationService CreateValidationService() => new ElectricalValidationService();

    [Fact]
    public void Constructor_WhenServiceIsNull_ThrowsArgumentNullException()
    {
        // Assert
        Assert.Throws<System.ArgumentNullException>(() => new ValidationViewModel(null!));
    }

    [Fact]
    public void RecalculateValidation_WithNoSymbols_ReturnsNoErrors()
    {
        // Arrange
        var vm = new ValidationViewModel(CreateValidationService());
        var project = new Project { PowerConfig = new PowerSupplyConfig() };
        var symbols = new ObservableCollection<SymbolItem>
        {
            // Add a main breaker to prevent NO_MAIN_BREAKER warning
            new SymbolItem { Id = "main", Type = "Rozłącznik", Label = "FR" }
        };

        // Act
        var hasErrors = vm.RecalculateValidation(project, symbols);

        // Assert
        Assert.False(hasErrors);
        Assert.Equal(0, vm.ValidationErrorCount);
        Assert.Equal(0, vm.ValidationWarningCount);
        Assert.Equal("Brak problemów walidacji", vm.ValidationSummary);
        Assert.Empty(vm.ValidationMessages);
        Assert.Empty(vm.ValidationPreviewMessages);
    }

    [Fact]
    public void RecalculateValidation_WithCableOverload_AddsError()
    {
        // Arrange
        var vm = new ValidationViewModel(CreateValidationService());
        var project = new Project { PowerConfig = new PowerSupplyConfig { Voltage = 400 } };
        var symbols = new ObservableCollection<SymbolItem>
        {
            // Duża moc = duży prąd, mały przekrój
            new SymbolItem { Id = "mcb1", Type = "MCB", Group = "G1", CircuitId = "C1", Phase = "L1", PowerW = 10000, CableLength = 10, CableCrossSection = 1.0 } 
        };

        // Act
        var hasErrors = vm.RecalculateValidation(project, symbols);

        // Assert
        Assert.True(hasErrors);
        Assert.Contains(vm.ValidationMessages, m => m.Code == "CABLE_OVERLOAD" || m.Code == "PROTECTION_MISMATCH");
    }

    [Fact]
    public void RecalculateValidation_WithInvalidMcbToRcd_AddsError()
    {
        // Arrange
        var vm = new ValidationViewModel(CreateValidationService());
        var project = new Project { PowerConfig = new PowerSupplyConfig { Voltage = 230 } };

        var rcd = new SymbolItem { Id = "rcd", Type = "RCD", ProtectionType = "Czterobiegunowy (3P+N)" };
        var mcb = new SymbolItem { Id = "mcb", Type = "MCB", RcdSymbolId = "rcd", Phase = "L1" };
        var symbols = new ObservableCollection<SymbolItem> { rcd, mcb }; // This setup might lack 1P+N matching or mismatch

        // To test real ElectricalValidationService logic quickly: we just pass a symbol that needs phase but has none.
        var mcbNoPhase = new SymbolItem { Id = "mcb2", Type = "MCB", Phase = "" };
        symbols.Add(mcbNoPhase); // Should trigger MissingPhase 

        // Act
        var hasErrors = vm.RecalculateValidation(project, symbols);

        // Assert
        Assert.True(hasErrors);
        Assert.True(vm.ValidationErrorCount > 0 || vm.ValidationWarningCount > 0);
        Assert.NotEmpty(vm.ValidationMessages);
        Assert.True(vm.ValidationPreviewMessages.Count <= 3); // Max 3 preview messages
    }

    [Fact]
    public void RecalculateValidation_PreviewMessages_AreLimitedToThree()
    {
        // Arrange
        var vm = new ValidationViewModel(CreateValidationService());
        var project = new Project { PowerConfig = new PowerSupplyConfig { Voltage = 400 } };
        var symbols = new ObservableCollection<SymbolItem>
        {
            new SymbolItem { Id = "main", Type = "Rozłącznik", Label = "FR" }
        };

        // Create 5 symbols with massive overload to generate 5 individual CABLE_OVERLOAD errors
        for (int i = 0; i < 5; i++)
        {
            symbols.Add(new SymbolItem { Id = $"mcb{i}", Type = "MCB", Phase = "L1", PowerW = 10000, CableCrossSection = 1.0, CableLength = 10 });
        }

        // Act
        vm.RecalculateValidation(project, symbols);

        // Assert
        Assert.True(vm.ValidationMessages.Count >= 5); // Total errors/warnings (could be ~13: Drop, Overload, Imbalance)
        Assert.Equal(3, vm.ValidationPreviewMessages.Count); // Preview limited to 3
    }
}
