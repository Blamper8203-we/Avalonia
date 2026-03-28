using DINBoard.Services;
using Xunit;

namespace Avalonia.Tests;

public class CircuitVoltageDropLimitProviderTests
{
    private readonly CircuitVoltageDropLimitProvider _provider = new();

    [Fact]
    public void GetMaxVoltageDrop_Lighting_ShouldReturnThreePercent()
    {
        var limit = _provider.GetMaxVoltageDrop("oświetlenie");

        Assert.Equal(3.0, limit);
    }

    [Fact]
    public void GetMaxVoltageDrop_Default_ShouldReturnFivePercent()
    {
        var limit = _provider.GetMaxVoltageDrop("gniazda");

        Assert.Equal(5.0, limit);
    }
}
