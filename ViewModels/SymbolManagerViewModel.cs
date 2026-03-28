using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DINBoard.Models;
using DINBoard.Services;
using DINBoard.ViewModels.Messages;

namespace DINBoard.ViewModels;

public partial class SymbolManagerViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;
    private readonly UndoRedoService? _undoRedoService;

    public SymbolManagerViewModel(MainViewModel mainViewModel, UndoRedoService? undoRedoService = null)
    {
        _mainViewModel = mainViewModel;
        _undoRedoService = undoRedoService;
    }

    // === UNDO / REDO ===

    [RelayCommand(CanExecute = nameof(CanUndo))]
    public void Undo()
    {
        _undoRedoService?.Undo();
        _mainViewModel.ForceCurrentProjectUpdate();
    }

    public bool CanUndo => _undoRedoService?.CanUndo ?? false;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    public void Redo()
    {
        _undoRedoService?.Redo();
        _mainViewModel.ForceCurrentProjectUpdate();
    }

    public bool CanRedo => _undoRedoService?.CanRedo ?? false;

    // === MODULE MANAGEMENT ===

    public void DeleteModule(SymbolItem? symbol)
    {
        if (symbol == null || !_mainViewModel.Symbols.Contains(symbol)) return;

        int index = _mainViewModel.Symbols.IndexOf(symbol);
        var originalGroup = symbol.Group;
        
        var action = new ActionCommand(
            () => 
            {
                _mainViewModel.Symbols.Remove(symbol);
                if (symbol.IsSelected)
                {
                    symbol.IsSelected = false;
                    if (_mainViewModel.SelectedSymbol == symbol)
                        _mainViewModel.SelectedSymbol = null;
                }
                RecalculateModuleNumbers();
                _mainViewModel.StatusMessage = $"Usunięto moduł: {symbol.Label ?? "Nieznany"}";
            },
            () => 
            {
                if (index >= 0 && index <= _mainViewModel.Symbols.Count)
                    _mainViewModel.Symbols.Insert(index, symbol);
                else
                    _mainViewModel.Symbols.Add(symbol);
                    
                symbol.Group = originalGroup;
                RecalculateModuleNumbers();
                _mainViewModel.StatusMessage = $"Przywrócono moduł: {symbol.Label ?? "Nieznany"}";
            }
        );
        
        if (_undoRedoService == null)
        {
            _mainViewModel.Symbols.Remove(symbol);
            if (symbol.IsSelected)
            {
                symbol.IsSelected = false;
                if (_mainViewModel.SelectedSymbol == symbol)
                    _mainViewModel.SelectedSymbol = null;
            }
            _mainViewModel.StatusMessage = $"Usunięto moduł: {symbol.Label ?? "Nieznany"}";
            _mainViewModel.MarkProjectAsChanged();
            return;
        }

        _undoRedoService?.Execute(action);
        _mainViewModel.MarkProjectAsChanged();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    public void DeleteMultipleModules(IList symbolsToRemove)
    {
        if (symbolsToRemove == null || symbolsToRemove.Count == 0) return;

        var symbolsList = symbolsToRemove.Cast<SymbolItem>().ToList();
        var indexMap = new Dictionary<SymbolItem, int>();
        var groupMap = new Dictionary<SymbolItem, string?>();

        foreach (var sym in symbolsList)
        {
            indexMap[sym] = _mainViewModel.Symbols.IndexOf(sym);
            groupMap[sym] = sym.Group;
        }

        var action = new ActionCommand(
            () => 
            {
                foreach (var sym in symbolsList)
                {
                    _mainViewModel.Symbols.Remove(sym);
                    if (sym.IsSelected) sym.IsSelected = false;
                }
                if (_mainViewModel.SelectedSymbol != null && symbolsList.Contains(_mainViewModel.SelectedSymbol))
                {
                    _mainViewModel.SelectedSymbol = null;
                }
                RecalculateModuleNumbers();
                _mainViewModel.StatusMessage = $"Usunięto {symbolsList.Count} modułów";
            },
            () => 
            {
                var sortedByOriginalIndex = symbolsList.OrderBy(s => indexMap[s]).ToList();
                foreach (var sym in sortedByOriginalIndex)
                {
                    int idx = indexMap[sym];
                    sym.Group = groupMap[sym];
                    if (idx >= 0 && idx <= _mainViewModel.Symbols.Count)
                        _mainViewModel.Symbols.Insert(idx, sym);
                    else
                        _mainViewModel.Symbols.Add(sym);
                }
                RecalculateModuleNumbers();
                _mainViewModel.StatusMessage = $"Przywrócono {symbolsList.Count} modułów";
            }
        );

        if (_undoRedoService == null)
        {
            foreach (var sym in symbolsList)
            {
                _mainViewModel.Symbols.Remove(sym);
                if (sym.IsSelected) sym.IsSelected = false;
            }
            if (_mainViewModel.SelectedSymbol != null && symbolsList.Contains(_mainViewModel.SelectedSymbol))
                _mainViewModel.SelectedSymbol = null;
            _mainViewModel.MarkProjectAsChanged();
            _mainViewModel.StatusMessage = $"Usunięto {symbolsList.Count} modułów";
            return;
        }

        _undoRedoService?.Execute(action);
        _mainViewModel.MarkProjectAsChanged();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    public void DeleteSelected()
    {
        var selectedSymbols = _mainViewModel.Symbols.Where(s => s.IsSelected).ToList();
        
        if (selectedSymbols.Count > 0)
        {
            DeleteMultipleModules(selectedSymbols);
            _mainViewModel.SelectedSymbol = null;
        }
        else if (_mainViewModel.SelectedSymbol != null)
        {
            _mainViewModel.Symbols.Remove(_mainViewModel.SelectedSymbol);
            _mainViewModel.SelectedSymbol = null;
            _mainViewModel.MarkProjectAsChanged();
        }
    }

    [RelayCommand]
    public void DeleteSymbol(SymbolItem? symbol)
    {
        if (symbol == null) return;
        DeleteModule(symbol);
    }

    [RelayCommand]
    public void DuplicateSelected()
    {
        var symbolsToDuplicate = _mainViewModel.Symbols.Where(s => s.IsSelected).ToList();
        if (symbolsToDuplicate.Count == 0 && _mainViewModel.SelectedSymbol != null)
        {
            symbolsToDuplicate.Add(_mainViewModel.SelectedSymbol);
        }

        if (symbolsToDuplicate.Count == 0) return;

        foreach (var symbol in _mainViewModel.Symbols)
        {
            symbol.IsSelected = false;
        }

        int duplicatedCount = 0;
        
        double totalSelectionWidth = 0;
        if (symbolsToDuplicate.Any())
        {
            var minX = symbolsToDuplicate.Min(s => s.X);
            var maxX = symbolsToDuplicate.Max(s => s.X + s.Width);
            totalSelectionWidth = maxX - minX;
        }

        double groupOffset = totalSelectionWidth + 20; 
        
        foreach (var symbol in symbolsToDuplicate)
        {
            var clone = symbol.Clone();
            clone.X += groupOffset;
            clone.ModuleNumber = _mainViewModel.Symbols.Count > 0 ? _mainViewModel.Symbols.Max(s => s.ModuleNumber) + 1 : 1;
            clone.ReferenceDesignation = "";
            clone.IsSelected = true;

            _mainViewModel.Symbols.Add(clone);
            duplicatedCount++;
        }

        _mainViewModel.SelectedSymbol = _mainViewModel.Symbols.LastOrDefault(s => s.IsSelected);
        
        _mainViewModel.MarkProjectAsChanged();
        _mainViewModel.StatusMessage = $"Skopiowano {duplicatedCount} elementów";
        RecalculateModuleNumbers();
    }

    public void StartPlacingClones(List<SymbolItem> symbolsToDuplicate)
    {
        if (symbolsToDuplicate == null || symbolsToDuplicate.Count == 0) return;

        var clones = new List<SymbolItem>();
        
        foreach (var symbol in symbolsToDuplicate)
        {
            var clone = symbol.Clone();
            
            clone.ModuleNumber = _mainViewModel.Symbols.Count > 0 ? _mainViewModel.Symbols.Max(s => s.ModuleNumber) + 1 : 1;
            clone.ReferenceDesignation = "";
            clone.IsSelected = true;
            
            clones.Add(clone);
        }

        foreach (var symbol in _mainViewModel.Symbols)
        {
            symbol.IsSelected = false;
        }
        
        var message = new StartPlacementMessage(clones, isCloningMode: true);
        WeakReferenceMessenger.Default.Send(message);
        
        _mainViewModel.StatusMessage = $"Wskaż miejsce na planszy, aby umieścić skopiowane element(y). Naciśnij ESC, aby anulować.";
    }

    public void CommitClonesPlacement(List<SymbolItem> clones)
    {
        if (clones == null || clones.Count == 0) return;

        var addedClones = new List<SymbolItem>(clones);

        var action = new ActionCommand(
            () => 
            {
                foreach (var clone in addedClones)
                {
                    if (!_mainViewModel.Symbols.Contains(clone))
                    {
                        _mainViewModel.Symbols.Add(clone);
                    }
                }
                _mainViewModel.SelectedSymbol = addedClones.LastOrDefault();
                RecalculateModuleNumbers();
                _mainViewModel.StatusMessage = $"Skopiowano i umieszczono {addedClones.Count} elementów";
            },
            () => 
            {
                foreach (var clone in addedClones)
                {
                    _mainViewModel.Symbols.Remove(clone);
                }
                _mainViewModel.SelectedSymbol = _mainViewModel.Symbols.LastOrDefault(s => s.IsSelected);
                RecalculateModuleNumbers();
                _mainViewModel.StatusMessage = $"Cofnięto skopiowanie i umieszczenie elementów";
            }
        );

        _undoRedoService?.Execute(action);
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    public void EnsureProjectGroupsFromSymbols()
    {
        if (_mainViewModel.CurrentProject == null) return;

        _mainViewModel.CurrentProject.Groups ??= new List<CircuitGroup>();

        var existingGroupIds = new HashSet<string>(_mainViewModel.CurrentProject.Groups.Select(g => g.Id));
        var symbolGroups = _mainViewModel.Symbols
            .Where(s => !string.IsNullOrEmpty(s.Group))
            .Select(s => s.Group!)
            .Distinct();

        bool changed = false;
        foreach (var groupId in symbolGroups)
        {
            if (!existingGroupIds.Contains(groupId))
            {
                _mainViewModel.CurrentProject.Groups.Add(new CircuitGroup 
                { 
                    Id = groupId, 
                    Name = groupId,
                    Order = _mainViewModel.CurrentProject.Groups.Count > 0 ? _mainViewModel.CurrentProject.Groups.Max(g => g.Order) + 1 : 1
                });
                changed = true;
            }
        }

        if (changed)
        {
            _mainViewModel.RecalculateValidation();
            WeakReferenceMessenger.Default.Send(new ProjectGroupsChangedMessage());
        }
    }

    public void RecalculateModuleNumbers()
    {
        EnsureProjectGroupsFromSymbols();

        var rawGroups = _mainViewModel.Symbols
            .Where(s => !string.IsNullOrEmpty(s.Group))
            .GroupBy(s => s.Group!)
            .ToList();

        var orderByGroupId = (_mainViewModel.CurrentProject?.Groups ?? new List<CircuitGroup>())
            .Where(g => g.Order > 0)
            .GroupBy(g => g.Id)
            .ToDictionary(g => g.Key, g => g.First().Order, StringComparer.Ordinal);

        var roundedGroups = rawGroups
            .Select(g => new
            {
                Group = g,
                GroupId = g.Key,
                Order = orderByGroupId.TryGetValue(g.Key, out var o) ? o : 0,
                MinY = g.Min(s => s.Y),
                MinX = g.Min(s => s.X)
            })
            .OrderBy(gw => gw.Order > 0 ? gw.Order : int.MaxValue)
            .ThenBy(gw => gw.MinY)
            .ThenBy(gw => gw.MinX)
            .ThenBy(gw => gw.GroupId)
            .Select(gw => gw.Group)
            .ToList();

        int terminalCounter = 1;

        foreach (var group in roundedGroups)
        {
            string groupId = group.Key;
            var modules = group.ToList();

            var rcds = modules.Where(s => s.Type?.Contains("RCD", StringComparison.OrdinalIgnoreCase) == true).ToList();
            foreach (var rcd in rcds)
                rcd.ModuleNumber = 0;

            double referenceX = rcds.Count > 0 ? rcds[0].X : 0;
            bool hasRcd = rcds.Count > 0;

            var othersQuery = modules.Where(s => s.Type?.Contains("RCD", StringComparison.OrdinalIgnoreCase) != true);

            List<SymbolItem> others = hasRcd
                ? othersQuery.OrderBy(s => Math.Abs(s.X - referenceX)).ToList()
                : othersQuery.OrderBy(s => s.X).ToList();

            int groupCounter = 1;
            foreach (var m in others)
            {
                if (m.IsTerminalBlock)
                {
                    if (m.ModuleNumber != terminalCounter) m.ModuleNumber = terminalCounter;
                    terminalCounter++;
                }
                else
                {
                    if (m.ModuleNumber != groupCounter) m.ModuleNumber = groupCounter;
                    groupCounter++;
                }
            }

            if (_mainViewModel.CurrentProject != null)
            {
                var firstSym = modules.FirstOrDefault();
                if (firstSym != null)
                {
                    var circuitGroup = _mainViewModel.CurrentProject.Groups.FirstOrDefault(g => g.Id == groupId);
                    if (circuitGroup != null && circuitGroup.Circuits != null)
                    {
                        var orderedSymbols = rcds.Concat(others).ToList();
                        var sortedCircuits = circuitGroup.Circuits
                            .OrderBy(c =>
                            {
                                var s = _mainViewModel.Symbols.FirstOrDefault(sym => sym.CircuitId == c.Id);
                                return s != null ? orderedSymbols.IndexOf(s) : 999;
                            })
                            .ToList();
                        circuitGroup.Circuits = sortedCircuits;
                    }
                }
            }
        }

        WeakReferenceMessenger.Default.Send(new SymbolsRefreshMessage());

        _mainViewModel.RecalculatePhaseBalance();
    }

    private sealed class ActionCommand : IUndoableCommand
    {
        private readonly Action _execute;
        private readonly Action _undo;

        public ActionCommand(Action execute, Action undo)
        {
            _execute = execute;
            _undo = undo;
        }

        public void Execute() => _execute();
        public void Undo() => _undo();
    }
}
