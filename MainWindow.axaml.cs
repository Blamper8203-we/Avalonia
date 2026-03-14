using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.Messaging;
using DINBoard.ViewModels;
using DINBoard.ViewModels.Messages;

using DINBoard.Services;
using DINBoard.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DINBoard;

public partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }
    private readonly SymbolImportService? _symbolImportService;
    private readonly IProjectService? _projectService;
    private readonly IModuleTypeService? _moduleTypeService;
    private readonly SymbolParameterService? _symbolParameterService;
    private bool _isExitCommandClosing;

    private Border? _canvasContainer;
    private Canvas? _zoomContainer;
    private Border? _dragPreviewBorder;
    private Image? _dragPreviewImage;
    private Border? _selectionRectangle;
    private Ellipse? _viewportCursorMarker;
    private Controls.DinRailView? _dinRailDisplay;
    private Controls.SingleLineDiagramCanvas? _schematicDiagram;

    private Services.SchematicCanvasController? _canvasController;
    private Services.SchematicDragDropController? _dragDropController;
    private Services.SchematicDinRailController? _dinRailController;

    // Skala szyny DIN - stosowana do importowanych modułów
    private double _dinRailScale = 0.20;

    private void CacheControls()
    {
        _canvasContainer = this.FindControl<Border>("CanvasContainer");
        _zoomContainer = this.FindControl<Canvas>("ZoomContainer");
        _dragPreviewBorder = this.FindControl<Border>("DragPreviewBorder");
        _dragPreviewImage = this.FindControl<Image>("DragPreviewImage");
        _selectionRectangle = this.FindControl<Border>("SelectionRectangle");
        _viewportCursorMarker = this.FindControl<Ellipse>("ViewportCursorMarker");
        _dinRailDisplay = this.FindControl<Controls.DinRailView>("DinRailDisplay");
        _schematicDiagram = this.FindControl<Controls.SingleLineDiagramCanvas>("SchematicDiagram");
    }

    public MainWindow()
    {
        InitializeComponent();
        CacheControls();
        ViewModel = new MainViewModel();
        _symbolImportService = null;
        _projectService = null;
        _symbolParameterService = null;
    }

    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        var app = (App)Application.Current!;
        _symbolImportService = app.Services.GetRequiredService<SymbolImportService>();
        _projectService = app.Services.GetRequiredService<IProjectService>();
        _symbolParameterService = app.Services.GetRequiredService<SymbolParameterService>();
        InitializeComponent();
        CacheControls();
        DataContext = ViewModel;
        Title = "DINBoard";
#if DEBUG
        this.AttachDevTools();
