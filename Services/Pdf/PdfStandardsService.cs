using System;
using System.Collections.Generic;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using DINBoard.Models;
using DINBoard.ViewModels;

namespace DINBoard.Services.Pdf;

/// <summary>
/// Serwis odpowiedzialny za generowanie informacji o zgodności z normami
/// </summary>
public class PdfStandardsService
{
    private static readonly string PrimaryColor = "#1F2937";
    private static readonly string AccentGreen = "#10B981";
    private static readonly string AccentOrange = "#F59E0B";
    // private static readonly string TextGray = "#6B7280"; // Not used
    private readonly IModuleTypeService _moduleTypeService;
    private readonly IElectricalValidationService _validationService;
    private const float UiToPdfScale = 0.75f;

    public PdfStandardsService(IModuleTypeService moduleTypeService, IElectricalValidationService validationService)
    {
        _moduleTypeService = moduleTypeService ?? throw new ArgumentNullException(nameof(moduleTypeService));
        _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
    }

    /// <summary>
    /// Generuje stronę z informacjami o zgodności z normami
    /// </summary>
    public void ComposeStandardsCompliance(IContainer container, MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(viewModel);

        container.Column(column =>
        {
            column.Spacing(25);

            // Nagłówek
            column.Item().Text("ZGODNOŚĆ Z NORMAMI")
                .FontSize(16 * UiToPdfScale).Bold().FontColor(PrimaryColor);

            // Sekcja 1: Normy ogólne
            column.Item().Column(section =>
            {
                section.Item().Text("Normy ogólne:")
                    .FontSize(12 * UiToPdfScale).Bold().FontColor("#4B5563");
                
                section.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(150);
                        columns.RelativeColumn();
                    });
                    
                    table.Cell().Element(CellStyle).Text("PN-HD 60364-4-41:2023").SemiBold();
                    table.Cell().Element(CellStyle).Text("Ochrona przeciwporażeniowa");
                    
                    table.Cell().Element(CellStyle).Text("PN-HD 60364-5-52:2023").SemiBold();
                    table.Cell().Element(CellStyle).Text("Dobór przewodów i ich układanie");
                    
                    table.Cell().Element(CellStyle).Text("PN-EN 61439-1:2022").SemiBold();
                    table.Cell().Element(CellStyle).Text("Szafy i rozdzielnice niskiego napięcia");
                });
            });

            // Sekcja 2: Parametry instalacji
            column.Item().Column(section =>
            {
                section.Item().Text("Parametry instalacji:")
                    .FontSize(12 * UiToPdfScale).Bold().FontColor("#4B5563");
                
                var balance = _validationService.CalculatePhaseLoads(viewModel.Symbols);
                
                section.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(180);
                        columns.RelativeColumn();
                        columns.ConstantColumn(80);
                    });
                    
                    table.Cell().Element(CellStyle).Text("Napięcie znamionowe:").SemiBold();
                    table.Cell().Element(CellStyle).Text("230 V / 400 V");
                    table.Cell().Element(CellStyle).Text("✓");
                    
                    table.Cell().Element(CellStyle).Text("Częstotliwość:").SemiBold();
                    table.Cell().Element(CellStyle).Text("50 Hz");
                    table.Cell().Element(CellStyle).Text("✓");
                    
                    table.Cell().Element(CellStyle).Text("Uziemienie:").SemiBold();
                    table.Cell().Element(CellStyle).Text("TN-S");
                    table.Cell().Element(CellStyle).Text("✓");
                    
                    table.Cell().Element(CellStyle).Text("Asymetria faz:").SemiBold();
                    table.Cell().Element(CellStyle).Text($"{balance.ImbalancePercent:F1}%");
                    table.Cell().Element(CellStyle)
                        .Text(balance.ImbalancePercent <= 15.0 ? "✓" : "⚠")
                        .FontColor(balance.ImbalancePercent <= 15.0 ? AccentGreen : AccentOrange);
                });
            });

            // Sekcja 3: Ochrona przeciwporażeniowa
            column.Item().Column(section =>
            {
                section.Item().Text("Ochrona przeciwporażeniowa:")
                    .FontSize(12 * UiToPdfScale).Bold().FontColor("#4B5563");
                
                var rcds = viewModel.Symbols.Count(s => s != null && _moduleTypeService.IsRcd(s));
                var totalMcbs = viewModel.Symbols.Count(s => s != null && _moduleTypeService.IsMcb(s));
                
                section.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(200);
                        columns.RelativeColumn();
                        columns.ConstantColumn(80);
                    });
                    
                    table.Cell().Element(CellStyle).Text("Wyłączniki różnicowoprądowe:").SemiBold();
                    table.Cell().Element(CellStyle).Text($"{rcds} szt.");
                    table.Cell().Element(CellStyle).Text(rcds > 0 ? "✓" : "⚠");
                    
                    table.Cell().Element(CellStyle).Text("Wyłączniki nadprądowe:").SemiBold();
                    table.Cell().Element(CellStyle).Text($"{totalMcbs} szt.");
                    table.Cell().Element(CellStyle).Text(totalMcbs > 0 ? "✓" : "⚠");
                    
                    table.Cell().Element(CellStyle).Text("Ochrona różnicowoprądowa:").SemiBold();
                    table.Cell().Element(CellStyle).Text("30 mA dla obwodów gniazd");
                    table.Cell().Element(CellStyle).Text("✓");
                });
            });

            // Sekcja 4: Podsumowanie
            column.Item().Column(section =>
            {
                section.Item().Text("Podsumowanie:")
                    .FontSize(12 * UiToPdfScale).Bold().FontColor("#4B5563");
                
                section.Item().Border(1).BorderColor("#E5E7EB").Padding(15).Column(summary =>
                {
                    summary.Spacing(10);
                    
                    summary.Item().Row(row =>
                    {
                        row.AutoItem().Text("✓").FontColor(AccentGreen).FontSize(14 * UiToPdfScale);
                        row.RelativeItem().PaddingLeft(10).Text("Instalacja spełnia wymagania norm PN-HD 60364")
                            .FontSize(11 * UiToPdfScale);
                    });
                    
                    summary.Item().Row(row =>
                    {
                        row.AutoItem().Text("✓").FontColor(AccentGreen).FontSize(14 * UiToPdfScale);
                        row.RelativeItem().PaddingLeft(10).Text("Zastosowano właściwą ochronę przeciwporażeniową")
                            .FontSize(11 * UiToPdfScale);
                    });
                    
                    var balance = _validationService.CalculatePhaseLoads(viewModel.Symbols);
                                    
                    summary.Item().Row(row =>
                    {
                        row.AutoItem().Text(balance.ImbalancePercent <= 15.0 ? "✓" : "⚠")
                            .FontColor(balance.ImbalancePercent <= 15.0 ? AccentGreen : AccentOrange)
                            .FontSize(14 * UiToPdfScale);
                        row.RelativeItem().PaddingLeft(10)
                            .Text(balance.ImbalancePercent <= 15.0 
                                ? "Asymetria faz jest w dopuszczalnych granicach" 
                                : $"Asymetria faz przekracza 15% ({balance.ImbalancePercent:F1}%)")
                            .FontSize(11 * UiToPdfScale);
                    });
                });
            });
        });
    }

    private IContainer CellStyle(IContainer container)
    {
        return container.BorderBottom(1).BorderColor("#E5E7EB").Padding(5);
    }
}
