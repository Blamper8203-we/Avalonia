using Xunit;
using System.Collections.Generic;
using DINBoard.ViewModels;
using DINBoard.Models;
using DINBoard.Services;

namespace Avalonia.Tests;

public class GroupViewModelTests
{
    private readonly IModuleTypeService _moduleTypeService = new ModuleTypeService();

    [Fact]
    public void CreateGroupsFromSymbols_ShouldGroupByGroupId()
    {
        // Arrange
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { Id = "1", Group = "G1", Type = "MCB" },
            new SymbolItem { Id = "2", Group = "G1", Type = "MCB" },
            new SymbolItem { Id = "3", Group = "G2", Type = "MCB" },
            new SymbolItem { Id = "4" } // No group
        };

        // Act
        var groups = GroupViewModel.CreateGroupsFromSymbols(symbols, null, _moduleTypeService);

        // Assert
        Assert.Equal(2, groups.Count);
        Assert.Equal(2, groups[0].Symbols.Count);
        Assert.Single(groups[1].Symbols);
    }

    [Fact]
    public void CreateGroupsFromSymbols_ShouldPlaceRcdFirst()
    {
        // Arrange
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { Id = "1", Group = "G1", Type = "MCB", X = 100 },
            new SymbolItem { Id = "2", Group = "G1", VisualPath = "RCD_2P.svg", X = 0 },
            new SymbolItem { Id = "3", Group = "G1", Type = "MCB", X = 200 },
        };

        // Act
        var groups = GroupViewModel.CreateGroupsFromSymbols(symbols, null, _moduleTypeService);

        // Assert
        Assert.Single(groups);
        Assert.Equal("2", groups[0].Symbols[0].Id); // RCD should be first
        Assert.Contains("RCD", groups[0].Symbols[0].VisualPath);
    }

    [Fact]
    public void MainSymbol_ShouldReturnRcdIfExists()
    {
        // Arrange
        var group = new GroupViewModel(_moduleTypeService)
        {
            Symbols = new List<SymbolItem>
            {
                new SymbolItem { Id = "1", Type = "MCB" },
                new SymbolItem { Id = "2", VisualPath = "RCD_4P.svg" },
                new SymbolItem { Id = "3", Type = "MCB" },
            }
        };

        // Act
        var main = group.MainSymbol;

        // Assert
        Assert.NotNull(main);
        Assert.Equal("2", main.Id);
    }

    [Fact]
    public void SubSymbols_ShouldExcludeRcdAndAssignNumbers()
    {
        // Arrange
        var group = new GroupViewModel(_moduleTypeService)
        {
            Symbols = new List<SymbolItem>
            {
                new SymbolItem { Id = "rcd", VisualPath = "RCD_2P.svg", RcdRatedCurrent = 40, RcdResidualCurrent = 30, RcdType = "A" },
                new SymbolItem { Id = "mcb1", Type = "MCB" },
                new SymbolItem { Id = "mcb2", Type = "MCB" },
            }
        };

        // Act
        var subs = group.SubSymbols;

        // Assert
        Assert.Equal(2, subs.Count);
        Assert.Equal(1, subs[0].ModuleNumber);
        Assert.Equal(2, subs[1].ModuleNumber);
        Assert.Equal("rcd", subs[0].RcdSymbolId);
        Assert.Equal(40, subs[0].RcdRatedCurrent);
    }

    [Fact]
    public void GetSymbolType_ShouldReturnCorrectType()
    {
        // Assert
        Assert.Equal("RCD", GroupViewModel.GetSymbolType(new SymbolItem { VisualPath = "RCD_2P.svg" }, _moduleTypeService));
        Assert.Equal("MCB", GroupViewModel.GetSymbolType(new SymbolItem { Type = "MCB" }, _moduleTypeService));
        Assert.Equal("CustomType", GroupViewModel.GetSymbolType(new SymbolItem { Type = "CustomType" }, _moduleTypeService));
    }

    [Fact]
    public void CreateGroupsFromSymbols_ShouldUseCustomGroupName()
    {
        // Arrange
        var symbols = new List<SymbolItem>
        {
            new SymbolItem { Id = "1", Group = "G1", GroupName = "Kuchnia" },
            new SymbolItem { Id = "2", Group = "G1" },
        };

        // Act
        var groups = GroupViewModel.CreateGroupsFromSymbols(symbols, null, _moduleTypeService);

        // Assert
        Assert.Single(groups);
        Assert.Equal("Kuchnia", groups[0].Name);
    }

    [Fact]
    public void CreateGroupsFromSymbols_WithProjectGroupOrder_ShouldOrderGroups()
    {
        // Arrange
        var groupA = "d5b5a4b4-1111-2222-3333-444455556666";
        var groupB = "a1b2c3d4-7777-8888-9999-000011112222";

        var symbols = new List<SymbolItem>
        {
            new SymbolItem { Id = "1", Group = groupA, Type = "MCB", X = 100, Y = 300 },
            new SymbolItem { Id = "2", Group = groupB, Type = "MCB", X = 100, Y = 100 },
        };

        var projectGroups = new List<CircuitGroup>
        {
            new CircuitGroup { Id = groupA, Name = "Kuchnia", Order = 2 },
            new CircuitGroup { Id = groupB, Name = "Łazienka", Order = 1 }
        };

        // Act
        var groups = GroupViewModel.CreateGroupsFromSymbols(symbols, projectGroups, _moduleTypeService);

        // Assert
        Assert.Equal(2, groups.Count);
        Assert.Equal(groupB, groups[0].GroupId);
        Assert.Equal(groupA, groups[1].GroupId);
        Assert.Equal("Łazienka", groups[0].Name);
        Assert.Equal("Kuchnia", groups[1].Name);
    }
}
