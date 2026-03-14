using System.Text.Json.Serialization;

namespace DINBoard.Models;

/// <summary>
/// Circuit - reprezentuje obwód elektryczny.
/// </summary>
public class Circuit
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = System.Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("phase")]
    public string Phase { get; set; } = "L1";

    [JsonPropertyName("isPhaseLocked")]
    public bool IsPhaseLocked { get; set; }

    [JsonPropertyName("circuitType")]
    public string CircuitType { get; set; } = "Gniazdo";

    [JsonPropertyName("protection")]
    public string Protection { get; set; } = "B16";

    [JsonPropertyName("rcdId")]
    public string? RcdId { get; set; }

    [JsonPropertyName("powerW")]
    public double PowerW { get; set; }

    [JsonPropertyName("group")]
    public string? Group { get; set; }

    [JsonPropertyName("zone")]
    public string? Zone { get; set; }

    public Circuit Clone() => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        Phase = Phase,
        IsPhaseLocked = IsPhaseLocked,
        CircuitType = CircuitType,
        Protection = Protection,
        RcdId = RcdId,
        PowerW = PowerW,
        Group = Group,
        Zone = Zone
    };
}

/// <summary>
/// CircuitGroup - grupa obwodów.
/// </summary>
public class CircuitGroup
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = System.Guid.NewGuid().ToString();

    /// <summary>
    /// Jawna kolejność grupy (1, 2, 3...).
    /// 0 oznacza brak/nieustalone (dla starych projektów lub migracji).
    /// </summary>
    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("circuits")]
    public System.Collections.Generic.List<Circuit> Circuits { get; set; } = new();
}

/// <summary>
/// CircuitZone - strefa obwodów.
/// </summary>
public class CircuitZone
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = System.Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
