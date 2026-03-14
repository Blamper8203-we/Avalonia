using System;
using System.Linq;
using System.Collections;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.ComponentModel;
using System.Text.Json;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DINBoard.Constants;
using DINBoard.Models;
using DINBoard.Services;
using DINBoard.ViewModels.Messages;
using DINBoard.Services.Pdf;

namespace DINBoard.ViewModels;

/// <summary>
/// Główny ViewModel dla edytora schematów - coordinator dla pozostałych ViewModels.
/// Zarządza: symbolami, przewodami, walidacją, bilansem faz, projektami.
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    // === MAIN COLLECTIONS ===

    [ObservableProperty]
    private ObservableCollection<SymbolItem> _symbols = new();

    [ObservableProperty]
    private SymbolItem? _selectedSymbol;

    // === HOME SCREEN & LICENSING ===

    [ObservableProperty]
    private bool _isHomeScreenVisible = true;

    [ObservableProperty]
    private LicenseInfo _license = new();

    [ObservableProperty]
    private string _trialMessage = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _recentProjects = new();

    public bool HasRecentProjects => RecentProjects.Count > 0;

    // Services moved to Workspace
    // private readonly LicenseService _licenseService = new();
    // private readonly RecentProjectsService _recentProjectsService = new();

    // === INJECTED VIEWMODELS ===

    [ObservableProperty]
    private PowerBalanceViewModel _powerBalance = new();

    [ObservableProperty]
    private ProjectThemeViewModel _projectTheme = new();

    public void ForceCurrentProjectUpdate() => OnPropertyChanged(nameof(CurrentProject));

    // === VALIDATION ===

    [ObservableProperty]
    private ValidationViewModel _validation = null!;

    [ObservableProperty]
    private SchematicViewModel _schematic = null!;
    
    [ObservableProperty]
    private LayoutViewModel _layout = null!;

    [ObservableProperty]
    private CircuitListViewModel _circuitList = null!;

    // === MEMORY MONITOR ===

    [ObservableProperty]
    private string _memoryStatus = "Pamięć: --";

    [ObservableProperty]
    private long _currentMemoryBytes;

    [ObservableProperty]
    private bool _isMemoryWarning;

    // === PROJECT ===

    public ExportViewModel Exporter { get; }
    public ProjectWorkspaceViewModel Workspace { get; }
    public SymbolManagerViewModel ModuleManager { get; }

    [ObservableProperty]
    private Project? _currentProject;

    [ObservableProperty]
    private string _statusMessage = "Gotowy";

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    // === POWER CONFIG OPTIONS ===
    public int[] VoltageOptions { get; } = new[] { 230, 400 };
    public int[] ProtectionOptions { get; } = new[] { 10, 16, 20, 25, 32, 40, 50, 63, 80, 100 };
    public int[] PhaseOptions { get; } = new[] { 1, 3 };

    // === CLONE PLACEMENT ===

    public bool IsPlacingClones
    {
        get => Layout?.IsPlacingClones ?? false;
        set
        {
            if (Layout != null) Layout.IsPlacingClones = value;
            OnPropertyChanged(nameof(IsPlacingClones));
        }
    }

    public List<SymbolItem> ClonesToPlace => Layout?.ClonesToPlace ?? new List<SymbolItem>();

    // === COMPUTED STATUS BAR PROPERTIES ===
    public double TotalPowerKW => Symbols.Sum(s => s.PowerW) / 1000.0;
    public int GroupCount => Symbols.Where(s => !string.IsNullOrEmpty(s.Group)).Select(s => s.Group).Distinct().Count();
    public string ProjectFileName => !string.IsNullOrEmpty(_projectService?.CurrentProjectPath) 
        ? System.IO.Path.GetFileName(_projectService.CurrentProjectPath) 
        : "Nowy projekt";

    public void RefreshStatusBarProperties()
    {
        OnPropertyChanged(nameof(TotalPowerKW));
        OnPropertyChanged(nameof(GroupCount));
        OnPropertyChanged(nameof(ProjectFileName));
    }

    // === SERVICES ===
    private readonly IProjectService? _projectService;

    public string? CurrentProjectPath => _projectService?.CurrentProjectPath;

    private readonly IDialogService? _dialogService;
    private readonly UndoRedoService? _undoRedoService;
    private readonly IModuleTypeService _moduleTypeService;
    private readonly SymbolImportService _symbolImportService;
    private readonly ProjectPersistenceService? _persistenceService;
    private readonly IElectricalValidationService _electricalValidationService;
    private readonly PdfExportService _pdfExportService;
    private readonly BomExportService _bomExportService;
    private readonly BusbarPlacementService? _busbarPlacementService;

    // === CONSTRUCTORS ===

    /// <summary>Design-time constructor — serwisy infrastrukturalne mogą być null.</summary>
    public MainViewModel()
    {
        _projectService = null;
        _dialogService = null;
        _undoRedoService = null;
        _persistenceService = null;
        _moduleTypeService = new ModuleTypeService();
        _symbolImportService = new SymbolImportService();
        _electricalValidationService = new ElectricalValidationService();
        _pdfExportService = new PdfExportService(_moduleTypeService, _electricalValidationService, _symbolImportService, new SvgProcessor());
        _bomExportService = new BomExportService(_moduleTypeService);
        _busbarPlacementService = null;
        Validation = new ValidationViewModel(_electricalValidationService);
        Schematic = new SchematicViewModel(this);
        CircuitList = new CircuitListViewModel(Symbols, _moduleTypeService);
        Exporter = new ExportViewModel(this, new DialogService(), _pdfExportService, _bomExportService);
        Layout = new LayoutViewModel(this, () => 
        { 
            PowerBalance.RecalculatePhaseBalance(Symbols, CurrentProject); 
            RecalculateValidation(); 
        });

        Workspace = new ProjectWorkspaceViewModel(this);
        ModuleManager = new SymbolManagerViewModel(this);

        Workspace.RefreshHomeScreenData();
    }

    public MainViewModel(
        IProjectService projectService,
        IDialogService dialogService,
        UndoRedoService undoRedoService,
        IModuleTypeService moduleTypeService,
        SymbolImportService symbolImportService,
        ProjectPersistenceService persistenceService,
        IElectricalValidationService electricalValidationService,
        PdfExportService pdfExportService,
        BomExportService bomExportService,
        BusbarPlacementService busbarPlacementService)
    {
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _undoRedoService = undoRedoService ?? throw new ArgumentNullException(nameof(undoRedoService));
        _moduleTypeService = moduleTypeService ?? throw new ArgumentNullException(nameof(moduleTypeService));
        _symbolImportService = symbolImportService ?? throw new ArgumentNullException(nameof(symbolImportService));
        _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
        _electricalValidationService = electricalValidationService ?? throw new ArgumentNullException(nameof(electricalValidationService));
        _pdfExportService = pdfExportService ?? throw new ArgumentNullException(nameof(pdfExportService));
        _bomExportService = bomExportService ?? throw new ArgumentNullException(nameof(bomExportService));
        _busbarPlacementService = busbarPlacementService ?? throw new ArgumentNullException(nameof(busbarPlacementService));

        Validation = new ValidationViewModel(_electricalValidationService);
        Schematic = new SchematicViewModel(this, _projectService);
        CircuitList = new CircuitListViewModel(Symbols, _moduleTypeService);
        Exporter = new ExportViewModel(this, _dialogService, _pdfExportService, _bomExportService);
        Workspace = new ProjectWorkspaceViewModel(this, _projectService, _dialogService, _persistenceService);
        ModuleManager = new SymbolManagerViewModel(this, _undoRedoService);
        Layout = new LayoutViewModel(this, () => 
        { 
            PowerBalance.RecalculatePhaseBalance(Symbols, CurrentProject); 
            RecalculateValidation(); 
            _projectService?.MarkAsChanged();
        });

        Workspace.RefreshHomeScreenData();

        _undoRedoService.StateChanged += () =>
        {
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        };

        // Hook up theme changed callback
        ProjectTheme.OnThemeChanged += (theme) =>
        {
            WeakReferenceMessenger.Default.Send(new ThemeChangedMessage(theme));
            StatusMessage = $"Zmieniono motyw: {theme}";
        };

        CurrentProject = new Project
        {
            Name = "Nowy projekt",
            PowerConfig = new PowerSupplyConfig()
        };

        RecalculateValidation();
    }

    // === VALIDATION ===

    internal void RecalculateValidation()
    {
        var project = CurrentProject ?? new Project();
        Validation.RecalculateValidation(project, Symbols);
    }



    partial void OnCurrentProjectChanged(Project? oldValue, Project? newValue)
    {
        if (oldValue?.PowerConfig != null)
        {
            oldValue.PowerConfig.PropertyChanged -= PowerConfig_PropertyChanged;
        }

        if (newValue?.PowerConfig != null)
        {
            newValue.PowerConfig.PropertyChanged += PowerConfig_PropertyChanged;
        }

        RecalculateValidation();
        RecalculatePhaseBalance();
    }

    partial void OnSymbolsChanged(ObservableCollection<SymbolItem> oldValue, ObservableCollection<SymbolItem> newValue)
    {
        if (oldValue != null)
        {
            oldValue.CollectionChanged -= Symbols_CollectionChanged;
            foreach (var symbol in oldValue)
            {
                symbol.PropertyChanged -= Symbol_PropertyChanged;
            }
        }

        if (newValue != null)
        {
            newValue.CollectionChanged += Symbols_CollectionChanged;
            foreach (var symbol in newValue)
            {
                symbol.PropertyChanged += Symbol_PropertyChanged;
            }
        }
        RecalculatePhaseBalance();
        RecalculateValidation();
    }

    private void Symbols_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (SymbolItem item in e.OldItems)
                item.PropertyChanged -= Symbol_PropertyChanged;
        }

        if (e.NewItems != null)
        {
            foreach (SymbolItem item in e.NewItems)
                item.PropertyChanged += Symbol_PropertyChanged;
        }
        
        RecalculatePhaseBalance();
        RecalculateValidation();
    }

    private void Symbol_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SymbolItem.Phase) ||
            e.PropertyName == nameof(SymbolItem.PowerW) ||
            e.PropertyName == nameof(SymbolItem.CircuitType) ||
            e.PropertyName == nameof(SymbolItem.IsPhaseLocked) ||
            e.PropertyName == nameof(SymbolItem.RcdRatedCurrent) ||
            e.PropertyName == nameof(SymbolItem.RcdSymbolId) ||
            e.PropertyName == nameof(SymbolItem.Type) ||
            e.PropertyName == nameof(SymbolItem.Width))
        {
            RecalculatePhaseBalance();
            RecalculateValidation();
            _projectService?.MarkAsChanged();
        }
    }


    private void PowerConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RecalculateValidation();
        _projectService?.MarkAsChanged();
    }

    [ObservableProperty]
    private bool _isRightPanelVisible = true;

    [RelayCommand]
    private void ToggleRightPanel() => IsRightPanelVisible = !IsRightPanelVisible;

    public void NavigateToReference(CircuitReference reference) => Schematic.NavigateToReference(reference);

    // === EXPORT === (Moved to ExportViewModel)

    // === MODULE MANAGEMENT ===

    /// <summary>
    /// Usuwa moduł (symbol) z projektu
    /// </summary>
    /// <param name="symbol">Moduł do usunięcia</param>
    public void DeleteModule(SymbolItem? symbol) => ModuleManager.DeleteModule(symbol);

    /// <summary>
    /// Usuwa wiele modułów naraz
    /// </summary>
    /// <param name="symbols">Moduły do usunięcia</param>
    public void DeleteMultipleModules(IList symbols) => ModuleManager.DeleteMultipleModules(symbols);

    // === PHASE BALANCE CALCULATION ===

    public void RecalculatePhaseBalance()
    {
        // Delegacja do PowerBalanceViewModel — jedyne źródło prawdy
        PowerBalance.RecalculatePhaseBalance(Symbols, CurrentProject);
    }

    [RelayCommand]
    private async Task GenerateBusbarAsync()
    {
        if (_dialogService == null || _busbarPlacementService == null)
        {
            return;
        }

        var result = await _dialogService.ShowBusbarGeneratorDialogAsync();
        if (result == null)
        {
            return;
        }

        var symbol = _busbarPlacementService.GenerateAndImportBusbar(
            result.PinCount,
            result.BusbarType,
            Schematic.DinRailScale);

        if (symbol == null)
        {
            StatusMessage = "Błąd generowania szyny prądowej";
            return;
        }

        AddBusbar(symbol, result.PinCount);
    }

    /// <summary>
    /// Dodaje wygenerowaną szynę prądową do projektu i aktualizuje stan.
    /// </summary>
    /// <param name="symbol">Symbol szyny prądowej.</param>
    /// <param name="pinCount">Liczba biegunów (pinów).</param>
    public void AddBusbar(SymbolItem symbol, int pinCount)
    {
        if (symbol == null)
        {
            return;
        }

        Symbols.Add(symbol);
        RecalculateModuleNumbers();
        StatusMessage = $"Dodano szynę prądową {pinCount}P";
        _projectService?.MarkAsChanged();
    }

    // === GROUPS & MODULE NUMBERS ===

    private static int? TryExtractPositiveNumber(string? text)
        => CommonHelpers.TryExtractPositiveNumber(text);

    internal void EnsureProjectGroupsFromSymbols() => ModuleManager.EnsureProjectGroupsFromSymbols();

    public void RecalculateModuleNumbers() => ModuleManager.RecalculateModuleNumbers();

    // === UNDO/REDO & PLACEMENT COMMANDS ===

    public void StartPlacingClones(List<SymbolItem> symbolsToDuplicate) => Layout.StartPlacingClones(symbolsToDuplicate);
    public void CommitClonesPlacement(List<SymbolItem> clones) => Layout.CommitClonesPlacement(clones);
    public void CommitClonesPlacement() => Layout.CommitClonesPlacement();

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        _undoRedoService?.Undo();
    }

    public bool CanUndo => _undoRedoService?.CanUndo ?? false;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        _undoRedoService?.Redo();
    }

    public bool CanRedo => _undoRedoService?.CanRedo ?? false;

    // === PROJECT LIFECYCLE COMMANDS ===
    // Moved to ProjectWorkspaceViewModel


    // === DISPOSE ===

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Layout?.Dispose();
        CircuitList?.Dispose();

        GC.SuppressFinalize(this);
    }
}

