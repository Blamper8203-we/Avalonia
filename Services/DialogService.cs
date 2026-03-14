using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DINBoard.Dialogs;
using DINBoard.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DINBoard.Services;

/// <summary>
/// Interfejs serwisu dialogów.
/// </summary>
public interface IDialogService
{
    void Initialize(Window window);
    Task<Circuit?> ShowCircuitConfigDialogAsync(Circuit circuit);
    Task<ProjectMetadata?> ShowProjectMetadataDialogAsync(ProjectMetadata data);
    Task<PdfExportOptions?> ShowPdfExportOptionsDialogAsync();
    Task<List<ExcelImportRow>?> ShowExcelImportDialogAsync();
    Task<BusbarGeneratorDialogResult?> ShowBusbarGeneratorDialogAsync();

    Task ShowMessageAsync(string title, string message);
    Task<string?> PickSaveFileAsync(string title, string defaultExtension, string filterName);
    Task<string?> PickOpenFileAsync(string title, string filterExtension, string filterName);
    Task<bool> ShowConfirmAsync(string title, string message);
    Task<bool> ShowConfirmationDialogAsync(string title, string message);
    Task<string?> ShowPromptAsync(string title, string message, string defaultValue = "");
}

/// <summary>
/// Wynik z dialogu generatora szyny prądowej.
/// </summary>
public class BusbarGeneratorDialogResult
{
    public int PinCount { get; init; }
    public BusbarType BusbarType { get; init; }
}

/// <summary>
/// Serwis zarządzający dialogami w Avalonia.
/// </summary>
public class DialogService : IDialogService
{
    private Window? _mainWindow;

