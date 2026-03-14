using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia.Threading;
using DINBoard.Models;
using E = DINBoard.Services.SchematicLayoutEngine;

namespace DINBoard.Services;

/// <summary>
/// Obsługuje animację przejścia modułów między układami schematu (np. po balansowaniu faz).
/// </summary>
public sealed class SchematicLayoutAnimator
{
    private DispatcherTimer? _timer;
    private readonly Stopwatch _watch = new();
    private Dictionary<string, (double StartX, double StartY, double TargetX, double TargetY)>? _animationMap;
    private const double DurationMs = 220.0;

    /// <summary>Przygotowuje mapę animacji między poprzednim a nowym układem.</summary>
    /// <returns>Słownik symbolId → (StartX, StartY, TargetX, TargetY) lub null jeśli brak ruchu.</returns>
    public Dictionary<string, (double StartX, double StartY, double TargetX, double TargetY)>? PrepareAnimation(
        SchematicLayout previousLayout,
        SchematicLayout newLayout)
    {
        var oldPositions = BuildNodePositionMap(previousLayout);
        var newPositions = BuildNodePositionMap(newLayout);
        var animationMap = new Dictionary<string, (double StartX, double StartY, double TargetX, double TargetY)>();

        foreach (var (symbolId, targetPos) in newPositions)
        {
            if (!oldPositions.TryGetValue(symbolId, out var startPos))
                continue;

            var dx = Math.Abs(targetPos.X - startPos.X);
            var dy = Math.Abs(targetPos.Y - startPos.Y);
            if (dx < 0.5 && dy < 0.5)
                continue;

            animationMap[symbolId] = (startPos.X, startPos.Y, targetPos.X, targetPos.Y);
        }

        if (animationMap.Count == 0)
            return null;

        // Ustaw węzły nowego układu na pozycje startowe
        foreach (var node in EnumerateAllNodes(newLayout))
        {
            var symbolId = node.Symbol?.Id;
            if (string.IsNullOrWhiteSpace(symbolId))
                continue;

            if (animationMap.TryGetValue(symbolId, out var anim))
            {
                node.X = anim.StartX;
                node.Y = anim.StartY;
            }
        }

        return animationMap;
    }

    private SchematicLayout? _layout;
    private Action? _invalidateVisual;

    /// <summary>Uruchamia animację.</summary>
    public void Start(
        SchematicLayout layout,
        Dictionary<string, (double StartX, double StartY, double TargetX, double TargetY)> animationMap,
        Action invalidateVisual)
    {
        _animationMap = animationMap;
        _layout = layout;
        _invalidateVisual = invalidateVisual;
        _watch.Restart();
        _timer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick -= OnTick;
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_layout == null || _animationMap == null || _animationMap.Count == 0 || _invalidateVisual == null)
        {
            Stop();
            return;
        }

        var t = _watch.Elapsed.TotalMilliseconds / DurationMs;
        if (t > 1.0) t = 1.0;

        var eased = 1.0 - Math.Pow(1.0 - t, 3.0);

        foreach (var node in EnumerateAllNodes(_layout))
        {
            var symbolId = node.Symbol?.Id;
            if (string.IsNullOrWhiteSpace(symbolId))
                continue;

            if (_animationMap.TryGetValue(symbolId, out var v))
            {
                node.X = v.StartX + (v.TargetX - v.StartX) * eased;
                node.Y = v.StartY + (v.TargetY - v.StartY) * eased;
            }
        }

        _invalidateVisual();

        if (t >= 1.0)
            Stop();
    }

    public void Stop()
    {
        if (_timer != null)
        {
            _timer.Tick -= OnTick;
            _timer.Stop();
        }
        _animationMap = null;
        _layout = null;
        _invalidateVisual = null;
        _watch.Reset();
    }

    public bool IsRunning => _timer?.IsEnabled ?? false;

    private static Dictionary<string, (double X, double Y)> BuildNodePositionMap(SchematicLayout layout)
    {
        var map = new Dictionary<string, (double X, double Y)>();
        foreach (var node in EnumerateAllNodes(layout))
        {
            var symbolId = node.Symbol?.Id;
            if (string.IsNullOrWhiteSpace(symbolId))
                continue;
            map[symbolId] = (node.X, node.Y);
        }
        return map;
    }

    private static IEnumerable<SchematicNode> EnumerateAllNodes(SchematicLayout layout)
    {
        foreach (var node in layout.Devices)
        {
            foreach (var child in EnumerateNode(node))
                yield return child;
        }
    }

    private static IEnumerable<SchematicNode> EnumerateNode(SchematicNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var nested in EnumerateNode(child))
                yield return nested;
        }
    }
}
