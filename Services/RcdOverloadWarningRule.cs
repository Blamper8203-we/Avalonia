using System;
using System.Collections.Generic;
using System.Linq;
using DINBoard.Models;

namespace DINBoard.Services;

public interface IRcdOverloadWarningRule
{
    IReadOnlyList<ValidationMessage> Evaluate(IEnumerable<SymbolItem> symbols, int mainProtection);
}

public sealed class RcdOverloadWarningRule : IRcdOverloadWarningRule
{
    private readonly IProtectionCurrentParser _protectionCurrentParser;

    public RcdOverloadWarningRule(IProtectionCurrentParser protectionCurrentParser)
    {
        _protectionCurrentParser = protectionCurrentParser ?? throw new ArgumentNullException(nameof(protectionCurrentParser));
    }

    public IReadOnlyList<ValidationMessage> Evaluate(IEnumerable<SymbolItem> symbols, int mainProtection)
    {
        var symbolList = symbols?.ToList() ?? new List<SymbolItem>();
        var warnings = new List<ValidationMessage>();

        var rcds = symbolList.Where(s =>
            (s.Type ?? "").Contains("RCD", StringComparison.OrdinalIgnoreCase) ||
            (s.Type ?? "").Contains("Różnicow", StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var rcd in rcds)
        {
            var rcdCurrent = _protectionCurrentParser.Parse(rcd.ProtectionType);
            if (rcdCurrent == 0)
            {
                continue;
            }

            var mcbsUnderRcd = symbolList.Where(s => s.RcdSymbolId == rcd.Id).ToList();
            var sumCurrents = mcbsUnderRcd.Sum(m => _protectionCurrentParser.Parse(m.ProtectionType));

            // Warn when sum of downstream MCB ratings exceeds RCD In and main protection does not limit this current.
            if (sumCurrents > rcdCurrent && (mainProtection == 0 || mainProtection > rcdCurrent))
            {
                warnings.Add(new ValidationMessage
                {
                    Code = "RCD_OVERLOAD",
                    Message = $"Suma prądów MCB ({sumCurrents}A) za RCD '{rcd.Label}' przekracza jego prąd znamionowy ({rcdCurrent}A)",
                    Severity = ValidationSeverity.Warning,
                    Details = $"Ryzyko przeciążenia styków RCD w skrajnym przypadku. Zastosuj zabezpieczenie przedlicznikowe max {rcdCurrent}A, RCD o wyższym In lub zmniejsz wartość zabezpieczeń za RCD.",
                    SymbolId = rcd.Id
                });
            }
        }

        return warnings;
    }
}
