using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DINBoard.Models;
using DINBoard.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DINBoard.Services.Pdf;

/// <summary>
/// Serwis odpowiedzialny za generowanie strony tytulowej dokumentacji.
/// </summary>
public class PdfTitlePageService
{
    private static readonly string BackgroundColor = "#F3F6FB";
    private static readonly string HeroColor = "#0F172A";
    private static readonly string PrimaryColor = "#1E293B";
    private static readonly string AccentColor = "#0EA5E9";
    private static readonly string AccentSoftColor = "#BAE6FD";
    private static readonly string CardBorderColor = "#D9E2F0";
    private static readonly string LabelColor = "#64748B";
    private static readonly string TextColor = "#0F172A";
    private static readonly string MutedTextColor = "#475569";
    private static readonly string BadgeBackgroundColor = "#F8FAFF";
    private static readonly string BadgeBorderColor = "#D7E4F6";
    private static readonly string PhaseL1Color = "#8B5A2B";
    private static readonly string PhaseL2Color = "#111827";
    private static readonly string PhaseL3Color = "#6B7280";
    private static readonly string PhaseNColor = "#2563EB";
    private static readonly string PhasePeColor = "#16A34A";
    private const float UiToPdfScale = 0.75f;

    private static readonly IReadOnlyList<string> DefaultStandards = new[]
    {
        "IEC 61082 - Dokumentacja elektrotechniczna",
        "PN-EN 60617 - Symbole graficzne",
        "IEC 61346 - Oznaczenia referencyjne",
        "PN-HD 60364 - Instalacje elektryczne"
    };

