using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using DINBoard.Models;
using DINBoard.Services;

namespace DINBoard.ViewModels;

public partial class CircuitListViewModel : ObservableObject, IDisposable
{
    private readonly IEnumerable<SymbolItem> _allSymbols;
    private readonly IModuleTypeService _moduleTypeService;
    private bool _disposed;

    [ObservableProperty]
    private DataGridCollectionView _circuitList;

    public CircuitListViewModel(IEnumerable<SymbolItem> allSymbols, IModuleTypeService moduleTypeService)
    {
        _circuitList = new DataGridCollectionView(new List<SymbolItem>());
        _allSymbols = allSymbols ?? throw new ArgumentNullException(nameof(allSymbols));
        _moduleTypeService = moduleTypeService ?? throw new ArgumentNullException(nameof(moduleTypeService));
        
        if (_allSymbols is INotifyCollectionChanged collectionChanged)
        {
            collectionChanged.CollectionChanged += OnSymbolsCollectionChanged;
        }

        // Subskrybuj istniejące elementy
        foreach (var symbol in _allSymbols)
        {
            symbol.PropertyChanged += OnSymbolPropertyChanged;
        }

        RefreshList();
    }

    private void OnSymbolsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (SymbolItem item in e.NewItems)
            {
                item.PropertyChanged += OnSymbolPropertyChanged;
            }
        }

        if (e.OldItems != null)
        {
            foreach (SymbolItem item in e.OldItems)
            {
                item.PropertyChanged -= OnSymbolPropertyChanged;
            }
        }

        RefreshList();
    }

    private void OnSymbolPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Odświeżamy listę przy zmianie istotnych właściwości dla tabeli
        var relevantProperties = new[] 
        { 
            nameof(SymbolItem.Label), 
            nameof(SymbolItem.Phase), 
            nameof(SymbolItem.DisplayProtection), 
            nameof(SymbolItem.CircuitName), 
            nameof(SymbolItem.PowerW),
            nameof(SymbolItem.CableLength),
            nameof(SymbolItem.CableCrossSection),
            nameof(SymbolItem.Location),
            nameof(SymbolItem.DisplayLocation),
            nameof(SymbolItem.ReferenceDesignation),
            nameof(SymbolItem.X),
            nameof(SymbolItem.Type)
        };

        if (string.IsNullOrEmpty(e.PropertyName) || relevantProperties.Contains(e.PropertyName))
        {
            RefreshList();
        }
    }

    /// <summary>
    /// Odświeża listę filtrując obwody końcowe (MCB, SPD, bez szyn i złączek)
    /// </summary>
    public void RefreshList()
    {
        var rawList = _allSymbols.Where(IsCircuitElement).ToList();

        // Podstawowe posortowanie: po X żeby były tak jak na szynie
        var sorted = rawList.OrderBy(s => s.X).ToList();

        var collectionView = new DataGridCollectionView(sorted);
        collectionView.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(SymbolItem.DisplayLocation)));
        
        CircuitList = collectionView;
    }

    /// <summary>
    /// Sprawdza czy element powinien znaleźć się w tabeli obwodów
    /// </summary>
    public bool IsCircuitElement(SymbolItem item)
    {
        if (item == null) return false;
        
        // Elementy ignorowane: kontrolki faz, bloki rozdzielcze, styki pomocnicze, listwy zaciskowe, zasilacze
        if (_moduleTypeService.IsPhaseIndicator(item)) return false;
        if (IsTerminalBlockOrAux(item)) return false;
        
        // Chcemy pokazać w tabeli:
        // - Obwody końcowe (MCB, RCBO, itp)
        // - Główne zasilanie / główne rozłączniki
        // - Ochronę przeciwprzepięciową (SPD)
        // - Pomijamy czyste RCD, bo to tylko zabezpieczenia grupowe (choć można je pokazać w zrębkach)
        if (_moduleTypeService.IsRcd(item)) return false;

        return true;
    }

    private bool IsTerminalBlockOrAux(SymbolItem item)
    {
        if (item.IsTerminalBlock) return true;

        var searchable = $"{item.Type} {item.Label} {item.VisualPath}".ToLowerInvariant();
        return searchable.Contains("złączk") ||
               searchable.Contains("zlaczk") ||
               searchable.Contains("zacisk") ||
               searchable.Contains("terminal") ||
               searchable.Contains("listwa") ||
               searchable.Contains("listwy") ||
               searchable.Contains("szyna") ||
               searchable.Contains("busbar") ||
               searchable.Contains("rozdzielcz") ||
               searchable.Contains("styk") ||
               searchable.Contains("zasilacz") ||
               searchable.Contains("blok");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_allSymbols is INotifyCollectionChanged collectionChanged)
        {
            collectionChanged.CollectionChanged -= OnSymbolsCollectionChanged;
        }

        foreach (var symbol in _allSymbols)
        {
            symbol.PropertyChanged -= OnSymbolPropertyChanged;
        }
    }
}
