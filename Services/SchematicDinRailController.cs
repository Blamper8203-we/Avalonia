using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using DINBoard.Constants;
using DINBoard.Controls;
using DINBoard.Dialogs;
using DINBoard.ViewModels;

namespace DINBoard.Services;

public sealed class SchematicDinRailController
{
    private readonly MainViewModel _viewModel;
    private readonly Window _owner;
    private readonly Border _canvasContainer;
    private readonly DinRailView _dinRailDisplay;
    private readonly Canvas _axisLinesContainer;

    public double DinRailScale { get; private set; } = AppDefaults.DinRailScale;

    public event EventHandler<double>? DinRailScaleChanged;

    public SchematicDinRailController(
        MainViewModel viewModel,
        Window owner,
        Border canvasContainer,
        DinRailView dinRailDisplay,
        Canvas axisLinesContainer)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _canvasContainer = canvasContainer ?? throw new ArgumentNullException(nameof(canvasContainer));
        _dinRailDisplay = dinRailDisplay ?? throw new ArgumentNullException(nameof(dinRailDisplay));
        _axisLinesContainer = axisLinesContainer ?? throw new ArgumentNullException(nameof(axisLinesContainer));
    }

    public async Task<bool> ShowAndGenerateAsync()
    {
        var dialog = new DinRailDialog();
        await dialog.ShowDialog(_owner).ConfigureAwait(true);

        if (!dialog.Confirmed)
            return false;

        int rows = dialog.Rows;
        int modules = dialog.Modules;

        try
        {
            var generator = new DinRailGeneratorProcedural();
            string svg = generator.Generate(rows, modules);
            var (width, height) = generator.GetDimensions(rows, modules);

            double visibleWidth = _canvasContainer.Bounds.Width;
            double visibleHeight = _canvasContainer.Bounds.Height;

            const double margin = 50.0;
            double availableWidth = visibleWidth - (2 * margin);
            double availableHeight = visibleHeight - (2 * margin);

            double scaleX = availableWidth / width;
            double scaleY = availableHeight / height;
            double scale = Math.Min(scaleX, scaleY);

            scale = Math.Min(scale, AppDefaults.DinRailMaxScale);
            DinRailScale = scale;
            DinRailScaleChanged?.Invoke(this, scale);

            double scaledWidth = width * scale;
            double scaledHeight = height * scale;

            _dinRailDisplay.SetRail(svg, scaledWidth, scaledHeight);

            Canvas.SetLeft(_dinRailDisplay, -scaledWidth / 2);
            Canvas.SetTop(_dinRailDisplay, -scaledHeight / 2);

            AppLog.Info($"DIN Rail: {width}x{height} -> scaled: {scaledWidth:F0}x{scaledHeight:F0}, centered at origin (0,0)");
            _viewModel.Schematic.IsDinRailVisible = true;
            _viewModel.Schematic.DinRailSvgContent = svg;
            _viewModel.Schematic.DinRailSize = (scaledWidth, scaledHeight);
            _viewModel.StatusMessage = $"Szyna DIN: {rows}x{modules} ({scaledWidth:F0}x{scaledHeight:F0})";

            _viewModel.Schematic.DinRailAxes.Clear();
            var rawCenters = generator.GetRowCenters(rows);
            foreach (var rawCenter in rawCenters)
            {
                double globalY = (rawCenter * scale) - (scaledHeight / 2);
                _viewModel.Schematic.DinRailAxes.Add(globalY);
            }
            AppLog.Info($"Wygenerowano {_viewModel.Schematic.DinRailAxes.Count} osi przyciągania.");

            DrawDinRailAxes(scaledWidth, scaledHeight, _viewModel.Schematic.DinRailAxes);
            return true;
        }
        catch (ArgumentException ex)
        {
            _viewModel.StatusMessage = $"Błąd: {ex.Message}";
            return false;
        }
        catch (InvalidOperationException ex)
        {
            _viewModel.StatusMessage = $"Błąd: {ex.Message}";
            return false;
        }
    }

    private void DrawDinRailAxes(double railWidth, double railHeight, List<double> horizontalAxes)
    {
        // Clear any existing axis lines
        _axisLinesContainer.Children.Clear();

        // NOTE: Axis lines are no longer drawn visually, but the axes are still calculated
        // and stored in _viewModel.Schematic.DinRailAxes for magnetic snapping functionality.
    }
}
