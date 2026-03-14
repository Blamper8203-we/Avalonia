using System;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DINBoard.Models;

namespace DINBoard.Services;

/// <summary>
/// Serwis odpowiedzialny za import symboli z plików SVG/obrazów.
/// Wydziela logikę parsowania z MainWindow.OnCanvasDrop.
/// </summary>
public class SymbolImportService
{
    private readonly SvgProcessor _svgProcessor;

    public SymbolImportService()
    {
        _svgProcessor = new SvgProcessor();
    }

    /// <summary>
    /// Importuje symbol z pliku (SVG, PNG, JPG).
    /// </summary>
    /// <param name="filePath">Ścieżka do pliku</param>
    /// <param name="moduleType">Typ modułu (opcjonalny)</param>
    /// <param name="moduleName">Nazwa modułu (opcjonalna)</param>
    /// <returns>SymbolItem lub null w przypadku błędu</returns>
    public SymbolItem? ImportFromFile(string filePath, string? moduleType = null, string? moduleName = null)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            AppLog.Warn($"SymbolImportService: Plik nie istnieje: {filePath}");
            return null;
        }

        try
        {
            string ext = Path.GetExtension(filePath);

            var symbol = new SymbolItem
            {
                Type = moduleType ?? "Unknown",
                Label = moduleName ?? Path.GetFileNameWithoutExtension(filePath),
                VisualPath = filePath
            };

            // Automatyczne wykrywanie ProtectionType z nazwy (np. "B16 1P" -> "B16")
            if (!string.IsNullOrEmpty(symbol.Label))
            {
                var parts = symbol.Label.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    // Przyjmujemy pierwsze słowo jako typ zabezpieczenia (np. B16, C20, RCD)
                    symbol.ProtectionType = parts[0];

                    // Jeśli to MCB (B.., C..), upewnij się że to wygląda jak zabezpieczenie
                    if (symbol.Type == "MCB" || (!symbol.Label.StartsWith("RCD") && !symbol.Label.StartsWith("FR") && !symbol.Label.StartsWith("SPD")))
                    {
                        // Można dodać bardziej zaawansowaną walidację regexem jeśli trzeba
                    }
                }
            }

            if (ext.Equals(".svg", StringComparison.OrdinalIgnoreCase))
            {
                return ImportSvg(symbol, filePath);
            }
            else if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase) || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                return ImportBitmap(symbol, filePath);
            }
            else
            {
                AppLog.Warn($"SymbolImportService: Nieobsługiwany format pliku: {ext}");
                return null;
            }
        }
        catch (Exception ex)
        {
            AppLog.Error($"SymbolImportService: Błąd importu pliku: {filePath}", ex);
            return null;
        }
    }

    /// <summary>
    /// Odświeża wizualizację symbolu (po zmianie parametrów).
    /// </summary>
    public void RefreshVisual(SymbolItem symbol)
    {
        if (symbol == null || string.IsNullOrEmpty(symbol.VisualPath))
        {
            AppLog.Warn("SymbolImportService: Brak ścieżki dla RefreshVisual");
            return;
        }

        if (!File.Exists(symbol.VisualPath))
        {
            AppLog.Warn($"SymbolImportService: Plik nie istnieje: {symbol.VisualPath}");
            return;
        }

        try
        {
            // Zachowaj oryginalne wymiary (już przeskalowane)
            double originalWidth = symbol.Width;
            double originalHeight = symbol.Height;

            string content = File.ReadAllText(symbol.VisualPath);

            // Zastosuj parametry
            content = _svgProcessor.ApplyParameters(content, symbol.Parameters);
            content = _svgProcessor.ApplyBlueCoverVisibility(content, symbol.Parameters);

            // Normalizuj SVG (ale nie zmieniaj wymiarów symbolu!)
            var normResult = _svgProcessor.NormalizeAndCalculateSize(content, symbol.VisualPath);

            // NIE nadpisuj wymiarów - zachowaj oryginalną skalę
            // symbol.Width = normResult.Width;  // USUNIĘTO
            // symbol.Height = normResult.Height; // USUNIĘTO

            // Utwórz obiekt wizualny
            var svgSource = global::Avalonia.Svg.Skia.SvgSource.LoadFromSvg(normResult.NormalizedSvg);
            if (svgSource != null)
            {
                symbol.Visual = new global::Avalonia.Svg.Skia.SvgImage { Source = svgSource };
            }

            AppLog.Debug($"SymbolImportService: Odświeżono wizualizację: {symbol.Label} (wymiary zachowane: {originalWidth:F0}x{originalHeight:F0})");
        }
        catch (Exception ex)
        {
            AppLog.Error($"SymbolImportService: Błąd odświeżania wizualizacji: {symbol.VisualPath}", ex);
        }
    }

    /// <summary>
    /// Tworzy podgląd SVG dla drag preview (bez tworzenia pełnego SymbolItem).
    /// </summary>
    public (global::Avalonia.Svg.Skia.SvgImage? Image, double Width, double Height) CreateSvgPreview(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return (null, 0, 0);

            string content = File.ReadAllText(filePath);
            var normResult = _svgProcessor.NormalizeAndCalculateSize(content, filePath);

            var svgSource = global::Avalonia.Svg.Skia.SvgSource.LoadFromSvg(normResult.NormalizedSvg);
            if (svgSource != null)
            {
                var image = new global::Avalonia.Svg.Skia.SvgImage { Source = svgSource };
                return (image, normResult.Width, normResult.Height);
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"SymbolImportService: Błąd tworzenia podglądu: {filePath}", ex);
        }

        return (null, 0, 0);
    }

    public void PrepareModuleReference(SymbolItem symbol, string projectPath)
    {
        if (symbol == null || string.IsNullOrWhiteSpace(symbol.VisualPath))
            return;

        if (!File.Exists(symbol.VisualPath))
            return;

        if (string.IsNullOrWhiteSpace(projectPath))
            return;

        var projectDir = Path.GetDirectoryName(projectPath);
        if (string.IsNullOrWhiteSpace(projectDir))
            return;

        var fullVisualPath = Path.GetFullPath(symbol.VisualPath);

        if (TryGetBuiltInModuleRef(fullVisualPath, out var builtInRef))
        {
            symbol.ModuleSourceType = ModuleSourceTypes.BuiltInAsset;
            symbol.ModuleRef = builtInRef;
            return;
        }

        if (IsUnderPath(fullVisualPath, projectDir))
        {
            symbol.ModuleSourceType = ModuleSourceTypes.ProjectRelativeFile;
            symbol.ModuleRef = NormalizeModuleRef(Path.GetRelativePath(projectDir, fullVisualPath));
            return;
        }

        var modulesDir = Path.Combine(projectDir, "modules");
        Directory.CreateDirectory(modulesDir);

        var fileName = Path.GetFileName(fullVisualPath);
        var destPath = GetUniquePath(Path.Combine(modulesDir, fileName));

        if (!string.Equals(fullVisualPath, destPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(fullVisualPath, destPath, overwrite: false);
        }

        symbol.ModuleSourceType = ModuleSourceTypes.ProjectRelativeFile;
        symbol.ModuleRef = NormalizeModuleRef(Path.GetRelativePath(projectDir, destPath));
        symbol.VisualPath = destPath;
    }

    public string? ResolveVisualPath(SymbolItem symbol, string? projectPath, out string? warning)
    {
        warning = null;
        if (symbol == null)
            return null;

        if (symbol.ModuleSourceType == ModuleSourceTypes.BuiltInAsset && !string.IsNullOrWhiteSpace(symbol.ModuleRef))
        {
            var builtInPath = Path.Combine(GetAssetsModulesRoot(), NormalizeModuleRef(symbol.ModuleRef)
                .Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(builtInPath))
                return builtInPath;

            warning = $"Brak wbudowanego modułu: {symbol.ModuleRef}";
        }
        else if (symbol.ModuleSourceType == ModuleSourceTypes.ProjectRelativeFile && !string.IsNullOrWhiteSpace(symbol.ModuleRef))
        {
            var projectDir = string.IsNullOrWhiteSpace(projectPath) ? null : Path.GetDirectoryName(projectPath);
            if (!string.IsNullOrWhiteSpace(projectDir))
            {
                var projectFilePath = Path.Combine(projectDir, NormalizeModuleRef(symbol.ModuleRef)
                    .Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(projectFilePath))
                    return projectFilePath;
            }

            warning = $"Brak pliku modułu w projekcie: {symbol.ModuleRef}";
        }
        else if (symbol.ModuleSourceType == ModuleSourceTypes.AbsoluteFileLegacy)
        {
            if (!string.IsNullOrWhiteSpace(symbol.VisualPath) && File.Exists(symbol.VisualPath))
                return symbol.VisualPath;

            warning = $"Brak pliku modułu: {symbol.VisualPath}";
        }

        if (!string.IsNullOrWhiteSpace(symbol.VisualPath) && File.Exists(symbol.VisualPath))
            return symbol.VisualPath;

        var fileName = Path.GetFileName(symbol.VisualPath ?? symbol.ModuleRef ?? "");
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var assetsRoot = GetAssetsModulesRoot();
            var mapped = FindFileByName(assetsRoot, fileName);
            if (mapped != null)
            {
                symbol.ModuleSourceType = ModuleSourceTypes.BuiltInAsset;
                symbol.ModuleRef = NormalizeModuleRef(Path.GetRelativePath(assetsRoot, mapped));
                return mapped;
            }

            if (!string.IsNullOrWhiteSpace(projectPath))
            {
                var projectDir = Path.GetDirectoryName(projectPath);
                if (!string.IsNullOrWhiteSpace(projectDir))
                {
                    var projectModules = Path.Combine(projectDir, "modules");
                    var projectMapped = FindFileByName(projectModules, fileName);
                    if (projectMapped != null)
                    {
                        symbol.ModuleSourceType = ModuleSourceTypes.ProjectRelativeFile;
                        symbol.ModuleRef = NormalizeModuleRef(Path.GetRelativePath(projectDir, projectMapped));
                        return projectMapped;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Rekord dla cache'owania wyników preview.
    /// </summary>
    private sealed record SvgPreviewCacheEntry(global::Avalonia.Svg.Skia.SvgImage Image, double Width, double Height);

    public static string GetAssetsModulesRoot() => Path.Combine(AppContext.BaseDirectory, "Assets", "Modules");

    public static bool TryGetBuiltInModuleRef(string filePath, out string? moduleRef)
    {
        moduleRef = null;
        var assetsRoot = GetAssetsModulesRoot();
        if (!IsUnderPath(filePath, assetsRoot))
            return false;

        moduleRef = NormalizeModuleRef(Path.GetRelativePath(assetsRoot, filePath));
        return true;
    }

    #region Private Methods

    private static bool IsUnderPath(string path, string root)
    {
        var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeModuleRef(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static string GetUniquePath(string path)
    {
        if (!File.Exists(path))
            return path;

        var directory = Path.GetDirectoryName(path) ?? "";
        var baseName = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        int index = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(directory, $"{baseName}_{index}{extension}");
            index++;
        }
        while (File.Exists(candidate));

        return candidate;
    }

    private static string? FindFileByName(string root, string fileName)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root) || string.IsNullOrWhiteSpace(fileName))
            return null;

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            if (string.Equals(Path.GetFileName(file), fileName, StringComparison.OrdinalIgnoreCase))
                return file;
        }

        return null;
    }

    private SymbolItem ImportSvg(SymbolItem symbol, string filePath)
    {
        string svgContent = File.ReadAllText(filePath);

        // Ustaw domyślne parametry dla bloku rozdzielczego
        if (filePath.Contains("blok rozdzielczy", StringComparison.OrdinalIgnoreCase))
        {
            if (!symbol.Parameters.ContainsKey("BLUE_COVER_VISIBLE"))
                symbol.Parameters["BLUE_COVER_VISIBLE"] = "True";
        }

        // Ekstrakcja placeholderów {{KEY}}
        ExtractPlaceholders(svgContent, symbol, filePath);

        // Zastosuj parametry
        svgContent = _svgProcessor.ApplyParameters(svgContent, symbol.Parameters);
        svgContent = _svgProcessor.ApplyBlueCoverVisibility(svgContent, symbol.Parameters);

        bool isBusbar = filePath.Contains("szyna_pradowa_", StringComparison.OrdinalIgnoreCase) ||
                        filePath.Contains("szyna prądowa", StringComparison.OrdinalIgnoreCase) ||
                        (symbol.Label?.Contains("Szyna prądowa", StringComparison.OrdinalIgnoreCase) == true);

        if (isBusbar)
        {
            var (width, height) = TryGetSvgSize(svgContent);
            if (width > 0 && height > 0)
            {
                symbol.Width = width;
                symbol.Height = height;
            }

            var svgSourceBusbar = global::Avalonia.Svg.Skia.SvgSource.LoadFromSvg(svgContent);
            if (svgSourceBusbar != null)
            {
                symbol.Visual = new global::Avalonia.Svg.Skia.SvgImage { Source = svgSourceBusbar };
            }

            if (symbol.Visual == null && svgSourceBusbar != null)
                symbol.Visual = new global::Avalonia.Svg.Skia.SvgImage { Source = svgSourceBusbar };

            AppLog.Info($"SymbolImportService: Zaimportowano SVG: {symbol.Label} ({symbol.Width:F0}x{symbol.Height:F0})");
            return symbol;
        }

        var normResult = _svgProcessor.NormalizeAndCalculateSize(svgContent, filePath);
        symbol.Width = normResult.Width;
        symbol.Height = normResult.Height;

        var svgSource = global::Avalonia.Svg.Skia.SvgSource.LoadFromSvg(normResult.NormalizedSvg);

        if (svgSource != null)
        {
            symbol.Visual = new global::Avalonia.Svg.Skia.SvgImage { Source = svgSource };
        }

        if (symbol.Visual == null && svgSource != null)
            symbol.Visual = new global::Avalonia.Svg.Skia.SvgImage { Source = svgSource };

        // Logowanie końcowe już w returnie
        AppLog.Info($"SymbolImportService: Zaimportowano SVG: {symbol.Label} ({symbol.Width:F0}x{symbol.Height:F0})");
        return symbol;
    }

    private SymbolItem ImportBitmap(SymbolItem symbol, string filePath)
    {
        using var fs = File.OpenRead(filePath);
        var bitmap = new global::Avalonia.Media.Imaging.Bitmap(fs);
        symbol.Visual = bitmap;
        symbol.Width = bitmap.Size.Width;
        symbol.Height = bitmap.Size.Height;

        AppLog.Info($"SymbolImportService: Zaimportowano obraz: {symbol.Label} ({symbol.Width:F0}x{symbol.Height:F0})");
        return symbol;
    }

    private void ExtractPlaceholders(string svgContent, SymbolItem symbol, string filePath)
    {
        var placeholders = Regex.Matches(svgContent, @"{{([A-Z0-9_]+)}}");

        foreach (Match ph in placeholders)
        {
            string key = ph.Groups[1].Value;
            if (symbol.Parameters.ContainsKey(key))
                continue;

            // Ustaw wartości domyślne bazując na kluczu
            switch (key)
            {
                case "CURRENT":
                    symbol.Parameters[key] = "40A";
                    break;
                case "SENSITIVITY":
                    symbol.Parameters[key] = "30mA";
                    break;
                case "TYPE":
                    symbol.Parameters[key] = "Typ A";
                    break;
                case "LABEL":
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    string labelValue = fileName.Split(' ')[0];
                    symbol.Parameters[key] = labelValue;
                    break;
                case "SUBTEXT":
                    symbol.Parameters[key] = "";
                    break;
                default:
                    symbol.Parameters[key] = "?";
                    break;
            }
        }
    }

    private static (double Width, double Height) TryGetSvgSize(string svgContent)
    {
        try
        {
            var doc = XDocument.Parse(svgContent);
            var root = doc.Root;
            if (root == null)
            {
                return (0, 0);
            }

            string? viewBox = (string?)root.Attribute("viewBox");
            if (!string.IsNullOrWhiteSpace(viewBox))
            {
                var parts = viewBox.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4 &&
                    double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double vw) &&
                    double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double vh))
                {
                    return (vw, vh);
                }
            }

            double width = ParseSvgLength((string?)root.Attribute("width"));
            double height = ParseSvgLength((string?)root.Attribute("height"));
            return (width, height);
        }
        catch
        {
            return (0, 0);
        }
    }

    private static double ParseSvgLength(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        string trimmed = value.Trim();
        if (trimmed.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^2];
        }
        else if (trimmed.EndsWith("%", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
            ? result
            : 0;
    }

    #endregion
}
