#nullable enable
using Xunit;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using DINBoard.Services;
using DINBoard.Services.Pdf;
using DINBoard.ViewModels;
using DINBoard.Models;

namespace Avalonia.Tests;

/// <summary>
/// Tests for MainViewModel functionality
/// </summary>
public class MainViewModelTests
{
    [Fact]
    public void Constructor_DefaultCtor_ShouldInitializeChildViewModels()
    {
        var vm = CreateTestViewModel();

        Assert.NotNull(vm.Validation);
        Assert.NotNull(vm.Schematic);
        Assert.NotNull(vm.CircuitList);
        Assert.NotNull(vm.Layout);
        Assert.NotNull(vm.Exporter);
        Assert.NotNull(vm.Workspace);
        Assert.NotNull(vm.ModuleManager);
        Assert.NotNull(vm.RecentProjects);
        Assert.NotNull(vm.License);
    }

    [Fact]
    public void Constructor_RuntimeCtor_ShouldInitializeRuntimeState()
    {
        var vm = CreateTestViewModelWithServices();

        Assert.NotNull(vm.Validation);
        Assert.NotNull(vm.Schematic);
        Assert.NotNull(vm.CircuitList);
        Assert.NotNull(vm.Layout);
        Assert.NotNull(vm.Exporter);
        Assert.NotNull(vm.Workspace);
        Assert.NotNull(vm.ModuleManager);
        Assert.NotNull(vm.CurrentProject);
        Assert.Equal("Nowy projekt", vm.CurrentProject!.Name);
        Assert.False(vm.HasUnsavedChanges);
    }

    [Fact]
    public void RecalculateModuleNumbers_ShouldAssignZeroToRcd()
    {
        // Arrange
        var vm = CreateTestViewModel();
        vm.Symbols.Add(new SymbolItem
        {
            Id = "rcd1",
            Type = "RCD",
            Group = "G1",
            X = 0
        });
        vm.Symbols.Add(new SymbolItem
        {
            Id = "mcb1",
            Type = "MCB",
            Group = "G1",
            X = 50
        });

        // Act
        vm.RecalculateModuleNumbers();

        // Assert
        var rcd = vm.Symbols[0];
        var mcb = vm.Symbols[1];
        Assert.Equal(0, rcd.ModuleNumber);
        Assert.Equal(1, mcb.ModuleNumber);
    }

    [Fact]
    public void RecalculateModuleNumbers_ShouldTreatRcdTypeCaseInsensitively()
    {
        // Arrange
        var vm = CreateTestViewModel();
        vm.Symbols.Add(new SymbolItem { Id = "rcd", Type = "rcd 2p", Group = "G1", X = 0 });
        vm.Symbols.Add(new SymbolItem { Id = "mcb", Type = "MCB", Group = "G1", X = 100 });

        // Act
        vm.RecalculateModuleNumbers();

        // Assert
        Assert.Equal(0, vm.Symbols.First(s => s.Id == "rcd").ModuleNumber);
        Assert.Equal(1, vm.Symbols.First(s => s.Id == "mcb").ModuleNumber);
    }

    [Fact]
    public void RecalculateModuleNumbers_ShouldNumberMcbsSequentially()
    {
        // Arrange
        var vm = CreateTestViewModel();
        vm.Symbols.Add(new SymbolItem { Id = "rcd", Type = "RCD", Group = "G1", X = 0 });
        vm.Symbols.Add(new SymbolItem { Id = "mcb1", Type = "MCB", Group = "G1", X = 100 });
        vm.Symbols.Add(new SymbolItem { Id = "mcb2", Type = "MCB", Group = "G1", X = 200 });
        vm.Symbols.Add(new SymbolItem { Id = "mcb3", Type = "MCB", Group = "G1", X = 300 });

        // Act
        vm.RecalculateModuleNumbers();

        // Assert
        Assert.Equal(0, vm.Symbols[0].ModuleNumber); // RCD
        Assert.Equal(1, vm.Symbols[1].ModuleNumber); // MCB1
        Assert.Equal(2, vm.Symbols[2].ModuleNumber); // MCB2
        Assert.Equal(3, vm.Symbols[3].ModuleNumber); // MCB3
    }

