using System;
using SkiaSharp;
using DINBoard.Models;
using E = DINBoard.Services.SchematicLayoutEngine;

namespace DINBoard.Services.Pdf;

public partial class PdfSingleLineDiagramService
{
    /// <summary>
    /// Rysuje tabelkę rysunkową (title block) bezpośrednio na kanwie Skia.
    /// Używane w podglądzie UI (SkiaRenderControl).
    /// </summary>
    public static void DrawSkiaTitleBlock(SKCanvas canvas, SchematicLayout lay, int pageNum, int totalPages, float yOffset, bool showPageNumbers = true)
    {
        // ... ramka tytułowa ...
        float tbW = (float)E.TitleW;
        float tbH = (float)E.TitleH;
        float tbX = (float)E.PageW - (float)E.FrameR - tbW;
        float tbY = (float)E.PageH - (float)E.FrameB - tbH + yOffset; // Apply yOffset here

        // Tło
        using var bg = new SKPaint { Color = CBoxBg, Style = SKPaintStyle.Fill };
        canvas.DrawRect(tbX, tbY, tbW, tbH, bg);

        // Ramka
        using var border = Stroke(CFrame, 0.8f);
        canvas.DrawRect(tbX, tbY, tbW, tbH, border);

        // Podział na sekcje pionowe
        float rowH = tbH / 6f; // 6 komórek żeby zmieścić inwestora i wykonawcę
        for (int i = 1; i < 6; i++)
        {
            float ly = tbY + i * rowH;
            canvas.DrawLine(tbX, ly, tbX + tbW, ly, border);
        }

        var p = lay.Project;
        var meta = p?.Metadata;
        string drawNum = meta?.ProjectNumber ?? "E-SCH-001";
        string title = GetProjectObjectName(p);
        string contractor = GetContractorName(meta);
        string investor = meta?.Investor ?? "---";
        string address = meta?.Address ?? "---";
        string date = (meta?.DateModified ?? DateTime.UtcNow).ToString("yyyy-MM-dd");
        string sheet = $"{pageNum} / {totalPages}";
        string designer = meta?.Author ?? "---";

        // Sekcja 1: Tytuł, Obiekt i Adres
        Txt(canvas, title, tbX + 3, tbY + 2, 6, CTxtLbl, true);
        Txt(canvas, $"Inwestor: {investor}", tbX + 3, tbY + 12, 4, CTxtDim);
        Txt(canvas, $"Adres: {address}", tbX + 3, tbY + 20, 4, CTxtDim);

        // Sekcja 2: Twórca / Wykonawca
        float s2Y = tbY + rowH;
        Txt(canvas, "Wykonawca:", tbX + 3, s2Y + 2, 4, CTxtLbl);
        Txt(canvas, contractor, tbX + 3, s2Y + 10, 5, CTxt, true);
        Txt(canvas, $"Projektant: {designer}", tbX + 3, s2Y + 20, 4, CTxtDim);

        // Sekcja 3: Nr rysunku
        float s3Y = tbY + 2 * rowH;
        Txt(canvas, "Nr rys.:", tbX + 3, s3Y + 2, 5, CTxtLbl);
        Txt(canvas, drawNum, tbX + 3, s3Y + 12, 6, CTxt, true);

        // Sekcja 4: Data
        float s4Y = tbY + 3 * rowH;
        Txt(canvas, "Data:", tbX + 3, s4Y + 2, 5, CTxtLbl);
        Txt(canvas, date, tbX + 3, s4Y + 12, 6, CTxtDim);

        // Sekcja 5: Arkusz
        float s5Y = tbY + 4 * rowH;
        Txt(canvas, "Arkusz:", tbX + 3, s5Y + 2, 5, CTxtLbl);
        if (showPageNumbers)
        {
            Txt(canvas, sheet, tbX + 3, s5Y + 12, 7, CTxt, true);
        }

        // Sekcja 6: Norma
        float s6Y = tbY + 5 * rowH;
        Txt(canvas, "PN-EN 60617", tbX + 3, s6Y + 6, 5, CTxtLbl);
    }
}
