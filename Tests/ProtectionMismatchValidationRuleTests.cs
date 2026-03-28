using System.Collections.Generic;
using DINBoard.Models;
using DINBoard.Services;
using Xunit;

namespace Avalonia.Tests;

public class ProtectionMismatchValidationRuleTests
{
    private readonly ProtectionMismatchValidationRule _rule = new(new ProtectionCurrentParser());

    [Fact]
    public void Evaluate_WhenProtectionExceedsCableCapacity_ShouldReturnError()
    {
        var symbols = new List<SymbolItem>
        {
            new() { Id = "mcb1", ProtectionType = "B32", CableCrossSection = 1.5 }
        };

        var errors = _rule.Evaluate(symbols);

        Assert.Contains(errors, e => e.Code == "PROTECTION_MISMATCH" && e.SymbolId == "mcb1");
    }

    [Fact]
    public void Evaluate_WhenProtectionFitsCableCapacity_ShouldReturnNoErrors()
    {
        var symbols = new List<SymbolItem>
        {
            new() { Id = "mcb1", ProtectionType = "B16", CableCrossSection = 2.5 }
        };

        var errors = _rule.Evaluate(symbols);

        Assert.Empty(errors);
    }
}
