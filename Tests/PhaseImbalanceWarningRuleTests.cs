using DINBoard.Services;
using Xunit;

namespace Avalonia.Tests;

public class PhaseImbalanceWarningRuleTests
{
    private readonly PhaseImbalanceWarningRule _rule = new();

    [Fact]
    public void Evaluate_WhenImbalanceExceedsThreshold_ShouldReturnWarning()
    {
        var phaseLoads = new PhaseLoadResult
        {
            L1CurrentA = 20,
            L2CurrentA = 5,
            L3CurrentA = 5,
            ImbalancePercent = 60
        };

        var warning = _rule.Evaluate(phaseLoads);

        Assert.NotNull(warning);
        Assert.Equal("PHASE_IMBALANCE", warning.Code);
        Assert.Equal(ValidationSeverity.Warning, warning.Severity);
    }

    [Fact]
    public void Evaluate_WhenImbalanceWithinThreshold_ShouldReturnNull()
    {
        var phaseLoads = new PhaseLoadResult
        {
            L1CurrentA = 10,
            L2CurrentA = 10,
            L3CurrentA = 10,
            ImbalancePercent = 0
        };

        var warning = _rule.Evaluate(phaseLoads);

        Assert.Null(warning);
    }
}
