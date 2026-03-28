#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;
using DINBoard.Models;
using DINBoard.Services;
using DINBoard.Services.Pdf;
using DINBoard.ViewModels;
using Moq;
using Xunit;

namespace DINBoard.Tests.ViewModels;

public class ProjectWorkspaceViewModelTests : IDisposable
{
    private readonly Mock<IProjectService> _projectServiceMock;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Mock<ProjectPersistenceService> _persistenceServiceMock;
    private readonly MainViewModel _mainViewModel;
    private readonly ProjectWorkspaceViewModel _workspace;
    private readonly string _licenseFilePath;

    public ProjectWorkspaceViewModelTests()
    {
        _projectServiceMock = new Mock<IProjectService>();
        _projectServiceMock.SetupAllProperties();
        _dialogServiceMock = new Mock<IDialogService>();

        var symbolImportService = new SymbolImportService();
        _persistenceServiceMock = new Mock<ProjectPersistenceService>(_projectServiceMock.Object, symbolImportService);
        var moduleTypeService = new Mock<IModuleTypeService>().Object;
        var validationService = new ElectricalValidationService();
        var pdfExportService = new PdfExportService(moduleTypeService, validationService, symbolImportService, new SvgProcessor());
        var bomExportService = new BomExportService(moduleTypeService);
        var latexExportService = new LatexExportService(moduleTypeService, validationService);
        var busbarGenerator = new PowerBusbarGenerator();
        var busbarPlacementService = new BusbarPlacementService(symbolImportService, _projectServiceMock.Object, busbarGenerator);
        _licenseFilePath = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.license.json");
        var licenseService = new LicenseService(enableLocalActivationShortcut: true, licenseFilePath: _licenseFilePath);
        var recentProjectsService = new RecentProjectsService();

        _mainViewModel = new MainViewModel(new MainViewModelDeps(
            _projectServiceMock.Object,
            _dialogServiceMock.Object,
            new Mock<UndoRedoService>().Object,
            moduleTypeService,
            symbolImportService,
            _persistenceServiceMock.Object,
            validationService,
            pdfExportService,
            bomExportService,
            latexExportService,
            busbarPlacementService,
            licenseService,
            recentProjectsService));

        _workspace = _mainViewModel.Workspace;
    }

    [Fact]
    public void RefreshHomeScreenData_ShouldPopulateRecentProjects()
    {
        _workspace.RefreshHomeScreenData();

        Assert.NotNull(_mainViewModel.License);
        Assert.NotNull(_mainViewModel.RecentProjects);
    }

    [Fact]
    public async Task SaveAsync_WithNoProject_SetsStatusMessage()
    {
        _mainViewModel.CurrentProject = null;

        await _workspace.SaveAsync();

        Assert.Equal("Brak projektu", _mainViewModel.StatusMessage);
    }

    [Fact]
    public async Task OpenRecentProjectAsync_WithInvalidPath_SetsStatusMessage()
    {
        await _workspace.OpenRecentProjectAsync("C:\\sciezka\\ktora\\nie\\istnieje\\brak.json");

        Assert.Equal("Plik nie istnieje", _mainViewModel.StatusMessage);
    }

    [Fact]
    public async Task ExitCommand_WithUnsavedChanges_ShowsConfirmDialog()
    {
        _projectServiceMock.Object.HasUnsavedChanges = true;
        _dialogServiceMock
            .Setup(d => d.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        await _workspace.ExitCommand.ExecuteAsync(null);

        _dialogServiceMock.Verify(d => d.ShowConfirmAsync("Niezapisane zmiany", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void ShowActivationDialog_ShouldEnableProVersionAndRefreshStatusBar()
    {
        Assert.True(_workspace.ShowActivationShortcut);

        _workspace.ShowActivationDialog();

        Assert.False(_mainViewModel.License.IsTrial);
        Assert.False(_workspace.ShowActivationShortcut);
        Assert.Equal("Zaktualizowano licencję: Wersja Pełna", _mainViewModel.StatusMessage);
    }

    [Fact]
    public void ActivateLicense_WhenLocalActivationShortcutDisabled_ShouldRejectBuiltInKey()
    {
        var isolatedLicensePath = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.license.json");

        try
        {
            var licenseService = new LicenseService(enableLocalActivationShortcut: false, licenseFilePath: isolatedLicensePath);

            var activated = licenseService.ActivateLicense("PRO-VERSION-OK");

            Assert.False(activated);
            Assert.False(licenseService.IsLocalActivationShortcutEnabled);
            Assert.True(licenseService.CurrentLicense.IsTrial);
        }
        finally
        {
            if (File.Exists(isolatedLicensePath))
            {
                File.Delete(isolatedLicensePath);
            }
        }
    }

    [Fact]
    public async Task NewProjectInternalAsync_WhenSaveIsCancelled_ShouldAbortProjectReset()
    {
        var originalProject = new Project { Name = "Aktualny projekt", PowerConfig = new PowerSupplyConfig() };
        _mainViewModel.CurrentProject = originalProject;
        _mainViewModel.Symbols.Add(new SymbolItem { Id = "sym-1", Type = "MCB" });
        _mainViewModel.HasUnsavedChanges = true;

        _dialogServiceMock
            .Setup(d => d.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        _dialogServiceMock
            .Setup(d => d.PickSaveFileAsync(It.IsAny<string>(), ".dinboard", It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        var created = await _workspace.NewProjectInternalAsync();

        Assert.False(created);
        Assert.Same(originalProject, _mainViewModel.CurrentProject);
        Assert.Single(_mainViewModel.Symbols);
        Assert.True(_mainViewModel.HasUnsavedChanges);
        Assert.True(_projectServiceMock.Object.HasUnsavedChanges);
        _projectServiceMock.Verify(p => p.CreateNewProject(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExitAsync_WhenSaveIsCancelled_ShouldKeepProjectDirty()
    {
        _mainViewModel.CurrentProject = new Project { Name = "Aktualny projekt", PowerConfig = new PowerSupplyConfig() };
        _mainViewModel.HasUnsavedChanges = true;
        _dialogServiceMock
            .Setup(d => d.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        _dialogServiceMock
            .Setup(d => d.PickSaveFileAsync(It.IsAny<string>(), ".dinboard", It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        await _workspace.ExitAsync();

        Assert.True(_mainViewModel.HasUnsavedChanges);
        Assert.True(_projectServiceMock.Object.HasUnsavedChanges);
    }

    public void Dispose()
    {
        if (File.Exists(_licenseFilePath))
        {
            File.Delete(_licenseFilePath);
        }
    }
}
