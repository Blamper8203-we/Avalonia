using SkiaSharp;
using E = DINBoard.Services.SchematicLayoutEngine;

namespace DINBoard.Services.Pdf;

public partial class PdfSingleLineDiagramService
{
    // â•â•â• Kolory (PN-EN 60446 / IEC 60446) â€” wersja jasna do druku â•â•â•
    static readonly SKColor CL1 = new(139, 69, 19);    // brÄ…zowy
    static readonly SKColor CL2 = new(30, 30, 30);     // czarny
    static readonly SKColor CL3 = new(120, 120, 120);   // szary
    static readonly SKColor CN  = new(0, 100, 210);     // niebieski
    static readonly SKColor CPE = new(0, 150, 80);      // zielony
    static readonly SKColor CWire = new(100, 105, 115);
    static readonly SKColor CBus = new(60, 65, 75);
    static readonly SKColor CFR = new(120, 60, 220);
    static readonly SKColor CSPD = new(210, 130, 0);
    static readonly SKColor CRCD = new(140, 50, 210);
    static readonly SKColor CKF = new(170, 130, 20);
    static readonly SKColor CFrame = new(60, 65, 75);
    static readonly SKColor CGrid = new(180, 185, 195);
    static readonly SKColor CGridTxt = new(130, 135, 145);
    static readonly SKColor CPageBg = SKColors.White;
    static readonly SKColor CBoxBg = new(245, 245, 250);
    static readonly SKColor CTxt = new(30, 35, 45);
    static readonly SKColor CTxtDim = new(90, 95, 105);
    static readonly SKColor CTxtLbl = new(110, 115, 125);
    static readonly SKColor CTxtDes = new(40, 40, 50);
    static readonly SKColor CTxtNum = new(80, 85, 100);
    static readonly SKColor CCont = new(180, 140, 30);
    static readonly SKColor CWhite = SKColors.White;
    static readonly SKColor CDarkBg = SKColors.White;

    const float NW = (float)E.NW, NH = (float)E.NH;

    static float Y(float yOff, double relY) => yOff + (float)E.DrawT + (float)relY;
}
