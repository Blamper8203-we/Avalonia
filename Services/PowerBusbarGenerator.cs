using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using DINBoard.Constants;

namespace DINBoard.Services;

public enum BusbarType
{
    ThreePhase,
    SinglePhaseL1,
    SinglePhaseL2,
    SinglePhaseL3
}

public sealed class PowerBusbarGenerator
{
    public const double BaseWidth = 768.0;
    public const double BaseHeight = 390.0;
    public const double BasePinStartX = 217.56;
    public const double PinPitch = 229.996;
    public const double PinTopY = 3.255;
    public const double PinCenterOffset = 52.3465;
    public const double LabelOffset = 39.499;

    public const double BodyX = 33.101;
    public const double BodyY = 162.164;
    public const double BodyWidth = 701.797;
    public const double BodyHeight = 218.51;

    private static readonly Lazy<BusbarTemplate?> TemplateCache = new(LoadBusbarTemplate);

    public IReadOnlyList<string> BuildPhasesForPinCount(int pinCount, BusbarType type = BusbarType.ThreePhase)
    {
        if (pinCount <= 0)
        {
            return Array.Empty<string>();
        }

        var phases = new List<string>(pinCount);

        if (type == BusbarType.ThreePhase)
        {
            string[] sequence = { "L1", "L2", "L3" };
            for (int i = 0; i < pinCount; i++)
            {
                phases.Add(sequence[i % sequence.Length]);
            }
        }
        else
        {
            string phaseLabel = type switch
            {
                BusbarType.SinglePhaseL1 => "L1",
                BusbarType.SinglePhaseL2 => "L2",
                BusbarType.SinglePhaseL3 => "L3",
                _ => "L1"
            };

            for (int i = 0; i < pinCount; i++)
            {
                phases.Add(phaseLabel);
            }
        }

        return phases;
    }

    public string GenerateSvgForPinCount(int pinCount, BusbarType type = BusbarType.ThreePhase)
    {
        var phases = BuildPhasesForPinCount(pinCount, type);
        return GenerateSvg(phases, type);
    }

