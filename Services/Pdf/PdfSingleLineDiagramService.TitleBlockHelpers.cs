using System;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using DINBoard.Models;

namespace DINBoard.Services.Pdf;

public partial class PdfSingleLineDiagramService
{
    private void DrawQuestTitleBlock(IContainer container, SchematicLayout layout, int pageNum, int totalPages)
    {
        var p = layout.Project;
        var meta = p?.Metadata;
        string drawingNum = meta?.ProjectNumber ?? "E-SCH-001";
        string title = GetProjectObjectName(p);
        string desc = p?.Description ?? "Schemat jednokreskowy";
        string date = (meta?.DateModified ?? DateTime.UtcNow).ToString("yyyy-MM-dd");

        // Narrow, tall title block matching the Skia layout.
        container.Width(52).BorderLeft(1.2f).BorderTop(1.2f).BorderColor("#3C414B").Column(col =>
        {
            col.Item().Padding(4).Column(c =>
            {
                c.Item().Text("Obiekt:").FontSize(5).FontColor("#6E737D");
                c.Item().Text(title).FontSize(8).Bold();
            });
            col.Item().BorderTop(0.5f).BorderColor("#3C414B").Padding(4).Column(c =>
            {
                c.Item().Text("Opis:").FontSize(5).FontColor("#6E737D");
                c.Item().Text(desc).FontSize(6).FontColor("#5A5F69");
            });
            col.Item().BorderTop(0.5f).BorderColor("#3C414B").Padding(4).Column(c =>
            {
                c.Item().Text("Nr rys.:").FontSize(5).FontColor("#6E737D");
                c.Item().Text(drawingNum).FontSize(8).Bold();
            });
            col.Item().BorderTop(0.5f).BorderColor("#3C414B").Padding(4).Column(c =>
            {
                c.Item().Text("Data:").FontSize(5).FontColor("#6E737D");
                c.Item().Text(date).FontSize(6).FontColor("#5A5F69");
            });
            col.Item().BorderTop(0.5f).BorderColor("#3C414B").Padding(4).Column(c =>
            {
                c.Item().Text("Arkusz:").FontSize(5).FontColor("#6E737D");
                c.Item().Text($"{pageNum} / {totalPages}").FontSize(8).Bold();
            });
            col.Item().BorderTop(0.5f).BorderColor("#3C414B").Padding(2).AlignRight().Text("PN-EN 60617").FontSize(4).FontColor("#6E737D");
        });
    }

    static string GetProjectObjectName(Project? project)
    {
        string? metadataName = project?.Metadata?.Company;
        if (!string.IsNullOrWhiteSpace(metadataName))
        {
            return metadataName;
        }

        return string.IsNullOrWhiteSpace(project?.Name) ? "Rozdzielnica" : project!.Name;
    }

    static string GetContractorName(ProjectMetadata? metadata)
    {
        if (!string.IsNullOrWhiteSpace(metadata?.Contractor))
        {
            return metadata.Contractor!;
        }

        return "---";
    }
}
