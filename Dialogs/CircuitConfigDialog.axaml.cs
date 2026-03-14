using Avalonia.Controls;
using Avalonia.Interactivity;
using DINBoard.Models;

namespace DINBoard.Dialogs;

public partial class CircuitConfigDialog : Window
{
    public Circuit? Result { get; private set; }
    private Circuit _circuit;

    public CircuitConfigDialog() : this(new Circuit())
    {
    }

    public CircuitConfigDialog(Circuit circuit)
    {
        InitializeComponent();
        _circuit = circuit;
        LoadCircuitData();
    }

    private void LoadCircuitData()
    {
        TxtName.Text = _circuit.Name ?? "";
        NumPower.Value = (decimal)_circuit.PowerW;

        // Set phase
        CmbPhase.SelectedIndex = _circuit.Phase switch
        {
            "L1" => 0,
            "L2" => 1,
            "L3" => 2,
            "L1+L2" => 3,
            "L2+L3" => 4,
            "L3+L1" => 5,
            "3P" or "L1+L2+L3" => 6,
            _ => 0
        };

        ChkPhaseLocked.IsChecked = _circuit.IsPhaseLocked;

        // Parse protection (e.g., "B16" -> type="B", rating="16")
        var protection = _circuit.Protection ?? "B16";
        var protectionType = protection.Length > 0 ? protection[0].ToString() : "B";
        var protectionRating = protection.Length > 1 ? protection.Substring(1) : "16";

        // Set protection type
        CmbProtectionType.SelectedIndex = protectionType switch
        {
            "B" => 0,
            "C" => 1,
            "D" => 2,
            _ => 0
        };

        // Set protection current
        for (int i = 0; i < CmbProtectionCurrent.Items.Count; i++)
        {
            if (CmbProtectionCurrent.Items[i] is ComboBoxItem item && item.Content?.ToString() == protectionRating)
            {
                CmbProtectionCurrent.SelectedIndex = i;
                break;
            }
        }
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }

    private void BtnSave_Click(object? sender, RoutedEventArgs e)
    {
        _circuit.Name = TxtName.Text ?? "";
        _circuit.PowerW = (double)(NumPower.Value ?? 1000);

        _circuit.Phase = CmbPhase.SelectedIndex switch
        {
            0 => "L1",
            1 => "L2",
            2 => "L3",
            3 => "L1+L2",
            4 => "L2+L3",
            5 => "L3+L1",
            6 => "L1+L2+L3",
            _ => "L1"
        };

        _circuit.IsPhaseLocked = ChkPhaseLocked.IsChecked == true;

        var protectionType = CmbProtectionType.SelectedIndex switch
        {
            0 => "B",
            1 => "C",
            2 => "D",
            _ => "B"
        };

        var protectionRating = "16";
        if (CmbProtectionCurrent.SelectedItem is ComboBoxItem item)
        {
            protectionRating = item.Content?.ToString() ?? "16";
        }

        _circuit.Protection = $"{protectionType}{protectionRating}";

        Result = _circuit;
        Close();
    }
}

