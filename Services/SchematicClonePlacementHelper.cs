using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using DINBoard.Models;
using DINBoard.ViewModels;

namespace DINBoard.Services;

/// <summary>
/// Oblicza pozycję grupy klonów podczas trybu "Stempel" (powielanie) z uwzględnieniem snap do szyny DIN i sąsiednich modułów.
/// </summary>
public static class SchematicClonePlacementHelper
{
    private const double SnapDistance = 80.0;
    private const double SnapThresholdX = 40.0;
    private const double SameRailToleranceY = 20.0;

    /// <summary>
    /// Aktualizuje pozycje klonów w trybie powielania. Uwzględnia snap do osi szyny DIN i krawędzi modułów.
    /// </summary>
    public static void UpdateClonesPosition(MainViewModel viewModel, Point currentPoint)
    {
        if (viewModel?.ClonesToPlace == null || !viewModel.ClonesToPlace.Any())
            return;

        var baseSymbol = viewModel.ClonesToPlace.OrderBy(s => s.X).First();
        double targetX = currentPoint.X - (baseSymbol.Width / 2.0);
        double targetY = currentPoint.Y - (baseSymbol.Height / 2.0);

        (double snapY, bool shouldSnap) = ComputeSnapToRail(viewModel, currentPoint, baseSymbol, targetY);
        double deltaX = targetX - baseSymbol.X;
        double deltaY = shouldSnap ? (snapY - baseSymbol.Y) : (targetY - baseSymbol.Y);

        if (shouldSnap)
        {
            deltaX = ApplySnapToNeighborModules(viewModel, baseSymbol, deltaX, deltaY);
        }

        ApplyDeltaToClones(viewModel.ClonesToPlace, deltaX, deltaY, shouldSnap);
    }

    private static (double snapY, bool shouldSnap) ComputeSnapToRail(
        MainViewModel viewModel,
        Point currentPoint,
        SymbolItem baseSymbol,
        double targetY)
    {
        var axes = viewModel.Schematic?.DinRailAxes;
        if (axes == null || axes.Count == 0)
        {
            viewModel.StatusMessage = "Tryb powielania - umieść elementy kliknięciem";
            return (targetY, false);
        }

        double moduleHalfHeight = baseSymbol.Height / 2.0;
        double closestAxis = axes[0];
        double minDistance = Math.Abs(currentPoint.Y - closestAxis);

        foreach (var axis in axes)
        {
            double d = Math.Abs(currentPoint.Y - axis);
            if (d < minDistance)
            {
                minDistance = d;
                closestAxis = axis;
            }
        }

        if (minDistance < SnapDistance)
        {
            viewModel.StatusMessage = "Przyciągnięto powielane elementy do szyny DIN. Naciśnij LMB, aby postawić.";
            return (closestAxis - moduleHalfHeight, true);
        }

        viewModel.StatusMessage = "Tryb powielania - umieść elementy kliknięciem";
        return (targetY, false);
    }

    private static double ApplySnapToNeighborModules(
        MainViewModel viewModel,
        SymbolItem baseSymbol,
        double deltaX,
        double deltaY)
    {
        double cloneGroupLeft = baseSymbol.X + deltaX;
        double cloneGroupRight = viewModel.ClonesToPlace!.Max(c => c.X + c.Width) + deltaX;
        double bestSnapDeltaX = double.MaxValue;

        foreach (var existing in viewModel.Symbols)
        {
            if (viewModel.ClonesToPlace.Contains(existing)) continue;
            if (Math.Abs(existing.Y - (baseSymbol.Y + deltaY)) > SameRailToleranceY) continue;

            double rightEdge = existing.X + existing.Width;
            double distToRight = Math.Abs(cloneGroupLeft - rightEdge);
            if (distToRight < SnapThresholdX && distToRight < Math.Abs(bestSnapDeltaX))
            {
                bestSnapDeltaX = rightEdge - cloneGroupLeft;
            }

            double leftEdge = existing.X;
            double distToLeft = Math.Abs(cloneGroupRight - leftEdge);
            if (distToLeft < SnapThresholdX && distToLeft < Math.Abs(bestSnapDeltaX))
            {
                bestSnapDeltaX = leftEdge - cloneGroupRight;
            }
        }

        return bestSnapDeltaX != double.MaxValue ? deltaX + bestSnapDeltaX : deltaX;
    }

    private static void ApplyDeltaToClones(
        IList<SymbolItem> clones,
        double deltaX,
        double deltaY,
        bool shouldSnap)
    {
        foreach (var clone in clones)
        {
            clone.X += deltaX;
            clone.Y += deltaY;
            clone.IsSnappedToRail = shouldSnap;
        }
    }
}
