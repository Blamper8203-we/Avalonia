using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;

namespace DINBoard.Views;

public partial class ModulesPaletteView : UserControl
{
    public class PaletteItem
    {
        public required string Name { get; set; }
        public required string Description { get; set; }
        public required string Type { get; set; } // "FR", "SPD", "RCD", "MCB"
        public string IconKind { get; set; } = "HelpCircle"; // Material Design Icon fallback

        /// <summary>
        /// Common image source for both Bitmaps (PNG/JPG) and SVGs.
        /// Avalonia Image control can display any IImage.
        /// </summary>
        public IImage? Visual { get; set; }
        public string? FilePath { get; set; } // Added for Drag & Drop

        public bool HasVisual => Visual != null;
    }

    public class ModuleCategory
    {
        public ModuleCategory(string name, ObservableCollection<PaletteItem> items)
        {
            Name = name;
            Items = items;
        }

        public string Name { get; }
        public ObservableCollection<PaletteItem> Items { get; }
    }

    private static readonly string[] DefaultCategoryNames =
    {
        "FR",
        "SPD",
        "RCD",
        "MCB",
        "Blok rozdzielczy",
        "Listwy",
        "Controls"
    };

    public ObservableCollection<PaletteItem> FrModules { get; } = new();
    public ObservableCollection<PaletteItem> SpdModules { get; } = new();
    public ObservableCollection<PaletteItem> RcdModules { get; } = new();
    public ObservableCollection<PaletteItem> McbModules { get; } = new();
    public ObservableCollection<PaletteItem> DistModules { get; } = new();
    public ObservableCollection<PaletteItem> TerminalModules { get; } = new();
    public ObservableCollection<PaletteItem> ControlsModules { get; } = new();
    public ObservableCollection<ModuleCategory> Categories { get; } = new();

