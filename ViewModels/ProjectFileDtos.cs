using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Avalonia;
using DINBoard.Constants;
using DINBoard.Models;
using System.Text.Json.Serialization;

namespace DINBoard.ViewModels;

/// <summary>
/// DTO dla punktu (serializacja)
/// </summary>
internal sealed class PointFile
{
    public double X { get; set; }
    public double Y { get; set; }
}

/// <summary>
/// DTO dla projektu (serializacja)
/// </summary>
internal sealed class ProjectFile
{
    public int SchemaVersion { get; set; } = ProjectSchema.CurrentVersion;
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public PowerSupplyConfig? PowerConfig { get; set; }
    public List<SymbolItemFile> Symbols { get; set; } = new();
    public List<Circuit> Circuits { get; set; } = new();
    public List<CircuitGroup> Groups { get; set; } = new();

    public string? DinRailSvgContent { get; set; }
    public double DinRailWidth { get; set; }
    public double DinRailHeight { get; set; }
    public bool IsDinRailVisible { get; set; }
    public List<double>? DinRailAxes { get; set; }
    public ProjectMetadata? Metadata { get; set; }
    public double DinRailScale { get; set; } = AppDefaults.DinRailScale;

    public static ProjectFile From(Project project, IEnumerable<SymbolItem> symbols, MainViewModel viewModel)
    {
        return new ProjectFile
        {
            SchemaVersion = ProjectSchema.CurrentVersion,
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            PowerConfig = project.PowerConfig,
            Circuits = project.Circuits ?? new List<Circuit>(),
            Groups = project.Groups ?? new List<CircuitGroup>(),
            Symbols = symbols.Select(SymbolItemFile.From).ToList(),
            DinRailSvgContent = viewModel.Schematic.DinRailSvgContent,
            DinRailWidth = viewModel.Schematic.DinRailSize.Width,
            DinRailHeight = viewModel.Schematic.DinRailSize.Height,
            IsDinRailVisible = viewModel.Schematic.IsDinRailVisible,
            DinRailAxes = viewModel.Schematic.DinRailAxes.ToList(),
            DinRailScale = viewModel.Schematic.DinRailScale,
            Metadata = project.Metadata
        };
    }

    public Project ToProject()
    {
        return new Project
        {
            Id = Id,
            Name = Name,
            Description = Description,
            PowerConfig = PowerConfig,
            Circuits = Circuits ?? new List<Circuit>(),
            Groups = Groups ?? new List<CircuitGroup>(),
            Symbols = new List<SymbolItem>(),
            Metadata = Metadata
        };
    }
}

