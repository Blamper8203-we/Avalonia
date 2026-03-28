using System.Collections.Generic;
using System.Linq;
using DINBoard.Models;

namespace DINBoard.Services;

public interface INoRcdProtectionWarningRule
{
    ValidationMessage? Evaluate(IEnumerable<SymbolItem> symbols);
}

public sealed class NoRcdProtectionWarningRule : INoRcdProtectionWarningRule
{
    public ValidationMessage? Evaluate(IEnumerable<SymbolItem> symbols)
    {
        var symbolList = symbols?.ToList() ?? new List<SymbolItem>();

        var circuitsWithoutRcd = symbolList
            .Where(s => s.Type?.Contains("MCB", System.StringComparison.OrdinalIgnoreCase) == true)
            .Where(s => string.IsNullOrEmpty(s.RcdSymbolId))
            .ToList();

        if (circuitsWithoutRcd.Count == 0)
        {
            return null;
        }

        return new ValidationMessage
        {
            Code = "NO_RCD_PROTECTION",
            Message = $"{circuitsWithoutRcd.Count} obwodów bez ochrony różnicowoprądowej",
            Severity = ValidationSeverity.Warning,
            Details = string.Join(", ", circuitsWithoutRcd.Select(s => s.Label ?? s.Id).Take(5))
        };
    }
}
