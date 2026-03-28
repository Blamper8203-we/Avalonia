using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DINBoard.Constants;
using DINBoard.Helpers;
using DINBoard.Models;
using DINBoard.Services;
using DINBoard.ViewModels.Messages;

namespace DINBoard.ViewModels;

/// <summary>
/// Specjalistyczny ViewModel obsługujący zarządzanie cyklem życia projektów.
/// Odpowiada za: zapisywanie, wczytywanie, ostatnie dokumenty, metadane i licencje.
/// Rozbija wielkiego MainViewModel zgodnie z zasadą SRP.
/// </summary>
public partial class ProjectWorkspaceViewModel : ObservableObject
{
    private const string ProjectFileExtension = ".dinboard";
    private const string ProjectFileFilterName = "Projekt DINBoard";
    private readonly MainViewModel _mainViewModel;
    private readonly IProjectService? _projectService;
    private readonly IDialogService? _dialogService;
    private readonly ProjectPersistenceService? _persistenceService;
    private readonly LicenseService _licenseService;
    private readonly RecentProjectsService _recentProjectsService;

    public bool ShowActivationShortcut => _mainViewModel.License.IsTrial && _licenseService.IsLocalActivationShortcutEnabled;

    public ProjectWorkspaceViewModel(
        MainViewModel mainViewModel,
        LicenseService licenseService,
        RecentProjectsService recentProjectsService,
        IProjectService? projectService = null,
        IDialogService? dialogService = null,
        ProjectPersistenceService? persistenceService = null)
    {
        _mainViewModel = mainViewModel;
        _licenseService = licenseService ?? throw new ArgumentNullException(nameof(licenseService));
        _recentProjectsService = recentProjectsService ?? throw new ArgumentNullException(nameof(recentProjectsService));
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
        foreach (var recentProject in _recentProjectsService.GetRecentProjects())
        {
            _mainViewModel.RecentProjects.Add(recentProject);
        }

        OnPropertyChanged(nameof(ShowActivationShortcut));
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (_projectService == null || _dialogService == null || _persistenceService == null)
        {
            return;
        }

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
        if (_projectService == null || _dialogService == null || _persistenceService == null)
        {
            return;
        }

        if (_mainViewModel.CurrentProject == null)
        {
            return;
        }

        var path = await _dialogService.PickSaveFileAsync("Zapisz projekt", ProjectFileExtension, ProjectFileFilterName).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await SaveProjectToPathAsync(EnsureProjectFileExtension(path)).ConfigureAwait(true);
    }

