using System;

namespace DINBoard.Services;

public sealed class CableSafetyProjectValidationRule : IProjectValidationRule
{
    private readonly ICableSafetyValidationRule _rule;

    public CableSafetyProjectValidationRule(ICableSafetyValidationRule rule)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
    }

    public int Order => 20;

    public void Apply(ProjectValidationContext context, ValidationResult result)
    {
        var cableResult = _rule.Evaluate(context.Symbols, context.PhaseVoltage);
        foreach (var error in cableResult.Errors)
        {
            result.Errors.Add(error);
        }

        foreach (var warning in cableResult.Warnings)
        {
            result.Warnings.Add(warning);
        }
    }
}