    [Fact]
    public void RecalculateModuleNumbers_ShouldHandleMultipleGroups()
    {
        // Arrange
        var vm = CreateTestViewModel();
        // Group 1
        vm.Symbols.Add(new SymbolItem { Id = "rcd1", Type = "RCD", Group = "G1", X = 0 });
        vm.Symbols.Add(new SymbolItem { Id = "mcb1", Type = "MCB", Group = "G1", X = 50 });
        // Group 2
        vm.Symbols.Add(new SymbolItem { Id = "rcd2", Type = "RCD", Group = "G2", X = 200 });
        vm.Symbols.Add(new SymbolItem { Id = "mcb2", Type = "MCB", Group = "G2", X = 250 });

        // Act
        vm.RecalculateModuleNumbers();

        // Assert - each group numbered separately
        Assert.Equal(0, vm.Symbols[0].ModuleNumber); // RCD1
        Assert.Equal(1, vm.Symbols[1].ModuleNumber); // MCB1 in G1
        Assert.Equal(0, vm.Symbols[2].ModuleNumber); // RCD2
        Assert.Equal(1, vm.Symbols[3].ModuleNumber); // MCB2 in G2
    }

    [Fact]
    public void RecalculateModuleNumbers_ShouldHandleUngroupedModules()
    {
        // Arrange
        var vm = CreateTestViewModel();
        vm.Symbols.Add(new SymbolItem { Id = "mcb1", Type = "MCB", X = 0 }); // No group
        vm.Symbols.Add(new SymbolItem { Id = "mcb2", Type = "MCB", X = 50 }); // No group

        // Act
        vm.RecalculateModuleNumbers();

        // Assert - ungrouped modules don't get numbered (only grouped modules are numbered)
        // RecalculateModuleNumbers dziala tylko na zgrupowanych modulach
        Assert.Equal(0, vm.Symbols[0].ModuleNumber);
        Assert.Equal(0, vm.Symbols[1].ModuleNumber);
    }

    [Fact]
    public void RecalculateModuleNumbers_ShouldSortByXPosition()
    {
        // Arrange
        var vm = CreateTestViewModel();
        vm.Symbols.Add(new SymbolItem { Id = "mcb3", Type = "MCB", Group = "G1", X = 300 });
        vm.Symbols.Add(new SymbolItem { Id = "mcb1", Type = "MCB", Group = "G1", X = 100 });
        vm.Symbols.Add(new SymbolItem { Id = "mcb2", Type = "MCB", Group = "G1", X = 200 });

        // Act
        vm.RecalculateModuleNumbers();

        // Assert - numbers should be based on X position, not insertion order
        Assert.Equal(1, vm.Symbols.First(s => s.Id == "mcb1").ModuleNumber);
        Assert.Equal(2, vm.Symbols.First(s => s.Id == "mcb2").ModuleNumber);
        Assert.Equal(3, vm.Symbols.First(s => s.Id == "mcb3").ModuleNumber);
    }

