using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Svg.Skia;
using DINBoard.Helpers;
using DINBoard.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DINBoard.Dialogs;

public partial class ImportModulesDialog : Window
{
    private readonly SvgModuleImporter _importer;
    private ObservableCollection<ImportableModule> _modules = new();
    private string _targetPath = "";
    private readonly ObservableCollection<string> _categories = new();

    public ImportModulesDialog()
    {
        var app = (App)Avalonia.Application.Current!;
        var svgProcessor = app.Services.GetRequiredService<SvgProcessor>();
        _importer = new SvgModuleImporter(svgProcessor);

        InitializeComponent();

        _targetPath = ResolveModulesRoot();
        LoadCategories();

        // Drag & drop
        AddHandler(DragDrop.DropEvent, OnFileDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    private void LoadCategories()
    {
        _categories.Clear();
        var categories = _importer.GetExistingCategories(_targetPath);
        foreach (var category in categories)
        {
            _categories.Add(category);
        }

        if (_categories.Count == 0)
        {
            _categories.Add("FR");
            _categories.Add("SPD");
            _categories.Add("RCD");
            _categories.Add("MCB");
            _categories.Add("Blok rozdzielczy");
            _categories.Add("Listwy");
            _categories.Add("Controls");
        }

        (CategoryComboBox as ComboBox)!.ItemsSource = _categories;
        (CategoryComboBox as ComboBox)!.SelectedIndex = 0;
    }

    private string ResolveModulesRoot()
    {
        // Szukaj katalogu projektu (z plikiem .csproj), pomijaj bin/obj
        // — ta sama logika co ModulesPaletteView.ResolveModulesRoot()
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var dirName = dir.Name;
            if (!dirName.Equals("bin", StringComparison.OrdinalIgnoreCase) &&
                !dirName.Equals("obj", StringComparison.OrdinalIgnoreCase))
            {
                var hasProject = dir.EnumerateFiles("*.csproj").Any();
                if (hasProject)
                {
                    var candidate = Path.Combine(dir.FullName, "Assets", "Modules");
                    if (!Directory.Exists(candidate))
                    {
                        Directory.CreateDirectory(candidate);
                    }
                    return candidate;
                }
            }

            dir = dir.Parent;
        }

        // Fallback - katalog uruchomieniowy
        return Path.Combine(AppContext.BaseDirectory, "Assets", "Modules");
    }

    /// <summary>
    /// Przycisk przeglądania plików źródłowych
    /// </summary>
    private async void BrowseSourceButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Wybierz pliki modułów",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Moduły (SVG, PNG, JPG)") { Patterns = new[] { "*.svg", "*.SVG", "*.png", "*.PNG", "*.jpg", "*.JPG", "*.jpeg", "*.JPEG" } },
                    new FilePickerFileType("SVG") { Patterns = new[] { "*.svg", "*.SVG" } },
                    new FilePickerFileType("Obrazy") { Patterns = new[] { "*.png", "*.PNG", "*.jpg", "*.JPG", "*.jpeg", "*.JPEG" } }
                }
            });

            if (files.Count > 0)
            {
                var paths = files.Select(f => f.Path.LocalPath).ToArray();
                LoadModuleFiles(paths);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Błąd podczas wyboru plików", ex);
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.Copy;
    }

    private void OnFileDrop(object? sender, DragEventArgs e)
    {
        try
        {
#pragma warning disable CS0618 // GetFiles is only available on IDataObject (OS drag)
            var files = e.Data.GetFiles();
#pragma warning restore CS0618
            if (files == null) return;

            var paths = files
                .Select(f => f.Path.LocalPath)
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

            if (paths.Length > 0)
                LoadModuleFiles(paths);
        }
        catch (Exception ex)
        {
            AppLog.Error("Błąd podczas drop plików", ex);
        }
    }

    private void LoadModuleFiles(string[] paths)
    {
        (SourcePathTextBox as TextBox)!.Text = paths.Length == 1
            ? Path.GetFileName(paths[0])
            : $"Wybrano: {paths.Length} plików";

        _modules = _importer.LoadFiles(paths);

        // Sprawdź duplikaty
        var targetCategory = GetTargetCategory();
        _importer.CheckDuplicates(_modules, _targetPath, targetCategory);

        // Auto-kategoria: jeśli włączone i wszystkie pliki mają ten sam typ, ustaw go
        if ((AutoCategoryCheckBox as CheckBox)?.IsChecked == true && _modules.Count > 0)
        {
            var detectedTypes = _modules.Select(m => m.ModuleType).Distinct().ToList();
            if (detectedTypes.Count == 1 && detectedTypes[0] != "Other")
            {
                var idx = _categories.IndexOf(detectedTypes[0]);
                if (idx >= 0)
                    (CategoryComboBox as ComboBox)!.SelectedIndex = idx;
            }
        }

        (ModulesList as ListBox)!.ItemsSource = _modules;
        (ModulesList as ListBox)!.SelectedIndex = _modules.Count > 0 ? 0 : -1;
        (DropZone as Border)!.IsVisible = _modules.Count == 0;

        UpdateStatus();
    }

    /// <summary>
    /// Obsługuje zmianę wyboru w liście modułów
    /// </summary>
    private void ModulesList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if ((ModulesList as ListBox)?.SelectedItem is ImportableModule module)
        {
            ShowPreview(module);
        }
    }

    /// <summary>
    /// Wyświetla podgląd wybranego modułu
    /// </summary>
    private void ShowPreview(ImportableModule module)
    {
        try
        {
            (ModuleTypeLabel as TextBlock)!.Text = module.ModuleType;
            (PolesLabel as TextBlock)!.Text = module.PoleCount > 0 ? $"{module.PoleCount}P" : "Nieznana";
            (FileNameLabel as TextBlock)!.Text = module.FileName;
            (FileDateLabel as TextBlock)!.Text = module.FileDate.ToString("yyyy-MM-dd HH:mm");
            (FileSizeLabel as TextBlock)!.Text = module.FileSizeFormatted;

            // Podgląd — użyj już załadowanej miniaturki lub załaduj na nowo
            try
            {
                if (module.Thumbnail != null)
                {
                    (PreviewImage as Image)!.Source = module.Thumbnail;
                }
                else if (File.Exists(module.FilePath))
                {
                    var ext = Path.GetExtension(module.FilePath);
                    if (ext.Equals(".svg", StringComparison.OrdinalIgnoreCase))
                    {
                        var content = File.ReadAllText(module.FilePath);
                        var svgSource = SvgSource.LoadFromSvg(content);
                        (PreviewImage as Image)!.Source = svgSource != null 
                            ? new SvgImage { Source = svgSource } 
                            : null;
                    }
                    else
                    {
                        using var fs = File.OpenRead(module.FilePath);
                        (PreviewImage as Image)!.Source = new Avalonia.Media.Imaging.Bitmap(fs);
                    }
                }
                else
                {
                    (PreviewImage as Image)!.Source = null;
                }
            }
            catch (Exception ex)
            {
                AppLog.Debug($"Nie można załadować podglądu: {ex.Message}");
                (PreviewImage as Image)!.Source = null;
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Błąd podczas wyświetlania podglądu", ex);
        }
    }

    /// <summary>
    /// Zaznacz wszystkie moduły
    /// </summary>
    private void SelectAllButton_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var module in _modules)
        {
            module.IsSelected = true;
        }
        UpdateStatus();
    }

    /// <summary>
    /// Odznacz wszystkie moduły
    /// </summary>
    private void DeselectAllButton_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var module in _modules)
        {
            module.IsSelected = false;
        }
        UpdateStatus();
    }

    /// <summary>
    /// Importuj wybrane moduły
    /// </summary>
    private void ImportButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var selectedCount = _modules.Count(m => m.IsSelected);

            if (selectedCount == 0)
            {
                (StatusLabel as TextBlock)!.Text = "Błąd: nie wybrano żadnych modułów";
                return;
            }

            var autoCat = (AutoCategoryCheckBox as CheckBox)?.IsChecked == true;

            if (autoCat)
            {
                // Import per-category: grupuj moduły wg wykrytego typu
                int totalImported = 0;
                var groups = _modules.Where(m => m.IsSelected).GroupBy(m => m.ModuleType);
                foreach (var group in groups)
                {
                    var category = group.Key == "Other" ? GetTargetCategory() : group.Key;
                    var subset = new ObservableCollection<ImportableModule>(group);
                    foreach (var m in subset) m.IsSelected = true;
                    totalImported += _importer.ImportModules(subset, _targetPath, category, false, null, overwriteExisting: true);
                }
                AppLog.Info($"Auto-import: zaimportowano {totalImported} modułów do wielu kategorii");
            }
            else
            {
                var targetCategory = GetTargetCategory();
                var imported = _importer.ImportModules(_modules, _targetPath, targetCategory, false, null, overwriteExisting: true);
                AppLog.Info($"Zaimportowano {imported} modułów do: {_targetPath}/{targetCategory}");
            }

            Close();
        }
        catch (Exception ex)
        {
            AppLog.Error("Błąd podczas importu", ex);
            (StatusLabel as TextBlock)!.Text = $"Błąd: {ex.Message}";
        }
    }

    /// <summary>
    /// Anuluj import
    /// </summary>
    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Aktualizuje status wyboru
    /// </summary>
    private void UpdateStatus()
    {
        var count = _modules.Count(m => m.IsSelected);
        var duplicates = _modules.Count(m => m.IsSelected && m.IsDuplicate);

        (SelectionCountLabel as TextBlock)!.Text = duplicates > 0
            ? string.Format(LocalizationHelper.GetString("FormatSelectionCountDupe"), count, duplicates)
            : string.Format(LocalizationHelper.GetString("FormatSelectionCount"), count);

        var importBtn = this.FindControl<Button>("ImportButton");
        if (importBtn != null)
        {
            importBtn.Content = string.Format(LocalizationHelper.GetString("FormatImportBtn"), count);
            importBtn.IsEnabled = count > 0;
        }

        (StatusLabel as TextBlock)!.Text = _modules.Count > 0
            ? $"Znaleziono: {_modules.Count} plik(ów)"
            : "";
    }

    private string GetTargetCategory()
    {
        var newCategory = (NewCategoryTextBox as TextBox)?.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(newCategory))
        {
            return newCategory;
        }

        var selected = (CategoryComboBox as ComboBox)?.SelectedItem as string;
        return selected ?? "Other";
    }

}
