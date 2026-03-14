#nullable enable
using System;
using System.Threading.Tasks;
using DINBoard.Models;
using DINBoard.Services;
using DINBoard.ViewModels;
using Moq;
using Xunit;

namespace DINBoard.Tests.ViewModels
{
    public class ExportViewModelTests
    {
        private readonly Mock<IProjectService> _projectServiceMock;
        private readonly Mock<IDialogService> _dialogServiceMock;
        private readonly Mock<UndoRedoService> _undoRedoMock;
        private readonly Mock<IModuleTypeService> _moduleTypeMock;
        private readonly Mock<ProjectPersistenceService> _persistenceMock;
        private readonly MainViewModel _mainViewModel;
        private readonly ExportViewModel _sut; // System Under Test

        public ExportViewModelTests()
        {
            _projectServiceMock = new Mock<IProjectService>();
            _dialogServiceMock = new Mock<IDialogService>();
            _undoRedoMock = new Mock<UndoRedoService>();
            _moduleTypeMock = new Mock<IModuleTypeService>();
            
            // SymbolImportService uses DI, can be null for this test if not called
            var importSvc = new SymbolImportService();
            _persistenceMock = new Mock<ProjectPersistenceService>(_projectServiceMock.Object, importSvc);
            var validationService = new ElectricalValidationService();
            var pdfExportService = new PdfExportService(_moduleTypeMock.Object, validationService, importSvc, new SvgProcessor());
            var bomExportService = new BomExportService(_moduleTypeMock.Object);
            var busbarGenerator = new PowerBusbarGenerator();
            var busbarPlacementService = new BusbarPlacementService(importSvc, _projectServiceMock.Object, busbarGenerator);

            _mainViewModel = new MainViewModel(
                _projectServiceMock.Object,
                _dialogServiceMock.Object,
                _undoRedoMock.Object,
                _moduleTypeMock.Object,
                importSvc,
                _persistenceMock.Object,
                validationService,
                pdfExportService,
                bomExportService,
                busbarPlacementService);

            _mainViewModel.CurrentProject = new Project { Name = "Test Project" };
            _sut = new ExportViewModel(_mainViewModel, _dialogServiceMock.Object, pdfExportService, bomExportService);
        }

        [Fact]
        public void Constructor_WithNullMainViewModel_ThrowsArgumentNullException()
        {
            // Assert
            var pdfExportService = new PdfExportService(_moduleTypeMock.Object, new ElectricalValidationService(), new SymbolImportService(), new SvgProcessor());
            var bomExportService = new BomExportService(_moduleTypeMock.Object);
            Assert.Throws<ArgumentNullException>(() => new ExportViewModel(null!, _dialogServiceMock.Object, pdfExportService, bomExportService));
        }

        [Fact]
        public void Constructor_WithNullDialogService_ThrowsArgumentNullException()
        {
            // Assert
            var pdfExportService = new PdfExportService(_moduleTypeMock.Object, new ElectricalValidationService(), new SymbolImportService(), new SvgProcessor());
            var bomExportService = new BomExportService(_moduleTypeMock.Object);
            Assert.Throws<ArgumentNullException>(() => new ExportViewModel(_mainViewModel, null!, pdfExportService, bomExportService));
        }

        [Fact]
        public async Task ExportPdfAsync_WithNullProject_DoesNotShowDialog()
        {
            // Arrange
            _mainViewModel.CurrentProject = null;

            // Act
            await _sut.ExportPdfAsync();

            // Assert
            _dialogServiceMock.Verify(d => d.ShowProjectMetadataDialogAsync(It.IsAny<ProjectMetadata>()), Times.Never);
        }

        [Fact]
        public async Task ExportPdfQuickAsync_WithValidProject_ShowsSaveDialog()
        {
            // Arrange
            _dialogServiceMock
                .Setup(d => d.PickSaveFileAsync(It.IsAny<string>(), ".pdf", It.IsAny<string>()))
                .ReturnsAsync((string?)null);

            // Act
            await _sut.ExportPdfQuickAsync();

            // Assert
            _dialogServiceMock.Verify(
                d => d.PickSaveFileAsync(It.IsAny<string>(), ".pdf", It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task ExportPngCleanAsync_WithValidProject_ShowsSaveDialog()
        {
            // Arrange
            _dialogServiceMock
                .Setup(d => d.PickSaveFileAsync(It.IsAny<string>(), ".png", It.IsAny<string>()))
                .ReturnsAsync((string?)null);

            // Act
            await _sut.ExportPngCleanAsync();

            // Assert
            _dialogServiceMock.Verify(
                d => d.PickSaveFileAsync(It.IsAny<string>(), ".png", It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task ExportBomAsync_WithValidProject_ShowsSaveDialog()
        {
            // Arrange
            _dialogServiceMock
                .Setup(d => d.PickSaveFileAsync(It.IsAny<string>(), ".csv", It.IsAny<string>()))
                .ReturnsAsync((string?)null);

            // Act
            await _sut.ExportBomAsync();

            // Assert
            _dialogServiceMock.Verify(
                d => d.PickSaveFileAsync(It.IsAny<string>(), ".csv", It.IsAny<string>()),
                Times.Once);
        }
    }
}
