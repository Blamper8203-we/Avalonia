using DINBoard.Services;
using Xunit;

namespace Avalonia.Tests;

public class CurrentFromPowerCalculatorTests
{
    private readonly CurrentFromPowerCalculator _calculator = new();

    [Fact]
    public void Calculate_ThreePhase_ShouldUseThreePhaseFormula()
    {
        var current = _calculator.Calculate(3000, "L1+L2+L3", 230);

        Assert.Equal(3000.0 / (3.0 * 230 * 0.9), current, 6);
    }

    [Fact]
    public void Calculate_SinglePhase_ShouldUseSinglePhaseFormula()
    {
        var current = _calculator.Calculate(2070, "L1", 230);

        Assert.Equal(10.0, current, 6);
    }

    [Fact]
    public void Calculate_ZeroPower_ShouldReturnZero()
    {
        var current = _calculator.Calculate(0, "L1", 230);

        Assert.Equal(0, current);
    }
}
