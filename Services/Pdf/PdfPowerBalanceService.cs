using System;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using DINBoard.Models;
using DINBoard.ViewModels;
using DINBoard.Services;

namespace DINBoard.Services.Pdf;

/// <summary>
/// Serwis odpowiedzialny za generowanie bilansu mocy
/// </summary>
public class PdfPowerBalanceService
{
    private static readonly string AccentBlue = "#3B82F6";
    private static readonly string AccentOrange = "#D97706";
    private static readonly string AccentRed = "#EF4444";
    private static readonly string TextGray = "#6B7280";
    private const float UiToPdfScale = 0.75f;
    private readonly IModuleTypeService _moduleTypeService;

    public PdfPowerBalanceService(IModuleTypeService moduleTypeService)
    {
        _moduleTypeService = moduleTypeService ?? throw new ArgumentNullException(nameof(moduleTypeService));
    }

    public void ComposePowerBalance(IContainer container, MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(viewModel);

        var allMcbs = viewModel.Symbols
            .Where(s => s != null && _moduleTypeService.IsMcb(s))
            .ToList();

        double totalPower = allMcbs.Sum(m => m.PowerW);
        double powerL1 = allMcbs.Where(m => m.Phase == "L1").Sum(m => m.PowerW);
        double powerL2 = allMcbs.Where(m => m.Phase == "L2").Sum(m => m.PowerW);
        double powerL3 = allMcbs.Where(m => m.Phase == "L3").Sum(m => m.PowerW);

        container.Column(column =>
        {
            column.Spacing(15);

            // Sumaryczna moc
            column.Item().Border(2).BorderColor(AccentBlue).Padding(20).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("MOC ZAINSTALOWANA").FontSize(12 * UiToPdfScale).SemiBold().FontColor(TextGray);
                    c.Item().Text($"{totalPower:N0} W").FontSize(32 * UiToPdfScale).Bold().FontColor(AccentBlue);
                    c.Item().Text($"({totalPower / 1000:N2} kW)").FontSize(14 * UiToPdfScale).FontColor(TextGray);
                });
            });

            // Rozkład na fazy
            column.Item().Text("Rozkład mocy na fazy:").FontSize(12 * UiToPdfScale).SemiBold();

            column.Item().Row(row =>
            {
                row.Spacing(10);

                row.RelativeItem().Border(1).BorderColor(AccentBlue).Padding(15).Column(c =>
                {
                    c.Item().AlignCenter().Text("L1").FontSize(14 * UiToPdfScale).Bold().FontColor(AccentBlue);
                    c.Item().AlignCenter().Text($"{powerL1:N0} W").FontSize(18 * UiToPdfScale).SemiBold();
                });

                row.RelativeItem().Border(1).BorderColor(AccentOrange).Padding(15).Column(c =>
                {
                    c.Item().AlignCenter().Text("L2").FontSize(14 * UiToPdfScale).Bold().FontColor(AccentOrange);
                    c.Item().AlignCenter().Text($"{powerL2:N0} W").FontSize(18 * UiToPdfScale).SemiBold();
                });

                row.RelativeItem().Border(1).BorderColor(AccentRed).Padding(15).Column(c =>
                {
                    c.Item().AlignCenter().Text("L3").FontSize(14 * UiToPdfScale).Bold().FontColor(AccentRed);
                    c.Item().AlignCenter().Text($"{powerL3:N0} W").FontSize(18 * UiToPdfScale).SemiBold();
                });
            });

            // Współczynnik jednoczesności
            column.Item().Height(20);
            column.Item().Text("Obliczenia:").FontSize(12 * UiToPdfScale).SemiBold();

            double simultaneityFactor = 0.6; // Typowy dla mieszkań
            double calculatedPower = totalPower * simultaneityFactor;

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2);
                    c.RelativeColumn(1);
                });

                table.Cell().Padding(5).Text("Współczynnik jednoczesności:").FontSize(10 * UiToPdfScale);
                table.Cell().Padding(5).Text($"{simultaneityFactor:P0}").FontSize(10 * UiToPdfScale).SemiBold();

                table.Cell().Padding(5).Text("Moc obliczeniowa:").FontSize(10 * UiToPdfScale);
                table.Cell().Padding(5).Text($"{calculatedPower:N0} W ({calculatedPower / 1000:N2} kW)").FontSize(10 * UiToPdfScale).SemiBold();

                table.Cell().Padding(5).Text("Prąd obliczeniowy (3x400V):").FontSize(10 * UiToPdfScale);
                table.Cell().Padding(5).Text($"{calculatedPower / (400 * 1.73):N1} A").FontSize(10 * UiToPdfScale).SemiBold();
            });
        });
    }
}
