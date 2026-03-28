using System;
using SkiaSharp;

namespace DINBoard.Services.Pdf;

public partial class PdfSingleLineDiagramService
{
    static float px(float val) => (val / 300f) * NW;
    static float py(float val) => (val / 350f) * NH;
    const float SymbolStrokeWidth = 0.85f;
    const float SymbolDashStrokeWidth = 0.55f;
    const float WireStrokeWidth = 1.5f;
    const float WireDotRadius = 2.2f;

    static void DrawP(SKCanvas c, SKColor clr, float w, params float[] l)
    {
        using var p = Stroke(clr, w);
        for(int i = 0; i < l.Length; i+=4)
            c.DrawLine(l[i], l[i+1], l[i+2], l[i+3], p);
    }
    
    static void SymFR(SKCanvas b, float cx, float cy, SKColor clr)
    {
        float x(float v) => cx - NW/2f + px(v);
        float y(float v) => cy - NH/2f + py(v);
        
        DrawP(b, clr, SymbolStrokeWidth,
            x(150), y(0), x(150), y(120),
            x(144), y(119), x(156), y(119),
            x(150), y(180), x(125), y(125),
            x(150), y(180), x(150), y(350)
        );
        using var p = Stroke(clr, SymbolStrokeWidth); b.DrawCircle(x(150), y(126), px(6), p);
        using var f = new SKPaint { Color = clr, Style = SKPaintStyle.Fill, IsAntialias = true }; b.DrawCircle(x(150), y(180), px(3), f);
    }

    static void SymMCB(SKCanvas b, float cx, float cy, SKColor clr)
    {
        float x(float v) => cx - NW/2f + px(v);
        float y(float v) => cy - NH/2f + py(v);
        
        DrawP(b, clr, SymbolStrokeWidth,
            x(150), y(0), x(150), y(120),
            x(144), y(120), x(156), y(120),
            x(150), y(180), x(125), y(125),
            x(144), y(102), x(156), y(114),
            x(144), y(114), x(156), y(102),
            x(150), y(180), x(150), y(350)
        );
        using var f = new SKPaint { Color = clr, Style = SKPaintStyle.Fill, IsAntialias = true }; b.DrawCircle(x(150), y(180), px(3), f);
    }

    static void SymRCD(SKCanvas b, float cx, float cy)
    {
        float x(float v) => cx - NW/2f + px(v);
        float y(float v) => cy - NH/2f + py(v);
        SKColor clr = CRCD;
        
        DrawP(b, clr, SymbolStrokeWidth,
            x(150), y(0), x(150), y(120),
            x(150), y(180), x(125), y(120),
            x(150), y(180), x(150), y(350)
        );
        // Current transformer mark.
        using var p = Stroke(clr, SymbolStrokeWidth);
        b.DrawOval(x(150), y(230), px(25), py(12), p);
        
        using var f = new SKPaint { Color = clr, Style = SKPaintStyle.Fill, IsAntialias = true }; 
        b.DrawCircle(x(150), y(120), px(3), f);
        b.DrawCircle(x(150), y(180), px(3), f);
        
        // Dashed lines
        using var dash = new SKPaint { Color = clr, Style = SKPaintStyle.Stroke, StrokeWidth = SymbolDashStrokeWidth, StrokeCap = SKStrokeCap.Butt, StrokeJoin = SKStrokeJoin.Miter, PathEffect = SKPathEffect.CreateDash(new[] { 2f, 2f }, 0), IsAntialias = true };
        b.DrawLine(x(125), y(230), x(100), y(230), dash);
        b.DrawLine(x(100), y(230), x(100), y(150), dash);
        b.DrawLine(x(100), y(150), x(135), y(150), dash);

    }

    static void SymSPD(SKCanvas b, float cx, float cy)
    {
        float x(float v) => cx - NW/2f + px(v);
        float y(float v) => cy - NH/2f + py(v);
        SKColor clr = CSPD;
        
        DrawP(b, clr, SymbolStrokeWidth,
            x(150), y(0), x(150), y(130),
            x(150), y(130), x(150), y(155),
            x(143), y(148), x(150), y(155),
            x(150), y(155), x(157), y(148),
            x(150), y(190), x(150), y(165),
            x(143), y(172), x(150), y(165),
            x(150), y(165), x(157), y(172),
            x(150), y(190), x(150), y(250),
            x(125), y(250), x(175), y(250),
            x(135), y(260), x(165), y(260),
            x(145), y(270), x(155), y(270)
        );
        using var p = Stroke(clr, SymbolStrokeWidth);
        b.DrawRect(x(130), y(130), px(40), py(60), p);
        
        using var f = new SKPaint { Color = clr, Style = SKPaintStyle.Fill, IsAntialias = true }; 
        b.DrawCircle(x(150), y(60), px(3), f);
    }