    [Fact]
    public void RecalculateModuleNumbers_ShouldKeepTerminalBlockCounterAcrossGroups()
    {
        // Arrange
        var vm = CreateTestViewModel();
        vm.Symbols.Add(new SymbolItem { Id = "tb1", Type = "TerminalBlock", Group = "G1", X = 0 });
        vm.Symbols.Add(new SymbolItem { Id = "mcb1", Type = "MCB", Group = "G1", X = 50 });
        vm.Symbols.Add(new SymbolItem { Id = "tb2", Type = "TerminalBlock", Group = "G2", X = 100 });
        vm.Symbols.Add(new SymbolItem { Id = "mcb2", Type = "MCB", Group = "G2", X = 150 });

        // Act
        vm.RecalculateModuleNumbers();

        // Assert - terminal block numbering is global, MCB numbering remains per group
        Assert.Equal(1, vm.Symbols.First(s => s.Id == "tb1").ModuleNumber);
        Assert.Equal(2, vm.Symbols.First(s => s.Id == "tb2").ModuleNumber);
        Assert.Equal(1, vm.Symbols.First(s => s.Id == "mcb1").ModuleNumber);
        Assert.Equal(1, vm.Symbols.First(s => s.Id == "mcb2").ModuleNumber);
    }

    [Fact]
    public void RecalculateModuleNumbers_ShouldReorderProjectCircuitsBySymbolOrder()
    {
        // Arrange
        var vm = CreateTestViewModel();
        var c1 = new Circuit { Id = "c1", Name = "C1" };
        var c2 = new Circuit { Id = "c2", Name = "C2" };
        var c3 = new Circuit { Id = "c3", Name = "C3" };
        var missing = new Circuit { Id = "cx", Name = "Missing" };

        vm.CurrentProject = new Project
        {
            Groups = new List<CircuitGroup>
            {
                new()
                {
                    Id = "G1",
                    Name = "G1",
                    Order = 1,
                    Circuits = new List<Circuit> { c3, c1, missing, c2 }
                }
            }
        };

        vm.Symbols.Add(new SymbolItem { Id = "s1", Type = "MCB", Group = "G1", CircuitId = "c1", X = 100, Y = 0 });
        vm.Symbols.Add(new SymbolItem { Id = "s2", Type = "MCB", Group = "G1", CircuitId = "c2", X = 200, Y = 0 });
        vm.Symbols.Add(new SymbolItem { Id = "s3", Type = "MCB", Group = "G1", CircuitId = "c3", X = 300, Y = 0 });

        // Act
        vm.RecalculateModuleNumbers();

        // Assert
        var order = vm.CurrentProject.Groups[0].Circuits.Select(c => c.Id).ToList();
        Assert.Equal(new List<string> { "c1", "c2", "c3", "cx" }, order);
    }

    [Fact]
    public void RecalculateModuleNumbers_WithRcd_ShouldOrderCircuitsByDistanceFromRcd()
    {
        // Arrange
        var vm = CreateTestViewModel();
        var cNear = new Circuit { Id = "near", Name = "Near" };
        var cFar = new Circuit { Id = "far", Name = "Far" };

        vm.CurrentProject = new Project
        {
            Groups = new List<CircuitGroup>
            {
                new()
                {
                    Id = "G1",
                    Name = "G1",
                    Order = 1,
                    Circuits = new List<Circuit> { cFar, cNear }
                }
            }
        };

        vm.Symbols.Add(new SymbolItem { Id = "r", Type = "RCD", Group = "G1", X = 0, Y = 0 });
        vm.Symbols.Add(new SymbolItem { Id = "n", Type = "MCB", Group = "G1", CircuitId = "near", X = 40, Y = 0 });
        vm.Symbols.Add(new SymbolItem { Id = "f", Type = "MCB", Group = "G1", CircuitId = "far", X = 140, Y = 0 });

        // Act
        vm.RecalculateModuleNumbers();

        // Assert
        var order = vm.CurrentProject.Groups[0].Circuits.Select(c => c.Id).ToList();
        Assert.Equal(new List<string> { "near", "far" }, order);
    }


    [Fact]
    public void SheetFlagsAndIndex_ShouldStayInSync()
    {
        // Arrange
        var vm = CreateTestViewModel();

        // Act
        vm.Schematic.CurrentSheetIndex = 1;

        // Assert
        Assert.False(vm.Schematic.IsSheet1Active);
        Assert.True(vm.Schematic.IsSheet2Active);

        // Act
        vm.Schematic.IsSheet1Active = true;

        // Assert
        Assert.Equal(0, vm.Schematic.CurrentSheetIndex);
        Assert.True(vm.Schematic.IsSheet1Active);
        Assert.False(vm.Schematic.IsSheet2Active);
    }

