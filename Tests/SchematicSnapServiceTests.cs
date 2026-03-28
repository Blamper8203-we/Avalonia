using System.Collections.Generic;
using Avalonia;
using DINBoard.Constants;
using DINBoard.Models;
using DINBoard.Services;
using Xunit;

namespace Avalonia.Tests;

public class SchematicSnapServiceTests
{
    private readonly SchematicSnapService _service = new();

    [Fact]
    public void CalculateSnap_WhenPointerIsNearDinRailAxis_ShouldSnapVerticallyToRail()
    {
        var result = _service.CalculateSnap(
            pointerPos: new Point(200, 95),
            selfWidth: 20,
            selfHeight: 40,
            symbols: new List<SymbolItem>(),
            dinRailAxes: new List<double> { 100 },
            dinRailScale: 1.0);

        Assert.True(result.IsRailSnapped);
        Assert.Equal(80, result.SnappedY, 3);
        Assert.False(result.IsHorizontalSnapped);
        Assert.True(result.IsSnapped);
    }

    [Fact]
    public void CalculateSnap_WhenPointerIsFarFromDinRailAxis_ShouldNotSnapVerticallyToRail()
    {
        var result = _service.CalculateSnap(
            pointerPos: new Point(200, 200),
            selfWidth: 20,
            selfHeight: 40,
            symbols: new List<SymbolItem>(),
            dinRailAxes: new List<double> { 100 },
            dinRailScale: 1.0);

        Assert.False(result.IsRailSnapped);
        Assert.Equal(180, result.SnappedY, 3);
        Assert.False(result.IsHorizontalSnapped);
        Assert.False(result.IsSnapped);
    }

    [Fact]
    public void CalculateSnap_WhenCandidateOnSameRailAndWithinThreshold_ShouldSnapHorizontallyToRightGap()
    {
        var existing = new SymbolItem
        {
            Id = "mcb-1",
            Type = "MCB",
            X = 100,
            Y = 80,
            Width = 36,
            Height = 40
        };

        var result = _service.CalculateSnap(
            pointerPos: new Point(150, 100),
            selfWidth: 20,
            selfHeight: 40,
            symbols: new List<SymbolItem> { existing },
            dinRailAxes: new List<double> { 100 },
            dinRailScale: 1.0);

        var expectedGap = AppDefaults.SnapGapUnit * AppDefaults.ModuleUnitWidth;
        var expectedX = existing.X + existing.Width + expectedGap;

        Assert.True(result.IsHorizontalSnapped);
        Assert.Same(existing, result.SnapTarget);
        Assert.Equal(expectedX, result.SnappedX, 3);
    }

    [Fact]
    public void CalculateSnap_WhenCandidateIsExcluded_ShouldNotSnapHorizontallyToThatCandidate()
    {
        var existing = new SymbolItem
        {
            Id = "mcb-1",
            Type = "MCB",
            X = 100,
            Y = 80,
            Width = 36,
            Height = 40
        };

        var result = _service.CalculateSnap(
            pointerPos: new Point(150, 100),
            selfWidth: 20,
            selfHeight: 40,
            symbols: new List<SymbolItem> { existing },
            dinRailAxes: new List<double> { 100 },
            dinRailScale: 1.0,
            excludeSymbols: new[] { existing });

        Assert.False(result.IsHorizontalSnapped);
        Assert.Null(result.SnapTarget);
        Assert.Equal(140, result.SnappedX, 3);
    }

    [Fact]
    public void CalculateSnap_WhenOverlapDetectedAndOnlyLeftSideFree_ShouldUseLeftFallbackPosition()
    {
        var main = new SymbolItem
        {
            Id = "mcb-main",
            Type = "MCB",
            X = 100,
            Y = 80,
            Width = 36,
            Height = 40
        };

        var blockerOnRight = new SymbolItem
        {
            Id = "mcb-right",
            Type = "MCB",
            X = 150,
            Y = 80,
            Width = 20,
            Height = 40
        };

        var result = _service.CalculateSnap(
            pointerPos: new Point(120, 100),
            selfWidth: 30,
            selfHeight: 40,
            symbols: new List<SymbolItem> { main, blockerOnRight },
            dinRailAxes: new List<double> { 100 },
            dinRailScale: 1.0);

        var expectedGap = AppDefaults.SnapGapUnit * AppDefaults.ModuleUnitWidth;
        var expectedX = main.X - expectedGap - 30;

        Assert.True(result.IsHorizontalSnapped);
        Assert.Same(main, result.SnapTarget);
        Assert.Equal(expectedX, result.SnappedX, 3);
    }

    [Fact]
    public void CalculateSnap_WhenBusbarNearModuleBottom_ShouldSnapVerticallyAndAlignPins()
    {
        var module = new SymbolItem
        {
            Id = "mcb-1",
            Type = "MCB",
            X = 300,
            Y = 100,
            Width = 18,
            Height = 90
        };

        var selfHeight = PowerBusbarGenerator.BaseHeight;
        var selfWidth = PowerBusbarGenerator.BaseWidth;

        var firstPinCenter = PowerBusbarGenerator.BasePinStartX - PowerBusbarGenerator.PinCenterOffset;
        var terminalX = module.X + (module.Width * 0.5);
        var expectedX = terminalX - firstPinCenter;
        var pointerX = expectedX + (selfWidth / 2.0);

        var result = _service.CalculateSnap(
            pointerPos: new Point(pointerX, 200),
            selfWidth: selfWidth,
            selfHeight: selfHeight,
            symbols: new List<SymbolItem> { module },
            dinRailAxes: new List<double>(),
            dinRailScale: 1.0,
            selfLabel: "szyna_pradowa 3P");

        var expectedY = (module.Y + module.Height) + 2.5 - PowerBusbarGenerator.BodyY;

        Assert.True(result.IsRailSnapped);
        Assert.True(result.IsHorizontalSnapped);
        Assert.Equal(expectedY, result.SnappedY, 3);
        Assert.Equal(expectedX, result.SnappedX, 3);
    }
}
