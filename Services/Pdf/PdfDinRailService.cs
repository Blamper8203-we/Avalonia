using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using DINBoard.Constants;
using DINBoard.Models;
using DINBoard.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;

namespace DINBoard.Services.Pdf;

public class PdfDinRailService
{
    private const float GroupFramePadding = 6f;
    private const float GroupFrameBorderThickness = 1.5f;
    private const float GroupFrameCornerRadius = 4f;
    private const float GroupLabelCornerRadius = 3f;
    private const float GroupLabelOffsetX = 4f;
    private const float GroupLabelOffsetY = -10f;
    private const float GroupLabelPaddingX = 5f;
    private const float GroupLabelPaddingY = 1f;

    private const float DesignationBadgeOffsetBottom = 12f;
    private const float DesignationTextVerticalInset = 1f;
    private const float DesignationBadgeFontSize = 7f;

    private static readonly SKColor GroupFrameBorderColor = SKColor.Parse("#883B82F6");
    private static readonly SKColor GroupFrameFillColor = SKColor.Parse("#0D3B82F6");
    private static readonly SKColor GroupLabelFillColor = SKColor.Parse("#CC3B82F6");
    private static readonly SKColor GroupLabelTextColor = SKColors.White;
    private static readonly SKColor DesignationBadgeTextColor = SKColors.Black;

    public sealed class RenderCache
    {
        internal Dictionary<string, string?> ResolvedVisualPaths { get; } = new(StringComparer.Ordinal);
        internal Dictionary<ModuleVisualCacheKey, SKPicture?> ModulePictures { get; } = new();
        internal Dictionary<DinRailImageCacheKey, byte[]?> RenderedImages { get; } = new();
        internal string? DinRailSvgContent { get; set; }
        internal SKPicture? DinRailPicture { get; set; }
    }

    internal readonly record struct ModuleVisualCacheKey(string VisualPath, string ParameterSignature);
    internal readonly record struct DinRailImageCacheKey(PngRenderQuality Quality, bool ShowNumbers, bool ShowGroups);

    private static readonly string PrimaryColor = "#1F2937";
    private static readonly string TextGray = "#6B7280";
    private const float UiToPdfScale = 0.75f;

    private readonly SymbolImportService _symbolImportService;
    private readonly SvgProcessor _svgProcessor;

    public PdfDinRailService(SymbolImportService symbolImportService, SvgProcessor svgProcessor)
    {
        _symbolImportService = symbolImportService ?? throw new ArgumentNullException(nameof(symbolImportService));
        _svgProcessor = svgProcessor ?? throw new ArgumentNullException(nameof(svgProcessor));
    }

    public RenderCache CreateRenderCache() => new();

    public void ComposeDinRailDiagram(
        IContainer container,
        MainViewModel viewModel,
        PdfExportOptions options,
        bool showNumbers = false,
        bool showGroups = false,
        RenderCache? renderCache = null)
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

