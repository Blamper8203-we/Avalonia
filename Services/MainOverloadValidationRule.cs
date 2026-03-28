using System;
using System.Collections.Generic;
using System.Linq;
using DINBoard.Models;

namespace DINBoard.Services;

public interface IMainOverloadValidationRule
{
    ValidationMessage? Evaluate(Project? project, IEnumerable<SymbolItem> symbols);
}

public sealed class MainOverloadValidationRule : IMainOverloadValidationRule
{
    public ValidationMessage? Evaluate(Project? project, IEnumerable<SymbolItem> symbols)
    {
        if (project?.PowerConfig == null)
        {
            return null;
        }

        var symbolList = symbols?.ToList() ?? new List<SymbolItem>();
        var totalPowerW = symbolList.Sum(s => s.PowerW);
        const double cosPhi = 0.9;

        // Active power allowed by main protection: P = U * I * cos(phi) * (sqrt(3) for 3-phase).
        var maxPowerW = project.PowerConfig.Voltage * project.PowerConfig.MainProtection * cosPhi
            * (project.PowerConfig.Phases == 3 ? Math.Sqrt(3) : 1);

        if (totalPowerW <= maxPowerW)
        {
            return null;
        }

        return new ValidationMessage
        {
            Code = "MAIN_OVERLOAD",
            Message = "Przekroczona moc zabezpieczenia głównego",
            Severity = ValidationSeverity.Error,
            Details = $"Moc zainstalowana: {totalPowerW / 1000:F1}kW, Max: {maxPowerW / 1000:F1}kW"
        };
    }
}
