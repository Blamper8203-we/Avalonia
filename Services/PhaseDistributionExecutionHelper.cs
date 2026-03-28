using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DINBoard.Models;

namespace DINBoard.Services;

/// <summary>
/// Wykonuje animowane mutacje symboli podczas bilansowania faz.
/// Pozwala trzymac opoznienia i zaznaczenia poza kalkulatorem domenowym.
/// </summary>
internal static class PhaseDistributionExecutionHelper
{
    private const int HighlightDelayMs = 200;
    private const int SettleDelayMs = 80;

    public static void ClearSelection(IEnumerable<SymbolItem> symbols)
    {
        foreach (var symbol in symbols)
        {
            symbol.IsSelected = false;
        }
    }

    public static async Task ExecuteGreedyAssignmentAsync(
        IEnumerable<PhaseDistributionCalculator.BalanceUnit> units,
        double[] loads,
        string[] phaseNames,
        Func<double[], int> minIndex)
    {
        foreach (var unit in units)
        {
            var symbols = unit.Symbols;
            await ExecuteAnimatedChangeAsync(symbols, () =>
            {
                int targetIdx = minIndex(loads);
                string assignedPhase = phaseNames[targetIdx];
                loads[targetIdx] += unit.TotalWeight;

                foreach (var symbol in symbols)
                {
                    symbol.Phase = assignedPhase;
                }
            });
        }
    }

    public static Task ExecuteAnimatedChangeAsync(IEnumerable<SymbolItem> symbols, Action applyChange)
        => ExecuteAnimatedChangeAsync(symbols, applyChange, HighlightDelayMs, SettleDelayMs);

    internal static async Task ExecuteAnimatedChangeAsync(
        IEnumerable<SymbolItem> symbols,
        Action applyChange,
        int highlightDelayMs,
        int settleDelayMs)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        ArgumentNullException.ThrowIfNull(applyChange);

        var symbolList = symbols.Distinct().ToList();
        if (symbolList.Count == 0)
        {
            applyChange();
            return;
        }

        foreach (var symbol in symbolList)
        {
            symbol.IsSelected = true;
        }

        if (highlightDelayMs > 0)
        {
            await Task.Delay(highlightDelayMs);
        }

        applyChange();

        foreach (var symbol in symbolList)
        {
            symbol.IsSelected = false;
        }

        if (settleDelayMs > 0)
        {
            await Task.Delay(settleDelayMs);
        }
    }
}
