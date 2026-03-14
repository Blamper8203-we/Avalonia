using System.Collections.Generic;
using System.Linq;
using SkiaSharp;
using DINBoard.Models;
using E = DINBoard.Services.SchematicLayoutEngine;

namespace DINBoard.Services.Pdf;

public partial class PdfSingleLineDiagramService
{
    // LEGENDA SYMBOLI
    static void DrawLegend(SKCanvas c, SchematicLayout lay, int pageIndex, float yo)
    {
        var devs = lay.Devices.Where(d => d.Page == pageIndex).ToList();
        var types = new HashSet<SchematicNodeType>();
        foreach (var d in devs)
        {
            types.Add(d.NodeType);
            if (d.Children.Count > 0)
            {
                foreach (var ch in d.Children)
                {
                    types.Add(ch.NodeType);
                }
            }
        }

        var items = new List<(string sym, string desc, SKColor clr)>();
        if (types.Contains(SchematicNodeType.MainBreaker)) items.Add(("FR", "Wyłącznik główny", CFR));
        if (types.Contains(SchematicNodeType.RCD)) items.Add(("RCD", "Wyłącznik różnicowoprądowy", CRCD));
        if (types.Contains(SchematicNodeType.MCB)) items.Add(("MCB", "Wyłącznik nadprądowy", CWire));
        if (types.Contains(SchematicNodeType.SPD)) items.Add(("SPD", "Ogranicznik przepięć", CSPD));
        if (types.Contains(SchematicNodeType.PhaseIndicator)) items.Add(("KF", "Kontrolka fazy", CKF));

        if (items.Count == 0) return;

        float legX = (float)E.DrawR;
        float legW = (float)E.TitleW;
        float tbBottom = (float)(E.PageH - E.FrameB - E.TitleH) + yo;
        float legY = tbBottom - (items.Count * 16 + 20);
        float rowHt = 16f;

        using var bg = new SKPaint { Color = CBoxBg, Style = SKPaintStyle.Fill };
        using var border = Stroke(CFrame, 0.5f);
        float legH = items.Count * rowHt + 18;
        c.DrawRect(legX, legY, legW, legH, bg);
        c.DrawRect(legX, legY, legW, legH, border);

        Txt(c, "LEGENDA", legX + 3, legY + 2, 5.5f, CTxtLbl, true);

        for (int i = 0; i < items.Count; i++)
        {
            var (sym, desc, clr) = items[i];
            float ry = legY + 14 + i * rowHt;
            using var dot = new SKPaint { Color = clr, Style = SKPaintStyle.Fill, IsAntialias = true };
            c.DrawCircle(legX + 8, ry + 5, 3, dot);
            Txt(c, sym, legX + 14, ry, 5.5f, CTxt, true);
            Txt(c, desc, legX + 14, ry + 7, 4, CTxtDim);
        }
    }

    // ETYKIETY KABLI PRZY PRZEWODACH
    static void DrawCableLabels(SKCanvas c, SchematicLayout lay, int pageIndex, float yo)
    {
        var devs = lay.Devices.Where(d => d.Page == pageIndex).ToList();
        foreach (var d in devs)
        {
            if (d.Children.Count > 0)
            {
                foreach (var ch in d.Children)
                {
                    DrawSingleCableLabel(c, ch, yo);
                }
            }
            else if (d.NodeType == SchematicNodeType.MCB)
            {
                DrawSingleCableLabel(c, d, yo);
            }
        }
    }

    static void DrawSingleCableLabel(SKCanvas c, SchematicNode n, float yo)
    {
        string spec = n.CableSpec ?? "";
        string cableType = n.CableType ?? "";
        if (string.IsNullOrEmpty(spec) && string.IsNullOrEmpty(cableType)) return;

        string label = !string.IsNullOrEmpty(spec) ? spec : cableType;
        float cx = (float)n.X + NW / 2;
        float mcbBottom = (float)n.Y + NH + 8;

        using var font = new SKFont(
            SKTypeface.FromFamilyName(
                "Segoe UI",
                SKFontStyleWeight.Bold,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright),
            8.5f);
        using var paint = new SKPaint { Color = CTxtDes, IsAntialias = true };

        c.Save();
        c.Translate(cx + 8, mcbBottom + 10);
        c.RotateDegrees(-90);
        c.DrawText(label, 0, 0, SKTextAlign.Right, font, paint);
        c.Restore();
    }
}
