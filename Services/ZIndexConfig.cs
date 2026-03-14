namespace DINBoard.Services;

/// <summary>
/// Konfiguracja Z-Index dla warstw rysowania.
/// </summary>
public class ZIndexConfig
{
    public static ZIndexConfig Instance { get; } = new();

    public int Grid { get; set; } = 0;
    public int Symbols { get; set; } = 20;
    public int Selection { get; set; } = 40;
    public int Overlay { get; set; } = 100;
}