#endif

        if (_canvasContainer != null && _zoomContainer != null && _selectionRectangle != null)
        {
            _canvasController = new Services.SchematicCanvasController(
                ViewModel,
                _canvasContainer,
                _zoomContainer,
                _selectionRectangle,
                _viewportCursorMarker);
            _canvasController.AttachInputHandlers();
        }

        if (_canvasContainer != null && _zoomContainer != null && _dragPreviewBorder != null && _dragPreviewImage != null)
        {
            var undoRedoService = app.Services.GetRequiredService<UndoRedoService>();
            _moduleTypeService = app.Services.GetRequiredService<IModuleTypeService>();

            _dragDropController = new Services.SchematicDragDropController(
                ViewModel,
                _canvasContainer,
                _zoomContainer,
                _dragPreviewBorder,
                _dragPreviewImage,
                _symbolImportService,
                undoRedoService,
                _moduleTypeService)
            {
                DinRailScale = _dinRailScale
            };
            _dragDropController.AttachInputHandlers();
        }

        if (_canvasContainer != null)
        {
            var axisLinesContainer = this.FindControl<Canvas>("AxisLinesContainer");
            if (_dinRailDisplay != null && axisLinesContainer != null)
            {
                _dinRailController = new Services.SchematicDinRailController(
                    ViewModel,
                    this,
                    _canvasContainer,
                    _dinRailDisplay,
                    axisLinesContainer);
                _dinRailController.DinRailScaleChanged += (s, scale) =>
                {
                    _dinRailScale = scale;
                    ViewModel.Schematic.DinRailScale = scale;
                    if (_dragDropController != null)
                        _dragDropController.DinRailScale = scale;
                };
            }
        }

        this.KeyDown += MainWindow_KeyDown;

        WeakReferenceMessenger.Default.Register<ThemeChangedMessage>(this, (_, message) =>
        {
            ApplyTheme(message.Value);
        });

        WeakReferenceMessenger.Default.Register<ShowToastMessage>(this, (r, message) =>
        {
            var toast = this.FindControl<Controls.ToastNotification>("Toast");
            toast?.Show(message.Value.Title, message.Value.Message, message.Value.Type, message.Value.DurationMs);
        });

        WeakReferenceMessenger.Default.Register<SymbolsRefreshMessage>(this, (_, _) =>
        {
            foreach (var symbol in ViewModel.Symbols)
            {
                RefreshSymbolVisual(symbol);
            }
        });

        WeakReferenceMessenger.Default.Register<DinRailRefreshMessage>(this, (_, _) =>
        {
            if (ViewModel.Schematic.IsDinRailVisible && !string.IsNullOrEmpty(ViewModel.Schematic.DinRailSvgContent) && _dinRailDisplay != null)
            {
                _dinRailDisplay.SetRail(ViewModel.Schematic.DinRailSvgContent, ViewModel.Schematic.DinRailSize.Width, ViewModel.Schematic.DinRailSize.Height);
                Canvas.SetLeft(_dinRailDisplay, -ViewModel.Schematic.DinRailSize.Width / 2);
                Canvas.SetTop(_dinRailDisplay, -ViewModel.Schematic.DinRailSize.Height / 2);
            }

            if (ViewModel.Schematic.DinRailScale > 0)
            {
                _dinRailScale = ViewModel.Schematic.DinRailScale;
                if (_dragDropController != null)
                {
                    _dragDropController.DinRailScale = ViewModel.Schematic.DinRailScale;
                    Services.AppLog.Info($"Przywrócono DinRailScale z projektu: {ViewModel.Schematic.DinRailScale:P0}");
                }
            }
        });

        // Initialize Canvas centering when window loads
        this.Loaded += InitializeCanvasTransform;

        // Schemat jednokreskowy — DI + podpięcie ViewModel
        if (_schematicDiagram != null)
        {
            if (_moduleTypeService != null)
                _schematicDiagram.SetModuleTypeService(_moduleTypeService);
            _schematicDiagram.SetViewModel(ViewModel);
        }

        var projectPanel = this.FindControl<Views.ProjectPropertiesView>("ProjectPanel");
        if (projectPanel != null)
        {
            projectPanel.PropertiesChanged += (s, e) =>
            {
                if (ViewModel.Schematic.IsSheet2Active)
                {
                    _schematicDiagram?.Rebuild();
                }
            };
        }

        ViewModel.Schematic.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SchematicViewModel.IsSheet2Active))
            {
                if (ViewModel.Schematic.IsSheet2Active)
                {
                    _schematicDiagram?.Rebuild();
                    
                    // Załaduj dane do formularza przy wejściu do arkusza 2
                    if (projectPanel != null && ViewModel.CurrentProject != null)
                    {
                        ViewModel.CurrentProject.Metadata ??= new ProjectMetadata();
                        projectPanel.LoadMetadata(ViewModel.CurrentProject.Metadata);
                    }
                }
                else
                {
                    // Zapis at the end is mostly a fallback now, as we save on TextChanged
                    if (projectPanel != null && ViewModel.CurrentProject != null)
                    {
                        ViewModel.CurrentProject.Metadata ??= new ProjectMetadata();
                        projectPanel.SaveMetadata(ViewModel.CurrentProject.Metadata);
                    }
                }
            }
            else if (e.PropertyName == nameof(SchematicViewModel.IsSingleLineLightTheme))
            {
                if (_schematicDiagram == null) return;

                var isLight = ViewModel.Schematic.IsSingleLineLightTheme;
                _schematicDiagram.IsLightTheme = isLight;

                var icon = this.FindControl<Material.Icons.Avalonia.MaterialIcon>("SchematicThemeIcon");
                var label = this.FindControl<Avalonia.Controls.TextBlock>("SchematicThemeLabel");
                if (isLight)
                {
                    if (icon != null) icon.Kind = Material.Icons.MaterialIconKind.WeatherNight;
                    if (label != null) label.Text = "Ciemny";
                }
                else
                {
                    if (icon != null) icon.Kind = Material.Icons.MaterialIconKind.WeatherSunny;
                    if (label != null) label.Text = "Jasny";
                }
            }
        };
    }

    private void OnCircuitWiringDiagramLoaded(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
    }

    private void InitializeCanvasTransform(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        _canvasController?.InitializeCanvasTransform();
    }

    private void ApplyTheme(string themeName)
    {
        if (Application.Current is App app)
        {
            app.UpdateTheme(themeName);
        }
    }

    private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (e.Key == Key.OemPlus || e.Key == Key.Add)
            {
                BtnZoomIn_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
            {
                BtnZoomOut_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.D0 || e.Key == Key.NumPad0)
            {
                BtnZoomFit_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
        }
    }

    public void RefreshSymbolVisual(Models.SymbolItem symbol)
    {
        _symbolImportService?.RefreshVisual(symbol);
    }

    public void EditSymbolParameters(Models.SymbolItem symbol)
    {
        ShowCircuitEditor(symbol);
    }

    public void ShowCircuitEditor(Models.SymbolItem symbol)
    {
        if (symbol == null) return;

        var editPanel = this.FindControl<Views.CircuitEditPanelView>("CircuitEditPanel");
        var tabControl = this.FindControl<TabControl>("RightTabControl");
        var editTab = this.FindControl<TabItem>("CircuitEditTab");

        if (editPanel == null || tabControl == null || editTab == null) return;

        editPanel.LoadSymbol(symbol,
            onSaved: () =>
            {
                _symbolParameterService?.UpdateSymbolParameters(symbol);

                // Odśwież SVG i przelicz numery
                RefreshSymbolVisual(symbol);
                ViewModel.RecalculateModuleNumbers();

                // Odśwież schemat jednokreskowy (Sheet2) aby pokazywał nowe dane
                _schematicDiagram?.Rebuild();

                // Obejście dla aktualizacji interfejsu (SymbolControl) - wymuszamy odświeżenie bindowań
                symbol.ReferenceDesignation = symbol.ReferenceDesignation; 
                symbol.CircuitName = symbol.CircuitName;

                Services.AppLog.Debug($"Zaktualizowano parametry symbolu {symbol.CircuitName}");
            },
            onClosed: () =>
            {
                // Przełącz na zakładkę "Konfiguracja" (indeks 0)
                tabControl.SelectedIndex = 0;
            });

        // Przełącz na zakładkę edycji
        tabControl.SelectedItem = editTab;
    }

    public void CenterViewOnSymbol(Models.SymbolItem symbol)
    {
        _canvasController?.CenterOnSymbol(symbol);
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (_isExitCommandClosing || _projectService?.HasUnsavedChanges != true)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        _isExitCommandClosing = true;
        try
        {
            await ViewModel.Workspace.ExitCommand.ExecuteAsync(null);
        }
        finally
        {
            _isExitCommandClosing = false;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _canvasController?.DetachInputHandlers();
        _dragDropController?.DetachInputHandlers();
        WeakReferenceMessenger.Default.UnregisterAll(this);
        ViewModel?.Dispose();
        base.OnClosed(e);
    }

    private void BtnDinRail_Click(object? sender, RoutedEventArgs e)
    {
        _ = _dinRailController?.ShowAndGenerateAsync();
    }



    private void BtnZoomIn_Click(object? sender, RoutedEventArgs e)
    {
        _canvasController?.ZoomIn();
    }

    private void BtnZoomOut_Click(object? sender, RoutedEventArgs e)
    {
        _canvasController?.ZoomOut();
    }

    private void BtnZoomFit_Click(object? sender, RoutedEventArgs e)
    {
        _canvasController?.ZoomFit();
        if (_dragDropController != null)
            _dragDropController.DinRailScale = _dinRailScale;
    }

    /// <summary>
    /// Obsługuje kliknięcie przycisku "Importuj moduły"
    /// </summary>
    private async void BtnImportModules_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new DINBoard.Dialogs.ImportModulesDialog();
            await dialog.ShowDialog(this);

            // Odśwież paletę modułów po zamknięciu dialogu
            var leftPanel = this.FindControl<DINBoard.Views.ModulesPaletteView>("LeftPanel");
            leftPanel?.ReloadModules();
        }
        catch (Exception ex)
        {
            AppLog.Error("Błąd przy otwieraniu dialogu importu modułów", ex);
            ViewModel.StatusMessage = $"Błąd: {ex.Message}";
        }
    }
}
