using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using DINBoard.Models;

namespace DINBoard.Controls;

public sealed class VirtualizingCanvasPanel : Panel
{
    private const double Buffer = 200;
    private readonly Dictionary<Control, SymbolItem?> _symbolSubscriptions = new();

    public VirtualizingCanvasPanel()
    {
        Children.CollectionChanged += OnChildrenChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Children.CollectionChanged -= OnChildrenChanged;
        foreach (var control in _symbolSubscriptions.Keys.ToList())
        {
            UnsubscribeControl(control);
        }
        _symbolSubscriptions.Clear();
    }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems)
            {
                if (item is Control removedControl)
                {
                    UnsubscribeControl(removedControl);
                }
            }
        }

        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems)
            {
                if (item is Control addedControl)
                {
                    SubscribeControl(addedControl);
                }
            }
        }
    }

    private void SubscribeControl(Control control)
    {
        control.DataContextChanged += OnChildDataContextChanged;
        var symbol = control.DataContext as SymbolItem;
        if (symbol != null)
        {
            symbol.PropertyChanged += OnSymbolPropertyChanged;
        }
        _symbolSubscriptions[control] = symbol;
    }

    private void UnsubscribeControl(Control control)
    {
        control.DataContextChanged -= OnChildDataContextChanged;
        if (_symbolSubscriptions.TryGetValue(control, out var symbol) && symbol != null)
        {
            symbol.PropertyChanged -= OnSymbolPropertyChanged;
        }
        _symbolSubscriptions.Remove(control);
    }

    private void OnChildDataContextChanged(object? sender, EventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        if (_symbolSubscriptions.TryGetValue(control, out var oldSymbol) && oldSymbol != null)
        {
            oldSymbol.PropertyChanged -= OnSymbolPropertyChanged;
        }

        var newSymbol = control.DataContext as SymbolItem;
        if (newSymbol != null)
        {
            newSymbol.PropertyChanged += OnSymbolPropertyChanged;
        }

        _symbolSubscriptions[control] = newSymbol;
        InvalidateMeasure();
        InvalidateArrange();
    }

    private void OnSymbolPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SymbolItem.X) ||
            e.PropertyName == nameof(SymbolItem.Y) ||
            e.PropertyName == nameof(SymbolItem.Width) ||
            e.PropertyName == nameof(SymbolItem.Height))
        {
            InvalidateMeasure();
            InvalidateArrange();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var visibleRect = GetVisibleRect();
        var maxRight = 0.0;
        var maxBottom = 0.0;

        foreach (var child in Children)
        {
            if (child is not Control control)
            {
                continue;
            }

            var bounds = GetChildBounds(control);
            if (double.IsFinite(bounds.Right))
            {
                maxRight = Math.Max(maxRight, bounds.Right);
            }

            if (double.IsFinite(bounds.Bottom))
            {
                maxBottom = Math.Max(maxBottom, bounds.Bottom);
            }

            var isVisible = IsVisibleInRect(control, visibleRect);
            control.IsVisible = isVisible;
            if (isVisible)
            {
                control.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            }
            else
            {
                control.Measure(new Size(0, 0));
            }
        }

        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : maxRight;
        var height = double.IsFinite(availableSize.Height) ? availableSize.Height : maxBottom;
        if (!double.IsFinite(width)) width = 0;
        if (!double.IsFinite(height)) height = 0;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var visibleRect = GetVisibleRect();

        foreach (var child in Children)
        {
            if (child is not Control control)
            {
                continue;
            }

            if (IsVisibleInRect(control, visibleRect))
            {
                var bounds = GetChildBounds(control);
                control.Arrange(bounds);
            }
            else
            {
                control.Arrange(new Rect(0, 0, 0, 0));
            }
        }

        return finalSize;
    }

    private Rect GetVisibleRect()
    {
        var canvasContainer = this.GetVisualAncestors().OfType<Border>().FirstOrDefault(b => b.Name == "CanvasContainer");
        var zoomContainer = this.GetVisualAncestors().OfType<Canvas>().FirstOrDefault(c => c.Name == "ZoomContainer");

        if (canvasContainer == null || zoomContainer == null)
        {
            return new Rect(-1_000_000, -1_000_000, 2_000_000, 2_000_000);
        }

        var bounds = canvasContainer.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return new Rect(-1_000_000, -1_000_000, 2_000_000, 2_000_000);
        }

        var matrix = (zoomContainer.RenderTransform as MatrixTransform)?.Matrix ?? Matrix.Identity;
        var scaleX = double.IsFinite(matrix.M11) && matrix.M11 != 0 ? matrix.M11 : 1;
        var scaleY = double.IsFinite(matrix.M22) && matrix.M22 != 0 ? matrix.M22 : 1;

        var x = (-matrix.M31) / scaleX;
        var y = (-matrix.M32) / scaleY;
        var w = bounds.Width / scaleX;
        var h = bounds.Height / scaleY;

        return new Rect(x - Buffer, y - Buffer, w + Buffer * 2, h + Buffer * 2);
    }

    private static bool IsVisibleInRect(Control child, Rect visibleRect)
    {
        if (child.DataContext is SymbolItem symbol)
        {
            var width = symbol.Width > 0 ? symbol.Width : Math.Max(1, child.DesiredSize.Width);
            var height = symbol.Height > 0 ? symbol.Height : Math.Max(1, child.DesiredSize.Height);
            var rect = new Rect(symbol.X, symbol.Y, width, height);
            return visibleRect.Intersects(rect);
        }

        var left = Canvas.GetLeft(child);
        var top = Canvas.GetTop(child);
        if (double.IsNaN(left)) left = 0;
        if (double.IsNaN(top)) top = 0;
        var size = child.DesiredSize;
        var fallbackRect = new Rect(left, top, Math.Max(1, size.Width), Math.Max(1, size.Height));
        return visibleRect.Intersects(fallbackRect);
    }

    private static Rect GetChildBounds(Control child)
    {
        if (child.DataContext is SymbolItem symbol)
        {
            var width = symbol.Width > 0 ? symbol.Width : Math.Max(1, child.DesiredSize.Width);
            var height = symbol.Height > 0 ? symbol.Height : Math.Max(1, child.DesiredSize.Height);
            return new Rect(symbol.X, symbol.Y, width, height);
        }

        var left = Canvas.GetLeft(child);
        var top = Canvas.GetTop(child);
        if (double.IsNaN(left)) left = 0;
        if (double.IsNaN(top)) top = 0;
        var size = child.DesiredSize;
        return new Rect(left, top, Math.Max(1, size.Width), Math.Max(1, size.Height));
    }
}
