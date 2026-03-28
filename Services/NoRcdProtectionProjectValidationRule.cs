using System;

namespace DINBoard.Services;

public sealed class NoRcdProtectionProjectValidationRule : IProjectValidationRule
{
    private readonly INoRcdProtectionWarningRule _rule;

    public NoRcdProtectionProjectValidationRule(INoRcdProtectionWarningRule rule)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
    }

    public int Order => 40;

    public void Apply(ProjectValidationContext context, ValidationResult result)
    {
        var warning = _rule.Evaluate(context.Symbols);
        if (warning != null)
        {
            result.Warnings.Add(warning);
        }
    }
}
