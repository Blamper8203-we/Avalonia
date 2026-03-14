using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using DINBoard.Constants;
using DINBoard.Models;

namespace DINBoard.Services;

public readonly struct SnapResult
{
    public SnapResult(SymbolItem? snapTarget, double snappedX, double snappedY, bool isRailSnapped, bool isHorizontalSnapped)
    {
        SnapTarget = snapTarget;
        SnappedX = snappedX;
        SnappedY = snappedY;
        IsRailSnapped = isRailSnapped;
        IsHorizontalSnapped = isHorizontalSnapped;
        IsSnapped = isRailSnapped || isHorizontalSnapped;
    }

    public SymbolItem? SnapTarget { get; }
    public double SnappedX { get; }
    public double SnappedY { get; }
    public bool IsRailSnapped { get; }
    public bool IsHorizontalSnapped { get; }
    public bool IsSnapped { get; }
}

public sealed class SchematicSnapService
{
    private readonly ModuleTypeService _moduleTypeService = new();

    public SnapResult CalculateSnap(
        Point pointerPos,
        double selfWidth,
        double selfHeight,
        IReadOnlyCollection<SymbolItem> symbols,
        IReadOnlyList<double> dinRailAxes,
        double dinRailScale,
        IEnumerable<SymbolItem>? excludeSymbols = null,
        SymbolItem? selfSymbol = null,
        string? selfType = null,
        string? selfLabel = null)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        ArgumentNullException.ThrowIfNull(dinRailAxes);
        var excludeSet = excludeSymbols != null
            ? new HashSet<SymbolItem>(excludeSymbols)
            : new HashSet<SymbolItem>();

        double finalY = pointerPos.Y - selfHeight / 2.0;
        bool isRailSnapped = false;

        bool isBusbar = IsBusbarSymbol(selfSymbol, selfType, selfLabel);
        var bottomTerminalPoints = new List<Point>();
        double? busbarSnapY = null;

        if (isBusbar && symbols.Count > 0)
        {
            var candidates = symbols.Where(s => !excludeSet.Contains(s) && IsBusbarTargetSymbol(s)).ToList();
            if (candidates.Count > 0)
            {
                var closest = candidates
                    .Select(s => new { Symbol = s, BottomY = s.Y + s.Height })
                    .OrderBy(s => Math.Abs(pointerPos.Y - s.BottomY))
                    .First();

                double targetBottomY = closest.BottomY;
                double minDistance = Math.Abs(pointerPos.Y - targetBottomY);

                double busbarSnapDistance = AppDefaults.DinRailSnapDistance * 0.6;
                if (minDistance < busbarSnapDistance)
                {
                    var aligned = candidates
                        .Where(s => Math.Abs((s.Y + s.Height) - targetBottomY) < busbarSnapDistance)
                        .ToList();

                    foreach (var module in aligned)
                    {
                        bottomTerminalPoints.AddRange(GetBottomTerminalPoints(module));
                    }

                    double busbarGap = aligned.Count > 0
                        ? GetBusbarGap(aligned[0])
                        : 0.0;

                    double scale = selfHeight > 0
                        ? selfHeight / PowerBusbarGenerator.BaseHeight
                        : 0.0;

                    if (scale > 0)
                    {
                        busbarSnapY = targetBottomY + busbarGap - PowerBusbarGenerator.BodyY * scale;
                    }
                }
            }
        }

        if (busbarSnapY.HasValue)
        {
            finalY = busbarSnapY.Value;
            isRailSnapped = true;
        }
        else if (dinRailAxes.Count > 0)
        {
            double moduleCenterY = pointerPos.Y;
            double closestAxis = dinRailAxes[0];
            double minDistance = Math.Abs(moduleCenterY - closestAxis);

            foreach (var axis in dinRailAxes)
            {
                double d = Math.Abs(moduleCenterY - axis);
                if (d < minDistance)
                {
                    minDistance = d;
                    closestAxis = axis;
                }
            }

            if (minDistance < AppDefaults.DinRailSnapDistance)
            {
                finalY = closestAxis - (selfHeight / 2.0);
                isRailSnapped = true;
            }
        }

        double gap = AppDefaults.SnapGapUnit * AppDefaults.ModuleUnitWidth * dinRailScale;
        double bestDistX = AppDefaults.SnapThreshold;
        double snappedX = pointerPos.X - selfWidth / 2.0;
        SymbolItem? snapTarget = null;
        bool isHorizontallySnapped = false;

