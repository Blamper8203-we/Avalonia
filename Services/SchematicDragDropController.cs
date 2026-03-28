using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using DINBoard.Constants;
using DINBoard.Models;
using DINBoard.ViewModels;

namespace DINBoard.Services;

public sealed class SchematicDragDropController
{
    private readonly MainViewModel _viewModel;
    private readonly Border _canvasContainer;
    private readonly Canvas _zoomContainer;
    private readonly Border _dragPreviewBorder;
    private readonly Image _dragPreviewImage;

    private readonly SymbolImportService _importService;
    private readonly UndoRedoService _undoRedoService;
    private readonly IModuleTypeService _moduleTypeService;
    private readonly IDialogService? _dialogService;
    private readonly SchematicSnapService _snapService = new();
    private bool _handlersAttached;

    public double DinRailScale { get; set; } = AppDefaults.DinRailScale;

    public SchematicDragDropController(
        MainViewModel viewModel,
        Border canvasContainer,
        Canvas zoomContainer,
        Border dragPreviewBorder,
        Image dragPreviewImage,
        SymbolImportService importService,
        UndoRedoService undoRedoService,
        IModuleTypeService moduleTypeService,
        IDialogService? dialogService = null)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _canvasContainer = canvasContainer ?? throw new ArgumentNullException(nameof(canvasContainer));
        _zoomContainer = zoomContainer ?? throw new ArgumentNullException(nameof(zoomContainer));
        _dragPreviewBorder = dragPreviewBorder ?? throw new ArgumentNullException(nameof(dragPreviewBorder));
        _dragPreviewImage = dragPreviewImage ?? throw new ArgumentNullException(nameof(dragPreviewImage));
        _importService = importService ?? throw new ArgumentNullException(nameof(importService));
        _undoRedoService = undoRedoService ?? throw new ArgumentNullException(nameof(undoRedoService));
        _moduleTypeService = moduleTypeService ?? throw new ArgumentNullException(nameof(moduleTypeService));
        _dialogService = dialogService;
    }

    public void AttachInputHandlers()
    {
        if (_handlersAttached)
        {
            return;
        }

        _canvasContainer.AddHandler(DragDrop.DragEnterEvent, OnCanvasDragEnter);
        _canvasContainer.AddHandler(DragDrop.DragOverEvent, OnCanvasDragOver);
        _canvasContainer.AddHandler(DragDrop.DragLeaveEvent, OnCanvasDragLeave);
        _canvasContainer.AddHandler(DragDrop.DropEvent, OnCanvasDrop);

        _handlersAttached = true;
    }

    public void DetachInputHandlers()
    {
        if (!_handlersAttached)
        {
            return;
        }

        _canvasContainer.RemoveHandler(DragDrop.DragEnterEvent, OnCanvasDragEnter);
        _canvasContainer.RemoveHandler(DragDrop.DragOverEvent, OnCanvasDragOver);
        _canvasContainer.RemoveHandler(DragDrop.DragLeaveEvent, OnCanvasDragLeave);
        _canvasContainer.RemoveHandler(DragDrop.DropEvent, OnCanvasDrop);

        _handlersAttached = false;
    }

    private void OnCanvasDragEnter(object? sender, DragEventArgs e)
    {
        HandleDragEnter(e);
    }

    private void OnCanvasDragOver(object? sender, DragEventArgs e)
    {
        HandleDragOver(e);
    }

    private void OnCanvasDragLeave(object? sender, DragEventArgs e)
    {
        HandleDragLeave();
    }

    private async void OnCanvasDrop(object? sender, DragEventArgs e)
    {
        await HandleDropAsync(e);
    }

    /// <summary>
    /// Sprawdza czy symbol może być nadrzędnym elementem grupy (RCD lub FR).
    /// </summary>
    private bool IsGroupHeadSymbol(SymbolItem? symbol) => _moduleTypeService.IsRcd(symbol) || _moduleTypeService.IsSwitch(symbol);

    private bool IsRcdSymbol(SymbolItem? symbol) => _moduleTypeService.IsRcd(symbol);

    private int GetNextGroupOrder()
    {
        var maxOrder = _viewModel.CurrentProject?.Groups
            ?.Where(g => g.Order > 0)
            .Select(g => g.Order)
            .DefaultIfEmpty(0)
            .Max() ?? 0;

        return maxOrder + 1;
    }

    private void RegisterProjectGroup(string groupId, string groupName, int order)
    {
        if (_viewModel.CurrentProject == null) return;

        _viewModel.CurrentProject.Groups.RemoveAll(g => g.Id == groupId);
        _viewModel.CurrentProject.Groups.Add(new CircuitGroup
        {
            Id = groupId,
            Name = groupName,
            Order = order
        });

        _viewModel.CurrentProject.Groups = _viewModel.CurrentProject.Groups
            .OrderBy(g => g.Order > 0 ? g.Order : int.MaxValue)
            .ThenBy(g => g.Name)
            .ToList();
    }

    public void HandleDragEnter(DragEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (!e.DataTransfer.Contains(DragDropFormats.ModuleFilePath))
            return;

        var moduleFilePath = e.DataTransfer.TryGetValue(DragDropFormats.ModuleFilePath);
        // Pobierz typ i nazwę, aby poprawnie zwymiarować podgląd specyficznych modułów (np. Listwy)
        var moduleType = e.DataTransfer.TryGetValue(DragDropFormats.ModuleType);
        var moduleName = e.DataTransfer.TryGetValue(DragDropFormats.ModuleName);

        if (string.IsNullOrEmpty(moduleFilePath))
            return;

        try
        {
            string ext = Path.GetExtension(moduleFilePath);

            if (ext.Equals(".svg", StringComparison.OrdinalIgnoreCase))
            {
                var (image, width, height) = _importService.CreateSvgPreview(moduleFilePath);
                if (image != null)
                {
                    _dragPreviewImage.Source = image;
                    _dragPreviewImage.Width = width * DinRailScale;
                    _dragPreviewImage.Height = height * DinRailScale;
                }
            }
            else if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase) || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                using var fs = File.OpenRead(moduleFilePath);
                var bitmap = new global::Avalonia.Media.Imaging.Bitmap(fs);
                _dragPreviewImage.Source = bitmap;
                _dragPreviewImage.Width = bitmap.Size.Width * DinRailScale;
                _dragPreviewImage.Height = bitmap.Size.Height * DinRailScale;
            }

            // Listwy: NIE nadpisuj wymiarów - SVG ma właściwe proporcje
            // if (moduleType == "Listwy")
            // {
            //     var dims = CalculateTerminalDimensions(moduleName, _dragPreviewImage.Width, _dragPreviewImage.Height);
            //     _dragPreviewImage.Width = dims.Width;
            //     _dragPreviewImage.Height = dims.Height;
            // }

            _dragPreviewBorder.IsVisible = true;
        }
        catch (IOException ex)
        {
            AppLog.Warn($"Błąd tworzenia podglądu: {moduleFilePath}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            AppLog.Warn($"Błąd tworzenia podglądu: {moduleFilePath}", ex);
        }
        catch (System.Security.SecurityException ex)
        {
            AppLog.Warn($"Błąd tworzenia podglądu: {moduleFilePath}", ex);
        }
        catch (InvalidOperationException ex)
        {
            AppLog.Warn($"Błąd tworzenia podglądu: {moduleFilePath}", ex);
        }
    }

    public void HandleDragOver(DragEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.DataTransfer.Contains(DragDropFormats.ModuleType))
            e.DragEffects = DragDropEffects.Copy;
        else
            e.DragEffects = DragDropEffects.None;

        if (!_dragPreviewBorder.IsVisible)
            return;

        var pos = e.GetPosition(_zoomContainer);
        var moduleType = e.DataTransfer.TryGetValue(DragDropFormats.ModuleType);
        var moduleName = e.DataTransfer.TryGetValue(DragDropFormats.ModuleName);

        // Calculate snap
        var snapResult = _snapService.CalculateSnap(
            pos,
            _dragPreviewBorder.Bounds.Width,
            _dragPreviewBorder.Bounds.Height,
            _viewModel.Symbols,
            _viewModel.Schematic.DinRailAxes,
            DinRailScale,
            null,
            null,
            moduleType,
            moduleName);

        Canvas.SetLeft(_dragPreviewBorder, snapResult.SnappedX);
        Canvas.SetTop(_dragPreviewBorder, snapResult.SnappedY);

        // Visual feedback? (Optional: could change border color if snapped)
        if (snapResult.IsSnapped)
            _dragPreviewBorder.Opacity = 1.0;
        else
            _dragPreviewBorder.Opacity = 0.65;
    }

    public void HandleDragLeave()
    {
        _dragPreviewBorder.IsVisible = false;
    }

    public void HandleDrop(DragEventArgs e)
    {
        HandleDropAsync(e).GetAwaiter().GetResult();
    }

    public async Task HandleDropAsync(DragEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        _dragPreviewBorder.IsVisible = false;

        if (!e.DataTransfer.Contains(DragDropFormats.ModuleType))
            return;

        var moduleType = e.DataTransfer.TryGetValue(DragDropFormats.ModuleType);
        var moduleName = e.DataTransfer.TryGetValue(DragDropFormats.ModuleName);
        var moduleFilePath = e.DataTransfer.TryGetValue(DragDropFormats.ModuleFilePath);

        if (string.IsNullOrEmpty(moduleFilePath))
            return;

        try
        {
            var dropPos = e.GetPosition(_zoomContainer);

            var newSymbol = _importService.ImportFromFile(moduleFilePath, moduleType, moduleName);

            if (newSymbol == null)
            {
                _viewModel.StatusMessage = $"Błąd importu: {moduleName}";
                return;
            }

            newSymbol.Width *= DinRailScale;
            newSymbol.Height *= DinRailScale;

            // === AUTOMATYCZNE WYKRYWANIE FAZY NA PODSTAWIE LICZBY BIEGUNÓW ===
            var poleCount = _moduleTypeService.GetPoleCount(newSymbol);
            if (poleCount != ModulePoleCount.Unknown)
            {
                newSymbol.Phase = _moduleTypeService.GetDefaultPhaseForPoleCount(poleCount);
                AppLog.Info($"Auto-faza: {moduleName} -> {poleCount} -> {newSymbol.Phase}");
            }

            // --- Listwy Zaciskowe: NIE NADPISUJ wymiarów ---
            // Listwa ma naturalne proporcje w SVG (10.5mm x 90mm dla 1 zacisku).
            // Pozwól SVG zachować swoje proporcje, przeskalowane tylko przez DinRailScale.
            if (moduleType == "Listwy")
            {
                // Tylko logowanie - nie zmieniaj wymiarów
                AppLog.Info($"Listwy: {moduleName} -> zachowano wymiary SVG: {newSymbol.Width:F1}x{newSymbol.Height:F1}");
            }

            // Recalculate snap to get final position and target
            var snapResult = _snapService.CalculateSnap(
                dropPos,
                newSymbol.Width,
                newSymbol.Height,
                _viewModel.Symbols,
                _viewModel.Schematic.DinRailAxes,
                DinRailScale,
                null,
                newSymbol,
                newSymbol.Type,
                newSymbol.Label);

            newSymbol.X = snapResult.SnappedX;
            newSymbol.Y = snapResult.SnappedY;
            newSymbol.IsSnappedToRail = snapResult.IsSnapped;

            // Auto-Grouping Logic
            // --- Wyklucz moduły które nie powinny być grupowane ---
            bool isTerminalBlock = moduleType == "Listwy";
            
            bool excludeFromGrouping = _moduleTypeService.IsSwitch(newSymbol) ||
                                       _moduleTypeService.IsSpd(newSymbol) ||
                                       _moduleTypeService.IsPhaseIndicator(newSymbol);

            // Listwy - wykluczamy CHYBA ŻE są po prawej stronie RCD
            if (isTerminalBlock && snapResult.SnapTarget != null)
            {
                bool isRightOfTarget = newSymbol.X > snapResult.SnapTarget.X;
                bool targetIsGroupHead = IsGroupHeadSymbol(snapResult.SnapTarget);

                // Pozwól na grupowanie tylko gdy listwa jest po prawej stronie głowy grupy
                excludeFromGrouping = !(isRightOfTarget && targetIsGroupHead);
            }
            else if (isTerminalBlock)
            {
                excludeFromGrouping = true; // Brak snap target - wyklucz
            }

            if (snapResult.SnapTarget != null && !excludeFromGrouping)
            {
                if (!string.IsNullOrEmpty(snapResult.SnapTarget.Group))
                {
                    // Dołącz do istniejącej grupy
                    newSymbol.Group = snapResult.SnapTarget.Group;
                    newSymbol.GroupName = snapResult.SnapTarget.GroupName;

                    // Kopiuj informacje RCD jeśli snap target ma RCD lub jest RCD
                    if (!string.IsNullOrEmpty(snapResult.SnapTarget.RcdSymbolId))
                    {
                        newSymbol.RcdSymbolId = snapResult.SnapTarget.RcdSymbolId;
                        newSymbol.RcdRatedCurrent = snapResult.SnapTarget.RcdRatedCurrent;
                        newSymbol.RcdResidualCurrent = snapResult.SnapTarget.RcdResidualCurrent;
                        newSymbol.RcdType = snapResult.SnapTarget.RcdType;
                    }
                    else if (IsRcdSymbol(snapResult.SnapTarget))
                    {
                        // Snap target jest RCD - ustaw bezpośrednio
                        newSymbol.RcdSymbolId = snapResult.SnapTarget.Id;
                        newSymbol.RcdRatedCurrent = snapResult.SnapTarget.RcdRatedCurrent;
                        newSymbol.RcdResidualCurrent = snapResult.SnapTarget.RcdResidualCurrent;
                        newSymbol.RcdType = snapResult.SnapTarget.RcdType;
                    }

                    _viewModel.StatusMessage = $"Dodano: {moduleName} (Dołączono do grupy)";
                }
                else
                {
                    // Snap target nie ma grupy - sprawdź czy można utworzyć nową grupę
                    bool targetIsGroupHead = IsGroupHeadSymbol(snapResult.SnapTarget);
                    bool newIsGroupHead = IsGroupHeadSymbol(newSymbol);

                    // Utwórz grupę jeśli jeden jest RCD/FR a drugi MCB
                    if (targetIsGroupHead || newIsGroupHead)
                    {
                        string newGroupId = Guid.NewGuid().ToString();

                        int nextGroupOrder = GetNextGroupOrder();
                        string groupName = $"Grupa-{nextGroupOrder}";
                        RegisterProjectGroup(newGroupId, groupName, nextGroupOrder);

                        // Ustaw grupę na obu symbolach
                        snapResult.SnapTarget.Group = newGroupId;
                        snapResult.SnapTarget.GroupName = groupName;
                        newSymbol.Group = newGroupId;
                        newSymbol.GroupName = groupName;

                        // Ustaw RCD info na MCB (tylko jeżeli głowa grupy to RCD)
                        bool targetIsRcdType = IsRcdSymbol(snapResult.SnapTarget);
                        bool newIsRcdType = IsRcdSymbol(newSymbol);

                        if (targetIsRcdType || newIsRcdType)
                        {
                            var rcdSymbol = targetIsRcdType ? snapResult.SnapTarget : newSymbol;
                            var mcbSymbol = targetIsRcdType ? newSymbol : snapResult.SnapTarget;

                            mcbSymbol.RcdSymbolId = rcdSymbol.Id;
                            mcbSymbol.RcdRatedCurrent = rcdSymbol.RcdRatedCurrent;
                            mcbSymbol.RcdResidualCurrent = rcdSymbol.RcdResidualCurrent;
                            mcbSymbol.RcdType = rcdSymbol.RcdType;
                        }

                        _viewModel.StatusMessage = $"Dodano: {moduleName} (Utworzono nową grupę: {groupName})";
                        AppLog.Debug($"Utworzono nową grupę {newGroupId} ({groupName}) z modułem nadrzędnym i dystrybucyjnymi");
                    }
                    else
                    {
                        _viewModel.StatusMessage = $"Dodano: {moduleName} (Przyciągnięto)";
                    }
                }
            }
            else
            {
                // Snap target nie ma grupy lub brak snap targetu.

                // --- NOWA LOGIKA: Automatyczna grupa dla KAŻDEGO nowego RCD / FR ---
                // Jeśli dodajemy głowę grupy i nie dołączył on do żadnej grupy, stwórz mu nową od razu.
                bool isNewGroupHead = IsGroupHeadSymbol(newSymbol);

                if (isNewGroupHead && string.IsNullOrEmpty(newSymbol.Group))
                {
                    string newGroupId = Guid.NewGuid().ToString();

                    int nextGroupOrder = GetNextGroupOrder();
                    string groupName = $"Grupa-{nextGroupOrder}";
                    RegisterProjectGroup(newGroupId, groupName, nextGroupOrder);

                    newSymbol.Group = newGroupId;
                    newSymbol.GroupName = groupName;

                    // Jeśli RCD ma jakieś info, ustawiamy je (choć to jego własne)
                    // W tym momencie ma GroupId, więc reszta logiki go obsłuży.

                    _viewModel.StatusMessage = $"Dodano: {moduleName} (Aparat Grupowy - Nowa grupa: {groupName})";
                }
                else
                {
                    _viewModel.StatusMessage = $"Dodano: {moduleName} (skala: {DinRailScale:P0})";
                }
            }

            // _viewModel.Symbols.Add(newSymbol);
            _undoRedoService.Execute(new DINBoard.Services.AddSymbolCommand(_viewModel.Symbols, newSymbol));
            _viewModel.RecalculateModuleNumbers();
            await PromptInductionOvenGroupScenarioAsync(newSymbol);
        }
        catch (UnauthorizedAccessException ex)
        {
            AppLog.Error($"Błąd drop symbolu: {moduleFilePath}", ex);
            _viewModel.StatusMessage = $"Błąd: {ex.Message}";
        }
        catch (System.Security.SecurityException ex)
        {
            AppLog.Error($"Błąd drop symbolu: {moduleFilePath}", ex);
            _viewModel.StatusMessage = $"Błąd: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            AppLog.Error($"Błąd drop symbolu: {moduleFilePath}", ex);
            _viewModel.StatusMessage = $"Błąd: {ex.Message}";
        }
        catch (ArgumentException ex)
        {
            AppLog.Error($"Błąd drop symbolu: {moduleFilePath}", ex);
            _viewModel.StatusMessage = $"Błąd: {ex.Message}";
        }
    }

    private async Task PromptInductionOvenGroupScenarioAsync(SymbolItem newSymbol)
    {
        if (_dialogService == null || string.IsNullOrWhiteSpace(newSymbol.Group))
        {
            return;
        }

        var groupSymbols = _viewModel.Symbols
            .Where(symbol => string.Equals(symbol.Group, newSymbol.Group, StringComparison.Ordinal))
            .ToList();

        if (groupSymbols.Count < 2)
        {
            return;
        }

        var rcd4PHead = groupSymbols.FirstOrDefault(symbol =>
            IsRcdSymbol(symbol) && _moduleTypeService.GetPoleCount(symbol) == ModulePoleCount.P4);
        if (rcd4PHead == null)
        {
            return;
        }

        if (TryGetScenarioFlag(rcd4PHead, GroupScenarioConstants.InductionWithOvenPrompted))
        {
            return;
        }

        var pattern = DetectInductionOvenPattern(groupSymbols);
        if (pattern == InductionOvenGroupPattern.None)
        {
            return;
        }

        string message = pattern switch
        {
            InductionOvenGroupPattern.Rcd4PWithMcb2PAnd1P =>
                "Wykryto grupę: RCD 4P + MCB 2P + MCB 1P.\nCzy to układ indukcja (2F) + piekarnik (1F)?\nPo potwierdzeniu pojawi się kalkulator doboru zabezpieczeń.",
            InductionOvenGroupPattern.Rcd4PWithMcb3P =>
                "Wykryto grupę: RCD 4P + MCB 3P.\nCzy to układ indukcja (L1+L2) + piekarnik (L3)?\nPo potwierdzeniu pojawi się kalkulator doboru zabezpieczeń.",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        bool accepted = await _dialogService.ShowConfirmAsync("Indukcja + piekarnik", message);

        foreach (var symbol in groupSymbols)
        {
            symbol.Parameters[GroupScenarioConstants.InductionWithOvenPrompted] = "true";
            symbol.Parameters[GroupScenarioConstants.InductionWithOvenEnabled] = accepted ? "true" : "false";
            symbol.Parameters[GroupScenarioConstants.InductionWithOvenPattern] = pattern.ToString();
        }

        _viewModel.MarkProjectAsChanged();

        if (accepted)
        {
            _viewModel.StatusMessage = "Włączono kalkulator: indukcja + piekarnik";
        }
    }

    private InductionOvenGroupPattern DetectInductionOvenPattern(IReadOnlyList<SymbolItem> groupSymbols)
    {
        var mcbs = groupSymbols.Where(symbol => _moduleTypeService.IsMcb(symbol)).ToList();
        if (mcbs.Count == 0)
        {
            return InductionOvenGroupPattern.None;
        }

        bool hasMcb3P = mcbs.Any(symbol => _moduleTypeService.GetPoleCount(symbol) == ModulePoleCount.P3);
        if (hasMcb3P)
        {
            return InductionOvenGroupPattern.Rcd4PWithMcb3P;
        }

        bool hasMcb2P = mcbs.Any(symbol => _moduleTypeService.GetPoleCount(symbol) == ModulePoleCount.P2);
        bool hasMcb1P = mcbs.Any(symbol => _moduleTypeService.GetPoleCount(symbol) == ModulePoleCount.P1);
        if (hasMcb2P && hasMcb1P)
        {
            return InductionOvenGroupPattern.Rcd4PWithMcb2PAnd1P;
        }

        return InductionOvenGroupPattern.None;
    }

    private static bool TryGetScenarioFlag(SymbolItem symbol, string key)
    {
        if (!symbol.Parameters.TryGetValue(key, out var raw))
        {
            return false;
        }

        return bool.TryParse(raw, out var parsed) && parsed;
    }

    private enum InductionOvenGroupPattern
    {
        None = 0,
        Rcd4PWithMcb2PAnd1P = 1,
        Rcd4PWithMcb3P = 2
    }

    private static (double Width, double Height) CalculateTerminalDimensions(string? moduleName, double currentWidth, double currentHeight)
    {
        int pinCount = 12; // Domyślnie
        bool pinsDetected = false;

        if (!string.IsNullOrEmpty(moduleName))
        {
            var match = System.Text.RegularExpressions.Regex.Match(moduleName, @"(\d+)\s*(?:pin|pol|tor|zacisk)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (int.TryParse(match.Groups[1].Value, out int pins))
                {
                    pinCount = pins;
                    pinsDetected = true;
                }
            }
            else
            {
                var numMatch = System.Text.RegularExpressions.Regex.Match(moduleName, @"(\d+)");
                if (numMatch.Success)
                {
                    int val = int.Parse(numMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                    if (val > 1 && val <= 50)
                    {
                        pinCount = val;
                        pinsDetected = true;
                    }
                }
            }
        }

        if (!pinsDetected && currentHeight > 0)
        {
            double ratio = currentWidth / currentHeight;
            if (ratio < 0.3)
            {
                pinCount = 1;
                AppLog.Info($"Auto-resize Listwy: Wykryto pojedynczy zacisk po proporcjach ({ratio:F2})");
            }
        }

        double finalWidth = pinCount * 10.5;
        double finalHeight = 90.0;

        return (finalWidth, finalHeight);
    }
}
