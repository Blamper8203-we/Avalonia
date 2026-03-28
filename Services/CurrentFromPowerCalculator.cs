namespace DINBoard.Services;

public interface ICurrentFromPowerCalculator
{
    double Calculate(double powerW, string? phase, int voltage = 230);
}

public sealed class CurrentFromPowerCalculator : ICurrentFromPowerCalculator
{
    public double Calculate(double powerW, string? phase, int voltage = 230)
    {
        if (powerW <= 0)
        {
            return 0;
        }

        // Assumed power factor.
        const double cosPhi = 0.9;

        return phase?.ToUpperInvariant() switch
        {
            // For 3-phase (230V phase-to-neutral, 400V phase-to-phase):
            // P = sqrt(3) * 400 * I * cosPhi = 3 * 230 * I * cosPhi
            "L1+L2+L3" or "3F" => powerW / (3.0 * voltage * cosPhi),
            // 2-phase (for example induction cooktop): split power across 2 phases.
            "L1+L2" or "L1+L3" or "L2+L3" => powerW / (2.0 * voltage * cosPhi),
            _ => powerW / (voltage * cosPhi)
        };
    }
}