/// <summary>
/// DTO dla symbolu (serializacja)
/// </summary>
internal sealed class SymbolItemFile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Rotation { get; set; }
    public string? Label { get; set; }
    public string? CircuitId { get; set; }
    public string? Protection { get; set; }
    public string? Group { get; set; }
    public string? GroupName { get; set; }
    public string? CircuitName { get; set; }
    public double PowerW { get; set; }
    public string Phase { get; set; } = "L1";
    public string? ProtectionType { get; set; }
    public string? CircuitDescription { get; set; }
    public string? Location { get; set; }
    public bool IsSnappedToRail { get; set; }
    public string? VisualPath { get; set; }
    public string? ModuleSourceType { get; set; }
    public string? ModuleRef { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    // === RCD ===
    public int RcdRatedCurrent { get; set; }
    public int RcdResidualCurrent { get; set; } = 30;
    public string RcdType { get; set; } = "A";
    public string? RcdSymbolId { get; set; }

    // === SPD ===
    public string SpdType { get; set; } = "T1+T2";
    public int SpdVoltage { get; set; } = 275;
    public double SpdDischargeCurrent { get; set; } = 25;

    // === FR ===
    public string FrRatedCurrent { get; set; } = "63A";
    public string FrType { get; set; } = "63";

    // === Kontrolki faz ===
    public string PhaseIndicatorModel { get; set; } = "3 lampki z bezpiecznikiem";
    public string PhaseIndicatorFuseRating { get; set; } = "2A gG";

    // === Kabel ===
    public double CableCrossSection { get; set; }
    public double CableLength { get; set; }

    public Dictionary<string, string> Parameters { get; set; } = new();

    public static SymbolItemFile From(SymbolItem s)
    {
        var shouldKeepVisualPath = string.IsNullOrEmpty(s.ModuleSourceType) ||
                                   s.ModuleSourceType == ModuleSourceTypes.AbsoluteFileLegacy;
        return new SymbolItemFile
        {
            Id = s.Id,
            Type = s.Type,
            X = s.X,
            Y = s.Y,
            Rotation = s.Rotation,
            Label = s.Label,
            CircuitId = s.CircuitId,
            Protection = s.Protection,
            Group = s.Group,
            GroupName = s.GroupName,
            CircuitName = s.CircuitName,
            PowerW = s.PowerW,
            Phase = s.Phase,
            ProtectionType = s.ProtectionType,
            CircuitDescription = s.CircuitDescription,
            Location = s.Location,
            IsSnappedToRail = s.IsSnappedToRail,
            VisualPath = shouldKeepVisualPath ? s.VisualPath : null,
            ModuleSourceType = s.ModuleSourceType,
            ModuleRef = s.ModuleRef,
            Width = s.Width,
            Height = s.Height,
            RcdRatedCurrent = s.RcdRatedCurrent,
            RcdResidualCurrent = s.RcdResidualCurrent,
            RcdType = s.RcdType,
            RcdSymbolId = s.RcdSymbolId,
            SpdType = s.SpdType,
            SpdVoltage = s.SpdVoltage,
            SpdDischargeCurrent = s.SpdDischargeCurrent,
            FrRatedCurrent = s.FrRatedCurrent,
            FrType = s.FrType,
            PhaseIndicatorModel = s.PhaseIndicatorModel,
            PhaseIndicatorFuseRating = s.PhaseIndicatorFuseRating,
            CableCrossSection = s.CableCrossSection,
            CableLength = s.CableLength,
            Parameters = s.Parameters != null
                ? new Dictionary<string, string>(s.Parameters)
                : new Dictionary<string, string>()
        };
    }

    public SymbolItem ToSymbolItem()
    {
        return new SymbolItem
        {
            Id = Id,
            Type = Type,
            X = X,
            Y = Y,
            Rotation = Rotation,
            Label = Label,
            CircuitId = CircuitId,
            Protection = Protection,
            Group = Group,
            GroupName = GroupName,
            CircuitName = CircuitName,
            PowerW = PowerW,
            Phase = Phase,
            ProtectionType = ProtectionType,
            CircuitDescription = CircuitDescription,
            Location = Location,
            IsSnappedToRail = IsSnappedToRail,
            VisualPath = VisualPath,
            ModuleSourceType = ModuleSourceType,
            ModuleRef = ModuleRef,
            Width = Width,
            Height = Height,
            RcdRatedCurrent = RcdRatedCurrent,
            RcdResidualCurrent = RcdResidualCurrent,
            RcdType = RcdType,
            RcdSymbolId = RcdSymbolId,
            SpdType = SpdType,
            SpdVoltage = SpdVoltage,
            SpdDischargeCurrent = SpdDischargeCurrent,
            FrRatedCurrent = FrRatedCurrent,
            FrType = FrType,
            PhaseIndicatorModel = PhaseIndicatorModel,
            PhaseIndicatorFuseRating = PhaseIndicatorFuseRating,
            CableCrossSection = CableCrossSection,
            CableLength = CableLength,
            Parameters = Parameters != null
                ? new Dictionary<string, string>(Parameters)
                : new Dictionary<string, string>()
        };
    }
}

/// <summary>
/// Ustawienia eksportu
/// </summary>
public class ExportSettings
{
    public bool IncludeSchematic { get; set; } = true;
    public bool IncludeCircuitList { get; set; } = true;
    public bool IncludePowerBalance { get; set; } = true;
    public byte[]? SchematicImageBytes { get; set; }
}

/// <summary>
/// Opcje serializacji JSON
/// </summary>
internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Instance = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}

internal static class ProjectSchema
{
    public const int CurrentVersion = 2;
}
