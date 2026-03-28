using System;

namespace DINBoard.Services;

public sealed class ProtectionMismatchProjectValidationRule : IProjectValidationRule
{
    private readonly IProtectionMismatchValidationRule _rule;

    public ProtectionMismatchProjectValidationRule(IProtectionMismatchValidationRule rule)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
    }

    public int Order => 30;

    public void Apply(ProjectValidationContext context, ValidationResult result)
    {
        var errors = _rule.Evaluate(context.Symbols);
        foreach (var error in errors)
        {
            result.Errors.Add(error);
        }
    }
}
