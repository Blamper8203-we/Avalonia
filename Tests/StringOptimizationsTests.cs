using System;
using Xunit;
using DINBoard.Services;

namespace DINBoard.Tests;

public class StringOptimizationsTests
{
    [Theory]
    [InlineData(1, 2, "wire_1_2")]
    [InlineData(100, 200, "wire_100_200")]
    [InlineData(0, 0, "wire_0_0")]
    public void GenerateWireId_ShouldReturnCorrectFormat(int fromId, int toId, string expected)
    {
        // Act
        var result = StringOptimizations.GenerateWireId(fromId, toId);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("MCB", 1, "MCB1")]
    [InlineData("RCD", 10, "RCD10")]
    [InlineData("Q", 123, "Q123")]
    public void FormatModuleLabel_ShouldReturnCorrectFormat(string prefix, int number, string expected)
    {
        // Act
        var result = StringOptimizations.FormatModuleLabel(prefix.AsSpan(), number);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(45, 90, "45x90")]
    [InlineData(18, 120, "18x120")]
    [InlineData(0, 0, "0x0")]
    public void FormatDimensions_ShouldReturnCorrectFormat(int width, int height, string expected)
    {
        // Act
        var result = StringOptimizations.FormatDimensions(width, height);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Hello World", "world", true)]
    [InlineData("Hello World", "HELLO", true)]
    [InlineData("Hello World", "xyz", false)]
    public void ContainsIgnoreCase_ShouldWorkCorrectly(string source, string value, bool expected)
    {
        // Act
        var result = StringOptimizations.ContainsIgnoreCase(source.AsSpan(), value.AsSpan());

        // Assert
        Assert.Equal(expected, result);
    }
}
