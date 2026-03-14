using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia;
using Avalonia.Media;
using System.Collections.ObjectModel;

namespace DINBoard.Models;
public partial class SymbolItem : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTerminalBlock))]
    [NotifyPropertyChangedFor(nameof(DisplayModuleNumber))]
    private string _type = "";

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private double _rotation;

    [ObservableProperty]
    private string? _label;

    [ObservableProperty]
    private string? _circuitId;

    [ObservableProperty]
    private string? _protection;

    [JsonIgnore]
    public string DisplayProtection
    {
        get
        {
            var typeStr = Type?.ToUpperInvariant() ?? "";
            if (typeStr.Contains("SPD")) return SpdInfo ?? "";
            if (typeStr.Contains("RCD")) return RcdInfo ?? "";
            if (typeStr.Contains("FR") || typeStr.Contains("SWITCH") || typeStr.Contains("ROZŁĄCZNIK")) return $"{FrRatedCurrent} (FR)";
            if (typeStr.Contains("KONTROLKI")) return PhaseIndicatorFuseRating ?? "";
            return ProtectionType ?? "";
        }
    }

    [ObservableProperty]
    private string? _group;

    /// <summary>
    /// Nazwa grupy (współdzielona między wszystkimi symbolami w grupie)
    /// </summary>
    [ObservableProperty]
    private string? _groupName;

    /// <summary>
    /// Nazwa obwodu/rola tego symbolu w grupie
    /// </summary>
    [ObservableProperty]
    private string? _circuitName;

    /// <summary>
    /// Moc urządzenia w watach [W]
    /// </summary>
    [ObservableProperty]
    private double _powerW;

    /// <summary>
    /// Faza zasilania (L1, L2, L3, L1+L2+L3)
    /// </summary>
    [ObservableProperty]
    private string _phase = "L1";

    /// <summary>
    /// Blokada zmiany fazy przy automatycznym bilansowaniu
    /// </summary>
    [ObservableProperty]
    private bool _isPhaseLocked;

    /// <summary>
    /// Typ obwodu: Oświetlenie, Gniazdo, Siła, Inne
    /// Wpływa na limit spadku napięcia (3% oświetlenie, 5% gniazda/siła)
    /// </summary>
    [ObservableProperty]
    private string _circuitType = "Gniazdo";

    /// <summary>
    /// Typ zabezpieczenia (B10, B16, C10, C16, C20, C25, C32 etc.)
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayProtection))]
    private string? _protectionType;

    /// <summary>
    /// Opis/uwagi do obwodu
    /// </summary>
    [ObservableProperty]
    private string? _circuitDescription;

    /// <summary>
    /// Lokalizacja obwodu (np. "Pokój dzienny", "Piętro 1", "Kuchnia")
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayLocation))]
    private string? _location;

    [JsonIgnore]
    public string DisplayLocation => string.IsNullOrWhiteSpace(Location) ? "Brak lokalizacji" : Location;

    /// <summary>
    /// ID symbolu RCD, do którego jest podpięty ten moduł (dla MCB)
    /// </summary>
    [ObservableProperty]
    private string? _rcdSymbolId;

    /// <summary>
    /// Prąd znamionowy RCD w amperach (np. 40, 63) - dla symboli typu RCD
    /// </summary>
    [ObservableProperty]
    private int _rcdRatedCurrent;

    /// <summary>
    /// Prąd różnicowy RCD w mA (np. 30, 100, 300) - dla symboli typu RCD
    /// </summary>
    [ObservableProperty]
    private int _rcdResidualCurrent = 30;

    /// <summary>
    /// Typ RCD (A, AC, B, F) - dla symboli typu RCD
    /// </summary>
    [ObservableProperty]
    private string _rcdType = "A";

    /// <summary>
    /// Informacja o RCD do wyświetlenia na karcie (computed property)
    /// Format: "RCD 40A/30mA Typ A"
    /// </summary>
    [JsonIgnore]
    public string? RcdInfo
    {
        get
        {
            if (RcdRatedCurrent > 0 && RcdResidualCurrent > 0)
            {
                return $"RCD {RcdRatedCurrent}A/{RcdResidualCurrent}mA Typ {RcdType}";
            }
            return null;
        }
    }

    // === SPD (Surge Protection Device) Properties ===

    /// <summary>
    /// Typ SPD (T1, T2, T1+T2, T2+T3) - dla symboli typu SPD
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayProtection))]
    private string _spdType = "T1+T2";

    /// <summary>
    /// Napięcie ochrony SPD w V (np. 275, 320, 385) - dla symboli typu SPD
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayProtection))]
    private int _spdVoltage = 275;

    /// <summary>
    /// Maksymalny prąd wyładowczy SPD w kA (np. 12, 25, 40, 65) - dla symboli typu SPD
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayProtection))]
    private double _spdDischargeCurrent = 25;

    /// <summary>
    /// Informacja o SPD do wyświetlenia na karcie (computed property)
    /// Format: "SPD T1+T2 275V 25kA"
    /// </summary>
    [JsonIgnore]
    public string? SpdInfo
    {
        get
        {
            if (!string.IsNullOrEmpty(SpdType) && SpdVoltage > 0)
            {
                return $"SPD {SpdType} {SpdVoltage}V {SpdDischargeCurrent}kA";
            }
            return null;
        }
    }

    // === FR (Rozłącznik główny) Properties ===

    /// <summary>
    /// Prąd znamionowy FR (np. "63A", "40A")
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayProtection))]
    private string _frRatedCurrent = "63A";

    /// <summary>
    /// Typ FR (np. "63")
    /// </summary>
    [ObservableProperty]
    private string _frType = "63";

    // === Kontrolki Faz (Phase Indicator) Properties ===

    /// <summary>
    /// Model kontrolek faz (np. "3 lampki z bezpiecznikiem")
    /// </summary>
    [ObservableProperty]
    private string _phaseIndicatorModel = "3 lampki z bezpiecznikiem";

    /// <summary>
    /// Bezpiecznik kontrolek faz (np. "2A gG")
    /// </summary>
    [ObservableProperty]
    private string _phaseIndicatorFuseRating = "2A gG";

    /// <summary>
    /// Numer modułu w grupie (1, 2, 3...)
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayModuleNumber))]
    [field: JsonIgnore]
    private int _moduleNumber;

    /// <summary>
    /// Oznaczenie referencyjne zgodne z normą IEC 61346 (np. Q1, F1, K1)
    /// </summary>
    [ObservableProperty]
    private string? _referenceDesignation;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isSnappedToRail;

    [ObservableProperty]
    [field: JsonIgnore]
    private bool _isDragging;

    [ObservableProperty]
    [field: JsonIgnore]
    private bool _isInSelectedGroup;

    [ObservableProperty]
    [field: JsonIgnore]
    private bool _isEditing;

    [RelayCommand]
    private void EnterEditMode() => IsEditing = true;

    [RelayCommand]
    private void ExitEditMode() => IsEditing = false;

    [ObservableProperty]
    [field: JsonIgnore] // Don't serialize the loaded image object
    private IImage? _visual;

    [ObservableProperty]
    private string? _visualPath;



    [ObservableProperty]
    private string? _moduleSourceType;

    [ObservableProperty]
    private string? _moduleRef;

    [ObservableProperty]
    private double _width = 232.58; // 1TE = 17.5mm × 13.29 scale

    [ObservableProperty]
    private double _height = 1103; // 83mm × 13.29 scale

    [ObservableProperty]
    private double _cableLength = 10.0;

    // === Catalog properties ===
    // Removed Manufacturer, Series, CatalogNumber

    [ObservableProperty]
    private double _cableCrossSection = 1.5;

    [ObservableProperty]
    private double _voltageDrop = 0.0;

    [ObservableProperty]
    private System.Collections.Generic.Dictionary<string, string> _parameters = [];

    [JsonIgnore]
    public bool ShowSelectionOutline => IsSelected && !IsDragging && !IsInSelectedGroup;

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSelectionOutline));
    }

    partial void OnIsInSelectedGroupChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSelectionOutline));
    }

    [JsonIgnore]
    public string DisplayModuleNumber
    {
        get
        {
            if (IsTerminalBlock)
                return $"LW{ModuleNumber}";
            if (Type?.Contains("RCD", StringComparison.OrdinalIgnoreCase) == true)
                return "#0";
            return $"#{ModuleNumber}";
        }
    }

    [JsonIgnore]
    public bool IsTerminalBlock =>
        Type?.Contains("TerminalBlock", StringComparison.OrdinalIgnoreCase) == true ||
        Type?.Contains("Listwy", StringComparison.OrdinalIgnoreCase) == true ||
        VisualPath?.Contains("LISTWA", StringComparison.OrdinalIgnoreCase) == true;

    public SymbolItem Clone()
    {
        var cloned = new SymbolItem
        {
            Id = Guid.NewGuid().ToString(),
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
            RcdSymbolId = RcdSymbolId,
            RcdRatedCurrent = RcdRatedCurrent,
            RcdResidualCurrent = RcdResidualCurrent,
            RcdType = RcdType,
            VisualPath = VisualPath,
            Visual = Visual,
            ModuleSourceType = ModuleSourceType,
            ModuleRef = ModuleRef,
            Width = Width,
            Height = Height,
            IsSnappedToRail = IsSnappedToRail,
            CableLength = CableLength,
            CableCrossSection = CableCrossSection,
            VoltageDrop = VoltageDrop,
            SpdType = SpdType,
            SpdVoltage = SpdVoltage,
            SpdDischargeCurrent = SpdDischargeCurrent,
            FrRatedCurrent = FrRatedCurrent,
            FrType = FrType,
            ReferenceDesignation = ReferenceDesignation,
            PhaseIndicatorModel = PhaseIndicatorModel,
            PhaseIndicatorFuseRating = PhaseIndicatorFuseRating,
            ModuleNumber = ModuleNumber,
            IsPhaseLocked = IsPhaseLocked,
            CircuitType = CircuitType,
            Parameters = new System.Collections.Generic.Dictionary<string, string>(Parameters)
        };
        return cloned;
    }
}

public static class ModuleSourceTypes
{
    public const string BuiltInAsset = "BuiltInAsset";
    public const string ProjectRelativeFile = "ProjectRelativeFile";
    public const string AbsoluteFileLegacy = "AbsoluteFileLegacy";
}
