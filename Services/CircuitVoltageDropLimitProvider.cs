namespace DINBoard.Services;

public interface ICircuitVoltageDropLimitProvider
{
    double GetMaxVoltageDrop(string? circuitType);
}

public sealed class CircuitVoltageDropLimitProvider : ICircuitVoltageDropLimitProvider
{
    public double GetMaxVoltageDrop(string? circuitType)
    {
        return circuitType?.ToLowerInvariant() switch
        {
            "oświetlenie" => 3.0,
            _ => 5.0
        };
    }
}
