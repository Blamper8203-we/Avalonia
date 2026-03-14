using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using DINBoard.Models;
using DINBoard.ViewModels;
using DINBoard.Services.Pdf;

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
        _connectionService = new PdfConnectionService();
        _singleLineDiagramService = new PdfSingleLineDiagramService(_moduleTypeService);
    }

    /// <summary>
    /// Eksportuje dokumentację rozdzielnicy do pliku PDF.
    /// </summary>
    public void ExportToPdf(MainViewModel viewModel, string filePath, PdfExportOptions options)
    {
        var document = Document.Create(container =>
        {
            // Strona 1: Tytułowa
            container.Page(page =>
            {
                _dinRailService.ConfigurePage(page);
                page.Content().Element(c => _titlePageService.ComposeTitlePage(c, viewModel, options));
            });

            // Strona 2: Schemat jednokreskowy (PN-EN 60617 / IEC 61082)
            container.Page(page =>
            {
                _dinRailService.ConfigurePage(page);
                page.Header().Element(c => _dinRailService.ComposeHeader(c, "Schemat Jednokreskowy"));
                page.Content().Element(c => _singleLineDiagramService.ComposeSingleLineDiagram(c, viewModel));
                page.Footer().Element(_dinRailService.ComposeFooter);
            });

            // Strona 3: Schemat szyny DIN (1:1 z canvasem)
            container.Page(page =>
            {
                _dinRailService.ConfigurePage(page);
                page.Header().Element(c => _dinRailService.ComposeHeader(c, "Schemat Rozdzielnicy"));
                page.Content().Element(c => _dinRailService.ComposeDinRailDiagram(c, viewModel, options));
                page.Footer().Element(_dinRailService.ComposeFooter);
            });

            // Strona 3: Lista obwodów
            container.Page(page =>
            {
                _dinRailService.ConfigurePage(page);
                page.Header().Element(c => _dinRailService.ComposeHeader(c, "Lista Obwodów"));
                page.Content().Element(c => _circuitTableService.ComposeCircuitTable(c, viewModel));
                page.Footer().Element(_dinRailService.ComposeFooter);
            });

            // Strona 4: Bilans mocy
            container.Page(page =>
            {
                _dinRailService.ConfigurePage(page);
                page.Header().Element(c => _dinRailService.ComposeHeader(c, "Bilans Mocy"));
                page.Content().Element(c => _powerBalanceService.ComposePowerBalance(c, viewModel));
                page.Footer().Element(_dinRailService.ComposeFooter);
            });

            // Strona 5: Przyłącze
            container.Page(page =>
            {
                _dinRailService.ConfigurePage(page);
                page.Header().Element(c => _dinRailService.ComposeHeader(c, "Przyłącze — Dobór Zabezpieczeń i WLZ"));
                page.Content().Element(c => _connectionService.ComposeConnectionPage(c, viewModel, options));
                page.Footer().Element(_dinRailService.ComposeFooter);
            });

            // Strona 6: Normy i zgodność
            container.Page(page =>
            {
                _dinRailService.ConfigurePage(page);
                page.Header().Element(c => _dinRailService.ComposeHeader(c, "Zgodność z Normami"));
                page.Content().Element(c => _standardsService.ComposeStandardsCompliance(c, viewModel));
                page.Footer().Element(_dinRailService.ComposeFooter);
            });
        });

        document.GeneratePdf(filePath);
    }

    /// <summary>
    /// Eksportuje widok szyny DIN do pliku PNG (ultra jakość).
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
            throw new InvalidOperationException("Nie udało się wyrenderować schematu do PNG. Sprawdź czy projekt zawiera moduły.");
        }
    }

    /// <summary>
    /// Asynchronicznie eksportuje dokumentację rozdzielnicy do pliku PDF.
    /// Wspiera anulowanie i raportowanie postępu.
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

            var document = Document.Create(container =>
            {
                // Strona 1: Tytułowa
                container.Page(page =>
                {
                    _dinRailService.ConfigurePage(page);
                    page.Content().Element(c => _titlePageService.ComposeTitlePage(c, viewModel, options));
                });

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(20);

                // Strona 2: Schemat jednokreskowy (PN-EN 60617 / IEC 61082)
                container.Page(page =>
                {
                    _dinRailService.ConfigurePage(page);
                    page.Header().Element(c => _dinRailService.ComposeHeader(c, "Schemat Jednokreskowy"));
                    page.Content().Element(c => _singleLineDiagramService.ComposeSingleLineDiagram(c, viewModel));
                    page.Footer().Element(_dinRailService.ComposeFooter);
                });

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(40);

                // Strona 3: Schemat szyny DIN (1:1 z canvasem)
                container.Page(page =>
                {
                    _dinRailService.ConfigurePage(page);
                    page.Header().Element(c => _dinRailService.ComposeHeader(c, "Schemat Rozdzielnicy"));
                    page.Content().Element(c => _dinRailService.ComposeDinRailDiagram(c, viewModel, options));
                    page.Footer().Element(_dinRailService.ComposeFooter);
                });

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(60);

                // Strona 3: Lista obwodów
                container.Page(page =>
                {
                    _dinRailService.ConfigurePage(page);
                    page.Header().Element(c => _dinRailService.ComposeHeader(c, "Lista Obwodów"));
                    page.Content().Element(c => _circuitTableService.ComposeCircuitTable(c, viewModel));
                    page.Footer().Element(_dinRailService.ComposeFooter);
                });

                progress?.Report(80);

                // Strona 4: Bilans mocy
                container.Page(page =>
                {
                    _dinRailService.ConfigurePage(page);
                    page.Header().Element(c => _dinRailService.ComposeHeader(c, "Bilans Mocy"));
                    page.Content().Element(c => _powerBalanceService.ComposePowerBalance(c, viewModel));
                    page.Footer().Element(_dinRailService.ComposeFooter);
                });

                // Strona 5: Przyłącze
                container.Page(page =>
                {
                    _dinRailService.ConfigurePage(page);
                    page.Header().Element(c => _dinRailService.ComposeHeader(c, "Przyłącze — Dobór Zabezpieczeń i WLZ"));
                    page.Content().Element(c => _connectionService.ComposeConnectionPage(c, viewModel, options));
                    page.Footer().Element(_dinRailService.ComposeFooter);
                });

                // Strona 6: Normy
                container.Page(page =>
                {
                    _dinRailService.ConfigurePage(page);
                    page.Header().Element(c => _dinRailService.ComposeHeader(c, "Zgodność z Normami"));
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
    /// Asynchronicznie eksportuje widok szyny DIN do pliku PNG.
    /// Wspiera anulowanie i raportowanie postępu.
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
                throw new InvalidOperationException("Nie udało się wyrenderować schematu do PNG. Sprawdź czy projekt zawiera moduły.");
            }
            
            progress?.Report(100);
        }, cancellationToken).ConfigureAwait(false);
    }
}
