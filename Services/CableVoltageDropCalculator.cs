using System;

namespace DINBoard.Services;

public interface ICableVoltageDropCalculator
{
    double Calculate(double currentA, double crossSectionMm2, double lengthM, int voltage = 230, bool isThreePhase = false);
}

public sealed class CableVoltageDropCalculator : ICableVoltageDropCalculator
{
    // Copper resistivity in Ohm*mm2/m.
    private const double CopperResistivity = 0.0175;

    public double Calculate(double currentA, double crossSectionMm2, double lengthM, int voltage = 230, bool isThreePhase = false)
    {
        if (crossSectionMm2 <= 0 || voltage <= 0)
        {
            return 0;
        }

        // Voltage drop per PN-HD 60364-5-52:
        // Single-phase: dU = 2 * I * L * rho / S (there and back)
        // Three-phase:  dU = sqrt(3) * I * L * rho / S
        var factor = isThreePhase ? Math.Sqrt(3) : 2.0;
        var resistanceOhm = factor * lengthM * CopperResistivity / crossSectionMm2;
        var voltageDropV = currentA * resistanceOhm;
        var voltageDropPercent = (voltageDropV / voltage) * 100.0;

        return voltageDropPercent;
    }
}
