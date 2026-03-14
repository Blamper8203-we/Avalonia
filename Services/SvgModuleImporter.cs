using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Media;
using DINBoard.Models;

namespace DINBoard.Services;

/// <summary>
/// Model dla modułu SVG w importerze
/// </summary>
public class ImportableModule : INotifyPropertyChanged
{
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ModuleType { get; set; } = "";
    public int PoleCount { get; set; }
    public DateTime FileDate { get; set; }
    public long FileSize { get; set; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }

    private bool _isDuplicate;
    public bool IsDuplicate
    {
        get => _isDuplicate;
        set { if (_isDuplicate != value) { _isDuplicate = value; OnPropertyChanged(); OnPropertyChanged(nameof(DuplicateWarning)); } }
    }

    private IImage? _thumbnail;
    public IImage? Thumbnail
    {
        get => _thumbnail;
        set { _thumbnail = value; OnPropertyChanged(); }
    }

    public string DisplayName => Path.GetFileNameWithoutExtension(FileName);

    public string PoleCountFormatted => PoleCount > 0 ? $"{PoleCount}P" : "";

    public string DuplicateWarning => IsDuplicate ? "⚠ duplikat" : "";

    public string FileSizeFormatted => FileSize >= 1024 * 1024
        ? $"{FileSize / (1024.0 * 1024.0):F1} MB"
        : $"{FileSize / 1024.0:F1} KB";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Usługa do importowania modułów SVG
/// </summary>
public class SvgModuleImporter
{
    private readonly SvgProcessor _svgProcessor;

    public SvgModuleImporter(SvgProcessor svgProcessor)
    {
        _svgProcessor = svgProcessor ?? throw new ArgumentNullException(nameof(svgProcessor));
    }

    public IReadOnlyCollection<string> GetExistingCategories(string modulesRoot)
    {
        if (!Directory.Exists(modulesRoot))
            return Array.Empty<string>();

        return Directory.EnumerateDirectories(modulesRoot)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .OrderBy(name => name)
            .ToArray();
    }

    /// <summary>
    /// Skanuje folder w poszukiwaniu plików SVG modułów
    /// </summary>
    public ObservableCollection<ImportableModule> ScanFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return new ObservableCollection<ImportableModule>();

        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true
            };

            var svgFiles = Directory.EnumerateFiles(folderPath, "*.*", options)
                .Where(IsSupportedFile);

