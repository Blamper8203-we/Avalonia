using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using DINBoard.Models;
using DINBoard.Services;
using DINBoard.ViewModels;
using E = DINBoard.Services.SchematicLayoutEngine;

namespace DINBoard.Controls;

/// <summary>
/// Schemat jednokreskowy bazujący na Canvas + Geometry + PanAndZoom.
/// Rendering: SkiaRenderControl. Edycja: SchematicCellEditController. Animacja: SchematicLayoutAnimator.
/// </summary>
public partial class SingleLineDiagramCanvas : UserControl
{
    private IModuleTypeService? _moduleTypeService;
    private SchematicLayoutEngine? _engine;
    private readonly SchematicLayoutAnimator _animator;
    private SchematicCellEditController? _cellEditController;

    private SchematicLayout? _lay;
    private MainViewModel? _vm;
    private bool _isLightTheme;
    private bool _isRebuilding;
    private readonly HashSet<SymbolItem> _symbolSubscriptions = new();

    private SkiaRenderControl? _skiaCanvas;
    private Canvas? _interactiveCanvas;

    /// <summary>Ustawia serwis typów modułów (DI). Wywołaj przed SetViewModel.</summary>
    public void SetModuleTypeService(IModuleTypeService service)
    {
        _moduleTypeService = service ?? throw new ArgumentNullException(nameof(service));
        _engine = new SchematicLayoutEngine(service);
    }

    private IModuleTypeService ModuleTypeService =>
        _moduleTypeService ??= ((App)Application.Current!).Services.GetRequiredService<IModuleTypeService>();

    private SchematicLayoutEngine Engine => _engine ??= new SchematicLayoutEngine(ModuleTypeService);

    public SingleLineDiagramCanvas()
    {
        _animator = new SchematicLayoutAnimator();
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);

        _skiaCanvas = this.FindControl<SkiaRenderControl>("SkiaCanvas");
        _interactiveCanvas = this.FindControl<Canvas>("InteractiveCanvas");

