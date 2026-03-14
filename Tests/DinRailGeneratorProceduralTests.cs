using System;
using System.Linq;
using Xunit;
using DINBoard.Services;

namespace Avalonia.Tests;

public class DinRailGeneratorProceduralTests
{
    private readonly DinRailGeneratorProcedural _generator = new();

    #region Basic Generation Tests

    [Fact]
    public void Generate_SingleRow_SingleModule_ShouldReturnValidSvg()
    {
        var svg = _generator.Generate(rows: 1, modulesPerRow: 1);

        Assert.NotNull(svg);
        Assert.Contains("<svg", svg);
        Assert.Contains("</svg>", svg);
        Assert.Contains("xmlns=\"http://www.w3.org/2000/svg\"", svg);
    }

    [Fact]
    public void Generate_MultipleRows_ShouldContainMultipleGroups()
    {
        var svg = _generator.Generate(rows: 3, modulesPerRow: 10);

        // Każdy rząd powinien być w osobnej grupie <g transform="translate...">
        var groupCount = svg.Split("<g transform=\"translate(0,").Length - 1;
        Assert.Equal(3, groupCount);
    }

    [Fact]
    public void Generate_MultipleModules_ShouldScaleWidth()
    {
        var svg1Module = _generator.Generate(rows: 1, modulesPerRow: 1);
        var svg10Modules = _generator.Generate(rows: 1, modulesPerRow: 10);

        // Większa liczba modułów powinna dać większy viewBox
        var viewBox1 = ExtractViewBoxWidth(svg1Module);
        var viewBox10 = ExtractViewBoxWidth(svg10Modules);

        Assert.True(viewBox10 > viewBox1, "10 modules should have wider viewBox than 1 module");
    }

    [Fact]
    public void Generate_InvalidRows_ShouldReturnErrorSvg()
    {
        var svg = _generator.Generate(rows: 0, modulesPerRow: 10);

        Assert.Contains("Invalid dimensions", svg);
        Assert.Contains("fill='red'", svg);
    }

    [Fact]
    public void Generate_InvalidModules_ShouldReturnErrorSvg()
    {
        var svg = _generator.Generate(rows: 1, modulesPerRow: -5);

        Assert.Contains("Invalid dimensions", svg);
    }

    #endregion

    #region Dimension Tests

    [Fact]
    public void GetDimensions_SingleRow_ShouldReturnCorrectHeight()
    {
        var (width, height) = _generator.GetDimensions(rows: 1, modulesPerRow: 10);

        Assert.True(height > 0);
        Assert.True(width > 0);
        // Wysokość jednego rzędu powinna być stała (~1642)
        Assert.InRange(height, 1600, 1700);
    }

    [Fact]
    public void GetDimensions_MultipleRows_ShouldScaleHeight()
    {
        var (width1, height1) = _generator.GetDimensions(rows: 1, modulesPerRow: 10);
        var (width2, height2) = _generator.GetDimensions(rows: 2, modulesPerRow: 10);
        var (width3, height3) = _generator.GetDimensions(rows: 3, modulesPerRow: 10);

        // Szerokość powinna być taka sama (ta sama liczba modułów)
        Assert.Equal(width1, width2);
        Assert.Equal(width1, width3);

        // Wysokość powinna rosnąć z liczbą rzędów
        Assert.True(height2 > height1);
        Assert.True(height3 > height2);
    }

    [Fact]
    public void GetDimensions_SameAsGeneratedViewBox()
    {
        var (width, height) = _generator.GetDimensions(rows: 2, modulesPerRow: 15);
        var svg = _generator.Generate(rows: 2, modulesPerRow: 15);

        var viewBoxWidth = ExtractViewBoxWidth(svg);
        var viewBoxHeight = ExtractViewBoxHeight(svg);

        Assert.Equal(width, viewBoxWidth, 0.1);
        Assert.Equal(height, viewBoxHeight, 0.1);
    }

    #endregion

    #region Row Centers Tests

    [Fact]
    public void GetRowCenters_SingleRow_ShouldReturnOneCenter()
    {
        var centers = _generator.GetRowCenters(rows: 1);

        Assert.Single(centers);
        Assert.True(centers[0] > 0);
    }

    [Fact]
    public void GetRowCenters_MultipleRows_ShouldReturnCorrectCount()
    {
        var centers = _generator.GetRowCenters(rows: 5);

        Assert.Equal(5, centers.Count);
    }

