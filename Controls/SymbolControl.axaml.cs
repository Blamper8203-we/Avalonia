using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using DINBoard.Models;
using DINBoard.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace DINBoard.Controls;

public partial class SymbolControl : UserControl
{
    private bool _isDragging;
    private Point _lastPointerWorldPosition;
    private Vector _dragOffset;
    private Canvas? _cachedCanvas;
    private Border? _referenceDesignationBadge;
    private TextBlock? _referenceDesignationText;
    private SymbolItem? _observedSymbol;

    public SymbolControl()
    {
        InitializeComponent();
        _referenceDesignationBadge = this.FindControl<Border>("ReferenceDesignationBadge");
        _referenceDesignationText = this.FindControl<TextBlock>("ReferenceDesignationText");
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += OnPointerCaptureLost;
        DoubleTapped += OnDoubleTapped;
        this.DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_observedSymbol != null)
        {
            _observedSymbol.PropertyChanged -= OnObservedSymbolPropertyChanged;
        }

        _observedSymbol = DataContext as SymbolItem;
        if (_observedSymbol != null)
        {
            _observedSymbol.PropertyChanged += OnObservedSymbolPropertyChanged;

            var symbol = _observedSymbol;
            bool isDist = IsDistributionBlock(symbol);

            var menuEnable = this.FindControl<MenuItem>("MenuEnableBlueCover");
            var menuDisable = this.FindControl<MenuItem>("MenuDisableBlueCover");
            var separator = this.FindControl<Separator>("SepBlueCover");

            if (menuEnable != null) menuEnable.IsVisible = isDist;
            if (menuDisable != null) menuDisable.IsVisible = isDist;
            if (separator != null) separator.IsVisible = isDist;

            var menuGroup = this.FindControl<MenuItem>("MenuGroupSelected");
            var menuUngroup = this.FindControl<MenuItem>("MenuUngroupSelected");
            var sepGrouping = this.FindControl<Separator>("SepGrouping");

            if (menuGroup != null) menuGroup.IsVisible = !isDist;
            if (menuUngroup != null) menuUngroup.IsVisible = !isDist;
            if (sepGrouping != null) sepGrouping.IsVisible = !isDist;

            UpdateReferenceDesignationBadgeScale(symbol);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_observedSymbol != null)
        {
            _observedSymbol.PropertyChanged -= OnObservedSymbolPropertyChanged;
            _observedSymbol = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnObservedSymbolPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not SymbolItem symbol)
        {
            return;
        }