    [Fact]
    public void SheetFlagsAndIndex_ShouldSwitchToSheet2WhenFlagSet()
    {
        // Arrange
        var vm = CreateTestViewModel();

        // Act
        vm.Schematic.IsSheet2Active = true;

        // Assert
        Assert.Equal(1, vm.Schematic.CurrentSheetIndex);
        Assert.False(vm.Schematic.IsSheet1Active);
        Assert.True(vm.Schematic.IsSheet2Active);
    }

    [Fact]
    public void NavigateToReference_ShouldSwitchSheetAndUpdateStatus()
    {
        // Arrange
        var vm = CreateTestViewModel();
        vm.Schematic.CurrentSheetIndex = 0;
        var reference = new CircuitReference
        {
            SheetNumber = 2,
            CircuitNumber = 3,
            CircuitName = "Kitchen",
            Phase = "L1"
        };

        // Act
        vm.NavigateToReference(reference);

        // Assert
        Assert.Equal(1, vm.Schematic.CurrentSheetIndex);
        Assert.Contains(reference.Label, vm.StatusMessage);
        Assert.Contains("Arkusz 2", vm.StatusMessage);
    }

    [Fact]
    public void NavigateToReference_WhenSheet2Active_ShouldSwitchBackToSheet1()
    {
        // Arrange
        var vm = CreateTestViewModel();
        vm.Schematic.CurrentSheetIndex = 1;
        var reference = new CircuitReference
        {
            SheetNumber = 2,
            CircuitNumber = 7,
            CircuitName = "Lighting",
            Phase = "L2"
        };

        // Act
        vm.NavigateToReference(reference);

        // Assert
        Assert.Equal(0, vm.Schematic.CurrentSheetIndex);
        Assert.Contains("Arkusz 1", vm.StatusMessage);
    }

    [Fact]
    public void NavigateToReference_WithNullReference_ShouldKeepCurrentSheetAndStatus()
    {
        // Arrange
        var vm = CreateTestViewModel();
        vm.Schematic.CurrentSheetIndex = 1;
        vm.StatusMessage = "Before";

        // Act
        vm.NavigateToReference(null!);

        // Assert
        Assert.Equal(1, vm.Schematic.CurrentSheetIndex);
        Assert.Equal("Before", vm.StatusMessage);
    }

    [Fact]
    public void StartPlacingClones_ShouldCreateDetachedCloneAndEnablePlacementMode()
    {
        // Arrange
        var vm = CreateTestViewModel();
        var original = new SymbolItem
        {
            Id = "orig",
            Type = "MCB",
            Group = "G1",
            GroupName = "Kitchen",
            CircuitId = "circuit-1",
            X = 100,
            Y = 50,
            IsSelected = true
        };
        vm.Symbols.Add(original);

        // Act
        vm.StartPlacingClones(new List<SymbolItem> { original });

        // Assert
        Assert.True(vm.IsPlacingClones);
        var clone = Assert.Single(vm.ClonesToPlace);
        Assert.Contains(clone, vm.Symbols);
        Assert.NotEqual(original.Id, clone.Id);
        Assert.Null(clone.Group);
        Assert.Null(clone.GroupName);
        Assert.NotEqual("circuit-1", clone.CircuitId);
        Assert.True(clone.IsSelected);
        Assert.False(original.IsSelected);
    }

    [Fact]
    public void CommitClonesPlacement_WhenNotInPlacementMode_ShouldDoNothing()
    {
        // Arrange
        var vm = CreateTestViewModelWithServices();
        vm.StatusMessage = "Before";
        vm.ClonesToPlace.Add(new SymbolItem { Id = "c1", Type = "MCB" });

        // Act
        vm.CommitClonesPlacement();

        // Assert
        Assert.False(vm.IsPlacingClones);
        Assert.False(vm.HasUnsavedChanges);
        Assert.Equal("Before", vm.StatusMessage);
        Assert.Single(vm.ClonesToPlace);
    }

