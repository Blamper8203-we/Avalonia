using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Collections;
using Xunit;
using Moq;
using DINBoard.Models;
using DINBoard.ViewModels;
using DINBoard.Services;

namespace Avalonia.Tests.ViewModels;

public class CircuitListViewModelTests
{
    private Mock<IModuleTypeService> _moduleTypeServiceMock;
    private ObservableCollection<SymbolItem> _symbols;

    public CircuitListViewModelTests()
    {
        _moduleTypeServiceMock = new Mock<IModuleTypeService>();
        _symbols = new ObservableCollection<SymbolItem>();
    }

    [Fact]
    public void IsCircuitElement_WhenModuleIsMcb_ReturnsTrue()
    {
        // Arrange
        var mcb = new SymbolItem { Type = "MCB" };
        _moduleTypeServiceMock.Setup(m => m.IsPhaseIndicator(mcb)).Returns(false);
        _moduleTypeServiceMock.Setup(m => m.IsRcd(mcb)).Returns(false);

        var viewModel = new CircuitListViewModel(_symbols, _moduleTypeServiceMock.Object);

        // Act
        var result = viewModel.IsCircuitElement(mcb);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsCircuitElement_WhenModuleIsRcd_ReturnsFalse()
    {
        // Arrange
        var rcd = new SymbolItem { Type = "RCD" };
        _moduleTypeServiceMock.Setup(m => m.IsPhaseIndicator(rcd)).Returns(false);
        _moduleTypeServiceMock.Setup(m => m.IsRcd(rcd)).Returns(true);

        var viewModel = new CircuitListViewModel(_symbols, _moduleTypeServiceMock.Object);

        // Act
        var result = viewModel.IsCircuitElement(rcd);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsCircuitElement_WhenModuleIsPhaseIndicator_ReturnsFalse()
    {
        // Arrange
        var indicator = new SymbolItem { Type = "PhaseIndicator" };
        _moduleTypeServiceMock.Setup(m => m.IsPhaseIndicator(indicator)).Returns(true);
        _moduleTypeServiceMock.Setup(m => m.IsRcd(indicator)).Returns(false);

        var viewModel = new CircuitListViewModel(_symbols, _moduleTypeServiceMock.Object);

        // Act
        var result = viewModel.IsCircuitElement(indicator);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsCircuitElement_WhenModuleIsTerminalBlock_ReturnsFalse()
    {
        // Arrange
        var terminal = new SymbolItem { Type = "złączka" };
        _moduleTypeServiceMock.Setup(m => m.IsPhaseIndicator(terminal)).Returns(false);
        _moduleTypeServiceMock.Setup(m => m.IsRcd(terminal)).Returns(false);

        var viewModel = new CircuitListViewModel(_symbols, _moduleTypeServiceMock.Object);

        // Act
        var result = viewModel.IsCircuitElement(terminal);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void RefreshList_SortsCircuitsByXCoordinateLinearly()
    {
        // Arrange
        var mcb2 = new SymbolItem { Id = "2", Type = "MCB", X = 200 };
        var mcb1 = new SymbolItem { Id = "1", Type = "MCB", X = 100 };
        var mcb3 = new SymbolItem { Id = "3", Type = "MCB", X = 300 };

        _symbols.Add(mcb2);
        _symbols.Add(mcb3);
        _symbols.Add(mcb1);

        _moduleTypeServiceMock.Setup(m => m.IsPhaseIndicator(It.IsAny<SymbolItem>())).Returns(false);
        _moduleTypeServiceMock.Setup(m => m.IsRcd(It.IsAny<SymbolItem>())).Returns(false);

        var viewModel = new CircuitListViewModel(_symbols, _moduleTypeServiceMock.Object);

        // Act
        viewModel.RefreshList();

        // Assert
        Assert.Equal(3, viewModel.CircuitList.SourceCollection.Cast<SymbolItem>().Count());
        Assert.Equal("1", viewModel.CircuitList.SourceCollection.Cast<SymbolItem>().ElementAt(0).Id);
        Assert.Equal("2", viewModel.CircuitList.SourceCollection.Cast<SymbolItem>().ElementAt(1).Id);
        Assert.Equal("3", viewModel.CircuitList.SourceCollection.Cast<SymbolItem>().ElementAt(2).Id);
    }

    [Fact]
    public void CollectionChanged_WhenNewSymbolAdded_UpdatesCircuitList()
    {
        // Arrange
        var viewModel = new CircuitListViewModel(_symbols, _moduleTypeServiceMock.Object);
        var mcb = new SymbolItem { Type = "MCB" };
        _moduleTypeServiceMock.Setup(m => m.IsPhaseIndicator(mcb)).Returns(false);
        _moduleTypeServiceMock.Setup(m => m.IsRcd(mcb)).Returns(false);

        // Act
        _symbols.Add(mcb);

        // Assert
        Assert.Single(viewModel.CircuitList.SourceCollection.Cast<SymbolItem>());
        Assert.Equal(mcb, viewModel.CircuitList.SourceCollection.Cast<SymbolItem>().First());
    }

    [Fact]
    public void PropertyChanged_WhenLabelChanged_UpdatesCircuitList()
    {
        // Arrange
        var mcb = new SymbolItem { Type = "MCB", Label = "Old" };
        _symbols.Add(mcb);
        _moduleTypeServiceMock.Setup(m => m.IsPhaseIndicator(mcb)).Returns(false);
        _moduleTypeServiceMock.Setup(m => m.IsRcd(mcb)).Returns(false);
        
        var viewModel = new CircuitListViewModel(_symbols, _moduleTypeServiceMock.Object);
        Assert.Equal("Old", viewModel.CircuitList.SourceCollection.Cast<SymbolItem>().First().Label);

        // Act
        mcb.Label = "New";

        // Assert
        Assert.Equal("New", viewModel.CircuitList.SourceCollection.Cast<SymbolItem>().First().Label);
    }

    [Fact]
    public void Dispose_UnsubscribesFromEvents()
    {
        // Arrange
        var viewModel = new CircuitListViewModel(_symbols, _moduleTypeServiceMock.Object);
        var mcb = new SymbolItem { Type = "MCB" };
        _moduleTypeServiceMock.Setup(m => m.IsPhaseIndicator(mcb)).Returns(false);
        _moduleTypeServiceMock.Setup(m => m.IsRcd(mcb)).Returns(false);

        // Act
        viewModel.Dispose();
        _symbols.Add(mcb);

        // Assert
        Assert.Empty(viewModel.CircuitList.SourceCollection.Cast<SymbolItem>()); // Should not have updated after dispose
    }
}
