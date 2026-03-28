using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using DINBoard.Models;

namespace DINBoard.Services;

internal sealed class SchematicNodeBuilderService
{
    private readonly IModuleTypeService _moduleTypeService;

    public SchematicNodeBuilderService(IModuleTypeService moduleTypeService)
    {
        _moduleTypeService = moduleTypeService ?? throw new ArgumentNullException(nameof(moduleTypeService));
    }

    public SchematicNodeBuildResult Build(IReadOnlyCollection<SymbolItem> symbols, Project? project)
    {
        var all = symbols.Where(s => !IsBusbarOrTerminal(s)).ToList();
        if (all.Count == 0)
        {
            return new SchematicNodeBuildResult(null, new List<SchematicNode>(), new List<SchematicNode>());
        }

        var grouped = all.Where(s => !string.IsNullOrEmpty(s.Group))
            .GroupBy(s => s.Group!).OrderBy(g => g.Min(s => s.X)).ToList();
        var standalone = all.Where(s => string.IsNullOrEmpty(s.Group) && !_moduleTypeService.IsDistributionBlock(s)).OrderBy(s => s.X).ToList();

        var fr = standalone.FirstOrDefault(s => _moduleTypeService.GetModuleType(s) == ModuleType.Switch);
        if (fr != null) standalone.Remove(fr);

        var standaloneSpd = standalone.Where(s => _moduleTypeService.IsSpd(s)).ToList();
        var standaloneKf = standalone.Where(IsKf).ToList();
        var standaloneMcb = standalone.Where(s => !_moduleTypeService.IsSpd(s) && !IsKf(s)).ToList();

        foreach (var sym in all)
        {
            sym.ReferenceDesignation = string.Empty;
        }

        int q = 1;
        int fa = 1;
        int h = 1;
        int w = 1;
        int pathNum = 1;

        var mainDevices = new List<SchematicNode>();
        var circuitDevices = new List<SchematicNode>();

        if (fr != null)
        {
            var cfg = project?.PowerConfig;
            var frProtection = fr.ProtectionType ?? (cfg != null ? $"C{cfg.MainProtection}" : "FR");

            mainDevices.Add(new SchematicNode
            {
                NodeType = SchematicNodeType.MainBreaker,
                Symbol = fr,
                Designation = "QS",
                Protection = frProtection,
                CircuitName = fr.CircuitName ?? "Zasilanie główne",
                Phase = fr.Phase ?? "L1+L2+L3",
                PhaseCount = cfg?.Phases ?? 3,
                Width = SchematicLayoutEngine.NW,
                Height = SchematicLayoutEngine.NH,
                CableDesig = GetParam(fr, "CableDesig"),
                CableType = GetParam(fr, "CableType"),
                CableSpec = GetParam(fr, "CableSpec"),
                CableLength = GetParam(fr, "CableLength"),
                PowerInfo = GetParam(fr, "PowerInfo")
            });
        }

        foreach (var kf in standaloneKf) mainDevices.Add(MakeKf(kf, ref h, ref pathNum, 0));
        foreach (var spd in standaloneSpd) mainDevices.Add(MakeSpd(spd, ref fa, ref pathNum, 0));

        var phaseNames = new[] { "L1", "L2", "L3" };
        int phaseIdx = 0;

        foreach (var group in grouped)
        {
            var modules = group.ToList();
            var headDevice = FindGroupHead(modules);
            var distributionBlock = modules.FirstOrDefault(s => _moduleTypeService.IsDistributionBlock(s));
            var groupSpds = modules.Where(s => _moduleTypeService.IsSpd(s)).ToList();
            var groupKfs = modules.Where(IsKf).ToList();
            var mcbs = modules.Where(s => headDevice == null || s.Id != headDevice.Id)
                .Where(s => !_moduleTypeService.IsRcd(s) && !_moduleTypeService.IsSpd(s) && !_moduleTypeService.IsDistributionBlock(s) && !IsKf(s) && !IsBusbarOrTerminal(s) && _moduleTypeService.GetModuleType(s) != ModuleType.Switch)
                .OrderBy(s => headDevice != null ? Math.Abs(s.X - headDevice.X) : s.X).ToList();

            if (headDevice != null)
            {
                bool is4P = DetectPhases(headDevice) >= 3;
                var headPoles = _moduleTypeService.GetPoleCount(headDevice);
                if (headPoles is ModulePoleCount.P4 or ModulePoleCount.P3) is4P = true;
                if (headPoles is ModulePoleCount.P2 or ModulePoleCount.P1) is4P = false;

                string assignedPhase = string.IsNullOrEmpty(headDevice.Phase) || headDevice.Phase is "PENDING" or "pending"
                    ? (is4P ? "L1+L2+L3" : "L1")
                    : headDevice.Phase;

                headDevice.Phase = assignedPhase;

                foreach (var mcb in mcbs)
                {
                    bool mcbManual = mcb.Parameters != null && mcb.Parameters.ContainsKey("ManualPhase") && mcb.Parameters["ManualPhase"] == "true";
                    if (!mcbManual && (string.IsNullOrEmpty(mcb.Phase) || mcb.Phase is "PENDING" or "pending" || !is4P))
                    {
                        if (!is4P) mcb.Phase = assignedPhase;
                        else if (string.IsNullOrEmpty(mcb.Phase) || mcb.Phase is "PENDING" or "pending") mcb.Phase = "L1";
                    }
                }

                bool isRcdHead = _moduleTypeService.IsRcd(headDevice);
                string headDesig = isRcdHead ? $"Q{q++}" : $"QS{q++}";

                string headLabel;
                if (isRcdHead)
                {
                    string rcdPoleLabel = is4P ? "4P" : "2P";
                    string rcdInfoText = headDevice.RcdInfo?.Replace("RCD ", "") ?? "";
                    rcdInfoText = rcdInfoText.Replace(" Typ ", "\ntyp ").Replace(" typ ", "\ntyp ");
                    headLabel = !string.IsNullOrEmpty(headDevice.RcdInfo) ? $"RCD {rcdPoleLabel}\n{rcdInfoText}" : $"RCD {rcdPoleLabel}";
                }
                else
                {
                    headLabel = headDevice.ProtectionType ?? headDevice.Label ?? "FR";
                }

                var headNode = new SchematicNode
                {
                    NodeType = isRcdHead ? SchematicNodeType.RCD : SchematicNodeType.MainBreaker,
                    Symbol = headDevice,
                    DistributionBlockSymbol = distributionBlock,
                    Designation = headDesig,
                    Protection = headLabel,
                    Phase = assignedPhase,
                    PhaseCount = DetectPhases(headDevice),
                    Width = SchematicLayoutEngine.NW,
                    Height = SchematicLayoutEngine.NH,
                    CircuitName = headDevice.CircuitName ?? "",
                    CableDesig = GetParam(headDevice, "CableDesig"),
                    CableType = GetParam(headDevice, "CableType"),
                    CableSpec = GetParam(headDevice, "CableSpec"),
                    CableLength = GetParam(headDevice, "CableLength"),
                    PowerInfo = GetParam(headDevice, "PowerInfo")
                };

                var allChildren = new List<SchematicNode>();
                foreach (var spd in groupSpds) allChildren.Add(MakeSpd(spd, ref fa, ref pathNum, 0));
                foreach (var kf in groupKfs) allChildren.Add(MakeKf(kf, ref h, ref pathNum, 0));

                string qNumStr = headDesig.Replace("Q", "").Replace("S", "");
                int mcbChildIdx = 1;
                foreach (var mcb in mcbs)
                {
                    allChildren.Add(MakeMcbWithDesignation(mcb, $"F{qNumStr}.{mcbChildIdx}", ref w, ref pathNum, 0));
                    mcbChildIdx++;
                }

                AssignChildrenPhase(allChildren, assignedPhase, is4P, phaseNames);

                if (allChildren.Count <= 10)
                {
                    headNode.Children.AddRange(allChildren);
                    if (is4P) mainDevices.Add(headNode);
                    else circuitDevices.Add(headNode);
                }
                else
                {
                    for (int i = 0; i < allChildren.Count; i += 10)
                    {
                        var chunk = allChildren.Skip(i).Take(10).ToList();
                        var chunkNode = new SchematicNode
                        {
                            NodeType = isRcdHead ? SchematicNodeType.RCD : SchematicNodeType.MainBreaker,
                            Symbol = headDevice,
                            DistributionBlockSymbol = distributionBlock,
                            Designation = headDesig,
                            Protection = i == 0 ? headLabel : $"{headLabel} (cd.)",
                            Phase = assignedPhase,
                            PhaseCount = DetectPhases(headDevice),
                            Width = SchematicLayoutEngine.NW,
                            Height = SchematicLayoutEngine.NH,
                            CircuitName = headDevice.CircuitName ?? "",
                            CableDesig = GetParam(headDevice, "CableDesig"),
                            CableType = GetParam(headDevice, "CableType"),
                            CableSpec = GetParam(headDevice, "CableSpec"),
                            CableLength = GetParam(headDevice, "CableLength"),
                            PowerInfo = GetParam(headDevice, "PowerInfo")
                        };
                        chunkNode.Children.AddRange(chunk);

                        if (is4P) mainDevices.Add(chunkNode);
                        else circuitDevices.Add(chunkNode);
                    }
                }
            }
            else
            {
                foreach (var spd in groupSpds) mainDevices.Add(MakeSpd(spd, ref fa, ref pathNum, 0));
                int noRcdIdx = 1;
                foreach (var mcb in mcbs)
                {
                    circuitDevices.Add(MakeMcbWithDesignation(mcb, $"F0.{noRcdIdx}", ref w, ref pathNum, 0));
                    noRcdIdx++;
                }
            }
        }

        int standaloneMcbIdx = 1;
        foreach (var mcb in standaloneMcb)
        {
            var node = MakeMcbWithDesignation(mcb, $"F0.{standaloneMcbIdx}", ref w, ref pathNum, 0);
            standaloneMcbIdx++;

            bool isManual = mcb.Parameters != null && mcb.Parameters.ContainsKey("ManualPhase") && mcb.Parameters["ManualPhase"] == "true";
            if (!isManual && (string.IsNullOrEmpty(node.Phase) || node.Phase is "PENDING" or "pending"))
            {
                node.Phase = phaseNames[phaseIdx % 3];
                mcb.Phase = node.Phase;
                phaseIdx++;
            }

            circuitDevices.Add(node);
        }

        return new SchematicNodeBuildResult(fr, mainDevices, circuitDevices);
    }

