using DINBoard.Services;
using DINBoard.Services.Pdf;

namespace DINBoard.ViewModels;

/// <summary>
/// Grupuje wszystkie zależności konstruktora runtime MainViewModel.
/// Eliminuje constructor z 13 parametrami — zastąpiony pojedynczym rekordem.
/// </summary>
public sealed record MainViewModelDeps(
    IProjectService ProjectService,
    IDialogService DialogService,
    UndoRedoService UndoRedoService,
    IModuleTypeService ModuleTypeService,
    SymbolImportService SymbolImportService,
    ProjectPersistenceService PersistenceService,
    IElectricalValidationService ElectricalValidationService,
    PdfExportService PdfExportService,
    BomExportService BomExportService,
    LatexExportService LatexExportService,
    BusbarPlacementService BusbarPlacementService,
    LicenseService LicenseService,
    RecentProjectsService RecentProjectsService
);
