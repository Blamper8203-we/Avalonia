using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DINBoard.Models;

namespace DINBoard.Services;

/// <summary>
/// Tryb obliczeń bilansowania: po mocy (W) lub po prądzie (A).
/// </summary>
public enum BalanceMode
{
    /// <summary>Bilansowanie na podstawie mocy [W]</summary>
    Power,
    /// <summary>Bilansowanie na podstawie prądu [A] (z uwzględnieniem cosφ)</summary>
    Current
}

/// <summary>
/// Zakres bilansowania: wszystkie 1F lub tylko niezablokowane.
/// </summary>
public enum BalanceScope
{
    /// <summary>Bilansuj wszystkie obwody jednofazowe</summary>
    AllSinglePhase,
    /// <summary>Bilansuj tylko obwody bez flagi IsPhaseLocked</summary>
    OnlyUnlocked
}

/// <summary>
/// Centralna logika rozkładu mocy na fazy L1/L2/L3.
/// Eliminuje duplikację między PowerBalanceViewModel i ElectricalValidationService.
/// </summary>
public static class PhaseDistributionCalculator
{
    /// <summary>
    /// Wynik rozkładu mocy na fazy.
    /// </summary>
    public record PhaseDistribution(
        double L1PowerW,
        double L2PowerW,
        double L3PowerW);

    /// <summary>
    /// Rozdziela moc symbolu na fazy na podstawie przypisanej fazy.
    /// </summary>
    public static PhaseDistribution DistributePower(double powerW, string? phase)
    {
        if (powerW <= 0)
            return new PhaseDistribution(0, 0, 0);

        return phase?.ToUpperInvariant() switch
        {
            "L2" => new PhaseDistribution(0, powerW, 0),
            "L3" => new PhaseDistribution(0, 0, powerW),
            "L1+L2+L3" or "3F" => new PhaseDistribution(powerW / 3.0, powerW / 3.0, powerW / 3.0),
            // 2-fazowe (np. indukcja) → moc dzielona po połowie
            "L1+L2" => new PhaseDistribution(powerW / 2.0, powerW / 2.0, 0),
            "L1+L3" => new PhaseDistribution(powerW / 2.0, 0, powerW / 2.0),
            "L2+L3" => new PhaseDistribution(0, powerW / 2.0, powerW / 2.0),
            // "L1" i domyślnie
            _ => new PhaseDistribution(powerW, 0, 0),
        };
    }

    /// <summary>
    /// Oblicza sumaryczny rozkład mocy na fazy dla kolekcji symboli.
    /// </summary>
    public static PhaseDistribution CalculateTotalDistribution(IEnumerable<SymbolItem> symbols)
    {
        double l1 = 0, l2 = 0, l3 = 0;

        foreach (var symbol in symbols)
        {
            var dist = DistributePower(symbol.PowerW, symbol.Phase);
            l1 += dist.L1PowerW;
            l2 += dist.L2PowerW;
            l3 += dist.L3PowerW;
        }

        return new PhaseDistribution(l1, l2, l3);
    }

    /// <summary>
    /// Oblicza prąd z mocy dla danej fazy.
    /// </summary>
    public static double CalculateCurrent(double powerW, string? phase, int voltage = 230)
    {
        if (powerW <= 0) return 0;

        const double cosPhi = 0.9;

        return phase?.ToUpperInvariant() switch
        {
            "L1+L2+L3" or "3F" => powerW / (3.0 * voltage * cosPhi),
            // 2-fazowe (indukcja): moc podzielona na 2 fazy → prąd na fazę = P / (2 × U × cosφ)
            "L1+L2" or "L1+L3" or "L2+L3" => powerW / (2.0 * voltage * cosPhi),
            _ => powerW / (voltage * cosPhi)
        };
    }

    /// <summary>
    /// Oblicza asymetrię obciążenia faz w procentach.
    /// </summary>
    public static double CalculateImbalancePercent(double l1, double l2, double l3)
    {
        var avg = (l1 + l2 + l3) / 3.0;
        if (avg <= 0) return 0;

        var maxDev = Math.Max(
            Math.Abs(l1 - avg),
            Math.Max(Math.Abs(l2 - avg), Math.Abs(l3 - avg)));

        return (maxDev / avg) * 100.0;
    }

    // ================================================================
    // Automatyczne bilansowanie faz
    // ================================================================

