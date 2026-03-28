using DINBoard.Services;
using Xunit;

namespace Avalonia.Tests;

public class ProtectionCurrentParserTests
{
    private readonly ProtectionCurrentParser _parser = new();

    [Theory]
    [InlineData("B16", 16)]
    [InlineData("C20", 20)]
    [InlineData("D32", 32)]
    [InlineData("16A", 16)]
    [InlineData("In=25A", 25)]
    public void Parse_WithKnownFormats_ShouldReturnCurrent(string input, int expected)
    {
        var result = _parser.Parse(input);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ABC")]
    public void Parse_WithUnknownOrEmptyInput_ShouldReturnZero(string input)
    {
        var result = _parser.Parse(input);

        Assert.Equal(0, result);
    }
}
