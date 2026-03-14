using System;
using System.Collections.Generic;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;
using DINBoard.Models;
using DINBoard.ViewModels;
using DINBoard.Constants;
using DINBoard.Services;

namespace DINBoard.Services.Pdf;

/// <summary>
/// Serwis odpowiedzialny za generowanie schematów obwodów i przewodów
/// </summary>
public class PdfCircuitWiringService
{
    private static readonly string TextGray = "#6B7280";
    private const float UiToPdfScale = 0.75f;
    private readonly IModuleTypeService _moduleTypeService;

    public PdfCircuitWiringService(IModuleTypeService moduleTypeService)
    {
        _moduleTypeService = moduleTypeService ?? throw new ArgumentNullException(nameof(moduleTypeService));
    }

    private static SKColor GetPhaseColor(string? phase)
    {
        var phaseKey = phase?.ToUpperInvariant() switch
        {
            "L1" => "L1",
            "L2" => "L2",
            "L3" => "L3",
            "N" => "N",
            "PE" => "PE",
            "L1+L2+L3" or "3F" => "L1",
            _ => "L1"
        };
        return WirePalette.GetSkia(phaseKey);
    }

    // Layout constants
    private const float PadX = 50f;
    private const float PadY = 20f;
    private const float LineSpacing = 30f;
    private const float VerticalLineHeight = 30f;
    private const float LabelHeight = 100f;
    private const float CircleRadius = 12f;
    private const float GroupSpacing = 40f;
    // Scale handled dynamically

 

    private static string GetPhaseLabel(string? phase)
    {
        return phase?.ToUpperInvariant() switch
        {
            "L1" => "L1",
            "L2" => "L2",
            "L3" => "L3",
            "L1+L2+L3" or "3F" => "3F",
            _ => "L1"
        };
    }

    private static bool IsThreePhase(string? phase)
    {
        var p = phase?.ToUpperInvariant();
        return p == "L1+L2+L3" || p == "3F";
    }

    /// <summary>
    /// Generuje schemat obwodów (Sheet 2)
    /// </summary>
    public void ComposeCircuitWiringDiagram(IContainer container, MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(viewModel);

        container.Column(column =>
        {
            column.Spacing(10);

            column.Item().Text("Schemat połączeń obwodów z rozdzielnicy (Arkusz 2):")
                .FontSize(11 * UiToPdfScale).FontColor(TextGray);

            // Render circuit diagram as image
            var imageData = RenderCircuitWiringToImage(viewModel);
            if (imageData != null)
            {
                column.Item().Image(imageData).FitArea();
            }
            else
            {
                column.Item().Border(1).BorderColor(TextGray)
                    .Background("#F9FAFB").AlignCenter().Padding(20)
                    .Text("Brak obwodów do wyświetlenia").FontColor(TextGray);
            }
        });
    }