    /// <summary>
    /// Jednostka bilansowania — może być pojedynczy MCB lub grupa RCD 2P + MCBs.
    /// Cała jednostka dostaje jedną fazę.
    /// </summary>
    internal sealed class BalanceUnit
    {
        public List<SymbolItem> Symbols { get; } = new();
        public double TotalWeight { get; set; }

        /// <summary>Nazwa (do debugowania)</summary>
        public string Label => Symbols.Count == 1
            ? Symbols[0].Label ?? Symbols[0].Id
            : $"RCD-grupa ({Symbols.Count} szt.)";
    }

    internal sealed record PhaseIndicatorAssignment(SymbolItem Symbol, string Phase);

    internal sealed class BalancePlan
    {
        public Dictionary<string, string> Snapshot { get; } = new();
        public List<PhaseIndicatorAssignment> PhaseIndicatorAssignments { get; } = new();
        public List<BalanceUnit> Units { get; } = new();
        public List<SymbolItem> WorkingSymbols { get; } = new();
        public double[] Loads { get; } = new double[3];
        public int RcdCount { get; set; }
    }

    internal sealed record RefinementMoveCandidate(
        SymbolItem Mcb,
        BalanceUnit SourceUnit,
        BalanceUnit? TargetRcdUnit,
        double Weight,
        bool IsStandalone);

    internal sealed record RefinementSwapCandidate(
        BalanceUnit HeavyUnit,
        BalanceUnit LightUnit,
        double ResultingImbalance);

    private static readonly string[] PhaseNames = { "L1", "L2", "L3" };
    private const double TargetImbalancePercent = 3.0;
    private const double MinRefinementDiff = 0.01;
    private const double MinSwapImprovementPercent = 0.5;
    private const int MaxMoveRefinementSteps = 50;
    private const int MaxSwapRefinementSteps = 30;

    private static bool IsSinglePhase(SymbolItem s)
    {
        var p = s.Phase?.ToUpperInvariant();
        if (p == "L1" || p == "L2" || p == "L3") return true;
        if (string.IsNullOrWhiteSpace(p) || p == "PENDING")
        {
            var poleCount = DetectPoleCount(s);
            return poleCount == ModulePoleCount.P1; // Default to single-phase if 1 pole
        }
        return false;
    }

    private static bool IsRcdSinglePhase(SymbolItem rcd)
    {
        var poleCount = DetectPoleCount(rcd);
        if (poleCount == ModulePoleCount.P3 || poleCount == ModulePoleCount.P4) return false;
        
        // Fallback for custom names if aspect ratio/path failed
        var combined = (rcd.Type + " " + (rcd.Label ?? "")).ToUpperInvariant();
        if (combined.Contains("4P") || combined.Contains("4-P") || combined.Contains("3P") || combined.Contains("3-P"))
            return false;

        return true;
    }

    private static ModulePoleCount DetectPoleCount(SymbolItem symbol)
    {
        var value = $"{symbol.Type} {symbol.Label} {symbol.VisualPath}".ToUpperInvariant();
        if (value.Contains("4P") || value.Contains("4-P")) return ModulePoleCount.P4;
        if (value.Contains("3P") || value.Contains("3-P")) return ModulePoleCount.P3;
        if (value.Contains("2P") || value.Contains("2-P")) return ModulePoleCount.P2;
        if (value.Contains("1P") || value.Contains("1-P")) return ModulePoleCount.P1;

        // Fallback oparty o proporcje modułu (jak w ModuleTypeService.GetPoleCount):
        // 1P ~ 18/90, 2P ~ 36/90, 3P ~ 54/90, 4P ~ 72/90.
        if (symbol.Height > 0)
        {
            var ratio = symbol.Width / symbol.Height;
            if (ratio < 0.30) return ModulePoleCount.P1;
            if (ratio < 0.55) return ModulePoleCount.P2;
            if (ratio < 0.75) return ModulePoleCount.P3;
            return ModulePoleCount.P4;
        }

        return symbol.Phase?.ToUpperInvariant() switch
        {
            "L1+L2+L3" or "3F" => ModulePoleCount.P3,
            "L1+L2" or "L1+L3" or "L2+L3" => ModulePoleCount.P2,
            "L1" or "L2" or "L3" => ModulePoleCount.P1,
            _ => ModulePoleCount.Unknown
        };
    }

    private static double GetWeight(double powerW, BalanceMode mode, int voltage = 230)
    {
        if (mode == BalanceMode.Current)
            return CalculateCurrent(powerW, "L1", voltage);
        return powerW;
    }