            return LoadFiles(svgFiles);
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Błąd podczas skanowania folderu: {folderPath}", ex);
            return new ObservableCollection<ImportableModule>();
        }
    }

    private ImportableModule BuildModule(FileInfo fileInfo)
    {
        var module = new ImportableModule
        {
            FilePath = fileInfo.FullName,
            FileName = fileInfo.Name,
            ModuleType = DetectModuleType(fileInfo.FullName, fileInfo.DirectoryName ?? ""),
            PoleCount = DetectPoleCount(fileInfo.Name),
            FileDate = fileInfo.LastWriteTime,
            FileSize = fileInfo.Length,
            IsSelected = true
        };

        // Załaduj miniaturkę
        try
        {
            var ext = Path.GetExtension(fileInfo.Name);
            if (ext.Equals(".svg", StringComparison.OrdinalIgnoreCase))
            {
                var content = File.ReadAllText(fileInfo.FullName);
                var svgSource = global::Avalonia.Svg.Skia.SvgSource.LoadFromSvg(content);
                if (svgSource != null)
                    module.Thumbnail = new global::Avalonia.Svg.Skia.SvgImage { Source = svgSource };
            }
            else
            {
                using var fs = File.OpenRead(fileInfo.FullName);
                module.Thumbnail = new global::Avalonia.Media.Imaging.Bitmap(fs);
            }
        }
        catch { /* Ignoruj błędy miniaturek */ }

        return module;
    }

    private static readonly string[] SupportedExtensions = { ".svg", ".png", ".jpg", ".jpeg" };

    private static bool IsSupportedFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return SupportedExtensions.Any(e => string.Equals(ext, e, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Ładuje moduły z listy plików
    /// </summary>
    public ObservableCollection<ImportableModule> LoadFiles(IEnumerable<string> filePaths)
    {
        var modules = new ObservableCollection<ImportableModule>();

        foreach (var filePath in filePaths)
        {
            if (!IsSupportedFile(filePath))
                continue;

            try
            {
                var fileInfo = new FileInfo(filePath);
                modules.Add(BuildModule(fileInfo));
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Błąd podczas odczytu pliku: {filePath}", ex);
            }
        }

        return modules;
    }

    /// <summary>
    /// Importuje wybrane moduły do docelowego folderu projektu.
    /// Kopie pliki bez modyfikacji - same jak istniejące moduły.
    /// </summary>
    public int ImportModules(
        ObservableCollection<ImportableModule> modules,
        string targetBaseDirectory,
        string targetCategory,
        bool scaleToStandard,
        int? poleCountOverride,
        bool overwriteExisting)
    {
        int importedCount = 0;

        if (!Directory.Exists(targetBaseDirectory))
            Directory.CreateDirectory(targetBaseDirectory);

        foreach (var module in modules.Where(m => m.IsSelected))
        {
            try
            {
                var category = string.IsNullOrWhiteSpace(targetCategory) ? module.ModuleType : targetCategory;
                var targetDir = Path.Combine(targetBaseDirectory, category);
                if (!Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);

                var targetPath = Path.Combine(targetDir, module.FileName);

                // Jeśli plik już istnieje i nie nadpisujemy, dodaj licznik
                if (File.Exists(targetPath) && !overwriteExisting)
                {
                    var baseName = Path.GetFileNameWithoutExtension(module.FileName);
                    var extension = Path.GetExtension(module.FileName);
                    int counter = 1;

                    while (File.Exists(targetPath))
                    {
                        var newFileName = $"{baseName}_{counter}{extension}";
                        targetPath = Path.Combine(targetDir, newFileName);
                        counter++;
                    }
                }

                // Kopiuj plik bez zmian - dokładnie jak istniejące moduły
                File.Copy(module.FilePath, targetPath, overwrite: overwriteExisting);
                importedCount++;
                AppLog.Debug($"Zaimportowano moduł: {targetPath}");
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Błąd podczas importu modułu: {module.FilePath}", ex);
            }
        }

        return importedCount;
    }

    /// <summary>
    /// Odczytuje zawartość SVG z podglądem (base64 dla wyświetlenia)
    /// </summary>
    public string GetSvgPreview(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
                return File.ReadAllText(filePath);
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Błąd podczas odczytywania SVG: {filePath}", ex);
        }
        return "";
    }

    /// <summary>
    /// Detektuje typ modułu na podstawie ścieżki i nazwy
    /// </summary>
    private string DetectModuleType(string filePath, string directoryName)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath).ToUpperInvariant();
        var dirName = directoryName.ToUpperInvariant();

        if (fileName.Contains("RCD") || dirName.Contains("RCD"))
            return "RCD";
        if (fileName.Contains("MCB") || dirName.Contains("MCB"))
            return "MCB";
        if (fileName.Contains("SPD") || dirName.Contains("SPD"))
            return "SPD";
        if (fileName.Contains("FR") || dirName.Contains("FR"))
            return "FR";
        if (fileName.Contains("BLOK") || dirName.Contains("BLOK"))
            return "Blok rozdzielczy";
        if (fileName.Contains("SZYNA") || dirName.Contains("SZYNA"))
            return "Szyna prądowa";
        if (fileName.Contains("LISTWA") || fileName.Contains("TERMINAL") || dirName.Contains("LISTWY"))
            return "Listwy";
        if (fileName.Contains("ZŁĄCZ") || fileName.Contains("ZLACZ") || dirName.Contains("ZŁĄCZA") || dirName.Contains("ZLACZA"))
            return "Złącza";
        if (fileName.Contains("KONTROLK") || dirName.Contains("CONTROLS"))
            return "Controls";

        return "Other";
    }

    /// <summary>
    /// Detektuje liczbę biegunów na podstawie nazwy pliku
    /// </summary>
    private int DetectPoleCount(string fileName)
    {
        var upperName = fileName.ToUpperInvariant();

        if (upperName.Contains("4P") || upperName.Contains("4-P"))
            return 4;
        if (upperName.Contains("3P") || upperName.Contains("3-P"))
            return 3;
        if (upperName.Contains("2P") || upperName.Contains("2-P"))
            return 2;
        if (upperName.Contains("1P") || upperName.Contains("1-P"))
            return 1;

        return 0;
    }

    /// <summary>
    /// Oznacza moduły, które już istnieją w docelowym folderze.
    /// </summary>
    public void CheckDuplicates(ObservableCollection<ImportableModule> modules, string targetBaseDirectory, string targetCategory)
    {
        var targetDir = Path.Combine(targetBaseDirectory, targetCategory);
        if (!Directory.Exists(targetDir))
        {
            foreach (var m in modules) m.IsDuplicate = false;
            return;
        }

        var existingFiles = new HashSet<string>(
            Directory.EnumerateFiles(targetDir).Select(Path.GetFileName).Where(n => n != null)!,
            StringComparer.OrdinalIgnoreCase);

        foreach (var module in modules)
        {
            module.IsDuplicate = existingFiles.Contains(module.FileName);
        }
    }
}
