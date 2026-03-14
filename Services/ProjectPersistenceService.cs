using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DINBoard.Models;
using DINBoard.ViewModels;

namespace DINBoard.Services;

/// <summary>
/// Serwis odpowiedzialny za serializację i deserializację projektów.
/// Wydzielony z MainViewModel dla separacji odpowiedzialności.
/// </summary>
public class ProjectPersistenceService
{
    private readonly IProjectService _projectService;
    private readonly SymbolImportService _symbolImportService;

    public ProjectPersistenceService(
        IProjectService projectService,
        SymbolImportService symbolImportService)
    {
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        _symbolImportService = symbolImportService ?? throw new ArgumentNullException(nameof(symbolImportService));
    }

    /// <summary>
    /// Serializuje i zapisuje projekt do pliku JSON.
    /// </summary>
    public async Task SaveAsync(
        Project project,
        IEnumerable<SymbolItem> symbols,
        MainViewModel viewModel,
        string path)
    {
        PrepareModuleSourcesForSave(symbols, path);

        if (project.Metadata != null)
            project.Metadata.DateModified = DateTime.UtcNow;

        var file = ProjectFile.From(project, symbols, viewModel);
        var json = JsonSerializer.Serialize(file, JsonOptions.Instance);
        await File.WriteAllTextAsync(path, json).ConfigureAwait(false);

        _projectService.CurrentProjectPath = path;
        _projectService.MarkAsSaved(path);
    }

    /// <summary>
    /// Deserializuje projekt z pliku JSON.
    /// </summary>
    public async Task<ProjectLoadResult?> LoadAsync(string path)
    {
        var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        var file = JsonSerializer.Deserialize<ProjectFile>(json, JsonOptions.Instance);
        if (file == null) return null;

        var project = file.ToProject();

        var symbols = new List<SymbolItem>();
        foreach (var sf in file.Symbols)
        {
            var sym = sf.ToSymbolItem();
            var resolved = _symbolImportService.ResolveVisualPath(sym, path, out _);
            if (resolved != null) sym.VisualPath = resolved;
            symbols.Add(sym);
        }

        _projectService.CurrentProjectPath = path;

        return new ProjectLoadResult
        {
            Project = project,
            Symbols = symbols,
            SchemaVersion = file.SchemaVersion,
            DinRailSvgContent = file.DinRailSvgContent,
            DinRailWidth = file.DinRailWidth,
            DinRailHeight = file.DinRailHeight,
            IsDinRailVisible = file.IsDinRailVisible,
            DinRailScale = file.DinRailScale,
            DinRailAxes = file.DinRailAxes
        };
    }

    private void PrepareModuleSourcesForSave(IEnumerable<SymbolItem> symbols, string path)
    {
        foreach (var symbol in symbols)
            _symbolImportService.PrepareModuleReference(symbol, path);
    }
}

/// <summary>
/// Wynik deserializacji projektu.
/// </summary>
public class ProjectLoadResult
{
    public Project Project { get; init; } = null!;
    public List<SymbolItem> Symbols { get; init; } = new();
    public int SchemaVersion { get; init; }
    public string? DinRailSvgContent { get; init; }
    public double DinRailWidth { get; init; }
    public double DinRailHeight { get; init; }
    public bool IsDinRailVisible { get; init; }
    public double DinRailScale { get; init; }
    public List<double>? DinRailAxes { get; init; }
}