    private static double GetSymbolWeight(SymbolItem s, BalanceMode mode, int voltage = 230)
        => GetWeight(s.PowerW, mode, voltage);

    internal static BalancePlan CreateBalancePlan(
        IEnumerable<SymbolItem> allSymbols,
        BalanceMode mode = BalanceMode.Current,
        BalanceScope scope = BalanceScope.OnlyUnlocked,
        int voltage = 230)
    {
        var plan = new BalancePlan();
        var sourceSymbols = allSymbols.ToList();

        foreach (var symbol in sourceSymbols)
        {
            plan.Snapshot[symbol.Id] = symbol.Phase;
        }

        plan.WorkingSymbols.AddRange(sourceSymbols);

        var phaseIndicators = sourceSymbols.Where(IsPhaseIndicator).ToList();
        for (int i = 0; i < phaseIndicators.Count; i++)
        {
            plan.PhaseIndicatorAssignments.Add(
                new PhaseIndicatorAssignment(phaseIndicators[i], PhaseNames[i % PhaseNames.Length]));
        }

        plan.WorkingSymbols.RemoveAll(IsPhaseIndicator);

        var symbolList = plan.WorkingSymbols;
        var rcdMap = new Dictionary<string, SymbolItem>();
        var mcbsByRcd = new Dictionary<string, List<SymbolItem>>();

        foreach (var symbol in symbolList)
        {
            if (IsGroupHeadSymbol(symbol))
            {
                rcdMap[symbol.Id] = symbol;
            }
        }

        plan.RcdCount = rcdMap.Count;

        foreach (var symbol in symbolList)
        {
            if (!string.IsNullOrEmpty(symbol.RcdSymbolId) && rcdMap.ContainsKey(symbol.RcdSymbolId))
            {
                if (!mcbsByRcd.TryGetValue(symbol.RcdSymbolId, out var list))
                {
                    list = new List<SymbolItem>();
                    mcbsByRcd[symbol.RcdSymbolId] = list;
                }

                list.Add(symbol);
            }
        }

        var processedSymbolIds = new HashSet<string>();

        // MCB z PowerW>0 -> waga z pradu/mocy; MCB z PowerW=0 -> waga 0.
        // Grupy z zerowa moca dostaja mikroskopijna wage, zeby nadal trafic na faze.
        const double ZeroPowerUnitWeight = 0.001;

        foreach (var kvp in rcdMap)
        {
            var rcd = kvp.Value;
            if (!IsRcdSinglePhase(rcd))
            {
                continue;
            }

            var mcbs = mcbsByRcd.TryGetValue(rcd.Id, out var list) ? list : new List<SymbolItem>();
            bool anyLocked = rcd.IsPhaseLocked || mcbs.Any(m => m.IsPhaseLocked);
            if (scope == BalanceScope.OnlyUnlocked && anyLocked)
            {
                double weight = mcbs.Sum(m => GetSymbolWeight(m, mode, voltage));
                var phase = rcd.Phase?.ToUpperInvariant();
                if (phase == "L2")
                {
                    plan.Loads[1] += weight;
                }
                else if (phase == "L3")
                {
                    plan.Loads[2] += weight;
                }
                else
                {
                    plan.Loads[0] += weight;
                }

                processedSymbolIds.Add(rcd.Id);
                foreach (var mcb in mcbs)
                {
                    processedSymbolIds.Add(mcb.Id);
                }

                continue;
            }

            var unit = new BalanceUnit();
            unit.Symbols.Add(rcd);
            unit.Symbols.AddRange(mcbs);
            double powerWeight = mcbs.Sum(m => GetSymbolWeight(m, mode, voltage));
            unit.TotalWeight = powerWeight > 0 ? powerWeight : ZeroPowerUnitWeight * Math.Max(mcbs.Count, 1);

            plan.Units.Add(unit);
            processedSymbolIds.Add(rcd.Id);
            foreach (var mcb in mcbs)
            {
                processedSymbolIds.Add(mcb.Id);
            }
        }

        foreach (var kvp in rcdMap)
        {
            var rcd = kvp.Value;
            if (IsRcdSinglePhase(rcd))
            {
                continue;
            }

            processedSymbolIds.Add(rcd.Id);

            var mcbs = mcbsByRcd.TryGetValue(rcd.Id, out var list) ? list : new List<SymbolItem>();
            foreach (var mcb in mcbs)
            {
                processedSymbolIds.Add(mcb.Id);

                if (!IsSinglePhase(mcb))
                {
                    continue;
                }

                double weight = GetSymbolWeight(mcb, mode, voltage);
                if (scope == BalanceScope.OnlyUnlocked && mcb.IsPhaseLocked)
                {
                    var phase = mcb.Phase?.ToUpperInvariant();
                    if (phase == "L2")
                    {
                        plan.Loads[1] += weight;
                    }
                    else if (phase == "L3")
                    {
                        plan.Loads[2] += weight;
                    }
                    else
                    {
                        plan.Loads[0] += weight;
                    }

                    continue;
                }

                var unit = new BalanceUnit();
                unit.Symbols.Add(mcb);
                unit.TotalWeight = weight > 0 ? weight : ZeroPowerUnitWeight;
                plan.Units.Add(unit);
            }
        }

        foreach (var symbol in symbolList)
        {
            if (processedSymbolIds.Contains(symbol.Id))
            {
                continue;
            }

            if (!IsSinglePhase(symbol))
            {
                if (symbol.PowerW > 0)
                {
                    var dist = DistributePower(symbol.PowerW, symbol.Phase);
                    plan.Loads[0] += GetWeight(dist.L1PowerW, mode, voltage);
                    plan.Loads[1] += GetWeight(dist.L2PowerW, mode, voltage);
                    plan.Loads[2] += GetWeight(dist.L3PowerW, mode, voltage);
                }

                continue;
            }

            double weight = GetSymbolWeight(symbol, mode, voltage);
            if (scope == BalanceScope.OnlyUnlocked && symbol.IsPhaseLocked)
            {
                var phase = symbol.Phase?.ToUpperInvariant();
                if (phase == "L2")
                {
                    plan.Loads[1] += weight;
                }
                else if (phase == "L3")
                {
                    plan.Loads[2] += weight;
                }
                else
                {
                    plan.Loads[0] += weight;
                }

                continue;
            }

            var unit = new BalanceUnit();
            unit.Symbols.Add(symbol);
            unit.TotalWeight = weight > 0 ? weight : ZeroPowerUnitWeight;
            plan.Units.Add(unit);
        }

        plan.Units.Sort((a, b) => b.TotalWeight.CompareTo(a.TotalWeight));
        return plan;
    }