    /// <summary>
    /// Renderuje schemat obwodów do obrazu PNG (układ schematyczny).
    /// </summary>
    public byte[]? RenderCircuitWiringToImage(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        try
        {
            var references = viewModel.Schematic.CircuitReferences.OrderBy(r => r.CircuitNumber).ToList();
            if (references.Count == 0) return null;

            // Calculate total dimensions based on schematic layout
            float totalHeight = 0;
            float maxWidth = 100; // Minimum width

            foreach (var reference in references)
            {
                var mcbCount = reference.Circuits.Count(c => c.LinkedSymbol != null);
                if (mcbCount == 0) mcbCount = 1;

                float refWidth = PadX * 2 + (mcbCount - 1) * LineSpacing + 80;
                float refHeight = PadY + VerticalLineHeight + LabelHeight + 20;

                maxWidth = Math.Max(maxWidth, refWidth);
                totalHeight += refHeight + GroupSpacing;
            }

            // Ensure minimum dimensions
            if (totalHeight < 100) totalHeight = 100;

            // Dynamic scaling logic
            const float MaxDimension = 8000f;
            float width = maxWidth;
            float height = totalHeight;
            float targetScale = 3f; // Default high quality

            if (width * targetScale > MaxDimension) targetScale = MaxDimension / width;
            if (height * targetScale > MaxDimension) targetScale = MaxDimension / height;

            targetScale = Math.Max(targetScale, 0.1f);

            int imgWidth = (int)(width * targetScale);
            int imgHeight = (int)(height * targetScale);

            if (imgWidth <= 0 || imgHeight <= 0) return null;

            using var surface = SKSurface.Create(new SKImageInfo(imgWidth, imgHeight));

            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);
            canvas.Scale(targetScale);

            float currentY = 0;

            foreach (var reference in references)
            {
                RenderSingleCircuitReference(canvas, reference, currentY);

                // Advance Y position
                var mcbCount = reference.Circuits.Count(c => c.LinkedSymbol != null);
                if (mcbCount == 0) mcbCount = 1;
                float refHeight = PadY + VerticalLineHeight + LabelHeight + 20;

                currentY += refHeight + GroupSpacing;
            }

            using var image = surface.Snapshot();
            if (image == null) return null;

            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data?.ToArray();
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

    private void RenderSingleCircuitReference(SKCanvas canvas, CircuitReference reference, float offsetY)
    {
        // MCB sorting logic from original
        var allCircuits = reference.Circuits
            .Where(c => c.LinkedSymbol != null)
            .OrderBy(c =>
            {
                var sym = c.LinkedSymbol!;
                bool isRcd = _moduleTypeService.IsRcd(sym);
                return isRcd ? 1000 : -sym.ModuleNumber;
            })
            .ToList();

        if (allCircuits.Count == 0) return;

        // Get group information
        var firstSymbol = allCircuits.FirstOrDefault()?.LinkedSymbol;
        string groupName = firstSymbol?.GroupName ?? $"Grupa {reference.CircuitNumber}";

        var rcdCircuit = allCircuits.FirstOrDefault(c => _moduleTypeService.IsRcd(c.LinkedSymbol));

        string groupPhase = rcdCircuit?.LinkedSymbol?.Phase ?? firstSymbol?.Phase ?? "L1";
        bool isThreePhaseGroup = IsThreePhase(groupPhase);

        float baseY = offsetY + PadY + 30;
        float hY = baseY;
        float wireBottomY = baseY + VerticalLineHeight;

        // Find last MCB index
        int lastMcbIndex = allCircuits.Count - 2;
        if (lastMcbIndex < 0) lastMcbIndex = 0;

        // 1. Draw Group Header
        float headerX = 5;
        float headerY = offsetY + 5;

        using var headerBgPaint = new SKPaint { Color = SKColor.Parse("#2D2D2D"), Style = SKPaintStyle.Fill, IsAntialias = true };
        using var headerTextPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };

        using var headerRect = new SKRoundRect(new SKRect(headerX, headerY, headerX + 120, headerY + 22), 4);
        canvas.DrawRoundRect(headerRect, headerBgPaint);

        using var headerFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 14 * UiToPdfScale);
        canvas.DrawText(groupName, headerX + 8, headerY + 15, headerFont, headerTextPaint);

        // Phase badge
        var phaseColor = GetPhaseColor(groupPhase);
        using var phaseBgPaint = new SKPaint { Color = phaseColor, Style = SKPaintStyle.Fill, IsAntialias = true };

