using System;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using DINBoard.ViewModels;

namespace DINBoard.Services.Pdf;

/// <summary>
/// Serwis generujący stronę przyłącza w dokumentacji PDF.
/// Zawiera: schemat przyłącza, dobór zabezpieczenia głównego, WLZ, parametry przyłącza.
/// </summary>
public class PdfConnectionService
{
    private static readonly string PrimaryColor = "#1F2937";
    private static readonly string AccentBlue = "#3B82F6";
    private static readonly string AccentGreen = "#10B981";
    private static readonly string AccentOrange = "#D97706";
    private static readonly string TextGray = "#6B7280";
    private static readonly string LightBg = "#F3F4F6";
    private const float S = 0.75f; // UiToPdfScale

    private static readonly ModuleTypeService _moduleTypeService = new();

    // Standardowe prądy znamionowe wyłączników [A]
    private static readonly int[] StandardBreakers = { 16, 20, 25, 32, 40, 50, 63, 80, 100, 125 };

    // Dobór WLZ: prąd znamionowy wyłącznika -> przekrój Cu [mm²]
    // Kabel ziemny YKY wg PN-HD 60364-5-52, metoda D1
    private static (double CrossSection, string Description) GetWlzCable(int breakerAmps) => breakerAmps switch
    {
        <= 25 => (4.0, "YKY 5x4 mm²"),
        <= 32 => (6.0, "YKY 5x6 mm²"),
        <= 40 => (10.0, "YKY 5x10 mm²"),
        <= 63 => (16.0, "YKY 5x16 mm²"),
        <= 80 => (25.0, "YKY 5x25 mm²"),
        <= 100 => (35.0, "YKY 5x35 mm²"),
        _ => (50.0, "YKY 5x50 mm²")
    };

    // Dobór zabezpieczenia przedlicznikowego
    private static string GetPreMeterBreaker(int mainBreakerAmps) => mainBreakerAmps switch
    {
        <= 25 => "SBI 25A gG",
        <= 32 => "SBI 32A gG",
        <= 40 => "SBI 40A gG",
        <= 50 => "SBI 50A gG",
        <= 63 => "SBI 63A gG",
        _ => $"SBI {mainBreakerAmps}A gG"
    };

