using SkiaSharp;
using E = DINBoard.Services.SchematicLayoutEngine;

namespace DINBoard.Services.Pdf;

public partial class PdfSingleLineDiagramService
{
    // Colors (PN-EN 60446 / IEC 60446) tuned for light print output.
    static readonly SKColor CL1 = new(139, 69, 19);    // Brown
    static readonly SKColor CL2 = new(30, 30, 30);     // Black
    static readonly SKColor CL3 = new(120, 120, 120);   // Gray
    static readonly SKColor CN  = new(0, 100, 210);     // Blue
    static readonly SKColor CPE = new(0, 150, 80);      // Green
    static readonly SKColor CWire = new(74, 80, 92);
    static readonly SKColor CBus = new(24, 28, 36);
    static readonly SKColor CFR = new(120, 60, 220);
    static readonly SKColor CSPD = new(210, 130, 0);
    static readonly SKColor CRCD = new(140, 50, 210);
    static readonly SKColor CKF = new(170, 130, 20);
    static readonly SKColor CFrame = new(108, 114, 124);
    static readonly SKColor CGrid = new(210, 215, 223);
    static readonly SKColor CGridTxt = new(144, 149, 159);
    static readonly SKColor CPageBg = SKColors.White;
    static readonly SKColor CBoxBg = new(250, 251, 254);
    static readonly SKColor CTxt = new(22, 26, 34);
    static readonly SKColor CTxtDim = new(72, 78, 88);
    static readonly SKColor CTxtLbl = new(96, 101, 111);
    static readonly SKColor CTxtDes = new(28, 30, 38);
    static readonly SKColor CTxtNum = new(66, 72, 82);
    static readonly SKColor CCont = new(180, 140, 30);
    static readonly SKColor CWhite = SKColors.White;
    static readonly SKColor CDarkBg = SKColors.White;

    const float NW = (float)E.NW, NH = (float)E.NH;

    static float Y(float yOff, double relY) => yOff + (float)E.DrawT + (float)relY;
}
