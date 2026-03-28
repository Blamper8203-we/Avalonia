using System.Collections.Generic;
using System.Linq;
using DINBoard.Models;
using DINBoard.Services;
using Xunit;

namespace Avalonia.Tests;

public class SchematicNodeBuilderServiceTests
{
    private readonly SchematicNodeBuilderService _builder = new(new ModuleTypeService());

    [Fact]
    public void Build_WithOnlyBusbarAndTerminal_ShouldReturnEmptyResult()
    {
        var symbols = new List<SymbolItem>
        {
            new() { Id = "bus", Type = "Busbar", X = 100 },
            new() { Id = "term", Type = "Listwy zaciskowe", X = 200 }
        };

        var result = _builder.Build(symbols, null);

        Assert.Null(result.Fr);
        Assert.Empty(result.MainDevices);
        Assert.Empty(result.CircuitDevices);
    }

    [Fact]
    public void Build_WithStandaloneFr_ShouldCreateQsNodeUsingProjectMainProtection()
    {
        var fr = new SymbolItem
        {
            Id = "fr1",
            Type = "FR",
            CircuitName = "Main Feed",
            X = 100
        };

        var project = new Project
        {
            PowerConfig = new PowerSupplyConfig
            {
                MainProtection = 50,
                Phases = 3
            }
        };

        var result = _builder.Build(new List<SymbolItem> { fr }, project);

        var node = Assert.Single(result.MainDevices);
        Assert.Equal(fr, result.Fr);
        Assert.Equal(SchematicNodeType.MainBreaker, node.NodeType);
        Assert.Equal("QS", node.Designation);
        Assert.Equal("C50", node.Protection);
        Assert.Equal(3, node.PhaseCount);
        Assert.Equal("Main Feed", node.CircuitName);
    }

    [Fact]
    public void Build_GroupWithFourPoleRcdAndMoreThanTenChildren_ShouldSplitIntoChunksAndAssignPhases()
    {
        var rcd = new SymbolItem
        {
            Id = "rcd1",
            Type = "RCD 4P",
            Group = "G1",
            Phase = "L1+L2+L3",
            X = 100
        };

        var children = Enumerable.Range(1, 11)
            .Select(i => new SymbolItem
            {
                Id = $"mcb{i}",
                Type = "MCB 1P",
                Group = "G1",
                Phase = "PENDING",
                X = 100 + (i * 50)
            })
            .ToList();

        var symbols = new List<SymbolItem> { rcd };
        symbols.AddRange(children);

        var result = _builder.Build(symbols, null);

        Assert.Equal(2, result.MainDevices.Count);
        Assert.Empty(result.CircuitDevices);

        var firstChunk = result.MainDevices[0];
        var secondChunk = result.MainDevices[1];

        Assert.Equal("Q1", firstChunk.Designation);
        Assert.Equal("Q1", secondChunk.Designation);
        Assert.Equal(10, firstChunk.Children.Count);
        Assert.Single(secondChunk.Children);
        Assert.Contains("(cd.)", secondChunk.Protection);

        Assert.Equal("F1.1", firstChunk.Children[0].Designation);
        Assert.Equal("F1.10", firstChunk.Children[9].Designation);
        Assert.Equal("F1.11", secondChunk.Children[0].Designation);

        Assert.Equal("L1", firstChunk.Children[0].Phase);
        Assert.Equal("L2", firstChunk.Children[1].Phase);
        Assert.Equal("L3", firstChunk.Children[2].Phase);
    }

    [Fact]
    public void Build_GroupWithoutHead_ShouldPutSpdInMainAndMcbInCircuits()
    {
        var symbols = new List<SymbolItem>
        {
            new() { Id = "spd1", Type = "SPD", Group = "G1", X = 100 },
            new() { Id = "mcb1", Type = "MCB", Group = "G1", X = 150 },
            new() { Id = "mcb2", Type = "MCB", Group = "G1", X = 200 }
        };

        var result = _builder.Build(symbols, null);

        var spd = Assert.Single(result.MainDevices);
        Assert.Equal(SchematicNodeType.SPD, spd.NodeType);
        Assert.Equal("FA1", spd.Designation);

        Assert.Equal(2, result.CircuitDevices.Count);
        Assert.Equal("F0.1", result.CircuitDevices[0].Designation);
        Assert.Equal("F0.2", result.CircuitDevices[1].Designation);
    }