    private static void ApplyPhaseIndicatorAssignments(BalancePlan plan)
    {
        foreach (var assignment in plan.PhaseIndicatorAssignments)
        {
            assignment.Symbol.Phase = assignment.Phase;
        }
    }

    private static void LogBalancePlan(BalancePlan plan, BalanceMode mode, BalanceScope scope)
    {
        AppLog.Debug($"BalancePhases: RCDs={plan.RcdCount}, units={plan.Units.Count}, base L1={plan.Loads[0]:F0} L2={plan.Loads[1]:F0} L3={plan.Loads[2]:F0}, scope={scope}, mode={mode}");
        foreach (var unit in plan.Units)
        {
            double unitPowerW = unit.Symbols.Sum(symbol => symbol.PowerW);
            AppLog.Debug($"  Unit: {unit.Label}, weight={unit.TotalWeight:F2}, powerW={unitPowerW:F0}, symbols={unit.Symbols.Count}");
        }
    }

    private static BalanceUnit? FindTargetRcdUnit(IEnumerable<BalanceUnit> units, string lightPhase)
        => units
            .Where(unit => unit.Symbols.Count > 0
                && string.Equals(unit.Symbols[0].Phase, lightPhase, StringComparison.OrdinalIgnoreCase)
                && unit.Symbols.Any(IsGroupHeadSymbol))
            .OrderBy(unit => unit.TotalWeight)
            .FirstOrDefault();

    private static void ConsiderMoveCandidate(
        IEnumerable<SymbolItem> movableSymbols,
        BalanceUnit sourceUnit,
        BalanceUnit? targetRcdUnit,
        bool isStandalone,
        double diff,
        double idealWeight,
        BalanceMode mode,
        int voltage,
        ref RefinementMoveCandidate? bestCandidate,
        ref double bestFit)
    {
        foreach (var mcb in movableSymbols)
        {
            double weight = GetSymbolWeight(mcb, mode, voltage);
            if (weight <= 0 || weight > diff)
            {
                continue;
            }

            double fit = Math.Abs(weight - idealWeight);
            if (fit < bestFit)
            {
                bestFit = fit;
                bestCandidate = new RefinementMoveCandidate(mcb, sourceUnit, targetRcdUnit, weight, isStandalone);
            }
        }
    }

