using DINBoard.Services;
using Xunit;

namespace Avalonia.Tests;

public class CableSizeValidationCalculatorTests
{
    private readonly CableSizeValidationCalculator _calculator = new(new CableVoltageDropCalculator());

    [Fact]
    public void Validate_AdequateCable_ShouldBeValid()
    {
        var result = _calculator.Validate(currentA: 10, crossSectionMm2: 2.5, lengthM: 10);

        Assert.True(result.IsValid);
        Assert.Equal(21.0, result.MaxCurrentA);
    }

    [Fact]
    public void Validate_LongCable_ShouldFailVoltageDropCriterion()
    {
        var result = _calculator.Validate(currentA: 16, crossSectionMm2: 1.5, lengthM: 50);

        Assert.False(result.IsVoltageDropOk);
        Assert.True(result.VoltageDropPercent > 3.0);
    }
}
