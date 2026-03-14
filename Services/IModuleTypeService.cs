namespace DINBoard.Services;

/// <summary>
/// Typ modułu elektrycznego
/// </summary>
public enum ModuleType
{
    Unknown,
    RCD,    // Residual Current Device (Wyłącznik różnicowo-prądowy)
    MCB,    // Miniature Circuit Breaker (Wyłącznik nadprądowy)
    SPD,    // Surge Protection Device (Ogranicznik przepięć)
    Switch, // Switch Disconnector (Rozłącznik izolacyjny / FR)
    PhaseIndicator, // Kontrolki faz (lampki sygnalizacyjne)
    DistributionBlock, // Blok rozdzielczy
    Other
}

/// <summary>
/// Liczba biegunów modułu
/// </summary>
public enum ModulePoleCount
{
    Unknown = 0,
    P1 = 1,     // 1-biegunowy (1 faza)
    P2 = 2,     // 2-biegunowy (1 faza + N lub 2 fazy)
    P3 = 3,     // 3-biegunowy (3 fazy)
    P4 = 4      // 4-biegunowy (3 fazy + N)
}

/// <summary>
/// Serwis do rozpoznawania typów modułów elektrycznych
/// </summary>
public interface IModuleTypeService
{
    /// <summary>
    /// Określa typ modułu na podstawie SymbolItem
    /// </summary>
    ModuleType GetModuleType(Models.SymbolItem? symbol);

    /// <summary>
    /// Sprawdza czy symbol to RCD
    /// </summary>
    bool IsRcd(Models.SymbolItem? symbol);

    /// <summary>
    /// Sprawdza czy symbol to MCB
    /// </summary>
    bool IsMcb(Models.SymbolItem? symbol);

    /// <summary>
    /// Sprawdza czy symbol to SPD
    /// </summary>
    bool IsSpd(Models.SymbolItem? symbol);

    /// <summary>
    /// Zwraca czytelną nazwę typu modułu
    /// </summary>
    string GetModuleTypeName(Models.SymbolItem? symbol);

    /// <summary>
    /// Określa liczbę biegunów modułu (1P, 2P, 3P, 4P)
    /// </summary>
    ModulePoleCount GetPoleCount(Models.SymbolItem? symbol);

    /// <summary>
    /// Określa liczbę biegunów na podstawie ścieżki pliku i typu
    /// </summary>
    ModulePoleCount GetPoleCount(string? visualPath, string? type);

    /// <summary>
    /// Zwraca odpowiednią fazę dla danej liczby biegunów
    /// </summary>
    string GetDefaultPhaseForPoleCount(ModulePoleCount poleCount);

    /// <summary>
    /// Sprawdza czy moduł jest 3-fazowy (3P lub 4P)
    /// </summary>
    bool IsThreePhase(Models.SymbolItem? symbol);

    /// <summary>
    /// Sprawdza czy symbol to Rozłącznik (Switch/FR)
    /// </summary>
    bool IsSwitch(Models.SymbolItem? symbol);

    /// <summary>
    /// Sprawdza czy symbol to Kontrolka Faz (lampka sygnalizacyjna)
    /// </summary>
    bool IsPhaseIndicator(Models.SymbolItem? symbol);

    /// <summary>
    /// Sprawdza czy symbol to Blok Rozdzielczy
    /// </summary>
    bool IsDistributionBlock(Models.SymbolItem? symbol);

    bool IsPowerBusbar(Models.SymbolItem? symbol);
}
