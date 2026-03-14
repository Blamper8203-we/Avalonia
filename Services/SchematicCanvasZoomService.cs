using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DINBoard.Models;
using DINBoard.ViewModels;

namespace DINBoard.Services;

/// <summary>
/// Obsługuje zoom, pan i transformację widoku schematu (Canvas + MatrixTransform).
/// </summary>
public sealed class SchematicCanvasZoomService
{
    private readonly Border _canvasContainer;
    private readonly Canvas _zoomContainer;
    private readonly MainViewModel _viewModel;

    private double _currentZoom = 1.0;
    private const double MinZoom = 0.1;
    private const double MaxZoom = 10.0;
    private const double ZoomFactor = 1.15;

    public SchematicCanvasZoomService(Border canvasContainer, Canvas zoomContainer, MainViewModel viewModel)
    {
        _canvasContainer = canvasContainer ?? throw new ArgumentNullException(nameof(canvasContainer));
        _zoomContainer = zoomContainer ?? throw new ArgumentNullException(nameof(zoomContainer));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    public double CurrentZoom => _currentZoom;

    public void InitializeTransform()
    {
        double viewportWidth = _canvasContainer.Bounds.Width;
        double viewportHeight = _canvasContainer.Bounds.Height;
        var matrix = Matrix.CreateTranslation(viewportWidth / 2, viewportHeight / 2);
        _zoomContainer.RenderTransform = new MatrixTransform(matrix);
        _currentZoom = 1.0;
        AppLog.Info($"Canvas initialized. Center at ({viewportWidth / 2}, {viewportHeight / 2})");
    }

    public void ApplyPan(Point delta)
    {
        var transform = GetOrCreateTransform();
        var matrix = transform.Matrix;
        transform.Matrix = new Matrix(matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.M31 + delta.X, matrix.M32 + delta.Y);
    }

    public void ApplyZoomAtPoint(Point viewportPoint, double zoomFactor)
    {
        var transform = GetOrCreateTransform();
        var matrix = transform.Matrix;
        double oldScale = matrix.M11;
        double newScale = Math.Clamp(oldScale * zoomFactor, MinZoom, MaxZoom);
        if (Math.Abs(newScale - oldScale) < 0.0001) return;

        double oldTx = matrix.M31, oldTy = matrix.M32;
        double localX = (viewportPoint.X - oldTx) / oldScale;
        double localY = (viewportPoint.Y - oldTy) / oldScale;
        double newTx = viewportPoint.X - localX * newScale;
        double newTy = viewportPoint.Y - localY * newScale;

        transform.Matrix = new Matrix(newScale, 0, 0, newScale, newTx, newTy);
        _currentZoom = newScale;
    }

    public void ZoomIn() => ZoomAtCenter(ZoomFactor);
    public void ZoomOut() => ZoomAtCenter(1.0 / ZoomFactor);

    public void ZoomAtCenter(double zoomFactor)
    {
        var transform = GetOrCreateTransform();
        var matrix = transform.Matrix;
        double oldScale = matrix.M11;
        var viewportPoint = new Point(_canvasContainer.Bounds.Width / 2, _canvasContainer.Bounds.Height / 2);

        double newScale = Math.Clamp(oldScale * zoomFactor, MinZoom, MaxZoom);
        if (Math.Abs(newScale - oldScale) < 0.0001) return;

        double oldTx = matrix.M31, oldTy = matrix.M32;
        double localX = (viewportPoint.X - oldTx) / oldScale;
        double localY = (viewportPoint.Y - oldTy) / oldScale;
        double newTx = viewportPoint.X - localX * newScale;
        double newTy = viewportPoint.Y - localY * newScale;

        transform.Matrix = new Matrix(newScale, 0, 0, newScale, newTx, newTy);
        _currentZoom = newScale;
    }

    public void ZoomExtents()
    {
        (double minX, double minY, double maxX, double maxY, bool hasContent) = ComputeContentBounds();
        if (!hasContent)
        {
            minX = -100; maxX = 100;
            minY = -100; maxY = 100;
        }

        double padding = 40;
        double contentWidth = maxX - minX + padding * 2;
        double contentHeight = maxY - minY + padding * 2;
        double centerX = (minX + maxX) / 2.0;
        double centerY = (minY + maxY) / 2.0;

        double viewportWidth = _canvasContainer.Bounds.Width;
        double viewportHeight = _canvasContainer.Bounds.Height;

        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            ResetTransform();
            return;
        }

        double scaleX = viewportWidth / contentWidth;
        double scaleY = viewportHeight / contentHeight;
        double newScale = Math.Clamp(Math.Min(scaleX, scaleY), MinZoom, MaxZoom);

        double newTx = (viewportWidth / 2.0) - (centerX * newScale);
        double newTy = (viewportHeight / 2.0) - (centerY * newScale);

        var transform = GetOrCreateTransform();
        transform.Matrix = new Matrix(newScale, 0, 0, newScale, newTx, newTy);
        _currentZoom = newScale;
        AppLog.Info($"ZoomExtents: Scale={newScale:F2}, Center=({centerX:F0},{centerY:F0})");
    }

