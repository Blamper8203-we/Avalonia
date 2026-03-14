using SkiaSharp;
using DINBoard.Services;
using E = DINBoard.Services.SchematicLayoutEngine;

namespace DINBoard.Services.Pdf;

public partial class PdfSingleLineDiagramService
{
    public static void DrawPageTemplate(SKCanvas canvas, float yOffset = 0, bool drawGrid = true)
    {
        float fl = (float)E.FrameL;
        float ft = (float)E.FrameT + yOffset;
        float fw = (float)(E.PageW - E.FrameL - E.FrameR);
        float fh = (float)(E.PageH - E.FrameT - E.FrameB);

        // BiaĹ‚e tĹ‚o strony
        using var bgPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
        canvas.DrawRect(0, yOffset, (float)E.PageW, (float)E.PageH, bgPaint);

        // Ramka gĹ‚Ăłwna
        using var framePen = Stroke(CFrame, 1.5f);
        canvas.DrawRect(fl, ft, fw, fh, framePen);
        
        // Ramka dla tytuĹ‚u
        canvas.DrawRect((float)E.DrawR, (float)(E.PageH - E.FrameB - E.TitleH) + yOffset, (float)E.TitleW, (float)E.TitleH, framePen);

        // Siatka i markery
        using var gridPen = Stroke(CFrame, 0.5f);
        using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 10);
        using var txtPaint = new SKPaint { Color = CFrame, IsAntialias = true };

        // Kolumny (1..8)
        float colSpace = fw / E.GridCols;
        for (int i = 0; i < E.GridCols; i++)
        {
            float cx = fl + i * colSpace;
            if (i > 0)
            {
                canvas.DrawLine(cx, ft, cx, ft + 6, gridPen);
                canvas.DrawLine(cx, ft + fh, cx, ft + fh - 6, gridPen);
            }
            float textW = font.MeasureText((i + 1).ToString());
            canvas.DrawText((i + 1).ToString(), cx + colSpace / 2 - textW / 2, ft - 4, SKTextAlign.Left, font, txtPaint);
            canvas.DrawText((i + 1).ToString(), cx + colSpace / 2 - textW / 2, ft + fh + 12, SKTextAlign.Left, font, txtPaint);
        }

        // RzÄ™dy (A..F)
        float rowSpace = fh / E.GridRows;
        for (int i = 0; i < E.GridRows; i++)
        {
            float cy = ft + i * rowSpace;
            if (i > 0)
            {
                canvas.DrawLine(fl, cy, fl + 6, cy, gridPen);
                canvas.DrawLine(fl + fw, cy, fl + fw - 6, cy, gridPen);
            }
            char letter = (char)('A' + i);
            canvas.DrawText(letter.ToString(), fl + fw + 6, cy + rowSpace / 2 + 4, SKTextAlign.Left, font, txtPaint);
        }
    }
}
