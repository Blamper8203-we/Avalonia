using System;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using DINBoard.Models;
using DINBoard.ViewModels;

namespace DINBoard.Services.Pdf;

/// <summary>
/// Serwis odpowiedzialny za generowanie tabeli obwodów
/// </summary>
public class PdfCircuitTableService
{
    private static readonly string PrimaryColor = "#1F2937";
    private static readonly string AccentGreen = "#10B981";
    private static readonly string AccentOrange = "#D97706";
    private static readonly string TextGray = "#6B7280";
    private const float UiToPdfScale = 0.75f;
    private readonly IModuleTypeService _moduleTypeService;

    public PdfCircuitTableService(IModuleTypeService moduleTypeService)
    {
        _moduleTypeService = moduleTypeService;
    }

    public void ComposeCircuitTable(IContainer container, MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(viewModel);

        container.Column(column =>
        {
            column.Spacing(10);

            var orderByGroupId = (viewModel.CurrentProject?.Groups ?? new System.Collections.Generic.List<CircuitGroup>())
                .Where(g => g.Order > 0)
                .GroupBy(g => g.Id)
                .ToDictionary(g => g.Key, g => g.First().Order, StringComparer.Ordinal);

            // Grupowanie obwodów wg RCD
            var groups = viewModel.Symbols
                .Where(s => s != null && (_moduleTypeService.IsMcb(s) || _moduleTypeService.IsRcd(s)))
                .GroupBy(s => s.Group ?? "")
                .Select(g =>
                {
                    var groupId = g.Key;
                    var list = g.ToList();
                    var groupName = list.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.GroupName))?.GroupName;

                    var explicitOrder = (!string.IsNullOrEmpty(groupId) && orderByGroupId.TryGetValue(groupId, out var o))
                        ? o
                        : 0;

                    var parsedFromName = TryExtractPositiveNumber(groupName);

                    var orderKey = explicitOrder > 0
                        ? explicitOrder
                        : (parsedFromName ?? int.MaxValue);

                    return new
                    {
                        GroupId = groupId,
                        Group = list,
                        OrderKey = orderKey,
                        MinY = list.Min(s => s.Y),
                        MinX = list.Min(s => s.X)
                    };
                })
                .OrderBy(g => g.OrderKey)
                .ThenBy(g => g.MinY)
                .ThenBy(g => g.MinX)
                .ThenBy(g => g.GroupId)
                .ToList();

            foreach (var groupInfo in groups)
            {
                var group = groupInfo.Group;
                var rcd = group.FirstOrDefault(s => _moduleTypeService.IsRcd(s));
                var mcbs = group.Where(s => _moduleTypeService.IsMcb(s))
                    .OrderBy(s => s.ModuleNumber)
                    .ThenBy(s => s.X)
                    .ToList();

                if (rcd != null || mcbs.Count != 0)
                {
                    // Nagłówek grupy RCD/SPD
                    var spd = group.FirstOrDefault(s => _moduleTypeService.IsSpd(s));

                    // Logic for group label: RCD/SPD info OR "Group-X of circuits"
                    var groupName = group.FirstOrDefault(s => !string.IsNullOrEmpty(s.GroupName))?.GroupName ?? "Grupa";
                    var fallbackLabel = $"{groupName} obwodów";

                    var groupLabel = rcd?.RcdInfo ?? spd?.SpdInfo ?? fallbackLabel;
                    var groupColor = spd != null ? AccentOrange : AccentGreen;

                    column.Item().Background("#E5E7EB").Padding(8).Row(row =>
                    {
                        row.RelativeItem().Text(groupLabel)
                            .FontSize(11 * UiToPdfScale).SemiBold().FontColor(groupColor);
                        row.AutoItem().Text($"{mcbs.Count} obwodów").FontSize(10 * UiToPdfScale).FontColor(TextGray);
                    });

                    // Tabela MCB
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(35);  // Ref
                            columns.RelativeColumn(2);   // Nazwa
                            columns.ConstantColumn(50);  // Zabezp.
                            columns.ConstantColumn(40);  // Faza
                            columns.ConstantColumn(60);  // Moc
                            columns.ConstantColumn(60);  // Przekrój
                            columns.RelativeColumn(1.5f);// Lokalizacja
                        });

                        // Nagłówki
                        table.Header(header =>
                        {
                            header.Cell().Background(PrimaryColor).Padding(5)
                                .Text("Ref.").FontColor("#FFFFFF").FontSize(9 * UiToPdfScale).SemiBold();
                            header.Cell().Background(PrimaryColor).Padding(5)
                                .Text("Nazwa obwodu").FontColor("#FFFFFF").FontSize(9 * UiToPdfScale).SemiBold();
                            header.Cell().Background(PrimaryColor).Padding(5)
                                .Text("Zabezp.").FontColor("#FFFFFF").FontSize(9 * UiToPdfScale).SemiBold();
                            header.Cell().Background(PrimaryColor).Padding(5)
                                .Text("Faza").FontColor("#FFFFFF").FontSize(9 * UiToPdfScale).SemiBold();
                            header.Cell().Background(PrimaryColor).Padding(5)
                                .Text("Moc").FontColor("#FFFFFF").FontSize(9 * UiToPdfScale).SemiBold();
                            header.Cell().Background(PrimaryColor).Padding(5)
                                .Text("Przekrój").FontColor("#FFFFFF").FontSize(9 * UiToPdfScale).SemiBold();
                            header.Cell().Background(PrimaryColor).Padding(5)
                                .Text("Lokalizacja").FontColor("#FFFFFF").FontSize(9 * UiToPdfScale).SemiBold();
                        });

                        // Wiersze
                        int idx = 1;
                        foreach (var mcb in mcbs)
                        {
                            var bg = idx % 2 == 0 ? "#F9FAFB" : "#FFFFFF";

                            // Generuj oznaczenie referencyjne jeśli nie ma
                            var refDesignation = mcb.ReferenceDesignation;
                            if (string.IsNullOrEmpty(refDesignation))
                            {
                                refDesignation = GenerateReferenceDesignation(mcb, idx);
                            }

                            table.Cell().Background(bg).Padding(5).Text(refDesignation).FontSize(9 * UiToPdfScale).Bold().FontColor(AccentGreen);
                            table.Cell().Background(bg).Padding(5).Text(mcb.CircuitName ?? "-").FontSize(9 * UiToPdfScale);
                            table.Cell().Background(bg).Padding(5).Text(mcb.ProtectionType ?? "-").FontSize(9 * UiToPdfScale).Bold();
                            table.Cell().Background(bg).Padding(5).Text(mcb.Phase ?? "L1").FontSize(9 * UiToPdfScale);
                            table.Cell().Background(bg).Padding(5).Text($"{mcb.PowerW}W").FontSize(9 * UiToPdfScale);
                            table.Cell().Background(bg).Padding(5)
                                .Text(mcb.CableCrossSection > 0 ? $"{mcb.CableCrossSection:0.#} mm²" : "-")
                                .FontSize(9 * UiToPdfScale);
                            table.Cell().Background(bg).Padding(5).Text(mcb.Location ?? "-").FontSize(9 * UiToPdfScale);

                            idx++;
                        }
                    });

                    column.Item().Height(15);
                }
            }
        });
    }

    private static int? TryExtractPositiveNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var digits = new string(text.Where(char.IsDigit).ToArray());
        if (!int.TryParse(digits, out var number)) return null;

        return number > 0 ? number : null;
    }

    /// <summary>
    /// Generuje oznaczenie referencyjne zgodne z normą IEC 61346
    /// </summary>
    private static string GenerateReferenceDesignation(SymbolItem symbol, int index)
    {
        // QS - Rozłącznik główny (FR)
        // Q  - Wyłączniki różnicowoprądowe (RCD)
        // F  - Zabezpieczenia nadprądowe (MCB)
        // FA - Ograniczniki przepięć (SPD)
        // H  - Kontrolki faz
        // K  - Styczniki/Przekaźniki
        // S  - Przełączniki

        var type = symbol.Type?.ToLowerInvariant() ?? "";

        if (type.Contains("mcb") || type.Contains("breaker"))
            return $"F{index}";
        else if (type.Contains("fuse"))
            return $"F{index}";
        else if (type.Contains("contactor") || type.Contains("relay"))
            return $"K{index}";
        else if (type.Contains("switch"))
            return $"S{index}";
        else
            return $"F{index}"; // Domyślnie MCB
    }
}