    private async Task SaveProjectToPathAsync(string path)
    {
        if (_persistenceService == null)
        {
            return;
        }

        try
        {
            _mainViewModel.StatusMessage = "Zapisywanie...";
            _mainViewModel.EnsureProjectGroupsFromSymbols();

            await _persistenceService
                .SaveAsync(_mainViewModel.CurrentProject!, _mainViewModel.Symbols.ToList(), _mainViewModel, path)
                .ConfigureAwait(true);

            _mainViewModel.StatusMessage = "Zapisano projekt";
            _mainViewModel.MarkProjectAsSaved();
            _mainViewModel.RefreshStatusBarProperties();
            WeakReferenceMessenger.Default.Send(new ShowToastMessage(new ToastData(
                LocalizationHelper.GetString("ToastTitleSaved"),
                Path.GetFileName(_projectService?.CurrentProjectPath ?? string.Empty),
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
        if (_dialogService == null || _mainViewModel.CurrentProject == null)
        {
            return;
        }

        _mainViewModel.CurrentProject.Metadata ??= new ProjectMetadata();
        var updatedMetadata = await _dialogService
            .ShowProjectMetadataDialogAsync(_mainViewModel.CurrentProject.Metadata)
            .ConfigureAwait(true);

        if (updatedMetadata != null)
        {
            _mainViewModel.CurrentProject.Metadata = updatedMetadata;
            _mainViewModel.MarkProjectAsChanged();
            _mainViewModel.ForceCurrentProjectUpdate();
        }
    }

    [RelayCommand]
    public async Task CreateProjectAsync()
    {
        if (_projectService == null || _dialogService == null)
        {
            return;
        }

        if (!_licenseService.CanCreateNewProject())
        {
            await _dialogService
                .ShowConfirmAsync("Licencja Wygasła", "Wykorzystano wszystkie 3 próbne projekty. Aktywuj pełną wersję aby pracować dalej.")
                .ConfigureAwait(true);
            return;
        }

        var created = await NewProjectInternalAsync().ConfigureAwait(true);
        if (!created)
        {
            return;
        }

        _licenseService.ConsumeTrialProject();
        RefreshHomeScreenData();
    }

    internal async Task<bool> NewProjectInternalAsync()
    {
        if (_projectService == null || _dialogService == null)
        {
            return false;
        }

        if (!await TryPersistUnsavedChangesAsync(
                "Masz niezapisane zmiany. Czy chcesz je zapisać przed utworzeniem nowego projektu?")
                .ConfigureAwait(true))
        {
            return false;
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
        _mainViewModel.MarkProjectAsSaved();
        _mainViewModel.IsHomeScreenVisible = false;
        _mainViewModel.RefreshStatusBarProperties();
        WeakReferenceMessenger.Default.Send(new ShowToastMessage(new ToastData(
            LocalizationHelper.GetString("ToastTitleNewProject"),
            LocalizationHelper.GetString("ToastMsgNewProject"),
            Controls.ToastType.Info)));
        return true;
    }

    [RelayCommand]
    public async Task OpenProjectAsync()
    {
        await LoadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    public async Task OpenRecentProjectAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            _mainViewModel.StatusMessage = "Plik nie istnieje";
            return;
        }

        await LoadFromPathAsync(path).ConfigureAwait(true);
    }

    [RelayCommand]
    public void ShowActivationDialog()
    {
        if (!_licenseService.IsLocalActivationShortcutEnabled)
        {
            _mainViewModel.StatusMessage = "Lokalna aktywacja pełnej wersji jest wyłączona w tej wersji aplikacji.";
            return;
        }

        if (!_licenseService.ActivateLicense("PRO-VERSION-OK"))
        {
            _mainViewModel.StatusMessage = "Nie udało się aktywować licencji.";
            return;
        }

        RefreshHomeScreenData();
        _mainViewModel.StatusMessage = "Zaktualizowano licencję: Wersja Pełna";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (_dialogService == null || _persistenceService == null || _projectService == null)
        {
            return;
        }

        try
        {
            var path = await _dialogService
                .PickOpenFileAsync("Otwórz projekt", ProjectFileExtension, ProjectFileFilterName)
                .ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            await LoadFromPathAsync(path).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _mainViewModel.StatusMessage = $"Błąd otwarcia: {ex.Message}";
            AppLog.Error("Błąd ładowania projektu", ex);
        }
    }

    private async Task LoadFromPathAsync(string path)
    {
        if (_persistenceService == null)
        {
            return;
        }

        try
        {
            _mainViewModel.StatusMessage = "Wczytywanie...";

            var result = await _persistenceService.LoadAsync(path).ConfigureAwait(true);
            if (result == null)
            {
                _mainViewModel.StatusMessage = "Błąd: pusty plik";
                return;
            }

            if (result.SchemaVersion > ProjectSchema.CurrentVersion)
            {
                _mainViewModel.StatusMessage =
                    $"Uwaga: plik zapisany w nowszej wersji (v{result.SchemaVersion}), niektóre dane mogą być niekompatybilne";
                await Task.Delay(2000).ConfigureAwait(true);
            }

            _mainViewModel.CurrentProject = result.Project;

            _mainViewModel.Symbols.Clear();
            foreach (var symbol in result.Symbols)
            {
                _mainViewModel.Symbols.Add(symbol);
            }

            _mainViewModel.Schematic.DinRailSvgContent = result.DinRailSvgContent;
            _mainViewModel.Schematic.DinRailSize = (result.DinRailWidth, result.DinRailHeight);
            _mainViewModel.Schematic.IsDinRailVisible = result.IsDinRailVisible;
            _mainViewModel.Schematic.DinRailScale = result.DinRailScale > 0 ? result.DinRailScale : AppDefaults.DinRailScale;
            _mainViewModel.Schematic.DinRailAxes.Clear();
            if (result.DinRailAxes != null)
            {
                _mainViewModel.Schematic.DinRailAxes.AddRange(result.DinRailAxes);
            }

            WeakReferenceMessenger.Default.Send(new DinRailRefreshMessage());
            _mainViewModel.RecalculateModuleNumbers();
            _mainViewModel.RecalculateValidation();

            _recentProjectsService.AddRecentProject(path);
            RefreshHomeScreenData();
            _mainViewModel.IsHomeScreenVisible = false;
            _mainViewModel.MarkProjectAsSaved();
            _mainViewModel.RefreshStatusBarProperties();
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
        if (_mainViewModel.CurrentProject == null)
        {
            return;
        }

        if (_mainViewModel.Exporter != null)
        {
            await _mainViewModel.Exporter.ExportPdfAsync().ConfigureAwait(true);
        }

        _mainViewModel.StatusMessage = "Przygotowano dokument do druku (PDF)";
    }

    [RelayCommand]
    public async Task ExitAsync()
    {
        if (!await TryPersistUnsavedChangesAsync(
                "Masz niezapisane zmiany. Czy chcesz je zapisać przed zamknięciem?")
                .ConfigureAwait(true))
        {
            return;
        }

        if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private async Task<bool> TryPersistUnsavedChangesAsync(string confirmationMessage)
    {
        if (_projectService == null || _dialogService == null || !_projectService.HasUnsavedChanges)
        {
            return true;
        }

        var shouldSave = await _dialogService
            .ShowConfirmAsync("Niezapisane zmiany", confirmationMessage)
            .ConfigureAwait(true);
        if (!shouldSave)
        {
            return true;
        }

        await SaveAsync().ConfigureAwait(true);
        return !_projectService.HasUnsavedChanges;
    }

    private static string EnsureProjectFileExtension(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        return string.IsNullOrWhiteSpace(Path.GetExtension(path))
            ? path + ProjectFileExtension
            : path;
    }
}
