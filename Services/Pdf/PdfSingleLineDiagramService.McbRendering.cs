using System;
using SkiaSharp;
using DINBoard.Models;
using E = DINBoard.Services.SchematicLayoutEngine;

namespace DINBoard.Services.Pdf;

public partial class PdfSingleLineDiagramService
{
    static void DrawMCB(SKCanvas c, SchematicNode n, float y, float yo, bool drawDinRailAxis = true)
    {
        var ph = PhClr(n.Phase);
        float cx = (float)n.X + NW / 2, cy = y + NH / 2;
        
        if (drawDinRailAxis)
        {
            using var dinAxisPen = Stroke(CGrid, 0.4f);
            dinAxisPen.PathEffect = SKPathEffect.CreateDash(new[] { 5f, 5f }, 0);
            c.DrawLine(cx - NW / 2 - 4, cy, cx + NW / 2 + 4, cy, dinAxisPen);
        }

        SymBox(c, (float)n.X, y, ph);

        SymMCB(c, cx, cy, ph);

        // PhaseBadge(c, n.Phase, cx - 20, y + 24); // Removed at the user's request.
        Txt(c, n.Designation, cx + 12, y + 25, 8.5f, CTxtDes, true);

        DrawWireLine(c, cx, y + NH, Y(yo, E.YWireEnd), CWire, 1.2f);
        PhaseMarks(c, cx, y + NH + (Y(yo, E.YWireEnd) - (y + NH)) / 2, n.PhaseCount, n.Phase);
    }
}