    [Fact]
    public void CommitClonesPlacement_WhenNoClonesToPlace_ShouldDoNothing()
    {
        // Arrange
        var vm = CreateTestViewModelWithServices();
        vm.StatusMessage = "Before";
        vm.IsPlacingClones = true;

        // Act
        vm.CommitClonesPlacement();

        // Assert
        Assert.True(vm.IsPlacingClones);
        Assert.False(vm.HasUnsavedChanges);
        Assert.Equal("Before", vm.StatusMessage);
        Assert.Empty(vm.ClonesToPlace);
    }

    [Fact]
    public void CommitClonesPlacement_WhenPlacementActive_ShouldFinalizeStateAndSetStatus()
    {
        // Arrange
        var vm = CreateTestViewModelWithServices();
        var original = new SymbolItem { Id = "orig", Type = "MCB", X = 10, Y = 20, Group = "G1", GroupName = "Kitchen" };
        vm.Symbols.Add(original);
        vm.StartPlacingClones(new List<SymbolItem> { original });

        // Act
        vm.CommitClonesPlacement();

        // Assert
        Assert.False(vm.IsPlacingClones);
        Assert.Empty(vm.ClonesToPlace);
        Assert.True(vm.HasUnsavedChanges);
        Assert.Equal("Powielone elementy postawione na schemacie", vm.StatusMessage);
    }

    [Fact]
    public void CommitClonesPlacement_WhenCloneAlreadyInSymbols_ShouldNotAddDuplicate()
    {
        // Arrange
        var vm = CreateTestViewModelWithServices();
        var original = new SymbolItem { Id = "orig", Type = "MCB", X = 10, Y = 20 };
        vm.Symbols.Add(original);
        vm.StartPlacingClones(new List<SymbolItem> { original });
        var countBeforeCommit = vm.Symbols.Count;

        // Act
        vm.CommitClonesPlacement();

        // Assert
        Assert.Equal(countBeforeCommit, vm.Symbols.Count);
    }

    [Fact]
    public void CommitClonesPlacement_AfterCommit_ShouldSupportUndoAndRedo()
    {
        // Arrange
        var vm = CreateTestViewModelWithServices();
        var original = new SymbolItem { Id = "orig", Type = "MCB", X = 10, Y = 20 };
        vm.Symbols.Add(original);
        vm.StartPlacingClones(new List<SymbolItem> { original });
        vm.CommitClonesPlacement();

        var symbolsAfterCommit = vm.Symbols.Count;

        // Act
        vm.UndoCommand.Execute(null);
        var symbolsAfterUndo = vm.Symbols.Count;

        vm.RedoCommand.Execute(null);
        var symbolsAfterRedo = vm.Symbols.Count;

        // Assert
        Assert.Equal(2, symbolsAfterCommit);
        Assert.Equal(1, symbolsAfterUndo);
        Assert.Equal(2, symbolsAfterRedo);
    }
    [Fact]
    public void DuplicateSelectedCommand_WhenNoMarkedSelection_ShouldCloneSelectedSymbol()
    {
        // Arrange
        var vm = CreateTestViewModelWithServices();
        var selected = new SymbolItem { Id = "single", Type = "MCB", X = 10, Y = 20, Width = 30, Height = 20 };
        vm.Symbols.Add(selected);
        vm.SelectedSymbol = selected;

        // Act
        vm.ModuleManager.DuplicateSelectedCommand.Execute(null);

        // Assert
        Assert.Equal(2, vm.Symbols.Count);
        Assert.False(selected.IsSelected);

        var clone = vm.Symbols.Single(s => s.Id != "single");
        Assert.True(clone.IsSelected);
        Assert.Equal(selected.X + 50, clone.X); // width(30) + margin(20)
        Assert.NotNull(vm.SelectedSymbol);
        Assert.Equal(clone.Id, vm.SelectedSymbol!.Id);
        Assert.Contains("Skopiowano 1", vm.StatusMessage);
        Assert.True(vm.HasUnsavedChanges);
    }

