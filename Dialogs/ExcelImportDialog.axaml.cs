using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DINBoard.Models;
using DINBoard.Services;

namespace DINBoard.Dialogs;

public partial class ExcelImportDialog : Window
{
    private readonly ExcelImportService _importService;
    private List<ExcelImportRow> _loadedRows = new();
    private string _currentFilePath = "";

    public ExcelImportDialog()
    {
        InitializeComponent();
        _importService = new ExcelImportService();
        UpdateStatus();
    }

    private async void BtnBrowse_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Wybierz arkusz z wykazem obwodów",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Excel Workbook") { Patterns = new[] { "*.xlsx", "*.xls" } },
                    new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } },
                    new FilePickerFileType("Wszystkie") { Patterns = new[] { "*.*" } }
                }
            });

            if (files.Count > 0)
            {
                _currentFilePath = files[0].Path.LocalPath;
                FilePathTextBox.Text = _currentFilePath;
                await RefreshDataAsync();
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Błąd podczas wyboru pliku Excel", ex);
            StatusTextBlock.Text = "Błąd: " + ex.Message;
            StatusTextBlock.Foreground = Avalonia.Media.Brushes.Red;
        }
    }

    private async void ChbHasHeaders_Changed(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath)) return;
        await RefreshDataAsync();
    }

    private async Task RefreshDataAsync()
    {
        try
        {
            StatusTextBlock.Text = "Wczytywanie...";
            StatusTextBlock.Foreground = Avalonia.Media.Brushes.Gray;
            BtnImport.IsEnabled = false;

            var hasHeaders = ChbHasHeaders.IsChecked ?? true;
            
            // Wczytywanie asynchroniczne, aby nie blokować UI
            _loadedRows = await Task.Run(() => _importService.ReadCircuitsFromFile(_currentFilePath, hasHeaders));

            PreviewGrid.ItemsSource = _loadedRows;

            UpdateStatus();
        }
        catch (Exception ex)
        {
            AppLog.Error("Błąd odczytu pliku Excel", ex);
            StatusTextBlock.Text = $"Błąd: {ex.Message}";
            StatusTextBlock.Foreground = Avalonia.Media.Brushes.Red;
            PreviewGrid.ItemsSource = null;
            _loadedRows.Clear();
            BtnImport.IsEnabled = false;
        }
    }

    private void UpdateStatus()
    {
        if (_loadedRows.Count == 0)
        {
            StatusTextBlock.Text = "Wybierz plik, aby załadować podgląd.";
            StatusTextBlock.Foreground = Avalonia.Media.Brushes.Gray;
            BtnImport.IsEnabled = false;
            return;
        }

        var validCount = _loadedRows.Count(x => string.IsNullOrWhiteSpace(x.ValidationError));
        var errorCount = _loadedRows.Count - validCount;

        StatusTextBlock.Text = $"Wczytano: {_loadedRows.Count} wierszy (Poprawne: {validCount}, Błędy: {errorCount})";
        StatusTextBlock.Foreground = errorCount > 0 ? Avalonia.Media.Brushes.DarkOrange : Avalonia.Media.Brushes.Gray;
        
        BtnImport.IsEnabled = validCount > 0;
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void BtnImport_Click(object? sender, RoutedEventArgs e)
    {
        // Return only valid rows to the caller
        var validRows = _loadedRows.Where(x => string.IsNullOrWhiteSpace(x.ValidationError)).ToList();
        Close(validRows);
    }
}
