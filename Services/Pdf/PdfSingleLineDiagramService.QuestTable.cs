using System.Collections.Generic;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using DINBoard.Models;

namespace DINBoard.Services.Pdf;

public partial class PdfSingleLineDiagramService
{
    private void DrawQuestTable(IContainer container, List<SchematicNode> devs)
    {
        var nodes = new List<SchematicNode>();
        foreach (var d in devs)
        {
            AddLeafNodes(nodes, d);
        }
        if (nodes.Count == 0) return;

        container.PaddingHorizontal(20).PaddingBottom(10).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(60);
                foreach (var n in nodes)
                {
                    columns.RelativeColumn((float)n.CellWidth);
                }
            });

            static IContainer CellH(IContainer c) => c.BorderBottom(0.5f).BorderColor("#B4B9C3").PaddingVertical(3).AlignMiddle();
            static IContainer CellD(IContainer c) => c.BorderBottom(0.5f).BorderColor("#B4B9C3").BorderLeft(0.28f).BorderColor("#B4B9C3").PaddingVertical(3).AlignCenter().AlignMiddle();

            table.Cell().Element(CellH).Text("Oznaczenie").FontSize(7.5f).FontColor("#6E737D");
            foreach (var n in nodes) table.Cell().Element(CellD).Text(CellText(n, "Designation")).FontSize(11.5f).Bold().FontColor("#282832");

            table.Cell().Element(CellH).Text("Zabezp.").FontSize(7.5f).FontColor("#6E737D");
            foreach (var n in nodes) table.Cell().Element(CellD).Text(CellText(n, "Protection")).FontSize(11.5f).Bold().FontColor("#1E232D");

            table.Cell().Element(CellH).Text("Obw\u00F3d").FontSize(7.5f).FontColor("#6E737D");
            foreach (var n in nodes) table.Cell().Element(CellD).Text(string.IsNullOrEmpty(CellText(n, "CircuitName")) ? "-" : CellText(n, "CircuitName")).FontSize(11.5f).FontColor("#6E737D");

            table.Cell().Element(CellH).Text("Lokalizacja").FontSize(7.5f).FontColor("#6E737D");
            foreach (var n in nodes) table.Cell().Element(CellD).Text(string.IsNullOrEmpty(CellText(n, "Location")) ? "-" : CellText(n, "Location")).FontSize(11.5f).FontColor("#5A5F69");

            table.Cell().ColumnSpan((uint)(nodes.Count + 1)).BorderBottom(1.5f).BorderColor("#3C414B");

            table.Cell().Element(CellH).Text("Kabel").FontSize(7.5f).FontColor("#6E737D");
            foreach (var n in nodes)
            {
                string text = "";
                string color = "#282832";
                if (n.NodeType == SchematicNodeType.MainBreaker) { text = "FR"; color = "#783CDC"; }
                else if (n.NodeType == SchematicNodeType.SPD) { text = "SPD"; color = "#D28200"; }
                else if (n.NodeType == SchematicNodeType.PhaseIndicator) { text = "KF"; color = "#AA8214"; }
                else { text = CellText(n, "CableDesig"); }

                table.Cell().Element(CellD).Text(text).FontSize(11.5f).Bold().FontColor(color);
            }

            table.Cell().Element(CellH).Text("Typ kabla").FontSize(7.5f).FontColor("#6E737D");
            foreach (var n in nodes) table.Cell().Element(CellD).Text(n.NodeType == SchematicNodeType.MainBreaker || n.NodeType == SchematicNodeType.MCB ? CellText(n, "CableType") : CellText(n, "Protection")).FontSize(11.5f).FontColor("#5A5F69");

            table.Cell().Element(CellH).Text("Przekr\u00F3j").FontSize(7.5f).FontColor("#6E737D");
            foreach (var n in nodes) table.Cell().Element(CellD).Text(n.NodeType == SchematicNodeType.MainBreaker || n.NodeType == SchematicNodeType.MCB ? (n.NodeType == SchematicNodeType.MainBreaker ? CellText(n, "CableDesig") : CellText(n, "CableSpec")) : "").FontSize(11.5f).FontColor("#1E232D");

            table.Cell().Element(CellH).Text("D\u0142ugo\u015B\u0107").FontSize(7.5f).FontColor("#6E737D");
            foreach (var n in nodes) table.Cell().Element(CellD).Text(n.NodeType == SchematicNodeType.MCB ? CellText(n, "CableLength") : "").FontSize(11.5f).FontColor("#5A5F69");

            table.Cell().Element(CellH).BorderBottom(0).PaddingVertical(3).AlignMiddle().Text("Moc").FontSize(7.5f).FontColor("#6E737D");
            foreach (var n in nodes) table.Cell().Element(c => c.BorderLeft(0.28f).BorderColor("#B4B9C3").PaddingVertical(3).AlignCenter().AlignMiddle()).Text(n.NodeType == SchematicNodeType.MCB ? CellText(n, "PowerInfo") : "").FontSize(11.5f).FontColor("#6E737D");
        });
    }
}