    private static IBrush ResolveBrush(string key, string fallbackHex)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var resource) == true && resource is IBrush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Color.Parse(fallbackHex));
    }

    public void Initialize(Window window)
    {
        _mainWindow = window;
    }

    public async Task<Circuit?> ShowCircuitConfigDialogAsync(Circuit circuit)
    {
        if (_mainWindow == null) return null;

        var dialog = new CircuitConfigDialog(circuit);
        await dialog.ShowDialog(_mainWindow);
        return dialog.Result;
    }

    public async Task<ProjectMetadata?> ShowProjectMetadataDialogAsync(ProjectMetadata data)
    {
        if (_mainWindow == null) return null;

        var dialog = new ProjectMetadataDialog(data);
        var result = await dialog.ShowDialog<ProjectMetadata?>(_mainWindow);
        return result;
    }

    public async Task<PdfExportOptions?> ShowPdfExportOptionsDialogAsync()
    {
        if (_mainWindow == null) return null;

        var dialog = new PdfExportDialog();
        var accepted = await dialog.ShowDialog<bool?>(_mainWindow);
        if (accepted != true)
        {
            return null;
        }

        return dialog.Result;
    }

    public async Task<List<ExcelImportRow>?> ShowExcelImportDialogAsync()
    {
        if (_mainWindow == null) return null;

        var dialog = new ExcelImportDialog();
        var result = await dialog.ShowDialog<List<ExcelImportRow>>(_mainWindow);

        // Zwracamy listę poprawnych wierszy do importu (lub null jeśli anulowano)
        return result != null && result.Count > 0 ? result : null;
    }

    public async Task<BusbarGeneratorDialogResult?> ShowBusbarGeneratorDialogAsync()
    {
        if (_mainWindow == null) return null;

        var dialog = new BusbarGeneratorDialog();
        await dialog.ShowDialog(_mainWindow);

        if (!dialog.Confirmed)
        {
            return null;
        }

        return new BusbarGeneratorDialogResult
        {
            PinCount = dialog.PinCount,
            BusbarType = dialog.SelectedType
        };
    }



    public async Task ShowMessageAsync(string title, string message)
    {
        if (_mainWindow == null) return;

        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var content = new Border
        {
            Padding = new Thickness(25),
            Background = ResolveBrush("PanelBackground", "#1E1E1E"),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("*,Auto")
            }
        };

        var msgBlock = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ResolveBrush("TextMain", "#FFFFFF"),
            FontSize = 14
        };
        Grid.SetRow(msgBlock, 0);

        var okButton = new Button
        {
            Content = "OK",
            HorizontalAlignment = HorizontalAlignment.Right,
            Width = 80,
            Margin = new Thickness(0, 20, 0, 0)
        };
        okButton.Classes.Add("accent");
        okButton.Click += (s, e) => dialog.Close();
        Grid.SetRow(okButton, 1);

        ((Grid)content.Child).Children.Add(msgBlock);
        ((Grid)content.Child).Children.Add(okButton);

        dialog.Content = content;
        await dialog.ShowDialog(_mainWindow);
    }

    public async Task<bool> ShowConfirmAsync(string title, string message)
    {
        if (_mainWindow == null) return false;

        bool result = false;

        var dialog = new Window
        {
            Title = title,
            Width = 450,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var content = new Border
        {
            Padding = new Thickness(25),
            Background = ResolveBrush("PanelBackground", "#1E1E1E"),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("*,Auto")
            }
        };

        var msgBlock = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ResolveBrush("TextMain", "#FFFFFF"),
            FontSize = 14
        };
        Grid.SetRow(msgBlock, 0);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Margin = new Thickness(0, 20, 0, 0)
        };

        var cancelButton = new Button { Content = "Anuluj", Width = 80 };
        cancelButton.Click += (s, e) => { result = false; dialog.Close(); };

        var confirmButton = new Button { Content = "Tak", Width = 80 };
        confirmButton.Classes.Add("accent");
        confirmButton.Click += (s, e) => { result = true; dialog.Close(); };

        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(confirmButton);
        Grid.SetRow(buttonPanel, 1);

        ((Grid)content.Child).Children.Add(msgBlock);
        ((Grid)content.Child).Children.Add(buttonPanel);

        dialog.Content = content;
        await dialog.ShowDialog(_mainWindow);
        return result;
    }

    public Task<bool> ShowConfirmationDialogAsync(string title, string message)
    {
        return ShowConfirmAsync(title, message);
    }

    public async Task<string?> PickSaveFileAsync(string title, string defaultExtension, string filterName)
    {
        if (_mainWindow == null) return null;

        var storageProvider = _mainWindow.StorageProvider;

        var options = new FilePickerSaveOptions
        {
            Title = title,
            DefaultExtension = defaultExtension,
            FileTypeChoices = new List<FilePickerFileType>
            {
                new FilePickerFileType(filterName)
                {
                    Patterns = new[] { $"*{defaultExtension}" }
                }
            }
        };

        var file = await storageProvider.SaveFilePickerAsync(options);
        return file?.Path?.LocalPath;
    }

    public async Task<string?> PickOpenFileAsync(string title, string filterExtension, string filterName)
    {
        if (_mainWindow == null) return null;

        var storageProvider = _mainWindow.StorageProvider;

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new FilePickerFileType(filterName)
                {
                    Patterns = new[] { $"*{filterExtension}" }
                }
            }
        };

        var files = await storageProvider.OpenFilePickerAsync(options);
        return files.Count > 0 ? files[0].Path?.LocalPath : null;
    }

    public async Task<string?> ShowPromptAsync(string title, string message, string defaultValue = "")
    {
        if (_mainWindow == null) return null;

        string? result = null;

        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var content = new Border
        {
            Padding = new Thickness(25),
            Background = ResolveBrush("PanelBackground", "#1E1E1E"),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,Auto,Auto")
            }
        };

        var msgBlock = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ResolveBrush("TextMain", "#FFFFFF"),
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 10)
        };
        Grid.SetRow(msgBlock, 0);

        var inputBox = new TextBox
        {
            Text = defaultValue,
            Margin = new Thickness(0, 0, 0, 20)
        };
        Grid.SetRow(inputBox, 1);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10
        };

        var cancelButton = new Button { Content = "Anuluj", Width = 80 };
        cancelButton.Click += (s, e) => { result = null; dialog.Close(); };

        var confirmButton = new Button { Content = "OK", Width = 80 };
        confirmButton.Classes.Add("accent");
        confirmButton.Click += (s, e) => { result = inputBox.Text; dialog.Close(); };

        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(confirmButton);
        Grid.SetRow(buttonPanel, 2);

        ((Grid)content.Child).Children.Add(msgBlock);
        ((Grid)content.Child).Children.Add(inputBox);
        ((Grid)content.Child).Children.Add(buttonPanel);

        dialog.Content = content;

        // Focus textbox on show
        dialog.Opened += (s, e) => inputBox.Focus();

        await dialog.ShowDialog(_mainWindow);
        return result;
    }
}
