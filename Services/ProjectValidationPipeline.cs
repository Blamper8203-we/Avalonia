using System;
using System.Collections.Generic;
using System.Linq;

namespace DINBoard.Services;

public sealed class ProjectValidationPipeline : IProjectValidationPipeline
{
    private readonly IReadOnlyList<IProjectValidationRule> _rules;

    public ProjectValidationPipeline(IEnumerable<IProjectValidationRule> rules)
    {
        _rules = (rules ?? Enumerable.Empty<IProjectValidationRule>())
            .OrderBy(r => r.Order)
            .ToList();
    }

    public void Apply(ProjectValidationContext context, ValidationResult result)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        foreach (var rule in _rules)
        {
            rule.Apply(context, result);
        }
    }
}