    public void ComposeConnectionPage(IContainer container, DINBoard.ViewModels.MainViewModel viewModel, PdfExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(viewModel);

        // === OBLICZENIA ===
        var allMcbs = viewModel.Symbols
            .Where(s => s != null && _moduleTypeService.IsMcb(s))
            .ToList();

        double totalPower = allMcbs.Sum(m => m.PowerW);
        double powerL1 = allMcbs.Where(m => m.Phase == "L1").Sum(m => m.PowerW);
        double powerL2 = allMcbs.Where(m => m.Phase == "L2").Sum(m => m.PowerW);
        double powerL3 = allMcbs.Where(m => m.Phase == "L3").Sum(m => m.PowerW);

        bool isThreePhase = powerL2 > 0 || powerL3 > 0;
        double simultaneity = 0.6;
        double calcPower = totalPower * simultaneity;

        // Prąd obliczeniowy
        double calcCurrent = isThreePhase
            ? calcPower / (400.0 * 1.732)
            : calcPower / 230.0;

        // Dobór wyłącznika głównego (następny standard powyżej prądu obliczeniowego)
        int mainBreakerAmps = StandardBreakers.FirstOrDefault(a => a >= calcCurrent);
        if (mainBreakerAmps == 0) mainBreakerAmps = StandardBreakers[^1];

        string mainBreakerType = isThreePhase ? $"S304 B{mainBreakerAmps} 4P" : $"S302 B{mainBreakerAmps} 2P";
        var (wlzCrossSection, wlzDescription) = GetWlzCable(mainBreakerAmps);
        string preMeterBreaker = GetPreMeterBreaker(mainBreakerAmps);
        string supplyType = isThreePhase ? "3-fazowe 3x400V/230V TN-S" : "1-fazowe 230V TN-S";
        string meterType = isThreePhase ? "Licznik 3-fazowy" : "Licznik 1-fazowy";
        int wireCount = isThreePhase ? 5 : 3;

        container.Column(column =>
        {
            column.Spacing(12);

            // === SEKCJA 1: PARAMETRY PRZYŁĄCZA ===
            column.Item().Border(2).BorderColor(AccentBlue).Background(LightBg).Padding(15).Column(c =>
            {
                c.Spacing(8);
                c.Item().Text("PARAMETRY PRZYŁĄCZA").FontSize(14 * S).Bold().FontColor(AccentBlue);

                c.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(1.5f);
                        cols.RelativeColumn(2);
                    });

                    AddParamRow(table, "Rodzaj zasilania:", supplyType);
                    AddParamRow(table, "Układ sieci:", "TN-S");
                    AddParamRow(table, "Napięcie znamionowe:", isThreePhase ? "3x 400V / 230V" : "230V");
                    AddParamRow(table, "Częstotliwość:", "50 Hz");
                    AddParamRow(table, "Moc zainstalowana:", $"{totalPower:N0} W ({totalPower / 1000:N2} kW)");
                    AddParamRow(table, "Współczynnik jednoczesności:", $"{simultaneity:P0}");
                    AddParamRow(table, "Moc obliczeniowa:", $"{calcPower:N0} W ({calcPower / 1000:N2} kW)");
                    AddParamRow(table, "Prąd obliczeniowy:", $"{calcCurrent:N1} A");
                });
            });

            // === SEKCJA 2: DOBÓR APARATURY ===
            column.Item().Border(2).BorderColor(AccentGreen).Background(LightBg).Padding(15).Column(c =>
            {
                c.Spacing(8);
                c.Item().Text("DOBÓR APARATURY ZABEZPIECZAJĄCEJ").FontSize(14 * S).Bold().FontColor(AccentGreen);

                c.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(1.5f);
                        cols.RelativeColumn(2);
                    });

                    AddParamRow(table, "Zabezpieczenie przedlicznikowe:", preMeterBreaker);
                    AddParamRow(table, "Licznik energii:", meterType);
                    AddParamRow(table, "Wyłącznik główny:", mainBreakerType);
                    AddParamRow(table, "Prąd znamionowy:", $"{mainBreakerAmps} A");
                });
            });

            // === SEKCJA 3: WLZ (Wewnętrzna Linia Zasilająca) ===
            column.Item().Border(2).BorderColor(AccentOrange).Background(LightBg).Padding(15).Column(c =>
            {
                c.Spacing(8);
                c.Item().Text("WEWNĘTRZNA LINIA ZASILAJĄCA (WLZ)").FontSize(14 * S).Bold().FontColor(AccentOrange);

                c.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(1.5f);
                        cols.RelativeColumn(2);
                    });

                    AddParamRow(table, "Typ przewodu:", wlzDescription);
                    AddParamRow(table, "Przekrój żyły:", $"{wlzCrossSection:0.#} mm² Cu");
                    AddParamRow(table, "Ilość żył:", $"{wireCount}");
                    AddParamRow(table, "Obciążalność prądowa:", $"{GetCableAmpacity(wlzCrossSection):N0} A (wg PN-HD 60364-5-52, metoda D1)");
                    AddParamRow(table, "Sposób ułożenia:", "Bezpośrednio w ziemi (metoda D1)");
                });
            });

            // === SEKCJA 4: SCHEMAT BLOKOWY PRZYŁĄCZA ===
            column.Item().Border(1).BorderColor(PrimaryColor).Padding(15).Column(c =>
            {
                c.Spacing(6);
                c.Item().Text("SCHEMAT BLOKOWY PRZYŁĄCZA").FontSize(14 * S).Bold().FontColor(PrimaryColor);

                c.Item().Height(10);
                ComposeConnectionDiagram(c, supplyType, preMeterBreaker, meterType, mainBreakerType,
                    wlzDescription, viewModel.CurrentProject?.Metadata?.Company ?? "Rozdzielnica");
            });

            // === SEKCJA 5: ROZKŁAD OBCIĄŻEŃ NA FAZY ===
            if (isThreePhase)
            {
                column.Item().Border(1).BorderColor(AccentBlue).Padding(15).Column(c =>
                {
                    c.Spacing(8);
                    c.Item().Text("ROZKŁAD OBCIĄŻEŃ NA FAZY").FontSize(14 * S).Bold().FontColor(AccentBlue);

                    double maxPhase = Math.Max(powerL1, Math.Max(powerL2, powerL3));
                    double asymmetry = maxPhase > 0 ? (maxPhase - Math.Min(powerL1, Math.Min(powerL2, powerL3))) / maxPhase * 100 : 0;

                    c.Item().Row(row =>
                    {
                        row.Spacing(8);
                        ComposePhaseBox(row.RelativeItem(), "L1", powerL1, maxPhase, "#964B00");
                        ComposePhaseBox(row.RelativeItem(), "L2", powerL2, maxPhase, "#333333");
                        ComposePhaseBox(row.RelativeItem(), "L3", powerL3, maxPhase, "#808080");
                    });

                    c.Item().Height(5);
                    string asymStatus = asymmetry < 15 ? "✅ Dobra symetria" : asymmetry < 30 ? "⚠ Dopuszczalna asymetria" : "❌ Za duża asymetria!";
                    c.Item().Text($"Asymetria obciążeń: {asymmetry:N1}% - {asymStatus}").FontSize(10 * S).FontColor(TextGray);
                });
            }

            // === UWAGI ===
            column.Item().Background(LightBg).Padding(12).Column(c =>
            {
                c.Spacing(4);
                c.Item().Text("UWAGI:").FontSize(10 * S).Bold().FontColor(TextGray);
                c.Item().Text("• Dobór zabezpieczeń wg PN-HD 60364-4-43, PN-HD 60364-5-52").FontSize(9 * S).FontColor(TextGray);
                c.Item().Text("• Przekroje przewodów dobrane dla metody ułożenia B2 (w tynku)").FontSize(9 * S).FontColor(TextGray);
                c.Item().Text("• Wymagana selektywność zabezpieczeń: przedlicznikowe > główne > obwodowe").FontSize(9 * S).FontColor(TextGray);
                c.Item().Text("• Instalacja wymaga ochrony przeciwprzepięciowej (SPD) wg PN-HD 60364-5-534").FontSize(9 * S).FontColor(TextGray);
            });
        });
    }

    private static void ComposeConnectionDiagram(ColumnDescriptor col,
        string supplyType, string preMeter, string meter, string mainBreaker,
        string wlz, string boardName)
    {
        // Schemat blokowy: linia elementów od góry do dołu
        var elements = new[]
        {
            ("⚡", "SIEĆ ENERGETYCZNA", supplyType, AccentBlue),
            ("⬇", "", "", TextGray),
            ("🔒", "ZABEZPIECZENIE PRZEDLICZNIKOWE", preMeter, AccentOrange),
            ("⬇", "", "", TextGray),
            ("📊", "LICZNIK ENERGII", meter, TextGray),
            ("⬇", $"WLZ: {wlz}", "", AccentOrange),
            ("🔌", "WYŁĄCZNIK GŁÓWNY", mainBreaker, AccentGreen),
            ("⬇", "", "", TextGray),
            ("📦", boardName.ToUpperInvariant(), "Rozdzielnica elektryczna", AccentBlue),
        };

        foreach (var (icon, title, subtitle, color) in elements)
        {
            if (title == "")
            {
                // Strzałka / linia łącząca
                col.Item().AlignCenter().Text(icon).FontSize(14 * S).FontColor(color);
                if (!string.IsNullOrEmpty(subtitle))
                    col.Item().AlignCenter().Text(subtitle).FontSize(9 * S).Italic().FontColor(AccentOrange);
                continue;
            }

            col.Item().AlignCenter().Border(1).BorderColor(color).Padding(8).MinWidth(250).Column(box =>
            {
                box.Item().AlignCenter().Text($"{icon} {title}").FontSize(11 * S).Bold().FontColor(color);
                if (!string.IsNullOrEmpty(subtitle))
                    box.Item().AlignCenter().Text(subtitle).FontSize(9 * S).FontColor(TextGray);
            });
        }
    }

    private static void ComposePhaseBox(IContainer container, string phase, double power, double maxPower, string color)
    {
        double pct = maxPower > 0 ? power / maxPower * 100 : 0;

        container.Border(1).BorderColor(color).Padding(10).Column(c =>
        {
            c.Item().AlignCenter().Text(phase).FontSize(14 * S).Bold().FontColor(color);
            c.Item().AlignCenter().Text($"{power:N0} W").FontSize(12 * S).SemiBold();
            c.Item().AlignCenter().Text($"{pct:N0}%").FontSize(10 * S).FontColor(TextGray);
        });
    }

    private static void AddParamRow(TableDescriptor table, string label, string value)
    {
        table.Cell().Padding(4).Text(label).FontSize(10 * S).FontColor(TextGray);
        table.Cell().Padding(4).Text(value).FontSize(10 * S).SemiBold();
    }

    /// <summary>Obciążalność prądowa przewodu Cu wg PN-HD 60364-5-52, metoda D1 (w ziemi)</summary>
    private static double GetCableAmpacity(double crossSection) =>
        CommonHelpers.GetCableCapacity(crossSection, CommonHelpers.CableAmpacityMethodD1);
}
