using Avalonia.Controls;
using Avalonia.Interactivity;
using DINBoard.Services;

namespace DINBoard.Dialogs;

public partial class BusbarGeneratorDialog : Window
{
    private NumericUpDown _numPins = null!;
    private ComboBox _comboType = null!;
    private TextBlock _txtPreview = null!;

    public int PinCount { get; private set; } = 3;
    public BusbarType SelectedType { get; private set; } = BusbarType.ThreePhase;
    public bool Confirmed { get; private set; }

    public BusbarGeneratorDialog()
    {
        InitializeComponent();

        _numPins = this.FindControl<NumericUpDown>("NumPins")!;
        _comboType = this.FindControl<ComboBox>("ComboBusbarType")!;
        _txtPreview = this.FindControl<TextBlock>("TxtPreview")!;

        _comboType.SelectedIndex = 0;

        UpdatePreview();
        _numPins.ValueChanged += (s, e) => UpdatePreview();
        _comboType.SelectionChanged += (s, e) => UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (_numPins == null || _comboType == null || _txtPreview == null) return;
        var pins = (int)(_numPins.Value ?? 3);
        var typeStr = (_comboType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "3-Fazowa";
        _txtPreview.Text = $"Podgląd: {pins} pinów, {typeStr}";
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }

    private void BtnGenerate_Click(object? sender, RoutedEventArgs e)
    {
        PinCount = (int)(_numPins.Value ?? 3);
        SelectedType = _comboType.SelectedIndex switch
        {
            0 => BusbarType.ThreePhase,
            1 => BusbarType.SinglePhaseL1,
            2 => BusbarType.SinglePhaseL2,
            3 => BusbarType.SinglePhaseL3,
            _ => BusbarType.ThreePhase
        };
        Confirmed = true;
        Close();
    }
}
