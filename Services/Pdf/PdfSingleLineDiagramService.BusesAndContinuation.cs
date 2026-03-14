using SkiaSharp;
using DINBoard.Models;
using E = DINBoard.Services.SchematicLayoutEngine;

namespace DINBoard.Services.Pdf;

public partial class PdfSingleLineDiagramService
{
    // CONTINUATION BETWEEN SHEETS
    static void DrawCont(SKCanvas c, PageInfo pi, float yo, int tgt, bool right)
    {
        float by = Y(yo, E.YMainBus);
        using var pen = Stroke(CCont, 1.2f);
        if (right)
        {
            float x = (float)pi.BusX2 + 4;
            c.DrawLine(x, by, x + 16, by, pen);
            c.DrawLine(x + 12, by - 3, x + 16, by, pen);
            c.DrawLine(x + 12, by + 3, x + 16, by, pen);
            Txt(c, $"→ ark. {tgt}", x, by - 20, 8, CCont, true);
        }
        else
        {
            float x = (float)pi.BusX1 - 4;
            c.DrawLine(x - 16, by, x, by, pen);
            c.DrawLine(x - 12, by - 3, x - 16, by, pen);
            c.DrawLine(x - 12, by + 3, x - 16, by, pen);
            Txt(c, $"← z ark. {tgt}", x - 48, by - 20, 8, CCont, true);
        }
    }

    // MAIN BUS
    static void DrawMainBus(SKCanvas c, PageInfo pi, float yo)
    {
        using var pen = Stroke(CBus, 3.5f);
        c.DrawLine((float)pi.BusX1, Y(yo, E.YMainBus), (float)pi.BusX2, Y(yo, E.YMainBus), pen);
    }

    // N / PE BARS
    static void DrawNPE(SKCanvas c, PageInfo pi, float yo)
    {
        using var nPen = Stroke(CN, 1.4f);
        c.DrawLine((float)pi.BusX1, Y(yo, E.YN), (float)pi.BusX2, Y(yo, E.YN), nPen);
        Txt(c, "N", (float)pi.BusX1 - 16, Y(yo, E.YN) - 4, 9, CN, true);

        using var pePen = new SKPaint { Color = CPE, Style = SKPaintStyle.Stroke, StrokeWidth = 1.4f, PathEffect = SKPathEffect.CreateDash(new[] { 6f, 3f }, 0), IsAntialias = true };
        c.DrawLine((float)pi.BusX1, Y(yo, E.YPE), (float)pi.BusX2, Y(yo, E.YPE), pePen);
        Txt(c, "PE", (float)pi.BusX1 - 22, Y(yo, E.YPE) - 4, 9, CPE, true);
    }
}

