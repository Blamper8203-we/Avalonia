using System;

namespace DINBoard.Services;

public sealed class PhaseImbalanceProjectValidationRule : IProjectValidationRule
{
    private readonly IPhaseImbalanceWarningRule _rule;

    public PhaseImbalanceProjectValidationRule(IPhaseImbalanceWarningRule rule)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
    }

    public int Order => 10;

    public void Apply(ProjectValidationContext context, ValidationResult result)
    {
        var warning = _rule.Evaluate(context.PhaseLoads);
        if (warning != null)
        {
            result.Warnings.Add(warning);
        }
    }
}
