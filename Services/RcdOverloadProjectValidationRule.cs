using System;

namespace DINBoard.Services;

public sealed class RcdOverloadProjectValidationRule : IProjectValidationRule
{
    private readonly IRcdOverloadWarningRule _rule;

    public RcdOverloadProjectValidationRule(IRcdOverloadWarningRule rule)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
    }

    public int Order => 70;

    public void Apply(ProjectValidationContext context, ValidationResult result)
    {
        var warnings = _rule.Evaluate(context.Symbols, context.MainProtection);
        foreach (var warning in warnings)
        {
            result.Warnings.Add(warning);
        }
    }
}
