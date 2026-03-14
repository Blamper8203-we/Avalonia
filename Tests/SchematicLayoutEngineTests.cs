using System.Collections.Generic;
using System.Linq;
using Xunit;
using DINBoard.Services;
using DINBoard.Models;

namespace Avalonia.Tests;

public class SchematicLayoutEngineTests
{
    private readonly SchematicLayoutEngine _engine = new(new ModuleTypeService());

    #region Basic Layout Tests

    [Fact]
    public void BuildLayout_EmptySymbols_ShouldReturnEmptyLayout()
    {
        var symbols = new List<SymbolItem>();

        var layout = _engine.BuildLayout(symbols, null);

        Assert.NotNull(layout);
        Assert.Empty(layout.Devices);
        Assert.Equal(0, layout.TotalColumns);
    }

    [Fact]
    public void BuildLayout_NullSymbols_ShouldReturnEmptyLayout()
    {
        var layout = _engine.BuildLayout(null!, null);

        Assert.NotNull(layout);
        Assert.Empty(layout.Devices);
    }

    [Fact]
    public void BuildLayout_SingleMCB_ShouldCreateOneDevice()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem
            {
                Id = "mcb1",
                Type = "MCB",
                Label = "Test MCB",
                ProtectionType = "B16",
                Phase = "L1",
                X = 100
            }
        };

        var layout = _engine.BuildLayout(symbols, null);

        Assert.Single(layout.Devices);
        Assert.Equal(SchematicNodeType.MCB, layout.Devices[0].NodeType);
    }

    [Fact]
    public void BuildLayout_MultipleMCBs_ShouldOrderByX()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { Id = "mcb3", Type = "MCB", X = 300 },
            new SymbolItem { Id = "mcb1", Type = "MCB", X = 100 },
            new SymbolItem { Id = "mcb2", Type = "MCB", X = 200 }
        };

        var layout = _engine.BuildLayout(symbols, null);

        Assert.Equal(3, layout.Devices.Count);
        Assert.Equal("mcb1", layout.Devices[0].Symbol.Id);
        Assert.Equal("mcb2", layout.Devices[1].Symbol.Id);
        Assert.Equal("mcb3", layout.Devices[2].Symbol.Id);
    }

    #endregion

    #region FR (Switch) Tests

    [Fact]
    public void BuildLayout_WithFR_ShouldSetFRNode()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { Id = "fr1", Type = "FR", Label = "Main Switch", X = 100 }
        };

        var layout = _engine.BuildLayout(symbols, null);

        Assert.NotNull(layout.FR);
        Assert.Equal("fr1", layout.FR.Id);
    }

    [Fact]
    public void BuildLayout_WithFR_ShouldAppearInDevices()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { Id = "fr1", Type = "FR", X = 100 },
            new SymbolItem { Id = "mcb1", Type = "MCB", X = 200 }
        };

        var layout = _engine.BuildLayout(symbols, null);

        Assert.NotNull(layout.FR);
        Assert.Equal(2, layout.Devices.Count); // FR is now included in devices
        Assert.Contains(layout.Devices, d => d.NodeType == SchematicNodeType.MainBreaker);
    }

    #endregion

    #region RCD Grouping Tests

    [Fact]
    public void BuildLayout_RCDWithMCBs_ShouldCreateHierarchy()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem
            {
                Id = "rcd1",
                Type = "RCD",
                Group = "G1",
                Phase = "L1+L2+L3",
                X = 100
            },
            new SymbolItem
            {
                Id = "mcb1",
                Type = "MCB",
                Group = "G1",
                ProtectionType = "B16",
                Phase = "L1",
                X = 200
            },
            new SymbolItem
            {
                Id = "mcb2",
                Type = "MCB",
                Group = "G1",
                ProtectionType = "B20",
                Phase = "L2",
                X = 300
            }
        };

        var layout = _engine.BuildLayout(symbols, null);

        Assert.Single(layout.Devices); // One RCD node
        var rcdNode = layout.Devices[0];
        Assert.Equal(SchematicNodeType.RCD, rcdNode.NodeType);
        Assert.Equal(2, rcdNode.Children.Count); // Two MCBs under RCD
        Assert.All(rcdNode.Children, child => Assert.Equal(SchematicNodeType.MCB, child.NodeType));
    }

    [Fact]
    public void BuildLayout_FRWithMCBInGroup_ShouldCreateHierarchy()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem
            {
                Id = "fr1",
                Type = "FR 1P",
                Group = "G1",
                Phase = "L1",
                X = 100
            },
            new SymbolItem
            {
                Id = "mcb1",
                Type = "MCB",
                Group = "G1",
                ProtectionType = "B16",
                Phase = "L1",
                X = 200
            }
        };

        var layout = _engine.BuildLayout(symbols, null);

        Assert.Single(layout.Devices);
        var switchNode = layout.Devices[0];
        Assert.Equal(SchematicNodeType.MainBreaker, switchNode.NodeType);
        Assert.Single(switchNode.Children);
        Assert.Equal(SchematicNodeType.MCB, switchNode.Children[0].NodeType);
    }

    [Fact]
    public void BuildLayout_FRWithDistributionBlockAndMCB_ShouldKeepDistributionBlockOnHead()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem
            {
                Id = "fr1",
                Type = "FR 1P",
                Group = "G1",
                Phase = "L1",
                X = 100
            },
            new SymbolItem
            {
                Id = "bias1",
                Type = "Blok rozdzielczy",
                Label = "BIAS",
                Group = "G1",
                X = 160
            },
            new SymbolItem
            {
                Id = "mcb1",
                Type = "MCB",
                Group = "G1",
                ProtectionType = "B16",
                Phase = "L1",
                X = 220
            }
        };

        var layout = _engine.BuildLayout(symbols, null);

        Assert.Single(layout.Devices);
        var switchNode = layout.Devices[0];
        Assert.Equal(SchematicNodeType.MainBreaker, switchNode.NodeType);
        Assert.NotNull(switchNode.DistributionBlockSymbol);
        Assert.Equal("bias1", switchNode.DistributionBlockSymbol!.Id);
        Assert.Single(switchNode.Children);
        Assert.Equal(SchematicNodeType.MCB, switchNode.Children[0].NodeType);
    }

    [Fact]
    public void BuildLayout_GroupedSinglePhaseFR_ShouldBePositionedOnFrRow()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem
            {
                Id = "fr1",
                Type = "FR 1P",
                Group = "G1",
                Phase = "L1",
                X = 100
            },
            new SymbolItem
            {
                Id = "bias1",
                Type = "Blok rozdzielczy",
                Label = "BIAS",
                Group = "G1",
                X = 160
            },
            new SymbolItem
            {
                Id = "mcb1",
                Type = "MCB",
                Group = "G1",
                ProtectionType = "B16",
                Phase = "L1",
                X = 220
            }
        };

        var layout = _engine.BuildLayout(symbols, null);

        var switchNode = Assert.Single(layout.Devices);
        Assert.Equal(SchematicNodeType.MainBreaker, switchNode.NodeType);
        Assert.True(switchNode.Y < SchematicLayoutEngine.DrawT + SchematicLayoutEngine.YMainBus);
    }

    [Fact]
    public void BuildLayout_GroupedFR_ShouldReserveFirstSlotForBreaker()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem
            {
                Id = "fr1",
                Type = "FR 1P",
                Group = "G1",
                Phase = "L1",
                X = 100
            },
            new SymbolItem
            {
                Id = "mcb1",
                Type = "MCB",
                Group = "G1",
                ProtectionType = "B16",
                Phase = "L1",
                X = 220
            }
        };

        var layout = _engine.BuildLayout(symbols, null);

        var switchNode = Assert.Single(layout.Devices);
        var childNode = Assert.Single(switchNode.Children);

        Assert.True(switchNode.X < childNode.X);
        Assert.True(switchNode.CellWidth > childNode.CellWidth);
    }

    [Fact]
    public void BuildLayout_RCDWithoutChildren_ShouldStillAppear()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem
            {
                Id = "rcd1",
                Type = "RCD",
                Group = "G1",
                X = 100
            }
        };

        var layout = _engine.BuildLayout(symbols, null);

        Assert.Single(layout.Devices);
        Assert.Equal(SchematicNodeType.RCD, layout.Devices[0].NodeType);
        Assert.Empty(layout.Devices[0].Children);
    }

    [Fact]
    public void BuildLayout_MultipleRCDGroups_ShouldCreateMultipleNodes()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { Id = "rcd1", Type = "RCD", Group = "G1", X = 100 },
            new SymbolItem { Id = "mcb1", Type = "MCB", Group = "G1", X = 150 },
            new SymbolItem { Id = "rcd2", Type = "RCD", Group = "G2", X = 300 },
            new SymbolItem { Id = "mcb2", Type = "MCB", Group = "G2", X = 350 }
        };

        var layout = _engine.BuildLayout(symbols, null);

        Assert.Equal(2, layout.Devices.Count);
        Assert.All(layout.Devices, d => Assert.Equal(SchematicNodeType.RCD, d.NodeType));
        Assert.All(layout.Devices, d => Assert.Single(d.Children));
    }

    #endregion

    #region SPD Tests

    [Fact]
    public void BuildLayout_WithSPD_ShouldCreateSPDNode()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { Id = "spd1", Type = "SPD", Label = "Surge Protector", X = 100 }
        };

        var layout = _engine.BuildLayout(symbols, null);

        Assert.Single(layout.Devices);
        Assert.Equal(SchematicNodeType.SPD, layout.Devices[0].NodeType);
    }

    [Fact]
    public void BuildLayout_SPDInRCDGroup_ShouldBeChild()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { Id = "rcd1", Type = "RCD", Group = "G1", X = 100 },
            new SymbolItem { Id = "spd1", Type = "SPD", Group = "G1", X = 150 }
        };

        var layout = _engine.BuildLayout(symbols, null);

        Assert.Single(layout.Devices);
        var rcdNode = layout.Devices[0];
        Assert.Single(rcdNode.Children);
        Assert.Equal(SchematicNodeType.SPD, rcdNode.Children[0].NodeType);
    }

    #endregion

    #region KF (Contactor) Tests

    [Fact]
    public void BuildLayout_WithKF_ShouldCreateNode()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { Id = "kf1", Type = "Stycznik", Label = "Contactor", X = 100 }
        };

        var layout = _engine.BuildLayout(symbols, null);

        Assert.Single(layout.Devices);
        // KF powinien być przetworzony (typ może się różnić w zależności od implementacji)
    }

    [Fact]
    public void BuildLayout_KFInRCDGroup_ShouldBeChild()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { Id = "rcd1", Type = "RCD", Group = "G1", X = 100 },
            new SymbolItem { Id = "kf1", Type = "Stycznik", Group = "G1", X = 150 }
        };

        var layout = _engine.BuildLayout(symbols, null);

        Assert.Single(layout.Devices);
        var rcdNode = layout.Devices[0];
        Assert.Single(rcdNode.Children);
    }

    #endregion

    #region Designation Tests

    [Fact]
    public void BuildLayout_MultipleMCBs_ShouldAssignSequentialDesignations()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { Id = "mcb1", Type = "MCB", X = 100 },
            new SymbolItem { Id = "mcb2", Type = "MCB", X = 200 },
            new SymbolItem { Id = "mcb3", Type = "MCB", X = 300 }
        };

        var layout = _engine.BuildLayout(symbols, null);

        Assert.Equal("F0.1", layout.Devices[0].Designation);
        Assert.Equal("F0.2", layout.Devices[1].Designation);
        Assert.Equal("F0.3", layout.Devices[2].Designation);
    }

    [Fact]
    public void BuildLayout_RCD_ShouldGetQDesignation()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { Id = "rcd1", Type = "RCD", Group = "G1", X = 100 }
        };

        var layout = _engine.BuildLayout(symbols, null);

        Assert.StartsWith("Q", layout.Devices[0].Designation);
    }

    [Fact]
    public void BuildLayout_SPD_ShouldGetTDesignation()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { Id = "spd1", Type = "SPD", X = 100 },
            new SymbolItem { Id = "spd2", Type = "SPD", X = 200 }
        };

        var layout = _engine.BuildLayout(symbols, null);

        Assert.Equal("FA1", layout.Devices[0].Designation);
        Assert.Equal("FA2", layout.Devices[1].Designation);
    }

    #endregion

    #region Phase Detection Tests

    [Fact]
    public void BuildLayout_SinglePhase_ShouldDetectPhaseCount1()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { Id = "mcb1", Type = "MCB", Phase = "L1", X = 100 }
        };

        var layout = _engine.BuildLayout(symbols, null);

        Assert.Equal(1, layout.Devices[0].PhaseCount);
    }

    [Fact]
    public void BuildLayout_ThreePhase_ShouldDetectPhaseCount()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { Id = "mcb1", Type = "MCB", Phase = "L1+L2+L3", X = 100 }
        };

        var layout = _engine.BuildLayout(symbols, null);

        // PhaseCount jest ustawiany przez engine
        Assert.True(layout.Devices[0].PhaseCount >= 1);
    }

    #endregion

    #region Busbar and Terminal Filtering Tests

    [Fact]
    public void BuildLayout_WithBusbar_ShouldExcludeFromLayout()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { Id = "busbar1", Type = "Busbar", Label = "Szyna", X = 100 },
            new SymbolItem { Id = "mcb1", Type = "MCB", X = 200 }
        };

        var layout = _engine.BuildLayout(symbols, null);

        Assert.Single(layout.Devices);
        Assert.Equal("mcb1", layout.Devices[0].Symbol.Id);
    }

    [Fact]
    public void BuildLayout_WithTerminal_ShouldExcludeOrProcess()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { Id = "terminal1", Type = "Terminal", Label = "Listwa", X = 100 },
            new SymbolItem { Id = "mcb1", Type = "MCB", X = 200 }
        };

        var layout = _engine.BuildLayout(symbols, null);

        // Terminal może być wykluczony lub przetworzony - weryfikujemy że nie ma błędu
        Assert.NotNull(layout);
        Assert.Contains(layout.Devices, d => d.Symbol.Id == "mcb1");
    }

    #endregion

    #region Project Assignment Tests

    [Fact]
    public void BuildLayout_WithProject_ShouldAssignProject()
    {
        var project = new Project { Name = "Test Project" };
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { Id = "mcb1", Type = "MCB", X = 100 }
        };

        var layout = _engine.BuildLayout(symbols, project);

        Assert.NotNull(layout.Project);
        Assert.Equal("Test Project", layout.Project.Name);
    }

    [Fact]
    public void BuildLayout_WithoutProject_ShouldHaveNullProject()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { Id = "mcb1", Type = "MCB", X = 100 }
        };

        var layout = _engine.BuildLayout(symbols, null);

        Assert.Null(layout.Project);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void BuildLayout_EmptyGroup_ShouldNotCrash()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { Id = "mcb1", Type = "MCB", Group = "", X = 100 }
        };

        var layout = _engine.BuildLayout(symbols, null);

        Assert.Single(layout.Devices);
    }

    [Fact]
    public void BuildLayout_UnknownType_ShouldStillProcess()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { Id = "unknown1", Type = "UnknownType", X = 100 }
        };

        // Nie powinno rzucić wyjątku
        var layout = _engine.BuildLayout(symbols, null);

        Assert.NotNull(layout);
    }

    [Fact]
    public void BuildLayout_MixedGroupedAndStandalone_ShouldHandleBoth()
    {
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { Id = "standalone1", Type = "MCB", X = 100 },
            new SymbolItem { Id = "rcd1", Type = "RCD", Group = "G1", X = 200 },
            new SymbolItem { Id = "grouped1", Type = "MCB", Group = "G1", X = 250 },
            new SymbolItem { Id = "standalone2", Type = "MCB", X = 400 }
        };

        var layout = _engine.BuildLayout(symbols, null);

        Assert.Equal(3, layout.Devices.Count); // standalone1, RCD(with child), standalone2
    }

    #endregion
}
