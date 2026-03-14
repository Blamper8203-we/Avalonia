using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using DINBoard.Dialogs;
using DINBoard.Models;
using DINBoard.ViewModels;
using DINBoard.Services;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace DINBoard.Views;

public partial class GroupedCircuitsPanel : UserControl
{
#pragma warning disable CS0067 // Event is never used - will be implemented later
    public event EventHandler<Circuit>? CircuitEditRequested;
#pragma warning restore CS0067

    private ItemsControl? _circuitsList;
    private Border? _emptyPlaceholder;
    private readonly IModuleTypeService _moduleTypeService;

    public GroupedCircuitsPanel()
    {
        _moduleTypeService = ((App)Application.Current!).Services.GetRequiredService<IModuleTypeService>();
        InitializeComponent();
        Loaded += GroupedCircuitsPanel_Loaded;
        DataContextChanged += GroupedCircuitsPanel_DataContextChanged;
    }

    private MainViewModel? _viewModel;
    private NotifyCollectionChangedEventHandler? _symbolsCollectionChangedHandler;
    private bool _isInitialized = false;

    private void GroupedCircuitsPanel_DataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            // Upewnij się, że kontrolki są załadowane
            EnsureControlsLoaded();
            AttachViewModel(vm);
        }
        else
        {
            DetachViewModel();
        }
    }

    private void EnsureControlsLoaded()
    {
        _circuitsList ??= this.FindControl<ItemsControl>("CircuitsList");
        _emptyPlaceholder ??= this.FindControl<Border>("EmptyPlaceholder");
    }

    private void GroupedCircuitsPanel_Loaded(object? sender, RoutedEventArgs e)
    {
        if (_isInitialized) return;

        try
        {
            EnsureControlsLoaded();

            if (_circuitsList == null || _emptyPlaceholder == null)
            {
                Services.AppLog.Warn("GroupedCircuitsPanel: Nie znaleziono kontrolek");
                return;
            }

            // Znajdź MainViewModel z DataContext okna
            var mainWindow = TopLevel.GetTopLevel(this) as Window;
            if (mainWindow?.DataContext is MainViewModel viewModel)
            {
                AttachViewModel(viewModel);
            }
        }
        catch (Exception ex)
        {
            Services.AppLog.Error("GroupedCircuitsPanel: Błąd w Loaded", ex);
        }
    }

    private void AttachViewModel(MainViewModel viewModel)
    {
        if (_viewModel == viewModel && _isInitialized) return;
        DetachViewModel();

        try
        {
            _viewModel = viewModel;

            _symbolsCollectionChangedHandler = Symbols_CollectionChanged;
            viewModel.Symbols.CollectionChanged += _symbolsCollectionChangedHandler;

            // Subskrybuj zmiany właściwości Group w istniejących symbolach
            try
            {
                foreach (var symbol in viewModel.Symbols)
                {
                    symbol.PropertyChanged += Symbol_PropertyChanged;
                }
            }
            catch (Exception ex)
            {
                Services.AppLog.Error("GroupedCircuitsPanel: Błąd subskrypcji PropertyChanged", ex);
            }

            // Inicjalna aktualizacja
            UpdateGroups(viewModel);
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Services.AppLog.Error("GroupedCircuitsPanel: Błąd w InitializeViewModel", ex);
        }
    }

    private void DetachViewModel()
    {
        if (_viewModel != null)
        {
            if (_symbolsCollectionChangedHandler != null)
            {
                _viewModel.Symbols.CollectionChanged -= _symbolsCollectionChangedHandler;
            }

            foreach (var symbol in _viewModel.Symbols)
            {
                symbol.PropertyChanged -= Symbol_PropertyChanged;
            }
        }

        _viewModel = null;
        _symbolsCollectionChangedHandler = null;
        _isInitialized = false;
    }

    private void Symbols_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (_viewModel == null) return;

        try
        {
            if (args.NewItems != null)
            {
                foreach (SymbolItem symbol in args.NewItems)
                {
                    symbol.PropertyChanged += Symbol_PropertyChanged;
                }
            }

            if (args.OldItems != null)
            {
                foreach (SymbolItem symbol in args.OldItems)
                {
                    symbol.PropertyChanged -= Symbol_PropertyChanged;
                }
            }

            UpdateGroups(_viewModel);
        }
        catch (Exception ex)
        {
            Services.AppLog.Error("GroupedCircuitsPanel: Błąd w CollectionChanged", ex);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DetachViewModel();
    }

    private void Symbol_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Reaguj na zmiany właściwości wpływających na widok hierarchii
        var relevantProperties = new[]
        {
            nameof(SymbolItem.Group),
            nameof(SymbolItem.CircuitName),
            nameof(SymbolItem.ProtectionType),
            nameof(SymbolItem.PowerW),
            nameof(SymbolItem.Phase),
            nameof(SymbolItem.Location),
            nameof(SymbolItem.Visual),
            nameof(SymbolItem.RcdRatedCurrent),
            nameof(SymbolItem.RcdResidualCurrent),
            nameof(SymbolItem.RcdType)
        };

        if (e.PropertyName != null && relevantProperties.Contains(e.PropertyName) && _viewModel != null)
        {
            // Użyj Dispatcher dla bezpiecznej aktualizacji UI
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                EnsureControlsLoaded();
                UpdateGroups(_viewModel);
            });
        }
    }

    private void UpdateGroups(MainViewModel viewModel)
    {
        try
        {
            if (_circuitsList == null || _emptyPlaceholder == null)
            {
                Services.AppLog.Warn("GroupedCircuitsPanel: UpdateGroups - brak kontrolek");
                return;
            }

            if (viewModel?.Symbols == null)
            {
                Services.AppLog.Warn("GroupedCircuitsPanel: UpdateGroups - brak Symbols");
                _circuitsList.ItemsSource = new List<GroupViewModel>();
                _emptyPlaceholder.IsVisible = true;
                return;
            }

            List<GroupViewModel> groups;
            try
            {
                groups = GroupViewModel.CreateGroupsFromSymbols(viewModel.Symbols, viewModel.CurrentProject?.Groups, _moduleTypeService);
            }
            catch (Exception ex2)
            {
                Services.AppLog.Error("GroupedCircuitsPanel: Błąd tworzenia grup", ex2);
                groups = new List<GroupViewModel>();
            }

            // Ustaw ItemsSource bezpośrednio (jesteśmy już na UI thread)
            try
            {
                _circuitsList.ItemsSource = groups;
                _emptyPlaceholder.IsVisible = groups.Count == 0;
            }
            catch (Exception ex3)
            {
                Services.AppLog.Error("GroupedCircuitsPanel: Błąd ustawiania ItemsSource", ex3);
            }
        }
        catch (Exception ex)
        {
            Services.AppLog.Error("GroupedCircuitsPanel: Błąd w UpdateGroups", ex);
        }
    }

    public void UpdateCircuit(Circuit circuit)
    {
        // Refresh circuit display
        // This would update the ItemsSource binding
    }

    private void UngroupButton_Click(object? sender, RoutedEventArgs e)
    {
        string? groupId = null;

        // Obsłuż zarówno Button jak i MenuItem
        if (sender is Button button && button.Tag is string btnGroupId)
            groupId = btnGroupId;
        else if (sender is MenuItem menuItem && menuItem.Tag is string menuGroupId)
            groupId = menuGroupId;
        else if (sender is MenuItem mi && mi.DataContext is GroupViewModel gvm)
            groupId = gvm.GroupId;

        if (groupId != null && _viewModel != null)
        {
            var symbolsInGroup = _viewModel.Symbols.Where(s => s.Group == groupId).ToList();
            foreach (var symbol in symbolsInGroup)
            {
                symbol.Group = null;
                symbol.GroupName = null;
                symbol.IsInSelectedGroup = false;
            }

            UpdateGroups(_viewModel);
            Services.AppLog.Debug($"GroupedCircuitsPanel: Rozgrupowano grupę {groupId}");
        }
    }

    private void DeleteGroup_Click(object? sender, RoutedEventArgs e)
    {
        string? groupId = null;

        if (sender is MenuItem menuItem && menuItem.Tag is string menuGroupId)
            groupId = menuGroupId;
        else if (sender is MenuItem mi && mi.DataContext is GroupViewModel gvm)
            groupId = gvm.GroupId;

        if (groupId != null && _viewModel != null)
        {
            // Usuń wszystkie symbole w grupie
            var symbolsToRemove = _viewModel.Symbols.Where(s => s.Group == groupId).ToList();
            foreach (var symbol in symbolsToRemove)
            {
                _viewModel.Symbols.Remove(symbol);
            }

            UpdateGroups(_viewModel);
            Services.AppLog.Debug($"GroupedCircuitsPanel: Usunięto grupę {groupId} wraz z {symbolsToRemove.Count} symbolami");
        }
    }

    private void GroupName_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.Tag is string groupId && _viewModel != null)
        {
            var newName = textBox.Text;

            // Zapisz nazwę grupy do wszystkich symboli w tej grupie
            var symbolsInGroup = _viewModel.Symbols.Where(s => s.Group == groupId).ToList();
            foreach (var symbol in symbolsInGroup)
            {
                symbol.GroupName = newName;
            }

            Services.AppLog.Debug($"GroupedCircuitsPanel: Zmieniono nazwę grupy {groupId} na: {newName}");
        }
    }

    private void ExpandButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string groupId)
        {
            // Znajdź Border z listą obwodów w tej grupie i przełącz widoczność
            // TODO: Implementacja zwijania/rozwijania grupy
            // Na razie tylko logujemy
            Services.AppLog.Debug($"GroupedCircuitsPanel: Kliknięto rozwiń/zwiń dla grupy {groupId}");
        }
    }

    private async void EditGroupModules_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Pobierz GroupViewModel z DataContext menu item
            GroupViewModel? group = null;

            if (sender is MenuItem menuItem && menuItem.DataContext is GroupViewModel gvm)
            {
                group = gvm;
            }

            if (group == null)
            {
                Services.AppLog.Warn("GroupedCircuitsPanel: Nie znaleziono grupy do edycji");
                return;
            }

            var mainWindow = TopLevel.GetTopLevel(this) as Window;
            if (mainWindow == null) return;

            var dialog = new GroupModulesDialog(group);
            var result = await dialog.ShowDialog<bool?>(mainWindow);

            if (result == true && dialog.WasModified)
            {
                Services.AppLog.Debug($"GroupedCircuitsPanel: Zapisano zmiany modułów w grupie {group.Name}");

                // Odśwież widok
                if (_viewModel != null)
                {
                    UpdateGroups(_viewModel);
                }
            }
        }
        catch (Exception ex)
        {
            Services.AppLog.Error("GroupedCircuitsPanel: Błąd edycji grupy", ex);
        }
    }

    private async void EditCircuit_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Pobierz SymbolItem z DataContext menu item
            SymbolItem? symbol = null;

            if (sender is MenuItem menuItem && menuItem.DataContext is SymbolItem si)
            {
                symbol = si;
            }

            if (symbol == null)
            {
                Services.AppLog.Warn("GroupedCircuitsPanel: Nie znaleziono obwodu do edycji");
                return;
            }

            var mainWindow = TopLevel.GetTopLevel(this) as Window;
            if (mainWindow == null) return;

            var dialog = new CircuitEditDialog(symbol);
            var result = await dialog.ShowDialog<bool?>(mainWindow);

            if (result == true && dialog.WasModified)
            {
                Services.AppLog.Debug($"GroupedCircuitsPanel: Zapisano zmiany obwodu {symbol.CircuitName}");

                // Odśwież widok i przelicz bilans faz
                if (_viewModel != null)
                {
                    _viewModel.RecalculateModuleNumbers(); // To wywoła też RecalculatePhaseBalance()
                    UpdateGroups(_viewModel);
                }
            }
        }
        catch (Exception ex)
        {
            Services.AppLog.Error("GroupedCircuitsPanel: Błąd edycji obwodu", ex);
        }
    }

    private void MoveToGroup_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.DataContext is not SymbolItem symbol || _viewModel == null)
            return;

        var groups = GroupViewModel.CreateGroupsFromSymbols(
            _viewModel.Symbols, _viewModel.CurrentProject?.Groups, _moduleTypeService);
        var currentGroupId = symbol.Group;
        var otherGroups = groups.Where(g => g.GroupId != currentGroupId).ToList();

        if (otherGroups.Count == 0)
        {
            Services.AppLog.Warn("MoveToGroup: Brak innych grup do przeniesienia");
            return;
        }

        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var ctx = new ContextMenu();
            foreach (var g in otherGroups)
            {
                var targetGroupId = g.GroupId;
                var targetName = g.Name;
                var mi = new MenuItem { Header = targetName };
                mi.Click += (_, _) => MoveSymbolToGroup(symbol, targetGroupId);
                ctx.Items.Add(mi);
            }
            ctx.Open(this);
        }, global::Avalonia.Threading.DispatcherPriority.Background);
    }

    private void MoveSymbolToGroup(SymbolItem symbol, string targetGroupId)
    {
        if (_viewModel == null) return;

        var oldGroup = symbol.Group;
        symbol.Group = targetGroupId;

        var rcdInTarget = _viewModel.Symbols
            .FirstOrDefault(s => s.Group == targetGroupId && _moduleTypeService.IsRcd(s));
        if (rcdInTarget != null)
        {
            symbol.RcdSymbolId = rcdInTarget.Id;
            symbol.RcdRatedCurrent = rcdInTarget.RcdRatedCurrent;
            symbol.RcdResidualCurrent = rcdInTarget.RcdResidualCurrent;
            symbol.RcdType = rcdInTarget.RcdType;
            symbol.Phase = rcdInTarget.Phase;
        }

        // Kompaktuj źródłową grupę (wypełnij lukę po zabranym MCB)
        if (!string.IsNullOrEmpty(oldGroup))
        {
            var sourceGroupSymbols = _viewModel.Symbols
                .Where(s => s.Group == oldGroup)
                .ToList();
            PhaseDistributionCalculator.CompactGroup(sourceGroupSymbols);
        }

        // Kompaktuj docelową grupę (MCB ustawi się za RCD / ostatnim MCB)
        var targetGroupSymbols = _viewModel.Symbols
            .Where(s => s.Group == targetGroupId)
            .ToList();
        PhaseDistributionCalculator.CompactGroup(targetGroupSymbols);

        _viewModel.RecalculateModuleNumbers();
        UpdateGroups(_viewModel);

        foreach (var s in _viewModel.Symbols) s.IsSelected = false;
        symbol.IsSelected = true;

        Services.AppLog.Debug($"MoveToGroup: Przeniesiono {symbol.CircuitName} z grupy {oldGroup} do {targetGroupId}");
    }

    private void OnCircuitPointerPressed(object? sender, global::Avalonia.Input.PointerPressedEventArgs e)
    {
        var border = sender as Border;
        if (border?.DataContext is SymbolItem symbol && _viewModel != null)
        {
            HandleSelection(symbol, e);

            if (e.ClickCount == 2)
            {
                var mainWindow = TopLevel.GetTopLevel(this) as MainWindow;
                mainWindow?.CenterViewOnSymbol(symbol);
            }
        }
    }

    private void OnGroupHeaderPointerPressed(object? sender, global::Avalonia.Input.PointerPressedEventArgs e)
    {
        var border = sender as Border;
        // DataContext tutaj to GroupViewModel.
        if (border?.DataContext is GroupViewModel groupVM && groupVM.MainSymbol != null && _viewModel != null)
        {
            HandleSelection(groupVM.MainSymbol, e);

            if (e.ClickCount == 2)
            {
                var mainWindow = TopLevel.GetTopLevel(this) as MainWindow;
                mainWindow?.CenterViewOnSymbol(groupVM.MainSymbol);
            }
        }
    }

    private void HandleSelection(SymbolItem symbol, global::Avalonia.Input.PointerPressedEventArgs e)
    {
        // Sprawdź modyfikatory (Ctrl/Shift) dla multiselect
        bool isMultiSelect = e.KeyModifiers.HasFlag(global::Avalonia.Input.KeyModifiers.Control) ||
                             e.KeyModifiers.HasFlag(global::Avalonia.Input.KeyModifiers.Shift);

        if (isMultiSelect)
        {
            symbol.IsSelected = !symbol.IsSelected;
        }
        else
        {
            // Odznacz wszystko inne
            foreach (var s in _viewModel!.Symbols)
            {
                s.IsSelected = false;
            }
            symbol.IsSelected = true;
        }
    }
}
