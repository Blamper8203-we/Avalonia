using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DINBoard.Models;
using DINBoard.Services.Pdf;
using DINBoard.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DINBoard.Services;

/// <summary>
/// Serwis eksportu dokumentacji rozdzielnicy do PDF.
/// Generuje profesjonalny dokument zgodny z normami PN-HD 60364.
/// </summary>
public class PdfExportService
{
    private readonly IModuleTypeService _moduleTypeService;
    private readonly PdfTitlePageService _titlePageService;
    private readonly PdfDinRailService _dinRailService;
    private readonly PdfCircuitWiringService _circuitWiringService;
    private readonly PdfStandardsService _standardsService;
    private readonly PdfCircuitTableService _circuitTableService;
    private readonly PdfConnectionService _connectionService;
    private readonly PdfSingleLineDiagramService _singleLineDiagramService;
    private readonly PdfPowerBalanceService _powerBalanceService;

    public PdfExportService(
        IModuleTypeService moduleTypeService,
        IElectricalValidationService electricalValidationService,
        SymbolImportService symbolImportService,
        SvgProcessor svgProcessor)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        _moduleTypeService = moduleTypeService ?? throw new ArgumentNullException(nameof(moduleTypeService));
        var resolvedValidationService = electricalValidationService ?? throw new ArgumentNullException(nameof(electricalValidationService));
        var resolvedImportService = symbolImportService ?? throw new ArgumentNullException(nameof(symbolImportService));
        var resolvedSvgProcessor = svgProcessor ?? throw new ArgumentNullException(nameof(svgProcessor));
        _titlePageService = new PdfTitlePageService();
        _dinRailService = new PdfDinRailService(resolvedImportService, resolvedSvgProcessor);
        _circuitWiringService = new PdfCircuitWiringService(_moduleTypeService);
        _standardsService = new PdfStandardsService(_moduleTypeService, resolvedValidationService);
        _circuitTableService = new PdfCircuitTableService(_moduleTypeService);
        _powerBalanceService = new PdfPowerBalanceService(_moduleTypeService);
        _connectionService = new PdfConnectionService(_moduleTypeService);
        _singleLineDiagramService = new PdfSingleLineDiagramService(_moduleTypeService);
    }

    /// <summary>
    /// Eksportuje dokumentacje rozdzielnicy do pliku PDF.
    /// </summary>
    public void ExportToPdf(MainViewModel viewModel, string filePath, PdfExportOptions options)
    {
        var dinRailRenderCache = _dinRailService.CreateRenderCache();
        var singleLineRenderCache = _singleLineDiagramService.CreateRenderCache();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                _dinRailService.ConfigurePage(page);
                page.Content().Element(c => _titlePageService.ComposeTitlePage(c, viewModel, options));
            });

            container.Page(page =>
            {
                _dinRailService.ConfigurePage(page);
                page.Header().Element(c => _dinRailService.ComposeHeader(c, "Schemat Jednokreskowy"));
                page.Content().Element(c => _singleLineDiagramService.ComposeSingleLineDiagram(c, viewModel, singleLineRenderCache));
                page.Footer().Element(_dinRailService.ComposeFooter);
            });

            container.Page(page =>
            {
                _dinRailService.ConfigurePage(page);
                page.Header().Element(c => _dinRailService.ComposeHeader(c, "Schemat Rozdzielnicy"));
                page.Content().Element(c => _dinRailService.ComposeDinRailDiagram(c, viewModel, options, renderCache: dinRailRenderCache));
                page.Footer().Element(_dinRailService.ComposeFooter);
            });

            container.Page(page =>
            {
                _dinRailService.ConfigurePage(page);
                page.Header().Element(c => _dinRailService.ComposeHeader(c, "Schemat Rozdzielnicy (Z numerami i grupami)"));
                page.Content().Element(c => _dinRailService.ComposeDinRailDiagram(c, viewModel, options, showNumbers: true, showGroups: true, renderCache: dinRailRenderCache));
                page.Footer().Element(_dinRailService.ComposeFooter);
            });

            container.Page(page =>
            {
                _dinRailService.ConfigurePage(page);
                page.Header().Element(c => _dinRailService.ComposeHeader(c, "Schemat Rozdzielnicy (Grupy)"));
                page.Content().Element(c => _dinRailService.ComposeDinRailDiagram(c, viewModel, options, showNumbers: false, showGroups: true, renderCache: dinRailRenderCache));
                page.Footer().Element(_dinRailService.ComposeFooter);
            });

            container.Page(page =>
            {
                _dinRailService.ConfigurePage(page);
                page.Header().Element(c => _dinRailService.ComposeHeader(c, "Schemat Rozdzielnicy (Numery)"));
                page.Content().Element(c => _dinRailService.ComposeDinRailDiagram(c, viewModel, options, showNumbers: true, showGroups: false, renderCache: dinRailRenderCache));
                page.Footer().Element(_dinRailService.ComposeFooter);
            });

            container.Page(page =>
            {
                _dinRailService.ConfigurePage(page);
                page.Header().Element(c => _dinRailService.ComposeHeader(c, "Lista Obwodow"));
                page.Content().Element(c => _circuitTableService.ComposeCircuitTable(c, viewModel));
                page.Footer().Element(_dinRailService.ComposeFooter);
            });

            container.Page(page =>
            {
                _dinRailService.ConfigurePage(page);
                page.Header().Element(c => _dinRailService.ComposeHeader(c, "Bilans Mocy"));
                page.Content().Element(c => _powerBalanceService.ComposePowerBalance(c, viewModel));
                page.Footer().Element(_dinRailService.ComposeFooter);
            });

            container.Page(page =>
            {
                _dinRailService.ConfigurePage(page);
                page.Header().Element(c => _dinRailService.ComposeHeader(c, "Przylacze - Dobor Zabezpieczen i WLZ"));
                page.Content().Element(c => _connectionService.ComposeConnectionPage(c, viewModel, options));
                page.Footer().Element(_dinRailService.ComposeFooter);
            });

            container.Page(page =>
            {
                _dinRailService.ConfigurePage(page);
                page.Header().Element(c => _dinRailService.ComposeHeader(c, "Zgodnosc z Normami"));
                page.Content().Element(c => _standardsService.ComposeStandardsCompliance(c, viewModel));
                page.Footer().Element(_dinRailService.ComposeFooter);
            });
        });

        document.GeneratePdf(filePath);
    }

    /// <summary>
    /// Eksportuje widok szyny DIN do pliku PNG.
    /// </summary>
    public void ExportToPng(MainViewModel viewModel, string filePath, PdfExportOptions options)
    {
        var imageData = _dinRailService.RenderDinRailToImage(viewModel, options);
        if (imageData != null && imageData.Length > 0)
        {
            File.WriteAllBytes(filePath, imageData);
        }
        else
        {
            throw new InvalidOperationException("Nie udalo sie wyrenderowac schematu do PNG. Sprawdz czy projekt zawiera moduly.");
        }
    }

    /// <summary>
    /// Asynchronicznie eksportuje dokumentacje rozdzielnicy do pliku PDF.
    /// Wspiera anulowanie i raportowanie postepu.
    /// </summary>
    public async Task ExportToPdfAsync(
        MainViewModel viewModel,
        string filePath,
        PdfExportOptions options,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(10);
            var dinRailRenderCache = _dinRailService.CreateRenderCache();
            var singleLineRenderCache = _singleLineDiagramService.CreateRenderCache();

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    _dinRailService.ConfigurePage(page);
                    page.Content().Element(c => _titlePageService.ComposeTitlePage(c, viewModel, options));
                });

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(20);

                container.Page(page =>
                {
                    _dinRailService.ConfigurePage(page);
                    page.Header().Element(c => _dinRailService.ComposeHeader(c, "Schemat Jednokreskowy"));
                    page.Content().Element(c => _singleLineDiagramService.ComposeSingleLineDiagram(c, viewModel, singleLineRenderCache));
                    page.Footer().Element(_dinRailService.ComposeFooter);
                });

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(40);

                container.Page(page =>
                {
                    _dinRailService.ConfigurePage(page);
                    page.Header().Element(c => _dinRailService.ComposeHeader(c, "Schemat Rozdzielnicy"));
                    page.Content().Element(c => _dinRailService.ComposeDinRailDiagram(c, viewModel, options, renderCache: dinRailRenderCache));
                    page.Footer().Element(_dinRailService.ComposeFooter);
                });

                container.Page(page =>
                {
                    _dinRailService.ConfigurePage(page);
                    page.Header().Element(c => _dinRailService.ComposeHeader(c, "Schemat Rozdzielnicy (Z numerami i grupami)"));
                    page.Content().Element(c => _dinRailService.ComposeDinRailDiagram(c, viewModel, options, showNumbers: true, showGroups: true, renderCache: dinRailRenderCache));
                    page.Footer().Element(_dinRailService.ComposeFooter);
                });

                container.Page(page =>
                {
                    _dinRailService.ConfigurePage(page);
                    page.Header().Element(c => _dinRailService.ComposeHeader(c, "Schemat Rozdzielnicy (Grupy)"));
                    page.Content().Element(c => _dinRailService.ComposeDinRailDiagram(c, viewModel, options, showNumbers: false, showGroups: true, renderCache: dinRailRenderCache));
                    page.Footer().Element(_dinRailService.ComposeFooter);
                });

                container.Page(page =>
                {
                    _dinRailService.ConfigurePage(page);
                    page.Header().Element(c => _dinRailService.ComposeHeader(c, "Schemat Rozdzielnicy (Numery)"));
                    page.Content().Element(c => _dinRailService.ComposeDinRailDiagram(c, viewModel, options, showNumbers: true, showGroups: false, renderCache: dinRailRenderCache));
                    page.Footer().Element(_dinRailService.ComposeFooter);
                });

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(60);

                container.Page(page =>
                {
                    _dinRailService.ConfigurePage(page);
                    page.Header().Element(c => _dinRailService.ComposeHeader(c, "Lista Obwodow"));
                    page.Content().Element(c => _circuitTableService.ComposeCircuitTable(c, viewModel));
                    page.Footer().Element(_dinRailService.ComposeFooter);
                });

                progress?.Report(80);

                container.Page(page =>
                {
                    _dinRailService.ConfigurePage(page);
                    page.Header().Element(c => _dinRailService.ComposeHeader(c, "Bilans Mocy"));
                    page.Content().Element(c => _powerBalanceService.ComposePowerBalance(c, viewModel));
                    page.Footer().Element(_dinRailService.ComposeFooter);
                });

                container.Page(page =>
                {
                    _dinRailService.ConfigurePage(page);
                    page.Header().Element(c => _dinRailService.ComposeHeader(c, "Przylacze - Dobor Zabezpieczen i WLZ"));
                    page.Content().Element(c => _connectionService.ComposeConnectionPage(c, viewModel, options));
                    page.Footer().Element(_dinRailService.ComposeFooter);
                });

                container.Page(page =>
                {
                    _dinRailService.ConfigurePage(page);
                    page.Header().Element(c => _dinRailService.ComposeHeader(c, "Zgodnosc z Normami"));
                    page.Content().Element(c => _standardsService.ComposeStandardsCompliance(c, viewModel));
                    page.Footer().Element(_dinRailService.ComposeFooter);
                });
            });

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(90);

            document.GeneratePdf(filePath);
            progress?.Report(100);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Generuje podglad dokumentacji PDF jako obrazy stron bez zapisu pliku.
    /// </summary>
    public async Task<IReadOnlyList<byte[]>> GeneratePdfPreviewImagesAsync(
        MainViewModel viewModel,
        PdfExportOptions options,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(options);

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(10);

            var dinRailRenderCache = _dinRailService.CreateRenderCache();
            var singleLineRenderCache = _singleLineDiagramService.CreateRenderCache();

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    _dinRailService.ConfigurePage(page);
                    page.Content().Element(c => _titlePageService.ComposeTitlePage(c, viewModel, options));
                });

                container.Page(page =>
                {
                    _dinRailService.ConfigurePage(page);
                    page.Header().Element(c => _dinRailService.ComposeHeader(c, "Schemat Jednokreskowy"));
                    page.Content().Element(c => _singleLineDiagramService.ComposeSingleLineDiagram(c, viewModel, singleLineRenderCache));
                    page.Footer().Element(_dinRailService.ComposeFooter);
                });

                container.Page(page =>
                {
                    _dinRailService.ConfigurePage(page);
                    page.Header().Element(c => _dinRailService.ComposeHeader(c, "Schemat Rozdzielnicy"));
                    page.Content().Element(c => _dinRailService.ComposeDinRailDiagram(c, viewModel, options, renderCache: dinRailRenderCache));
                    page.Footer().Element(_dinRailService.ComposeFooter);
                });

                container.Page(page =>
                {
                    _dinRailService.ConfigurePage(page);
                    page.Header().Element(c => _dinRailService.ComposeHeader(c, "Schemat Rozdzielnicy (Z numerami i grupami)"));
                    page.Content().Element(c => _dinRailService.ComposeDinRailDiagram(c, viewModel, options, showNumbers: true, showGroups: true, renderCache: dinRailRenderCache));
                    page.Footer().Element(_dinRailService.ComposeFooter);
                });

                container.Page(page =>
                {
                    _dinRailService.ConfigurePage(page);
                    page.Header().Element(c => _dinRailService.ComposeHeader(c, "Schemat Rozdzielnicy (Grupy)"));
                    page.Content().Element(c => _dinRailService.ComposeDinRailDiagram(c, viewModel, options, showNumbers: false, showGroups: true, renderCache: dinRailRenderCache));
                    page.Footer().Element(_dinRailService.ComposeFooter);
                });

                container.Page(page =>
                {
                    _dinRailService.ConfigurePage(page);
                    page.Header().Element(c => _dinRailService.ComposeHeader(c, "Schemat Rozdzielnicy (Numery)"));
                    page.Content().Element(c => _dinRailService.ComposeDinRailDiagram(c, viewModel, options, showNumbers: true, showGroups: false, renderCache: dinRailRenderCache));
                    page.Footer().Element(_dinRailService.ComposeFooter);
                });

                container.Page(page =>
                {
                    _dinRailService.ConfigurePage(page);
                    page.Header().Element(c => _dinRailService.ComposeHeader(c, "Lista Obwodow"));
                    page.Content().Element(c => _circuitTableService.ComposeCircuitTable(c, viewModel));
                    page.Footer().Element(_dinRailService.ComposeFooter);
                });

                container.Page(page =>
                {
                    _dinRailService.ConfigurePage(page);
                    page.Header().Element(c => _dinRailService.ComposeHeader(c, "Bilans Mocy"));
                    page.Content().Element(c => _powerBalanceService.ComposePowerBalance(c, viewModel));
                    page.Footer().Element(_dinRailService.ComposeFooter);
                });

                container.Page(page =>
                {
                    _dinRailService.ConfigurePage(page);
                    page.Header().Element(c => _dinRailService.ComposeHeader(c, "Przylacze - Dobor Zabezpieczen i WLZ"));
                    page.Content().Element(c => _connectionService.ComposeConnectionPage(c, viewModel, options));
                    page.Footer().Element(_dinRailService.ComposeFooter);
                });

                container.Page(page =>
                {
                    _dinRailService.ConfigurePage(page);
                    page.Header().Element(c => _dinRailService.ComposeHeader(c, "Zgodnosc z Normami"));
                    page.Content().Element(c => _standardsService.ComposeStandardsCompliance(c, viewModel));
                    page.Footer().Element(_dinRailService.ComposeFooter);
                });
            });

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(70);

            var generatedImages = document.GenerateImages(new ImageGenerationSettings
            {
                RasterDpi = 144
            });

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(100);
            return (IReadOnlyList<byte[]>)generatedImages.ToList();
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronicznie eksportuje widok szyny DIN do pliku PNG.
    /// Wspiera anulowanie i raportowanie postepu.
    /// </summary>
    public async Task ExportToPngAsync(
        MainViewModel viewModel,
        string filePath,
        PdfExportOptions options,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(30);

            var imageData = _dinRailService.RenderDinRailToImage(viewModel, options);

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(70);

            if (imageData != null && imageData.Length > 0)
            {
                File.WriteAllBytes(filePath, imageData);
            }
            else
            {
                throw new InvalidOperationException("Nie udalo sie wyrenderowac schematu do PNG. Sprawdz czy projekt zawiera moduly.");
            }

            progress?.Report(100);
        }, cancellationToken).ConfigureAwait(false);
    }
}
