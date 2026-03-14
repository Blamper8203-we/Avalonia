using System;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SkiaSharp;
using DINBoard.Models;
using DINBoard.ViewModels;
using DINBoard.Services;
using E = DINBoard.Services.SchematicLayoutEngine;

namespace DINBoard.Services.Pdf;

/// <summary>
/// Generuje schemat jednokreskowy (single-line diagram) do PDF
/// â€” port SkiaSharp renderera z SingleLineDiagramControl.
/// </summary>
public partial class PdfSingleLineDiagramService
{
    private readonly IModuleTypeService _moduleTypeService;
    private const float CircuitImageScale = 4.0f;
    private const float FullPageScale = 8.0f;

    public PdfSingleLineDiagramService(IModuleTypeService moduleTypeService)
    {
        _moduleTypeService = moduleTypeService ?? throw new ArgumentNullException(nameof(moduleTypeService));
    }

    /// <summary>
    /// Renderuje obwody elektryczne schematu (tylko czÄ™Ĺ›Ä‡ wektorowa - symbole i kable) do PNG
    /// </summary>
    public static byte[]? RenderCircuitImage(SchematicLayout lay, int pageIndex, MainViewModel viewModel)
    {
        if (lay == null || lay.IsEmpty) return null;

        float pageW = (float)E.DrawW;
        
        // Zmniejszamy wysokoĹ›Ä‡ by nie rysowaÄ‡ pustego terenu tabelki
        float drawTop = (float)E.DrawT;
        float drawBottom = (float)E.YWireEnd + 50; 
        float actualH = drawBottom - drawTop;

        float scale = CircuitImageScale; // wysoka rozdzielczoĹ›Ä‡ do eksportu
        int imgW = Math.Max(1, (int)Math.Round(pageW * scale));
        int imgH = Math.Max(1, (int)Math.Round(actualH * scale));

        using var surface = SKSurface.Create(new SKImageInfo(imgW, imgH));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(scale);
        
        // PrzesuniÄ™cie lokalne - rysujemy od 0
        canvas.Translate(0, -drawTop);

        DrawCircuitVectors(canvas, lay, pageIndex);

        return EncodeSurfaceAsPng(surface);
    }

    /// <summary>
    /// Rysuje same wektory obwodĂłw (urzÄ…dzenia, kable) na podanym pĹ‚Ăłtnie Skia.
    /// UĹĽywane zarĂłwno do eksportu PNG (do PDF) jak i do podglÄ…du UI w SkiaRenderControl.
    /// </summary>
    public static void DrawCircuitVectors(SKCanvas canvas, SchematicLayout lay, int pageIndex, float yOffset = 0, bool drawDinRailAxis = true)
    {
        var pi = lay.Pages.Count > pageIndex ? lay.Pages[pageIndex] : null;
        var pageDev = lay.Devices.Where(d => d.Page == pageIndex).ToList();
        
        if (pi != null)
        {
            float pseudoYOff = yOffset;
            DrawMainBus(canvas, pi, pseudoYOff);
            DrawPathNumbers(canvas, lay, pageDev, pseudoYOff);
            foreach (var d in pageDev) DrawDevice(canvas, d, pi, pseudoYOff, drawDinRailAxis);
            DrawNPE(canvas, pi, pseudoYOff);
            DrawCableLabels(canvas, lay, pageIndex, pseudoYOff);

            if (pageIndex < lay.TotalPages - 1) DrawCont(canvas, pi, pseudoYOff, pageIndex + 2, true);
            if (pageIndex > 0) DrawCont(canvas, pi, pseudoYOff, pageIndex, false);
        }
    }

    /// <summary>
    /// Komponuje stronÄ™ schematu jednokreskowego w dokumencie QuestPDF (Hybryda: QuestPDF + Skia image).
    /// </summary>
    public void ComposeSingleLineDiagram(IContainer container, MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(viewModel);

        var engine = new SchematicLayoutEngine(_moduleTypeService);
        var layout = engine.BuildLayout(viewModel.Symbols, viewModel.CurrentProject);
        
        if (layout == null || layout.IsEmpty)
        {
            container.AlignCenter().AlignMiddle()
                .Text("Brak obwodĂłw do wyĹ›wietlenia na schemacie jednokreskowym")
                .FontSize(10).FontColor("#6B7280");
            return;
        }

        container.Column(col => 
        {
            for (int pg = 0; pg < layout.TotalPages; pg++)
            {
                if (pg > 0)
                {
                    col.Item().PageBreak();
                }
                
                // Renderujemy CAĹÄ„ stronÄ™ jako jeden obraz Skia
                var fullPageImg = RenderFullPage(layout, pg, viewModel);
                if (fullPageImg != null)
                {
                    col.Item().Image(fullPageImg);
                }
            }
        });
    }

    /// <summary>
    /// Renderuje peĹ‚nÄ… stronÄ™ schematu (szablon + obwody + tabelÄ™ + tabelkÄ™ rysunkowÄ…) jako obraz PNG.
    /// </summary>
    private static byte[] RenderFullPage(SchematicLayout lay, int pageIndex, MainViewModel viewModel)
    {
        float w = (float)E.PageW;
        float h = (float)E.PageH;
        float scale = FullPageScale; // ZwiÄ™kszona rozdzielczoĹ›Ä‡ dla idealnej ostroĹ›ci PDF

        // Zamieniamy szerokoĹ›Ä‡ z wysokoĹ›ciÄ…, by otrzymaÄ‡ obraz w orientacji pionowej (Portrait)
        using var surface = CreatePortraitPageSurface(w, h, scale);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        float yOff = ConfigurePortraitPageCanvas(canvas, h, scale, pageIndex);
        DrawFullPageLayers(canvas, lay, pageIndex, yOff);

        return EncodeSurfaceAsPng(surface);
    }

}
