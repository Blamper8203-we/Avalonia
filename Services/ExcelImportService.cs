using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DINBoard.Models;
using ExcelDataReader;

namespace DINBoard.Services;

public class ExcelImportService
{
    public ExcelImportService()
    {
        // Rejestrujemy dostawcę kodowania wymaganego przez ExcelDataReader w .NET Core/5+
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// Parsuje plik Excel i zwraca listę odczytanych obwodów.
    /// Zakłada prosty układ bez nagłówków w pierwszych wierszach, lub po prostu odczytuje kolumny A..E startując od wiersza 1 lub 2.
    /// Z założenia: A = Nazwa, B = Moc (W), C = Zabezpieczenie, D = Faza, E = Lokalizacja/Opis
    /// </summary>
    public List<ExcelImportRow> ReadCircuitsFromFile(string filePath, bool hasHeaders = true)
    {
        var result = new List<ExcelImportRow>();
        
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            throw new FileNotFoundException("Nie znaleziono pliku.", filePath);

        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);

        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration()
        {
            ConfigureDataTable = (_) => new ExcelDataTableConfiguration()
            {
                UseHeaderRow = hasHeaders
            }
        });

        if (dataSet.Tables.Count == 0)
            throw new Exception("Plik Excel jest pusty lub nie zawiera odczytywalnych arkuszy.");

        var table = dataSet.Tables[0];
        
        for (int i = 0; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];
            
            // Pomiń całkowicie puste wiersze
            bool isEmpty = true;
            for(int col = 0; col < table.Columns.Count; col++)
            {
                if (row[col] != null && !string.IsNullOrWhiteSpace(row[col].ToString()))
                {
                    isEmpty = false;
                    break;
                }
            }
            if (isEmpty) continue;

            var importRow = new ExcelImportRow
            {
                RowIndex = i + (hasHeaders ? 2 : 1) // Offset wizualny 1-based (Excel)
            };

            // Parsowanie kolumn
            try
            {
                if (table.Columns.Count > 0) importRow.CircuitName = row[0]?.ToString()?.Trim() ?? "";
                
                if (table.Columns.Count > 1) 
                {
                    var powerStr = row[1]?.ToString()?.Trim();
                    if (double.TryParse(powerStr, out double power))
                    {
                        importRow.PowerW = power;
                    }
                }
                
                if (table.Columns.Count > 2) importRow.Protection = row[2]?.ToString()?.Trim() ?? "";
                if (table.Columns.Count > 3) importRow.Phase = row[3]?.ToString()?.Trim() ?? "";
                if (table.Columns.Count > 4) importRow.Location = row[4]?.ToString()?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(importRow.CircuitName))
                {
                    importRow.ValidationError = "Brak nazwy obwodu";
                }
            }
            catch (Exception ex)
            {
                importRow.ValidationError = $"Błąd odczytu: {ex.Message}";
            }

            result.Add(importRow);
        }

        return result;
    }
}