    private void AssignChildrenPhase(List<SchematicNode> allChildren, string assignedPhase, bool is4P, string[] phaseNames)
    {
        if (is4P)
        {
            int childPhaseIdx = 0;
            string[][] phasePairs = { new[] { "L1", "L2" }, new[] { "L2", "L3" }, new[] { "L3", "L1" } };
            foreach (var ch in allChildren)
            {
                var childPoles = ch.Symbol != null ? _moduleTypeService.GetPoleCount(ch.Symbol) : ModulePoleCount.P1;
                string childPhase;
                if (childPoles is ModulePoleCount.P3 or ModulePoleCount.P4)
                {
                    childPhase = "L1+L2+L3";
                }
                else if (childPoles == ModulePoleCount.P2)
                {
                    var pair = phasePairs[childPhaseIdx % 3];
                    childPhase = $"{pair[0]}+{pair[1]}";
                    childPhaseIdx++;
                }
                else
                {
                    childPhase = phaseNames[childPhaseIdx % 3];
                    childPhaseIdx++;
                }

                if (ch.Symbol?.Parameters != null && ch.Symbol.Parameters.ContainsKey("ManualPhase") && ch.Symbol.Parameters["ManualPhase"] == "true")
                {
                    childPhase = ch.Symbol.Phase ?? childPhase;
                }

                ch.Phase = childPhase;
                if (ch.Symbol != null) ch.Symbol.Phase = childPhase;
            }

            return;
        }

        foreach (var ch in allChildren)
        {
            string childPhase = assignedPhase;
            if (ch.Symbol?.Parameters != null && ch.Symbol.Parameters.ContainsKey("ManualPhase") && ch.Symbol.Parameters["ManualPhase"] == "true")
            {
                childPhase = ch.Symbol.Phase ?? childPhase;
            }

            ch.Phase = childPhase;
            if (ch.Symbol != null) ch.Symbol.Phase = childPhase;
        }
    }

