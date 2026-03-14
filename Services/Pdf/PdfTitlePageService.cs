using System;
using System.Collections.Generic;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;
using DINBoard.Models;
using DINBoard.ViewModels;
using DINBoard.Constants;

namespace DINBoard.Services.Pdf;

/// <summary>
/// Serwis odpowiedzialny za generowanie strony tytułowej dokumentacji
/// </summary>
public class PdfTitlePageService
{
    private static readonly string PrimaryColor = "#1F2937";
    private static readonly string AccentBlue = "#3B82F6";
    private static readonly string TextGray = "#6B7280";
    private const float UiToPdfScale = 0.75f;

    public void ComposeTitlePage(IContainer container, MainViewModel viewModel, PdfExportOptions options)
    {
        var meta = viewModel.CurrentProject?.Metadata ?? new ProjectMetadata();
        container.Column(column =>
        {
            column.Spacing(20);

            // Logo / Nagłówek
            column.Item().Height(100).AlignCenter().AlignMiddle()
                .Text("⚡").FontSize(60 * UiToPdfScale);

            column.Item().Height(30);

            // Tytuł główny
            column.Item().AlignCenter()
                .Text("DOKUMENTACJA TECHNICZNA")
                .FontSize(28 * UiToPdfScale).Bold().FontColor(PrimaryColor);

            column.Item().AlignCenter()
                .Text("ROZDZIELNICA ELEKTRYCZNA")
                .FontSize(22 * UiToPdfScale).SemiBold().FontColor(AccentBlue);

            column.Item().Height(30);

            // Blok tytułowy zgodny z normami
            column.Item().Border(2).BorderColor(PrimaryColor).Column(titleBlock =>
            {
                // Nagłówek bloku
                titleBlock.Item().Background(PrimaryColor).Padding(10)
                    .Text("BLOK TYTUŁOWY / TITLE BLOCK")
                    .FontSize(14 * UiToPdfScale).Bold().FontColor(Colors.White);

                // Główne dane projektu
                titleBlock.Item().Padding(15).Column(info =>
                {
                    info.Spacing(10);

                    // Rząd 1: Nazwa projektu i numer
                    info.Item().Row(row =>
                    {
                        row.RelativeItem(3).Column(col =>
                        {
                            col.Item().Text("NAZWA PROJEKTU / PROJECT NAME").FontSize(8 * UiToPdfScale).FontColor(TextGray);
                            col.Item().Text(meta.Company ?? "Rozdzielnica").FontSize(12 * UiToPdfScale).Bold();
                        });
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("NR PROJEKTU / PROJECT NO.").FontSize(8 * UiToPdfScale).FontColor(TextGray);
                            col.Item().Text(meta.ProjectNumber ?? "-").FontSize(12 * UiToPdfScale).Bold();
                        });
                    });

                    info.Item().LineHorizontal(1).LineColor(TextGray);

                    // Rząd 2: Lokalizacja i inwestor
                    info.Item().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("ADRES / ADDRESS").FontSize(8 * UiToPdfScale).FontColor(TextGray);
                            col.Item().Text(meta.Address ?? "-").FontSize(10 * UiToPdfScale);
                        });
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("INWESTOR / CLIENT").FontSize(8 * UiToPdfScale).FontColor(TextGray);
                            col.Item().Text(meta.Investor ?? "-").FontSize(10 * UiToPdfScale);
                        });
                    });

                    info.Item().LineHorizontal(1).LineColor(TextGray);

                    // Rząd 3: Projektant i uprawnienia
                    info.Item().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("PROJEKTANT / DESIGNER").FontSize(8 * UiToPdfScale).FontColor(TextGray);
                            col.Item().Text(meta.Author ?? "DINBoard").FontSize(10 * UiToPdfScale);
                        });
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("UPRAWNIENIA / LICENSE").FontSize(8 * UiToPdfScale).FontColor(TextGray);
                            col.Item().Text(meta.DesignerId ?? "-").FontSize(10 * UiToPdfScale);
                        });
                    });

                    info.Item().LineHorizontal(1).LineColor(TextGray);

                    // Rząd 4: Daty i rewizja
                    info.Item().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("DATA UTWORZENIA / DATE").FontSize(8 * UiToPdfScale).FontColor(TextGray);
                            col.Item().Text(DateTime.Now.ToString("dd.MM.yyyy")).FontSize(10 * UiToPdfScale);
                        });
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("REWIZJA / REVISION").FontSize(8 * UiToPdfScale).FontColor(TextGray);
                            col.Item().Text(meta.Revision ?? "1.0").FontSize(10 * UiToPdfScale);
                        });
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("SKALA / SCALE").FontSize(8 * UiToPdfScale).FontColor(TextGray);
                            col.Item().Text("1:1").FontSize(10 * UiToPdfScale);
                        });
                    });
                });
            });

            column.Item().Height(20);

            // Normy i zgodność
            column.Item().Border(1).BorderColor(TextGray).Padding(15).Column(standards =>
            {
                standards.Item().Text("ZGODNOŚĆ Z NORMAMI / STANDARDS COMPLIANCE")
                    .FontSize(12 * UiToPdfScale).Bold().FontColor(PrimaryColor);

                standards.Item().Height(8);

                standards.Item().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("• IEC 61082 - Dokumentacja elektrotechniczna").FontSize(9 * UiToPdfScale);
                        col.Item().Text("• PN-EN 60617 - Symbole graficzne").FontSize(9 * UiToPdfScale);
                    });
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("• IEC 61346 - Oznaczenia referencyjne").FontSize(9 * UiToPdfScale);
                        col.Item().Text("• PN-HD 60364 - Instalacje elektryczne").FontSize(9 * UiToPdfScale);
                    });
                });
            });

            column.Item().Height(20);

            // Stopka z datą
            column.Item().AlignCenter()
                .Text($"Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm}")
                .FontSize(10 * UiToPdfScale).FontColor(TextGray);
        });
    }
}
