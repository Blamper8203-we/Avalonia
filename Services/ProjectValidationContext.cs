using System;
using System.Collections.Generic;
using DINBoard.Models;

namespace DINBoard.Services;

public sealed class ProjectValidationContext
{
    public ProjectValidationContext(Project project, IReadOnlyList<SymbolItem> symbols, int phaseVoltage, PhaseLoadResult phaseLoads)
    {
        Project = project ?? new Project();
        Symbols = symbols ?? Array.Empty<SymbolItem>();
        PhaseVoltage = phaseVoltage;
        PhaseLoads = phaseLoads ?? new PhaseLoadResult();
    }

    public Project Project { get; }
    public IReadOnlyList<SymbolItem> Symbols { get; }
    public int PhaseVoltage { get; }
    public PhaseLoadResult PhaseLoads { get; }
    public int MainProtection => Project.PowerConfig?.MainProtection ?? 0;
}
