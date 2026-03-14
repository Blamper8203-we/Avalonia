using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DINBoard.Models;

namespace DINBoard.ViewModels;

/// <summary>
/// Zarządza logiką czysto wizualną i koordynacyjną na diagramie montażowym (arkusz 1):
/// - Przeliczanie ramek grup (obszarów wizualnych wokół zgrupowanych obwodów).
/// - Śledzenie zmian właściwości symboli (nasłuchiwanie zdarzeń), powiadamianie o zmianach Power/Phase na zewnątrz.
/// - Zarządzanie stanem i pozycjonowaniem trybu klonowania modułów.
/// </summary>
public partial class LayoutViewModel : ObservableObject, IDisposable
{
    private readonly MainViewModel _mainViewModel;
    private readonly Action _onRelevantSymbolChanged;
    private bool _disposed;

    /// <summary>Ramki grup — wyświetlane na canvasie wokół zgrupowanych modułów.</summary>
    [ObservableProperty]
    private ObservableCollection<GroupFrameInfo> _groupFrames = new();

    /// <summary>Flaga wskazująca, czy znajdujemy się w trybie duplikowania elementów.</summary>
    [ObservableProperty]
    private bool _isPlacingClones;

    /// <summary>Lista sklonowanych elementów tymczasowo "przyklejonych" do kursora w trakcie stawiania.</summary>
    public List<SymbolItem> ClonesToPlace { get; } = new();

    /// <summary>
    /// Tworzy LayoutViewModel.
    /// </summary>
    /// <param name="mainViewModel">Referencja do głównego VM (proxy), używana do dostępu do symboli i współdzielenia stanu.</param>
    /// <param name="onRelevantSymbolChanged">Akcja wywoływana, gdy zostanie zmieniona właściwość symbolu mająca wpływ na obliczenia lub walidację.</param>
    public LayoutViewModel(MainViewModel mainViewModel, Action onRelevantSymbolChanged)
    {
        _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
        _onRelevantSymbolChanged = onRelevantSymbolChanged ?? throw new ArgumentNullException(nameof(onRelevantSymbolChanged));
        
        _mainViewModel.Symbols.CollectionChanged += Symbols_CollectionChanged;
        
        foreach (var symbol in _mainViewModel.Symbols)
            HookSymbol(symbol);
            
        RecalculateGroupFrames();
    }

    private void Symbols_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
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

        _onRelevantSymbolChanged.Invoke();
        RecalculateGroupFrames();
    }

    private void HookSymbol(SymbolItem symbol)
    {
        symbol.PropertyChanged += Symbol_PropertyChanged;
    }

    private void UnhookSymbol(SymbolItem symbol)
    {
        symbol.PropertyChanged -= Symbol_PropertyChanged;
    }

    private void Symbol_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == null) return;

        if (e.PropertyName == nameof(SymbolItem.X) || e.PropertyName == nameof(SymbolItem.Y))
        {
            RecalculateGroupFrames();
            return;
        }

        var relevant = e.PropertyName == nameof(SymbolItem.PowerW) ||
                       e.PropertyName == nameof(SymbolItem.Phase) ||
                       e.PropertyName == nameof(SymbolItem.CableCrossSection) ||
                       e.PropertyName == nameof(SymbolItem.CableLength) ||
                       e.PropertyName == nameof(SymbolItem.ProtectionType) ||
                       e.PropertyName == nameof(SymbolItem.Type) ||
                       e.PropertyName == nameof(SymbolItem.RcdSymbolId) ||
                       e.PropertyName == nameof(SymbolItem.Group) ||
                       e.PropertyName == nameof(SymbolItem.GroupName);

        if (!relevant) return;

        _onRelevantSymbolChanged.Invoke();
        RecalculateGroupFrames();
    }

    /// <summary>
    /// Przelicza rozmiar i pozycję ram dla grup zdefiniowanych na zespole modułów.
    /// </summary>
    public void RecalculateGroupFrames()
    {
        const double padding = 6;
        var newFrames = new List<GroupFrameInfo>();

        var groups = _mainViewModel.Symbols
            .Where(s => !string.IsNullOrEmpty(s.Group))
            .GroupBy(s => s.Group!);

        foreach (var group in groups)
        {
            var symbols = group.ToList();
            if (symbols.Count == 0) continue;

            double minX = symbols.Min(s => s.X) - padding;
            double minY = symbols.Min(s => s.Y) - padding;
            double maxX = symbols.Max(s => s.X + s.Width) + padding;
            double maxY = symbols.Max(s => s.Y + s.Height) + padding;

            var groupName = symbols.FirstOrDefault(s => !string.IsNullOrEmpty(s.GroupName))?.GroupName ?? group.Key;

            newFrames.Add(new GroupFrameInfo
            {
                GroupName = groupName,
                X = minX,
                Y = minY,
                Width = maxX - minX,
                Height = maxY - minY
            });
        }

        GroupFrames.Clear();
        foreach (var frame in newFrames)
            GroupFrames.Add(frame);
    }

    /// <summary>
    /// Inicjuje proces duplikacji symboli - kopiuje wybrane symbole na listę ClonesToPlace.
    /// </summary>
    public void StartPlacingClones(List<SymbolItem> symbolsToDuplicate)
    {
        if (symbolsToDuplicate == null || symbolsToDuplicate.Count == 0) return;

        ClonesToPlace.Clear();

        foreach (var symbol in symbolsToDuplicate)
        {
            var clone = symbol.Clone();
            clone.ModuleNumber = _mainViewModel.Symbols.Count > 0 ? _mainViewModel.Symbols.Max(s => s.ModuleNumber) + 1 : 1;
            clone.ReferenceDesignation = "";
            clone.IsSelected = true;
            clone.Group = null;
            clone.GroupName = null;
            clone.CircuitId = Guid.NewGuid().ToString();

            _mainViewModel.Symbols.Add(clone);
            ClonesToPlace.Add(clone);
        }

        foreach (var symbol in symbolsToDuplicate)
        {
            symbol.IsSelected = false;
        }

        IsPlacingClones = true;
        _mainViewModel.StatusMessage = $"Tryb powielania ({ClonesToPlace.Count} szt.) - kliknij LMB aby postawić";
    }

    /// <summary>
    /// Finalizuje umiejscowienie klonów podanych w argumencie na canvasie.
    /// </summary>
    public void CommitClonesPlacement(List<SymbolItem> clones)
    {
        if (clones == null || clones.Count == 0) return;
        _mainViewModel.ModuleManager.CommitClonesPlacement(clones);
        IsPlacingClones = false;
        ClonesToPlace.Clear();
        _mainViewModel.HasUnsavedChanges = true;
    }

    /// <summary>
    /// Finalizuje umiejscowienie wszystkich śledzonych klonów na canvasie (metoda używana m.in. z CanvasController).
    /// </summary>
    public void CommitClonesPlacement()
    {
        if (!IsPlacingClones || ClonesToPlace.Count == 0) return;
        _mainViewModel.ModuleManager.CommitClonesPlacement(ClonesToPlace.ToList());
        IsPlacingClones = false;
        ClonesToPlace.Clear();
        _mainViewModel.HasUnsavedChanges = true;
        _mainViewModel.StatusMessage = "Powielone elementy postawione na schemacie";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_mainViewModel != null)
        {
            _mainViewModel.Symbols.CollectionChanged -= Symbols_CollectionChanged;
            foreach (var symbol in _mainViewModel.Symbols)
                UnhookSymbol(symbol);
        }

        GC.SuppressFinalize(this);
    }
}
