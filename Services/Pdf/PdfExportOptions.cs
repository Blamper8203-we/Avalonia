namespace DINBoard.Services;

public enum PngRenderQuality
{
    Standard,
    High,
    Ultra
}

/// <summary>
/// Opcje eksportu PDF.
/// </summary>
public class PdfExportOptions
{
    public string? ObjectName { get; set; }
    public string? Address { get; set; }
    public string? InvestorName { get; set; }
    public string? DesignerName { get; set; }
    public string? DesignerLicense { get; set; }
    public string? ProjectNumber { get; set; }
    public string? Revision { get; set; } = "1.0";

    /// <summary>Czy dodać dodatkową stronę z czystym schematem</summary>
    public bool IncludeCleanSchematic { get; set; } = true;

    public PngRenderQuality PngQuality { get; set; } = PngRenderQuality.High;
}
