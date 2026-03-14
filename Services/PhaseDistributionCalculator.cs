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
    private class BalanceUnit
    {
        public List<SymbolItem> Symbols { get; } = new();
        public double TotalWeight { get; set; }

        /// <summary>Nazwa (do debugowania)</summary>
        public string Label => Symbols.Count == 1
            ? Symbols[0].Label ?? Symbols[0].Id
            : $"RCD-grupa ({Symbols.Count} szt.)";
    }

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
    public static async Task<Dictionary<string, string>> BalancePhasesAsync(
        IEnumerable<SymbolItem> allSymbols,
        BalanceMode mode = BalanceMode.Current,
        BalanceScope scope = BalanceScope.OnlyUnlocked,
        int voltage = 230)
    {
        var symbolList = allSymbols.ToList();

        // 1. Snapshot do Undo
        var snapshot = new Dictionary<string, string>();
        foreach (var s in symbolList)
            snapshot[s.Id] = s.Phase;

        // 1b. Kontrolki faz — wyklucz z bilansowania, przypisz L1/L2/L3
        var phaseIndicators = symbolList.Where(IsPhaseIndicator).ToList();
        if (phaseIndicators.Count > 0)
        {
            string[] phases = { "L1", "L2", "L3" };
            for (int i = 0; i < phaseIndicators.Count; i++)
                phaseIndicators[i].Phase = phases[i % 3];
            symbolList = symbolList.Where(s => !IsPhaseIndicator(s)).ToList();
            AppLog.Debug($"BalancePhases: wykluczono {phaseIndicators.Count} kontrolek faz (przypisano L1/L2/L3)");
        }

        // 2. Zidentyfikuj RCD-y i ich podrzędne MCBs
        var rcdMap = new Dictionary<string, SymbolItem>(); // rcdId → RCD symbol
        var mcbsByRcd = new Dictionary<string, List<SymbolItem>>(); // rcdId → MCBs pod nim

        foreach (var s in symbolList)
        {
            if (IsGroupHeadSymbol(s))
                rcdMap[s.Id] = s;
        }

        foreach (var s in symbolList)
        {
            if (!string.IsNullOrEmpty(s.RcdSymbolId) && rcdMap.ContainsKey(s.RcdSymbolId))
            {
                if (!mcbsByRcd.ContainsKey(s.RcdSymbolId))
                    mcbsByRcd[s.RcdSymbolId] = new List<SymbolItem>();
                mcbsByRcd[s.RcdSymbolId].Add(s);
            }
        }

        // 3. Buduj jednostki bilansowania i stałe obciążenie bazowe
        double baseL1 = 0, baseL2 = 0, baseL3 = 0;
        var units = new List<BalanceUnit>();
        var processedSymbolIds = new HashSet<string>();

        // Waga = suma wag indywidualnych MCB (spójna z refinement).
        // MCB z PowerW>0 → waga z prądu/mocy; MCB z PowerW=0 → waga 0 (nie wpływa na bilans mocy).
        // Grupy z zerową mocą dostają mikroskopijną wagę żeby i tak trafiły na fazę.
        const double ZeroPowerUnitWeight = 0.001;

        // 3a. RCD 2P (jednofazowe) → atomowe grupy
        foreach (var kvp in rcdMap)
        {
            var rcd = kvp.Value;
            if (!IsRcdSinglePhase(rcd)) continue;

            var mcbs = mcbsByRcd.TryGetValue(rcd.Id, out var list) ? list : new List<SymbolItem>();

            bool anyLocked = rcd.IsPhaseLocked || mcbs.Any(m => m.IsPhaseLocked);
            if (scope == BalanceScope.OnlyUnlocked && anyLocked)
            {
                double w = mcbs.Sum(m => GetSymbolWeight(m, mode, voltage));
                var phase = rcd.Phase?.ToUpperInvariant();
                if (phase == "L2") baseL2 += w;
                else if (phase == "L3") baseL3 += w;
                else baseL1 += w;

                processedSymbolIds.Add(rcd.Id);
                foreach (var m in mcbs) processedSymbolIds.Add(m.Id);
                continue;
            }

            var unit = new BalanceUnit();
            unit.Symbols.Add(rcd);
            unit.Symbols.AddRange(mcbs);
            double powerWeight = mcbs.Sum(m => GetSymbolWeight(m, mode, voltage));
            unit.TotalWeight = powerWeight > 0 ? powerWeight : ZeroPowerUnitWeight * Math.Max(mcbs.Count, 1);

            units.Add(unit);
            processedSymbolIds.Add(rcd.Id);
            foreach (var m in mcbs) processedSymbolIds.Add(m.Id);
        }

        // 3b. MCBs pod RCD 4P (trójfazowe) → indywidualne jednostki
        // UWAGA: RCD sam nie pobiera mocy — jest urządzeniem ochronnym.
        // Moc mają tylko MCBs podrzędne.
        foreach (var kvp in rcdMap)
        {
            var rcd = kvp.Value;
            if (IsRcdSinglePhase(rcd)) continue;
            processedSymbolIds.Add(rcd.Id);

            // RCD 4P nie wnosi własnej mocy — pomijamy rcd.PowerW (Bug #3 fix)

            var mcbs = mcbsByRcd.TryGetValue(rcd.Id, out var list) ? list : new List<SymbolItem>();
            foreach (var m in mcbs)
            {
                processedSymbolIds.Add(m.Id);

                if (!IsSinglePhase(m)) continue;
                double mw = GetSymbolWeight(m, mode, voltage);

                if (scope == BalanceScope.OnlyUnlocked && m.IsPhaseLocked)
                {
                    var ph = m.Phase?.ToUpperInvariant();
                    if (ph == "L2") baseL2 += mw;
                    else if (ph == "L3") baseL3 += mw;
                    else baseL1 += mw;
                    continue;
                }

                var unit = new BalanceUnit();
                unit.Symbols.Add(m);
                unit.TotalWeight = mw > 0 ? mw : ZeroPowerUnitWeight;
                units.Add(unit);
            }
        }

        // 3c. Pozostałe symbole (MCB bez RCD, standalone)
        foreach (var s in symbolList)
        {
            if (processedSymbolIds.Contains(s.Id)) continue;

            if (!IsSinglePhase(s))
            {
                if (s.PowerW > 0)
                {
                    var dist = DistributePower(s.PowerW, s.Phase);
                    baseL1 += GetWeight(dist.L1PowerW, mode, voltage);
                    baseL2 += GetWeight(dist.L2PowerW, mode, voltage);
                    baseL3 += GetWeight(dist.L3PowerW, mode, voltage);
                }
                continue;
            }

            double sw = GetSymbolWeight(s, mode, voltage);

            if (scope == BalanceScope.OnlyUnlocked && s.IsPhaseLocked)
            {
                var ph = s.Phase?.ToUpperInvariant();
                if (ph == "L2") baseL2 += sw;
                else if (ph == "L3") baseL3 += sw;
                else baseL1 += sw;
                continue;
            }

            var unit = new BalanceUnit();
            unit.Symbols.Add(s);
            unit.TotalWeight = sw > 0 ? sw : ZeroPowerUnitWeight;
            units.Add(unit);
        }

        // 4. Sortuj jednostki malejąco po wadze (Largest-First Decreasing)
        units.Sort((a, b) => b.TotalWeight.CompareTo(a.TotalWeight));

        AppLog.Debug($"BalancePhases: RCDs={rcdMap.Count}, units={units.Count}, base L1={baseL1:F0} L2={baseL2:F0} L3={baseL3:F0}, scope={scope}, mode={mode}");
        foreach (var u in units)
        {
            double unitPowerW = u.Symbols.Sum(s => s.PowerW);
            AppLog.Debug($"  Unit: {u.Label}, weight={u.TotalWeight:F2}, powerW={unitPowerW:F0}, symbols={u.Symbols.Count}");
        }

        foreach (var s in symbolList)
            s.IsSelected = false;

        // 5. Zachłanne przypisywanie (z animacją)
        double[] loads = { baseL1, baseL2, baseL3 };
        string[] phaseNames = { "L1", "L2", "L3" };

        foreach (var unit in units)
        {
            foreach (var s in unit.Symbols)
                s.IsSelected = true;

            await Task.Delay(200);

            int targetIdx = MinIndex(loads);
            string assignedPhase = phaseNames[targetIdx];
            loads[targetIdx] += unit.TotalWeight;

            foreach (var s in unit.Symbols)
            {
                s.Phase = assignedPhase;
                s.IsSelected = false;
            }

            await Task.Delay(80);
        }

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
        for (int step = 0; step < 50 && CalculateImbalancePercent(loads[0], loads[1], loads[2]) > 3.0; step++)
        {
            int heavyIdx = MaxIndex(loads);
            int lightIdx = MinIndex(loads);
            if (heavyIdx == lightIdx) break;

            double diff = loads[heavyIdx] - loads[lightIdx];
            if (diff < 0.01) break;
            double idealW = diff / 2.0;

            var targetRcdUnit = units
                .Where(u => u.Symbols.Count > 0
                    && u.Symbols[0].Phase == phaseNames[lightIdx]
                    && u.Symbols.Any(s => IsGroupHeadSymbol(s)))
                .OrderBy(u => u.TotalWeight)
                .FirstOrDefault();

            SymbolItem? bestMcb = null;
            BalanceUnit? bestSource = null;
            double bestWeight = 0;
            double bestFit = double.MaxValue;
            bool bestIsStandalone = false;

            foreach (var unit in units)
            {
                if (unit.Symbols.Count == 0) continue;
                if (unit.Symbols[0].Phase != phaseNames[heavyIdx]) continue;

                bool hasRcd = unit.Symbols.Any(s => IsGroupHeadSymbol(s));

                if (hasRcd)
                {
                    if (targetRcdUnit == null) continue;

                    var movable = unit.Symbols
                        .Where(s => !IsGroupHeadSymbol(s) && IsSinglePhase(s) && s.PowerW > 0)
                        .ToList();
                    if (movable.Count <= 1) continue;

                    foreach (var mcb in movable)
                    {
                        double w = GetSymbolWeight(mcb, mode, voltage);
                        if (w <= 0 || w > diff) continue;
                        double fit = Math.Abs(w - idealW);
                        if (fit < bestFit)
                        {
                            bestFit = fit;
                            bestMcb = mcb;
                            bestSource = unit;
                            bestWeight = w;
                            bestIsStandalone = false;
                        }
                    }
                }
                else
                {
                    var movable = unit.Symbols
                        .Where(s => IsSinglePhase(s) && s.PowerW > 0)
                        .ToList();
                    foreach (var mcb in movable)
                    {
                        double w = GetSymbolWeight(mcb, mode, voltage);
                        if (w <= 0 || w > diff) continue;
                        double fit = Math.Abs(w - idealW);
                        if (fit < bestFit)
                        {
                            bestFit = fit;
                            bestMcb = mcb;
                            bestSource = unit;
                            bestWeight = w;
                            bestIsStandalone = true;
                        }
                    }
                }
            }

            if (bestMcb == null || bestSource == null) break;

            bestMcb.IsSelected = true;
            await Task.Delay(200);

            if (bestIsStandalone)
            {
                // Bug #1 fix: tylko przenosimy bestMcb, NIE wszystkie symbole w unit
                bestMcb.Phase = phaseNames[lightIdx];
                bestSource.TotalWeight -= bestWeight;
            }
            else if (targetRcdUnit != null)
            {
                var targetRcd = targetRcdUnit.Symbols.First(s => IsGroupHeadSymbol(s));

                bestSource.Symbols.Remove(bestMcb);
                bestSource.TotalWeight -= bestWeight;

                targetRcdUnit.Symbols.Add(bestMcb);
                targetRcdUnit.TotalWeight += bestWeight;

                bestMcb.Phase = targetRcd.Phase;
                bestMcb.RcdSymbolId = targetRcd.Id;
                bestMcb.Group = targetRcd.Group;

                CompactGroup(bestSource.Symbols);
                CompactGroup(targetRcdUnit.Symbols);
            }

            loads[heavyIdx] -= bestWeight;
            loads[lightIdx] += bestWeight;

            bestMcb.IsSelected = false;
            await Task.Delay(80);

            AppLog.Debug($"BalancePhases refinement #{step}: {(bestIsStandalone ? "standalone" : "RCD→RCD")} {bestMcb.Label ?? bestMcb.Id} ({bestMcb.PowerW}W, w={bestWeight:F2}) → {phaseNames[lightIdx]}, L1={loads[0]:F2} L2={loads[1]:F2} L3={loads[2]:F2}");
        }

        double imbalanceAfterMoves = CalculateImbalancePercent(loads[0], loads[1], loads[2]);
        AppLog.Debug($"BalancePhases po move-refinement: L1={loads[0]:F0} L2={loads[1]:F0} L3={loads[2]:F0}, asymetria={imbalanceAfterMoves:F1}%");

        // ==========================================
        // 7. Refinement Phase 2: Swap pairs between phases
        // ==========================================
        // Jeśli pojedyncze przeniesienie nie pomaga, próbujemy zamiany par:
        // MCB z ciężkiej fazy ↔ MCB z lekkiej fazy, jeśli swap zmniejsza asymetrię.
        for (int step = 0; step < 30 && CalculateImbalancePercent(loads[0], loads[1], loads[2]) > 3.0; step++)
        {
            int heavyIdx = MaxIndex(loads);
            int lightIdx = MinIndex(loads);
            if (heavyIdx == lightIdx) break;

            double currentImbalance = CalculateImbalancePercent(loads[0], loads[1], loads[2]);

            // Zbierz standalone MCBs na ciężkiej i lekkiej fazie
            var heavyStandalone = units
                .Where(u => u.Symbols.Count > 0
                    && !u.Symbols.Any(s => IsGroupHeadSymbol(s))
                    && u.Symbols[0].Phase == phaseNames[heavyIdx]
                    && u.TotalWeight > 0)
                .ToList();

            var lightStandalone = units
                .Where(u => u.Symbols.Count > 0
                    && !u.Symbols.Any(s => IsGroupHeadSymbol(s))
                    && u.Symbols[0].Phase == phaseNames[lightIdx]
                    && u.TotalWeight > 0)
                .ToList();

            BalanceUnit? bestHeavyUnit = null;
            BalanceUnit? bestLightUnit = null;
            double bestSwapImbalance = currentImbalance;

            foreach (var hu in heavyStandalone)
            {
                foreach (var lu in lightStandalone)
                {
                    // Symuluj swap
                    double[] testLoads = { loads[0], loads[1], loads[2] };
                    testLoads[heavyIdx] -= hu.TotalWeight;
                    testLoads[heavyIdx] += lu.TotalWeight;
                    testLoads[lightIdx] -= lu.TotalWeight;
                    testLoads[lightIdx] += hu.TotalWeight;

                    double testImbalance = CalculateImbalancePercent(testLoads[0], testLoads[1], testLoads[2]);
                    if (testImbalance < bestSwapImbalance - 0.5) // min 0.5% improvement
                    {
                        bestSwapImbalance = testImbalance;
                        bestHeavyUnit = hu;
                        bestLightUnit = lu;
                    }
                }
            }

            if (bestHeavyUnit == null || bestLightUnit == null) break;

            // Wykonaj swap
            foreach (var s in bestHeavyUnit.Symbols)
            {
                s.IsSelected = true;
            }
            foreach (var s in bestLightUnit.Symbols)
            {
                s.IsSelected = true;
            }
            await Task.Delay(200);

            loads[heavyIdx] -= bestHeavyUnit.TotalWeight;
            loads[heavyIdx] += bestLightUnit.TotalWeight;
            loads[lightIdx] -= bestLightUnit.TotalWeight;
            loads[lightIdx] += bestHeavyUnit.TotalWeight;

            foreach (var s in bestHeavyUnit.Symbols)
                s.Phase = phaseNames[lightIdx];
            foreach (var s in bestLightUnit.Symbols)
                s.Phase = phaseNames[heavyIdx];

            foreach (var s in bestHeavyUnit.Symbols)
                s.IsSelected = false;
            foreach (var s in bestLightUnit.Symbols)
                s.IsSelected = false;

            await Task.Delay(80);

            AppLog.Debug($"BalancePhases swap #{step}: {bestHeavyUnit.Label} ↔ {bestLightUnit.Label}, imbalance={bestSwapImbalance:F1}%, L1={loads[0]:F2} L2={loads[1]:F2} L3={loads[2]:F2}");
        }

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