            decoration.Content().AlignCenter().AlignMiddle().Element(content =>
            {
                var imageData = RenderDinRailToImage(viewModel, options, showNumbers, showGroups, renderCache);
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
        page.PageColor(Colors.White);
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
        container.AlignCenter().Text(text =>
        {
            text.Span("Strona ").FontSize(9 * UiToPdfScale).FontColor(PrimaryColor);
            text.CurrentPageNumber().FontSize(9 * UiToPdfScale).FontColor(PrimaryColor);
            text.Span(" z ").FontSize(9 * UiToPdfScale).FontColor(PrimaryColor);
            text.TotalPages().FontSize(9 * UiToPdfScale).FontColor(PrimaryColor);
        });
    }

    /// <summary>
    /// Renderuje canvas 1:1 do obrazu PNG.
    /// Koordynaty: szyna DIN centrowana wokol (0,0), moduly na niej.
    /// </summary>
    public byte[]? RenderDinRailToImage(
        MainViewModel viewModel,
        PdfExportOptions options,
        bool showNumbers = false,
        bool showGroups = false,
        RenderCache? renderCache = null)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(options);

        var imageCacheKey = new DinRailImageCacheKey(options.PngQuality, showNumbers, showGroups);
        if (renderCache != null && renderCache.RenderedImages.TryGetValue(imageCacheKey, out var cachedImage))
        {
            return cachedImage;
        }

        var stopwatch = Stopwatch.StartNew();
        var symbols = viewModel.Symbols.ToList();
        if (symbols.Count == 0)
        {
            if (renderCache != null)
            {
                renderCache.RenderedImages[imageCacheKey] = null;
            }

            return null;
        }

        double bbMinX = symbols.Min(s => s.X);
        double bbMinY = symbols.Min(s => s.Y);
        double bbMaxX = symbols.Max(s => s.X + s.Width);
        double bbMaxY = symbols.Max(s => s.Y + s.Height);

        if (viewModel.Schematic.IsDinRailVisible &&
            viewModel.Schematic.DinRailSize.Width > 0 &&
            viewModel.Schematic.DinRailSize.Height > 0)
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

        ExpandBoundsForAnnotations(
            symbols,
            showNumbers,
            showGroups,
            ref bbMinX,
            ref bbMinY,
            ref bbMaxX,
            ref bbMaxY);

        if (viewModel.Schematic.IsDinRailVisible &&
            viewModel.Schematic.DinRailSize.Width > 0 &&
            viewModel.Schematic.DinRailSize.Height > 0)
        {
            CenterBoundsAroundDinRail(ref bbMinX, ref bbMinY, ref bbMaxX, ref bbMaxY);
        }

        const float margin = 40f;
        float contentWidth = (float)(bbMaxX - bbMinX);
        float contentHeight = (float)(bbMaxY - bbMinY);
        float totalWidth = contentWidth + margin * 2;
        float totalHeight = contentHeight + margin * 2;

        float maxWidth;
        float maxHeight;
        float maxScale;
        long maxPixels;

        switch (options.PngQuality)
        {
            case PngRenderQuality.Standard:
                maxWidth = 4096f;
                maxHeight = 2304f;
                maxScale = 12f;
                maxPixels = 80_000_000;
                break;
            case PngRenderQuality.High:
                maxWidth = 8192f;
                maxHeight = 4608f;
                maxScale = 24f;
                maxPixels = 160_000_000;
                break;
            default:
                maxWidth = 15360f;
                maxHeight = 8640f;
                maxScale = 48f;
                maxPixels = 260_000_000;
                break;
        }

        float scale = Math.Min(maxWidth / totalWidth, maxHeight / totalHeight);
        if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0)
        {
            scale = 1f;
        }

        if (scale > maxScale)
        {
            scale = maxScale;
        }

        double rawPixels = (double)totalWidth * totalHeight * scale * scale;
        if (rawPixels > maxPixels && totalWidth > 0 && totalHeight > 0)
        {
            var scaleByPixels = (float)Math.Sqrt(maxPixels / ((double)totalWidth * totalHeight));
            if (scaleByPixels > 0 && scaleByPixels < scale)
            {
                scale = scaleByPixels;
            }
        }

        int imgWidth = Math.Max(1, (int)Math.Round(totalWidth * scale));
        int imgHeight = Math.Max(1, (int)Math.Round(totalHeight * scale));

        float offsetX = (float)(-bbMinX * scale + margin * scale);
        float offsetY = (float)(-bbMinY * scale + margin * scale);

