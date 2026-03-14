namespace DINBoard.Models;

public enum SchematicNodeType
{
    MainBreaker, SPD, RCD, MCB, PhaseIndicator, Other
}

public class SchematicNode
{
    public SchematicNodeType NodeType { get; set; }
    public SymbolItem? Symbol { get; set; }
    public SymbolItem? DistributionBlockSymbol { get; set; }

    // Oznaczenia wg PN-EN 81346
    public string Designation { get; set; } = "";      // Q0, Q1, QF1, T1, H1
    public string Protection { get; set; } = "";       // B16, C20, 40A/30mA
    public string CircuitName { get; set; } = "";      // Oświetlenie salon
    public string CableDesig { get; set; } = "";       // W1, W2...
    public string CableType { get; set; } = "";        // YDYp
    public string CableSpec { get; set; } = "";        // 3×1.5mm²
    public string CableLength { get; set; } = "";      // 10m
    public string PowerInfo { get; set; } = "";        // 500W
    public string? Phase { get; set; }
    public int PhaseCount { get; set; } = 1;
    public string Location { get; set; } = "";         // Pokój dzienny

    // Pozycja
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 36;
    public double Height { get; set; } = 52;
    public int Column { get; set; }
    public int Page { get; set; }
    public double CellWidth { get; set; } = 114; // Domyślna szerokość (ColStep - 6)

    public System.Collections.Generic.List<SchematicNode> Children { get; set; } = new();
}