    public string GenerateSvg(IReadOnlyList<string> phases, BusbarType type = BusbarType.ThreePhase)
    {
        ArgumentNullException.ThrowIfNull(phases);
        if (phases.Count == 0)
        {
            return "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"200\" height=\"50\"></svg>";
        }

        var template = TemplateCache.Value;

        double baseWidth = template?.ViewBoxWidth ?? BaseWidth;
        double baseHeight = template?.ViewBoxHeight ?? BaseHeight;
        double bodyX = template?.Body.X ?? BodyX;
        double bodyY = template?.Body.Y ?? BodyY;
        double bodyWidth = template?.Body.Width ?? BodyWidth;
        double bodyHeight = template?.Body.Height ?? BodyHeight;
        string bodyStyle = template?.Body.Style ?? "fill:#ebebeb;stroke:#000;stroke-width:3.04px;";
        string pinPath = template?.Pin.D ?? "M217.56,3.255l25.695,0l0,158.91l-130.388,0l0,-158.91l25.695,0l0,50.703c0,21.8 17.699,39.499 39.499,39.499c21.8,0 39.499,-17.699 39.499,-39.499l0,-50.703Z";
        string pinStyle = template?.Pin.Style ?? "fill:#ebebeb;stroke:#000;stroke-width:2.82px;";
        pinStyle = pinStyle.Replace("fill:#ebebeb", "fill:#B87333", StringComparison.OrdinalIgnoreCase);
        pinStyle = pinStyle.Replace("stroke:#000", "stroke:#B87333", StringComparison.OrdinalIgnoreCase);
        string leftCoverPath = template?.LeftCover.D ?? "M64.286,157.902l0,227.036c0,0.998 -0.81,1.808 -1.808,1.808l-55.59,0c-2.744,0 -4.971,-2.228 -4.971,-4.971l0,-220.709c0,-2.744 2.228,-4.971 4.971,-4.971l55.59,0c0.998,0 1.808,0.81 1.808,1.808Z";
        string leftCoverStyle = template?.LeftCover.Style ?? "fill:#ebebeb;stroke:#000;stroke-width:3.04px;";
        string rightCoverPath = template?.RightCover.D ?? "M703.714,384.938l-0,-227.036c0,-0.998 0.81,-1.808 1.808,-1.808l55.59,-0c2.744,0 4.971,2.228 4.971,4.971l0,220.709c0,2.744 -2.228,4.971 -4.971,4.971l-55.59,0c-0.998,0 -1.808,-0.81 -1.808,-1.808Z";
        string rightCoverStyle = template?.RightCover.Style ?? "fill:#ebebeb;stroke:#000;stroke-width:2.97px;";
        string labelStyle = template?.LabelStyle ?? "font-family:'ArialMT', 'Arial', sans-serif;font-size:50px;";
        if (!labelStyle.Contains("text-anchor", StringComparison.Ordinal))
        {
            labelStyle = labelStyle.TrimEnd(';') + ";text-anchor:middle;";
        }
        IReadOnlyList<double> labelXs = template?.LabelXs ?? new[] { BasePinStartX - LabelOffset, BasePinStartX + PinPitch - LabelOffset, BasePinStartX + 2 * PinPitch - LabelOffset };
        IReadOnlyList<double> labelYs = template?.LabelYs ?? new[] { 228.941, 228.941, 228.306 };
        string shadowLeftVerticalD = template?.ShadowLeftVertical.D ?? "M7.423,167.653l0,207.533";
        string shadowLeftVerticalStyle = template?.ShadowLeftVertical.Style ?? "fill:none;stroke:#000;stroke-width:0.67px;";
        string shadowRightVerticalD = template?.ShadowRightVertical.D ?? "M760.577,167.653l0,207.533";
        string shadowRightVerticalStyle = template?.ShadowRightVertical.Style ?? "fill:none;stroke:#000;stroke-width:0.67px;";

        double mmToSvg = PinPitch / AppDefaults.ModuleUnitWidth;
        double extraRightSpace = 5 * mmToSvg;
        double totalWidth = baseWidth + (phases.Count - 3) * PinPitch + extraRightSpace;
        double deltaWidth = totalWidth - baseWidth;
        double rightCoverShift = deltaWidth;

        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>");
        sb.Append("<!DOCTYPE svg PUBLIC \"-//W3C//DTD SVG 1.1//EN\" \"http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd\">");
        sb.Append(CultureInfo.InvariantCulture, $"<svg width=\"100%\" height=\"100%\" viewBox=\"0 0 {F(totalWidth)} {F(baseHeight)}\"");
        if (!string.IsNullOrWhiteSpace(template?.Root.Version))
        {
            sb.Append(CultureInfo.InvariantCulture, $" version=\"{template!.Root.Version}\"");
        }
        sb.Append(" xmlns=\"http://www.w3.org/2000/svg\"");
        if (!string.IsNullOrWhiteSpace(template?.Root.XmlnsXlink))
        {
            sb.Append(CultureInfo.InvariantCulture, $" xmlns:xlink=\"{template!.Root.XmlnsXlink}\"");
        }
        if (!string.IsNullOrWhiteSpace(template?.Root.XmlSpace))
        {
            sb.Append(CultureInfo.InvariantCulture, $" xml:space=\"{template!.Root.XmlSpace}\"");
        }
        if (!string.IsNullOrWhiteSpace(template?.Root.XmlnsSerif))
        {
            sb.Append(CultureInfo.InvariantCulture, $" xmlns:serif=\"{template!.Root.XmlnsSerif}\"");
        }
        if (!string.IsNullOrWhiteSpace(template?.Root.Style))
        {
            sb.Append(CultureInfo.InvariantCulture, $" style=\"{template!.Root.Style}\"");
        }
        sb.Append('>');
        sb.Append("<g>");

        string bodyId = template?.Body.Id ?? "Obudowa";
        sb.Append(CultureInfo.InvariantCulture, $"<rect id=\"{bodyId}\" x=\"{F(bodyX)}\" y=\"{F(bodyY)}\" width=\"{F(bodyWidth + deltaWidth)}\" height=\"{F(bodyHeight)}\" style=\"{bodyStyle}\"/>");

        for (int i = 0; i < phases.Count; i++)
        {
            double dx = i * PinPitch;
            string pinId = i < 3 ? $"Pin-L{i + 1}" : $"Pin-L{i + 1}";
            string pinSerifId = i < 3 ? $"Pin L{i + 1}" : $"Pin L{i + 1}";
            string pinD = ShiftMoveX(pinPath, dx);
            sb.Append(CultureInfo.InvariantCulture, $"<path id=\"{pinId}\" serif:id=\"{pinSerifId}\" d=\"{pinD}\" style=\"{pinStyle}\"/>");
        }

        string leftCoverId = template?.LeftCover.Id ?? "Osłona-Lewa";
        string leftCoverSerifId = template?.LeftCover.SerifId ?? "Osłona Lewa";
        string rightCoverId = template?.RightCover.Id ?? "Osłona-Prawa";
        string rightCoverSerifId = template?.RightCover.SerifId ?? "Osłona Prawa";
        sb.Append(CultureInfo.InvariantCulture, $"<path id=\"{leftCoverId}\" serif:id=\"{leftCoverSerifId}\" d=\"{leftCoverPath}\" style=\"{leftCoverStyle}\"/>");
        string rightCoverAdjusted = ShiftMoveX(rightCoverPath, rightCoverShift);
        sb.Append(CultureInfo.InvariantCulture, $"<path id=\"{rightCoverId}\" serif:id=\"{rightCoverSerifId}\" d=\"{rightCoverAdjusted}\" style=\"{rightCoverStyle}\"/>");

        double matrixA = template?.LabelMatrixA ?? 50;
        double matrixB = template?.LabelMatrixB ?? 0;
        double matrixC = template?.LabelMatrixC ?? 0;
        double matrixD = template?.LabelMatrixD ?? 50;
        IReadOnlyList<double> labelMatrixXs = template?.LabelMatrixXs ?? Array.Empty<double>();
        IReadOnlyList<double> labelMatrixYs = template?.LabelMatrixYs ?? Array.Empty<double>();
        double matrixOffsetX = template?.LabelMatrixOffsetX ?? 55.615054;
        double matrixOffsetY = template?.LabelMatrixOffsetY ?? 0;
        IReadOnlyList<double> labelOffsets = template?.LabelOffsets ?? new[] { LabelOffset, LabelOffset, LabelOffset };

        for (int i = 0; i < phases.Count; i++)
        {
            double pinStartX = BasePinStartX + i * PinPitch;
            double labelX = pinStartX - LabelOffset;
            double labelY = labelYs[i % labelYs.Count];
            double matrixX = i < labelMatrixXs.Count ? labelMatrixXs[i] : labelX + matrixOffsetX;
            double matrixY = i < labelMatrixYs.Count ? labelMatrixYs[i] : labelY + matrixOffsetY;
            sb.Append(CultureInfo.InvariantCulture, $"<g transform=\"matrix({F(matrixA)},{F(matrixB)},{F(matrixC)},{F(matrixD)},{F(matrixX)},{F(matrixY)})\"></g>");

            string currentLabelStyle = labelStyle;
            if (type == BusbarType.ThreePhase)
            {
                // Dodajemy czerwony kolor dla etykiet w szynie 3-fazowej
                if (!currentLabelStyle.Contains("fill:", StringComparison.Ordinal))
                {
                    currentLabelStyle = currentLabelStyle.TrimEnd(';') + ";fill:#ff0000;";
                }
                else
                {
                    // Jeśli styl już ma fill, podmieniamy go na czerwony
                    // Prosta podmiana dla demonstracji, w realnym kodzie można użyć regex lub parsowania stylu
                    currentLabelStyle = currentLabelStyle.Replace("fill:#000000", "fill:#ff0000", StringComparison.OrdinalIgnoreCase);
                    currentLabelStyle = currentLabelStyle.Replace("fill:black", "fill:#ff0000", StringComparison.OrdinalIgnoreCase);
                }
            }

            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{F(labelX)}px\" y=\"{F(labelY)}px\" style=\"{currentLabelStyle}\">{phases[i]}</text>");
        }

