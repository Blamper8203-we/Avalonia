using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DINBoard.Models;
using DINBoard.Services;

namespace DINBoard.Services;

/// <summary>
/// Serwis eksportu Zestawienia Materiałowego (BOM - Bill of Materials) do pliku CSV.
/// Grupuje takie same aparaty (np. 5 szt. wyłączników B16 1P) w jedną pozycję.
/// </summary>
public class BomExportService
{
    private readonly IModuleTypeService _moduleTypeService;

    public BomExportService(IModuleTypeService moduleTypeService)
    {
        _moduleTypeService = moduleTypeService;
    }

    /// <summary>
    /// Generuje i zapisuje CSV pod wskazaną ścieżką.
    /// </summary>
    public void ExportToCsv(Project project, IEnumerable<SymbolItem> symbols, string filePath)
    {
        var csvContent = GenerateCsv(project, symbols);
        
        // Zapisujemy używając UTF8 z BOM (Byte Order Mark), żeby Excel poprawnie wykrył polskie znaki
        File.WriteAllText(filePath, csvContent, new UTF8Encoding(true));
    }

    /// <summary>
    /// Generuje ciąg znaków (string) w formacie CSV z elementami rozdzielnicy.
    /// </summary>
    public string GenerateCsv(Project project, IEnumerable<SymbolItem> symbols)
    {
        if (symbols == null || !symbols.Any())
            return string.Empty;

        var sb = new StringBuilder();

        // 1. Nagłówek pliku z metadanymi projektu
        sb.AppendLine($"Zestawienie Materiałowe (BOM) - Projekt: {project?.Name ?? "Nowy Projekt"}");
        sb.AppendLine($"Data wygenerowania: {DateTime.Now:yyyy-MM-dd HH:mm}");
        if (project?.Metadata != null)
        {
            if (!string.IsNullOrWhiteSpace(project.Metadata.ProjectNumber))
                sb.AppendLine($"Numer rysunku: {project.Metadata.ProjectNumber}");
            if (!string.IsNullOrWhiteSpace(project.Metadata.Investor))
                sb.AppendLine($"Inwestor: {project.Metadata.Investor}");
            if (!string.IsNullOrWhiteSpace(project.Metadata.Address))
                sb.AppendLine($"Adres obiektu: {project.Metadata.Address}");
            if (!string.IsNullOrWhiteSpace(project.Metadata.Company) || !string.IsNullOrWhiteSpace(project.Metadata.Contractor))
                sb.AppendLine($"Wykonawca: {project.Metadata.Contractor ?? project.Metadata.Company}");
            if (!string.IsNullOrWhiteSpace(project.Metadata.Author))
                sb.AppendLine($"Projektant: {project.Metadata.Author}");
        }
        sb.AppendLine();

        // 2. Nagłówki kolumn CSV (separatorem będzie średnik ';', który jest domyślny dla polskiego Excela)
        sb.AppendLine("ID / Oznaczenie;Kategoria;Opis aparatu;Bieguny;Zabezpieczenie;Ilość");

        // 3. Grupowanie aparatów
        // Grupujemy po Typie (Category), Zabezpieczeniu (np. B16) i Oznaczeniu (ReferenceDesignation/Label - opcjonalnie)
        // Ze względu na specyfikę projektu grupom przypisujemy te same właściwości "elektryczne"
        
        // Klasa pomocnicza do grupowania
        var groupedItems = symbols
            .Where(s => !s.IsTerminalBlock && !IsBusbar(s)) // Pomijamy złączki i gołe szyny, albo jeśli chcemy miedź - odkomentować
            .GroupBy(s => new 
            { 
                Type = _moduleTypeService.GetModuleType(s),
                VisualName = s.Type ?? "Nieznany moduł", 
                Protection = s.ProtectionType ?? "", 
                PoleCount = _moduleTypeService.GetPoleCount(s)
            })
            .OrderBy(g => g.Key.Type.ToString())
            .ToList();

        // Dodatkowa grupa dla listew zaciskowych, bo są przydatne w zestawieniu
        var terminalBlocksCount = symbols.Count(s => s.IsTerminalBlock);

        // 4. Generowanie wierszy
        foreach (var group in groupedItems)
        {
            var sample = group.First();
            string categoryName = GetCategoryName(group.Key.Type);
            
            // Kolumny: ID, Kategoria, Opis (Złączone stringi), Biegunność, Parametr (A), Ilość
            string ids = string.Join(", ", group.Select(s => s.ReferenceDesignation ?? s.Id).Where(s => !string.IsNullOrEmpty(s)));
            if (ids.Length > 25) ids = ids.Substring(0, 22) + "..."; // Skracamy jeśli zbyt długa lista ID

            string typ = EscapeCsv(group.Key.VisualName);
            string poles = group.Key.PoleCount == ModulePoleCount.Unknown ? "-" : group.Key.PoleCount.ToString().Replace("P", "P");
            string prot = EscapeCsv(group.Key.Protection);
            int count = group.Count();

            sb.AppendLine($"{EscapeCsv(ids)};{EscapeCsv(categoryName)};{typ};{poles};{prot};{count}");
        }

        if (terminalBlocksCount > 0)
        {
            sb.AppendLine($"X;- Złączki szynowe;Listwa przyłączeniowa / Zacisk PE/N;-;-;{terminalBlocksCount}");
        }

        // 5. Podsumowanie obudowy (na podstawie sumy biegunów lub ilości)
        double estimatedTE = symbols.Sum(s => 
        {
            var p = _moduleTypeService.GetPoleCount(s);
            return p == ModulePoleCount.Unknown ? 1.0 : (double)p; // 1P = 1TE, 3P = 3TE itp.
        });
        
        sb.AppendLine();
        sb.AppendLine($"-;Obudowa;Zajętość miejsca na szynie DIN w modułach (TE);-;~ {estimatedTE} TE;-");

        return sb.ToString();
    }

    private bool IsBusbar(SymbolItem s) =>
        s.Type?.Contains("Busbar", StringComparison.OrdinalIgnoreCase) == true;

    private string GetCategoryName(ModuleType type)
    {
        return type switch
        {
            ModuleType.MCB => "Wyłączniki nadprądowe (MCB)",
            ModuleType.RCD => "Wyłączniki różnicowoprądowe (RCD)",
            ModuleType.Switch => "Rozłączniki / Główne",
            ModuleType.SPD => "Ograniczniki przepięć (SPD)",
            ModuleType.PhaseIndicator => "Lampki kontrolne / Kontrolki faz",
            ModuleType.DistributionBlock => "Bloki rozdzielcze",
            _ => "Aparatura modułowa"
        };
    }

    private string EscapeCsv(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        
        // Jeśli pole zawiera średnik, cudzysłów lub nową linię, musi być otoczone cudzysłowami
        if (field.Contains(";") || field.Contains("\"") || field.Contains("\n"))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }
}
