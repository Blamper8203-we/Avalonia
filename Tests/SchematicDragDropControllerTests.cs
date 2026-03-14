using Avalonia.Controls;
using DINBoard.Services;
using DINBoard.ViewModels;
using Xunit;

namespace Avalonia.Tests;

public class SchematicDragDropControllerTests
{
    [Fact]
    public void Constructor_WithValidArgs_DoesNotThrow()
    {
        var vm = new MainViewModel();
        var container = new Border();
        var zoom = new Canvas();
        var preview = new Border();
        var image = new Image();
        var importService = new SymbolImportService();
        var undoRedo = new UndoRedoService();
        var moduleType = new ModuleTypeService();

        var controller = new SchematicDragDropController(
            vm, container, zoom, preview, image,
            importService, undoRedo, moduleType);

        Assert.NotNull(controller);
    }

    [Fact]
    public void AttachInputHandlers_DoesNotThrow()
    {
        var controller = CreateController();
        controller.AttachInputHandlers();
    }

    [Fact]
    public void DetachInputHandlers_AfterAttach_DoesNotThrow()
    {
        var controller = CreateController();
        controller.AttachInputHandlers();
        controller.DetachInputHandlers();
    }

    private static SchematicDragDropController CreateController()
    {
        var vm = new MainViewModel();
        var container = new Border();
        var zoom = new Canvas();
        var preview = new Border();
        var image = new Image();
        var importService = new SymbolImportService();
        var undoRedo = new UndoRedoService();
        var moduleType = new ModuleTypeService();

        return new SchematicDragDropController(
            vm, container, zoom, preview, image,
            importService, undoRedo, moduleType);
    }
}
