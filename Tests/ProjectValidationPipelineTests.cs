using System;
using System.Collections.Generic;
using DINBoard.Models;
using DINBoard.Services;
using Xunit;

namespace Avalonia.Tests;

public class ProjectValidationPipelineTests
{
    [Fact]
    public void Apply_ShouldExecuteRulesInOrderAndAggregateMessages()
    {
        var executionOrder = new List<int>();
        var rules = new IProjectValidationRule[]
        {
            new RecordingRule(30, executionOrder, (_, result) => result.Info.Add(new ValidationMessage { Code = "INFO_30" })),
            new RecordingRule(10, executionOrder, (_, result) => result.Errors.Add(new ValidationMessage { Code = "ERROR_10" })),
            new RecordingRule(20, executionOrder, (_, result) => result.Warnings.Add(new ValidationMessage { Code = "WARN_20" }))
        };

        var pipeline = new ProjectValidationPipeline(rules);
        var context = new ProjectValidationContext(new Project(), new List<SymbolItem>(), 230, new PhaseLoadResult());
        var result = new ValidationResult();

        pipeline.Apply(context, result);

        Assert.Equal(new[] { 10, 20, 30 }, executionOrder);
        Assert.Contains(result.Errors, e => e.Code == "ERROR_10");
        Assert.Contains(result.Warnings, w => w.Code == "WARN_20");
        Assert.Contains(result.Info, i => i.Code == "INFO_30");
    }

    [Fact]
    public void Apply_WhenContextIsNull_ShouldThrowArgumentNullException()
    {
        var pipeline = new ProjectValidationPipeline(Array.Empty<IProjectValidationRule>());
        var result = new ValidationResult();

        Assert.Throws<ArgumentNullException>(() => pipeline.Apply(null!, result));
    }

    [Fact]
    public void Apply_WhenResultIsNull_ShouldThrowArgumentNullException()
    {
        var pipeline = new ProjectValidationPipeline(Array.Empty<IProjectValidationRule>());
        var context = new ProjectValidationContext(new Project(), new List<SymbolItem>(), 230, new PhaseLoadResult());

        Assert.Throws<ArgumentNullException>(() => pipeline.Apply(context, null!));
    }

    private sealed class RecordingRule : IProjectValidationRule
    {
        private readonly List<int> _executionOrder;
        private readonly Action<ProjectValidationContext, ValidationResult> _onApply;

        public RecordingRule(int order, List<int> executionOrder, Action<ProjectValidationContext, ValidationResult> onApply)
        {
            Order = order;
            _executionOrder = executionOrder;
            _onApply = onApply;
        }

        public int Order { get; }

        public void Apply(ProjectValidationContext context, ValidationResult result)
        {
            _executionOrder.Add(Order);
            _onApply(context, result);
        }
    }
}