        if (_interactiveCanvas != null)
        {
            _interactiveCanvas.PointerPressed += Canvas_PointerPressed;
        }
    }

    public bool IsLightTheme
    {
        get => _isLightTheme;
        set { if (_isLightTheme != value) { _isLightTheme = value; _t = value ? Light : Dark; Rebuild(); } }
    }

    record SchematicTheme(IBrush Fg, IBrush Dim, IBrush Grid, IBrush GridTxt, IBrush PageBg, IBrush BoxBg);

    static IBrush Br(byte r, byte g, byte b) => new SolidColorBrush(Color.FromRgb(r, g, b));

    static readonly SchematicTheme Dark = new(
        Fg: Brushes.White,
        Dim: Br(200, 200, 200),
        Grid: Br(60, 60, 60),
        GridTxt: Br(120, 120, 120),
        PageBg: new SolidColorBrush(Color.FromArgb(10, 255, 255, 255)),
        BoxBg: new SolidColorBrush(Color.FromArgb(15, 255, 255, 255)));

    static readonly SchematicTheme Light = new(
        Fg: Brushes.Black,
        Dim: Br(80, 80, 80),
        Grid: Br(200, 200, 200),
        GridTxt: Br(120, 120, 120),
        PageBg: Brushes.White,
        BoxBg: new SolidColorBrush(Color.FromArgb(255, 245, 245, 250)));

    SchematicTheme _t = Dark;

    public void SetViewModel(MainViewModel vm)
    {
        if (_vm != null)
        {
            _vm.Symbols.CollectionChanged -= OnChg;
            UnhookSymbols(_vm.Symbols);
        }
        _vm = vm;
        if (_vm != null)
        {
            _vm.Symbols.CollectionChanged += OnChg;
            HookSymbols(_vm.Symbols);
        }
        EnsureCellEditController();
        Rebuild();
    }

    private void EnsureCellEditController()
    {
        if (_cellEditController != null || _interactiveCanvas == null || _skiaCanvas == null) return;

        _cellEditController = new SchematicCellEditController(
            _interactiveCanvas,
            _skiaCanvas,
            ModuleTypeService,
            OnSymbolCommitEdit);
    }

    private void OnSymbolCommitEdit(SymbolItem sym)
    {
        var mainWindow = this.GetVisualParent();
        while (mainWindow != null && mainWindow is not MainWindow)
            mainWindow = mainWindow.GetVisualParent();
        (mainWindow as MainWindow)?.RefreshSymbolVisual(sym);
        _vm?.RecalculateModuleNumbers();
        sym.ReferenceDesignation = sym.ReferenceDesignation;
        sym.CircuitName = sym.CircuitName;
        Rebuild();
        _skiaCanvas?.InvalidateVisual();
    }

    void OnChg(object? s, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (SymbolItem symbol in e.NewItems)
                HookSymbol(symbol);
        }
        if (e.OldItems != null)
        {
            foreach (SymbolItem symbol in e.OldItems)
                UnhookSymbol(symbol);
        }
        Rebuild();
    }

    void HookSymbols(IEnumerable<SymbolItem> symbols)
    {
        foreach (var symbol in symbols)
            HookSymbol(symbol);
    }

    void UnhookSymbols(IEnumerable<SymbolItem> symbols)
    {
        foreach (var symbol in symbols)
            UnhookSymbol(symbol);
    }

    void HookSymbol(SymbolItem symbol)
    {
        if (_symbolSubscriptions.Add(symbol))
            symbol.PropertyChanged += OnSymbolPropertyChanged;
    }

    void UnhookSymbol(SymbolItem symbol)
    {
        if (_symbolSubscriptions.Remove(symbol))
            symbol.PropertyChanged -= OnSymbolPropertyChanged;
    }

    void OnSymbolPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isRebuilding) return;
        if (e.PropertyName == nameof(SymbolItem.Phase))
            Rebuild();
    }

    public void Rebuild()
    {
        if (_isRebuilding) return;
        if (_vm == null || _interactiveCanvas == null || _skiaCanvas == null) return;

        _isRebuilding = true;
        try
        {
            var previousLayout = _lay;
            var newLayout = Engine.BuildLayout(_vm.Symbols, _vm.CurrentProject);
            _animator.Stop();

            if (_vm.Schematic.IsBalancing && previousLayout != null && !previousLayout.IsEmpty && !newLayout.IsEmpty)
            {
                var animationMap = _animator.PrepareAnimation(previousLayout, newLayout);
                if (animationMap != null && animationMap.Count > 0)
                {
                    _lay = newLayout;
                    _interactiveCanvas.Width = _lay.TotalWidth;
                    _interactiveCanvas.Height = _lay.TotalHeight;
                    _skiaCanvas.Width = _lay.TotalWidth;
                    _skiaCanvas.Height = _lay.TotalHeight;
                    if (_cellEditController?.IsEditing != true)
                        _interactiveCanvas.Children.Clear();
                    _skiaCanvas.ShowGrid = _vm.Schematic.ShowGrid;
                    _skiaCanvas.ShowPageNumbers = _vm.Schematic.ShowPageNumbers;
                    _skiaCanvas.ShowDinRailAxis = _vm.Schematic.ShowDinRailAxis;
                    _skiaCanvas.LayoutData = _lay;
                    _skiaCanvas.InvalidateVisual();
                    _animator.Start(_lay, animationMap, () => _skiaCanvas.InvalidateVisual());
                    return;
                }
            }

            _lay = newLayout;
            _interactiveCanvas.Width = _lay.TotalWidth;
            _interactiveCanvas.Height = _lay.TotalHeight;
            _skiaCanvas.Width = _lay.TotalWidth;
            _skiaCanvas.Height = _lay.TotalHeight;

            if (_cellEditController?.IsEditing != true)
                _interactiveCanvas.Children.Clear();

            _skiaCanvas.ShowGrid = _vm.Schematic.ShowGrid;
            _skiaCanvas.ShowPageNumbers = _vm.Schematic.ShowPageNumbers;
            _skiaCanvas.ShowDinRailAxis = _vm.Schematic.ShowDinRailAxis;
            _skiaCanvas.LayoutData = _lay;
            _skiaCanvas.InvalidateVisual();
        }
        finally
        {
            _isRebuilding = false;
        }
    }

    void Canvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_lay == null || _interactiveCanvas == null) return;

        EnsureCellEditController();
        var pos = e.GetPosition(_interactiveCanvas);

        var cellHit = _cellEditController!.FindCellAt(_lay, pos);
        if (cellHit != null)
        {
            _cellEditController.StartCellEdit(cellHit.Value.Node, cellHit.Value.Field, cellHit.Value.CellRect);
            e.Handled = true;
            return;
        }

        _cellEditController?.CommitEdit();
    }

    /// <summary>Zatwierdza edycję komórki (np. z MainWindow).</summary>
    public void CommitEdit()
    {
        _cellEditController?.CommitEdit();
    }

    SchematicNode? FindAt(Point pos)
    {
        if (_lay == null) return null;
        foreach (var d in _lay.Devices)
        {
            var h = Hit(d, pos);
            if (h != null) return h;
        }
        return null;
    }

    static SchematicNode? Hit(SchematicNode n, Point pos)
    {
        if (new Rect(n.X - 4, n.Y - 4, n.Width + 8, n.Height + 8).Contains(pos)) return n;
        foreach (var ch in n.Children)
        {
            var h = Hit(ch, pos);
            if (h != null) return h;
        }
        return null;
    }
}