        using var surface = SKSurface.Create(new SKImageInfo(imgWidth, imgHeight));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        var dinRailPicture = GetDinRailPicture(viewModel, renderCache);
        if (dinRailPicture != null)
        {
            var pictureRect = dinRailPicture.CullRect;
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
                canvas.Translate(-pictureRect.Left, -pictureRect.Top);
                canvas.DrawPicture(dinRailPicture);
                canvas.Restore();
            }
        }

        if (showGroups)
        {
            DrawGroupFrames(canvas, symbols, scale, offsetX, offsetY);
        }

        foreach (var symbol in symbols)
        {
            float x = (float)(symbol.X * scale) + offsetX;
            float y = (float)(symbol.Y * scale) + offsetY;
            float w = (float)(symbol.Width * scale);
            float h = (float)(symbol.Height * scale);

            DrawModule(canvas, x, y, w, h, symbol, viewModel.CurrentProjectPath, showNumbers, renderCache);
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = data.ToArray();

        stopwatch.Stop();
        AppLog.Info($"PDF PNG render: quality={options.PngQuality} size={imgWidth}x{imgHeight} scale={scale:F3} bytes={bytes.Length} elapsedMs={stopwatch.ElapsedMilliseconds}");

        if (renderCache != null)
        {
            renderCache.RenderedImages[imageCacheKey] = bytes;
        }

        return bytes;
    }

    private SKPicture? GetDinRailPicture(MainViewModel viewModel, RenderCache? renderCache)
    {
        var dinRailSvgContent = viewModel.Schematic.DinRailSvgContent;
        if (string.IsNullOrEmpty(dinRailSvgContent))
        {
            return null;
        }

        if (renderCache != null &&
            string.Equals(renderCache.DinRailSvgContent, dinRailSvgContent, StringComparison.Ordinal) &&
            renderCache.DinRailPicture != null)
        {
            return renderCache.DinRailPicture;
        }

        try
        {
            var svgSource = Avalonia.Svg.Skia.SvgSource.LoadFromSvg(dinRailSvgContent);
            var picture = svgSource?.Picture;

            if (renderCache != null)
            {
                renderCache.DinRailSvgContent = dinRailSvgContent;
                renderCache.DinRailPicture = picture;
            }

            return picture;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private string? GetResolvedVisualPath(SymbolItem symbol, string? projectPath, RenderCache? renderCache)
    {
        if (renderCache != null && renderCache.ResolvedVisualPaths.TryGetValue(symbol.Id, out var cachedPath))
        {
            return cachedPath;
        }

        var path = _symbolImportService.ResolveVisualPath(symbol, projectPath, out _);

        if (renderCache != null)
        {
            renderCache.ResolvedVisualPaths[symbol.Id] = path;
        }

        return path;
    }

    private static string BuildParameterSignature(SymbolItem symbol)
    {
        if (symbol.Parameters == null || symbol.Parameters.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("|", symbol.Parameters
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .Select(kvp => $"{kvp.Key}={kvp.Value}"));
    }

    private SKPicture? GetModulePicture(SymbolItem symbol, string? projectPath, RenderCache? renderCache)
    {
        var path = GetResolvedVisualPath(symbol, projectPath, renderCache);
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var cacheKey = new ModuleVisualCacheKey(path, BuildParameterSignature(symbol));
        if (renderCache != null && renderCache.ModulePictures.TryGetValue(cacheKey, out var cachedPicture))
        {
            return cachedPicture;
        }

        if (!File.Exists(path))
        {
            if (renderCache != null)
            {
                renderCache.ModulePictures[cacheKey] = null;
            }

            return null;
        }

        try
        {
            var svgContent = File.ReadAllText(path);
            svgContent = _svgProcessor.ApplyParameters(svgContent, symbol.Parameters);
            svgContent = _svgProcessor.ApplyBlueCoverVisibility(svgContent, symbol.Parameters);

            var svgSource = Avalonia.Svg.Skia.SvgSource.LoadFromSvg(svgContent);
            var picture = svgSource?.Picture;

            if (renderCache != null)
            {
                renderCache.ModulePictures[cacheKey] = picture;
            }

            return picture;
        }
        catch (Exception ex)
        {
            AppLog.Warn($"[PdfDinRailService] Błąd renderowania modułu {symbol.Label}: {ex.Message}");

            if (renderCache != null)
            {
                renderCache.ModulePictures[cacheKey] = null;
            }

            return null;
        }
    }

    private void DrawModule(
        SKCanvas canvas,
        float x,
        float y,
        float w,
        float h,
        SymbolItem symbol,
        string? projectPath,
        bool showNumbers,
        RenderCache? renderCache)
    {
        bool drawnFromVisual = false;

        var picture = GetModulePicture(symbol, projectPath, renderCache);
        if (picture != null)
        {
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
                canvas.Translate(-pictureRect.Left, -pictureRect.Top);

                canvas.DrawPicture(picture);
                canvas.Restore();
                drawnFromVisual = true;
            }
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

            float fallbackScale = h / (float)symbol.Height;
            using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), Math.Min(w * 0.3f, 24f * fallbackScale));
            using var textPaint = new SKPaint
            {
                Color = SKColor.Parse("#1F2937"),
                IsAntialias = true
            };

            var label = symbol.ProtectionType ?? symbol.Type ?? string.Empty;
            if (label.Length > 6)
            {
                label = label[..6];
            }

            canvas.DrawText(label, x + w / 2, y + h / 2 + font.Size / 3, SKTextAlign.Center, font, textPaint);
        }

        if (showNumbers)
        {
            float currentScale = h / (float)symbol.Height;

            DrawReferenceDesignationBadge(canvas, symbol, x, y, w, h, currentScale);
        }
    }

    private static void DrawGroupFrames(
        SKCanvas canvas,
        List<SymbolItem> symbols,
        float scale,
        float offsetX,
        float offsetY)
    {
        var groups = symbols
            .Where(s => !string.IsNullOrWhiteSpace(s.Group))
            .GroupBy(s => s.Group!, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            var groupedSymbols = group.ToList();
            if (groupedSymbols.Count == 0)
            {
                continue;
            }

            var minX = groupedSymbols.Min(s => s.X) - GroupFramePadding;
            var minY = groupedSymbols.Min(s => s.Y) - GroupFramePadding;
            var maxX = groupedSymbols.Max(s => s.X + s.Width) + GroupFramePadding;
            var maxY = groupedSymbols.Max(s => s.Y + s.Height) + GroupFramePadding;

            var x = (float)(minX * scale) + offsetX;
            var y = (float)(minY * scale) + offsetY;
            var w = (float)((maxX - minX) * scale);
            var h = (float)((maxY - minY) * scale);

            using var frameFill = new SKPaint
            {
                Color = GroupFrameFillColor,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            using var frameBorder = new SKPaint
            {
                Color = GroupFrameBorderColor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = GroupFrameBorderThickness * scale,
                IsAntialias = true
            };

            var frameRadius = GroupFrameCornerRadius * scale;
            canvas.DrawRoundRect(x, y, w, h, frameRadius, frameRadius, frameFill);
            canvas.DrawRoundRect(x, y, w, h, frameRadius, frameRadius, frameBorder);

            var groupName = groupedSymbols.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.GroupName))?.GroupName ?? group.Key;
            if (string.IsNullOrWhiteSpace(groupName))
            {
                continue;
            }

            var fontSize = Math.Max(1f, 9f * scale);
            using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), fontSize);
            using var textPaint = new SKPaint
            {
                Color = GroupLabelTextColor,
                IsAntialias = true
            };

            var textWidth = font.MeasureText(groupName);
            var metrics = font.Metrics;
            var textHeight = metrics.Descent - metrics.Ascent;
            var padX = GroupLabelPaddingX * scale;
            var padY = GroupLabelPaddingY * scale;

            var labelWidth = textWidth + 2 * padX;
            var labelHeight = textHeight + 2 * padY;
            var labelX = x + GroupLabelOffsetX * scale;
            var labelY = y + GroupLabelOffsetY * scale;

            using var labelFill = new SKPaint
            {
                Color = GroupLabelFillColor,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            var labelRadius = GroupLabelCornerRadius * scale;
            canvas.DrawRoundRect(labelX, labelY, labelWidth, labelHeight, labelRadius, labelRadius, labelFill);

            var textX = labelX + padX;
            var textY = labelY + padY - metrics.Ascent;
            canvas.DrawText(groupName, textX, textY, SKTextAlign.Left, font, textPaint);
        }
    }

    private static void DrawReferenceDesignationBadge(
        SKCanvas canvas,
        SymbolItem symbol,
        float x,
        float y,
        float w,
        float h,
        float currentScale)
    {
        if (string.IsNullOrWhiteSpace(symbol.ReferenceDesignation))
        {
            return;
        }

        var text = symbol.ReferenceDesignation;
        var fontSize = Math.Max(1f, DesignationBadgeFontSize * currentScale);
        using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), fontSize);
        using var textPaint = new SKPaint
        {
            Color = DesignationBadgeTextColor,
            IsAntialias = true
        };

        var metrics = font.Metrics;
        var textX = x + (w / 2f);
        var textY = y + h + (DesignationBadgeOffsetBottom * currentScale) - (metrics.Descent + (DesignationTextVerticalInset * currentScale));
        canvas.DrawText(text, textX, textY, SKTextAlign.Center, font, textPaint);
    }

    private static void ExpandBoundsForAnnotations(
        List<SymbolItem> symbols,
        bool showNumbers,
        bool showGroups,
        ref double bbMinX,
        ref double bbMinY,
        ref double bbMaxX,
        ref double bbMaxY)
    {
        if (!showNumbers && !showGroups)
        {
            return;
        }

        using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), DesignationBadgeFontSize);
        var metrics = font.Metrics;
        var textHeight = metrics.Descent - metrics.Ascent;

        if (showGroups)
        {
            var groups = symbols
                .Where(s => !string.IsNullOrWhiteSpace(s.Group))
                .GroupBy(s => s.Group!, StringComparer.Ordinal);

            foreach (var group in groups)
            {
                var groupedSymbols = group.ToList();
                if (groupedSymbols.Count == 0)
                {
                    continue;
                }

                var frameMinX = groupedSymbols.Min(s => s.X) - GroupFramePadding;
                var frameMinY = groupedSymbols.Min(s => s.Y) - GroupFramePadding;
                var frameMaxX = groupedSymbols.Max(s => s.X + s.Width) + GroupFramePadding;
                var frameMaxY = groupedSymbols.Max(s => s.Y + s.Height) + GroupFramePadding;

                bbMinX = Math.Min(bbMinX, frameMinX);
                bbMinY = Math.Min(bbMinY, frameMinY);
                bbMaxX = Math.Max(bbMaxX, frameMaxX);
                bbMaxY = Math.Max(bbMaxY, frameMaxY);

                var groupName = groupedSymbols.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.GroupName))?.GroupName ?? group.Key;
                if (string.IsNullOrWhiteSpace(groupName))
                {
                    continue;
                }

                var labelWidth = font.MeasureText(groupName) + (2 * GroupLabelPaddingX);
                var labelHeight = textHeight + (2 * GroupLabelPaddingY);
                var labelX = frameMinX + GroupLabelOffsetX;
                var labelY = frameMinY + GroupLabelOffsetY;

                bbMinX = Math.Min(bbMinX, labelX);
                bbMinY = Math.Min(bbMinY, labelY);
                bbMaxX = Math.Max(bbMaxX, labelX + labelWidth);
                bbMaxY = Math.Max(bbMaxY, labelY + labelHeight);
            }
        }

        if (showNumbers)
        {
            foreach (var symbol in symbols)
            {
                if (string.IsNullOrWhiteSpace(symbol.ReferenceDesignation))
                {
                    continue;
                }

                var textWidth = font.MeasureText(symbol.ReferenceDesignation);
                var textX = symbol.X + ((symbol.Width - textWidth) / 2f);
                var textY = symbol.Y + symbol.Height + DesignationBadgeOffsetBottom - (metrics.Descent + DesignationTextVerticalInset);
                var textTop = textY + metrics.Ascent;
                var textBottom = textY + metrics.Descent;

                bbMinX = Math.Min(bbMinX, textX);
                bbMinY = Math.Min(bbMinY, textTop);
                bbMaxX = Math.Max(bbMaxX, textX + textWidth);
                bbMaxY = Math.Max(bbMaxY, textBottom);
            }
        }
    }

    private static void CenterBoundsAroundDinRail(
        ref double bbMinX,
        ref double bbMinY,
        ref double bbMaxX,
        ref double bbMaxY)
    {
        var halfWidth = Math.Max(Math.Abs(bbMinX), Math.Abs(bbMaxX));
        var halfHeight = Math.Max(Math.Abs(bbMinY), Math.Abs(bbMaxY));

        bbMinX = -halfWidth;
        bbMaxX = halfWidth;
        bbMinY = -halfHeight;
        bbMaxY = halfHeight;
    }
}
