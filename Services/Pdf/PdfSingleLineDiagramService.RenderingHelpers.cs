using SkiaSharp;
using DINBoard.Services;
using E = DINBoard.Services.SchematicLayoutEngine;

namespace DINBoard.Services.Pdf;

public partial class PdfSingleLineDiagramService
{
    private static SKSurface CreatePortraitPageSurface(float pageWidth, float pageHeight, float scale)
    {
        // Portrait output: dimensions swapped before rotating drawing space.
        return SKSurface.Create(new SKImageInfo((int)(pageHeight * scale), (int)(pageWidth * scale)));
    }

    private static float ConfigurePortraitPageCanvas(SKCanvas canvas, float pageHeight, float scale, int pageIndex)
    {
        canvas.Translate(pageHeight * scale, 0);
        canvas.RotateDegrees(90);
        canvas.Scale(scale);

        float yOff = pageIndex * (pageHeight + (float)E.PageGap);
        canvas.Translate(0, -yOff);
        return yOff;
    }

    private static void DrawFullPageLayers(SKCanvas canvas, SchematicLayout lay, int pageIndex, float yOff)
    {
        DrawPageTemplate(canvas, yOff);
        DrawCircuitVectors(canvas, lay, pageIndex, yOff);
        DrawSkiaTable(canvas, lay, pageIndex, yOff);
        DrawSkiaTitleBlock(canvas, lay, pageIndex + 1, lay.TotalPages, yOff);
        DrawCableLabels(canvas, lay, pageIndex, yOff);
    }

    private static byte[] EncodeSurfaceAsPng(SKSurface surface)
    {
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
