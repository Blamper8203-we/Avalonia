using System;
using DINBoard.Services.Pdf;
using Xunit;

namespace Avalonia.Tests;

public class PdfDesignCurrentConsistencyTests
{
    [Theory]
    [InlineData(12000d, 400d, true, 0.9d)]
    [InlineData(4600d, 400d, false, 0.9d)]
    [InlineData(5000d, 400d, true, 0.95d)]
    public void CalculateDesignCurrent_AllPdfServices_UseSameFormula(
        double powerW,
        double lineVoltageV,
        bool isThreePhase,
        double cosPhi)
    {
        var expected = CalculateExpected(powerW, lineVoltageV, isThreePhase, cosPhi);

        var connection = PdfConnectionService.CalculateDesignCurrent(powerW, lineVoltageV, isThreePhase, cosPhi);
        var powerBalance = PdfPowerBalanceService.CalculateDesignCurrent(powerW, lineVoltageV, isThreePhase, cosPhi);
        var latex = LatexExportService.CalculateDesignCurrent(powerW, lineVoltageV, isThreePhase, cosPhi);

        Assert.Equal(expected, connection, 10);
        Assert.Equal(expected, powerBalance, 10);
        Assert.Equal(expected, latex, 10);
        Assert.Equal(connection, powerBalance, 10);
        Assert.Equal(connection, latex, 10);
    }

    [Theory]
    [InlineData(0d, 400d, true, 0.9d)]
    [InlineData(-100d, 400d, true, 0.9d)]
    [InlineData(1000d, 0d, true, 0.9d)]
    [InlineData(1000d, 400d, true, 0d)]
    [InlineData(1000d, 400d, true, -0.1d)]
    public void CalculateDesignCurrent_InvalidInput_ReturnsZero(
        double powerW,
        double lineVoltageV,
        bool isThreePhase,
        double cosPhi)
    {
        var connection = PdfConnectionService.CalculateDesignCurrent(powerW, lineVoltageV, isThreePhase, cosPhi);
        var powerBalance = PdfPowerBalanceService.CalculateDesignCurrent(powerW, lineVoltageV, isThreePhase, cosPhi);
        var latex = LatexExportService.CalculateDesignCurrent(powerW, lineVoltageV, isThreePhase, cosPhi);

        Assert.Equal(0d, connection);
        Assert.Equal(0d, powerBalance);
        Assert.Equal(0d, latex);
    }

    private static double CalculateExpected(double powerW, double lineVoltageV, bool isThreePhase, double cosPhi)
    {
        var phaseVoltageV = lineVoltageV / Math.Sqrt(3);
        return isThreePhase
            ? powerW / (lineVoltageV * Math.Sqrt(3) * cosPhi)
            : powerW / (phaseVoltageV * cosPhi);
    }
}
