using System;
using SkiaSharp;
using DINBoard.Models;
using E = DINBoard.Services.SchematicLayoutEngine;

namespace DINBoard.Services.Pdf;

public partial class PdfSingleLineDiagramService
{
    static void DrawDevice(SKCanvas c, SchematicNode n, PageInfo pi, float yo, bool drawDinRailAxis = true)
    {
        float cx = (float)n.X + NW / 2, by = Y(yo, E.YMainBus);
        switch (n.NodeType)
        {
            case SchematicNodeType.MainBreaker:
                if (n.Children.Count > 0)
                {
                    DrawGroupedMainBreaker(c, n, pi, yo, drawDinRailAxis);
                    break;
                }

                string supplyLabel = GetSupplyLabel(n.PhaseCount);
                DrawWireLine(c, cx, Y(yo, E.YSupply), (float)n.Y, CFR, 1.8f);
                PhaseMarks(c, cx, Y(yo, E.YSupply) + ((float)n.Y - Y(yo, E.YSupply)) / 2, n.PhaseCount, n.Phase, hasNeutral: true);
                Txt(c, supplyLabel, cx - 38, Y(yo, E.YSupply) + ((float)n.Y - Y(yo, E.YSupply)) / 2 - 2, 8, CTxtDim);
                SymBox(c, (float)n.X, (float)n.Y, CFR);
                SymFR(c, cx, (float)n.Y + NH / 2, CFR);
                Txt(c, n.Designation, cx + 12, (float)n.Y + 25, 9, CTxtDes, true);
                DrawWireLine(c, cx, (float)n.Y + NH, by, CWire, 1.2f);
                PhaseMarks(c, cx, (float)n.Y + NH + (by - ((float)n.Y + NH)) / 2, n.PhaseCount, n.Phase, hasNeutral: true);
                DrawDot(c, cx, by, CWire, 2.5f);
                break;

            case SchematicNodeType.PhaseIndicator:
                WireDot(c, cx, by, (float)n.Y);
                PhaseMarks(c, cx, by + ((float)n.Y - by) / 2, n.PhaseCount, n.Phase);
                SymBox(c, (float)n.X, (float)n.Y, CKF);
                SymKF(c, cx, (float)n.Y + NH / 2);
                Txt(c, n.Designation, cx + 12, (float)n.Y + 25, 8.5f, CTxtDes, true);
                TxtR(c, n.Protection, cx - 12, (float)n.Y + NH / 2 + 5, 7.5f, CTxtDim);
                break;

            case SchematicNodeType.SPD:
                WireDot(c, cx, by, (float)n.Y);
                PhaseMarks(c, cx, by + ((float)n.Y - by) / 2, n.PhaseCount, n.Phase, hasNeutral: true);
                SymBox(c, (float)n.X, (float)n.Y, CSPD);
                SymSPD(c, cx, (float)n.Y + NH / 2);
                Txt(c, n.Designation, cx + 12, (float)n.Y + 25, 8.5f, CTxtDes, true);
                SymGround(c, cx, (float)n.Y + NH + 3);
                DrawTextCenteredInBox(c, n.Protection, cx - 35, (float)n.Y + NH + 24, 70, 7, CTxtDim);
                break;

            case SchematicNodeType.RCD:
                DrawRCD(c, n, pi, yo, drawDinRailAxis);
                break;

            case SchematicNodeType.MCB:
                WireDot(c, cx, by, (float)n.Y);
                PhaseMarks(c, cx, by + ((float)n.Y - by) / 2, n.PhaseCount, n.Phase);
                DrawMCB(c, n, (float)n.Y, yo, drawDinRailAxis);
                break;
        }
    }

    // UsuniÄ™to DrawInfoTable, CellText, DrawNodeTblData z logiki Skia.
    // Od teraz formatowane sÄ… przez metody DrawQuestTable w QuestPDF.

}
