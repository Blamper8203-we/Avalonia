using System;
using System.Collections.Generic;
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
/// ASCII-safe port of the SkiaSharp renderer from SingleLineDiagramControl.
/// </summary>
public partial class PdfSingleLineDiagramService
{
    public sealed class RenderCache
    {
        internal SchematicLayout? Layout { get; set; }
        internal Dictionary<int, byte[]?> FullPageImages { get; } = new();
    }

    private readonly IModuleTypeService _moduleTypeService;
    private const float CircuitImageScale = 4.0f;
    private const float FullPageScale = 8.0f;

    public PdfSingleLineDiagramService(IModuleTypeService moduleTypeService)
    {
        _moduleTypeService = moduleTypeService ?? throw new ArgumentNullException(nameof(moduleTypeService));
    }

    public RenderCache CreateRenderCache() => new();

    /// <summary>
    /// Renders the electrical circuits (vector-only: symbols and wires) to PNG.
    /// </summary>
    public static byte[]? RenderCircuitImage(SchematicLayout lay, int pageIndex, MainViewModel viewModel)
    {
        if (lay == null || lay.IsEmpty) return null;

        float pageW = (float)E.DrawW;
        
        // Reduce the height so we do not render the empty table area.
        float drawTop = (float)E.DrawT;
        float drawBottom = (float)E.YWireEnd + 50; 
        float actualH = drawBottom - drawTop;

        float scale = CircuitImageScale; // High export resolution.
        int imgW = Math.Max(1, (int)Math.Round(pageW * scale));
        int imgH = Math.Max(1, (int)Math.Round(actualH * scale));

        using var surface = SKSurface.Create(new SKImageInfo(imgW, imgH));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(scale);
        
        // Local offset: render from 0.
        canvas.Translate(0, -drawTop);

        DrawCircuitVectors(canvas, lay, pageIndex);

        return EncodeSurfaceAsPng(surface);
    }

    /// <summary>
    /// Draws only the circuit vectors (devices and wires) on the provided Skia canvas.
    /// Used both for PNG export (for PDF) and for the UI preview in SkiaRenderControl.
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
    /// Composes the single-line diagram page in QuestPDF (QuestPDF + Skia image hybrid).
    /// </summary>
    public void ComposeSingleLineDiagram(IContainer container, MainViewModel viewModel, RenderCache? renderCache = null)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(viewModel);

        var layout = renderCache?.Layout;
        if (layout == null)
        {
            var engine = new SchematicLayoutEngine(_moduleTypeService);
            layout = engine.BuildLayout(viewModel.Symbols, viewModel.CurrentProject);

            if (renderCache != null)
            {
                renderCache.Layout = layout;
            }
        }
        
        if (layout == null || layout.IsEmpty)
        {
            container.AlignCenter().AlignMiddle()
                .Text("Brak obwod\u00F3w do wy\u015Bwietlenia na schemacie jednokreskowym")
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
                
                byte[]? fullPageImg;
                if (renderCache != null && renderCache.FullPageImages.TryGetValue(pg, out var cachedImage))
                {
                    fullPageImg = cachedImage;
                }
                else
                {
                    // Render the full page as a single Skia image.
                    fullPageImg = RenderFullPage(layout, pg, viewModel);

                    if (renderCache != null)
                    {
                        renderCache.FullPageImages[pg] = fullPageImg;
                    }
                }

                if (fullPageImg != null)
                {
                    col.Item().Image(fullPageImg);
                }
            }
        });
    }

    /// <summary>
    /// Renders the full diagram page (template + circuits + table + title block) as PNG.
    /// </summary>
    private static byte[] RenderFullPage(SchematicLayout lay, int pageIndex, MainViewModel viewModel)
    {
        float w = (float)E.PageW;
        float h = (float)E.PageH;
        float scale = FullPageScale; // Increased resolution for crisp PDF output.

        // Swap width and height to get portrait orientation.
        using var surface = CreatePortraitPageSurface(w, h, scale);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        float yOff = ConfigurePortraitPageCanvas(canvas, h, scale, pageIndex);
        DrawFullPageLayers(canvas, lay, pageIndex, yOff);

        return EncodeSurfaceAsPng(surface);
    }

}
