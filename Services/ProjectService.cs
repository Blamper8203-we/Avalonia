using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DINBoard.Models;

namespace DINBoard.Services;

/// <summary>
/// Interfejs serwisu projektu.
/// </summary>
public interface IProjectService
{
    string? CurrentProjectPath { get; set; }
    Project? CurrentProject { get; set; }
    bool HasUnsavedChanges { get; set; }

    event EventHandler<Project>? ProjectLoaded;
    event EventHandler<Project>? ProjectSaved;
    event EventHandler? ProjectChanged;

    Task<bool> SaveJsonAsync(string path, string jsonContent);
    Task<string?> LoadJsonAsync(string path);
    Project CreateNewProject(string name = "Nowy projekt");
    void MarkAsChanged();
    void MarkAsSaved(string path);
    string GetProjectDisplayName();
}

/// <summary>
/// Serwis zarządzający projektem - rozszerzona wersja z obsługą zapisu/odczytu i zdarzeń.
/// </summary>
public class ProjectService : IProjectService
{
    public string? CurrentProjectPath { get; set; }
    public Project? CurrentProject { get; set; }
    public bool HasUnsavedChanges { get; set; }

    public event EventHandler<Project>? ProjectLoaded;
    public event EventHandler<Project>? ProjectSaved;
    public event EventHandler? ProjectChanged;

    public async Task<string?> LoadJsonAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            CurrentProjectPath = path;
            HasUnsavedChanges = false;

            if (CurrentProject != null)
                ProjectLoaded?.Invoke(this, CurrentProject);

            return json;
        }
        catch (IOException ex)
        {
            AppLog.Error($"Błąd wczytywania projektu: {path}", ex);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            AppLog.Error($"Błąd wczytywania projektu: {path}", ex);
            return null;
        }
        catch (System.Security.SecurityException ex)
        {
            AppLog.Error($"Błąd wczytywania projektu: {path}", ex);
            return null;
        }
    }

    public async Task<bool> SaveJsonAsync(string path, string jsonContent)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            await File.WriteAllTextAsync(path, jsonContent).ConfigureAwait(false);

            CurrentProjectPath = path;
            HasUnsavedChanges = false;

            if (CurrentProject != null)
                ProjectSaved?.Invoke(this, CurrentProject);

            return true;
        }
        catch (IOException ex)
        {
            AppLog.Error($"Błąd zapisu projektu: {path}", ex);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            AppLog.Error($"Błąd zapisu projektu: {path}", ex);
            return false;
        }
        catch (System.Security.SecurityException ex)
        {
            AppLog.Error($"Błąd zapisu projektu: {path}", ex);
            return false;
        }
    }

    public Project CreateNewProject(string name = "Nowy projekt")
    {
        var project = new Project
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            PowerConfig = new PowerSupplyConfig()
        };

        CurrentProject = project;
        CurrentProjectPath = null;
        HasUnsavedChanges = false;

        return project;
    }

    public void MarkAsChanged()
    {
        HasUnsavedChanges = true;
        ProjectChanged?.Invoke(this, EventArgs.Empty);
    }

    public void MarkAsSaved(string path)
    {
        CurrentProjectPath = path;
        HasUnsavedChanges = false;

        if (CurrentProject != null)
            ProjectSaved?.Invoke(this, CurrentProject);
    }

    public string GetProjectDisplayName()
    {
        if (CurrentProject == null)
            return "Brak projektu";

        var name = CurrentProject.Name;

        if (!string.IsNullOrEmpty(CurrentProjectPath))
        {
            var fileName = Path.GetFileNameWithoutExtension(CurrentProjectPath);
            name = fileName;
        }

        return HasUnsavedChanges ? $"{name} *" : name;
    }
}
