namespace DINBoard.Services;

public interface IPhaseImbalanceWarningRule
{
    ValidationMessage? Evaluate(PhaseLoadResult? phaseLoads);
}

public sealed class PhaseImbalanceWarningRule : IPhaseImbalanceWarningRule
{
    private const double MaxPhaseImbalancePercent = 15.0;

    public ValidationMessage? Evaluate(PhaseLoadResult? phaseLoads)
    {
        if (phaseLoads == null || phaseLoads.ImbalancePercent <= MaxPhaseImbalancePercent)
        {
            return null;
        }

        return new ValidationMessage
        {
            Code = "PHASE_IMBALANCE",
            Message = $"Asymetria obciążenia faz: {phaseLoads.ImbalancePercent:F1}% (max {MaxPhaseImbalancePercent}%)",
            Severity = ValidationSeverity.Warning,
            Details = $"L1: {phaseLoads.L1CurrentA:F1}A, L2: {phaseLoads.L2CurrentA:F1}A, L3: {phaseLoads.L3CurrentA:F1}A"
        };
    }
}
