#nullable enable

using Xunit;
using DINBoard.ViewModels;

namespace DINBoard.Tests;

public class ProjectThemeViewModelTests
{
    [Fact]
    public void Constructor_InitializesDefaults()
    {
        // Act
        var vm = new ProjectThemeViewModel();

        // Assert
        Assert.Equal("Ciemny (Antracyt)", vm.SelectedTheme);
        Assert.True(vm.ShowBottomWires);
    }

    [Fact]
    public void AvailableThemes_ContainsFourThemes()
    {
        // Arrange & Act
        var vm = new ProjectThemeViewModel();

        // Assert
        Assert.Equal(4, vm.AvailableThemes.Count);
        Assert.Contains("Jasny", vm.AvailableThemes);
        Assert.Contains("Ciemny (Antracyt)", vm.AvailableThemes);
        Assert.Contains("Ciemny (Granat)", vm.AvailableThemes);
        Assert.Contains("Ciemny (Czerń)", vm.AvailableThemes);
    }

    [Fact]
    public void SelectedTheme_CanBeChanged()
    {
        // Arrange
        var vm = new ProjectThemeViewModel();

        // Act
        vm.SelectedTheme = "Jasny";

        // Assert
        Assert.Equal("Jasny", vm.SelectedTheme);
    }

    [Fact]
    public void OnThemeChanged_IsInvokedWhenThemeChanges()
    {
        // Arrange
        var vm = new ProjectThemeViewModel();
        var callbackCalled = false;
        string? changedTheme = null;

        vm.OnThemeChanged += (theme) =>
        {
            callbackCalled = true;
            changedTheme = theme;
        };

        // Act
        vm.SelectedTheme = "Jasny";

        // Assert
        Assert.True(callbackCalled);
        Assert.Equal("Jasny", changedTheme);
    }

    [Fact]
    public void ShowBottomWires_CanBeToggled()
    {
        // Arrange
        var vm = new ProjectThemeViewModel();

        // Act
        vm.ShowBottomWires = false;

        // Assert
        Assert.False(vm.ShowBottomWires);
    }
}