        string shadowRightVerticalId = template?.ShadowRightVertical.Id ?? "cień-6";
        string shadowRightVerticalSerifId = template?.ShadowRightVertical.SerifId ?? "cień 6";
        string shadowLeftVerticalId = template?.ShadowLeftVertical.Id ?? "cień-5";
        string shadowLeftVerticalSerifId = template?.ShadowLeftVertical.SerifId ?? "cień 5";
        string shadowRightVerticalAdjusted = ShiftMoveX(shadowRightVerticalD, rightCoverShift);

        sb.Append(CultureInfo.InvariantCulture, $"<path id=\"{shadowLeftVerticalId}\" serif:id=\"{shadowLeftVerticalSerifId}\" d=\"{shadowLeftVerticalD}\" style=\"{shadowLeftVerticalStyle}\"/>");
        sb.Append(CultureInfo.InvariantCulture, $"<path id=\"{shadowRightVerticalId}\" serif:id=\"{shadowRightVerticalSerifId}\" d=\"{shadowRightVerticalAdjusted}\" style=\"{shadowRightVerticalStyle}\"/>");

        string shadowLeftBottomId = template?.ShadowLeftBottom.Id ?? "cień-3";
        string shadowLeftBottomSerifId = template?.ShadowLeftBottom.SerifId ?? "cień 3";
        string shadowLeftBottomD = template?.ShadowLeftBottom.D ?? "M7.154,380.621l51.895,0";
        string shadowLeftBottomStyle = template?.ShadowLeftBottom.Style ?? "fill:none;stroke:#000;stroke-width:0.67px;";