        if (e.PropertyName == nameof(SymbolItem.Width) || e.PropertyName == nameof(SymbolItem.Height))
        {
            UpdateReferenceDesignationBadgeScale(symbol);
        }
    }

    private void UpdateReferenceDesignationBadgeScale(SymbolItem symbol)
    {
        if (_referenceDesignationBadge == null || _referenceDesignationText == null)
        {
            return;
        }

        var symbolWidth = Math.Max(1.0, symbol.Width);
        var symbolHeight = Math.Max(1.0, symbol.Height);

        // Badge skaluje się wraz z modułem na szynie DIN (zamiast stałych wartości).
        var fontSize = Math.Clamp(symbolWidth * 0.10, 5.0, 9.0);
        var horizontalPadding = Math.Clamp(fontSize * 0.45, 2.0, 4.0);
        var verticalPadding = Math.Clamp(fontSize * 0.18, 1.0, 2.0);
        var cornerRadius = Math.Clamp(fontSize * 0.35, 2.0, 3.5);
        var bottomOffset = Math.Clamp(symbolHeight * 0.085, 10.0, 20.0);

        _referenceDesignationText.FontSize = fontSize;
        _referenceDesignationBadge.Padding = new Thickness(horizontalPadding, verticalPadding);
        _referenceDesignationBadge.CornerRadius = new CornerRadius(cornerRadius);
        _referenceDesignationBadge.Margin = new Thickness(0, 0, 0, -bottomOffset);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // W trybie wklejania klonów, nie przechwytuj kliknięcia — pozwól CanvasController obsłużyć upuszczenie
        var placingVm = GetViewModel();
        if (placingVm != null && placingVm.IsPlacingClones)
        {
            return; // Przepuść do SchematicCanvasController.HandlePointerPressed
        }

        if (DataContext is SymbolItem symbol && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            // EDGE SAFETY MARGIN (Fix for zoomed-out selection clearing)
            // If click is very close to the edge, treat it as a click on "empty space"
            var pos = e.GetCurrentPoint(this).Position;
            double margin = 4.0; // 4 units tolerance

            if (pos.X <= margin || pos.X >= (this.Bounds.Width - margin) ||
                pos.Y <= margin || pos.Y >= (this.Bounds.Height - margin))
            {
                // Pass through - allow Canvas to handle this as "Clear Selection"
                return;
            }

            var viewModel = GetViewModel();

            // Handle selection
            if (viewModel != null)
            {
                // If Ctrl or Shift is held, toggle this module's selection
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    symbol.IsSelected = !symbol.IsSelected;
                }
                else
                {
                    // If this module is not selected, select only this one
                    if (!symbol.IsSelected)
                    {
                        foreach (var s in viewModel.Symbols)
                        {
                            s.IsSelected = false;
                        }
                        symbol.IsSelected = true;
                    }
                    // If already selected, keep selection (for group dragging)
                }
            }

            _isDragging = true;
            _lastPointerWorldPosition = GetPointerWorldPosition(e);
            _dragOffset = _lastPointerWorldPosition - new Point(symbol.X, symbol.Y);
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDragging && DataContext is SymbolItem symbol)
        {
            var currentPointerWorldPosition = GetPointerWorldPosition(e);
            var targetPos = currentPointerWorldPosition - _dragOffset;

            var viewModel = GetViewModel();

            List<SymbolItem> movedSymbols;
            string? groupId = symbol.Group;

            if (viewModel != null && !string.IsNullOrWhiteSpace(groupId))
            {
                movedSymbols = viewModel.Symbols.Where(s => s.Group == groupId).ToList();
                if (movedSymbols.Count == 0) movedSymbols = new List<SymbolItem> { symbol };
            }
            else
            {
                movedSymbols = new List<SymbolItem> { symbol };
            }

            // Calculate delta X to apply to group
            double deltaX = targetPos.X - symbol.X;

            // 1. Apply X (Always follow cursor)
            foreach (var s in movedSymbols)
            {
                s.X += deltaX;
            }

            // 2. Y Logic with Magnetic Snap
            double targetY = targetPos.Y;
            bool shouldSnap = false;
            double snapY = 0;

            if (viewModel != null && viewModel.Schematic.DinRailAxes.Count > 0)
            {
                double moduleHalfHeight = symbol.Height / 2.0;
                double moduleCenterY = targetY + moduleHalfHeight;

                double closestAxis = viewModel.Schematic.DinRailAxes[0];
                double minDistance = Math.Abs(moduleCenterY - closestAxis);

                foreach (var axis in viewModel.Schematic.DinRailAxes)
                {
                    double d = Math.Abs(moduleCenterY - axis);
                    if (d < minDistance)
                    {
                        minDistance = d;
                        closestAxis = axis;
                    }
                }

                // Keep consistent with drag-preview/drop snap thresholds in MainWindow.
                // Add hysteresis so the snap state doesn't "drop" easily and reliably re-engages.
                const double SNAP_IN = 50.0;
                const double SNAP_OUT = 80.0;

                if (symbol.IsSnappedToRail)
                    shouldSnap = minDistance < SNAP_OUT;
                else
                    shouldSnap = minDistance < SNAP_IN;

                if (shouldSnap)
                {
                    snapY = closestAxis - moduleHalfHeight;
                    viewModel.StatusMessage = "Przyciągnięto do szyny DIN";
                }
                else
                {
                    viewModel.StatusMessage = "";
                }
            }

            // Apply Y Movement
            double globalCorrectionY;

            if (shouldSnap)
            {
                // Force Main Symbol to Snap Y
                globalCorrectionY = snapY - symbol.Y;
                symbol.IsSnappedToRail = true;
            }
            else
            {
                // Force Main Symbol to Target Y (Cursor Sync)
                globalCorrectionY = targetY - symbol.Y;
                symbol.IsSnappedToRail = false;
            }

            foreach (var s in movedSymbols)
            {
                s.Y += globalCorrectionY;
            }

            _lastPointerWorldPosition = currentPointerWorldPosition;
            e.Handled = true;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;

            // Reset snap state
            if (DataContext is SymbolItem symbol)
            {
                symbol.IsSnappedToRail = false;
            }

            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isDragging = false;
    }

    private void EditParameters_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is SymbolItem symbol)
        {
            var mainWindow = GetMainWindow();
            mainWindow?.ShowCircuitEditor(symbol);
        }
    }

    private void OnDoubleTapped(object? sender, global::Avalonia.Input.TappedEventArgs e)
    {
        if (DataContext is SymbolItem symbol)
        {
            var mainWindow = GetMainWindow();
            mainWindow?.ShowCircuitEditor(symbol);
            e.Handled = true;
        }
    }

    private void DuplicateSymbol_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is SymbolItem symbol)
        {
            var viewModel = GetViewModel();
            if (viewModel != null)
            {
                List<SymbolItem> toDuplicate;

                if (symbol.IsSelected)
                {
                    // Moduł jest zaznaczony — powiel całą zaznaczoną grupę
                    toDuplicate = viewModel.Symbols.Where(s => s.IsSelected).ToList();
                }
                else
                {
                    // Prawoklik na niezaznaczonym module — powiel tylko ten jeden
                    toDuplicate = new List<SymbolItem> { symbol };
                }

                viewModel.StartPlacingClones(toDuplicate);
            }
        }
    }

    private void DeleteSymbol_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is SymbolItem symbol)
        {
            var viewModel = GetViewModel();
            if (viewModel != null)
            {
                if (symbol.IsSelected)
                {
                    viewModel.ModuleManager.DeleteSelectedCommand.Execute(null);
                }
                else
                {
                    viewModel.Symbols.Remove(symbol);
                }
            }
        }
    }

    private void EnableBlueCover_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is SymbolItem symbol)
        {
            symbol.Parameters["BLUE_COVER_VISIBLE"] = "True";
            var mainWindow = GetMainWindow();
            mainWindow?.RefreshSymbolVisual(symbol);
        }
    }

    private void DisableBlueCover_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is SymbolItem symbol)
        {
            symbol.Parameters["BLUE_COVER_VISIBLE"] = "False";
            var mainWindow = GetMainWindow();
            mainWindow?.RefreshSymbolVisual(symbol);
        }
    }

    private void GroupSelected_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not SymbolItem symbol) return;
        if (IsDistributionBlock(symbol)) return;

        var viewModel = GetViewModel();
        if (viewModel == null) return;

        var selected = viewModel.Symbols
            .Where(s => s.IsSelected && !IsDistributionBlock(s))
            .ToList();

        if (selected.Count == 0)
        {
            selected.Add(symbol);
        }

        var groupId = Guid.NewGuid().ToString("N");
        foreach (var s in selected)
        {
            s.Group = groupId;
            s.IsSelected = true;
        }
    }

    private void UngroupSelected_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not SymbolItem symbol) return;
        if (IsDistributionBlock(symbol)) return;

        var viewModel = GetViewModel();
        if (viewModel == null) return;

        var selected = viewModel.Symbols
            .Where(s => s.IsSelected && !IsDistributionBlock(s))
            .ToList();

        if (selected.Count == 0)
        {
            selected.Add(symbol);
        }

        foreach (var s in selected)
        {
            s.Group = null;
            s.IsInSelectedGroup = false;
        }
    }

    private static bool IsDistributionBlock(SymbolItem symbol)
    {
        return !string.IsNullOrEmpty(symbol.VisualPath) &&
               symbol.VisualPath.Contains("blok rozdzielczy", StringComparison.OrdinalIgnoreCase);
    }

    public static readonly StyledProperty<MainViewModel?> MainContextProperty =
        AvaloniaProperty.Register<SymbolControl, MainViewModel?>(nameof(MainContext));

    public MainViewModel? MainContext
    {
        get => GetValue(MainContextProperty);
        set => SetValue(MainContextProperty, value);
    }

    private MainViewModel? GetViewModel()
    {
        if (MainContext != null) return MainContext;

        // Prefer TopLevel (more reliable than walking visual parents from within templates)
        var top = TopLevel.GetTopLevel(this);
        if (top is Window w && w.DataContext is MainViewModel wvm) return wvm;
        if (top is Control c && c.DataContext is MainViewModel cvm) return cvm;

        // Fallback: walk up the visual tree
        var current = this as global::Avalonia.Visual;
        while (current != null)
        {
            if (current is Window window && window.DataContext is MainViewModel vm)
            {
                return vm;
            }
            current = global::Avalonia.VisualTree.VisualExtensions.GetVisualParent(current);
        }
        return null;
    }

    private MainWindow? GetMainWindow()
    {
        var current = this as global::Avalonia.Visual;
        while (current != null)
        {
            if (current is MainWindow window)
            {
                return window;
            }
            current = global::Avalonia.VisualTree.VisualExtensions.GetVisualParent(current);
        }
        return null;
    }

    private Point GetPointerWorldPosition(PointerEventArgs e)
    {
        if (_cachedCanvas == null)
        {
            // Find ZoomContainer Canvas from the Window
            var mainWindow = GetMainWindow();
            _cachedCanvas = mainWindow?.FindControl<Canvas>("ZoomContainer");

            if (_cachedCanvas == null)
            {
                var top = TopLevel.GetTopLevel(this) as Control;
                _cachedCanvas = top?.FindControl<Canvas>("ZoomContainer");
            }
        }

        if (_cachedCanvas != null)
        {
            // Get position relative to SchematicGrid Canvas (direct world coords)
            return e.GetPosition(_cachedCanvas);
        }

        // Last-resort fallback (treat local coords as world coords)
        return e.GetPosition(this);
    }
}
