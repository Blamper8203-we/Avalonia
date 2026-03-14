using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DINBoard.ViewModels;

namespace DINBoard.Models;

/// <summary>
/// Kierunek linii poziomej odsyłacza
/// </summary>
public enum ReferenceDirection
{
    Left,
    Right
}

/// <summary>
/// Odsyłacz obwodu - element wizualny wskazujący kontynuację obwodu na innym arkuszu.
/// Wyświetlany jako kółko z numerem na końcu linii poziomej.
/// </summary>
public partial class CircuitReference : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    /// <summary>
    /// Numer arkusza docelowego (np. 2 = Sheet 2)
    /// </summary>
    [ObservableProperty]
    private int _sheetNumber = 2;

    /// <summary>
    /// Numer obwodu na arkuszu (np. 1, 2, 3...)
    /// </summary>
    [ObservableProperty]
    private int _circuitNumber;

    /// <summary>
    /// Nazwa obwodu (np. "Kuchnia", "Pralka")
    /// </summary>
    [ObservableProperty]
    private string _circuitName = "";

    /// <summary>
    /// Faza zasilania (L1, L2, L3)
    /// </summary>
    [ObservableProperty]
    private string _phase = "L1";

    /// <summary>
    /// Globalny indeks RCD 2P (0=L1, 1=L2, 2=L3) do kolorowania linii w diagramie
    /// </summary>
    [ObservableProperty]
    private int _rcdPhaseIndex;

    /// <summary>
    /// Pozycja X odsyłacza na canvasie
    /// </summary>
    [ObservableProperty]
    private double _x;

    /// <summary>
    /// Pozycja Y odsyłacza na canvasie
    /// </summary>
    [ObservableProperty]
    private double _y;

    /// <summary>
    /// Kierunek linii poziomej (Left/Right)
    /// </summary>
    [ObservableProperty]
    private ReferenceDirection _direction = ReferenceDirection.Right;

    /// <summary>
    /// ID grupy, do której należy odsyłacz
    /// </summary>
    [ObservableProperty]
    private string? _groupId;

    /// <summary>
    /// Pozycja Y linii poziomej (wspólna dla wszystkich MCB w grupie)
    /// </summary>
    [ObservableProperty]
    private double _horizontalLineY;

    /// <summary>
    /// Pozycja X początku linii poziomej
    /// </summary>
    [ObservableProperty]
    private double _horizontalLineStartX;

    /// <summary>
    /// Pozycja X końca linii poziomej (przy odsyłaczu)
    /// </summary>
    [ObservableProperty]
    private double _horizontalLineEndX;

    /// <summary>
    /// Sformatowana etykieta odsyłacza (np. "2.1")
    /// </summary>
    public string Label => $"{SheetNumber}.{CircuitNumber}";

    /// <summary>
    /// Pełny opis do tooltipa
    /// </summary>
    public string FullDescription => $"Sheet {SheetNumber}, Obwód {CircuitNumber}\n{CircuitName}\nFaza: {Phase}";

    /// <summary>
    /// Lista identyfikatorów MCB powiązanych z tym odsyłaczem.
    /// </summary>
    public List<string> LinkedMcbIds { get; set; } = new();

    /// <summary>
    /// Lista szczegółowych pozycji obwodów w tym odsyłaczu (do edycji w Sheet 2).
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<CircuitItemViewModel> _circuits = new();

    /// <summary>
    /// Brush helper for XAML binding
    /// </summary>
    public Avalonia.Media.IBrush PhaseBrush => Phase switch
    {
        "L1" => ResolveBrush("AccentBlue", "#3B82F6"),
        "L2" => ResolveBrush("AccentOrange", "#D97706"),
        "L3" => ResolveBrush("AccentRed", "#EF4444"),
        _ => ResolveBrush("TextSecondary", "#6B7280")
    };

    private static Avalonia.Media.IBrush ResolveBrush(string key, string fallbackHex)
    {
        if (Avalonia.Application.Current?.Resources.TryGetValue(key, out var resource) == true
            && resource is Avalonia.Media.IBrush brush)
        {
            return brush;
        }

        return new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(fallbackHex));
    }

    /// <summary>
    /// Czy odsyłacz jest podświetlony (po nawigacji)
    /// </summary>
    [ObservableProperty]
    private bool _isHighlighted;

    /// <summary>
    /// Podświetl odsyłacz na 5 sekund (po nawigacji)
    /// </summary>
    public async void HighlightTemporarily(int durationMs = 5000)
    {
        IsHighlighted = true;
        await System.Threading.Tasks.Task.Delay(durationMs);
        IsHighlighted = false;
    }
}

/// <summary>
/// Pojedynczy obwód na liście w Sheet 2.
/// </summary>
public partial class CircuitItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty; // Nazwa obwodu (edytowalna)

    [ObservableProperty]
    private string _phase = string.Empty; // Faza (L1/L2/L3)

    [ObservableProperty]
    private string _protection = string.Empty; // Zabezpieczenie (np. B16)

    /// <summary>
    /// Numer porządkowy w grupie (1, 2, 3...)
    /// </summary>
    [ObservableProperty]
    private int _position;

    /// <summary>
    /// Referencja do oryginalnego symbolu (do synchronizacji)
    /// </summary>
    public SymbolItem? LinkedSymbol { get; set; }

    /// <summary>
    /// Callback do synchronizacji zmian nazwy z symbolem
    /// </summary>
    partial void OnNameChanged(string value)
    {
        if (LinkedSymbol != null && !string.IsNullOrEmpty(value))
        {
            LinkedSymbol.Label = value;
            LinkedSymbol.CircuitName = value;
        }
    }
}
