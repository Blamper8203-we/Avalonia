using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using DINBoard.Controls;
using DINBoard.Models;
using E = DINBoard.Services.SchematicLayoutEngine;

namespace DINBoard.Services;

/// <summary>
/// Obsługuje edycję inline komórek tabeli w schemacie jednokreskowym.
/// </summary>
public sealed class SchematicCellEditController
{
    private readonly Canvas _interactiveCanvas;
    private readonly SkiaRenderControl? _skiaCanvas;
    private readonly IModuleTypeService _moduleTypeService;
    private readonly Action<SymbolItem> _onCommitEdit;

    private TextBox? _editBox;
    private SchematicNode? _editNode;
    private string _editField = "";

    private static readonly (double YOff, string Field)[] EditableRows =
    {
        (E.YRowDesig, "Designation"),
        (E.YRowProt, "Protection"),
        (E.YRowCircuit, "CircuitName"),
        (E.YRowLocation, "Location"),
        (E.YRowCable, "CableDesig"),
        (E.YRowCableType, "CableType"),
        (E.YRowCableSpec, "CableSpec"),
        (E.YRowCableLen, "CableLength"),
        (E.YRowPower, "PowerInfo"),
    };

    public SchematicCellEditController(
        Canvas interactiveCanvas,
        SkiaRenderControl? skiaCanvas,
        IModuleTypeService moduleTypeService,
        Action<SymbolItem> onCommitEdit)
    {
        _interactiveCanvas = interactiveCanvas;
        _skiaCanvas = skiaCanvas;
        _moduleTypeService = moduleTypeService;
        _onCommitEdit = onCommitEdit;
    }

    /// <summary>Sprawdza, czy kliknięcie trafiło w edytowalną komórkę.</summary>
    public CellHitInfo? FindCellAt(SchematicLayout? layout, Avalonia.Point pos)
    {
        if (layout == null) return null;

        var allLeaves = new List<SchematicNode>();
        foreach (var d in layout.Devices)
        {
            if (d.Children.Count > 0)
            {
                foreach (var ch in d.Children) allLeaves.Add(ch);
            }
            else
            {
                allLeaves.Add(d);
            }
        }

        foreach (var n in allLeaves)
        {
            double cw = n.CellWidth - 6;
            double cx = n.X + E.NW / 2;
            double cellL = cx - cw / 2;

            foreach (var (yOff, field) in EditableRows)
            {
                var pi = layout.Pages.FirstOrDefault(p => p.PageIndex == n.Page);
                if (pi == null) continue;

                double cellY = Y(pi.YOffset, yOff) - 1;
                var cellRect = new Rect(cellL, cellY, cw, E.RowH);

                if (cellRect.Contains(pos))
                    return new CellHitInfo(n, field, cellRect);
            }
        }
        return null;
    }

    private static double Y(double yOff, double relY) => yOff + E.DrawT + relY;

