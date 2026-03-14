using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DINBoard.Dialogs;

public partial class DinRailDialog : Window
{
    private NumericUpDown _numRows = null!;
    private NumericUpDown _numModules = null!;
    private TextBlock _txtPreview = null!;

    public int Rows { get; private set; } = 1;
    public int Modules { get; private set; } = 24;
    public bool Confirmed { get; private set; } = false;

    public DinRailDialog()
    {
        InitializeComponent();

        _numRows = this.FindControl<NumericUpDown>("NumRows")!;
        _numModules = this.FindControl<NumericUpDown>("NumModules")!;
        _txtPreview = this.FindControl<TextBlock>("TxtPreview")!;

        UpdatePreview();

        // Subscribe to value changes
        _numRows.ValueChanged += (s, e) => UpdatePreview();
        _numModules.ValueChanged += (s, e) => UpdatePreview();
    }

    private void UpdatePreview()
    {
        var rows = (int)(_numRows.Value ?? 1);
        var modules = (int)(_numModules.Value ?? 24);
        _txtPreview.Text = $"Podgląd: {rows} x {modules} modułów";
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }

    private void BtnGenerate_Click(object? sender, RoutedEventArgs e)
    {
        Rows = (int)(_numRows.Value ?? 1);
        Modules = (int)(_numModules.Value ?? 24);
        Confirmed = true;
        Close();
    }
}
