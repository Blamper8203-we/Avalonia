using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DINBoard.Models;
using DINBoard.ViewModels;

namespace DINBoard.Services.Pdf;

/// <summary>
/// Serwis eksportu dokumentacji rozdzielnicy do formatu LaTeX (.tex).
/// Generuje kompletny dokument LaTeX gotowy do kompilacji (pdflatex/xelatex).
/// </summary>
public class LatexExportService
{
    private readonly IModuleTypeService _moduleTypeService;
    private readonly IElectricalValidationService _validationService;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public LatexExportService(
        IModuleTypeService moduleTypeService,
        IElectricalValidationService validationService)
    {
        _moduleTypeService = moduleTypeService ?? throw new ArgumentNullException(nameof(moduleTypeService));
        _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
    }

    /// <summary>
    /// Eksportuje dokumentację do pliku .tex.
    /// </summary>
    public void ExportToLatex(MainViewModel viewModel, string filePath, PdfExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(filePath);

        var sb = new StringBuilder(8192);

        ComposePreamble(sb, viewModel);
        ComposeTitlePage(sb, viewModel, options);
        ComposeCircuitTable(sb, viewModel);
        ComposePowerBalance(sb, viewModel);
        ComposeConnection(sb, viewModel);
        ComposeStandards(sb, viewModel);
        ComposeEndDocument(sb);

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// Asynchroniczny wariant eksportu.
    /// </summary>
    public async Task ExportToLatexAsync(
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
            ExportToLatex(viewModel, filePath, options);
            progress?.Report(100);
        }, cancellationToken).ConfigureAwait(false);
    }

    internal static double CalculateDesignCurrent(double calculatedPowerW, double lineVoltageV, bool isThreePhase, double cosPhi = 0.9)
    {
        if (calculatedPowerW <= 0 || lineVoltageV <= 0 || cosPhi <= 0)
        {
            return 0;
        }

        var phaseVoltageV = lineVoltageV / Math.Sqrt(3);
        return isThreePhase
            ? calculatedPowerW / (lineVoltageV * Math.Sqrt(3) * cosPhi)
            : calculatedPowerW / (phaseVoltageV * cosPhi);
    }

    // =====================================================================
    //  PREAMBLE
    // =====================================================================

    private static void ComposePreamble(StringBuilder sb, MainViewModel viewModel)
    {
        var meta = viewModel.CurrentProject?.Metadata;
        var projectName = viewModel.CurrentProject?.Name ?? "Projekt";

        sb.AppendLine(@"\documentclass[a4paper,11pt]{article}");
        sb.AppendLine();
        sb.AppendLine(@"% --- Kodowanie i język ---");
        sb.AppendLine(@"\usepackage[utf8]{inputenc}");
        sb.AppendLine(@"\usepackage[T1]{fontenc}");
        sb.AppendLine(@"\usepackage[polish]{babel}");
        sb.AppendLine();
        sb.AppendLine(@"% --- Układ strony ---");
        sb.AppendLine(@"\usepackage[margin=25mm]{geometry}");
        sb.AppendLine(@"\usepackage{fancyhdr}");
        sb.AppendLine(@"\usepackage{lastpage}");
        sb.AppendLine();
        sb.AppendLine(@"% --- Tabele i formatowanie ---");
        sb.AppendLine(@"\usepackage{booktabs}");
        sb.AppendLine(@"\usepackage{longtable}");
        sb.AppendLine(@"\usepackage{array}");
        sb.AppendLine(@"\usepackage{xcolor}");
        sb.AppendLine(@"\usepackage{colortbl}");
        sb.AppendLine();
        sb.AppendLine(@"% --- Kolory (spójne z PDF) ---");
        sb.AppendLine(@"\definecolor{AccentBlue}{HTML}{3B82F6}");
        sb.AppendLine(@"\definecolor{AccentGreen}{HTML}{10B981}");
        sb.AppendLine(@"\definecolor{AccentOrange}{HTML}{D97706}");
        sb.AppendLine(@"\definecolor{AccentRed}{HTML}{EF4444}");
        sb.AppendLine(@"\definecolor{TextGray}{HTML}{6B7280}");
        sb.AppendLine(@"\definecolor{LightBg}{HTML}{F3F4F6}");
        sb.AppendLine(@"\definecolor{PhaseL1}{HTML}{964B00}");
        sb.AppendLine(@"\definecolor{PhaseL2}{HTML}{333333}");
        sb.AppendLine(@"\definecolor{PhaseL3}{HTML}{808080}");
        sb.AppendLine();
        sb.AppendLine(@"% --- Nagłówek i stopka ---");
        sb.AppendLine(@"\pagestyle{fancy}");
        sb.AppendLine(@"\fancyhf{}");
        sb.AppendLine($@"\lhead{{\small {Escape(projectName)}}}");
        sb.AppendLine($@"\rhead{{\small {Escape(meta?.Company ?? "")}}}");
        sb.AppendLine(@"\cfoot{\small Strona \thepage\ z \pageref{LastPage}}");
        sb.AppendLine(@"\renewcommand{\headrulewidth}{0.4pt}");
        sb.AppendLine(@"\renewcommand{\footrulewidth}{0.4pt}");
        sb.AppendLine();
        sb.AppendLine(@"% --- Tytuł ---");
        sb.AppendLine($@"\title{{{Escape(projectName)} \\ \large Dokumentacja rozdzielnicy}}");
        sb.AppendLine($@"\author{{{Escape(meta?.Author ?? "")}}}");
        sb.AppendLine($@"\date{{{DateTime.Now:dd.MM.yyyy}}}");
        sb.AppendLine();
        sb.AppendLine(@"\begin{document}");
    }

    // =====================================================================
    //  TITLE PAGE
    // =====================================================================

    private static void ComposeTitlePage(StringBuilder sb, MainViewModel viewModel, PdfExportOptions options)
    {
        var meta = viewModel.CurrentProject?.Metadata;
        var projectName = viewModel.CurrentProject?.Name ?? "Projekt";

        sb.AppendLine();
        sb.AppendLine(@"\begin{titlepage}");
        sb.AppendLine(@"\centering");
        sb.AppendLine(@"\vspace*{3cm}");
        sb.AppendLine($@"{{\Huge\bfseries {Escape(projectName)}}}\\[1cm]");
        sb.AppendLine($@"{{\Large Dokumentacja rozdzielnicy elektrycznej}}\\[2cm]");
        sb.AppendLine();
        sb.AppendLine(@"\begin{tabular}{rl}");

        AddTitleField(sb, "Obiekt:", meta?.Company);
        AddTitleField(sb, "Adres:", meta?.Address);
        AddTitleField(sb, "Inwestor:", meta?.Investor);
        AddTitleField(sb, "Projektant:", meta?.Author);
        AddTitleField(sb, "Nr uprawnień:", meta?.AuthorLicense);
        AddTitleField(sb, "Nr projektu:", meta?.ProjectNumber);
        AddTitleField(sb, "Rewizja:", meta?.Revision);

        sb.AppendLine(@"\end{tabular}");
        sb.AppendLine();
        sb.AppendLine(@"\vfill");
        sb.AppendLine($@"{{\small Data: {DateTime.Now:dd.MM.yyyy}}}");
        sb.AppendLine(@"\end{titlepage}");
        sb.AppendLine();
        sb.AppendLine(@"\tableofcontents");
        sb.AppendLine(@"\newpage");
    }

    // =====================================================================
    //  CIRCUIT TABLE
    // =====================================================================

    private void ComposeCircuitTable(StringBuilder sb, MainViewModel viewModel)
    {
        sb.AppendLine();
        sb.AppendLine(@"\section{Lista obwodów}");
        sb.AppendLine();

        var mcbs = viewModel.Symbols
            .Where(s => s != null && _moduleTypeService.IsMcb(s))
            .OrderBy(s => s.ModuleNumber)
            .ToList();

        if (mcbs.Count == 0)
        {
            sb.AppendLine(@"\textit{Brak obwodów w projekcie.}");
            sb.AppendLine();
            return;
        }

        // Group by RCD
        var groups = mcbs.GroupBy(m =>
        {
            if (string.IsNullOrEmpty(m.RcdSymbolId)) return (string?)null;
            return m.RcdSymbolId;
        });

        foreach (var group in groups)
        {
            if (group.Key != null)
            {
                var rcd = viewModel.Symbols.FirstOrDefault(s => s.Id == group.Key);
                if (rcd != null)
                {
                    sb.AppendLine($@"\subsection*{{Grupa RCD: {Escape(rcd.Label ?? rcd.Type)} " +
                                  $@"({rcd.RcdRatedCurrent}A / {rcd.RcdResidualCurrent}mA typ {Escape(rcd.RcdType)})}}");
                }
            }
            else
            {
                sb.AppendLine(@"\subsection*{Obwody bez RCD}");
            }

            sb.AppendLine();
            sb.AppendLine(@"\begin{longtable}{clllrrp{3cm}}");
            sb.AppendLine(@"\toprule");
            sb.AppendLine(@"\textbf{Nr} & \textbf{Oznaczenie} & \textbf{Zabezpieczenie} & \textbf{Faza} & " +
                          @"\textbf{Moc [W]} & \textbf{Typ obwodu} & \textbf{Opis} \\");
            sb.AppendLine(@"\midrule");
            sb.AppendLine(@"\endhead");

            foreach (var mcb in group.OrderBy(m => m.ModuleNumber))
            {
                var nr = mcb.ModuleNumber > 0 ? mcb.ModuleNumber.ToString(Inv) : "--";
                var label = Escape(mcb.Label ?? mcb.Type);
                var protection = Escape(mcb.Protection ?? "--");
                var phase = Escape(mcb.Phase ?? "L1");
                var power = mcb.PowerW.ToString("N0", Inv);
                var circuitType = Escape(mcb.CircuitType ?? "--");
                var desc = Escape(mcb.CircuitDescription ?? "");

                sb.AppendLine($@"{nr} & {label} & {protection} & {phase} & {power} & {circuitType} & {desc} \\");
            }

            sb.AppendLine(@"\bottomrule");
            sb.AppendLine(@"\end{longtable}");
            sb.AppendLine();
        }
    }

    // =====================================================================
    //  POWER BALANCE
    // =====================================================================

    private static void ComposePowerBalance(StringBuilder sb, MainViewModel viewModel)
    {
        sb.AppendLine(@"\section{Bilans mocy}");
        sb.AppendLine();

        var dist = PhaseDistributionCalculator.CalculateTotalDistribution(viewModel.Symbols);
        double powerL1 = dist.L1PowerW;
        double powerL2 = dist.L2PowerW;
        double powerL3 = dist.L3PowerW;
        double totalPower = powerL1 + powerL2 + powerL3;

        double simultaneity = viewModel.PowerBalance.SimultaneityFactor;
        double calcPower = totalPower * simultaneity;

        var lineVoltage = viewModel.CurrentProject?.PowerConfig?.Voltage ?? 400;
        var phaseVoltage = lineVoltage / Math.Sqrt(3);
        bool isThreePhase = viewModel.CurrentProject?.PowerConfig?.Phases == 3;

        double calcCurrent = CalculateDesignCurrent(calcPower, lineVoltage, isThreePhase);

        double asymmetry = PhaseDistributionCalculator.CalculateImbalancePercent(powerL1, powerL2, powerL3);

        sb.AppendLine(@"\subsection{Moc zainstalowana}");
        sb.AppendLine();
        sb.AppendLine(@"\begin{tabular}{lr}");
        sb.AppendLine(@"\toprule");
        sb.AppendLine($@"\textbf{{Moc zainstalowana}} & \textbf{{{totalPower.ToString("N0", Inv)} W}} ({(totalPower / 1000).ToString("N2", Inv)} kW) \\");
        sb.AppendLine(@"\midrule");
        sb.AppendLine($@"Faza L1 & {powerL1.ToString("N0", Inv)} W \\");
        sb.AppendLine($@"Faza L2 & {powerL2.ToString("N0", Inv)} W \\");
        sb.AppendLine($@"Faza L3 & {powerL3.ToString("N0", Inv)} W \\");
        sb.AppendLine(@"\bottomrule");
        sb.AppendLine(@"\end{tabular}");
        sb.AppendLine();

        sb.AppendLine(@"\subsection{Obliczenia}");
        sb.AppendLine();

        var voltageLabel = isThreePhase ? $"3\\times{lineVoltage}V" : $"{phaseVoltage.ToString("N0", Inv)}V";

        sb.AppendLine(@"\begin{tabular}{lr}");
        sb.AppendLine(@"\toprule");
        sb.AppendLine($@"Współczynnik jednoczesności & {simultaneity.ToString("P0", new CultureInfo("pl-PL"))} \\");
        sb.AppendLine($@"Moc obliczeniowa & {calcPower.ToString("N0", Inv)} W ({(calcPower / 1000).ToString("N2", Inv)} kW) \\");
        sb.AppendLine($@"Prąd obliczeniowy (${voltageLabel}$) & {calcCurrent.ToString("N1", Inv)} A \\");
        if (isThreePhase)
            sb.AppendLine($@"Asymetria faz & {asymmetry.ToString("N1", Inv)}\% \\");
        sb.AppendLine(@"\bottomrule");
        sb.AppendLine(@"\end{tabular}");
        sb.AppendLine();
        sb.AppendLine(@"\newpage");
    }

    // =====================================================================
    //  CONNECTION / SUPPLY
    // =====================================================================

    private static void ComposeConnection(StringBuilder sb, MainViewModel viewModel)
    {
        sb.AppendLine(@"\section{Przyłącze --- Dobór zabezpieczeń i WLZ}");
        sb.AppendLine();

        var dist = PhaseDistributionCalculator.CalculateTotalDistribution(viewModel.Symbols);
        double totalPower = dist.L1PowerW + dist.L2PowerW + dist.L3PowerW;

        double simultaneity = viewModel.PowerBalance.SimultaneityFactor;
        double calcPower = totalPower * simultaneity;

        var lineVoltage = viewModel.CurrentProject?.PowerConfig?.Voltage ?? 400;
        var phaseVoltage = lineVoltage / Math.Sqrt(3);
        bool isThreePhase = viewModel.CurrentProject?.PowerConfig?.Phases == 3;

        double calcCurrent = CalculateDesignCurrent(calcPower, lineVoltage, isThreePhase);

        int[] standardBreakers = [16, 20, 25, 32, 40, 50, 63, 80, 100, 125];
        int mainBreakerAmps = standardBreakers.FirstOrDefault(a => a >= calcCurrent);
        if (mainBreakerAmps == 0) mainBreakerAmps = standardBreakers[^1];

        string mainBreakerType = isThreePhase ? $"S304 B{mainBreakerAmps} 4P" : $"S302 B{mainBreakerAmps} 2P";
        string supplyType = isThreePhase
            ? $"3-fazowe 3\\times{lineVoltage}V/{phaseVoltage.ToString("N0", Inv)}V TN-S"
            : $"1-fazowe {phaseVoltage.ToString("N0", Inv)}V TN-S";
        int wireCount = isThreePhase ? 5 : 3;

        // Dobór WLZ
        var (wlzCrossSection, wlzDescription) = mainBreakerAmps switch
        {
            <= 25 => (4.0, "YKY 5x4 mm²"),
            <= 32 => (6.0, "YKY 5x6 mm²"),
            <= 40 => (10.0, "YKY 5x10 mm²"),
            <= 63 => (16.0, "YKY 5x16 mm²"),
            <= 80 => (25.0, "YKY 5x25 mm²"),
            <= 100 => (35.0, "YKY 5x35 mm²"),
            _ => (50.0, "YKY 5x50 mm²")
        };

        double cableAmpacity = CommonHelpers.GetCableCapacity(wlzCrossSection, CommonHelpers.CableAmpacityMethodD1);

        // Zabezpieczenie przedlicznikowe
        string preMeterBreaker = mainBreakerAmps switch
        {
            <= 25 => "SBI 25A gG",
            <= 32 => "SBI 32A gG",
            <= 40 => "SBI 40A gG",
            <= 50 => "SBI 50A gG",
            <= 63 => "SBI 63A gG",
            _ => $"SBI {mainBreakerAmps}A gG"
        };

        sb.AppendLine(@"\subsection{Parametry przyłącza}");
        sb.AppendLine();
        sb.AppendLine(@"\begin{tabular}{p{6cm}l}");
        sb.AppendLine(@"\toprule");
        sb.AppendLine($@"Rodzaj zasilania & {supplyType} \\");
        sb.AppendLine(@"Układ sieci & TN-S \\");
        sb.AppendLine($@"Napięcie znamionowe & {(isThreePhase ? $"3\\times {lineVoltage}V / {phaseVoltage.ToString("N0", Inv)}V" : $"{phaseVoltage.ToString("N0", Inv)}V")} \\");
        sb.AppendLine(@"Częstotliwość & 50 Hz \\");
        sb.AppendLine($@"Moc zainstalowana & {totalPower.ToString("N0", Inv)} W ({(totalPower / 1000).ToString("N2", Inv)} kW) \\");
        sb.AppendLine($@"Współczynnik jednoczesności & {simultaneity.ToString("P0", new CultureInfo("pl-PL"))} \\");
        sb.AppendLine($@"Moc obliczeniowa & {calcPower.ToString("N0", Inv)} W ({(calcPower / 1000).ToString("N2", Inv)} kW) \\");
        sb.AppendLine($@"Prąd obliczeniowy & {calcCurrent.ToString("N1", Inv)} A \\");
        sb.AppendLine(@"\bottomrule");
        sb.AppendLine(@"\end{tabular}");
        sb.AppendLine();

        sb.AppendLine(@"\subsection{Dobór aparatury zabezpieczającej}");
        sb.AppendLine();
        sb.AppendLine(@"\begin{tabular}{p{6cm}l}");
        sb.AppendLine(@"\toprule");
        sb.AppendLine($@"Zabezpieczenie przedlicznikowe & {Escape(preMeterBreaker)} \\");
        sb.AppendLine($@"Licznik energii & {(isThreePhase ? "Licznik 3-fazowy" : "Licznik 1-fazowy")} \\");
        sb.AppendLine($@"Wyłącznik główny & {Escape(mainBreakerType)} \\");
        sb.AppendLine($@"Prąd znamionowy & {mainBreakerAmps} A \\");
        sb.AppendLine(@"\bottomrule");
        sb.AppendLine(@"\end{tabular}");
        sb.AppendLine();

        sb.AppendLine(@"\subsection{Wewnętrzna linia zasilająca (WLZ)}");
        sb.AppendLine();
        sb.AppendLine(@"\begin{tabular}{p{6cm}l}");
        sb.AppendLine(@"\toprule");
        sb.AppendLine($@"Typ przewodu & {Escape(wlzDescription)} \\");
        sb.AppendLine($@"Przekrój żyły & {wlzCrossSection.ToString("0.#", Inv)} mm$^2$ Cu \\");
        sb.AppendLine($@"Ilość żył & {wireCount} \\");
        sb.AppendLine($@"Obciążalność prądowa & {cableAmpacity.ToString("N0", Inv)} A (wg PN-HD 60364-5-52, metoda D1) \\");
        sb.AppendLine(@"Sposób ułożenia & Bezpośrednio w ziemi (metoda D1) \\");
        sb.AppendLine(@"\bottomrule");
        sb.AppendLine(@"\end{tabular}");
        sb.AppendLine();

        // Rozkład na fazy (3F)
        if (isThreePhase)
        {
            double asymmetry = PhaseDistributionCalculator.CalculateImbalancePercent(dist.L1PowerW, dist.L2PowerW, dist.L3PowerW);
            string status = asymmetry < 15 ? "Dobra symetria" : asymmetry < 30 ? "Dopuszczalna asymetria" : "Za duża asymetria!";

            sb.AppendLine(@"\subsection{Rozkład obciążeń na fazy}");
            sb.AppendLine();
            sb.AppendLine(@"\begin{tabular}{lrr}");
            sb.AppendLine(@"\toprule");
            sb.AppendLine(@"\textbf{Faza} & \textbf{Moc [W]} & \textbf{Udział} \\");
            sb.AppendLine(@"\midrule");

            double maxPhase = Math.Max(dist.L1PowerW, Math.Max(dist.L2PowerW, dist.L3PowerW));
            double pctL1 = maxPhase > 0 ? dist.L1PowerW / maxPhase * 100 : 0;
            double pctL2 = maxPhase > 0 ? dist.L2PowerW / maxPhase * 100 : 0;
            double pctL3 = maxPhase > 0 ? dist.L3PowerW / maxPhase * 100 : 0;

            sb.AppendLine($@"\textcolor{{PhaseL1}}{{L1}} & {dist.L1PowerW.ToString("N0", Inv)} & {pctL1.ToString("N0", Inv)}\% \\");
            sb.AppendLine($@"\textcolor{{PhaseL2}}{{L2}} & {dist.L2PowerW.ToString("N0", Inv)} & {pctL2.ToString("N0", Inv)}\% \\");
            sb.AppendLine($@"\textcolor{{PhaseL3}}{{L3}} & {dist.L3PowerW.ToString("N0", Inv)} & {pctL3.ToString("N0", Inv)}\% \\");
            sb.AppendLine(@"\bottomrule");
            sb.AppendLine(@"\end{tabular}");
            sb.AppendLine();
            sb.AppendLine($@"\noindent Asymetria obciążeń: {asymmetry.ToString("N1", Inv)}\% --- {status}");
            sb.AppendLine();
        }

        sb.AppendLine(@"\newpage");
    }

    // =====================================================================
    //  STANDARDS
    // =====================================================================

    private void ComposeStandards(StringBuilder sb, MainViewModel viewModel)
    {
        sb.AppendLine(@"\section{Zgodność z normami}");
        sb.AppendLine();

        sb.AppendLine(@"\subsection{Normy ogólne}");
        sb.AppendLine();
        sb.AppendLine(@"\begin{tabular}{p{5cm}l}");
        sb.AppendLine(@"\toprule");
        sb.AppendLine(@"PN-HD 60364-4-41:2023 & Ochrona przeciwporażeniowa \\");
        sb.AppendLine(@"PN-HD 60364-5-52:2023 & Dobór przewodów i ich układanie \\");
        sb.AppendLine(@"PN-EN 61439-1:2022 & Szafy i rozdzielnice niskiego napięcia \\");
        sb.AppendLine(@"\bottomrule");
        sb.AppendLine(@"\end{tabular}");
        sb.AppendLine();

        sb.AppendLine(@"\subsection{Parametry instalacji}");
        sb.AppendLine();

        var balance = _validationService.CalculatePhaseLoads(viewModel.Symbols);
        var lineVoltage = viewModel.CurrentProject?.PowerConfig?.Voltage ?? 400;
        var phaseVoltage = lineVoltage / Math.Sqrt(3);
        bool isThreePhase = viewModel.CurrentProject?.PowerConfig?.Phases == 3;
        var voltageLabel = isThreePhase ? $"{phaseVoltage.ToString("N0", Inv)} V / {lineVoltage} V" : $"{phaseVoltage.ToString("N0", Inv)} V";

        var rcds = viewModel.Symbols.Count(s => s != null && _moduleTypeService.IsRcd(s));
        var totalMcbs = viewModel.Symbols.Count(s => s != null && _moduleTypeService.IsMcb(s));

        sb.AppendLine(@"\begin{tabular}{p{5cm}lr}");
        sb.AppendLine(@"\toprule");
        sb.AppendLine($@"Napięcie znamionowe & {voltageLabel} & $\checkmark$ \\");
        sb.AppendLine($@"Częstotliwość & 50 Hz & $\checkmark$ \\");
        sb.AppendLine($@"Uziemienie & TN-S & $\checkmark$ \\");
        sb.AppendLine($@"Asymetria faz & {balance.ImbalancePercent.ToString("F1", Inv)}\% & " +
                      $@"{(balance.ImbalancePercent <= 15.0 ? "$\\checkmark$" : "$\\triangle$")} \\");
        sb.AppendLine(@"\midrule");
        sb.AppendLine($@"Wyłączniki różnicowoprądowe & {rcds} szt. & {(rcds > 0 ? "$\\checkmark$" : "$\\triangle$")} \\");
        sb.AppendLine($@"Wyłączniki nadprądowe & {totalMcbs} szt. & {(totalMcbs > 0 ? "$\\checkmark$" : "$\\triangle$")} \\");
        sb.AppendLine(@"Ochrona różnicowoprądowa & 30 mA dla obwodów gniazd & $\checkmark$ \\");
        sb.AppendLine(@"\bottomrule");
        sb.AppendLine(@"\end{tabular}");
        sb.AppendLine();

        sb.AppendLine(@"\subsection{Podsumowanie}");
        sb.AppendLine();
        sb.AppendLine(@"\begin{itemize}");
        sb.AppendLine(@"  \item[$\checkmark$] Instalacja spełnia wymagania norm PN-HD 60364");
        sb.AppendLine(@"  \item[$\checkmark$] Zastosowano właściwą ochronę przeciwporażeniową");
        if (balance.ImbalancePercent <= 15.0)
            sb.AppendLine(@"  \item[$\checkmark$] Asymetria faz jest w dopuszczalnych granicach");
        else
            sb.AppendLine($@"  \item[$\triangle$] Asymetria faz przekracza 15\% ({balance.ImbalancePercent.ToString("F1", Inv)}\%)");
        sb.AppendLine(@"\end{itemize}");
        sb.AppendLine();
    }

    // =====================================================================
    //  END DOCUMENT
    // =====================================================================

    private static void ComposeEndDocument(StringBuilder sb)
    {
        sb.AppendLine(@"\end{document}");
    }

    // =====================================================================
    //  HELPERS
    // =====================================================================

    private static void AddTitleField(StringBuilder sb, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        sb.AppendLine($@"\textbf{{{Escape(label)}}} & {Escape(value)} \\[4pt]");
    }

    /// <summary>
    /// Escapes special LaTeX characters in user-provided text.
    /// </summary>
    private static string Escape(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        return text
            .Replace(@"\", @"\textbackslash{}")
            .Replace("{", @"\{")
            .Replace("}", @"\}")
            .Replace("&", @"\&")
            .Replace("%", @"\%")
            .Replace("$", @"\$")
            .Replace("#", @"\#")
            .Replace("_", @"\_")
            .Replace("~", @"\textasciitilde{}")
            .Replace("^", @"\textasciicircum{}");
    }
}