    static void SymKF(SKCanvas b, float cx, float cy)
    {
        float x(float v) => cx - NW/2f + px(v);
        float y(float v) => cy - NH/2f + py(v);
        SKColor clr = CKF;
        
        DrawP(b, clr, SymbolStrokeWidth,
            x(150), y(0), x(150), y(130),
            x(144), y(154), x(156), y(166),
            x(144), y(166), x(156), y(154),
            x(150), y(190), x(150), y(350)
        );
        using var p = Stroke(clr, SymbolStrokeWidth);
        b.DrawRect(x(130), y(130), px(40), py(60), p);
        b.DrawCircle(x(150), y(160), px(8), p);

        using var f = new SKPaint { Color = clr, Style = SKPaintStyle.Fill, IsAntialias = true }; 
        b.DrawCircle(x(150), y(60), px(3), f);
        b.DrawCircle(x(150), y(250), px(3), f);

        Txt(b, "N", x(165), y(350), 5f, clr, true);
    }

    static void SymGround(SKCanvas c, float cx, float y)
    {
        float g = y + 5;
        float w = NW * 0.3f;
        using var p1 = Stroke(CPE, 1.1f); c.DrawLine(cx, y, cx, g, p1);
        using var p12 = Stroke(CPE, 1.35f); c.DrawLine(cx - w/2, g, cx + w/2, g, p12);
        using var p8 = Stroke(CPE, 0.95f); c.DrawLine(cx - w/3, g + 3, cx + w/3, g + 3, p8);
        using var p5 = Stroke(CPE, 0.65f); c.DrawLine(cx - w/6, g + 6, cx + w/6, g + 6, p5);
    }

    static void PhaseMarks(SKCanvas c, float cx, float cy, int n, string? phaseText = null, bool hasNeutral = false)
    {
        // The user asked to remove the neutral-wire mark.
        // The 'hasNeutral' flag is ignored here; we only draw phase marks.
        int totalMarks = Math.Clamp(n, 1, 3);
        float h = 4, gap = 2.5f, off = -(totalMarks - 1) * gap / 2;
        using var pen = Stroke(CTxt, 1.15f);
        for (int i = 0; i < totalMarks; i++)
        {
            float d = off + i * gap;
            c.DrawLine(cx - h + d, cy + h, cx + h + d, cy - h, pen);
        }

        // Podpisz fazę (np. L1+L2) z boku kresek
        if (!string.IsNullOrEmpty(phaseText) && phaseText != "PENDING" && phaseText != "pending" && phaseText != "3P")
        {
            // Skip "L1+L2+L3" for FR/SPD when a dedicated larger label is used.
            // For 2P/3P MCBs we still want to show the phase text.
            // Move the label 3 pt up to avoid overlapping the bus line.
            Txt(c, phaseText, cx + 8, cy - 1, 6f, CTxtDim);
        }
    }

    // Helpers
    static void SymBox(SKCanvas c, float x, float y, SKColor accent)
    {
        // Intentionally left blank: user requested no boxes around devices.
    }

    static void WireDot(SKCanvas c, float x, float y1, float y2)
    {
        DrawDot(c, x, y1, CWire, WireDotRadius);
        DrawWireLine(c, x, y1, y2, CWire, WireStrokeWidth);
    }

    static void DrawDot(SKCanvas c, float x, float y, SKColor color, float r)
    {
        using var fill = new SKPaint { Color = color, Style = SKPaintStyle.Fill, IsAntialias = true };
        c.DrawCircle(x, y, r, fill);
    }

    static void DrawWireLine(SKCanvas c, float x, float y1, float y2, SKColor color, float width)
    {
        using var pen = Stroke(color, width);
        c.DrawLine(x, y1, x, y2, pen);
    }

    static void PhaseBadge(SKCanvas c, string? phase, float x, float y)
    {
        if (string.IsNullOrEmpty(phase) || phase == "PENDING") return;
        var br = PhClr(phase);
        string t = phase switch { "L1" => "1", "L2" => "2", "L3" => "3", "L1+L2+L3" => "3P", _ => "" };
        if (string.IsNullOrEmpty(t)) return;
        using var bg = new SKPaint { Color = br, Style = SKPaintStyle.Fill, IsAntialias = true };
        c.DrawRoundRect(new SKRoundRect(new SKRect(x, y, x + 13, y + 13), 2, 2), bg);
        using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 7.5f);
        using var tp = new SKPaint { Color = CWhite, IsAntialias = true };
        float tw = font.MeasureText(t);
        c.DrawText(t, x + (13 - tw) / 2, y + 9.5f, SKTextAlign.Left, font, tp);
    }

    static SKColor PhClr(string? p) => p switch
    {
        "L1" => CL1, "L2" => CL2, "L3" => CL3, "L1+L2+L3" => CL1, _ => CL1
    };
}
