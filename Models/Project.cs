using System.Collections.Generic;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DINBoard.Models;

/// <summary>
/// Project - główny model projektu.
/// </summary>
public class Project
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = System.Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = "Nowy projekt";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("symbols")]
    public List<SymbolItem> Symbols { get; set; } = new();

    [JsonPropertyName("circuits")]
    public List<Circuit> Circuits { get; set; } = new();

    [JsonPropertyName("groups")]
    public List<CircuitGroup> Groups { get; set; } = new();

    [JsonPropertyName("powerConfig")]
    public PowerSupplyConfig? PowerConfig { get; set; }

    [JsonPropertyName("metadata")]
    public ProjectMetadata? Metadata { get; set; }
}

/// <summary>
/// ProjectMetadata - metadane projektu zgodne z normami.
/// </summary>
public class ProjectMetadata
{
    [JsonPropertyName("projectNumber")]
    public string? ProjectNumber { get; set; }

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("authorLicense")]
    public string? AuthorLicense { get; set; }

    [JsonPropertyName("company")]
    public string? Company { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("investor")]
    public string? Investor { get; set; }

    [JsonPropertyName("contractor")]
    public string? Contractor { get; set; }

    [JsonPropertyName("designerId")]
    public string? DesignerId { get; set; }

    [JsonPropertyName("revision")]
    public string? Revision { get; set; } = "1.0";

    [JsonPropertyName("dateCreated")]
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("dateModified")]
    public DateTime DateModified { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("standards")]
    public List<string> Standards { get; set; } = new()
    {
        "IEC 61082",
        "PN-EN 60617",
        "IEC 61346",
        "PN-HD 60364"
    };
}

/// <summary>
/// PowerSupplyConfig - konfiguracja zasilania.
/// </summary>
public partial class PowerSupplyConfig : ObservableObject
{
    [ObservableProperty]
    [property: JsonPropertyName("voltage")]
    private int _voltage = 400;

    [ObservableProperty]
    [property: JsonPropertyName("mainProtection")]
    private int _mainProtection = 32;

    [ObservableProperty]
    [property: JsonPropertyName("powerKw")]
    private double _powerKw = 15;

    [ObservableProperty]
    [property: JsonPropertyName("phases")]
    private int _phases = 3;
}

/// <summary>
/// RcdOption - opcja zabezpieczenia różnicowo-prądowego.
/// </summary>
public class RcdOption
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = System.Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("currentMa")]
    public int CurrentMa { get; set; } = 30;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "AC";
}

/// <summary>
/// Terminal - terminal elektryczny.
/// </summary>
public class Terminal
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("symbolId")]
    public string SymbolId { get; set; } = "";

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("isTop")]
    public bool IsTop { get; set; }

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }
}

/// <summary>
/// WireType - typ przewodu.
/// </summary>
public class WireType
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("colorHex")]
    public string ColorHex { get; set; } = "#000000";

    [JsonPropertyName("crossSection")]
    public double CrossSection { get; set; } = 2.5;
}
