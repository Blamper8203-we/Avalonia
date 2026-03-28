#nullable enable
using System;
using Xunit;
using DINBoard.Models;
using DINBoard.ViewModels;
using DINBoard.Services;
using DINBoard.Services.Pdf;
using Avalonia.Controls;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Avalonia.Tests;

/// <summary>
/// Tests for the SchematicViewModel extracted from MainViewModel.
/// </summary>
public class SchematicViewModelTests
{
    /// <summary>
    /// Creates a real MainViewModel with real services (no mocks for concrete types).
    /// Mirrors the pattern used in MainViewModelTests.CreateTestViewModelWithServices().
    /// </summary>
    private static (MainViewModel MainVm, SchematicViewModel SchematicVm) CreateTestEnvironment()
    {
        var projectService = new ProjectService();
        var dialogService = new SchematicTestDialogService();
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

        var mainVm = new MainViewModel(new MainViewModelDeps(
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
            recentProjectsService
        ));

        // Access the injected SchematicViewModel that MainViewModel creates internally.
        // This ensures we test the same instance that MainViewModel uses.
        return (mainVm, mainVm.Schematic);
    }

    [Fact]
    public void Constructor_WhenMainViewModelIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var projectService = new ProjectService();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SchematicViewModel(null!, projectService));
    }

    [Fact]
    public void SwitchToSheet1_SetsCurrentSheetIndexToZero()
    {
        // Arrange
        var (_, schematicVm) = CreateTestEnvironment();
        schematicVm.CurrentSheetIndex = 1;

        // Act
        schematicVm.SwitchToSheet1Command.Execute(null);

        // Assert
        Assert.Equal(0, schematicVm.CurrentSheetIndex);
    }

    [Fact]
    public void SwitchToSheet2_SetsCurrentSheetIndexToOne()
    {
        // Arrange
        var (_, schematicVm) = CreateTestEnvironment();
        schematicVm.CurrentSheetIndex = 0;

        // Act
        schematicVm.SwitchToSheet2Command.Execute(null);

        // Assert
        Assert.Equal(1, schematicVm.CurrentSheetIndex);
    }

    [Fact]
    public void SheetFlags_Sheet1Active_WhenIndexIsZero()
    {
        // Arrange
        var (_, schematicVm) = CreateTestEnvironment();

        // Act
        schematicVm.CurrentSheetIndex = 0;

        // Assert
        Assert.True(schematicVm.IsSheet1Active);
        Assert.False(schematicVm.IsSheet2Active);
    }

    [Fact]
    public void SheetFlags_Sheet2Active_WhenIndexIsOne()
    {
        // Arrange
        var (_, schematicVm) = CreateTestEnvironment();

        // Act
        schematicVm.CurrentSheetIndex = 1;

        // Assert
        Assert.False(schematicVm.IsSheet1Active);
        Assert.True(schematicVm.IsSheet2Active);
    }

    [Fact]
    public void NavigateToReference_SwitchesToSheet1_AndHighlights()
    {
        // Arrange
        var (mainVm, schematicVm) = CreateTestEnvironment();
        schematicVm.CurrentSheetIndex = 1; // Start on sheet 2
        var reference = new CircuitReference { SheetNumber = 1, CircuitNumber = 3 };

        // Act
        schematicVm.NavigateToReference(reference);

        // Assert — navigating to a reference on sheet 1 should switch to sheet index 0
        Assert.Equal(0, schematicVm.CurrentSheetIndex);
        Assert.True(reference.IsHighlighted, "Reference should be highlighted after navigation");
    }

    /// <summary>A minimal no-op dialog service for unit tests.</summary>
    private sealed class SchematicTestDialogService : IDialogService
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
