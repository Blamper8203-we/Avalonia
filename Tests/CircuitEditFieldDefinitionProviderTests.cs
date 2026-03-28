#nullable enable
using System.Collections.Generic;
using System.Linq;
using DINBoard.Constants;
using DINBoard.Models;
using DINBoard.Services;
using Xunit;

namespace Avalonia.Tests;

public class CircuitEditFieldDefinitionProviderTests
{
    private readonly ModuleTypeService _moduleTypeService = new();

    [Fact]
    public void GetFields_ForFr_ShouldReturnExpectedFieldsInOrder()
    {
        var symbol = new SymbolItem
        {
            Type = "FR 4P",
            ReferenceDesignation = "Q1",
            Label = "Main switch",
            FrType = "40",
            FrRatedCurrent = "40A"
        };

        var fields = CircuitEditFieldDefinitionProvider.GetFields(symbol, _moduleTypeService).ToList();

        Assert.Equal(
            new[] { "ReferenceDesignation", "Label", "FrType", "FrRatedCurrent" },
            fields.Select(field => field.Key).ToArray());
        AssertField(fields[0], CircuitEditFieldKind.Text, "Oznaczenie", "Q1");
        AssertField(fields[1], CircuitEditFieldKind.Text, "Etykieta", "Main switch");
        AssertField(fields[2], CircuitEditFieldKind.Combo, "Typ FR", "40");
        Assert.Equal(DialogConstants.FrPresets, fields[2].Options);
        AssertField(fields[3], CircuitEditFieldKind.Text, "Prad znamionowy", "40A");
    }

    [Fact]
    public void GetFields_ForPhaseIndicator_ShouldReturnExpectedFieldsInOrder()
    {
        var symbol = new SymbolItem
        {
            Type = "KontrolkiFaz",
            ReferenceDesignation = "H1",
            Label = "Kontrolki",
            PhaseIndicatorModel = "3 lampki z bezpiecznikiem",
            PhaseIndicatorFuseRating = "4A gG"
        };

        var fields = CircuitEditFieldDefinitionProvider.GetFields(symbol, _moduleTypeService).ToList();

        Assert.Equal(
            new[] { "ReferenceDesignation", "Label", "PhaseIndicatorModel", "PhaseIndicatorFuseRating" },
            fields.Select(field => field.Key).ToArray());
        AssertField(fields[2], CircuitEditFieldKind.Combo, "Model", "3 lampki z bezpiecznikiem");
        Assert.Equal(DialogConstants.PhaseIndicatorModelPresets, fields[2].Options);
        AssertField(fields[3], CircuitEditFieldKind.Combo, "Bezpiecznik", "4A gG");
        Assert.Equal(DialogConstants.PhaseIndicatorFusePresets, fields[3].Options);
    }

    [Fact]
    public void GetFields_ForRcd_ShouldBuildPresetFromSymbolValues()
    {
        var symbol = new SymbolItem
        {
            Type = "RCD 2P",
            ReferenceDesignation = "FI1",
            RcdRatedCurrent = 63,
            RcdResidualCurrent = 100,
            RcdType = "A"
        };

        var fields = CircuitEditFieldDefinitionProvider.GetFields(symbol, _moduleTypeService).ToList();

        Assert.Equal(new[] { "ReferenceDesignation", "RcdPreset" }, fields.Select(field => field.Key).ToArray());
        AssertField(fields[0], CircuitEditFieldKind.Text, "Oznaczenie", "FI1");
        AssertField(fields[1], CircuitEditFieldKind.Combo, "Typ RCD", "63A/100mA Typ A");
        Assert.Equal(DialogConstants.RcdPresets, fields[1].Options);
    }

    [Fact]
    public void GetFields_ForSpd_ShouldBuildPresetFromSymbolValues()
    {
        var symbol = new SymbolItem
        {
            Type = "SPD 4P",
            ReferenceDesignation = "SPD1",
            SpdType = "T2",
            SpdVoltage = 320,
            SpdDischargeCurrent = 40
        };

        var fields = CircuitEditFieldDefinitionProvider.GetFields(symbol, _moduleTypeService).ToList();

        Assert.Equal(new[] { "ReferenceDesignation", "SpdPreset" }, fields.Select(field => field.Key).ToArray());
        AssertField(fields[0], CircuitEditFieldKind.Text, "Oznaczenie", "SPD1");
        AssertField(fields[1], CircuitEditFieldKind.Combo, "Typ SPD", "T2 320V 40kA");
        Assert.Equal(DialogConstants.SpdPresets, fields[1].Options);
    }

    [Fact]
    public void GetFields_ForMcb2P_ShouldNormalizePendingPhase()
    {
        var symbol = new SymbolItem
        {
            Type = "MCB 2P",
            ReferenceDesignation = "F1",
            CircuitName = "Plyta",
            Location = "Kuchnia",
            ProtectionType = "C20",
            PowerW = 7200,
            Phase = "pending",
            CableLength = 12.5,
            CableCrossSection = 4,
            Parameters = new Dictionary<string, string>
            {
                ["CableDesig"] = "W1",
                ["CableType"] = "YDY 5x4"
            }
        };

        var fields = CircuitEditFieldDefinitionProvider.GetFields(symbol, _moduleTypeService).ToList();

        Assert.Equal(
            new[]
            {
                "ReferenceDesignation",
                "CircuitName",
                "Location",
                "CircuitType",
                "ProtectionType",
                "PowerW",
                "Phase",
                "CableDesig",
                "CableType",
                "CableLength",
                "CableCrossSection"
            },
            fields.Select(field => field.Key).ToArray());

        AssertField(fields[3], CircuitEditFieldKind.Combo, "Typ obwodu", "Gniazdo");
        AssertField(fields[6], CircuitEditFieldKind.Combo, "Faza", "L1");
        Assert.Equal(new[] { "L1+L2", "L2+L3", "L3+L1", "L1", "L2", "L3", "L1+L2+L3" }, fields[6].Options);
        Assert.Equal(7200d, fields[5].NumberValue);
        Assert.Equal(12.5, fields[9].NumberValue);
        Assert.Equal(4d, fields[10].NumberValue);
    }

    [Fact]
    public void GetFields_ForMcb3P_ShouldUseThreePhaseOptions()
    {
        var symbol = new SymbolItem
        {
            Type = "MCB 3P",
            Phase = "L1+L2+L3"
        };

        var fields = CircuitEditFieldDefinitionProvider.GetFields(symbol, _moduleTypeService).ToList();
        var phaseField = fields.Single(field => field.Key == "Phase");

        AssertField(phaseField, CircuitEditFieldKind.Combo, "Faza", "L1+L2+L3");
        Assert.Equal(new[] { "L1+L2+L3", "L1", "L2", "L3", "L1+L2", "L2+L3", "L3+L1" }, phaseField.Options);
    }

    private static void AssertField(
        CircuitEditFieldDefinition field,
        CircuitEditFieldKind kind,
        string expectedLabel,
        string expectedTextValue)
    {
        Assert.Equal(kind, field.Kind);
        Assert.Equal(expectedLabel, field.Label);
        Assert.Equal(expectedTextValue, field.TextValue);
    }
}
