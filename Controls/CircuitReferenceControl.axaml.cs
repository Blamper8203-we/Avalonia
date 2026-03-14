using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DINBoard.Models;

namespace DINBoard.Controls;

/// <summary>
/// Kontrolka odsyłacza obwodu - kółko z numerem, klikalne.
/// </summary>
public partial class CircuitReferenceControl : UserControl
{
    /// <summary>
    /// Event wywoływany przy kliknięciu na odsyłacz
    /// </summary>
    public event EventHandler<CircuitReference>? ReferenceClicked;

    public CircuitReferenceControl()
    {
        InitializeComponent();

        // Obsługa kliknięcia
        var circle = this.FindControl<Border>("ReferenceCircle");
        if (circle != null)
        {
            circle.PointerPressed += OnReferenceClicked;
        }

        // Aktualizuj kolor obramowania przy zmianie DataContext
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        UpdateBorderColor();
    }

    private void UpdateBorderColor()
    {
        var circle = this.FindControl<Border>("ReferenceCircle");
        if (circle == null) return;

        if (DataContext is CircuitReference reference)
        {
            circle.BorderBrush = reference.Phase switch
            {
                "L1" => ResolveBrush("AccentBlue", "#3B82F6"),
                "L2" => ResolveBrush("AccentOrange", "#D97706"),
                "L3" => ResolveBrush("AccentRed", "#EF4444"),
                _ => ResolveBrush("TextSecondary", "#6B7280")
            };
        }
    }

    private static IBrush ResolveBrush(string key, string fallbackHex)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var resource) == true && resource is IBrush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Color.Parse(fallbackHex));
    }

    private void OnReferenceClicked(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is CircuitReference reference)
        {
            e.Handled = true;
            ReferenceClicked?.Invoke(this, reference);
        }
    }
}
