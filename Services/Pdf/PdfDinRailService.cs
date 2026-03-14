using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;
using DINBoard.Models;
using DINBoard.ViewModels;
using DINBoard.Constants;
using DINBoard.Services;

namespace DINBoard.Services.Pdf;

public class PdfDinRailService
{
    private static readonly string PrimaryColor = "#1F2937";
    private static readonly string AccentBlue = "#3B82F6";
    private static readonly string AccentGreen = "#10B981";
    private static readonly string AccentOrange = "#D97706";
    private static readonly string AccentRed = "#EF4444";
    private static readonly string TextGray = "#6B7280";
    private const float UiToPdfScale = 0.75f;
    private readonly SymbolImportService _symbolImportService;
    private readonly SvgProcessor _svgProcessor;

    public PdfDinRailService(SymbolImportService symbolImportService, SvgProcessor svgProcessor)
    {
        _symbolImportService = symbolImportService ?? throw new ArgumentNullException(nameof(symbolImportService));
        _svgProcessor = svgProcessor ?? throw new ArgumentNullException(nameof(svgProcessor));
    }

    public void ComposeDinRailDiagram(IContainer container, MainViewModel viewModel, PdfExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(options);

        container.Decoration(decoration =>
        {
            decoration.Before().PaddingBottom(10).Column(column =>
            {
                column.Item().Text("Wizualizacja rozdzielnicy z rozmieszczeniem modułów:")
                    .FontSize(11 * UiToPdfScale).FontColor(TextGray);
            });

            decoration.After().PaddingTop(10).Column(column =>
            {
                column.Item().Text("Legenda:").FontSize(11 * UiToPdfScale).SemiBold();
                column.Item().Row(row =>
                {
                    row.Spacing(20);
                    row.AutoItem().Row(r => { r.AutoItem().Width(12).Height(12).Background(AccentBlue); r.AutoItem().PaddingLeft(5).Text("Faza L1").FontSize(9 * UiToPdfScale); });
                    row.AutoItem().Row(r => { r.AutoItem().Width(12).Height(12).Background(AccentOrange); r.AutoItem().PaddingLeft(5).Text("Faza L2").FontSize(9 * UiToPdfScale); });
                    row.AutoItem().Row(r => { r.AutoItem().Width(12).Height(12).Background(AccentRed); r.AutoItem().PaddingLeft(5).Text("Faza L3").FontSize(9 * UiToPdfScale); });
                    row.AutoItem().Row(r => { r.AutoItem().Width(12).Height(12).Background(AccentGreen); r.AutoItem().PaddingLeft(5).Text("RCD").FontSize(9 * UiToPdfScale); });
                });
            });

            decoration.Content().AlignCenter().AlignMiddle().Element(content =>
            {
                var imageData = RenderDinRailToImage(viewModel, options);
                if (imageData != null)
                {
                    content.Image(imageData).FitArea();
                }
                else
                {
                    content.Border(1).BorderColor(TextGray)
                        .Background("#F9FAFB").AlignCenter().AlignMiddle()
                        .Text("Brak modułów do wyświetlenia").FontColor(TextGray);
                }
            });
        });
    }

    public void ConfigurePage(PageDescriptor page)
    {
        ArgumentNullException.ThrowIfNull(page);
        page.Size(PageSizes.A4);
        page.Margin(40);
        page.DefaultTextStyle(x => x.FontSize(10 * UiToPdfScale).FontFamily("Segoe UI"));
    }