    [Fact]
    public void Build_StandalonePendingPhases_ShouldAssignRoundRobinAndRespectManualPhase()
    {
        var auto1 = new SymbolItem { Id = "m1", Type = "MCB", Phase = "PENDING", X = 100 };
        var auto2 = new SymbolItem { Id = "m2", Type = "MCB", Phase = "", X = 200 };
        var manual = new SymbolItem
        {
            Id = "m3",
            Type = "MCB",
            Phase = "L3",
            X = 300,
            Parameters = new Dictionary<string, string> { ["ManualPhase"] = "true" }
        };

        var result = _builder.Build(new List<SymbolItem> { auto1, auto2, manual }, null);

        Assert.Equal(3, result.CircuitDevices.Count);
        Assert.Equal("L1", result.CircuitDevices[0].Phase);
        Assert.Equal("L2", result.CircuitDevices[1].Phase);
        Assert.Equal("L3", result.CircuitDevices[2].Phase);

        Assert.Equal("L1", auto1.Phase);
        Assert.Equal("L2", auto2.Phase);
        Assert.Equal("L3", manual.Phase);
    }

    [Fact]
    public void Build_GroupWithSinglePhaseHead_ShouldForceChildrenToHeadPhaseExceptManual()
    {
        var head = new SymbolItem
        {
            Id = "fr-g1",
            Type = "FR 1P",
            Group = "G1",
            Phase = "L2",
            X = 100
        };

        var childAuto = new SymbolItem
        {
            Id = "mcb-auto",
            Type = "MCB",
            Group = "G1",
            Phase = "L1",
            X = 160
        };

        var childManual = new SymbolItem
        {
            Id = "mcb-manual",
            Type = "MCB",
            Group = "G1",
            Phase = "L3",
            X = 220,
            Parameters = new Dictionary<string, string> { ["ManualPhase"] = "true" }
        };

        var result = _builder.Build(new List<SymbolItem> { head, childAuto, childManual }, null);

        Assert.Empty(result.MainDevices);
        var groupNode = Assert.Single(result.CircuitDevices);
        Assert.Equal("QS1", groupNode.Designation);
        Assert.Equal("L2", groupNode.Phase);
        Assert.Equal(2, groupNode.Children.Count);

        var autoChild = groupNode.Children.Single(c => c.Symbol?.Id == "mcb-auto");
        var manualChild = groupNode.Children.Single(c => c.Symbol?.Id == "mcb-manual");

        Assert.Equal("L2", autoChild.Phase);
        Assert.Equal("L3", manualChild.Phase);
        Assert.Equal("L2", childAuto.Phase);
        Assert.Equal("L3", childManual.Phase);
    }

    [Fact]
    public void Build_McbWithoutTableParameters_ShouldUseSymbolNumericFallbackForCableAndPower()
    {
        var mcb = new SymbolItem
        {
            Id = "mcb-fallback",
            Type = "MCB",
            Phase = "L1",
            X = 100,
            PowerW = 2300,
            CableLength = 12.5,
            CableCrossSection = 2.5
        };

        var result = _builder.Build(new List<SymbolItem> { mcb }, null);
        var node = Assert.Single(result.CircuitDevices);

        Assert.Equal("2.5 mm²", node.CableSpec);
        Assert.Equal("12.5 m", node.CableLength);
        Assert.Equal("2300 W", node.PowerInfo);
    }

    [Fact]
    public void Build_McbWithTableParameters_ShouldPreferExplicitTableValues()
    {
        var mcb = new SymbolItem
        {
            Id = "mcb-override",
            Type = "MCB",
            Phase = "L1",
            X = 100,
            PowerW = 2300,
            CableLength = 12.5,
            CableCrossSection = 2.5,
            Parameters = new Dictionary<string, string>
            {
                ["CableSpec"] = "3x4 mm²",
                ["CableLength"] = "25 m",
                ["PowerInfo"] = "2.3 kW"
            }
        };

        var result = _builder.Build(new List<SymbolItem> { mcb }, null);
        var node = Assert.Single(result.CircuitDevices);

        Assert.Equal("3x4 mm²", node.CableSpec);
        Assert.Equal("25 m", node.CableLength);
        Assert.Equal("2.3 kW", node.PowerInfo);
    }
}
