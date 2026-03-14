using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Layout;
using DINBoard.Models;

namespace DINBoard.Services;

/// <summary>
/// Fabryka elementów schematu. 
/// Odpowiada za produkcję gotowych Avalonia.Controls (np. Path w Viewbox) dla symboli,
/// linii, tekstów i oznaczeń faz na podstawie geometrii zdefiniowanych w SchematicSymbols.axaml.
/// </summary>
public static class SchematicElementFactory
{
    public static Control CreateSymbol(SchematicNodeType type, string designation, double x, double y, double width, double height, int phaseCount, bool isLightTheme)
    {
        var container = new Canvas { Width = width, Height = height };
        Canvas.SetLeft(container, x);
        Canvas.SetTop(container, y);

        IBrush fgBrush = isLightTheme ? Brushes.Black : Brushes.White;
        double st = 0.6; 
        double stThin = 0.4;

        double px(double val) => (val / 300.0) * width;
        double py(double val) => (val / 350.0) * height;

        Avalonia.Controls.Shapes.Shape Stroke(Avalonia.Controls.Shapes.Shape s, double thickness = -1) 
        {
            s.Stroke = fgBrush;
            s.StrokeThickness = thickness < 0 ? st : thickness;
            s.StrokeLineCap = PenLineCap.Flat;
            s.StrokeJoin = PenLineJoin.Miter;
            RenderOptions.SetEdgeMode(s, EdgeMode.Aliased); // Wymuszenie ostrości pikseli
            return s;
        }

        Avalonia.Controls.Shapes.Line Line(double x1, double y1, double x2, double y2, double thickness = -1) 
            => (Avalonia.Controls.Shapes.Line)Stroke(new Avalonia.Controls.Shapes.Line { StartPoint = new Point(px(x1), py(y1)), EndPoint = new Point(px(x2), py(y2)) }, thickness);
            
        Avalonia.Controls.Shapes.Ellipse Circ(double cx, double cy, double r, double thickness = -1)
        {
            var crc = (Avalonia.Controls.Shapes.Ellipse)Stroke(new Avalonia.Controls.Shapes.Ellipse { Width = px(r*2), Height = px(r*2) }, thickness);
            Canvas.SetLeft(crc, px(cx - r)); Canvas.SetTop(crc, py(cy) - px(r)); // r is effectively scaled by X axis
            return crc;
        }
        
        Avalonia.Controls.Shapes.Rectangle Rect(double rx, double ry, double rw, double rh, double thickness = -1)
        {
            var r = (Avalonia.Controls.Shapes.Rectangle)Stroke(new Avalonia.Controls.Shapes.Rectangle { Width = px(rw), Height = py(rh) }, thickness);
            Canvas.SetLeft(r, px(rx)); Canvas.SetTop(r, py(ry));
            return r;
        }

        switch (type)
        {
            case SchematicNodeType.MainBreaker: // FR
            {
                container.Children.Add(Line(150, 60, 150, 120));
                container.Children.Add(Line(144, 119, 156, 119));
                container.Children.Add(Circ(150, 126, 6));
                container.Children.Add(Line(150, 180, 125, 125));
                
                var c2 = Circ(150, 180, 3);
                c2.Fill = fgBrush;
                container.Children.Add(c2);
                
                container.Children.Add(Line(150, 180, 150, 300));
                break;
            }
            case SchematicNodeType.MCB:
            {
                container.Children.Add(Line(150, 60, 150, 120));
                container.Children.Add(Line(144, 120, 156, 120));
                container.Children.Add(Line(150, 180, 125, 125));
                container.Children.Add(Line(144, 102, 156, 114));
                container.Children.Add(Line(144, 114, 156, 102));
                
                var c1 = Circ(150, 180, 3);
                c1.Fill = fgBrush;
                container.Children.Add(c1);
                
                container.Children.Add(Line(150, 180, 150, 300));
                break;
            }
            case SchematicNodeType.RCD:
            {
                container.Children.Add(Line(150, 60, 150, 120));
                
                var c1 = Circ(150, 120, 3); c1.Fill = fgBrush; container.Children.Add(c1);
                
                container.Children.Add(Line(150, 180, 125, 120));
                
                var c2 = Circ(150, 180, 3); c2.Fill = fgBrush; container.Children.Add(c2);
                
                container.Children.Add(Line(150, 180, 150, 300));
                
                var el = (Avalonia.Controls.Shapes.Ellipse)Stroke(new Avalonia.Controls.Shapes.Ellipse { Width = px(50), Height = py(24) });
                Canvas.SetLeft(el, px(150 - 25)); Canvas.SetTop(el, py(230 - 12));
                container.Children.Add(el);
                
                var l1 = Line(125, 230, 100, 230, stThin); l1.StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 2, 2 }; container.Children.Add(l1);
                var l2 = Line(100, 230, 100, 150, stThin); l2.StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 2, 2 }; container.Children.Add(l2);
                var l3 = Line(100, 150, 135, 150, stThin); l3.StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 2, 2 }; container.Children.Add(l3);
                