        double dinRailAxis = isRailSnapped && dinRailAxes.Count > 0
            ? dinRailAxes.OrderBy(a => Math.Abs(pointerPos.Y - a)).First()
            : pointerPos.Y;

        bool IsOnSameRail(SymbolItem s)
        {
            double sCenterY = s.Y + s.Height / 2.0;
            return Math.Abs(sCenterY - dinRailAxis) < AppDefaults.DinRailSameRailTolerance;
        }

        bool IsSpaceFree(double startX, double width)
        {
            double endX = startX + width;
            double tolerance = AppDefaults.SnapOverlapTolerance;
            foreach (var s in symbols)
            {
                if (excludeSet.Contains(s)) continue;
                if (!IsOnSameRail(s)) continue;

                double sEndX = s.X + s.Width;
                if (Math.Max(startX, s.X) < Math.Min(endX, sEndX) - tolerance)
                {
                    return false;
                }
            }
            return true;
        }

        if (!isBusbar)
        {
            foreach (var existing in symbols)
            {
                if (excludeSet.Contains(existing)) continue;
                if (!IsOnSameRail(existing))
                    continue;

                double rightTarget = existing.X + existing.Width + gap;
                double distRight = Math.Abs(snappedX - rightTarget);

                if (distRight < bestDistX)
                {
                    if (IsSpaceFree(rightTarget, selfWidth))
                    {
                        bestDistX = distRight;
                        snappedX = rightTarget;
                        snapTarget = existing;
                        isHorizontallySnapped = true;
                    }
                }

                double leftTarget = existing.X - gap - selfWidth;
                double distLeft = Math.Abs(snappedX - leftTarget);

                if (distLeft < bestDistX)
                {
                    if (IsSpaceFree(leftTarget, selfWidth))
                    {
                        bestDistX = distLeft;
                        snappedX = leftTarget;
                        snapTarget = existing;
                        isHorizontallySnapped = true;
                    }
                }
            }

            if (!isHorizontallySnapped)
            {
                foreach (var existing in symbols)
                {
                    if (excludeSet.Contains(existing)) continue;
                    if (!IsOnSameRail(existing)) continue;

                    double overlapStart = Math.Max(pointerPos.X - selfWidth / 2.0, existing.X);
                    double overlapEnd = Math.Min(pointerPos.X + selfWidth / 2.0, existing.X + existing.Width);

                    if (overlapEnd > overlapStart + 10.0)
                    {
                        double leftEdge = existing.X - gap - selfWidth;
                        double rightEdge = existing.X + existing.Width + gap;

                        bool rightFree = IsSpaceFree(rightEdge, selfWidth);
                        bool leftFree = IsSpaceFree(leftEdge, selfWidth);

                        if (rightFree && !leftFree)
                        {
                            snappedX = rightEdge;
                            snapTarget = existing;
                            isHorizontallySnapped = true;
                            break;
                        }
                        else if (leftFree && !rightFree)
                        {
                            snappedX = leftEdge;
                            snapTarget = existing;
                            isHorizontallySnapped = true;
                            break;
                        }
                        else if (rightFree && leftFree)
                        {
                            double distToRight = Math.Abs(pointerPos.X - rightEdge);
                            double distToLeft = Math.Abs(pointerPos.X - leftEdge);

                            snappedX = distToRight <= distToLeft ? rightEdge : leftEdge;
                            snapTarget = existing;
                            isHorizontallySnapped = true;
                            break;
                        }
                    }
                }
            }
        }

        if (isBusbar && bottomTerminalPoints.Count > 0 && selfHeight > 0)
        {
            int pinCount = GetBusbarPinCount(selfSymbol, selfLabel, selfWidth, selfHeight);
            double scale = selfHeight / PowerBusbarGenerator.BaseHeight;
            if (pinCount > 0 && scale > 0)
            {
                var pinCenters = GetBusbarPinCenters(pinCount, scale);
                double targetTerminalX = bottomTerminalPoints
                    .OrderBy(p => Math.Abs(p.X - pointerPos.X))
                    .First()
                    .X;

                double bestDelta = double.MaxValue;
                foreach (var center in pinCenters)
                {
                    double delta = targetTerminalX - (snappedX + center);
                    if (Math.Abs(delta) < Math.Abs(bestDelta))
                    {
                        bestDelta = delta;
                    }
                }

                double busbarSnapThreshold = AppDefaults.SnapThreshold * 0.6;
                if (!double.IsInfinity(bestDelta) && Math.Abs(bestDelta) <= busbarSnapThreshold)
                {
                    snappedX += bestDelta;
                    isHorizontallySnapped = true;
                }
            }
        }