    internal static RefinementMoveCandidate? FindBestMoveCandidate(
        IEnumerable<BalanceUnit> units,
        string heavyPhase,
        string lightPhase,
        double diff,
        double idealWeight,
        BalanceMode mode,
        int voltage)
    {
        var targetRcdUnit = FindTargetRcdUnit(units, lightPhase);
        RefinementMoveCandidate? bestCandidate = null;
        double bestFit = double.MaxValue;

        foreach (var unit in units)
        {
            if (unit.Symbols.Count == 0)
            {
                continue;
            }

            if (!string.Equals(unit.Symbols[0].Phase, heavyPhase, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool hasRcd = unit.Symbols.Any(IsGroupHeadSymbol);
            if (hasRcd)
            {
                if (targetRcdUnit == null)
                {
                    continue;
                }

                var movable = unit.Symbols
                    .Where(symbol => !IsGroupHeadSymbol(symbol) && IsSinglePhase(symbol) && symbol.PowerW > 0)
                    .ToList();
                if (movable.Count <= 1)
                {
                    continue;
                }

                ConsiderMoveCandidate(
                    movable,
                    unit,
                    targetRcdUnit,
                    false,
                    diff,
                    idealWeight,
                    mode,
                    voltage,
                    ref bestCandidate,
                    ref bestFit);
            }
            else
            {
                var movable = unit.Symbols
                    .Where(symbol => IsSinglePhase(symbol) && symbol.PowerW > 0)
                    .ToList();

                ConsiderMoveCandidate(
                    movable,
                    unit,
                    null,
                    true,
                    diff,
                    idealWeight,
                    mode,
                    voltage,
                    ref bestCandidate,
                    ref bestFit);
            }
        }

        return bestCandidate;
    }

    private static void ApplyMoveCandidate(
        RefinementMoveCandidate candidate,
        string lightPhase,
        int heavyIdx,
        int lightIdx,
        double[] loads)
    {
        if (candidate.IsStandalone)
        {
            candidate.Mcb.Phase = lightPhase;
            candidate.SourceUnit.TotalWeight -= candidate.Weight;
        }
        else if (candidate.TargetRcdUnit != null)
        {
            var targetRcd = candidate.TargetRcdUnit.Symbols.First(symbol => IsGroupHeadSymbol(symbol));

            candidate.SourceUnit.Symbols.Remove(candidate.Mcb);
            candidate.SourceUnit.TotalWeight -= candidate.Weight;

            candidate.TargetRcdUnit.Symbols.Add(candidate.Mcb);
            candidate.TargetRcdUnit.TotalWeight += candidate.Weight;

            candidate.Mcb.Phase = targetRcd.Phase;
            candidate.Mcb.RcdSymbolId = targetRcd.Id;
            candidate.Mcb.Group = targetRcd.Group;

            CompactGroup(candidate.SourceUnit.Symbols);
            CompactGroup(candidate.TargetRcdUnit.Symbols);
        }

        loads[heavyIdx] -= candidate.Weight;
        loads[lightIdx] += candidate.Weight;
    }

    private static List<BalanceUnit> GetStandaloneUnitsForPhase(IEnumerable<BalanceUnit> units, string phase)
        => units
            .Where(unit => unit.Symbols.Count > 0
                && !unit.Symbols.Any(IsGroupHeadSymbol)
                && string.Equals(unit.Symbols[0].Phase, phase, StringComparison.OrdinalIgnoreCase)
                && unit.TotalWeight > 0)
            .ToList();

    internal static RefinementSwapCandidate? FindBestSwapCandidate(
        IEnumerable<BalanceUnit> units,
        double[] loads,
        string heavyPhase,
        string lightPhase,
        int heavyIdx,
        int lightIdx,
        double currentImbalance)
    {
        var heavyStandalone = GetStandaloneUnitsForPhase(units, heavyPhase);
        var lightStandalone = GetStandaloneUnitsForPhase(units, lightPhase);
        RefinementSwapCandidate? bestCandidate = null;
        double bestSwapImbalance = currentImbalance;

        foreach (var heavyUnit in heavyStandalone)
        {
            foreach (var lightUnit in lightStandalone)
            {
                double[] testLoads = { loads[0], loads[1], loads[2] };
                testLoads[heavyIdx] -= heavyUnit.TotalWeight;
                testLoads[heavyIdx] += lightUnit.TotalWeight;
                testLoads[lightIdx] -= lightUnit.TotalWeight;
                testLoads[lightIdx] += heavyUnit.TotalWeight;

                double testImbalance = CalculateImbalancePercent(testLoads[0], testLoads[1], testLoads[2]);
                if (testImbalance < bestSwapImbalance - MinSwapImprovementPercent)
                {
                    bestSwapImbalance = testImbalance;
                    bestCandidate = new RefinementSwapCandidate(heavyUnit, lightUnit, testImbalance);
                }
            }
        }

        return bestCandidate;
    }

    private static void ApplySwapCandidate(
        RefinementSwapCandidate candidate,
        string heavyPhase,
        string lightPhase,
        int heavyIdx,
        int lightIdx,
        double[] loads)
    {
        loads[heavyIdx] -= candidate.HeavyUnit.TotalWeight;
        loads[heavyIdx] += candidate.LightUnit.TotalWeight;
        loads[lightIdx] -= candidate.LightUnit.TotalWeight;
        loads[lightIdx] += candidate.HeavyUnit.TotalWeight;

        foreach (var symbol in candidate.HeavyUnit.Symbols)
        {
            symbol.Phase = lightPhase;
        }

        foreach (var symbol in candidate.LightUnit.Symbols)
        {
            symbol.Phase = heavyPhase;
        }
    }

    private static bool NeedsRefinement(double[] loads)
        => CalculateImbalancePercent(loads[0], loads[1], loads[2]) > TargetImbalancePercent;

    private static async Task<bool> TryApplyMoveRefinementStepAsync(
        IEnumerable<BalanceUnit> units,
        double[] loads,
        string[] phaseNames,
        BalanceMode mode,
        int voltage,
        int step)
    {
        int heavyIdx = MaxIndex(loads);
        int lightIdx = MinIndex(loads);
        if (heavyIdx == lightIdx)
        {
            return false;
        }

        double diff = loads[heavyIdx] - loads[lightIdx];
        if (diff < MinRefinementDiff)
        {
            return false;
        }

        double idealWeight = diff / 2.0;
        var moveCandidate = FindBestMoveCandidate(
            units,
            phaseNames[heavyIdx],
            phaseNames[lightIdx],
            diff,
            idealWeight,
            mode,
            voltage);
        if (moveCandidate == null)
        {
            return false;
        }

        await PhaseDistributionExecutionHelper.ExecuteAnimatedChangeAsync(
            new[] { moveCandidate.Mcb },
            () => ApplyMoveCandidate(moveCandidate, phaseNames[lightIdx], heavyIdx, lightIdx, loads));

        AppLog.Debug($"BalancePhases refinement #{step}: {(moveCandidate.IsStandalone ? "standalone" : "RCD→RCD")} {moveCandidate.Mcb.Label ?? moveCandidate.Mcb.Id} ({moveCandidate.Mcb.PowerW}W, w={moveCandidate.Weight:F2}) → {phaseNames[lightIdx]}, L1={loads[0]:F2} L2={loads[1]:F2} L3={loads[2]:F2}");
        return true;
    }

    private static async Task<bool> TryApplySwapRefinementStepAsync(
        IEnumerable<BalanceUnit> units,
        double[] loads,
        string[] phaseNames,
        int step)
    {
        int heavyIdx = MaxIndex(loads);
        int lightIdx = MinIndex(loads);
        if (heavyIdx == lightIdx)
        {
            return false;
        }

        double currentImbalance = CalculateImbalancePercent(loads[0], loads[1], loads[2]);
        var swapCandidate = FindBestSwapCandidate(
            units,
            loads,
            phaseNames[heavyIdx],
            phaseNames[lightIdx],
            heavyIdx,
            lightIdx,
            currentImbalance);
        if (swapCandidate == null)
        {
            return false;
        }

        var swapSymbols = swapCandidate.HeavyUnit.Symbols.Concat(swapCandidate.LightUnit.Symbols).ToList();
        await PhaseDistributionExecutionHelper.ExecuteAnimatedChangeAsync(
            swapSymbols,
            () => ApplySwapCandidate(swapCandidate, phaseNames[heavyIdx], phaseNames[lightIdx], heavyIdx, lightIdx, loads));

        AppLog.Debug($"BalancePhases swap #{step}: {swapCandidate.HeavyUnit.Label} ↔ {swapCandidate.LightUnit.Label}, imbalance={swapCandidate.ResultingImbalance:F1}%, L1={loads[0]:F2} L2={loads[1]:F2} L3={loads[2]:F2}");
        return true;
    }

    /// <summary>
    /// Automatycznie bilansuje obwody na fazy L1/L2/L3
    /// z uwzględnieniem grup RCD (MCBs pod RCD 2P muszą mieć tę samą fazę).
    /// 
    /// Algorytm zachłanny Largest-First Decreasing operujący na "jednostkach bilansowania":
    /// - RCD 2P + wszystkie MCB pod nim → jedna atomowa jednostka
    /// - RCD 4P → MCBs bilansowane indywidualnie
    /// - MCB bez RCD → indywidualnie
    /// 
    /// Zwraca snapshot do Undo.
    /// </summary>
    private static async Task RunMoveRefinementAsync(
        IEnumerable<BalanceUnit> units,
        double[] loads,
        string[] phaseNames,
        BalanceMode mode,
        int voltage)
    {
        for (int step = 0; step < MaxMoveRefinementSteps && NeedsRefinement(loads); step++)
        {
            bool appliedMove = await TryApplyMoveRefinementStepAsync(units, loads, phaseNames, mode, voltage, step);
            if (!appliedMove)
            {
                break;
            }
        }

        double imbalanceAfterMoves = CalculateImbalancePercent(loads[0], loads[1], loads[2]);
        AppLog.Debug($"BalancePhases po move-refinement: L1={loads[0]:F0} L2={loads[1]:F0} L3={loads[2]:F0}, asymetria={imbalanceAfterMoves:F1}%");
    }

    private static async Task RunSwapRefinementAsync(
        IEnumerable<BalanceUnit> units,
        double[] loads,
        string[] phaseNames)
    {
        for (int step = 0; step < MaxSwapRefinementSteps && NeedsRefinement(loads); step++)
        {
            bool appliedSwap = await TryApplySwapRefinementStepAsync(units, loads, phaseNames, step);
            if (!appliedSwap)
            {
                break;
            }
        }
    }

    public static async Task<Dictionary<string, string>> BalancePhasesAsync(
        IEnumerable<SymbolItem> allSymbols,
        BalanceMode mode = BalanceMode.Current,
        BalanceScope scope = BalanceScope.OnlyUnlocked,
        int voltage = 230)
    {
        var plan = CreateBalancePlan(allSymbols, mode, scope, voltage);
        var snapshot = plan.Snapshot;
        var symbolList = plan.WorkingSymbols;
        var units = plan.Units;
        var loads = plan.Loads;

        ApplyPhaseIndicatorAssignments(plan);

        // 1b. Kontrolki faz — wyklucz z bilansowania, przypisz L1/L2/L3
        if (plan.PhaseIndicatorAssignments.Count > 0)
        {
            AppLog.Debug($"BalancePhases: wykluczono {plan.PhaseIndicatorAssignments.Count} kontrolek faz (przypisano L1/L2/L3)");
        }

        LogBalancePlan(plan, mode, scope);

        PhaseDistributionExecutionHelper.ClearSelection(symbolList);

        // 5. Zachłanne przypisywanie (z animacją)
        string[] phaseNames = PhaseNames;
        await PhaseDistributionExecutionHelper.ExecuteGreedyAssignmentAsync(units, loads, phaseNames, MinIndex);

        double imbalanceAfterGreedy = CalculateImbalancePercent(loads[0], loads[1], loads[2]);
        AppLog.Debug($"BalancePhases po greedy: L1={loads[0]:F0} L2={loads[1]:F0} L3={loads[2]:F0}, asymetria={imbalanceAfterGreedy:F1}%");

        // ==========================================
        // 6. Refinement Phase 1: Move MCBs heavy → light
        // ==========================================
        // Przenoszenie MCB z realną mocą (PowerW>0) między fazami.
        // Zasada elektryczna: MCB pod RCD 2P MUSI mieć tę samą fazę co RCD.
        // Dozwolone ruchy:
        //   a) MCB z grupy RCD → inna grupa RCD na lżejszej fazie
        //   b) Standalone MCB → zmiana fazy bezpośrednio
        // Cel: asymetria ≤ 3%
        await RunMoveRefinementAsync(units, loads, phaseNames, mode, voltage);

        // ==========================================
        // 7. Refinement Phase 2: Swap pairs between phases
        // ==========================================
        // Jeśli pojedyncze przeniesienie nie pomaga, próbujemy zamiany par:
        // MCB z ciężkiej fazy ↔ MCB z lekkiej fazy, jeśli swap zmniejsza asymetrię.
        await RunSwapRefinementAsync(units, loads, phaseNames);

        double finalImbalance = CalculateImbalancePercent(loads[0], loads[1], loads[2]);
        AppLog.Debug($"BalancePhases DONE: L1={loads[0]:F0} L2={loads[1]:F0} L3={loads[2]:F0}, asymetria={finalImbalance:F1}%");

        return snapshot;
    }

    private const double ModuleGap = 0.2;

    /// <summary>
    /// Układa MCB w docelowej grupie: RCD jako pierwszy (skrajnie lewy),
    /// potem MCB jeden za drugim od lewej, gap=0.2, bez luk.
    /// </summary>
    private static void RepositionMcbInTargetGroup(SymbolItem mcb, List<SymbolItem> targetGroupSymbols)
    {
        CompactGroup(targetGroupSymbols);
    }

    /// <summary>
    /// Kompaktuje całą grupę: RCD NIE zmienia pozycji, MCB układane ciasno
    /// na lewo od RCD (od lewej do prawej, kończąc tuż przed RCD).
    /// Eliminuje luki po zabranych modułach.
    /// </summary>
    internal static void CompactGroup(List<SymbolItem> groupSymbols)
    {
        if (groupSymbols == null || groupSymbols.Count == 0) return;

        var head = groupSymbols.FirstOrDefault(s => IsGroupHeadSymbol(s));
        var mcbs = groupSymbols.Where(s => s != head).OrderBy(s => s.X).ToList();

        if (head != null)
        {
            double totalMcbWidth = mcbs.Sum(m => m.Width) + Math.Max(mcbs.Count - 1, 0) * ModuleGap;
            double startX = head.X - ModuleGap - totalMcbWidth;

            double cursor = startX;
            foreach (var mcb in mcbs)
            {
                mcb.X = cursor;
                mcb.Y = head.Y;
                cursor += mcb.Width + ModuleGap;
            }
        }
        else
        {
            var leftmost = mcbs.OrderBy(s => s.X).First();
            double cursor = leftmost.X;
            double anchorY = leftmost.Y;
            foreach (var mcb in mcbs)
            {
                mcb.X = cursor;
                mcb.Y = anchorY;
                cursor += mcb.Width + ModuleGap;
            }
        }
    }

    private static bool IsGroupHeadSymbol(SymbolItem s)
    {
        var combined = ((s.Type ?? "") + " " + (s.VisualPath ?? "")).ToUpperInvariant();
        return combined.Contains("RCD")
            || combined.Contains("RÓŻNICOW")
            || combined.Contains("FR")
            || combined.Contains("ROZŁĄCZNIK")
            || combined.Contains("ISOLATOR")
            || combined.Contains("SWITCH");
    }

    private static bool IsPhaseIndicator(SymbolItem s)
    {
        var combined = ((s.Type ?? "") + " " + (s.VisualPath ?? "") + " " + (s.Label ?? "")).ToUpperInvariant();
        return combined.Contains("KONTROLK") || combined.Contains("INDICATOR")
            || combined.Contains("LAMPKA") || combined.Contains("SYGNALIZAT")
            || combined.Contains("KONTROLKIFAZ");
    }

    /// <summary>
    /// Bug #2 fix: przy remisie rozkładamy round-robin zamiast zawsze faworyzować L1.
    /// Używamy statycznego licznika żeby przy identycznych obciążeniach
    /// rozdzielać równomiernie na L1→L2→L3→L1→...
    /// </summary>
    [System.ThreadStatic]
    private static int _minTieBreaker;

    private static int MinIndex(double[] arr)
    {
        double minVal = arr[0];
        for (int i = 1; i < arr.Length; i++)
            if (arr[i] < minVal) minVal = arr[i];

        // Zbierz wszystkie indeksy z wartością minimalną (tolerancja 0.01)
        var tied = new List<int>();
        for (int i = 0; i < arr.Length; i++)
            if (Math.Abs(arr[i] - minVal) < 0.01)
                tied.Add(i);

        if (tied.Count == 1) return tied[0];

        // Round-robin wśród remisujących
        int pick = _minTieBreaker % tied.Count;
        _minTieBreaker++;
        return tied[pick];
    }

    private static int MaxIndex(double[] arr)
    {
        int idx = 0;
        for (int i = 1; i < arr.Length; i++)
            if (arr[i] > arr[idx]) idx = i;
        return idx;
    }
}
