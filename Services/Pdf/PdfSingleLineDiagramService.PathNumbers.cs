using System.Collections.Generic;
using SkiaSharp;
using DINBoard.Models;
using E = DINBoard.Services.SchematicLayoutEngine;

namespace DINBoard.Services.Pdf;

public partial class PdfSingleLineDiagramService
{
    static void DrawPathNumbers(SKCanvas c, SchematicLayout lay, List<SchematicNode> devs, float yo)
    {
        int n = 1;
        foreach (var d in lay.Devices)
        {
            if (devs.Contains(d)) break;
            n += GetVisualSlotCount(d);
        }

        foreach (var d in devs)
        {
            if (ShouldReserveHeadSlot(d))
            {
                DrawPL(c, (float)d.X + NW / 2, ref n, yo, -10f);
                foreach (var ch in d.Children)
                {
                    DrawPL(c, (float)ch.X + NW / 2, ref n, yo);
                }
            }
            else if (d.Children.Count > 0)
            {
                foreach (var ch in d.Children)
                {
                    DrawPL(c, (float)ch.X + NW / 2, ref n, yo);
                }
            }
            else
            {
                DrawPL(c, (float)d.X + NW / 2, ref n, yo);
            }
        }
    }

    static void DrawPL(SKCanvas c, float cx, ref int n, float yo, float labelOffsetX = 0)
    {
        DrawPathNumberLabel(c, n.ToString(), cx + labelOffsetX, Y(yo, E.YPathNums) - 10);
        using var gp = Stroke(CGrid, 0.4f);
        gp.PathEffect = SKPathEffect.CreateDash(new[] { 2f, 2f }, 0);
        c.DrawLine(cx, Y(yo, E.YPathNums + 8), cx, Y(yo, E.YLabelTop), gp);
        n++;
    }
}
