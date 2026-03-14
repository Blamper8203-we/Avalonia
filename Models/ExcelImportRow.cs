namespace DINBoard.Models;

/// <summary>
/// Reprezentuje pojedynczy wiersz (pojedynczy obwód) odczytany z pliku Excel.
/// </summary>
public class ExcelImportRow
{
    public int RowIndex { get; set; }
    public string CircuitName { get; set; } = string.Empty;
    public double PowerW { get; set; }
    public string Phase { get; set; } = string.Empty; // L1, L2, L3
    public string Protection { get; set; } = string.Empty; // np. B16
    public string Location { get; set; } = string.Empty; // Opis / Lokalizacja
    
    // Wynik walidacji
    public bool IsValid => !string.IsNullOrWhiteSpace(CircuitName) && PowerW >= 0;
    public string ValidationError { get; set; } = string.Empty;
}