    [Fact]
    public void DuplicateSelectedCommand_WhenNothingSelected_ShouldDoNothing()
    {
        // Arrange
        var vm = CreateTestViewModel();
        var symbol = new SymbolItem { Id = "only", Type = "MCB", X = 10, Y = 20, Width = 30, Height = 20 };
        vm.Symbols.Add(symbol);

        // Act
        vm.ModuleManager.DuplicateSelectedCommand.Execute(null);

        // Assert
        Assert.Single(vm.Symbols);
        Assert.Equal("only", vm.Symbols[0].Id);
    }

    [Fact]
    public void DuplicateSelectedCommand_ShouldCloneSelectedSymbolsWithGroupOffset()
    {
        // Arrange
        var vm = CreateTestViewModelWithServices();
        var first = new SymbolItem { Id = "s1", Type = "MCB", Group = "G1", X = 10, Y = 20, Width = 30, Height = 20, IsSelected = true };
        var second = new SymbolItem { Id = "s2", Type = "MCB", Group = "G1", X = 60, Y = 20, Width = 20, Height = 20, IsSelected = true };

        vm.Symbols.Add(first);
        vm.Symbols.Add(second);

        // Act
        vm.ModuleManager.DuplicateSelectedCommand.Execute(null);

        // Assert
        Assert.Equal(4, vm.Symbols.Count);
        Assert.False(first.IsSelected);
        Assert.False(second.IsSelected);

        var clones = vm.Symbols.Where(s => s.IsSelected).ToList();
        Assert.Equal(2, clones.Count);

        const double expectedOffset = 90; // (maxX+width=80) - minX(10) + 20 margin
        Assert.Contains(clones, c => c.X == first.X + expectedOffset);
        Assert.Contains(clones, c => c.X == second.X + expectedOffset);

        Assert.NotNull(vm.SelectedSymbol);
        Assert.True(vm.SelectedSymbol!.IsSelected);
        Assert.Contains("Skopiowano 2", vm.StatusMessage);
        Assert.True(vm.HasUnsavedChanges);
    }

    [Fact]
    public void DeleteSelectedCommand_WhenNoMarkedSelection_ShouldRemoveSelectedSymbolOnly()
    {
        // Arrange
        var vm = CreateTestViewModelWithServices();
        var selected = new SymbolItem { Id = "sel", Type = "MCB", X = 0, Y = 0 };
        var other = new SymbolItem { Id = "other", Type = "MCB", X = 50, Y = 0 };
        vm.Symbols.Add(selected);
        vm.Symbols.Add(other);
        vm.SelectedSymbol = selected;

        // Act
        vm.ModuleManager.DeleteSelectedCommand.Execute(null);

        // Assert
        Assert.DoesNotContain(selected, vm.Symbols);
        Assert.Contains(other, vm.Symbols);
        Assert.Null(vm.SelectedSymbol);
        Assert.True(vm.HasUnsavedChanges);
    }

