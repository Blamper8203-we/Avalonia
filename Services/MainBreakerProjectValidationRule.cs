using System;

namespace DINBoard.Services;

public sealed class MainBreakerProjectValidationRule : IProjectValidationRule
{
    private readonly IMainBreakerWarningRule _rule;

    public MainBreakerProjectValidationRule(IMainBreakerWarningRule rule)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
    }

    public int Order => 60;

    public void Apply(ProjectValidationContext context, ValidationResult result)
    {
        var warning = _rule.Evaluate(context.Symbols);
        if (warning != null)
        {
            result.Warnings.Add(warning);
        }
    }
}
