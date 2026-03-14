using SkiaSharp;
using E = DINBoard.Services.SchematicLayoutEngine;

namespace DINBoard.Services.Pdf;

public partial class PdfSingleLineDiagramService
{
    /// <summary>
    /// Renderuje szablon strony do obrazu PNG (używane tylko w PDF jako tło warstwy).
    /// </summary>
    private static byte[] RenderPageTemplate()
    {
        float w = (float)E.PageW;
        float h = (float)E.PageH;
        float scale = 4.0f;

        using var surface = SKSurface.Create(new SKImageInfo((int)(w * scale), (int)(h * scale)));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        canvas.Scale(scale);

        DrawPageTemplate(canvas);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }
}