    [Fact]
    public void DeleteSelectedCommand_WhenMarkedSelectionExists_ShouldRemoveAllMarked()
    {
        // Arrange
        var vm = CreateTestViewModelWithServices();
        var first = new SymbolItem { Id = "s1", Type = "MCB", X = 0, Y = 0, IsSelected = true };
        var second = new SymbolItem { Id = "s2", Type = "MCB", X = 20, Y = 0, IsSelected = true };
        var remaining = new SymbolItem { Id = "s3", Type = "MCB", X = 40, Y = 0 };
        vm.Symbols.Add(first);
        vm.Symbols.Add(second);
        vm.Symbols.Add(remaining);
        vm.SelectedSymbol = remaining;

        // Act
        vm.ModuleManager.DeleteSelectedCommand.Execute(null);

        // Assert
        Assert.DoesNotContain(first, vm.Symbols);
        Assert.DoesNotContain(second, vm.Symbols);
        Assert.Contains(remaining, vm.Symbols);
        Assert.Null(vm.SelectedSymbol);
        Assert.True(vm.HasUnsavedChanges);
    }

    [Fact]
    public void DeleteSelectedCommand_WhenNothingSelected_ShouldDoNothing()
    {
        // Arrange
        var vm = CreateTestViewModel();
        var symbol = new SymbolItem { Id = "only", Type = "MCB", X = 0, Y = 0 };
        vm.Symbols.Add(symbol);

        // Act
        vm.ModuleManager.DeleteSelectedCommand.Execute(null);

        // Assert
        Assert.Single(vm.Symbols);
        Assert.Equal("only", vm.Symbols[0].Id);
    }

    private static MainViewModel CreateTestViewModel()
    {
        // Create a minimal VM without DI for testing
        return new MainViewModel();
    }

    private static MainViewModel CreateTestViewModelWithServices()
    {
        var projectService = new ProjectService();
        var dialogService = new TestDialogService();
        var undoRedoService = new UndoRedoService();
        var moduleTypeService = new ModuleTypeService();
        var symbolImportService = new SymbolImportService();
        var persistenceService = new ProjectPersistenceService(projectService, symbolImportService);
        var validationService = new ElectricalValidationService();
        var pdfExportService = new PdfExportService(moduleTypeService, validationService, symbolImportService, new SvgProcessor());
        var bomExportService = new BomExportService(moduleTypeService);
        var latexExportService = new LatexExportService(moduleTypeService, validationService);
        var busbarGenerator = new PowerBusbarGenerator();
        var busbarPlacementService = new BusbarPlacementService(symbolImportService, projectService, busbarGenerator);
        var licenseService = new LicenseService();
        var recentProjectsService = new RecentProjectsService();

        return new MainViewModel(new MainViewModelDeps(
            projectService,
            dialogService,
            undoRedoService,
            moduleTypeService,
            symbolImportService,
            persistenceService,
            validationService,
            pdfExportService,
            bomExportService,
            latexExportService,
            busbarPlacementService,
            licenseService,
            recentProjectsService));
    }

    private sealed class TestDialogService : IDialogService
    {
        public void Initialize(Window window) { }
        public Task<Circuit?> ShowCircuitConfigDialogAsync(Circuit circuit) => Task.FromResult<Circuit?>(circuit);
        public Task<ProjectMetadata?> ShowProjectMetadataDialogAsync(ProjectMetadata data) => Task.FromResult<ProjectMetadata?>(data);
        public Task<PdfExportOptions?> ShowPdfExportOptionsDialogAsync() => Task.FromResult<PdfExportOptions?>(new PdfExportOptions());
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task<string?> PickSaveFileAsync(string title, string defaultExtension, string filterName) => Task.FromResult<string?>(null);
        public Task<string?> PickOpenFileAsync(string title, string filterExtension, string filterName) => Task.FromResult<string?>(null);
        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(false);
        public Task<bool> ShowConfirmationDialogAsync(string title, string message) => Task.FromResult(false);
        public Task<string?> ShowPromptAsync(string title, string message, string defaultValue = "") => Task.FromResult<string?>(null);
        public Task<BusbarGeneratorDialogResult?> ShowBusbarGeneratorDialogAsync() => Task.FromResult<BusbarGeneratorDialogResult?>(null);
        public Task<List<ExcelImportRow>?> ShowExcelImportDialogAsync() => Task.FromResult<List<ExcelImportRow>?>(null);
    }
}
