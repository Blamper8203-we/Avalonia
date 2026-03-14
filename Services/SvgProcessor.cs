using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace DINBoard.Services;

/// <summary>
/// Procesor SVG — normalizacja, parametry, wymiary.
/// </summary>
public class SvgProcessor
{
    public const double StandardModuleWidth = 232.58;  // 1P (approx 17.5mm)
    public const double StandardModuleHeight = 1103.0;
    private const double SCALE_FACTOR = StandardModuleWidth / 212.0;
    private const double DEFAULT_WIDTH = StandardModuleWidth;
    private const double DEFAULT_HEIGHT = StandardModuleHeight;

    public string ApplyParameters(string svgContent, Dictionary<string, string> parameters)
    {
        if (parameters == null || parameters.Count == 0)
            return svgContent;

        foreach (var kvp in parameters)
        {
            svgContent = svgContent.Replace($"{{{{{kvp.Key}}}}}", kvp.Value, StringComparison.Ordinal);
        }

        return svgContent;
    }

    public string ApplyBlueCoverVisibility(string svgContent, Dictionary<string, string> parameters)
    {
        if (parameters == null) return svgContent;

        bool showBlueCover = parameters.TryGetValue("BLUE_COVER_VISIBLE", out var val) &&
                             string.Equals(val, "True", StringComparison.OrdinalIgnoreCase);

        string visibility = showBlueCover ? "visible" : "hidden";
        svgContent = svgContent.Replace("{{BLUE_COVER_VISIBILITY}}", visibility, StringComparison.Ordinal);

        return svgContent;
    }

    /// <summary>
    /// Normalizuje SVG i oblicza wymiary na podstawie zawartości.
    /// Usuwa transformacje translate i ustawia viewBox dokładnie na granice zawartości.
    /// </summary>
    public (string NormalizedSvg, double Width, double Height) NormalizeAndCalculateSize(string svgContent, string? filePath)
    {
        double width = DEFAULT_WIDTH;
        double height = DEFAULT_HEIGHT;

        try
        {
            bool isDist = filePath != null && filePath.Contains("blok rozdzielczy", StringComparison.OrdinalIgnoreCase);

            var doc = XDocument.Parse(svgContent);
            var svgRoot = doc.Root;
            if (svgRoot == null)
            {
                AppLog.Warn("SvgProcessor: Brak elementu root w SVG");
                return (svgContent, width, height);
            }

            // Znajdź największy widoczny prostokąt (body elementu) - będzie naszą referencją
            var (found, bx, by, bw, bh) = FindLargestVisibleRect(svgRoot);

            if (found && bw > 10 && bh > 10)
            {
                // Sprawdź czy oryginalny viewBox jest znacząco większy niż body rect
                // Jeśli tak — moduł ma elementy poza prostokątami (np. SPD) i nie należy przycinać
                bool useOriginalViewBox = false;
                var origViewBox = svgRoot.Attribute("viewBox")?.Value;
                if (!string.IsNullOrEmpty(origViewBox))
                {
                    var parts = origViewBox.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4 &&
                        TryParseDouble(parts[2], out var origW) &&
                        TryParseDouble(parts[3], out var origH))
                    {
                        double bodyArea = bw * bh;
                        double origArea = origW * origH;
                        // Jeśli body rect pokrywa <70% oryginalnego viewBox — zachowaj oryginał
                        if (bodyArea < origArea * 0.7)
                        {
                            useOriginalViewBox = true;
                            width = origW * SCALE_FACTOR;
                            height = origH * SCALE_FACTOR;
                            AppLog.Debug($"SvgProcessor: Zachowano oryginalny viewBox (body={bodyArea:F0} < orig={origArea:F0}), wymiary: {width:F0}x{height:F0}");
                        }
                    }
                }

                if (!useOriginalViewBox)
                {
                    string newViewBox = $"{Fmt(bx)} {Fmt(by)} {Fmt(bw)} {Fmt(bh)}";
                    svgRoot.SetAttributeValue("viewBox", newViewBox);

                    width = bw * SCALE_FACTOR;
                    height = bh * SCALE_FACTOR;

                    AppLog.Debug($"SvgProcessor: viewBox ustawiony na {newViewBox}, wymiary: {width:F0}x{height:F0}");
                }

                var firstG = svgRoot.Elements().FirstOrDefault(e => e.Name.LocalName == "g");
                if (firstG != null)
                {
                    firstG.Attribute("transform")?.Remove();
                }
            }
            else
            {
                // Fallback: użyj istniejącego viewBox
                var viewBox = svgRoot.Attribute("viewBox")?.Value;
                if (!string.IsNullOrEmpty(viewBox))
                {
                    var parts = viewBox.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4 &&
                        double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var vbW) &&
                        double.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out var vbH))
                    {
                        width = vbW * SCALE_FACTOR;
                        height = vbH * SCALE_FACTOR;
                    }
                }
            }

            // Wymuszone wymiary dla bloku rozdzielczego
            if (isDist)
            {
                double targetHeight = 88.0 * 13.29;
                double aspect = (bw > 0 && bh > 0) ? bw / bh : width / height;
                height = targetHeight;
                width = targetHeight * aspect;
            }

            return (doc.ToString(SaveOptions.DisableFormatting), width, height);
        }
        catch (Exception ex)
        {
            AppLog.Error("SvgProcessor: Błąd normalizacji SVG", ex);
            return (svgContent, width, height);
        }
    }

    /// <summary>
    /// Oblicza bounding box na podstawie widocznych prostokątów w SVG.
    /// </summary>
    private static (bool Found, double X, double Y, double Width, double Height) FindLargestVisibleRect(XElement svgRoot)
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        bool found = false;

        var rects = svgRoot.Descendants().Where(e => e.Name.LocalName == "rect");

        foreach (var rect in rects)
        {
            var style = (string?)rect.Attribute("style") ?? "";
            var id = (string?)rect.Attribute("id") ?? "";

            if (style.Contains("fill:none", StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("Page", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!TryParseDouble(rect.Attribute("width")?.Value, out var w) ||
                !TryParseDouble(rect.Attribute("height")?.Value, out var h))
                continue;

            if (w > 10 && h > 10)
            {
                TryParseDouble(rect.Attribute("x")?.Value, out var rx);
                TryParseDouble(rect.Attribute("y")?.Value, out var ry);

                if (rx < minX) minX = rx;
                if (ry < minY) minY = ry;
                if (rx + w > maxX) maxX = rx + w;
                if (ry + h > maxY) maxY = ry + h;
                found = true;
            }
        }

        if (!found)
            return (false, 0, 0, 0, 0);

        return (true, minX, minY, maxX - minX, maxY - minY);
    }

    private static bool TryParseDouble(string? value, out double result)
    {
        result = 0;
        if (string.IsNullOrEmpty(value))
            return false;
        return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }

    private static string Fmt(double value)
    {
        return value.ToString("G", CultureInfo.InvariantCulture);
    }
}