        return new SnapResult(snapTarget, snappedX, finalY, isRailSnapped, isHorizontallySnapped);
    }

    private bool IsBusbarTargetSymbol(SymbolItem symbol)
    {
        if (symbol == null) return false;
        if (symbol.IsTerminalBlock) return false;
        if (IsKontrolkiFaz(symbol)) return true;
        return _moduleTypeService.IsRcd(symbol)
               || _moduleTypeService.IsMcb(symbol)
               || _moduleTypeService.IsSpd(symbol)
               || _moduleTypeService.IsSwitch(symbol);
    }

    private static bool IsBusbarSymbol(SymbolItem? selfSymbol, string? selfType, string? selfLabel)
    {
        if (selfSymbol?.IsTerminalBlock == true) return true;
        if (!string.IsNullOrWhiteSpace(selfType) &&
            selfType.Contains("Listwy", StringComparison.OrdinalIgnoreCase))
            return true;

        var label = selfLabel ?? selfSymbol?.Label ?? "";
        if (label.Contains("Szyna prądowa", StringComparison.OrdinalIgnoreCase)) return true;
        return label.Contains("szyna_pradowa", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKontrolkiFaz(SymbolItem symbol)
    {
        if (symbol.Type?.Contains("KontrolkiFaz", StringComparison.OrdinalIgnoreCase) == true)
            return true;
        if (symbol.VisualPath?.Contains("kontrolki faz", StringComparison.OrdinalIgnoreCase) == true)
            return true;
        if (symbol.Label?.Contains("Kontrolki faz", StringComparison.OrdinalIgnoreCase) == true)
            return true;
        return false;
    }

    private IEnumerable<Point> GetBottomTerminalPoints(SymbolItem symbol)
    {
        var poleCount = _moduleTypeService.GetPoleCount(symbol);
        var ratios = GetTerminalRatios(poleCount);
        double bottomY = symbol.Y + symbol.Height;
        return ratios.Select(r => new Point(symbol.X + symbol.Width * r, bottomY));
    }

    private static double[] GetTerminalRatios(ModulePoleCount poleCount)
    {
        if (poleCount == ModulePoleCount.P2)
            return new[] { 0.25, 0.75 };
        if (poleCount == ModulePoleCount.P3 || poleCount == ModulePoleCount.P4)
            return new[] { 0.163, 0.486, 0.821 };
        return new[] { 0.5 };
    }

    private double GetBusbarGap(SymbolItem module)
    {
        var poleCount = _moduleTypeService.GetPoleCount(module);
        int poles = poleCount switch
        {
            ModulePoleCount.P4 => 4,
            ModulePoleCount.P3 => 3,
            ModulePoleCount.P2 => 2,
            ModulePoleCount.P1 => 1,
            _ => 1
        };

        double mmPerPixel = module.Width / (poles * AppDefaults.ModuleUnitWidth);
        return 2.5 * mmPerPixel;
    }

    private static int GetBusbarPinCount(SymbolItem? selfSymbol, string? selfLabel, double selfWidth, double selfHeight)
    {
        var label = selfLabel ?? selfSymbol?.Label ?? "";
        if (!string.IsNullOrWhiteSpace(label))
        {
            var match = System.Text.RegularExpressions.Regex.Match(label, @"(\d+)\s*P", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int pins))
                return Math.Max(3, pins);
        }

        double scale = selfHeight > 0 ? selfHeight / PowerBusbarGenerator.BaseHeight : 0;
        if (scale <= 0) return 0;

        double totalWidth = selfWidth / scale;
        int estimated = (int)Math.Round((totalWidth - PowerBusbarGenerator.BaseWidth) / PowerBusbarGenerator.PinPitch + 3);
        return Math.Max(3, estimated);
    }

    private static List<double> GetBusbarPinCenters(int pinCount, double scale)
    {
        var centers = new List<double>(pinCount);
        double start = (PowerBusbarGenerator.BasePinStartX - PowerBusbarGenerator.PinCenterOffset) * scale;
        double step = PowerBusbarGenerator.PinPitch * scale;
        for (int i = 0; i < pinCount; i++)
        {
            centers.Add(start + i * step);
        }

        return centers;
    }
}
