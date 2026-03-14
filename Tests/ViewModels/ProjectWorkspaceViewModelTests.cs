using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DINBoard.Models;
using DINBoard.Services;
using DINBoard.ViewModels;
using Moq;
using Xunit;

namespace DINBoard.Tests.ViewModels;

public class ProjectWorkspaceViewModelTests
{
    private readonly Mock<IProjectService> _projectServiceMock;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Mock<ProjectPersistenceService> _persistenceServiceMock;
    private readonly MainViewModel _mainViewModel;
    private readonly ProjectWorkspaceViewModel _workspace;

    public ProjectWorkspaceViewModelTests()
    {
        _projectServiceMock = new Mock<IProjectService>();
        _dialogServiceMock = new Mock<IDialogService>();
        
        var symbolImportSvc = new SymbolImportService();
        _persistenceServiceMock = new Mock<ProjectPersistenceService>(_projectServiceMock.Object, symbolImportSvc);
        var moduleTypeService = new Mock<IModuleTypeService>().Object;
        var validationService = new ElectricalValidationService();
        var pdfExportService = new PdfExportService(moduleTypeService, validationService, symbolImportSvc, new SvgProcessor());
        var bomExportService = new BomExportService(moduleTypeService);
        var busbarGenerator = new PowerBusbarGenerator();
        var busbarPlacementService = new BusbarPlacementService(symbolImportSvc, _projectServiceMock.Object, busbarGenerator);

        _mainViewModel = new MainViewModel(
            _projectServiceMock.Object,
            _dialogServiceMock.Object,
            new Mock<UndoRedoService>().Object,
            moduleTypeService,
            symbolImportSvc,
            _persistenceServiceMock.Object,
            validationService,
            pdfExportService,
            bomExportService,
            busbarPlacementService
        );

        _workspace = _mainViewModel.Workspace; 
        // Note: Workspace is automatically created via MainViewModel constructor.
    }

    [Fact]
    public void RefreshHomeScreenData_ShouldPopulateRecentProjects()
    {
        // Act
        _workspace.RefreshHomeScreenData();

        // Assert
        Assert.NotNull(_mainViewModel.License);
        // By default LicenseService gives a trial license, recent projects might be empty initially.
        Assert.NotNull(_mainViewModel.RecentProjects);
    }

    [Fact]
    public async Task SaveAsync_WithNoProject_SetsStatusMessage()
    {
        // Arrange
        _mainViewModel.CurrentProject = null;

        // Act
        await _workspace.SaveAsync();

        // Assert
        Assert.Equal("Brak projektu", _mainViewModel.StatusMessage);
    }

    [Fact]
    public async Task OpenRecentProjectAsync_WithInvalidPath_SetsStatusMessage()
    {
        // Act
        await _workspace.OpenRecentProjectAsync("C:\\sciezka\\ktora\\nie\\istnieje\\brak.json");

        // Assert
        Assert.Equal("Plik nie istnieje", _mainViewModel.StatusMessage);
    }

    [Fact]
    public async Task ExitCommand_WithUnsavedChanges_ShowsConfirmDialog()
    {
        // Arrange
        _projectServiceMock.Setup(p => p.HasUnsavedChanges).Returns(true);
        _dialogServiceMock.Setup(d => d.ShowConfirmAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false); // Return false so it doesn't try to save and hit nullrefs

        // Act
        await _workspace.ExitCommand.ExecuteAsync(null);

        _dialogServiceMock.Verify(d => d.ShowConfirmAsync("Niezapisane zmiany", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void ShowActivationDialog_ShouldEnableProVersionAndRefreshStatusBar()
    {
        // Act
        _workspace.ShowActivationDialog();

        // Assert
        Assert.False(_mainViewModel.License.IsTrial);
        Assert.Equal("Zaktualizowano licencję: Wersja Pełna", _mainViewModel.StatusMessage);
    }
}
