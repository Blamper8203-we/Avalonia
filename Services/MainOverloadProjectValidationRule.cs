using System;

namespace DINBoard.Services;

public sealed class MainOverloadProjectValidationRule : IProjectValidationRule
{
    private readonly IMainOverloadValidationRule _rule;

    public MainOverloadProjectValidationRule(IMainOverloadValidationRule rule)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
    }

    public int Order => 50;

    public void Apply(ProjectValidationContext context, ValidationResult result)
    {
        var error = _rule.Evaluate(context.Project, context.Symbols);
        if (error != null)
        {
            result.Errors.Add(error);
        }
    }
}
