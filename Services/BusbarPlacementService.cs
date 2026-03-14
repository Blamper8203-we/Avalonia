using System;
using System.IO;
using IOPath = System.IO.Path;
using DINBoard.Models;

namespace DINBoard.Services;

/// <summary>
/// Generuje oraz importuje symbol szyny pradowej do projektu.
/// </summary>
public class BusbarPlacementService
{
    private readonly SymbolImportService _symbolImportService;
    private readonly IProjectService _projectService;
    private readonly PowerBusbarGenerator _busbarGenerator;

    public BusbarPlacementService(
        SymbolImportService symbolImportService,
        IProjectService projectService,
        PowerBusbarGenerator busbarGenerator)
    {
        _symbolImportService = symbolImportService ?? throw new ArgumentNullException(nameof(symbolImportService));
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        _busbarGenerator = busbarGenerator ?? throw new ArgumentNullException(nameof(busbarGenerator));
    }

    public SymbolItem? GenerateAndImportBusbar(int pinCount, BusbarType busbarType, double dinRailScale)
    {
        var svg = _busbarGenerator.GenerateSvgForPinCount(pinCount, busbarType);
        var filePath = GetBusbarOutputPath(pinCount);

        Directory.CreateDirectory(IOPath.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, svg);

        var symbol = _symbolImportService.ImportFromFile(filePath, "Listwy", $"Szyna pradowa {pinCount}P");
        if (symbol == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(_projectService.CurrentProjectPath))
        {
            _symbolImportService.PrepareModuleReference(symbol, _projectService.CurrentProjectPath);
        }

        if (dinRailScale > 0)
        {
            symbol.Width *= dinRailScale;
            symbol.Height *= dinRailScale;
        }

        symbol.X = -symbol.Width / 2;
        symbol.Y = -symbol.Height / 2;
        symbol.IsSelected = true;
        return symbol;
    }

    private string GetBusbarOutputPath(int pinCount)
    {
        if (!string.IsNullOrWhiteSpace(_projectService.CurrentProjectPath))
        {
            var projectDir = IOPath.GetDirectoryName(_projectService.CurrentProjectPath);
            if (!string.IsNullOrWhiteSpace(projectDir))
            {
                return IOPath.Combine(projectDir, "modules", $"szyna_pradowa_{pinCount}p_{DateTime.Now:yyyyMMdd_HHmmss}.svg");
            }
        }

        var tempDir = IOPath.Combine(IOPath.GetTempPath(), "DINBoard", "busbars");
        return IOPath.Combine(tempDir, $"szyna_pradowa_{pinCount}p_{DateTime.Now:yyyyMMdd_HHmmss}.svg");
    }
}