    public void CenterOnSymbol(SymbolItem symbol)
    {
        if (symbol == null) return;

        var transform = GetOrCreateTransform();
        var matrix = transform.Matrix;
        double scale = matrix.M11;

        double symbolCenterX = symbol.X + symbol.Width / 2.0;
        double symbolCenterY = symbol.Y + symbol.Height / 2.0;
        double viewportCenterX = _canvasContainer.Bounds.Width / 2.0;
        double viewportCenterY = _canvasContainer.Bounds.Height / 2.0;

        double newTx = viewportCenterX - symbolCenterX * scale;
        double newTy = viewportCenterY - symbolCenterY * scale;
        transform.Matrix = new Matrix(scale, 0, 0, scale, newTx, newTy);
    }

    public void HandleResize(Size oldSize, Size newSize)
    {
        if (oldSize.Width <= 0 || oldSize.Height <= 0 || newSize.Width <= 0 || newSize.Height <= 0)
            return;

        var transform = _zoomContainer.RenderTransform as MatrixTransform;
        if (transform == null)
        {
            InitializeTransform();
            return;
        }

        var matrix = transform.Matrix;
        var scale = matrix.M11;
        if (!double.IsFinite(scale) || Math.Abs(scale) < 0.0001)
        {
            InitializeTransform();
            return;
        }

        double oldCenterX = oldSize.Width / 2.0, oldCenterY = oldSize.Height / 2.0;
        double localCenterX = (oldCenterX - matrix.M31) / scale;
        double localCenterY = (oldCenterY - matrix.M32) / scale;
        double newCenterX = newSize.Width / 2.0, newCenterY = newSize.Height / 2.0;
        double newTx = newCenterX - localCenterX * scale;
        double newTy = newCenterY - localCenterY * scale;

        transform.Matrix = new Matrix(scale, 0, 0, scale, newTx, newTy);
    }

    private MatrixTransform GetOrCreateTransform()
    {
        var transform = _zoomContainer.RenderTransform as MatrixTransform;
        if (transform == null)
        {
            transform = new MatrixTransform();
            _zoomContainer.RenderTransform = transform;
        }
        return transform;
    }

    private void ResetTransform()
    {
        var transform = GetOrCreateTransform();
        transform.Matrix = Matrix.Identity;
        _currentZoom = 1.0;
    }

    private (double minX, double minY, double maxX, double maxY, bool hasContent) ComputeContentBounds()
    {
        double minX = 0, minY = 0, maxX = 0, maxY = 0;
        bool hasContent = false;

        if (_viewModel.Schematic.IsDinRailVisible && _viewModel.Schematic.DinRailSize.Width > 0 && _viewModel.Schematic.DinRailSize.Height > 0)
        {
            double halfW = _viewModel.Schematic.DinRailSize.Width / 2.0;
            double halfH = _viewModel.Schematic.DinRailSize.Height / 2.0;
            minX = -halfW; maxX = halfW;
            minY = -halfH; maxY = halfH;
            hasContent = true;
        }

        foreach (var symbol in _viewModel.Symbols)
        {
            if (!hasContent)
            {
                minX = symbol.X; minY = symbol.Y;
                maxX = symbol.X + symbol.Width; maxY = symbol.Y + symbol.Height;
                hasContent = true;
            }
            else
            {
                minX = Math.Min(minX, symbol.X);
                minY = Math.Min(minY, symbol.Y);
                maxX = Math.Max(maxX, symbol.X + symbol.Width);
                maxY = Math.Max(maxY, symbol.Y + symbol.Height);
            }
        }

        return (minX, minY, maxX, maxY, hasContent);
    }
}