        string shadowRightBottomId = template?.ShadowRightBottom.Id ?? "cień-4";
        string shadowRightBottomSerifId = template?.ShadowRightBottom.SerifId ?? "cień 4";
        string shadowRightBottomD = template?.ShadowRightBottom.D ?? "M708.681,380.621l51.895,0";
        string shadowRightBottomStyle = template?.ShadowRightBottom.Style ?? "fill:none;stroke:#000;stroke-width:0.67px;";
        string shadowRightBottomAdjusted = ShiftMoveX(shadowRightBottomD, rightCoverShift);

        string shadowMiddleBottomId = template?.ShadowMiddleBottom.Id ?? "cień-2";
        string shadowMiddleBottomSerifId = template?.ShadowMiddleBottom.SerifId ?? "cień 2";
        string shadowMiddleBottomD = template?.ShadowMiddleBottom.D ?? "M96.604,372.842l574.792,0";
        string shadowMiddleBottomStyle = template?.ShadowMiddleBottom.Style ?? "fill:none;stroke:#000;stroke-width:0.67px;";
        string shadowMiddleBottomAdjusted = ExtendFirstLineX(shadowMiddleBottomD, deltaWidth);

        string shadowMiddleTopId = template?.ShadowMiddleTop.Id ?? "cień-1";
        string shadowMiddleTopSerifId = template?.ShadowMiddleTop.SerifId ?? "cień 1";
        string shadowMiddleTopD = template?.ShadowMiddleTop.D ?? "M100.096,170.233l574.792,0";
        string shadowMiddleTopStyle = template?.ShadowMiddleTop.Style ?? "fill:none;stroke:#000;stroke-width:0.67px;";
        string shadowMiddleTopAdjusted = ExtendFirstLineX(shadowMiddleTopD, deltaWidth);

        // Całkowite usunięcie dolnych cieni (również narożnych), aby wyeliminować "czarną kreskę"
        // sb.Append($"<path id=\"{shadowLeftBottomId}\" serif:id=\"{shadowLeftBottomSerifId}\" d=\"{shadowLeftBottomD}\" style=\"{shadowLeftBottomStyle}\"/>");
        // sb.Append($"<path id=\"{shadowRightBottomId}\" serif:id=\"{shadowRightBottomSerifId}\" d=\"{shadowRightBottomAdjusted}\" style=\"{shadowRightBottomStyle}\"/>");
        // sb.Append($"<path id=\"{shadowMiddleBottomId}\" serif:id=\"{shadowMiddleBottomSerifId}\" d=\"{shadowMiddleBottomAdjusted}\" style=\"{shadowMiddleBottomStyle}\"/>");
        // sb.Append($"<path id=\"{shadowMiddleTopId}\" serif:id=\"{shadowMiddleTopSerifId}\" d=\"{shadowMiddleTopAdjusted}\" style=\"{shadowMiddleTopStyle}\"/>");