    public void StartCellEdit(SchematicNode node, string field, Rect cellRect)
    {
        CommitEdit();
        if (node.Symbol == null) return;

        _editNode = node;
        _editField = field;
        string currentValue = GetFieldValue(node, field);

        _editBox = new TextBox
        {
            Text = currentValue,
            FontSize = E.CellFontSize,
            Padding = new Thickness(1, 0),
            MinHeight = 0,
            Width = cellRect.Width,
            Height = cellRect.Height,
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.CornflowerBlue,
            Background = Brushes.White,
            Foreground = Brushes.Black,
            CaretBrush = Brushes.Black,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        };
        ToolTip.SetIsOpen(_editBox, false);
        ToolTip.SetTip(_editBox, null);

        Canvas.SetLeft(_editBox, cellRect.X);
        Canvas.SetTop(_editBox, cellRect.Y);

        if (_skiaCanvas != null)
        {
            _skiaCanvas.EditingCellRect = new SkiaSharp.SKRect(
                (float)(cellRect.X + 1), (float)(cellRect.Y + 1),
                (float)(cellRect.X + cellRect.Width - 1), (float)(cellRect.Y + cellRect.Height - 1));
        }

        _interactiveCanvas.Children.Add(_editBox);

        _editBox.KeyDown += EditBox_KeyDown;
        _editBox.LostFocus += EditBox_LostFocus;

        _editBox.AttachedToVisualTree += (_, _) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                _editBox?.Focus();
                _editBox?.SelectAll();
            }, DispatcherPriority.Input);
        };
    }

    private void EditBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { CommitEdit(); e.Handled = true; }
        else if (e.Key == Key.Escape) { CancelEdit(); e.Handled = true; }
        else if (e.Key == Key.Tab) { CommitEdit(); e.Handled = true; }
    }

    private void EditBox_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => CommitEdit();

    public void CommitEdit()
    {
        if (_editBox == null || _editNode == null) return;

        string newValue = _editBox.Text ?? "";
        SetFieldValue(_editNode, _editField, newValue);

        var sym = _editNode.Symbol;
        if (sym != null)
        {
            SyncSymbolParameters(sym);
            _onCommitEdit(sym);
        }

        RemoveEditBox();
    }

    private void SyncSymbolParameters(SymbolItem sym)
    {
        if (sym.Parameters == null) return;

        var modType = _moduleTypeService.GetModuleType(sym);
        string label = modType == ModuleType.Switch ? sym.FrType ?? sym.Label ?? ""
            : modType == ModuleType.PhaseIndicator ? sym.PhaseIndicatorModel ?? sym.Label ?? ""
            : sym.ProtectionType ?? sym.Label ?? "";
        sym.Parameters["LABEL"] = label;

        string protect = sym.ProtectionType ?? "";
        if (sym.Type?.Contains("RCD", StringComparison.OrdinalIgnoreCase) == true)
            protect = $"{sym.RcdRatedCurrent}A";
        sym.Parameters["CURRENT"] = protect;
        sym.Parameters["POWER"] = sym.PowerW.ToString();
    }

    public void CancelEdit() => RemoveEditBox();

    public void RemoveEditBox()
    {
        if (_editBox == null) return;

        _editBox.KeyDown -= EditBox_KeyDown;
        _editBox.LostFocus -= EditBox_LostFocus;
        _interactiveCanvas.Children.Remove(_editBox);

        if (_skiaCanvas != null)
            _skiaCanvas.EditingCellRect = null;

        _editBox = null;
        _editNode = null;
        _editField = "";
    }

    public bool IsEditing => _editBox != null;

    private static string GetFieldValue(SchematicNode n, string field) => field switch
    {
        "Designation" => n.Symbol?.ReferenceDesignation ?? n.Designation ?? "",
        "Protection" => n.Symbol?.ProtectionType ?? n.Protection ?? "",
        "CircuitName" => n.Symbol?.CircuitName ?? n.CircuitName ?? "",
        "Location" => n.Symbol?.Location ?? n.Location ?? "",
        "CableDesig" => n.CableDesig ?? "",
        "CableType" => n.CableType ?? "",
        "CableSpec" => n.CableSpec ?? "",
        "CableLength" => n.CableLength ?? "",
        "PowerInfo" => n.PowerInfo ?? "",
        _ => ""
    };

    private static void SetFieldValue(SchematicNode n, string field, string val)
    {
        var sym = n.Symbol;
        if (sym == null) return;

        switch (field)
        {
            case "Designation": sym.ReferenceDesignation = val; n.Designation = val; break;
            case "Protection": sym.ProtectionType = val; n.Protection = val; break;
            case "CircuitName": sym.CircuitName = val; n.CircuitName = val; break;
            case "Location": sym.Location = val; n.Location = val; break;
            case "CableDesig":
            case "CableType":
            case "CableSpec":
            case "CableLength":
            case "PowerInfo":
                sym.Parameters ??= new Dictionary<string, string>();
                sym.Parameters[field] = val;
                if (field == "CableDesig") n.CableDesig = val;
                else if (field == "CableType") n.CableType = val;
                else if (field == "CableSpec") n.CableSpec = val;
                else if (field == "CableLength") n.CableLength = val;
                else if (field == "PowerInfo") n.PowerInfo = val;
                break;
        }
    }
}

/// <summary>Informacja o kliknięciu w komórkę tabeli.</summary>
public readonly record struct CellHitInfo(SchematicNode Node, string Field, Rect CellRect);