                var tTxt = new TextBlock { Text = "IΔ", FontSize = 5, Foreground = fgBrush, FontWeight = FontWeight.Bold };
                Canvas.SetLeft(tTxt, px(175)); Canvas.SetTop(tTxt, py(225));
                container.Children.Add(tTxt);
                break;
            }
            case SchematicNodeType.PhaseIndicator: // Lampka
            {
                container.Children.Add(Line(150, 60, 150, 130));
                var c1 = Circ(150, 60, 3); c1.Fill = fgBrush; container.Children.Add(c1);
                container.Children.Add(Rect(130, 130, 40, 60));
                container.Children.Add(Circ(150, 160, 8));
                container.Children.Add(Line(144, 154, 156, 166));
                container.Children.Add(Line(144, 166, 156, 154));
                container.Children.Add(Line(150, 190, 150, 250));
                var c2 = Circ(150, 250, 3); c2.Fill = fgBrush; container.Children.Add(c2);
                
                var txt = new TextBlock { Text = "N", FontSize = 5, Foreground = fgBrush, FontWeight = FontWeight.Bold };
                Canvas.SetLeft(txt, px(160)); Canvas.SetTop(txt, py(245));
                container.Children.Add(txt);
                break;
            }
            case SchematicNodeType.SPD:
            {
                container.Children.Add(Line(150, 60, 150, 130));
                var c1 = Circ(150, 60, 3); c1.Fill = fgBrush; container.Children.Add(c1);
                container.Children.Add(Rect(130, 130, 40, 60));
                
                container.Children.Add(Line(150, 130, 150, 155));
                container.Children.Add(Line(143, 148, 150, 155));
                container.Children.Add(Line(150, 155, 157, 148));
                
                container.Children.Add(Line(150, 190, 150, 165));
                container.Children.Add(Line(143, 172, 150, 165));
                container.Children.Add(Line(150, 165, 157, 172));
                
                container.Children.Add(Line(150, 190, 150, 250));
                container.Children.Add(Line(125, 250, 175, 250)); // PE 1
                container.Children.Add(Line(135, 260, 165, 260)); // PE 2
                container.Children.Add(Line(145, 270, 155, 270)); // PE 3
                break;
            }
            default:
                container.Children.Add(Line(150, 60, 150, 300));
                break;
        }

        // 2. Dodaj oznaczenie (Designation) (np. "F1", "Q1")
        if (!string.IsNullOrEmpty(designation))
        {
            var textBlock = new TextBlock
            {
                Text = designation,
                FontSize = 9,
                Foreground = fgBrush,
                FontWeight = FontWeight.SemiBold
            };
            
            // Etykietę po prawej stronie symbolu
            Canvas.SetLeft(textBlock, width + 5);
            Canvas.SetTop(textBlock, height / 2 - 6);
            container.Children.Add(textBlock);
        }

        // 3. Ewentualne dodanie kropek, kresek faz na podstawie logicznych portów
        // Te będą dodawane z zewnątrz przez diagram, by ułatwić zarządzanie pozycjami absolutnymi.

        return container;
    }

    public static Line CreateWire(Point start, Point end, bool isLightTheme, double thickness = 1.2, Avalonia.Collections.AvaloniaList<double>? dashArray = null)
    {
        var l = new Line
        {
            StartPoint = start,
            EndPoint = end,
            Stroke = isLightTheme ? Brushes.Black : Brushes.White,
            StrokeThickness = thickness,
            StrokeDashArray = dashArray
        };
        RenderOptions.SetEdgeMode(l, EdgeMode.Aliased);
        return l;
    }

    public static TextBlock CreateLabel(string text, Point pos, bool isLightTheme, double fontSize = 10, bool dim = false, bool center = false)
    {
        IBrush color = isLightTheme 
            ? (dim ? new SolidColorBrush(Color.FromRgb(100,100,100)) : Brushes.Black)
            : (dim ? new SolidColorBrush(Color.FromRgb(180,180,180)) : Brushes.White);

        var tb = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            Foreground = color,
            TextAlignment = center ? TextAlignment.Center : TextAlignment.Left
        };

        if (center)
        {
            // Zgrubne wyśrodkowanie - Canvas wymaga określenia Left/Top. 
            // W środowisku produkcyjnym można to zaktualizować w evencie layoutu
            Canvas.SetLeft(tb, pos.X - text.Length * (fontSize * 0.3));
        }
        else
        {
            Canvas.SetLeft(tb, pos.X);
        }
        
        Canvas.SetTop(tb, pos.Y);
        return tb;
    }

    public static Ellipse CreateDot(Point center, double radius, bool isLightTheme, bool isTransparent = false)
    {
        var dot = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Fill = isTransparent ? Brushes.Transparent : (isLightTheme ? Brushes.Black : Brushes.White),
            Stroke = isLightTheme ? Brushes.Black : Brushes.White,
            StrokeThickness = 1.0
        };

        Canvas.SetLeft(dot, center.X - radius);
        Canvas.SetTop(dot, center.Y - radius);
        return dot;
    }

    public static Control CreatePhaseMarks(Point center, int phaseCount, bool isLightTheme)
    {
        IBrush fg = isLightTheme ? Brushes.Black : Brushes.White;
        var cvs = new Canvas { Width = 20, Height = 10 };
        Canvas.SetLeft(cvs, center.X - 10);
        Canvas.SetTop(cvs, center.Y - 5);

        phaseCount = System.Math.Clamp(phaseCount, 1, 3);
        double h = 4, gap = 2.5, off = -(phaseCount - 1) * gap / 2;
        
        for (int i = 0; i < phaseCount; i++)
        {
            double d = off + i * gap;
            cvs.Children.Add(new Line
            {
                StartPoint = new Point(10 - h + d, 5 + h),
                EndPoint = new Point(10 + h + d, 5 - h),
                Stroke = fg,
                StrokeThickness = 1.0
            });
        }
        return cvs;
    }
}
