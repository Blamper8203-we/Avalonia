using System;
using System.Collections.Generic;
using System.Linq;
using DINBoard.Models;

namespace DINBoard.Services;

public interface IMainBreakerWarningRule
{
    ValidationMessage? Evaluate(IEnumerable<SymbolItem> symbols);
}

public sealed class MainBreakerWarningRule : IMainBreakerWarningRule
{
    public ValidationMessage? Evaluate(IEnumerable<SymbolItem> symbols)
    {
        var symbolList = symbols?.ToList() ?? new List<SymbolItem>();

        var hasMainBreaker = symbolList.Any(s =>
            (s.Type ?? "").Contains("Rozłącznik", StringComparison.OrdinalIgnoreCase) ||
            (s.Type ?? "").Contains("Switch", StringComparison.OrdinalIgnoreCase) ||
            (s.Label ?? "").StartsWith("FR", StringComparison.OrdinalIgnoreCase) ||
            (s.VisualPath ?? "").Contains("switch_disconnector", StringComparison.OrdinalIgnoreCase));

        if (hasMainBreaker)
        {
            return null;
        }

        return new ValidationMessage
        {
            Code = "NO_MAIN_BREAKER",
            Message = "Brak rozłącznika głównego (FR) w rozdzielnicy",
            Severity = ValidationSeverity.Warning,
            Details = "Zalecane jest zainstalowanie głównego aparatu odcinającego zasilanie przed zabezpieczeniami instalacji."
        };
    }
}