        sb.Append("</g></svg>");
        return sb.ToString();
    }

    public IReadOnlyList<(double X, double Y)> GetLabelPositions(int count)
    {
        if (count <= 0) return Array.Empty<(double X, double Y)>();
        var template = TemplateCache.Value;
        IReadOnlyList<double> labelYs = template?.LabelYs ?? new[] { 228.941, 228.941, 228.306 };
        var positions = new List<(double X, double Y)>(count);
        for (int i = 0; i < count; i++)
        {
            double pinStartX = BasePinStartX + i * PinPitch;
            double labelX = pinStartX - LabelOffset;
            double labelY = labelYs[i % labelYs.Count];
            positions.Add((labelX, labelY));
        }
        return positions;
    }

    public (double Width, double Height) GetDimensions(int pinCount)
    {
        if (pinCount < 3)
        {
            pinCount = 3;
        }

        double width = BaseWidth + (pinCount - 3) * PinPitch;
        return (width, BaseHeight);
    }

    private static BusbarTemplate? LoadBusbarTemplate()
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", "szyna prądowa", "szyna prądowa.svg");
            if (!File.Exists(path))
            {
                return null;
            }

            string svg = File.ReadAllText(path);
            var doc = XDocument.Parse(svg);
            var root = doc.Root;
            if (root == null)
            {
                return null;
            }

            var ns = root.Name.Namespace;
            var serifNs = root.GetNamespaceOfPrefix("serif") ?? XNamespace.None;
            var body = root.Descendants(ns + "rect").FirstOrDefault(e => (string?)e.Attribute("id") == "Obudowa");
            var pin = root.Descendants(ns + "path").FirstOrDefault(e => (string?)e.Attribute("id") == "Pin-L1");
            var leftCover = root.Descendants(ns + "path").FirstOrDefault(e => (string?)e.Attribute("id") == "Osłona-Lewa");
            var rightCover = root.Descendants(ns + "path").FirstOrDefault(e => (string?)e.Attribute("id") == "Osłona-Prawa");
            var shadowLeftVertical = root.Descendants(ns + "path").FirstOrDefault(e => (string?)e.Attribute("id") == "cień-5");
            var shadowRightVertical = root.Descendants(ns + "path").FirstOrDefault(e => (string?)e.Attribute("id") == "cień-6");
            var shadowLeftBottom = root.Descendants(ns + "path").FirstOrDefault(e => (string?)e.Attribute("id") == "cień-3");
            var shadowRightBottom = root.Descendants(ns + "path").FirstOrDefault(e => (string?)e.Attribute("id") == "cień-4");
            var shadowMiddleBottom = root.Descendants(ns + "path").FirstOrDefault(e => (string?)e.Attribute("id") == "cień-2");
            var shadowMiddleTop = root.Descendants(ns + "path").FirstOrDefault(e => (string?)e.Attribute("id") == "cień-1");
            var labels = root.Descendants(ns + "text").Take(3).ToList();
            var labelGroups = root.Descendants(ns + "g").Where(e => e.Attribute("transform") != null).ToList();

            if (body == null || pin == null || leftCover == null || rightCover == null)
            {
                return null;
            }

            string viewBox = (string?)root.Attribute("viewBox") ?? $"0 0 {F(BaseWidth)} {F(BaseHeight)}";
            var parts = viewBox.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            double viewBoxWidth = parts.Length >= 4 ? ParseDouble(parts[2]) : BaseWidth;
            double viewBoxHeight = parts.Length >= 4 ? ParseDouble(parts[3]) : BaseHeight;

            var templateBody = new BusbarRect(
                (string?)body.Attribute("id") ?? "Obudowa",
                ParseDouble((string?)body.Attribute("x")),
                ParseDouble((string?)body.Attribute("y")),
                ParseDouble((string?)body.Attribute("width")),
                ParseDouble((string?)body.Attribute("height")),
                (string?)body.Attribute("style") ?? "fill:#ebebeb;stroke:#000;stroke-width:3.04px;");

            var templatePin = new BusbarPath(
                (string?)pin.Attribute("id") ?? "Pin-L1",
                (string?)pin.Attribute(serifNs + "id") ?? "Pin L1",
                (string?)pin.Attribute("d") ?? "",
                (string?)pin.Attribute("style") ?? "fill:#ebebeb;stroke:#000;stroke-width:2.82px;");

            var templateLeft = new BusbarPath(
                (string?)leftCover.Attribute("id") ?? "Osłona-Lewa",
                (string?)leftCover.Attribute(serifNs + "id") ?? "Osłona Lewa",
                (string?)leftCover.Attribute("d") ?? "",
                (string?)leftCover.Attribute("style") ?? "fill:#ebebeb;stroke:#000;stroke-width:3.04px;");

            var templateRight = new BusbarPath(
                (string?)rightCover.Attribute("id") ?? "Osłona-Prawa",
                (string?)rightCover.Attribute(serifNs + "id") ?? "Osłona Prawa",
                (string?)rightCover.Attribute("d") ?? "",
                (string?)rightCover.Attribute("style") ?? "fill:#ebebeb;stroke:#000;stroke-width:2.97px;");

            var templateRoot = new BusbarRootAttributes(
                (string?)root.Attribute("version"),
                (string?)root.Attribute("style"),
                (string?)root.Attribute(XNamespace.Xml + "space"),
                (string?)root.Attribute(XNamespace.Xmlns + "xlink"),
                (string?)root.Attribute(XNamespace.Xmlns + "serif"));

            var templateShadowLeftVertical = new BusbarPath(
                (string?)shadowLeftVertical?.Attribute("id") ?? "cień-5",
                (string?)shadowLeftVertical?.Attribute(serifNs + "id") ?? "cień 5",
                (string?)shadowLeftVertical?.Attribute("d") ?? "M7.423,167.653l0,207.533",
                (string?)shadowLeftVertical?.Attribute("style") ?? "fill:none;stroke:#000;stroke-width:0.67px;");

            var templateShadowRightVertical = new BusbarPath(
                (string?)shadowRightVertical?.Attribute("id") ?? "cień-6",
                (string?)shadowRightVertical?.Attribute(serifNs + "id") ?? "cień 6",
                (string?)shadowRightVertical?.Attribute("d") ?? "M760.577,167.653l0,207.533",
                (string?)shadowRightVertical?.Attribute("style") ?? "fill:none;stroke:#000;stroke-width:0.67px;");

            var templateShadowLeftBottom = new BusbarPath(
                (string?)shadowLeftBottom?.Attribute("id") ?? "cień-3",
                (string?)shadowLeftBottom?.Attribute(serifNs + "id") ?? "cień 3",
                (string?)shadowLeftBottom?.Attribute("d") ?? "M7.154,380.621l51.895,0",
                (string?)shadowLeftBottom?.Attribute("style") ?? "fill:none;stroke:#000;stroke-width:0.67px;");

            var templateShadowRightBottom = new BusbarPath(
                (string?)shadowRightBottom?.Attribute("id") ?? "cień-4",
                (string?)shadowRightBottom?.Attribute(serifNs + "id") ?? "cień 4",
                (string?)shadowRightBottom?.Attribute("d") ?? "M708.681,380.621l51.895,0",
                (string?)shadowRightBottom?.Attribute("style") ?? "fill:none;stroke:#000;stroke-width:0.67px;");

            var templateShadowMiddleBottom = new BusbarPath(
                (string?)shadowMiddleBottom?.Attribute("id") ?? "cień-2",
                (string?)shadowMiddleBottom?.Attribute(serifNs + "id") ?? "cień 2",
                (string?)shadowMiddleBottom?.Attribute("d") ?? "M96.604,372.842l574.792,0",
                (string?)shadowMiddleBottom?.Attribute("style") ?? "fill:none;stroke:#000;stroke-width:0.67px;");

            var templateShadowMiddleTop = new BusbarPath(
                (string?)shadowMiddleTop?.Attribute("id") ?? "cień-1",
                (string?)shadowMiddleTop?.Attribute(serifNs + "id") ?? "cień 1",
                (string?)shadowMiddleTop?.Attribute("d") ?? "M100.096,170.233l574.792,0",
                (string?)shadowMiddleTop?.Attribute("style") ?? "fill:none;stroke:#000;stroke-width:0.67px;");

            double[] defaultLabelXs = { 153.013, 359.171, 571.872 };
            double[] defaultLabelYs = { 228.941, 228.941, 228.306 };
            var labelXs = new List<double>(3);
            var labelYs = new List<double>(3);
            foreach (var label in labels)
            {
                labelXs.Add(ParseDouble(((string?)label.Attribute("x"))?.Replace("px", "", StringComparison.OrdinalIgnoreCase)));
                labelYs.Add(ParseDouble(((string?)label.Attribute("y"))?.Replace("px", "", StringComparison.OrdinalIgnoreCase)));
            }
            while (labelXs.Count < 3)
            {
                labelXs.Add(defaultLabelXs[labelXs.Count]);
            }
            while (labelYs.Count < 3)
            {
                labelYs.Add(defaultLabelYs[labelYs.Count]);
            }
            string labelStyle = labels.Count > 0
                ? (string?)labels[0].Attribute("style") ?? "font-family:'ArialMT', 'Arial', sans-serif;font-size:50px;"
                : "font-family:'ArialMT', 'Arial', sans-serif;font-size:50px;";

            var labelOffsets = new List<double>(3);
            for (int i = 0; i < 3; i++)
            {
                double pinStartX = BasePinStartX + i * PinPitch;
                labelOffsets.Add(pinStartX - labelXs[i]);
            }

            var labelMatrixXs = new List<double>();
            var labelMatrixYs = new List<double>();
            double matrixA = 50;
            double matrixB = 0;
            double matrixC = 0;
            double matrixD = 50;
            foreach (var g in labelGroups.Take(3))
            {
                if (TryParseMatrix((string?)g.Attribute("transform"), out double a, out double b, out double c, out double d, out double e, out double f))
                {
                    matrixA = a;
                    matrixB = b;
                    matrixC = c;
                    matrixD = d;
                    labelMatrixXs.Add(e);
                    labelMatrixYs.Add(f);
                }
            }
            double matrixOffsetX = labelMatrixXs.Count > 0 ? labelMatrixXs[0] - labelXs[0] : 55.615054;
            double matrixOffsetY = labelMatrixYs.Count > 0 ? labelMatrixYs[0] - labelYs[0] : 0;

            return new BusbarTemplate(
                viewBoxWidth,
                viewBoxHeight,
                svg,
                templateRoot,
                templateBody,
                templatePin,
                templateLeft,
                templateRight,
                templateShadowLeftVertical,
                templateShadowRightVertical,
                templateShadowLeftBottom,
                templateShadowRightBottom,
                templateShadowMiddleBottom,
                templateShadowMiddleTop,
                labelXs,
                labelYs,
                labelStyle,
                labelOffsets,
                labelMatrixXs,
                labelMatrixYs,
                matrixA,
                matrixB,
                matrixC,
                matrixD,
                matrixOffsetX,
                matrixOffsetY);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    private static double ParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
        {
            return result;
        }

        return 0;
    }

    private sealed record BusbarTemplate(
        double ViewBoxWidth,
        double ViewBoxHeight,
        string RawSvg,
        BusbarRootAttributes Root,
        BusbarRect Body,
        BusbarPath Pin,
        BusbarPath LeftCover,
        BusbarPath RightCover,
        BusbarPath ShadowLeftVertical,
        BusbarPath ShadowRightVertical,
        BusbarPath ShadowLeftBottom,
        BusbarPath ShadowRightBottom,
        BusbarPath ShadowMiddleBottom,
        BusbarPath ShadowMiddleTop,
        IReadOnlyList<double> LabelXs,
        IReadOnlyList<double> LabelYs,
        string LabelStyle,
        IReadOnlyList<double> LabelOffsets,
        IReadOnlyList<double> LabelMatrixXs,
        IReadOnlyList<double> LabelMatrixYs,
        double LabelMatrixA,
        double LabelMatrixB,
        double LabelMatrixC,
        double LabelMatrixD,
        double LabelMatrixOffsetX,
        double LabelMatrixOffsetY);

    private sealed record BusbarRootAttributes(
        string? Version,
        string? Style,
        string? XmlSpace,
        string? XmlnsXlink,
        string? XmlnsSerif);

    private sealed record BusbarRect(string Id, double X, double Y, double Width, double Height, string Style);

    private sealed record BusbarPath(string Id, string SerifId, string D, string Style);

    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static double GetMoveX(string d, double fallback)
    {
        if (string.IsNullOrWhiteSpace(d))
        {
            return fallback;
        }

        int mIndex = d.IndexOf('M', StringComparison.Ordinal);
        if (mIndex < 0)
        {
            return fallback;
        }

        int start = mIndex + 1;
        int end = start;
        while (end < d.Length && (char.IsDigit(d[end]) || d[end] == '.' || d[end] == '-' || d[end] == '+'))
        {
            end++;
        }

        if (end <= start)
        {
            return fallback;
        }

        if (double.TryParse(d.AsSpan(start, end - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double x))
        {
            return x;
        }
        return fallback;
    }

    private static string ShiftMoveX(string d, double dx)
    {
        if (dx == 0 || string.IsNullOrWhiteSpace(d))
        {
            return d;
        }

        int mIndex = d.IndexOf('M', StringComparison.Ordinal);
        if (mIndex < 0)
        {
            return d;
        }

        int start = mIndex + 1;
        int end = start;
        while (end < d.Length && (char.IsDigit(d[end]) || d[end] == '.' || d[end] == '-' || d[end] == '+'))
        {
            end++;
        }

        if (end <= start)
        {
            return d;
        }

        if (!double.TryParse(d.AsSpan(start, end - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double x))
        {
            return d;
        }

        double nx = x + dx;
        return string.Concat(d.AsSpan(0, start), F(nx), d.AsSpan(end));
    }

    private static string ExtendFirstLineX(string d, double delta)
    {
        if (delta == 0 || string.IsNullOrWhiteSpace(d))
        {
            return d;
        }

        int lIndex = d.IndexOf('l', StringComparison.Ordinal);
        if (lIndex < 0)
        {
            lIndex = d.IndexOf('L', StringComparison.Ordinal);
        }

        if (lIndex < 0)
        {
            return d;
        }

        int start = lIndex + 1;
        int end = start;
        while (end < d.Length && (char.IsDigit(d[end]) || d[end] == '.' || d[end] == '-' || d[end] == '+'))
        {
            end++;
        }

        if (end <= start)
        {
            return d;
        }

        if (!double.TryParse(d.AsSpan(start, end - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double x))
        {
            return d;
        }

        double nx = x + delta;
        return string.Concat(d.AsSpan(0, start), F(nx), d.AsSpan(end));
    }

    private static bool TryParseMatrix(string? transform, out double a, out double b, out double c, out double d, out double e, out double f)
    {
        a = 1;
        b = 0;
        c = 0;
        d = 1;
        e = 0;
        f = 0;

        if (string.IsNullOrWhiteSpace(transform))
        {
            return false;
        }

        int start = transform.IndexOf('(', StringComparison.Ordinal);
        int end = transform.IndexOf(')', StringComparison.Ordinal);
        if (start < 0 || end < 0 || end <= start + 1)
        {
            return false;
        }

        string content = transform.Substring(start + 1, end - start - 1);
        var parts = content.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 6)
        {
            return false;
        }

        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out a)) return false;
        if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out b)) return false;
        if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out c)) return false;
        if (!double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return false;
        if (!double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out e)) return false;
        if (!double.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out f)) return false;
        return true;
    }
}
