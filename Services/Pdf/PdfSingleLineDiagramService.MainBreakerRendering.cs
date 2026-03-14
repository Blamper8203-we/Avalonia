using System;
using SkiaSharp;
using DINBoard.Models;
using E = DINBoard.Services.SchematicLayoutEngine;

namespace DINBoard.Services.Pdf;

public partial class PdfSingleLineDiagramService
{
    static void DrawGroupedMainBreaker(SKCanvas c, SchematicNode breaker, PageInfo pi, float yo, bool drawDinRailAxis = true)
    {
        float cx = (float)breaker.X + NW / 2;
        string supplyLabel = GetSupplyLabel(breaker.PhaseCount);
        DrawWireLine(c, cx, Y(yo, E.YSupply), (float)breaker.Y, CFR, 1.8f);
        PhaseMarks(c, cx, Y(yo, E.YSupply) + ((float)breaker.Y - Y(yo, E.YSupply)) / 2, breaker.PhaseCount, breaker.Phase, hasNeutral: true);
        Txt(c, supplyLabel, cx - 38, Y(yo, E.YSupply) + ((float)breaker.Y - Y(yo, E.YSupply)) / 2 - 2, 8, CTxtDim);
        SymBox(c, (float)breaker.X, (float)breaker.Y, CFR);
        SymFR(c, cx, (float)breaker.Y + NH / 2, CFR);
        Txt(c, breaker.Designation, cx + 12, (float)breaker.Y + 25, 9, CTxtDes, true);
        TxtR(c, breaker.Protection, cx - 18, (float)breaker.Y + NH / 2 + 5, 7.5f, CTxtDim);

        if (breaker.Children.Count == 0) return;

        bool hasDistributionBlock = breaker.DistributionBlockSymbol != null;
        bool useMainBusAsDistribution = breaker.PhaseCount == 1;
        float by = Y(yo, E.YMainBus);
        float gY = useMainBusAsDistribution ? by : (hasDistributionBlock ? Y(yo, E.YGroupBus + 22) : Y(yo, E.YGroupBus));
        float f = (float)breaker.Children[0].X + NW / 2;
        float l = (float)breaker.Children[^1].X + NW / 2;
        if (useMainBusAsDistribution)
        {
            DrawWireLine(c, cx, (float)breaker.Y + NH, by, CWire, 1.2f);
            PhaseMarks(c, cx, (float)breaker.Y + NH + (by - ((float)breaker.Y + NH)) / 2, breaker.PhaseCount, breaker.Phase);
            DrawDot(c, cx, by, CWire, 2.5f);
            DrawDistributionBlockLabel(c, breaker, f - NW / 2 + 4f, by - 10f);
        }
        else if (hasDistributionBlock)
        {
            float blockW = 52f;
            float blockH = 18f;
            float blockTop = gY - blockH - 18f;
            float blockLeft = cx - blockW / 2f;

            DrawWireLine(c, cx, (float)breaker.Y + NH, blockTop, CWire, 1.2f);
            PhaseMarks(c, cx, (float)breaker.Y + NH + (blockTop - ((float)breaker.Y + NH)) / 2, breaker.PhaseCount, breaker.Phase);
            DrawDistributionBlock(c, breaker, blockLeft, blockTop, blockW, blockH);
            WireDot(c, cx, blockTop + blockH, gY);
        }
        else
        {
            WireDot(c, cx, (float)breaker.Y + NH, gY);
            PhaseMarks(c, cx, (float)breaker.Y + NH + (gY - ((float)breaker.Y + NH)) / 2, breaker.PhaseCount, breaker.Phase);
        }

        if (!useMainBusAsDistribution)
        {
            using var gBus = Stroke(CWire, 2.2f);
            c.DrawLine(f - 8, gY, l + 8, gY, gBus);
        }

        foreach (var ch in breaker.Children)
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

    static void DrawDistributionBlock(SKCanvas c, SchematicNode node, float x, float y, float width, float height)
    {
        using var fill = new SKPaint { Color = CBoxBg, Style = SKPaintStyle.Fill, IsAntialias = true };
        using var stroke = Stroke(CBus, 1.2f);
        c.DrawRoundRect(new SKRoundRect(new SKRect(x, y, x + width, y + height), 4, 4), fill);
        c.DrawRoundRect(new SKRoundRect(new SKRect(x, y, x + width, y + height), 4, 4), stroke);

        float cx = x + width / 2f;
        DrawDot(c, cx, y, CWire, 2.2f);
        DrawDot(c, cx, y + height, CWire, 2.2f);

        string label = GetDistributionBlockLabel(node);
        DrawTextCentered(c, label, cx, y + height / 2f + 3, 7, CTxtDim, true);
    }

    static void DrawDistributionBlockLabel(SKCanvas c, SchematicNode node, float x, float y)
    {
        string label = GetDistributionBlockLabel(node);
        Txt(c, label, x, y - 7f, 7, CTxtDim, true);
    }

    static string GetSupplyLabel(int phaseCount) => phaseCount >= 3 ? "3~ 400V" : "1~ 230V";
    static string GetDistributionBlockLabel(SchematicNode node)
    {
        string? label = node.DistributionBlockSymbol?.Label;
        if (string.IsNullOrWhiteSpace(label) || label.Contains("blok", StringComparison.OrdinalIgnoreCase))
        {
            return "BIAS";
        }

        return label;
    }
}
