using System;
using System.Collections.Generic;
using SkiaSharp;
using DINBoard.Models;
using DINBoard.Services;
using E = DINBoard.Services.SchematicLayoutEngine;

namespace DINBoard.Services.Pdf;

public partial class PdfSingleLineDiagramService
{
    private string CellText(SchematicNode n, string field)
    {
        var sym = n.Symbol;
        switch (field)
        {
            case "Designation": return sym?.ReferenceDesignation ?? n.Designation ?? "";
            case "Protection": return sym?.ProtectionType ?? n.Protection ?? "";
            case "CircuitName": return sym?.CircuitName ?? n.CircuitName ?? "";
            case "Location": return sym?.Location ?? n.Location ?? "";
            case "CableDesig": return (sym?.Parameters != null && sym.Parameters.TryGetValue("CableDesig", out var v1)) ? v1 : (n.CableDesig ?? "");
            case "CableType": return (sym?.Parameters != null && sym.Parameters.TryGetValue("CableType", out var v2)) ? v2 : (n.CableType ?? "");
            case "CableSpec": return (sym?.Parameters != null && sym.Parameters.TryGetValue("CableSpec", out var v3)) ? v3 : (n.CableSpec ?? "");
            case "CableLength": return (sym?.Parameters != null && sym.Parameters.TryGetValue("CableLength", out var v4)) ? v4 : (n.CableLength ?? "");
            case "PowerInfo": return (sym?.Parameters != null && sym.Parameters.TryGetValue("PowerInfo", out var v5)) ? v5 : (n.PowerInfo ?? "");
            default: return "";
        }
    }

    static void TblCell(SKCanvas c, string text, float x, float y, float w, SKColor color, bool bold = false)
    {
        if (string.IsNullOrEmpty(text)) return;

        float sz = (float)E.CellFontSize;
        using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", bold ? SKFontStyle.Bold : SKFontStyle.Normal), sz);
        using var paint = new SKPaint { Color = color, IsAntialias = true };

        string[] manualLines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var wrappedLines = new List<string>();

        float maxW = w - 4f;
        foreach (var line in manualLines)
        {
            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) continue;

            string currentLine = words[0];
            for (int i = 1; i < words.Length; i++)
            {
                string word = words[i];
                string testLine = currentLine + " " + word;
                if (font.MeasureText(testLine) <= maxW)
                {
                    currentLine = testLine;
                }
                else
                {
                    wrappedLines.Add(currentLine);
                    currentLine = word;
                }
            }
            wrappedLines.Add(currentLine);
        }

        float lineGap = 1.0f;
        float totalHeight = wrappedLines.Count * sz + (wrappedLines.Count - 1) * lineGap;
        float startY = y + ((float)E.RowH - totalHeight) / 2 + sz - (sz * 0.1f);

        for (int i = 0; i < wrappedLines.Count; i++)
        {
            string lineToDraw = wrappedLines[i];
            float tw = font.MeasureText(lineToDraw);

            if (tw > w)
            {
                while (lineToDraw.Length > 2 && font.MeasureText(lineToDraw + "...") > maxW)
                    lineToDraw = lineToDraw[..^1];
                lineToDraw += "...";
                tw = font.MeasureText(lineToDraw);
            }

            c.DrawText(lineToDraw, x + (w - tw) / 2, startY + i * (sz + lineGap), SKTextAlign.Left, font, paint);
        }
    }
}