    private SchematicNode MakeMcbWithDesignation(SymbolItem mcb, string designation, ref int wIdx, ref int pathNum, int column)
    {
        return new SchematicNode
        {
            NodeType = SchematicNodeType.MCB,
            Symbol = mcb,
            Designation = designation,
            Protection = mcb.ProtectionType ?? mcb.Label ?? "MCB",
            CircuitName = mcb.CircuitName ?? mcb.CircuitDescription ?? "",
            Location = mcb.Location ?? "",
            CableDesig = GetParam(mcb, "CableDesig"),
            CableType = GetParam(mcb, "CableType"),
            CableSpec = GetParam(mcb, "CableSpec"),
            CableLength = GetParam(mcb, "CableLength"),
            PowerInfo = GetParam(mcb, "PowerInfo"),
            Phase = mcb.Phase,
            PhaseCount = DetectPhases(mcb),
            Column = column,
            Width = SchematicLayoutEngine.NW,
            Height = SchematicLayoutEngine.NH
        };
    }

    private SchematicNode MakeSpd(SymbolItem spd, ref int faIdx, ref int pathNum, int column)
    {
        return new SchematicNode
        {
            NodeType = SchematicNodeType.SPD,
            Symbol = spd,
            Designation = $"FA{faIdx++}",
            Protection = spd.SpdInfo ?? "SPD",
            Phase = spd.Phase,
            PhaseCount = DetectPhases(spd),
            Column = column,
            Width = SchematicLayoutEngine.NW,
            Height = SchematicLayoutEngine.NH,
            CableDesig = GetParam(spd, "CableDesig"),
            CableType = GetParam(spd, "CableType"),
            CableSpec = GetParam(spd, "CableSpec"),
            CableLength = GetParam(spd, "CableLength"),
            PowerInfo = GetParam(spd, "PowerInfo")
        };
    }

