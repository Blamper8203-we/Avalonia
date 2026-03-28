using System.Collections.Generic;
using System.Linq;
using SkiaSharp;
using DINBoard.Models;
using E = DINBoard.Services.SchematicLayoutEngine;

namespace DINBoard.Services.Pdf;

public partial class PdfSingleLineDiagramService
{
    public static void DrawSkiaTable(SKCanvas canvas, SchematicLayout lay, int pageIndex, float yOffset)
    {
        var devs = lay.Devices.Where(d => d.Page == pageIndex).ToList();
        var nodes = new List<SchematicNode>();
        foreach (var d in devs)
        {
            AddLeafNodes(nodes, d);
        }
        if (nodes.Count == 0) return;

        using var linePen = Stroke(CGrid, 0.4f);
        using var sepPen = Stroke(CFrame, 1.0f);

        (double yRow, string label)[] headers = {
            (E.YRowDesig,    "Oznaczenie"),
            (E.YRowProt,     "Zabezp."),
            (E.YRowCircuit,  "Obw\u00F3d"),
            (E.YRowLocation, "Lokalizacja"),
            (E.YRowCable,    "Kabel"),
            (E.YRowCableType,"Typ kabla"),
            (E.YRowCableSpec,"Przekr\u00F3j"),
            (E.YRowCableLen, "D\u0142ugo\u015B\u0107"),
            (E.YRowPower,    "Moc"),
        };
        foreach (var (yRow, label) in headers)
        {
            float ry = Y(yOffset, yRow);
            Txt(canvas, label, (float)E.DrawL + 4, ry + 2, (float)E.HeaderFontSize, CTxtLbl);
            canvas.DrawLine((float)E.DrawL, ry + (float)E.RowH, (float)E.DrawR, ry + (float)E.RowH, linePen);
        }

        float sepY = Y(yOffset, E.YRowSep);
        canvas.DrawLine((float)E.DrawL, sepY, (float)E.DrawR, sepY, sepPen);

        foreach (var n in nodes)
        {
            float cx = (float)(n.X + E.NW / 2);
            float cw = (float)n.CellWidth;
            float cellL = cx - cw / 2f;

            canvas.DrawLine(cellL, Y(yOffset, E.YRowDesig), cellL, Y(yOffset, E.YTableEnd), linePen);

            string desig = n.Symbol?.ReferenceDesignation ?? n.Designation ?? "";
            string prot = n.Symbol?.ProtectionType ?? n.Protection ?? "";
            string circuit = n.Symbol?.CircuitName ?? n.CircuitName ?? "";
            string location = n.Symbol?.Location ?? n.Location ?? "";
            string cableDesig = n.CableDesig ?? "";
            string cableType = n.CableType ?? "";
            string cableSpec = n.CableSpec ?? "";
            string cableLen = n.CableLength ?? "";
            string power = n.PowerInfo ?? "";

            if (n.NodeType == SchematicNodeType.MainBreaker) { cableDesig = "FR"; }
            else if (n.NodeType == SchematicNodeType.SPD) { cableDesig = "SPD"; }
            else if (n.NodeType == SchematicNodeType.PhaseIndicator) { cableDesig = "KF"; }

            TblCell(canvas, desig, cellL, Y(yOffset, E.YRowDesig), cw, CTxtDes, true);
            TblCell(canvas, prot, cellL, Y(yOffset, E.YRowProt), cw, CTxt, true);
            TblCell(canvas, string.IsNullOrEmpty(circuit) ? "-" : circuit, cellL, Y(yOffset, E.YRowCircuit), cw, CTxtDim);
            TblCell(canvas, string.IsNullOrEmpty(location) ? "-" : location, cellL, Y(yOffset, E.YRowLocation), cw, CTxtDim);
            TblCell(canvas, cableDesig, cellL, Y(yOffset, E.YRowCable), cw, CTxtDes, true);
            TblCell(canvas, cableType, cellL, Y(yOffset, E.YRowCableType), cw, CTxtDim);
            TblCell(canvas, cableSpec, cellL, Y(yOffset, E.YRowCableSpec), cw, CTxt);
            TblCell(canvas, cableLen, cellL, Y(yOffset, E.YRowCableLen), cw, CTxtDim);
            TblCell(canvas, power, cellL, Y(yOffset, E.YRowPower), cw, CTxtDim);
        }
    }
}