    [Fact]
    public void GetRowCenters_ShouldBeAscending()
    {
        var centers = _generator.GetRowCenters(rows: 4);

        for (int i = 1; i < centers.Count; i++)
        {
            Assert.True(centers[i] > centers[i - 1], $"Row {i} center should be below row {i - 1}");
        }
    }

    [Fact]
    public void GetRowCenters_ShouldHaveEvenSpacing()
    {
        var centers = _generator.GetRowCenters(rows: 3);

        // Sprawdź, czy odstępy między centrami są równe
        var spacing1 = centers[1] - centers[0];
        var spacing2 = centers[2] - centers[1];

        Assert.Equal(spacing1, spacing2, 0.1);
    }

    #endregion

    #region SVG Structure Tests

    [Fact]
    public void Generate_ShouldContainVerticalGuides()
    {
        var svg = _generator.Generate(rows: 1, modulesPerRow: 10);

        // Powinny być 2 prostokąty (lewy i prawy)
        var rectCount = svg.Split("<rect").Length - 1;
        Assert.True(rectCount >= 2, "Should contain at least 2 vertical guide rectangles");
    }

    [Fact]
    public void Generate_ShouldContainScrews()
    {
        var svg = _generator.Generate(rows: 1, modulesPerRow: 10);

        // Śruby są reprezentowane przez <path> z określonym d="M181.452..."
        Assert.Contains("M181.452,822.032", svg);
    }

    [Fact]
    public void Generate_ShouldContainStyles()
    {
        var svg = _generator.Generate(rows: 1, modulesPerRow: 10);

        Assert.Contains("<defs>", svg);
        Assert.Contains("<style>", svg);
        Assert.Contains(".rail-stroke", svg);
        Assert.Contains("stroke:#1e1e1c", svg);
    }

    [Fact]
    public void Generate_LargeRail_ShouldContainHoles()
    {
        // Duża szyna (wiele modułów) powinna mieć otwory montażowe
        var svg = _generator.Generate(rows: 1, modulesPerRow: 20);

        // Otwory są reprezentowane przez <path> z określonym d="m 0,0 l -403.562..."
        Assert.Contains("l -403.562,0", svg);
    }

    [Fact]
    public void Generate_SmallRail_MayNotContainHoles()
    {
        // Mała szyna może nie mieć otworów (zależy od safeMargin)
        var svg = _generator.Generate(rows: 1, modulesPerRow: 2);

        // To nie jest błąd - po prostu dokumentujemy zachowanie
        Assert.NotNull(svg);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Generate_MinimumSize_ShouldWork()
    {
        var svg = _generator.Generate(rows: 1, modulesPerRow: 1);

        Assert.NotNull(svg);
        Assert.Contains("<svg", svg);
        Assert.DoesNotContain("Invalid", svg);
    }

    [Fact]
    public void Generate_LargeSize_ShouldWork()
    {
        var svg = _generator.Generate(rows: 10, modulesPerRow: 50);

        Assert.NotNull(svg);
        Assert.Contains("<svg", svg);
        Assert.DoesNotContain("Invalid", svg);
    }

    [Fact]
    public void Generate_ConsecutiveCalls_ShouldReturnSameResult()
    {
        var svg1 = _generator.Generate(rows: 2, modulesPerRow: 10);
        var svg2 = _generator.Generate(rows: 2, modulesPerRow: 10);

        Assert.Equal(svg1, svg2);
    }

    #endregion

    #region Helper Methods

    private double ExtractViewBoxWidth(string svg)
    {
        var viewBoxStart = svg.IndexOf("viewBox=\"") + 9;
        var viewBoxEnd = svg.IndexOf("\"", viewBoxStart);
        var viewBox = svg.Substring(viewBoxStart, viewBoxEnd - viewBoxStart);
        var parts = viewBox.Split(' ');
        return double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
    }

    private double ExtractViewBoxHeight(string svg)
    {
        var viewBoxStart = svg.IndexOf("viewBox=\"") + 9;
        var viewBoxEnd = svg.IndexOf("\"", viewBoxStart);
        var viewBox = svg.Substring(viewBoxStart, viewBoxEnd - viewBoxStart);
        var parts = viewBox.Split(' ');
        return double.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);
    }

    #endregion
}