    public ModulesPaletteView()
    {
        InitializeComponent();

        // IMPORTANT: Do NOT set DataContext = this, as it breaks binding inheritance from MainWindow.
        // Internal bindings use #PaletteRoot reference instead.
        // DataContext = this;

        LoadAllModules();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void LoadAllModules()
    {
        AddDefaultModules();

        LoadModulesFromFolder("FR", FrModules);
        LoadModulesFromFolder("SPD", SpdModules);
        LoadModulesFromFolder("RCD", RcdModules);
        LoadModulesFromFolder("MCB", McbModules);
        LoadModulesFromFolder("Blok rozdzielczy", DistModules);
        LoadModulesFromFolder("Listwy", TerminalModules);
        LoadModulesFromFolder("Controls", ControlsModules);

        Categories.Clear();
        AddCategory("FR", FrModules);
        AddCategory("SPD", SpdModules);
        AddCategory("RCD", RcdModules);
        AddCategory("MCB", McbModules);
        AddCategory("Blok rozdzielczy", DistModules);
        AddCategory("Listwy", TerminalModules);
        AddCategory("Controls", ControlsModules);

        LoadAdditionalCategories();
    }

    /// <summary>
    /// Przeładowuje wszystkie moduły z dysku (np. po imporcie nowych modułów).
    /// </summary>
    public void ReloadModules()
    {
        FrModules.Clear();
        SpdModules.Clear();
        RcdModules.Clear();
        McbModules.Clear();
        DistModules.Clear();
        TerminalModules.Clear();
        ControlsModules.Clear();
        Categories.Clear();

        LoadAllModules();
    }

    private void AddCategory(string name, ObservableCollection<PaletteItem> items)
    {
        if (Categories.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return;

        Categories.Add(new ModuleCategory(name, items));
    }

    private void LoadAdditionalCategories()
    {
        var modulesRoot = ResolveModulesRoot();
        if (!Directory.Exists(modulesRoot)) return;

        var known = new HashSet<string>(DefaultCategoryNames, StringComparer.OrdinalIgnoreCase);

        foreach (var dir in Directory.EnumerateDirectories(modulesRoot))
        {
            var name = Path.GetFileName(dir);
            if (string.IsNullOrWhiteSpace(name) || known.Contains(name))
                continue;

            var items = new ObservableCollection<PaletteItem>();
            LoadModulesFromFolder(name, items);
            if (items.Count > 0)
            {
                Categories.Add(new ModuleCategory(name, items));
            }
        }
    }

    private static string GetModulesPath(string category)
    {
        return Path.Combine(ResolveModulesRoot(), category);
    }

    private static string ResolveModulesRoot()
    {
        // Prefer project root (folder with .csproj) to avoid bin\Debug output paths
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var dirName = dir.Name;
            if (!dirName.Equals("bin", StringComparison.OrdinalIgnoreCase) &&
                !dirName.Equals("obj", StringComparison.OrdinalIgnoreCase))
            {
                if (dir.EnumerateFiles("*.csproj").Any())
                {
                    return Path.Combine(dir.FullName, "Assets", "Modules");
                }
            }
            dir = dir.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "Assets", "Modules");
    }

    private static bool IsSupportedModuleFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext.Equals(".svg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasAnyModuleFiles(string category)
    {
        var path = GetModulesPath(category);
        return Directory.Exists(path) && Directory.EnumerateFiles(path).Any(IsSupportedModuleFile);
    }

    private void AddDefaultModules()
    {
        if (FrModules.Count == 0 && !HasAnyModuleFiles("FR"))
        {
            FrModules.Add(new() { Name = "FR 3P", Description = "Rozłącznik 3-faz", Type = "FR", IconKind = "PowerSettings" });
            FrModules.Add(new() { Name = "FR 1P", Description = "Rozłącznik 1-faz", Type = "FR", IconKind = "PowerSettings" });
            FrModules.Add(new() { Name = "FR 100A", Description = "Główny 100A", Type = "FR", IconKind = "LightningBoltOutline" });
        }

        if (SpdModules.Count == 0 && !HasAnyModuleFiles("SPD"))
        {
            SpdModules.Add(new() { Name = "SPD T1+T2", Description = "Ochronnik B+C", Type = "SPD", IconKind = "ShieldCheck" });
            SpdModules.Add(new() { Name = "SPD T2", Description = "Ochronnik C", Type = "SPD", IconKind = "ShieldOutline" });
        }

        if (RcdModules.Count == 0 && !HasAnyModuleFiles("RCD"))
        {
            RcdModules.Add(new() { Name = "RCD 40A", Description = "Różnicówka 3-faz", Type = "RCD", IconKind = "ToggleSwitch" });
            RcdModules.Add(new() { Name = "RCD 25A", Description = "Różnicówka 1-faz", Type = "RCD", IconKind = "ToggleSwitchOff" });
        }

        if (McbModules.Count == 0 && !HasAnyModuleFiles("MCB"))
        {
            McbModules.Add(new() { Name = "B16", Description = "Bezpiecznik B16", Type = "MCB", IconKind = "Fuse" });
        }
    }

    private void LoadModulesFromFolder(string category, ObservableCollection<PaletteItem> collection)
    {
        string modulesPath = GetModulesPath(category);

        if (Directory.Exists(modulesPath))
        {
            var files = Directory.EnumerateFiles(modulesPath).Where(IsSupportedModuleFile);
            foreach (var file in files)
            {
                var ext = Path.GetExtension(file);
                var name = Path.GetFileNameWithoutExtension(file);

                if (collection.Any(i => i.Name == name)) continue;

                var item = new PaletteItem
                {
                    Name = name,
                    Description = "Moduł użytkownika",
                    Type = category,
                    FilePath = file
                };

                try
                {
                    if (ext.Equals(".svg", StringComparison.OrdinalIgnoreCase))
                    {
                        var content = File.ReadAllText(file);
                        var svgSource = SvgSource.LoadFromSvg(content);
                        if (svgSource != null)
                        {
                            item.Visual = new SvgImage { Source = svgSource };
                        }
                    }
                    else if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase) || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
                    {
                        using var fs = File.OpenRead(file);
                        item.Visual = new Bitmap(fs);
                    }
                }
                catch { /* Ignore bad files */ }

                collection.Add(item);
            }
        }
    }

    public static readonly StyledProperty<bool> IsModulesEnabledProperty =
        AvaloniaProperty.Register<ModulesPaletteView, bool>(nameof(IsModulesEnabled), false);

    public bool IsModulesEnabled
    {
        get => GetValue(IsModulesEnabledProperty);
        set => SetValue(IsModulesEnabledProperty, value);
    }

    private void DeleteModule_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is PaletteItem item)
        {
            // Remove from the appropriate category collection
            foreach (var category in Categories)
            {
                if (category.Items.Remove(item))
                {
                    // If it's a file-based module, delete the file
                    if (!string.IsNullOrEmpty(item.FilePath) && File.Exists(item.FilePath))
                    {
                        try
                        {
                            File.Delete(item.FilePath);
                        }
                        catch { /* Ignore file deletion errors */ }
                    }
                    break;
                }
            }
        }
    }

    private void DeleteCategory_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is ModuleCategory category)
        {
            // Usuń folder z dysku (wraz z plikami modułów)
            var folderPath = GetModulesPath(category.Name);
            if (Directory.Exists(folderPath))
            {
                try
                {
                    Directory.Delete(folderPath, recursive: true);
                }
                catch { /* Ignore deletion errors */ }
            }

            Categories.Remove(category);
        }
    }

    private async void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Only start drag on left mouse button; right-click opens context menu
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        // CRITICAL BLOCK: If modules are disabled (no DIN rail), DO NOT START DRAG.
        if (!IsModulesEnabled)
        {
            e.Handled = true; // Stop event propagation
            return;
        }

        var border = sender as Border;
        if (border?.DataContext is PaletteItem item)
        {
            var dragData = new DataTransfer();
            dragData.Add(DataTransferItem.Create(DragDropFormats.ModuleType, item.Type));
            dragData.Add(DataTransferItem.Create(DragDropFormats.ModuleName, item.Name));

            if (!string.IsNullOrEmpty(item.FilePath))
                dragData.Add(DataTransferItem.Create(DragDropFormats.ModuleFilePath, item.FilePath));

            await DragDrop.DoDragDropAsync(e, dragData, DragDropEffects.Copy);
        }
    }
}
