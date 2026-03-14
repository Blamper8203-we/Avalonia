using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DINBoard.Constants;
using DINBoard.Models;
using DINBoard.Services;
using DINBoard.ViewModels;
using DINBoard.ViewModels.Messages;
using DINBoard.Helpers;

namespace DINBoard.ViewModels;

/// <summary>
/// Specjalistyczny ViewModel obsługujący zarządzanie cyklem życia projektów.
/// Odpowiada za: zapisywanie, wczytywanie, ostatnie dokumenty, metadane i licencję.
/// Rozbija wielkiego MainViewModel zgodnie z zasadą SRP.
/// </summary>
public partial class ProjectWorkspaceViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;
    private readonly IProjectService? _projectService;
    private readonly IDialogService? _dialogService;
    private readonly ProjectPersistenceService? _persistenceService;

    // Lokalne serwisy (przeniesione z MainViewModel)
    private readonly LicenseService _licenseService = new();
    private readonly RecentProjectsService _recentProjectsService = new();

    public ProjectWorkspaceViewModel(
        MainViewModel mainViewModel,
        IProjectService? projectService = null,
        IDialogService? dialogService = null,
        ProjectPersistenceService? persistenceService = null)
    {
        _mainViewModel = mainViewModel;
        _projectService = projectService;
        _dialogService = dialogService;
        _persistenceService = persistenceService;
    }

    /// <summary>
    /// Odświeża dane specyficzne dla Workspace na ekranie powitalnym.
    /// Zwraca informacje o licencji i listę ostatnich projektów.
    /// </summary>
    public void RefreshHomeScreenData()
    {
        _mainViewModel.License = _licenseService.CurrentLicense;
        _mainViewModel.RecentProjects.Clear();
        foreach (var p in _recentProjectsService.GetRecentProjects())
        {
            _mainViewModel.RecentProjects.Add(p);
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (_projectService == null || _dialogService == null || _persistenceService == null) return;

        if (_mainViewModel.CurrentProject == null)
        {
            _mainViewModel.StatusMessage = "Brak projektu";
            return;
        }

        if (string.IsNullOrEmpty(_projectService.CurrentProjectPath))
        {
            await SaveAsAsync().ConfigureAwait(true);
            return;
        }

        await SaveProjectToPathAsync(_projectService.CurrentProjectPath).ConfigureAwait(true);
    }

    [RelayCommand]
    public async Task SaveAsAsync()
    {
        if (_projectService == null || _dialogService == null || _persistenceService == null) return;
        if (_mainViewModel.CurrentProject == null) return;

        var path = await _dialogService.PickSaveFileAsync("Zapisz projekt", ".json", "Projekt (JSON)").ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path)) return;

        await SaveProjectToPathAsync(path).ConfigureAwait(true);
    }

    private async Task SaveProjectToPathAsync(string path)
    {
        if (_persistenceService == null) return;
        try
        {
            _mainViewModel.StatusMessage = "Zapisywanie...";
            _mainViewModel.EnsureProjectGroupsFromSymbols();

            await _persistenceService.SaveAsync(_mainViewModel.CurrentProject!, _mainViewModel.Symbols.ToList(), _mainViewModel, path).ConfigureAwait(true);

            _mainViewModel.StatusMessage = "Zapisano projekt";
            _mainViewModel.HasUnsavedChanges = false;
            _mainViewModel.RefreshStatusBarProperties();
            WeakReferenceMessenger.Default.Send(new ShowToastMessage(new ToastData(
                LocalizationHelper.GetString("ToastTitleSaved"), 
                System.IO.Path.GetFileName(_projectService?.CurrentProjectPath ?? ""), 
                Controls.ToastType.Success)));
        }
        catch (Exception ex)
        {
            _mainViewModel.StatusMessage = $"Błąd zapisu: {ex.Message}";
            AppLog.Error("Błąd zapisu", ex);
            WeakReferenceMessenger.Default.Send(new ShowToastMessage(new ToastData(
                LocalizationHelper.GetString("ToastTitleSaveError"), 
                ex.Message, 
                Controls.ToastType.Error)));
        }
    }

    [RelayCommand]
    public async Task EditProjectMetadataAsync()
    {
        if (_dialogService == null) return;
        if (_mainViewModel.CurrentProject == null) return;

        _mainViewModel.CurrentProject.Metadata ??= new ProjectMetadata();
        var updatedMetadata = await _dialogService.ShowProjectMetadataDialogAsync(_mainViewModel.CurrentProject.Metadata);
        
        if (updatedMetadata != null)
        {
            _mainViewModel.CurrentProject.Metadata = updatedMetadata;
            _mainViewModel.HasUnsavedChanges = true;
            _mainViewModel.ForceCurrentProjectUpdate();
        }
    }

    [RelayCommand]
    public async Task CreateProjectAsync()
    {
        if (_projectService == null || _dialogService == null) return;
        if (!_licenseService.CanCreateNewProject())
        {
            await _dialogService.ShowConfirmAsync("Licencja Wygasła", "Wykorzystano wszystkie 3 próbne projekty. Aktywuj pełną wersję aby pracować dalej.");
            return;
        }

        await NewProjectInternalAsync();
        _licenseService.ConsumeTrialProject();
        RefreshHomeScreenData();
    }

    internal async Task NewProjectInternalAsync()
    {
        if (_projectService == null || _dialogService == null) return;
        if (_projectService.HasUnsavedChanges)
        {
            var save = await _dialogService.ShowConfirmAsync(
                "Niezapisane zmiany",
                "Masz niezapisane zmiany. Czy chcesz je zapisać przed utworzeniem nowego projektu?").ConfigureAwait(true);
            if (save)
            {
                await SaveAsync().ConfigureAwait(true);
            }
        }

        _mainViewModel.Symbols.Clear();
        _mainViewModel.SelectedSymbol = null;
        _mainViewModel.Schematic.DinRailSvgContent = null;
        _mainViewModel.Schematic.DinRailSize = (0, 0);
        _mainViewModel.Schematic.IsDinRailVisible = false;
        _mainViewModel.Schematic.DinRailScale = AppDefaults.DinRailScale;
        _mainViewModel.Schematic.DinRailAxes.Clear();
        _mainViewModel.Schematic.CircuitReferences.Clear();

        _mainViewModel.CurrentProject = _projectService.CreateNewProject();

        WeakReferenceMessenger.Default.Send(new DinRailRefreshMessage());
        _mainViewModel.RecalculateValidation();

        _mainViewModel.StatusMessage = "Utworzono nowy projekt";
        _mainViewModel.HasUnsavedChanges = false;
        _mainViewModel.IsHomeScreenVisible = false;
        _mainViewModel.RefreshStatusBarProperties();
        WeakReferenceMessenger.Default.Send(new ShowToastMessage(new ToastData(
            LocalizationHelper.GetString("ToastTitleNewProject"), 
            LocalizationHelper.GetString("ToastMsgNewProject"), 
            Controls.ToastType.Info)));
    }

    [RelayCommand]
    public async Task OpenProjectAsync()
    {
        await LoadAsync();
    }

    [RelayCommand]
    public async Task OpenRecentProjectAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            _mainViewModel.StatusMessage = "Plik nie istnieje";
            return;
        }

        await LoadFromPathAsync(path);
    }

    [RelayCommand]
    public void ShowActivationDialog()
    {
        _licenseService.ActivateLicense("PRO-VERSION-OK");
        RefreshHomeScreenData();
        _mainViewModel.StatusMessage = "Zaktualizowano licencję: Wersja Pełna";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (_dialogService == null || _persistenceService == null || _projectService == null) return;
        try
        {
            var path = await _dialogService.PickOpenFileAsync("Otwórz projekt", ".json", "Projekt (JSON)").ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(path)) return;

            await LoadFromPathAsync(path);
        }
        catch (Exception ex)
        {
            _mainViewModel.StatusMessage = $"Błąd otwarcia: {ex.Message}";
            AppLog.Error("Błąd ładowania projektu", ex);
        }
    }

    private async Task LoadFromPathAsync(string path)
    {
        if (_persistenceService == null) return;
        try
        {
            _mainViewModel.StatusMessage = "Wczytywanie...";

            var result = await _persistenceService.LoadAsync(path).ConfigureAwait(true);
            if (result == null) { _mainViewModel.StatusMessage = "Błąd: pusty plik"; return; }

            if (result.SchemaVersion > ProjectSchema.CurrentVersion)
            {
                _mainViewModel.StatusMessage = $"Uwaga: plik zapisany w nowszej wersji (v{result.SchemaVersion}), niektóre dane mogą być niekompatybilne";
                await Task.Delay(2000).ConfigureAwait(true);
            }

            _mainViewModel.CurrentProject = result.Project;

            _mainViewModel.Symbols.Clear();
            foreach (var sym in result.Symbols)
                _mainViewModel.Symbols.Add(sym);

            _mainViewModel.Schematic.DinRailSvgContent = result.DinRailSvgContent;
            _mainViewModel.Schematic.DinRailSize = (result.DinRailWidth, result.DinRailHeight);
            _mainViewModel.Schematic.IsDinRailVisible = result.IsDinRailVisible;
            _mainViewModel.Schematic.DinRailScale = result.DinRailScale > 0 ? result.DinRailScale : AppDefaults.DinRailScale;
            _mainViewModel.Schematic.DinRailAxes.Clear();
            if (result.DinRailAxes != null) _mainViewModel.Schematic.DinRailAxes.AddRange(result.DinRailAxes);

            WeakReferenceMessenger.Default.Send(new DinRailRefreshMessage());
            _mainViewModel.RecalculateModuleNumbers();
            _mainViewModel.RecalculateValidation();

            _recentProjectsService.AddRecentProject(path);
            RefreshHomeScreenData();
            _mainViewModel.IsHomeScreenVisible = false;

            _mainViewModel.StatusMessage = $"Otwarto: {result.Project.Name}";
        }
        catch (Exception ex)
        {
            _mainViewModel.StatusMessage = $"Błąd otwarcia: {ex.Message}";
            AppLog.Error("Błąd ładowania projektu", ex);
        }
    }

    [RelayCommand]
    public async Task PrintAsync()
    {
        if (_mainViewModel.CurrentProject == null) return;
        if (_mainViewModel.Exporter != null)
            await _mainViewModel.Exporter.ExportPdfAsync().ConfigureAwait(true);
        _mainViewModel.StatusMessage = "Przygotowano dokument do druku (PDF)";
    }

    [RelayCommand]
    public async Task ExitAsync()
    {
        if (_projectService != null && _dialogService != null && _projectService.HasUnsavedChanges)
        {
            var save = await _dialogService.ShowConfirmAsync(
                "Niezapisane zmiany",
                "Masz niezapisane zmiany. Czy chcesz je zapisać przed zamknięciem?").ConfigureAwait(true);
            if (save)
            {
                await SaveAsync().ConfigureAwait(true);
            }
        }

        if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
