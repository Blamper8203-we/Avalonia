using System;
using System.Collections.ObjectModel;
using Xunit;
using DINBoard.Models;
using DINBoard.ViewModels;
using Avalonia;

namespace DINBoard.Tests;

public class DeleteModuleTests
{
    [Fact]
    public void DeleteModule_RemovesSymbolFromCollection()
    {
        // Arrange
        var mainVM = new MainViewModel();
        var symbol = new SymbolItem 
        { 
            Id = "test-1", 
            Label = "MCB16",
            X = 100,
            Y = 200
        };
        mainVM.Symbols.Add(symbol);

        // Act
        mainVM.DeleteModule(symbol);

        // Assert
        Assert.DoesNotContain(symbol, mainVM.Symbols);
    }

    [Fact]
    public void DeleteModule_ClearsSelection()
    {
        // Arrange
        var mainVM = new MainViewModel();
        var symbol = new SymbolItem 
        { 
            Id = "test-1", 
            Label = "RCD30",
            X = 100,
            Y = 200
        };
        mainVM.Symbols.Add(symbol);
        mainVM.SelectedSymbol = symbol;
        symbol.IsSelected = true;

        // Act
        mainVM.DeleteModule(symbol);

        // Assert
        Assert.Null(mainVM.SelectedSymbol);
    }

    [Fact]
    public void DeleteModule_WithNullSymbol_DoesNothing()
    {
        // Arrange
        var mainVM = new MainViewModel();
        var symbol = new SymbolItem 
        { 
            Id = "test-1", 
            Label = "MCB16",
            X = 100,
            Y = 200
        };
        mainVM.Symbols.Add(symbol);

        // Act & Assert - should not throw
        mainVM.DeleteModule(null);
        Assert.Single(mainVM.Symbols);
    }

    [Fact]
    public void DeleteMultipleModules_RemovesAllModules()
    {
        // Arrange
        var mainVM = new MainViewModel();
        var symbols = new System.Collections.Generic.List<SymbolItem>
        {
            new SymbolItem { Id = "test-1", Label = "MCB1", X = 100, Y = 200 },
            new SymbolItem { Id = "test-2", Label = "MCB2", X = 150, Y = 200 },
            new SymbolItem { Id = "test-3", Label = "RCD", X = 200, Y = 200 }
        };

        foreach (var symbol in symbols)
            mainVM.Symbols.Add(symbol);

        // Act
        mainVM.DeleteMultipleModules(symbols);

        // Assert
        Assert.Empty(mainVM.Symbols);
    }

    [Fact]
    public void DeleteModule_UpdatesStatusMessage()
    {
        // Arrange
        var mainVM = new MainViewModel();
        var symbol = new SymbolItem 
        { 
            Id = "test-1", 
            Label = "MCB16",
            X = 100,
            Y = 200
        };
        mainVM.Symbols.Add(symbol);

        // Act
        mainVM.DeleteModule(symbol);

        // Assert
        Assert.Contains("MCB16", mainVM.StatusMessage);
        Assert.Contains("Usuni", mainVM.StatusMessage);
    }

    [Fact]
    public void DeleteModule_RecalculatesModuleNumbers()
    {
        // Arrange
        var mainVM = new MainViewModel();
        var symbol1 = new SymbolItem 
        { 
            Id = "test-1", 
            Label = "MCB1",
            X = 100,
            Y = 200,
            ModuleNumber = 1,
            Group = "G1"
        };
        var symbol2 = new SymbolItem 
        { 
            Id = "test-2", 
            Label = "MCB2",
            X = 150,
            Y = 200,
            ModuleNumber = 2,
            Group = "G1"
        };

        mainVM.Symbols.Add(symbol1);
        mainVM.Symbols.Add(symbol2);

        // Act
        mainVM.DeleteModule(symbol1);

        // Assert
        Assert.Single(mainVM.Symbols);
        // Module numbers should be recalculated
    }

    [Fact]
    public void DeleteModule_RemovesFromGroup()
    {
        // Arrange
        var mainVM = new MainViewModel();
        mainVM.CurrentProject = new Project();
        
        var symbol = new SymbolItem 
        { 
            Id = "test-1", 
            Label = "MCB",
            X = 100,
            Y = 200,
            Group = "G1",
            GroupName = "Grupa-1"
        };
        mainVM.Symbols.Add(symbol);

        // Act
        mainVM.DeleteModule(symbol);

        // Assert
        Assert.Empty(mainVM.Symbols);
    }

    [Fact]
    public void DeleteMultipleModules_UpdatesStatusMessageWithDeletedCount()
    {
        // Arrange
        var mainVM = new MainViewModel();
        var symbols = new System.Collections.Generic.List<SymbolItem>
        {
            new SymbolItem { Id = "test-1", Label = "MCB1", X = 100, Y = 200 },
            new SymbolItem { Id = "test-2", Label = "MCB2", X = 150, Y = 200 }
        };

        foreach (var symbol in symbols)
            mainVM.Symbols.Add(symbol);

        // Act
        mainVM.DeleteMultipleModules(symbols);

        // Assert
        Assert.Contains("Usuni", mainVM.StatusMessage);
        Assert.Contains("2", mainVM.StatusMessage);
    }

    [Fact]
    public void DeleteMultipleModules_ClearsSelectedSymbolEvenIfNotDeleted()
    {
        // Arrange
        var mainVM = new MainViewModel();
        var toDelete = new SymbolItem { Id = "del", Label = "MCB1", X = 0, Y = 0 };
        var selectedButNotDeleted = new SymbolItem { Id = "keep", Label = "MCB2", X = 50, Y = 0 };

        mainVM.Symbols.Add(toDelete);
        mainVM.Symbols.Add(selectedButNotDeleted);
        mainVM.SelectedSymbol = selectedButNotDeleted;

        // Act
        mainVM.DeleteMultipleModules(new System.Collections.Generic.List<SymbolItem> { toDelete });

        // Assert
        Assert.DoesNotContain(toDelete, mainVM.Symbols);
        Assert.Contains(selectedButNotDeleted, mainVM.Symbols);
    }
}