    public void ComposeTitlePage(IContainer container, MainViewModel viewModel, PdfExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(options);

        var meta = viewModel.CurrentProject?.Metadata ?? new ProjectMetadata();
        var now = DateTime.Now;

        var projectName = PickFirst(options.ObjectName, viewModel.CurrentProject?.Name, "Rozdzielnica elektryczna");
        var companyName = PickFirst(meta.Contractor, meta.Company, "DINBoard");
        var projectNumber = PickFirst(meta.ProjectNumber, options.ProjectNumber, "-");
        var revision = PickFirst(meta.Revision, options.Revision, "1.0");
        var address = PickFirst(meta.Address, options.Address, "-");
        var investor = PickFirst(meta.Investor, options.InvestorName, "-");
        var designer = PickFirst(meta.Author, options.DesignerName, "DINBoard");
        var designerLicense = PickFirst(meta.DesignerId, meta.AuthorLicense, options.DesignerLicense, "-");
        var contractor = PickFirst(meta.Contractor, meta.Company, "-");
        var standards = ResolveStandards(meta);

        var modulesCount = viewModel.Symbols.Count;
        var circuitsCount = viewModel.CurrentProject?.Circuits?.Count ?? 0;
        var phases = viewModel.CurrentProject?.PowerConfig?.Phases ?? 3;
        var voltage = viewModel.CurrentProject?.PowerConfig?.Voltage ?? 400;
        var totalPowerKw = Math.Round(viewModel.Symbols.Sum(symbol => symbol.PowerW) / 1000.0, 2);

        container.Layers(layers =>
        {
            layers.Layer().Background(BackgroundColor);
            layers.Layer().AlignTop().Height(210).Background(HeroColor);
            layers.Layer().AlignTop().PaddingTop(210).Height(4).Background(AccentColor);

            layers.PrimaryLayer().Padding(26).Column(column =>
            {
                column.Spacing(16);

                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(hero =>
                    {
                        hero.Item().Text("DINBOARD").FontSize(11 * UiToPdfScale).SemiBold().FontColor(AccentSoftColor);
                        hero.Item().Text("DOKUMENTACJA TECHNICZNA")
                            .FontSize(32 * UiToPdfScale).Bold().FontColor(Colors.White);
                        hero.Item().Text(projectName)
                            .FontSize(21 * UiToPdfScale).SemiBold().FontColor(AccentSoftColor);
                        hero.Item().PaddingTop(5).Text($"Jednostka: {companyName}")
                            .FontSize(11 * UiToPdfScale).FontColor("#CBD5E1");
                    });

                    row.ConstantItem(160).AlignMiddle().Border(1).BorderColor("#334155").Background("#111C30").Padding(12).Column(side =>
                    {
                        side.Spacing(6);
                        side.Item().Text("NUMER PROJEKTU").FontSize(8 * UiToPdfScale).FontColor("#94A3B8");
                        side.Item().Text(projectNumber).FontSize(13 * UiToPdfScale).SemiBold().FontColor(Colors.White);
                        side.Item().LineHorizontal(1).LineColor("#334155");
                        side.Item().Text("REWIZJA").FontSize(8 * UiToPdfScale).FontColor("#94A3B8");
                        side.Item().Text(revision).FontSize(12 * UiToPdfScale).SemiBold().FontColor(Colors.White);
                        side.Item().LineHorizontal(1).LineColor("#334155");
                        side.Item().Text("DATA").FontSize(8 * UiToPdfScale).FontColor("#94A3B8");
                        side.Item().Text(FormatDate(now)).FontSize(11 * UiToPdfScale).FontColor("#E2E8F0");
                    });
                });

                column.Item().Column(electricalStyle =>
                {
                    electricalStyle.Spacing(8);

                    electricalStyle.Item().Row(row =>
                    {
                        row.Spacing(6);
                        row.RelativeItem().Element(c => ComposeConductorBadge(c, "L1", PhaseL1Color));
                        row.RelativeItem().Element(c => ComposeConductorBadge(c, "L2", PhaseL2Color));
                        row.RelativeItem().Element(c => ComposeConductorBadge(c, "L3", PhaseL3Color));
                        row.RelativeItem().Element(c => ComposeConductorBadge(c, "N", PhaseNColor));
                        row.RelativeItem().Element(c => ComposeConductorBadge(c, "PE", PhasePeColor));
                    });

                    electricalStyle.Item().Background("#0B1324").Border(1).BorderColor("#22324A").Padding(10).Column(diagram =>
                    {
                        diagram.Spacing(5);
                        diagram.Item().Text("MOTYW JEDNOKRESKOWY").FontSize(8 * UiToPdfScale).FontColor("#94A3B8");
                        diagram.Item().Row(row =>
                        {
                            row.Spacing(4);
                            row.ConstantItem(90).Element(c => ComposeElectricalNode(c, "Zasilanie", "#22C55E"));
                            row.RelativeItem().PaddingTop(11).LineHorizontal(2).LineColor("#334155");
                            row.ConstantItem(90).Element(c => ComposeElectricalNode(c, "RCD", "#38BDF8"));
                            row.RelativeItem().PaddingTop(11).LineHorizontal(2).LineColor("#334155");
                            row.ConstantItem(90).Element(c => ComposeElectricalNode(c, "MCB", "#F59E0B"));
                            row.RelativeItem().PaddingTop(11).LineHorizontal(2).LineColor("#334155");
                            row.ConstantItem(90).Element(c => ComposeElectricalNode(c, "Obwody", "#A3E635"));
                        });
                    });
                });

                column.Item().Background(Colors.White).Border(1).BorderColor(CardBorderColor).Padding(16).Column(content =>
                {
                    content.Spacing(12);

                    content.Item().Text("KARTA PROJEKTU").FontSize(12 * UiToPdfScale).Bold().FontColor(PrimaryColor);

                    content.Item().Row(row =>
                    {
                        row.Spacing(12);
                        row.RelativeItem().Column(col =>
                        {
                            col.Spacing(8);
                            ComposeInfoCell(col, "Adres inwestycji", address);
                            ComposeInfoCell(col, "Inwestor", investor);
                            ComposeInfoCell(col, "Wykonawca", contractor);
                        });

                        row.RelativeItem().Column(col =>
                        {
                            col.Spacing(8);
                            ComposeInfoCell(col, "Projektant", designer);
                            ComposeInfoCell(col, "Uprawnienia", designerLicense);
                            ComposeInfoCell(col, "Data utworzenia", FormatDate(meta.DateCreated));
                        });
                    });

                    content.Item().LineHorizontal(1).LineColor(CardBorderColor);

                    content.Item().Row(row =>
                    {
                        row.Spacing(10);
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Moduly", $"{modulesCount} szt."));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Obwody", $"{circuitsCount} szt."));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Zasilanie", $"{phases}F / {voltage}V"));
                        row.RelativeItem().Element(c => ComposeMetricCard(c, "Moc", $"{totalPowerKw:0.##} kW"));
                    });
                });

                column.Item().Background(Colors.White).Border(1).BorderColor(CardBorderColor).Padding(14).Column(standardSection =>
                {
                    standardSection.Spacing(10);
                    standardSection.Item().Text("Normy i zgodnosc elektryczna").FontSize(12 * UiToPdfScale).Bold().FontColor(PrimaryColor);

                    var leftStandards = standards.Where((_, index) => index % 2 == 0).ToList();
                    var rightStandards = standards.Where((_, index) => index % 2 == 1).ToList();

                    standardSection.Item().Row(row =>
                    {
                        row.Spacing(10);
                        row.RelativeItem().Column(col =>
                        {
                            col.Spacing(6);
                            foreach (var standard in leftStandards)
                            {
                                col.Item().Element(c => ComposeStandardChip(c, standard));
                            }
                        });

                        row.RelativeItem().Column(col =>
                        {
                            col.Spacing(6);
                            foreach (var standard in rightStandards)
                            {
                                col.Item().Element(c => ComposeStandardChip(c, standard));
                            }
                        });
                    });
                });

                column.Item().AlignCenter().PaddingTop(6).Text($"Wygenerowano: {now.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture)}")
                    .FontSize(9 * UiToPdfScale).FontColor(MutedTextColor);
            });
        });
    }

    private void ComposeInfoCell(ColumnDescriptor column, string label, string value)
    {
        column.Item().Text(label).FontSize(8 * UiToPdfScale).FontColor(LabelColor);
        column.Item().Text(value).FontSize(10 * UiToPdfScale).SemiBold().FontColor(TextColor);
    }

    private void ComposeMetricCard(IContainer container, string label, string value)
    {
        container.Border(1).BorderColor(BadgeBorderColor).Background(BadgeBackgroundColor).Padding(8).Column(column =>
        {
            column.Spacing(3);
            column.Item().Text(label.ToUpperInvariant()).FontSize(7 * UiToPdfScale).FontColor(LabelColor);
            column.Item().Text(value).FontSize(11 * UiToPdfScale).SemiBold().FontColor(TextColor);
        });
    }

    private void ComposeConductorBadge(IContainer container, string label, string color)
    {
        container.Border(1).BorderColor(BadgeBorderColor).Background(Colors.White).Padding(6).Row(row =>
        {
            row.Spacing(6);
            row.ConstantItem(10).AlignMiddle().Height(10).Background(color);
            row.RelativeItem().AlignMiddle().Text(label).FontSize(9 * UiToPdfScale).SemiBold().FontColor(TextColor);
        });
    }

    private void ComposeElectricalNode(IContainer container, string label, string accentColor)
    {
        container.Border(1).BorderColor("#334155").Background("#111C30").Padding(6).Column(column =>
        {
            column.Spacing(3);
            column.Item().Height(4).Background(accentColor);
            column.Item().AlignCenter().Text(label.ToUpperInvariant()).FontSize(7 * UiToPdfScale).SemiBold().FontColor("#E2E8F0");
        });
    }

    private void ComposeStandardChip(IContainer container, string text)
    {
        container.Border(1).BorderColor(BadgeBorderColor).Background(BadgeBackgroundColor).Padding(7)
            .Text(text).FontSize(9 * UiToPdfScale).FontColor(MutedTextColor);
    }

    private static IReadOnlyList<string> ResolveStandards(ProjectMetadata meta)
    {
        var resolved = (meta.Standards ?? new List<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(6)
            .ToList();

        return resolved.Count > 0 ? resolved : DefaultStandards;
    }

    private static string PickFirst(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return "-";
    }

    private static string FormatDate(DateTime value)
    {
        if (value == default)
        {
            value = DateTime.Now;
        }

        if (value.Kind == DateTimeKind.Utc)
        {
            value = value.ToLocalTime();
        }

        return value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
    }
}
