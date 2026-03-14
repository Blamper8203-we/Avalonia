using Avalonia.Controls;
using Avalonia.Interactivity;
using DINBoard.Services;
using DINBoard.Services.Pdf;

namespace DINBoard.Dialogs;

public partial class PdfExportDialog : Window
{
    public PdfExportOptions? Result { get; private set; }

    public PdfExportDialog()
    {
        InitializeComponent();
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close(false);
    }

    private void BtnExport_Click(object? sender, RoutedEventArgs e)
    {
        Result = new PdfExportOptions();
        Close(true);
    }
}