    public void ComposeHeader(IContainer container, string title)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(title);
        container.Row(row =>
        {
            row.RelativeItem()
                .Text(title)
                .FontSize(14 * UiToPdfScale).Bold().FontColor(PrimaryColor);
            row.AutoItem()
                .Text(DateTime.Now.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture))
                .FontSize(10 * UiToPdfScale).FontColor(TextGray);
        });
    }

    public void ComposeFooter(IContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);
        container.AlignCenter()
            .Text("Strona [page] z [totalPages]")
            .FontSize(9 * UiToPdfScale).FontColor(TextGray);
    }

    /// <summary>
    /// Renderuje canvas 1:1 do obrazu PNG.
    /// Koordynaty: szyna DIN centrowana wokół (0,0), moduły na niej.
    /// </summary>
    public byte[]? RenderDinRailToImage(MainViewModel viewModel, PdfExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(options);
        var stopwatch = Stopwatch.StartNew();
        var symbols = viewModel.Symbols.ToList();
        if (symbols.Count == 0) return null;

        // 1. Bounding box
        double bbMinX = symbols.Min(s => s.X);
        double bbMinY = symbols.Min(s => s.Y);
        double bbMaxX = symbols.Max(s => s.X + s.Width);
        double bbMaxY = symbols.Max(s => s.Y + s.Height);

        if (viewModel.Schematic.IsDinRailVisible && viewModel.Schematic.DinRailSize.Width > 0 && viewModel.Schematic.DinRailSize.Height > 0)
        {
            double railLeft = -viewModel.Schematic.DinRailSize.Width / 2;
            double railTop = -viewModel.Schematic.DinRailSize.Height / 2;
            double railRight = viewModel.Schematic.DinRailSize.Width / 2;
            double railBottom = viewModel.Schematic.DinRailSize.Height / 2;

            bbMinX = Math.Min(bbMinX, railLeft);
            bbMinY = Math.Min(bbMinY, railTop);
            bbMaxX = Math.Max(bbMaxX, railRight);
            bbMaxY = Math.Max(bbMaxY, railBottom);
        }

        const float margin = 40f;
        float contentWidth = (float)(bbMaxX - bbMinX);
        float contentHeight = (float)(bbMaxY - bbMinY);
        float totalWidth = contentWidth + margin * 2;
        float totalHeight = contentHeight + margin * 2;

        // 2. Skala
        float maxWidth, maxHeight, maxScale;
        long maxPixels;
        switch (options.PngQuality)
        {
            case PngRenderQuality.Standard:
                maxWidth = 4096f; maxHeight = 2304f; maxScale = 12f; maxPixels = 80_000_000; break;
            case PngRenderQuality.High:
                maxWidth = 8192f; maxHeight = 4608f; maxScale = 24f; maxPixels = 160_000_000; break;
            default:
                maxWidth = 15360f; maxHeight = 8640f; maxScale = 48f; maxPixels = 260_000_000; break;
        }

        float scale = Math.Min(maxWidth / totalWidth, maxHeight / totalHeight);
        if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0) scale = 1f;
        if (scale > maxScale) scale = maxScale;

        double rawPixels = (double)totalWidth * totalHeight * scale * scale;
        if (rawPixels > maxPixels && totalWidth > 0 && totalHeight > 0)
        {
            var scaleByPixels = (float)Math.Sqrt(maxPixels / ((double)totalWidth * totalHeight));
            if (scaleByPixels > 0 && scaleByPixels < scale) scale = scaleByPixels;
        }

        int imgWidth = Math.Max(1, (int)Math.Round(totalWidth * scale));
        int imgHeight = Math.Max(1, (int)Math.Round(totalHeight * scale));

        // 3. Canvas→Image transform: punkt (bbMinX,bbMinY) → (margin*scale, margin*scale)
        float offsetX = (float)(-bbMinX * scale + margin * scale);
        float offsetY = (float)(-bbMinY * scale + margin * scale);

        using var surface = SKSurface.Create(new SKImageInfo(imgWidth, imgHeight));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        // 4. Szyna DIN
        if (!string.IsNullOrEmpty(viewModel.Schematic.DinRailSvgContent))
        {
            try
            {
                var svgSource = Avalonia.Svg.Skia.SvgSource.LoadFromSvg(viewModel.Schematic.DinRailSvgContent);
                if (svgSource?.Picture != null)
                {
                    var picture = svgSource.Picture;
                    var pictureRect = picture.CullRect;

                    if (pictureRect.Width > 0 && pictureRect.Height > 0)
                    {
                        canvas.Save();
                        float railCanvasX = (float)(-viewModel.Schematic.DinRailSize.Width / 2);
                        float railCanvasY = (float)(-viewModel.Schematic.DinRailSize.Height / 2);
                        float railImgX = railCanvasX * scale + offsetX;
                        float railImgY = railCanvasY * scale + offsetY;

                        canvas.Translate(railImgX, railImgY);
                        float railScaleX = (float)(viewModel.Schematic.DinRailSize.Width * scale / pictureRect.Width);
                        float railScaleY = (float)(viewModel.Schematic.DinRailSize.Height * scale / pictureRect.Height);
                        canvas.Scale(railScaleX, railScaleY);
                        canvas.DrawPicture(picture);
                        canvas.Restore();
                    }
                }
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
        }

        // 5. Moduły — pozycje 1:1 z canvasem
        foreach (var symbol in symbols)
        {
            float x = (float)(symbol.X * scale) + offsetX;
            float y = (float)(symbol.Y * scale) + offsetY;
            float w = (float)(symbol.Width * scale);
            float h = (float)(symbol.Height * scale);
            DrawModule(canvas, x, y, w, h, symbol, viewModel.CurrentProjectPath);
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = data.ToArray();
        stopwatch.Stop();
        AppLog.Info($"PDF PNG render: quality={options.PngQuality} size={imgWidth}x{imgHeight} scale={scale:F3} bytes={bytes.Length} elapsedMs={stopwatch.ElapsedMilliseconds}");
        return bytes;
    }

    private void DrawModule(SKCanvas canvas, float x, float y, float w, float h, SymbolItem symbol, string? projectPath)
    {
        bool drawnFromVisual = false;

        try
        {
            string? warning;
            var path = _symbolImportService.ResolveVisualPath(symbol, projectPath, out warning);

            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
            {
                string svgContent = System.IO.File.ReadAllText(path);
                
                svgContent = _svgProcessor.ApplyParameters(svgContent, symbol.Parameters);
                svgContent = _svgProcessor.ApplyBlueCoverVisibility(svgContent, symbol.Parameters);
                
                var svgSource = Avalonia.Svg.Skia.SvgSource.LoadFromSvg(svgContent);
                if (svgSource?.Picture != null)
                {
                    var picture = svgSource.Picture;
                    var pictureRect = picture.CullRect;

                    if (pictureRect.Width > 0 && pictureRect.Height > 0)
                    {
                        canvas.Save();
                        canvas.Translate(x, y);

                        float scaleX = w / pictureRect.Width;
                        float scaleY = h / pictureRect.Height;
                        float scale = Math.Min(scaleX, scaleY);

                        float ox = (w - pictureRect.Width * scale) / 2;
                        float oy = (h - pictureRect.Height * scale) / 2;
                        canvas.Translate(ox, oy);
                        canvas.Scale(scale);

                        canvas.DrawPicture(picture);
                        canvas.Restore();
                        drawnFromVisual = true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"[PdfDinRailService] Błąd renderowania modułu {symbol.Label}: {ex.Message}");
        }

        if (!drawnFromVisual)
        {
            using var bgPaint = new SKPaint
            {
                Color = SKColor.Parse("#E5E7EB"),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawRoundRect(x, y, w, h, 4, 4, bgPaint);

            var phaseColor = symbol.Phase switch
            {
                "L1" => SKColor.Parse(DialogConstants.PhaseColors.L1),
                "L2" => SKColor.Parse(DialogConstants.PhaseColors.L2),
                "L3" => SKColor.Parse(DialogConstants.PhaseColors.L3),
                _ => SKColor.Parse("#9CA3AF")
            };
            using var phasePaint = new SKPaint
            {
                Color = phaseColor,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawRect(x, y, w, 6, phasePaint);

            using var borderPaint = new SKPaint
            {
                Color = SKColor.Parse("#374151"),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                IsAntialias = true
            };
            canvas.DrawRoundRect(x, y, w, h, 4, 4, borderPaint);

            using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), Math.Min(w * 0.3f, 12 * UiToPdfScale));
            using var textPaint = new SKPaint
            {
                Color = SKColor.Parse("#1F2937"),
                IsAntialias = true
            };
            var label = symbol.ProtectionType ?? symbol.Type ?? "";
            if (label.Length > 6) label = label[..6];
            canvas.DrawText(label, x + w / 2, y + h / 2 + font.Size / 3, SKTextAlign.Center, font, textPaint);
        }
    }
}
