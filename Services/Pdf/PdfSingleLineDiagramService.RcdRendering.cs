using System;
using SkiaSharp;
using DINBoard.Models;
using E = DINBoard.Services.SchematicLayoutEngine;

namespace DINBoard.Services.Pdf;

public partial class PdfSingleLineDiagramService
{
    static void DrawRCD(SKCanvas c, SchematicNode rcd, PageInfo pi, float yo, bool drawDinRailAxis = true)
    {
        float cx = (float)rcd.X + NW / 2, by = Y(yo, E.YMainBus);
        WireDot(c, cx, by, (float)rcd.Y);
        PhaseMarks(c, cx, by + ((float)rcd.Y - by) / 2, rcd.PhaseCount, rcd.Phase, hasNeutral: true);
        SymBox(c, (float)rcd.X, (float)rcd.Y, CRCD);
        SymRCD(c, cx, (float)rcd.Y + NH / 2);
        Txt(c, rcd.Designation, cx + 12, (float)rcd.Y + 25, 9, CTxtDes, true);
        TxtR(c, rcd.Protection, cx - 22, (float)rcd.Y + NH / 2 + 5, 7.5f, CRCD);

        if (rcd.Children.Count == 0) return;

        float gY = Y(yo, E.YGroupBus);
        float f = (float)rcd.Children[0].X + NW / 2;
        float l = (float)rcd.Children[^1].X + NW / 2;
        WireDot(c, cx, (float)rcd.Y + NH, gY);
        PhaseMarks(c, cx, (float)rcd.Y + NH + (gY - ((float)rcd.Y + NH)) / 2, rcd.PhaseCount, rcd.Phase, hasNeutral: true);
        using var gBus = Stroke(CWire, 2.2f);
        c.DrawLine(f - 8, gY, l + 8, gY, gBus);

        foreach (var ch in rcd.Children)
        {
            float chCx = (float)ch.X + NW / 2;
            WireDot(c, chCx, gY, (float)ch.Y);
            bool childHasN = ch.NodeType == SchematicNodeType.SPD;
            PhaseMarks(c, chCx, gY + ((float)ch.Y - gY) / 2, ch.PhaseCount, ch.Phase, hasNeutral: childHasN);
            switch (ch.NodeType)
            {
                case SchematicNodeType.MCB:
                    DrawMCB(c, ch, (float)ch.Y, yo, drawDinRailAxis);
                    break;
                case SchematicNodeType.SPD:
                    SymBox(c, (float)ch.X, (float)ch.Y, CSPD);
                    SymSPD(c, chCx, (float)ch.Y + NH / 2);
                    Txt(c, ch.Designation, chCx + 12, (float)ch.Y + 25, 8, CTxtDes, true);
                    SymGround(c, chCx, (float)ch.Y + NH + 3);
                    DrawTextCenteredInBox(c, ch.Protection, chCx - 35, (float)ch.Y + NH + 22, 70, 6.5f, CTxtDim);
                    break;
                case SchematicNodeType.PhaseIndicator:
                    SymBox(c, (float)ch.X, (float)ch.Y, CKF);
                    SymKF(c, chCx, (float)ch.Y + NH / 2);
                    Txt(c, ch.Designation, chCx + 12, (float)ch.Y + 25, 8, CTxtDes, true);
                    TxtR(c, ch.Protection, chCx - 12, (float)ch.Y + NH / 2 + 5, 7, CTxtDim);
                    break;
            }
        }
    }
}
