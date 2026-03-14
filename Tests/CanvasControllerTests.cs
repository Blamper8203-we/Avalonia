using Avalonia.Controls;
using Avalonia;
using Xunit;
using DINBoard.Services;
using DINBoard.ViewModels;

namespace Avalonia.Tests
{
    public class SchematicCanvasControllerTests
    {
        private SchematicCanvasController CreateController()
        {
            var viewModel = new MainViewModel(); // uses default ctor
            var canvasContainer = new Border();
            var zoomContainer = new Canvas();
            var selectionRectangle = new Border();
            // viewport cursor marker not needed for these tests
            var controller = new SchematicCanvasController(viewModel, canvasContainer, zoomContainer, selectionRectangle, null);
            controller.InitializeCanvasTransform();
            return controller;
        }

        private static double GetCurrentZoom(SchematicCanvasController controller)
        {
            return controller.CurrentZoom;
        }

        [Fact]
        public void ZoomIn_ShouldIncreaseScale()
        {
            var ctrl = CreateController();
            double before = GetCurrentZoom(ctrl);
            ctrl.ZoomIn();
            double after = GetCurrentZoom(ctrl);
            Assert.True(after > before, "ZoomIn should increase the current zoom factor.");
        }

        [Fact]
        public void ZoomFit_ShouldResetScaleToOne()
        {
            var ctrl = CreateController();
            // Apply a zoom first
            ctrl.ZoomIn();
            Assert.NotEqual(1.0, GetCurrentZoom(ctrl));
            // Now reset
            ctrl.ZoomFit();
            double after = GetCurrentZoom(ctrl);
            Assert.Equal(1.0, after);
        }
    }
}
