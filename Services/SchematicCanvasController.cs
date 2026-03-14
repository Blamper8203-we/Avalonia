using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using DINBoard.Models;
using DINBoard.ViewModels;

namespace DINBoard.Services;

public sealed class SchematicCanvasController
{
    private readonly MainViewModel _viewModel;
    private readonly Border _canvasContainer;
    private readonly Canvas _zoomContainer;
    private readonly Border _selectionRectangle;
    private readonly SchematicCanvasZoomService _zoomService;

    private bool _isSelecting;
    private Point _selectionStartPoint;

    private bool _isPanning;
    private Point _lastPanPoint;
    private bool _handlersAttached;

    /// <summary>Aktualny współczynnik zoom (do testów).</summary>
    public double CurrentZoom => _zoomService.CurrentZoom;

    public SchematicCanvasController(
        MainViewModel viewModel,
        Border canvasContainer,
        Canvas zoomContainer,
        Border selectionRectangle,
        Ellipse? viewportCursorMarker)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _canvasContainer = canvasContainer ?? throw new ArgumentNullException(nameof(canvasContainer));
        _zoomContainer = zoomContainer ?? throw new ArgumentNullException(nameof(zoomContainer));
        _selectionRectangle = selectionRectangle ?? throw new ArgumentNullException(nameof(selectionRectangle));
        _zoomService = new SchematicCanvasZoomService(canvasContainer, zoomContainer, viewModel);
    }

    public void AttachInputHandlers()
    {
        if (_handlersAttached)
        {
            return;
        }

        _canvasContainer.AddHandler(InputElement.PointerPressedEvent, HandlePointerPressed, RoutingStrategies.Bubble, true);
        _canvasContainer.AddHandler(InputElement.PointerMovedEvent, HandlePointerMoved, RoutingStrategies.Bubble, true);
        _canvasContainer.AddHandler(InputElement.PointerReleasedEvent, HandlePointerReleased, RoutingStrategies.Bubble, true);
        _canvasContainer.PointerWheelChanged += HandlePointerWheelChanged;
        _canvasContainer.SizeChanged += HandleSizeChanged;

        _handlersAttached = true;
    }

    public void DetachInputHandlers()
    {
        if (!_handlersAttached)
        {
            return;
        }

        _canvasContainer.RemoveHandler(InputElement.PointerPressedEvent, HandlePointerPressed);
        _canvasContainer.RemoveHandler(InputElement.PointerMovedEvent, HandlePointerMoved);
        _canvasContainer.RemoveHandler(InputElement.PointerReleasedEvent, HandlePointerReleased);
        _canvasContainer.PointerWheelChanged -= HandlePointerWheelChanged;
        _canvasContainer.SizeChanged -= HandleSizeChanged;

        _handlersAttached = false;
    }

    public void InitializeCanvasTransform()
    {
        _zoomService.InitializeTransform();
    }

    public void HandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Handled) return;

        var point = e.GetCurrentPoint(_canvasContainer);

        if (point.Properties.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _lastPanPoint = point.Position;
            e.Pointer.Capture(_canvasContainer);
            e.Handled = true;
            return;
        }

        if (!point.Properties.IsLeftButtonPressed && !point.Properties.IsRightButtonPressed) return;

        // --- Obsługa upuszczania i anulowania "Stempla" ---
        if (_viewModel.IsPlacingClones)
        {
            if (point.Properties.IsLeftButtonPressed)
            {
                // Zatwierdzenie nowej pozycji klonów
                _viewModel.CommitClonesPlacement();
                e.Handled = true;
                return;
            }
            else if (point.Properties.IsRightButtonPressed)
            {
                // Anulowanie układania
                _viewModel.IsPlacingClones = false;
                foreach (var clone in _viewModel.ClonesToPlace)
                {
                    _viewModel.Symbols.Remove(clone);
                }
                _viewModel.ClonesToPlace.Clear();
                _viewModel.StatusMessage = "Anulowano powielanie";
            }
            e.Handled = true;
            return;
        }

        if (!point.Properties.IsLeftButtonPressed) return;

        bool isModifierHeld = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (!isModifierHeld)
        {
            foreach (var s in _viewModel.Symbols)
            {
                s.IsSelected = false;
            }
        }

        _isSelecting = true;
        _selectionStartPoint = e.GetCurrentPoint(_zoomContainer).Position;

        Canvas.SetLeft(_selectionRectangle, _selectionStartPoint.X);
        Canvas.SetTop(_selectionRectangle, _selectionStartPoint.Y);
        _selectionRectangle.Width = 0;
        _selectionRectangle.Height = 0;
        _selectionRectangle.IsVisible = true;

        e.Pointer.Capture(_canvasContainer);
        e.Handled = true;
    }

    public void HandlePointerMoved(object? sender, PointerEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (_viewModel.IsPlacingClones && _viewModel.ClonesToPlace.Any())
        {
            var currentPoint = e.GetPosition(_zoomContainer);
            SchematicClonePlacementHelper.UpdateClonesPosition(_viewModel, currentPoint);
            e.Handled = true;
            return;
        }

        if (_isPanning)
        {
            var currentPoint = e.GetPosition(_canvasContainer);
            _zoomService.ApplyPan(currentPoint - _lastPanPoint);
            _lastPanPoint = currentPoint;
            e.Handled = true;
            return;
        }

        if (!_isSelecting) return;

        var selPoint = e.GetPosition(_zoomContainer);

        var x = Math.Min(_selectionStartPoint.X, selPoint.X);
        var y = Math.Min(_selectionStartPoint.Y, selPoint.Y);
        var w = Math.Abs(selPoint.X - _selectionStartPoint.X);
        var h = Math.Abs(selPoint.Y - _selectionStartPoint.Y);

        Canvas.SetLeft(_selectionRectangle, x);
        Canvas.SetTop(_selectionRectangle, y);
        _selectionRectangle.Width = w;
        _selectionRectangle.Height = h;

        e.Handled = true;
    }

    public void HandlePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (_isPanning)
        {
            _isPanning = false;
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }

        if (!_isSelecting) return;

        var currentPoint = e.GetPosition(_zoomContainer);
        var rectX = Math.Min(_selectionStartPoint.X, currentPoint.X);
        var rectY = Math.Min(_selectionStartPoint.Y, currentPoint.Y);
        var rectW = Math.Abs(currentPoint.X - _selectionStartPoint.X);
        var rectH = Math.Abs(currentPoint.Y - _selectionStartPoint.Y);

        var selectionRect = new Rect(rectX, rectY, rectW, rectH);

        foreach (var symbol in _viewModel.Symbols)
        {
            var symbolRect = new Rect(symbol.X, symbol.Y, symbol.Width, symbol.Height);
            if (selectionRect.Intersects(symbolRect))
            {
                symbol.IsSelected = true;
            }
        }

        _selectionRectangle.IsVisible = false;
        _isSelecting = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    public void HandlePointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var viewportPoint = e.GetPosition(_canvasContainer);
        double zoomFactor = e.Delta.Y > 0 ? 1.15 : (1.0 / 1.15);
        _zoomService.ApplyZoomAtPoint(viewportPoint, zoomFactor);
        e.Handled = true;
    }

    private void HandleSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        _viewModel.StatusMessage = $"Obszar roboczy: {e.NewSize.Width:F0}x{e.NewSize.Height:F0}";

        var deltaW = Math.Abs(e.NewSize.Width - e.PreviousSize.Width);
        var deltaH = Math.Abs(e.NewSize.Height - e.PreviousSize.Height);
        if (deltaW < 2 && deltaH < 2)
        {
            return;
        }

        if (TopLevel.GetTopLevel(_canvasContainer) is Window window && window.WindowState != WindowState.Normal)
        {
            return;
        }

        HandleResize(e.PreviousSize, e.NewSize);
    }

    public void ZoomIn() => _zoomService.ZoomIn();
    public void ZoomOut() => _zoomService.ZoomOut();
    public void ZoomFit() => _zoomService.ZoomExtents();
    public void ZoomExtents() => _zoomService.ZoomExtents();
    public void CenterOnSymbol(SymbolItem symbol) => _zoomService.CenterOnSymbol(symbol);

    public void HandleResize(Size oldSize, Size newSize)
    {
        _zoomService.HandleResize(oldSize, newSize);
    }
}
