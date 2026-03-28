using System;
using SkiaSharp;
using DINBoard.Services;
using E = DINBoard.Services.SchematicLayoutEngine;

namespace DINBoard.Services.Pdf;

public partial class PdfSingleLineDiagramService
{
    static SKPaint Stroke(SKColor color, float width) => new()
    {
        Color = color, Style = SKPaintStyle.Stroke, StrokeWidth = width, IsAntialias = true,
        StrokeCap = SKStrokeCap.Butt,
        StrokeJoin = SKStrokeJoin.Miter
    };

    static void Txt(SKCanvas c, string text, float x, float y, float size, SKColor color, bool bold = false)
    {
        if (string.IsNullOrEmpty(text)) return;

        using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", bold ? SKFontStyle.Bold : SKFontStyle.Normal), size);
        using var paint = new SKPaint { Color = color, IsAntialias = true };

        string[] lines = text.Split('\n');
        float lineGap = 1.5f;

        for (int i = 0; i < lines.Length; i++)
        {
            c.DrawText(lines[i], x, y + size + i * (size + lineGap), SKTextAlign.Left, font, paint);
        }
    }

    static void TxtR(SKCanvas c, string text, float xRight, float y, float size, SKColor color)
    {
        if (string.IsNullOrEmpty(text)) return;

        using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), size);
        using var paint = new SKPaint { Color = color, IsAntialias = true };

        string[] lines = text.Split('\n');
        float lineGap = 1.5f;
        float totalHeight = lines.Length * size + (lines.Length - 1) * lineGap;
        float startY = y - totalHeight / 2 + size;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.Length > 22) line = line[..21] + "...";
            float tw = font.MeasureText(line);
            c.DrawText(line, xRight - tw, startY + i * (size + lineGap), SKTextAlign.Left, font, paint);
        }
    }

    static void DrawTextCentered(SKCanvas c, string text, float cx, float y, float size, SKColor color, bool bold = false)
    {
        if (string.IsNullOrEmpty(text)) return;
        using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", bold ? SKFontStyle.Bold : SKFontStyle.Normal), size);
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        c.DrawText(text, cx, y + size, SKTextAlign.Center, font, paint);
    }

    static void DrawPathNumberLabel(SKCanvas c, string text, float cx, float y)
    {
        using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 10);
        float textWidth = font.MeasureText(text);
        float paddingX = 4f;
        float rectWidth = textWidth + paddingX * 2f;
        float rectHeight = 13f;
        float rectLeft = cx - rectWidth / 2f;
        float rectTop = y - 1.5f;

        using var fill = new SKPaint { Color = CWhite, Style = SKPaintStyle.Fill, IsAntialias = true };
        using var border = Stroke(CGridTxt, 0.6f);
        c.DrawRoundRect(new SKRoundRect(new SKRect(rectLeft, rectTop, rectLeft + rectWidth, rectTop + rectHeight), 2, 2), fill);
        c.DrawRoundRect(new SKRoundRect(new SKRect(rectLeft, rectTop, rectLeft + rectWidth, rectTop + rectHeight), 2, 2), border);

        DrawTextCentered(c, text, cx, y, 9.5f, CTxtDes, true);
    }

    static void DrawTextCenteredInBox(SKCanvas c, string text, float x, float y, float w, float size, SKColor color)
    {
        if (string.IsNullOrEmpty(text)) return;

        using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), size);
        using var paint = new SKPaint { Color = color, IsAntialias = true };

        string[] lines = text.Split('\n');
        float lineGap = 1.5f;
        float totalHeight = lines.Length * size + (lines.Length - 1) * lineGap;
        float startY = y + (float)(E.RowH - totalHeight) / 2 + size - (size * 0.1f);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.Length > 22) line = line[..21] + "...";
            float tw = font.MeasureText(line);
            c.DrawText(line, x + (w - tw) / 2, startY + i * (size + lineGap), SKTextAlign.Left, font, paint);
        }
    }
}
