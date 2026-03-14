using Avalonia;
using DINBoard.Models;
using DINBoard.Services;
using DINBoard.ViewModels;
using Xunit;

namespace Avalonia.Tests;

public class SchematicClonePlacementHelperTests
{
    [Fact]
    public void UpdateClonesPosition_WithEmptyClones_DoesNotThrow()
    {
        var vm = new MainViewModel();
        SchematicClonePlacementHelper.UpdateClonesPosition(vm, new Point(100, 100));
    }

    [Fact]
    public void UpdateClonesPosition_WithNullViewModel_DoesNotThrow()
    {
        SchematicClonePlacementHelper.UpdateClonesPosition(null!, new Point(100, 100));
    }

    [Fact]
    public void UpdateClonesPosition_WithOneClone_MovesCloneToCursor()
    {
        var vm = new MainViewModel();
        var clone = new SymbolItem { X = 50, Y = 50, Width = 36, Height = 52 };
        vm.ClonesToPlace.Add(clone);

        var cursorPos = new Point(200, 150);
        SchematicClonePlacementHelper.UpdateClonesPosition(vm, cursorPos);

        // Clone should move: targetX = 200 - 18 = 182, deltaX = 132, deltaY = 100 - 26 = 74 (approx)
        Assert.NotEqual(50, clone.X);
        Assert.NotEqual(50, clone.Y);
    }

    [Fact]
    public void UpdateClonesPosition_WithDinRailAxes_SnapsWhenNearAxis()
    {
        var vm = new MainViewModel();
        vm.Schematic.DinRailAxes.Add(100);
        var clone = new SymbolItem { X = 0, Y = 0, Width = 36, Height = 52 };
        vm.ClonesToPlace.Add(clone);

        // Cursor near axis Y=100 (within 80px)
        var cursorPos = new Point(50, 95);
        SchematicClonePlacementHelper.UpdateClonesPosition(vm, cursorPos);

        Assert.True(clone.IsSnappedToRail);
    }
}
