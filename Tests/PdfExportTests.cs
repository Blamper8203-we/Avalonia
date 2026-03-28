using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using DINBoard.Services;
using DINBoard.ViewModels;
using DINBoard.Models;
using DINBoard.Services.Pdf;

namespace Avalonia.Tests;

public class PdfExportTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PdfExportService _pdfService;
    private readonly MainViewModel _viewModel;

    public PdfExportTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AvaloniaPdfTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);

        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        _pdfService = new PdfExportService(
            new ModuleTypeService(),
            new ElectricalValidationService(),
            new SymbolImportService(),
            new SvgProcessor());
        _viewModel = new MainViewModel();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
        GC.SuppressFinalize(this);
    }

    #region PDF Export Tests

    [Fact]
    public void ExportToPdf_ShouldCreateFile()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test_export.pdf");
        var options = new PdfExportOptions
        {
            ObjectName = "Test Object",
            Address = "Test Address",
            InvestorName = "Test Investor",
            DesignerName = "Test Designer"
        };

        // Act
        _pdfService.ExportToPdf(_viewModel, filePath, options);

        // Assert
        Assert.True(File.Exists(filePath));
        Assert.True(new FileInfo(filePath).Length > 0);
    }

    [Fact]
    public void ExportToPdf_WithSymbols_ShouldIncludeDinRail()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test_with_symbols.pdf");
        var options = new PdfExportOptions
        {
            ObjectName = "Test Object",
            Address = "Test Address",
            InvestorName = "Test Investor",
            DesignerName = "Test Designer"
        };

        // Add test symbols
        _viewModel.Symbols.Add(new SymbolItem
        {
            Id = "test1",
            Type = "MCB",
            Label = "Test MCB",
            X = 100,
            Y = 100,
            Width = 200,
            Height = 1000
        });

        // Act
        _pdfService.ExportToPdf(_viewModel, filePath, options);

        // Assert
        Assert.True(File.Exists(filePath));
        Assert.True(new FileInfo(filePath).Length > 0);
    }

    [Fact]
    public void ExportToPdf_WithCircuitReferences_ShouldIncludeSheet2()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test_with_circuits.pdf");
        var options = new PdfExportOptions
        {
            ObjectName = "Test Object",
            Address = "Test Address",
            InvestorName = "Test Investor",
            DesignerName = "Test Designer"
        };

        // Add circuit references
        _viewModel.Schematic.CircuitReferences.Add(new CircuitReference
        {
            CircuitNumber = 1,
            CircuitName = "Test Circuit",
            Phase = "L1",
            Direction = ReferenceDirection.Right,
            X = 500,
            Y = 200,
            HorizontalLineY = 150,
            HorizontalLineStartX = 300,
            HorizontalLineEndX = 450,
            LinkedMcbIds = new List<string> { "test1" }
        });

        // Act
        _pdfService.ExportToPdf(_viewModel, filePath, options);

        // Assert
        Assert.True(File.Exists(filePath));
        Assert.True(new FileInfo(filePath).Length > 0);
    }

    [Fact]
    public void ExportToPdf_WithPowerBalance_ShouldIncludeSheet4()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test_with_power.pdf");
        var options = new PdfExportOptions
        {
            ObjectName = "Test Object",
            Address = "Test Address",
            InvestorName = "Test Investor",
            DesignerName = "Test Designer"
        };

        // Set power values
        _viewModel.PowerBalance.L1PowerW = 1000;
        _viewModel.PowerBalance.L2PowerW = 1500;
        _viewModel.PowerBalance.L3PowerW = 1200;
        _viewModel.PowerBalance.SimultaneityFactor = 0.8;

        // Act
        _pdfService.ExportToPdf(_viewModel, filePath, options);

        // Assert
        Assert.True(File.Exists(filePath));
        Assert.True(new FileInfo(filePath).Length > 0);
    }

    #endregion

    #region PNG Export Tests

    [Fact]
    public void ExportToPng_ShouldCreateFile()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test_export.png");
        var options = new PdfExportOptions
        {
            ObjectName = "Test Object",
            Address = "Test Address",
            InvestorName = "Test Investor",
            DesignerName = "Test Designer"
        };

        // Dodaj symbol żeby RenderDinRailToImage zwrócił dane
        _viewModel.Symbols.Add(new SymbolItem
        {
            Id = "test1",
            Type = "MCB",
            Label = "Test MCB",
            X = 100,
            Y = 100,
            Width = 200,
            Height = 1000
        });

        // Act
        _pdfService.ExportToPng(_viewModel, filePath, options);

        // Assert
        Assert.True(File.Exists(filePath));
        Assert.True(new FileInfo(filePath).Length > 0);
    }

    [Fact]
    public void ExportToPng_WithSymbols_ShouldIncludeContent()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test_png_with_symbols.png");
        var options = new PdfExportOptions
        {
            ObjectName = "Test Object",
            Address = "Test Address",
            InvestorName = "Test Investor",
            DesignerName = "Test Designer"
        };

        // Add test symbols
        _viewModel.Symbols.Add(new SymbolItem
        {
            Id = "test1",
            Type = "MCB",
            Label = "Test MCB",
            X = 100,
            Y = 100,
            Width = 200,
            Height = 1000
        });

        // Act
        _pdfService.ExportToPng(_viewModel, filePath, options);

        // Assert
        Assert.True(File.Exists(filePath));
        Assert.True(new FileInfo(filePath).Length > 0);
    }

    [Fact]
    public void ExportToPng_WithGroups_ShouldIncludeGroups()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test_png_with_groups.png");
        var options = new PdfExportOptions
        {
            ObjectName = "Test Object",
            Address = "Test Address",
            InvestorName = "Test Investor",
            DesignerName = "Test Designer"
        };

        // Add grouped symbols
        _viewModel.Symbols.Add(new SymbolItem
        {
            Id = "rcd1",
            Type = "RCD",
            Group = "G1",
            GroupName = "Grupa 1",
            Label = "RCD 40A",
            X = 100,
            Y = 100,
            Width = 200,
            Height = 1000
        });

        _viewModel.Symbols.Add(new SymbolItem
        {
            Id = "mcb1",
            Type = "MCB",
            Group = "G1",
            GroupName = "Grupa 1",
            Label = "MCB B16",
            X = 350,
            Y = 100,
            Width = 200,
            Height = 1000
        });

        // Act
        _pdfService.ExportToPng(_viewModel, filePath, options);

        // Assert
        Assert.True(File.Exists(filePath));
        Assert.True(new FileInfo(filePath).Length > 0);
    }

    [Fact]
    public void PdfDinRailService_RenderDinRailToImage_WithRenderCache_ReusesBytesPerVariant()
    {
        var service = new PdfDinRailService(new SymbolImportService(), new SvgProcessor());
        var options = new PdfExportOptions { PngQuality = PngRenderQuality.High };
        var cache = service.CreateRenderCache();

        _viewModel.Symbols.Add(new SymbolItem
        {
            Id = "cache-test",
            Type = "MCB",
            Label = "Cache Test",
            X = 100,
            Y = 100,
            Width = 200,
            Height = 1000
        });

        var baseImageFirst = service.RenderDinRailToImage(_viewModel, options, renderCache: cache);
        var baseImageSecond = service.RenderDinRailToImage(_viewModel, options, renderCache: cache);
        var numberedImage = service.RenderDinRailToImage(_viewModel, options, showNumbers: true, showGroups: false, renderCache: cache);

        Assert.NotNull(baseImageFirst);
        Assert.Same(baseImageFirst, baseImageSecond);
        Assert.NotNull(numberedImage);
        Assert.NotSame(baseImageFirst, numberedImage);
    }

    #endregion

    #region PDF Service Component Tests - Null Container Validation

    // These tests verify that PDF services correctly throw ArgumentNullException
    // when null container is passed (after null-checks were added)

    [Fact]
    public void PdfDinRailService_ThrowsOnNullContainer()
    {
        var service = new PdfDinRailService(new SymbolImportService(), new SvgProcessor());
        var options = new PdfExportOptions();

        Assert.Throws<ArgumentNullException>(() =>
            service.ComposeDinRailDiagram(null!, _viewModel, options));
    }

    [Fact]
    public void PdfCircuitWiringService_ThrowsOnNullContainer()
    {
        var service = new PdfCircuitWiringService(new ModuleTypeService());

        Assert.Throws<ArgumentNullException>(() =>
            service.ComposeCircuitWiringDiagram(null!, _viewModel));
    }

    [Fact]
    public void PdfCircuitTableService_ThrowsOnNullContainer()
    {
        var service = new PdfCircuitTableService(new ModuleTypeService());

        Assert.Throws<ArgumentNullException>(() =>
            service.ComposeCircuitTable(null!, _viewModel));
    }

    [Fact]
    public void PdfPowerBalanceService_ThrowsOnNullContainer()
    {
        var service = new PdfPowerBalanceService(new ModuleTypeService());
        Assert.Throws<ArgumentNullException>(() =>
            service.ComposePowerBalance(null!, _viewModel));
    }

    [Fact]
    public void PdfStandardsService_ThrowsOnNullContainer()
    {
        var service = new PdfStandardsService(new ModuleTypeService(), new ElectricalValidationService());

        Assert.Throws<ArgumentNullException>(() =>
            service.ComposeStandardsCompliance(null!, _viewModel));
    }

    [Fact]
    public void PdfSingleLineDiagramService_ThrowsOnNullContainer()
    {
        var service = new PdfSingleLineDiagramService(new ModuleTypeService());

        Assert.Throws<ArgumentNullException>(() =>
            service.ComposeSingleLineDiagram(null!, _viewModel));
    }

    [Fact]
    public void PdfSingleLineDiagramService_RenderCircuitImage_WithEmptyLayout_ReturnsNull()
    {
        var layout = new SchematicLayout { IsEmpty = true };

        var image = PdfSingleLineDiagramService.RenderCircuitImage(layout, pageIndex: 0, _viewModel);

        Assert.Null(image);
    }

    [Fact]
    public void PdfSingleLineDiagramService_RenderCircuitImage_WithMinimalLayout_ReturnsPngBytes()
    {
        var layout = new SchematicLayout
        {
            IsEmpty = false,
            TotalPages = 1,
            Pages = new List<PageInfo>
            {
                new() { PageIndex = 0, BusX1 = 120, BusX2 = 800 }
            }
        };

        var image = PdfSingleLineDiagramService.RenderCircuitImage(layout, pageIndex: 0, _viewModel);

        Assert.NotNull(image);
        Assert.NotEmpty(image!);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ExportToPdf_WithEmptyViewModel_ShouldCreateFile()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test_empty.pdf");
        var options = new PdfExportOptions
        {
            ObjectName = "Test Object",
            Address = "Test Address",
            InvestorName = "Test Investor",
            DesignerName = "Test Designer"
        };

        // Act
        _pdfService.ExportToPdf(_viewModel, filePath, options);

        // Assert
        Assert.True(File.Exists(filePath));
        Assert.True(new FileInfo(filePath).Length > 0);
    }

    [Fact]
    public void ExportToPdf_WithInvalidPath_ShouldThrow()
    {
        // Arrange - użyj nieistniejącego dysku dla pewności że ścieżka jest nieprawidłowa
        var invalidPath = "Z:\\NonExistentDrive\\test.pdf";
        var options = new PdfExportOptions();

        // Act & Assert
        Assert.ThrowsAny<Exception>(() =>
        {
            _pdfService.ExportToPdf(_viewModel, invalidPath, options);
        });
    }

    [Fact]
    public void ExportToPng_WithInvalidPath_ShouldThrow()
    {
        // Arrange - użyj nieistniejącego dysku dla pewności że ścieżka jest nieprawidłowa
        var invalidPath = "Z:\\NonExistentDrive\\test.png";
        var options = new PdfExportOptions();

        // Act & Assert
        Assert.ThrowsAny<Exception>(() =>
        {
            _pdfService.ExportToPng(_viewModel, invalidPath, options);
        });
    }

    [Fact]
    public void ExportToPng_WithEmptyViewModel_MayNotCreateFile()
    {
        // Arrange - test dla przypadku gdy RenderDinRailToImage może zwrócić null
        var filePath = Path.Combine(_tempDir, "test_empty_png.png");
        var emptyViewModel = new MainViewModel();
        var options = new PdfExportOptions();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            _pdfService.ExportToPng(emptyViewModel, filePath, options));
    }

    #endregion

    #region Async Export Tests

    [Fact]
    public async Task ExportToPdfAsync_ShouldCreateFile()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test_async_export.pdf");
        var options = new PdfExportOptions
        {
            ObjectName = "Test Object",
            Address = "Test Address",
            InvestorName = "Test Investor",
            DesignerName = "Test Designer"
        };

        // Act
        await _pdfService.ExportToPdfAsync(_viewModel, filePath, options);

        // Assert
        Assert.True(File.Exists(filePath));
        Assert.True(new FileInfo(filePath).Length > 0);
    }

    [Fact]
    public async Task ExportToPdfAsync_WithProgress_ShouldReportProgress()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test_async_progress.pdf");
        var options = new PdfExportOptions();
        var progressValues = new List<int>();
        var progress = new Progress<int>(value => progressValues.Add(value));

        // Act
        await _pdfService.ExportToPdfAsync(_viewModel, filePath, options, progress);

        // Small delay to allow progress reports to be processed
        await Task.Delay(100);

        // Assert
        Assert.True(File.Exists(filePath));
        Assert.True(progressValues.Count > 0, "Progress should have been reported at least once");
        Assert.Contains(100, progressValues);
    }

    [Fact]
    public async Task ExportToPdfAsync_WithCancellation_ShouldThrow()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test_async_cancel.pdf");
        var options = new PdfExportOptions();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert - TaskCanceledException is a subclass of OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await _pdfService.ExportToPdfAsync(_viewModel, filePath, options, null, cts.Token);
        });
    }

    [Fact]
    public async Task ExportToPngAsync_ShouldCreateFile()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test_async_export.png");
        var options = new PdfExportOptions();

        // Add symbol to ensure image is generated
        _viewModel.Symbols.Add(new SymbolItem
        {
            Id = "test1",
            Type = "MCB",
            Label = "Test MCB",
            X = 100,
            Y = 100,
            Width = 200,
            Height = 1000
        });

        // Act
        await _pdfService.ExportToPngAsync(_viewModel, filePath, options);

        // Assert
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public async Task ExportToPngAsync_WithProgress_ShouldReportProgressAndReach100()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test_async_png_progress.png");
        var options = new PdfExportOptions();
        var progressValues = new List<int>();
        var progress = new Progress<int>(value => progressValues.Add(value));

        _viewModel.Symbols.Add(new SymbolItem
        {
            Id = "test-progress",
            Type = "MCB",
            Label = "Test MCB",
            X = 100,
            Y = 100,
            Width = 200,
            Height = 1000
        });

        // Act
        await _pdfService.ExportToPngAsync(_viewModel, filePath, options, progress);
        await Task.Delay(100);

        // Assert
        Assert.True(File.Exists(filePath));
        Assert.True(progressValues.Count > 0, "Progress should have been reported at least once");
        Assert.Contains(100, progressValues);
    }

    [Fact]
    public async Task ExportToPngAsync_WithCancellation_ShouldThrow()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test_async_png_cancel.png");
        var options = new PdfExportOptions();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert - TaskCanceledException is a subclass of OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await _pdfService.ExportToPngAsync(_viewModel, filePath, options, null, cts.Token);
        });
    }

    #endregion
}
