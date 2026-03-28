using System;
using System.Collections.Generic;
using System.Linq;
using DINBoard.Models;

namespace DINBoard.Services;

public interface IProtectionMismatchValidationRule
{
    IReadOnlyList<ValidationMessage> Evaluate(IEnumerable<SymbolItem> symbols);
}

public sealed class ProtectionMismatchValidationRule : IProtectionMismatchValidationRule
{
    private readonly IProtectionCurrentParser _protectionCurrentParser;

    public ProtectionMismatchValidationRule(IProtectionCurrentParser protectionCurrentParser)
    {
        _protectionCurrentParser = protectionCurrentParser ?? throw new ArgumentNullException(nameof(protectionCurrentParser));
    }

    public IReadOnlyList<ValidationMessage> Evaluate(IEnumerable<SymbolItem> symbols)
    {
        var symbolList = symbols?.ToList() ?? new List<SymbolItem>();
        var errors = new List<ValidationMessage>();

        foreach (var symbol in symbolList.Where(s => !string.IsNullOrEmpty(s.ProtectionType)))
        {
            var protectionCurrent = _protectionCurrentParser.Parse(symbol.ProtectionType);
            if (protectionCurrent <= 0 || symbol.CableCrossSection <= 0)
            {
                continue;
            }

            var maxCableCurrent = CommonHelpers.GetCableCapacity(symbol.CableCrossSection, CommonHelpers.CableAmpacityMethodB2);
            if (protectionCurrent > maxCableCurrent)
            {
                errors.Add(new ValidationMessage
                {
                    Code = "PROTECTION_MISMATCH",
                    Message = $"Zabezpieczenie {symbol.ProtectionType} za duże dla przewodu {symbol.CableCrossSection}mm²",
                    Severity = ValidationSeverity.Error,
                    Details = $"Zabezpieczenie: {protectionCurrent}A, Max dla przewodu: {maxCableCurrent:F1}A",
                    SymbolId = symbol.Id
                });
            }
        }

        return errors;
    }
}
