using DINBoard.Services;
using Xunit;

namespace Avalonia.Tests;

public class CableVoltageDropCalculatorTests
{
    private readonly CableVoltageDropCalculator _calculator = new();

    [Fact]
    public void Calculate_DoubleLength_ShouldDoubleVoltageDrop()
    {
        var drop10m = _calculator.Calculate(10, 2.5, 10);
        var drop20m = _calculator.Calculate(10, 2.5, 20);

        Assert.Equal(drop10m * 2, drop20m, 6);
    }

    [Fact]
    public void Calculate_ZeroCrossSection_ShouldReturnZero()
    {
        var drop = _calculator.Calculate(10, 0, 20);

        Assert.Equal(0, drop);
    }
}
