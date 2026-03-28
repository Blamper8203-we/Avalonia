using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using CommunityToolkit.Mvvm.Messaging;
using DINBoard.Models;
using DINBoard.Services;
using DINBoard.Services.Pdf;
using DINBoard.ViewModels.Messages;
using SkiaSharp;
using E = DINBoard.Services.SchematicLayoutEngine;

namespace DINBoard.Controls;

/// <summary>
/// Prosta kontrolka wektorowa podłączająca pipeline rysowania SkiaSharp pod klatki Avalonia.
/// Nie wymaga specjalnych pakietów NuGet, wykorzystuje ICustomDrawOperation w standardowej pętli renderingu.
/// </summary>
public class SkiaRenderControl : Control
{
    private static readonly object RenderFailureGate = new();
    private static DateTime _lastRenderFailureNotificationUtc = DateTime.MinValue;
    private static readonly TimeSpan RenderFailureNotificationCooldown = TimeSpan.FromSeconds(10);

    private SchematicLayout? _layoutData;
    private SKRect? _editingCellRect;

    public SchematicLayout? LayoutData
    {
        get => _layoutData;
        set
        {
            _layoutData = value;
            InvalidateVisual();
        }
    }

    public bool ShowGrid { get; set; } = true;
    public bool ShowPageNumbers { get; set; } = true;
    public bool ShowDinRailAxis { get; set; } = true;

    /// <summary>
    /// Prostokąt aktualnie edytowanej komórki (we współrzędnych schematu).
    /// Gdy ustawiony, Skia rysuje białe tło w tym miejscu, żeby nie kolidować z TextBoxem.
    /// </summary>
    public SKRect? EditingCellRect
    {
        get => _editingCellRect;
        set
        {
            _editingCellRect = value;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_layoutData == null || _layoutData.IsEmpty)
        {
            return;
        }

        var drawOp = new SkiaDrawOperation(
            _layoutData,
            new Rect(0, 0, Bounds.Width, Bounds.Height),
            _editingCellRect,
            ShowGrid,
            ShowPageNumbers,
            ShowDinRailAxis);
        context.Custom(drawOp);
    }

    private static void ReportRenderFailure(Exception ex)
    {
        var reportPath = TryWriteRenderCrashReport(ex);
        var reportSuffix = reportPath != null ? $" Raport: {reportPath}" : string.Empty;
        AppLog.Error($"Błąd renderera schematu Skia.{reportSuffix}", ex);

        if (!ShouldNotifyUserAboutRenderFailure())
        {
            return;
        }

        WeakReferenceMessenger.Default.Send(new ShowToastMessage(new ToastData(
            "Błąd renderera",
            "Nie udało się odświeżyć schematu. Szczegóły zapisano w logu aplikacji.",
            ToastType.Error,
            5000)));
    }

    private static bool ShouldNotifyUserAboutRenderFailure()
    {
        lock (RenderFailureGate)
        {
            var now = DateTime.UtcNow;
            if (now - _lastRenderFailureNotificationUtc < RenderFailureNotificationCooldown)
            {
                return false;
            }

            _lastRenderFailureNotificationUtc = now;
            return true;
        }
    }

    private static string? TryWriteRenderCrashReport(Exception ex)
    {
        try
        {
            var logsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DINBoard",
                "Logs");

            Directory.CreateDirectory(logsDirectory);

            var reportPath = Path.Combine(logsDirectory, "render-crash-latest.txt");
            var reportContent =
                $"Timestamp (UTC): {DateTime.UtcNow:O}{Environment.NewLine}" +
                $"Control: {nameof(SkiaRenderControl)}{Environment.NewLine}{Environment.NewLine}" +
                ex;

            File.WriteAllText(reportPath, reportContent);
            return reportPath;
        }
        catch (Exception writeEx)
        {
            AppLog.Warn("Nie udało się zapisać raportu błędu renderera.", writeEx);
            return null;
        }
    }

    private sealed class SkiaDrawOperation : ICustomDrawOperation
    {
        private readonly SchematicLayout _layout;
        private readonly SKRect? _editRect;
        private readonly bool _showGrid;
        private readonly bool _showPageNumbers;
        private readonly bool _showDinRailAxis;

        public SkiaDrawOperation(SchematicLayout layout, Rect bounds, SKRect? editRect, bool showGrid, bool showPageNumbers, bool showDinRailAxis)
        {
            _layout = layout;
            Bounds = bounds;
            _editRect = editRect;
            _showGrid = showGrid;
            _showPageNumbers = showPageNumbers;
            _showDinRailAxis = showDinRailAxis;
        }

        public Rect Bounds { get; }

        public bool HitTest(Point p) => false;

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Dispose()
        {
        }

        public void Render(ImmediateDrawingContext context)
        {
            try
            {
                var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
                if (leaseFeature == null)
                {
                    return;
                }

                using var lease = leaseFeature.Lease();
                var skCanvas = lease.SkCanvas;

                skCanvas.Save();

                for (var pg = 0; pg < _layout.TotalPages; pg++)
                {
                    var yOff = (float)(pg * (E.PageH + E.PageGap));

                    PdfSingleLineDiagramService.DrawPageTemplate(skCanvas, yOff, _showGrid);
                    PdfSingleLineDiagramService.DrawCircuitVectors(skCanvas, _layout, pg, yOff);
                    PdfSingleLineDiagramService.DrawSkiaTable(skCanvas, _layout, pg, yOff);
                    PdfSingleLineDiagramService.DrawSkiaTitleBlock(skCanvas, _layout, pg + 1, _layout.TotalPages, yOff, _showPageNumbers);
                }

                if (_showDinRailAxis)
                {
                    // Placeholder: the control keeps the flag for compatibility even though axis drawing
                    // is currently handled by the Avalonia overlay layer.
                }

                if (_editRect.HasValue)
                {
                    using var mask = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
                    skCanvas.DrawRect(_editRect.Value, mask);
                }

                skCanvas.Restore();
            }
            catch (Exception ex)
            {
                ReportRenderFailure(ex);
            }
        }
    }
}
