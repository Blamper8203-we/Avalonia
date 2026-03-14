using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DINBoard.Services;

/// <summary>
/// Helper dla operacji na SVG - konsoliduje duplikaty kodu
/// </summary>
public static class SvgHelper
{
    /// <summary>
    /// Pobiera wymiary (viewBox) z SVG
    /// </summary>
    public static (double Width, double Height) GetDimensionsFromViewBox(string svgContent)
    {
        try
        {
            var doc = XDocument.Parse(svgContent);
            var root = doc.Root;
            if (root == null) return (0, 0);

            // Pobierz viewBox - to jest źródło prawdy
            var viewBox = (string?)root.Attribute("viewBox");
            if (!string.IsNullOrEmpty(viewBox))
            {
                var parts = viewBox.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4 &&
                    double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var vbW) &&
                    double.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out var vbH))
                {
                    return (vbW, vbH);
                }
            }

            // Fallback: jeśli brak viewBox, spróbuj width/height
            var widthAttr = (string?)root.Attribute("width");
            var heightAttr = (string?)root.Attribute("height");
            if (!string.IsNullOrEmpty(widthAttr) && !string.IsNullOrEmpty(heightAttr))
            {
                var wStr = Regex.Replace(widthAttr, @"[^\d\.\-]", "");
                var hStr = Regex.Replace(heightAttr, @"[^\d\.\-]", "");
                if (double.TryParse(wStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var wVal) &&
                    double.TryParse(hStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var hVal))
                {
                    return (wVal, hVal);
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Błąd podczas pobierania wymiarów SVG", ex);
        }

        return (0, 0);
    }

    /// <summary>
    /// Przelicza pozycję z SVG viewBox na canvas (Stretch=Uniform)
    /// </summary>
    public static Avalonia.Point SvgToCanvasPoint(
        Avalonia.Point svgPoint,
        double svgViewBoxWidth,
        double svgViewBoxHeight,
        double canvasWidth,
        double canvasHeight)
    {
        double scaleX = canvasWidth / svgViewBoxWidth;
        double scaleY = canvasHeight / svgViewBoxHeight;
        double scale = Math.Min(scaleX, scaleY);
        
        double offsetX = (canvasWidth - svgViewBoxWidth * scale) / 2;
        double offsetY = (canvasHeight - svgViewBoxHeight * scale) / 2;

        return new Avalonia.Point(
            offsetX + svgPoint.X * scale,
            offsetY + svgPoint.Y * scale
        );
    }

    /// <summary>
    /// Oblicza transformację SVG viewBox -> canvas
    /// </summary>
    public static (double OffsetX, double OffsetY, double Scale) GetSvgToCanvasTransform(
        double svgViewBoxWidth,
        double svgViewBoxHeight,
        double canvasWidth,
        double canvasHeight)
    {
        double scaleX = canvasWidth / svgViewBoxWidth;
        double scaleY = canvasHeight / svgViewBoxHeight;
        double scale = Math.Min(scaleX, scaleY);
        
        double offsetX = (canvasWidth - svgViewBoxWidth * scale) / 2;
        double offsetY = (canvasHeight - svgViewBoxHeight * scale) / 2;

        return (offsetX, offsetY, scale);
    }
}
