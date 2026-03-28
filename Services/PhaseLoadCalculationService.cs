using System.Collections.Generic;
using DINBoard.Models;

namespace DINBoard.Services;

public interface IPhaseLoadCalculationService
{
    PhaseLoadResult Calculate(IEnumerable<SymbolItem> symbols, int voltage = 230);
}

public sealed class PhaseLoadCalculationService : IPhaseLoadCalculationService
{
    public PhaseLoadResult Calculate(IEnumerable<SymbolItem> symbols, int voltage = 230)
    {
        var dist = PhaseDistributionCalculator.CalculateTotalDistribution(symbols);

        var result = new PhaseLoadResult
        {
            L1PowerW = dist.L1PowerW,
            L2PowerW = dist.L2PowerW,
            L3PowerW = dist.L3PowerW
        };

        result.L1CurrentA = PhaseDistributionCalculator.CalculateCurrent(result.L1PowerW, "L1", voltage);
        result.L2CurrentA = PhaseDistributionCalculator.CalculateCurrent(result.L2PowerW, "L2", voltage);
        result.L3CurrentA = PhaseDistributionCalculator.CalculateCurrent(result.L3PowerW, "L3", voltage);

        result.ImbalancePercent = PhaseDistributionCalculator.CalculateImbalancePercent(
            result.L1CurrentA, result.L2CurrentA, result.L3CurrentA);

        return result;
    }
}