        if (isThreePhaseGroup)
        {
            // 3-phase group - show "3F" label
            using var phaseRect = new SKRoundRect(new SKRect(headerX + 80, headerY + 3, headerX + 115, headerY + 19), 3);
            canvas.DrawRoundRect(phaseRect, phaseBgPaint);
            using var phaseFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 8 * UiToPdfScale);
            canvas.DrawText("3F", headerX + 88, headerY + 14, phaseFont, headerTextPaint);
        }
        else
        {
            // Single phase - show phase label
            using var phaseRect = new SKRoundRect(new SKRect(headerX + 80, headerY + 3, headerX + 115, headerY + 19), 3);
            canvas.DrawRoundRect(phaseRect, phaseBgPaint);
            using var phaseFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 8 * UiToPdfScale);
            canvas.DrawText(GetPhaseLabel(groupPhase), headerX + 88, headerY + 14, phaseFont, headerTextPaint);
        }

        // 2. Draw Reference Circle
        float circX = 15;
        // Position circle on middle line for 3-phase groups, or on main line for single-phase
        float circY = isThreePhaseGroup ? hY + 5 : hY; // Middle line (L2) for 3-phase, main line for single-phase

        using var circlePaint = new SKPaint { Color = phaseColor, Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };
        canvas.DrawCircle(circX, circY, CircleRadius, circlePaint);

        using var labelFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 9 * UiToPdfScale);
        using var labelPaint = new SKPaint { Color = phaseColor, IsAntialias = true };
        float labelWidth = labelFont.MeasureText(reference.Label);
        canvas.DrawText(reference.Label, circX - labelWidth / 2, circY + 3, labelFont, labelPaint);

        // 3. NIE rysujemy wspólnej linii poziomej (magistrali)
        // Każdy MCB będzie miał osobną linię poziomą od etykiety do swojej pozycji
        float hStartX = circX + CircleRadius + 2;

        // 4. Draw Vertical Wires + Labels (matching Sheet 2 logic)
        int lineIndex = 0;
        int rcdPhaseIndex = reference.RcdPhaseIndex;

        foreach (var circuit in allCircuits)
        {
            float drawX = PadX + (lineIndex * LineSpacing);
            var symbol = circuit.LinkedSymbol;

            bool isRcd = _moduleTypeService.IsRcd(symbol);

            // Determine if this is 3P, 2P, or 1P module
            var poleCount = _moduleTypeService.GetPoleCount(symbol);
            bool is3P = poleCount == ModulePoleCount.P3 || poleCount == ModulePoleCount.P4;
            bool is2P = poleCount == ModulePoleCount.P2;

            if (isRcd)
            {
                // Handle RCD (2P or 4P)
                if (isThreePhaseGroup)
                {
                    // RCD 4P - four vertical lines: L1, L2, L3, N
                    var phases = new[] { "L3", "L2", "L1", "N" };
                    for (int i = 0; i < 4; i++)
                    {
                        float rcdLineX = drawX + (i - 1) * 4;
                        float busY = hY + i * 5;
                        var rcdPhaseColor = GetPhaseColor(phases[i]);

                        using var rcdLinePaint = new SKPaint
                        {
                            Color = rcdPhaseColor,
                            Style = SKPaintStyle.Stroke,
                            StrokeWidth = 3f,
                            IsAntialias = true
                        };
                        canvas.DrawLine(rcdLineX, wireBottomY, rcdLineX, busY, rcdLinePaint);
                    }

                    // "L1+L2+L3+N" label above RCD
                    using var rcdLabelBg = new SKPaint { Color = SKColor.Parse("#DC2626"), Style = SKPaintStyle.Fill, IsAntialias = true };
                    using var rcdLabelRect = new SKRoundRect(new SKRect(drawX - 20, wireBottomY - 30, drawX + 20, wireBottomY - 15), 3);
                    canvas.DrawRoundRect(rcdLabelRect, rcdLabelBg);
                    using var rcdLabelFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 8 * UiToPdfScale);
                    using var whitePaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
                    canvas.DrawText("L1+L2+L3+N", drawX - 18, wireBottomY - 20, rcdLabelFont, whitePaint);
                }
                else
                {
                    // RCD 2P - single line
                    string[] rcdPhaseLabels = { "L1", "L2", "L3" };
                    string phaseLabel = rcdPhaseLabels[rcdPhaseIndex % 3];
                    var rcdPhaseColor = GetPhaseColor(phaseLabel);

                    using var rcdLinePaint = new SKPaint
                    {
                        Color = rcdPhaseColor,
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = 3f,
                        IsAntialias = true
                    };
                    canvas.DrawLine(drawX, wireBottomY, drawX, hY, rcdLinePaint);

                    // Phase label above RCD line
                    using var phaseLabelBg = new SKPaint { Color = rcdPhaseColor, Style = SKPaintStyle.Fill, IsAntialias = true };
                    using var phaseLabelRect = new SKRoundRect(new SKRect(drawX - 10, hY - 18, drawX + 10, hY - 4), 2);
                    canvas.DrawRoundRect(phaseLabelRect, phaseLabelBg);
                    using var phaseLabelFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 8 * UiToPdfScale);
                    using var whitePaintRcd = new SKPaint { Color = SKColors.White, IsAntialias = true };
                    canvas.DrawText(phaseLabel, drawX - 6, hY - 8, phaseLabelFont, whitePaintRcd);
                }
            }
            else
            {
                // Handle MCB (1P, 2P, or 3P)
                if (is3P && isThreePhaseGroup)
                {
                    // MCB 3P - three L-shaped connections to bus
                    var phases = new[] { "L3", "L2", "L1" };
                    for (int i = 0; i < 3; i++)
                    {
                        float wireX = drawX + (i - 1) * 4;
                        float busY = hY + i * 5;
                        var phaseBrush = GetPhaseColor(phases[i]);

                        // Vertical line
                        using var vertLinePaint = new SKPaint
                        {
                            Color = phaseBrush,
                            Style = SKPaintStyle.Stroke,
                            StrokeWidth = 2f,
                            IsAntialias = true
                        };
                        canvas.DrawLine(wireX, wireBottomY, wireX, busY, vertLinePaint);

                        // Horizontal connection to bus
                        canvas.DrawLine(wireX, busY, hStartX, busY, vertLinePaint);
                    }
                }
                else if (is2P && isThreePhaseGroup)
                {
                    // MCB 2P in 3-phase group - two lines: L1, L2
                    float wireX_L1 = drawX - 2;
                    float wireX_L2 = drawX + 2;
                    float busY_L1 = hY + 10; // L1 position
                    float busY_L2 = hY + 5;  // L2 position

                    // L1 (brown)
                    var brushL1 = GetPhaseColor("L1");
                    using var l1Paint = new SKPaint { Color = brushL1, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, IsAntialias = true };
                    canvas.DrawLine(wireX_L1, wireBottomY, wireX_L1, busY_L1, l1Paint);
                    canvas.DrawLine(wireX_L1, busY_L1, hStartX, busY_L1, l1Paint);

                    // L2 (dark gray)
                    var brushL2 = GetPhaseColor("L2");
                    using var l2Paint = new SKPaint { Color = brushL2, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, IsAntialias = true };
                    canvas.DrawLine(wireX_L2, wireBottomY, wireX_L2, busY_L2, l2Paint);
                    canvas.DrawLine(wireX_L2, busY_L2, hStartX, busY_L2, l2Paint);
                }
                else if (!is2P && !is3P && isThreePhaseGroup)
                {
                    // MCB 1P in 3-phase group - single L3 line
                    float busY = hY; // L3 position
                    var phaseBrush = GetPhaseColor("L3");

                    using var singleLinePaint = new SKPaint { Color = phaseBrush, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, IsAntialias = true };
                    canvas.DrawLine(drawX, wireBottomY, drawX, busY, singleLinePaint);
                    canvas.DrawLine(drawX, busY, hStartX, busY, singleLinePaint);
                }
                else
                {
                    // Standard single-phase MCB - L-kształtna linia od etykiety do MCB
                    using var mcbLinePaint = new SKPaint { Color = phaseColor, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, IsAntialias = true };
                    // Linia pozioma od etykiety do pozycji MCB
                    canvas.DrawLine(hStartX, hY, drawX, hY, mcbLinePaint);
                    // Linia pionowa od linii poziomej w dół do MCB
                    canvas.DrawLine(drawX, hY, drawX, wireBottomY, mcbLinePaint);
                }
            }

            // Module number badge
            var badgeColor = isRcd ? SKColor.Parse("#DC2626") : SKColor.Parse("#3B82F6");
            using var badgePaint = new SKPaint { Color = badgeColor, Style = SKPaintStyle.Fill, IsAntialias = true };
            using var badgeRect = new SKRoundRect(new SKRect(drawX - 12, wireBottomY + 2, drawX + 12, wireBottomY + 16), 3);
            canvas.DrawRoundRect(badgeRect, badgePaint);

            using var badgeFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 10 * UiToPdfScale);
            using var whitePaint2 = new SKPaint { Color = SKColors.White, IsAntialias = true };
            string numText = $"#{symbol?.ModuleNumber ?? lineIndex}";
            canvas.DrawText(numText, drawX - 8, wireBottomY + 12, badgeFont, whitePaint2);

            // Type label
            string typeLabel = isRcd ? "RCD" : "MCB";
            using var typeFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 9 * UiToPdfScale);
            using var grayPaint = new SKPaint { Color = SKColor.Parse("#9CA3AF"), IsAntialias = true };
            canvas.DrawText(typeLabel, drawX - 8, wireBottomY + 28, typeFont, grayPaint);

            // Phase indicator
            var circuitPhase = symbol?.Phase ?? groupPhase;
            using var phaseIndBg = new SKPaint { Color = GetPhaseColor(circuitPhase), Style = SKPaintStyle.Fill, IsAntialias = true };
            using var phaseIndRect = new SKRoundRect(new SKRect(drawX - 8, wireBottomY + 32, drawX + 8, wireBottomY + 44), 2);
            canvas.DrawRoundRect(phaseIndRect, phaseIndBg);
            using var phaseIndFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 8 * UiToPdfScale);
            canvas.DrawText(circuitPhase.Length > 2 ? circuitPhase.Substring(0, 2) : circuitPhase, drawX - 6, wireBottomY + 40, phaseIndFont, whitePaint2);

            // Circuit name (rotated)
            string circuitName = !string.IsNullOrEmpty(circuit.Name) ? circuit.Name :
                                 !string.IsNullOrEmpty(symbol?.Label) ? symbol.Label :
                                 !string.IsNullOrEmpty(symbol?.CircuitName) ? symbol.CircuitName :
                                 isRcd ? "Zabezp. różn." : $"Obwód {symbol?.ModuleNumber}";

            canvas.Save();
            canvas.Translate(drawX - 3, wireBottomY + 50);
            canvas.RotateDegrees(90);
            using var nameFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 9 * UiToPdfScale);
            using var blackPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            canvas.DrawText(circuitName, 0, 0, nameFont, blackPaint);
            canvas.Restore();

            lineIndex++;
        }
    }
}
