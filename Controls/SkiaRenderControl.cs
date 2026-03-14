using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using DINBoard.Models;
using DINBoard.Services;
using DINBoard.Services.Pdf;
using SkiaSharp;
using System;
using System.Linq;
using E = DINBoard.Services.SchematicLayoutEngine;

namespace DINBoard.Controls;

/// <summary>
/// Prosta kontrolka wektorowa podłączająca pipeline rysowania SkiaSharp pod klatki Avalonia.
/// Nie wymaga specjalnych pakietów Nuget, wykorzystuje ICustomDrawOperation w standardowej pętli renderingu.
/// </summary>
public class SkiaRenderControl : Control
{
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

        if (_layoutData == null || _layoutData.IsEmpty) return;

        var drawOp = new SkiaDrawOperation(_layoutData, new Rect(0, 0, Bounds.Width, Bounds.Height), _editingCellRect, ShowGrid, ShowPageNumbers, ShowDinRailAxis);
        context.Custom(drawOp);
    }

    private class SkiaDrawOperation : ICustomDrawOperation
    {
        private readonly SchematicLayout _layout;
        private readonly SKRect? _editRect;
        private readonly bool _showGrid;
        private readonly bool _showPageNumbers;
        private readonly bool _showDinRailAxis;
        
        public Rect Bounds { get; }

        public SkiaDrawOperation(SchematicLayout lay, Rect bounds, SKRect? editRect, bool showGrid, bool showPageNumbers, bool showDinRailAxis)
        {
            _layout = lay;
            Bounds = bounds;
            _editRect = editRect;
            _showGrid = showGrid;
            _showPageNumbers = showPageNumbers;
            _showDinRailAxis = showDinRailAxis;
        }

        public bool HitTest(Point p) => false;
        public bool Equals(ICustomDrawOperation? other) => false;
        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)
        {
            try
            {
                var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
                if (leaseFeature == null) return;

                using var lease = leaseFeature.Lease();
                var skCanvas = lease.SkCanvas;

                skCanvas.Save();
                
                for (int pg = 0; pg < _layout.TotalPages; pg++)
                {
                    float yOff = (float)(pg * (E.PageH + E.PageGap));
                    
                    // Rysujemy szablon strony (białe tło, ramka, siatka)
                    PdfSingleLineDiagramService.DrawPageTemplate(skCanvas, yOff, _showGrid);
                    
                    // Rysujemy obwody (symbole, szyny, przewody)
                    PdfSingleLineDiagramService.DrawCircuitVectors(skCanvas, _layout, pg, yOff);
                    
                    // Rysujemy tabelę obwodów (Oznaczenie, Zabezp., Obwód, itd.)
                    PdfSingleLineDiagramService.DrawSkiaTable(skCanvas, _layout, pg, yOff);
                    
                    // Rysujemy tabelkę rysunkową (title block)
                    PdfSingleLineDiagramService.DrawSkiaTitleBlock(skCanvas, _layout, pg + 1, _layout.TotalPages, yOff, _showPageNumbers);
                }

                // Maskujemy edytowaną komórkę białym prostokątem — TextBox z warstwy Avalonia
                // będzie widoczny pod spodem (bo Avalonia renderuje go PO naszym ICustomDrawOperation)
                if (_editRect.HasValue)
                {
                    using var mask = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
                    skCanvas.DrawRect(_editRect.Value, mask);
                }

                skCanvas.Restore();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SkiaRenderControl] Crash w custom drawer (Avalonia): {ex}");
                System.IO.File.WriteAllText("crash.txt", ex.ToString());
            }
        }
    }
}
