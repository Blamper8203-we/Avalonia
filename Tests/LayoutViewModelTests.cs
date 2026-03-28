using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using DINBoard.Models;
using DINBoard.ViewModels;

using Xunit;

namespace DINBoard.Tests;

public class LayoutViewModelTests
{
    private MainViewModel CreateMockMainViewModel()
    {
        var projectService = new DINBoard.Services.ProjectService();
        var dialogService = new DINBoard.Services.DialogService();
        var undoRedoService = new DINBoard.Services.UndoRedoService();
        var importService = new DINBoard.Services.SymbolImportService();
        var typeService = new DINBoard.Services.ModuleTypeService();
        var persistenceService = new DINBoard.Services.ProjectPersistenceService(projectService, importService);
        var validationService = new DINBoard.Services.ElectricalValidationService();
        var pdfExportService = new DINBoard.Services.PdfExportService(typeService, validationService, importService, new DINBoard.Services.SvgProcessor());
        var bomExportService = new DINBoard.Services.BomExportService(typeService);
        var latexExportService = new DINBoard.Services.Pdf.LatexExportService(typeService, validationService);
        var busbarGenerator = new DINBoard.Services.PowerBusbarGenerator();
        var busbarPlacementService = new DINBoard.Services.BusbarPlacementService(importService, projectService, busbarGenerator);
        var licenseService = new DINBoard.Services.LicenseService();
        var recentProjectsService = new DINBoard.Services.RecentProjectsService();
        
        return new MainViewModel(new MainViewModelDeps(projectService, dialogService, undoRedoService, typeService, importService, persistenceService, validationService, pdfExportService, bomExportService, latexExportService, busbarPlacementService, licenseService, recentProjectsService));
    }

    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        var mainVm = CreateMockMainViewModel();
        var invoked = false;
        using var vm = new LayoutViewModel(mainVm, () => invoked = true);

        Assert.NotNull(vm.GroupFrames);
        Assert.Empty(vm.GroupFrames);
        Assert.NotNull(vm.ClonesToPlace);
        Assert.Empty(vm.ClonesToPlace);
        Assert.False(vm.IsPlacingClones);
        Assert.False(invoked);
    }

    [Fact]
    public void RecalculateGroupFrames_WithNoGroupedSymbols_ShouldClearFrames()
    {
        var mainVm = CreateMockMainViewModel();
        using var vm = new LayoutViewModel(mainVm, () => {});

        mainVm.Symbols.Add(new SymbolItem { X = 10, Y = 10 });
        mainVm.Symbols.Add(new SymbolItem { X = 20, Y = 20, Group = "" });

        vm.RecalculateGroupFrames();

        Assert.Empty(vm.GroupFrames);
    }

    [Fact]
    public void RecalculateGroupFrames_WithGroupedSymbols_ShouldCalculateBoundingBoxWithPadding()
    {
        var mainVm = CreateMockMainViewModel();
        using var vm = new LayoutViewModel(mainVm, () => {});

        mainVm.Symbols.Add(new SymbolItem { X = 10, Y = 10, Width = 18, Height = 90, Group = "G1", GroupName = "Ogrzewanie" });
        mainVm.Symbols.Add(new SymbolItem { X = 50, Y = 10, Width = 36, Height = 90, Group = "G1" });
        mainVm.Symbols.Add(new SymbolItem { X = 100, Y = 200, Width = 18, Height = 90, Group = "G2" });

        vm.RecalculateGroupFrames();

        Assert.Equal(2, vm.GroupFrames.Count);

        var frame1 = vm.GroupFrames[0];
        Assert.Equal("Ogrzewanie", frame1.GroupName);
        Assert.Equal(10 - 6, frame1.X); // minX - padding
        Assert.Equal(10 - 6, frame1.Y); // minY - padding
        Assert.Equal((50 + 36 + 6) - (10 - 6), frame1.Width); // maxX + padding - minX_padding
        Assert.Equal((10 + 90 + 6) - (10 - 6), frame1.Height); // maxY + padding - minY_padding

        var frame2 = vm.GroupFrames[1];
        Assert.Equal("G2", frame2.GroupName);
        Assert.Equal(100 - 6, frame2.X);
    }

    [Fact]
    public void StartPlacingClones_GivenSymbols_PlacesCopiesInCollectionAndEnablesMode()
    {
         var mainVm = CreateMockMainViewModel();
         using var vm = new LayoutViewModel(mainVm, () => {});

         var itemToClone = new SymbolItem { ModuleNumber = 1, Width = 18, Type = "MCB" };
         mainVm.Symbols.Add(itemToClone);

         Assert.False(vm.IsPlacingClones);

         vm.StartPlacingClones(new List<SymbolItem> { itemToClone });

         Assert.True(vm.IsPlacingClones);
         Assert.Single(vm.ClonesToPlace);
         
         var cloned = vm.ClonesToPlace[0];
         Assert.Equal(2, cloned.ModuleNumber);
         Assert.Equal("MCB", cloned.Type);
         Assert.True(cloned.IsSelected);
         Assert.False(itemToClone.IsSelected);
         
         Assert.Equal(2, mainVm.Symbols.Count); // Original + Clone
    }

    [Fact]
    public void SymbolPropertyChanges_RelevantProperty_ShouldInvokeCallback()
    {
        var mainVm = CreateMockMainViewModel();
        var invokeCount = 0;
        using var vm = new LayoutViewModel(mainVm, () => invokeCount++);

        var symbol = new SymbolItem { X = 10, Y = 10, PowerW = 1000 };
        mainVm.Symbols.Add(symbol);
        
        // Addition triggers callback once
        Assert.Equal(1, invokeCount);

        symbol.PowerW = 2000;
        
        // Assert PowerW change
        Assert.Equal(2, invokeCount);

        // X/Y do NOT trigger the generic relevant changed callback, but they trigger RecalculateGroupFrames
        symbol.X = 50; 
        Assert.Equal(2, invokeCount);
    }

    [Fact]
    public void CommitPlacingClones_ShouldForwardToManagerAndResetState()
    {
        var mainVm = CreateMockMainViewModel();
        using var vm = new LayoutViewModel(mainVm, () => {});

        vm.IsPlacingClones = true;
        vm.ClonesToPlace.Add(new SymbolItem());

        vm.CommitClonesPlacement();

        Assert.False(vm.IsPlacingClones);
        Assert.Empty(vm.ClonesToPlace);
    }
}
