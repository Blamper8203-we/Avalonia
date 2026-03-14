using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using DINBoard.Constants;
using DINBoard.Models;
using DINBoard.Services;
using DINBoard.ViewModels;

namespace Avalonia.Tests;

public sealed class ProjectRoundTripTests : IDisposable
{
    private readonly string _tempProjectDir1;
    private readonly string _tempProjectDir2;
    private readonly string _assetCategoryDir;
    private readonly string _assetFilePath;

    public ProjectRoundTripTests()
    {
        _tempProjectDir1 = Path.Combine(Path.GetTempPath(), "AvaloniaTests_" + Guid.NewGuid());
        _tempProjectDir2 = Path.Combine(Path.GetTempPath(), "AvaloniaTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempProjectDir1);
        Directory.CreateDirectory(_tempProjectDir2);

        _assetCategoryDir = Path.Combine(SymbolImportService.GetAssetsModulesRoot(), "Test");
        Directory.CreateDirectory(_assetCategoryDir);
        _assetFilePath = Path.Combine(_assetCategoryDir, "RoundTrip.svg");
        File.WriteAllText(_assetFilePath, "<svg width='10' height='10'></svg>");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempProjectDir1))
        {
            try { Directory.Delete(_tempProjectDir1, true); } catch { }
        }

        if (Directory.Exists(_tempProjectDir2))
        {
            try { Directory.Delete(_tempProjectDir2, true); } catch { }
        }

        if (File.Exists(_assetFilePath))
        {
            try { File.Delete(_assetFilePath); } catch { }
        }

        if (Directory.Exists(_assetCategoryDir))
        {
            try
            {
                if (Directory.GetFileSystemEntries(_assetCategoryDir).Length == 0)
                    Directory.Delete(_assetCategoryDir, true);
            }
            catch { }
        }
    }

    [Fact]
    public void RoundTrip_WithBuiltInModule_ShouldResolveAfterBasePathChange()
    {
        var projectPath1 = Path.Combine(_tempProjectDir1, "project.json");
        var projectPath2 = Path.Combine(_tempProjectDir2, "project.json");

        var symbol = new SymbolItem
        {
            Type = "MCB",
            VisualPath = _assetFilePath,
            Width = 10,
            Height = 10
        };

        var importService = new SymbolImportService();
        importService.PrepareModuleReference(symbol, projectPath1);

        var project = new Project
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Test",
            PowerConfig = new PowerSupplyConfig()
        };

        var file = new ProjectFile
        {
            SchemaVersion = ProjectSchema.CurrentVersion,
            Id = project.Id,
            Name = project.Name,
            PowerConfig = project.PowerConfig,
            Circuits = new List<Circuit>(),
            Groups = new List<CircuitGroup>(),
            Symbols = new List<SymbolItemFile> { SymbolItemFile.From(symbol) },
            DinRailWidth = 0,
            DinRailHeight = 0,
            IsDinRailVisible = false,
            DinRailAxes = new List<double>(),
            DinRailScale = AppDefaults.DinRailScale
        };

        var json = JsonSerializer.Serialize(file, JsonOptions.Instance);
        File.WriteAllText(projectPath1, json);
        File.WriteAllText(projectPath2, json);

        var loaded = JsonSerializer.Deserialize<ProjectFile>(File.ReadAllText(projectPath2), JsonOptions.Instance);
        Assert.NotNull(loaded);
        Assert.Single(loaded!.Symbols);

        var loadedSymbol = loaded.Symbols[0].ToSymbolItem();
        var resolved = importService.ResolveVisualPath(loadedSymbol, projectPath2, out var warning);

        Assert.True(string.IsNullOrWhiteSpace(warning));
        Assert.False(string.IsNullOrWhiteSpace(resolved));
        Assert.True(File.Exists(resolved));

        loadedSymbol.VisualPath = resolved;
        importService.RefreshVisual(loadedSymbol);
        Assert.NotNull(loadedSymbol.Visual);
    }

    [Fact]
    public async Task ProjectPersistenceService_SaveLoad_ShouldRoundTripProjectAndDinRailState()
    {
        var path = Path.Combine(_tempProjectDir1, "persisted_project.json");

        var projectService = new ProjectService();
        var importService = new SymbolImportService();
        var persistenceService = new ProjectPersistenceService(projectService, importService);

        var project = new Project
        {
            Id = Guid.NewGuid().ToString(),
            Name = "RT Project",
            Description = "Round-trip test",
            PowerConfig = new PowerSupplyConfig
            {
                Voltage = 400,
                MainProtection = 40,
                PowerKw = 18,
                Phases = 3
            },
            Metadata = new ProjectMetadata
            {
                Author = "Test Author",
                Company = "Test Company",
                Revision = "2.1"
            }
        };

        var symbols = new List<SymbolItem>
        {
            new()
            {
                Type = "MCB",
                Label = "B16",
                VisualPath = _assetFilePath,
                Width = 18,
                Height = 90,
                X = 10,
                Y = 20,
                PowerW = 1200,
                Phase = "L1"
            }
        };

        var vm = new MainViewModel();
        vm.Schematic.DinRailSvgContent = "<svg width='200' height='40'></svg>";
        vm.Schematic.DinRailSize = (200, 40);
        vm.Schematic.IsDinRailVisible = true;
        vm.Schematic.DinRailScale = 0.22;
        vm.Schematic.DinRailAxes.AddRange(new[] { 0.0, 25.0, 50.0 });

        await persistenceService.SaveAsync(project, symbols, vm, path);
        var loaded = await persistenceService.LoadAsync(path);

        Assert.NotNull(loaded);
        Assert.Equal(project.Name, loaded!.Project.Name);
        Assert.Equal(project.Description, loaded.Project.Description);
        Assert.Equal(project.Metadata?.Author, loaded.Project.Metadata?.Author);
        Assert.Equal(project.PowerConfig?.MainProtection, loaded.Project.PowerConfig?.MainProtection);

        Assert.Single(loaded.Symbols);
        Assert.Equal(symbols[0].Type, loaded.Symbols[0].Type);
        Assert.Equal(symbols[0].Label, loaded.Symbols[0].Label);
        Assert.Equal(symbols[0].PowerW, loaded.Symbols[0].PowerW);

        Assert.Equal(vm.Schematic.DinRailSvgContent, loaded.DinRailSvgContent);
        Assert.Equal(vm.Schematic.DinRailSize.Width, loaded.DinRailWidth);
        Assert.Equal(vm.Schematic.DinRailSize.Height, loaded.DinRailHeight);
        Assert.Equal(vm.Schematic.IsDinRailVisible, loaded.IsDinRailVisible);
        Assert.Equal(vm.Schematic.DinRailScale, loaded.DinRailScale);
        Assert.Equal(vm.Schematic.DinRailAxes.Count, loaded.DinRailAxes?.Count ?? 0);
    }
}
