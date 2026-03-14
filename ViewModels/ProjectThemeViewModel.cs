using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DINBoard.ViewModels;

/// <summary>
/// ViewModel obsługujący ustawienia tematu i wyglądu aplikacji.
/// </summary>
public partial class ProjectThemeViewModel : ObservableObject
{
    /// <summary>Wybrany motyw</summary>
    [ObservableProperty]
    private string _selectedTheme = "Ciemny (Antracyt)";

    /// <summary>Lista dostępnych motywów</summary>
    public ObservableCollection<string> AvailableThemes { get; } = new()
    {
        "Jasny",
        "Ciemny (Antracyt)",
        "Ciemny (Granat)",
        "Ciemny (Czerń)"
    };

    /// <summary>Czy pokazywać przewody dolne (prądowe)</summary>
    [ObservableProperty]
    private bool _showBottomWires = true;

    /// <summary>Callback wywoływany przy zmianie motywu</summary>
    public Action<string>? OnThemeChanged { get; set; }

    partial void OnSelectedThemeChanged(string value)
    {
        OnThemeChanged?.Invoke(value);
    }
}
