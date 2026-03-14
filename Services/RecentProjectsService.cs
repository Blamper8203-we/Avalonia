using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DINBoard.Services;

public class RecentProjectsService
{
    private readonly string _recentFilePath;
    private const int MaxRecentFiles = 5;

    public RecentProjectsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appData, "DINBoard");
        if (!Directory.Exists(appFolder))
        {
            Directory.CreateDirectory(appFolder);
        }
        
        _recentFilePath = Path.Combine(appFolder, "recent_projects.json");
    }

    public List<string> GetRecentProjects()
    {
        if (File.Exists(_recentFilePath))
        {
            try
            {
                var json = File.ReadAllText(_recentFilePath);
                var recent = JsonSerializer.Deserialize<List<string>>(json);
                if (recent != null)
                {
                    // Weryfikacja czy pliki nadal istnieją
                    var existingFiles = new List<string>();
                    bool changed = false;
                    foreach (var file in recent)
                    {
                        if (File.Exists(file)) existingFiles.Add(file);
                        else changed = true;
                    }
                    if (changed) SaveRecentProjects(existingFiles); // aktualizujemy, jeśli jakiś plik usunięto
                    
                    return existingFiles;
                }
            }
            catch
            {
                // Błąd odczytu
            }
        }
        
        return new List<string>();
    }

    public void AddRecentProject(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        var recents = GetRecentProjects();
        
        // Zawsze przesuwamy na górę listy
        recents.Remove(filePath);
        recents.Insert(0, filePath);
        
        if (recents.Count > MaxRecentFiles)
        {
            recents = recents.GetRange(0, MaxRecentFiles);
        }
        
        SaveRecentProjects(recents);
    }

    private void SaveRecentProjects(List<string> projects)
    {
        try
        {
            var json = JsonSerializer.Serialize(projects, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_recentFilePath, json);
        }
        catch
        {
            // ignorujemy
        }
    }
}