    private SchematicNode MakeKf(SymbolItem kf, ref int hIdx, ref int pathNum, int column)
    {
        return new SchematicNode
        {
            NodeType = SchematicNodeType.PhaseIndicator,
            Symbol = kf,
            Designation = $"H{hIdx++}",
            Protection = kf.Label ?? "KF",
            Phase = "L1+L2+L3",
            PhaseCount = 3,
            Column = column,
            Width = SchematicLayoutEngine.NW,
            Height = SchematicLayoutEngine.NH,
            CableDesig = GetParam(kf, "CableDesig"),
            CableType = GetParam(kf, "CableType"),
            CableSpec = GetParam(kf, "CableSpec"),
            CableLength = GetParam(kf, "CableLength"),
            PowerInfo = GetParam(kf, "PowerInfo")
        };
    }

    private SymbolItem? FindGroupHead(List<SymbolItem> modules)
    {
        return modules.FirstOrDefault(s => _moduleTypeService.IsRcd(s))
            ?? modules.FirstOrDefault(s => _moduleTypeService.IsSwitch(s));
    }

    private int DetectPhases(SymbolItem? sym)
    {
        if (sym == null) return 1;

        if (!string.IsNullOrEmpty(sym.Phase) && sym.Phase != "PENDING" && sym.Phase != "pending" && sym.Phase != "3P")
        {
            int lCount = sym.Phase.Count(c => c == 'L');
            if (lCount > 0) return lCount;
        }

        var poleCount = _moduleTypeService.GetPoleCount(sym);
        if (poleCount != ModulePoleCount.Unknown)
        {
            return poleCount switch
            {
                ModulePoleCount.P4 => 3,
                ModulePoleCount.P3 => 3,
                ModulePoleCount.P2 => 1,
                ModulePoleCount.P1 => 1,
                _ => 1
            };
        }

        return sym.Phase switch
        {
            "L1+L2+L3" => 3,
            "3P" => 3,
            _ => 1
        };
    }

    private bool IsKf(SymbolItem s) => Any(s, "kontrolka", "kontrolki", "indicator", "lampka", "sygnalizat");

    private bool IsBusbarOrTerminal(SymbolItem s) =>
        s.IsTerminalBlock ||
        (s.Type ?? "").Contains("Busbar", StringComparison.OrdinalIgnoreCase) ||
        (s.VisualPath ?? "").Contains("busbar", StringComparison.OrdinalIgnoreCase);

    private static bool Any(SymbolItem s, params string[] keywords)
    {
        var visualPath = s.VisualPath ?? "";
        var type = s.Type ?? "";
        foreach (var keyword in keywords)
        {
            if (visualPath.Contains(keyword, StringComparison.OrdinalIgnoreCase) || type.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetParam(SymbolItem sym, string key, string def = "") =>
        sym.Parameters != null && sym.Parameters.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val)
            ? val
            : key switch
            {
                "CableSpec" => sym.CableCrossSection > 0
                    ? $"{sym.CableCrossSection.ToString("0.#", CultureInfo.InvariantCulture)} mm²"
                    : def,
                "CableLength" => sym.CableLength > 0
                    ? $"{sym.CableLength.ToString("0.#", CultureInfo.InvariantCulture)} m"
                    : def,
                "PowerInfo" => sym.PowerW > 0
                    ? $"{sym.PowerW.ToString("0.#", CultureInfo.InvariantCulture)} W"
                    : def,
                _ => def
            };
}

internal sealed record SchematicNodeBuildResult(
    SymbolItem? Fr,
    List<SchematicNode> MainDevices,
    List<SchematicNode> CircuitDevices);
