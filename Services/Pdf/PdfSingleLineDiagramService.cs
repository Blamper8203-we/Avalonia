using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SkiaSharp;
using DINBoard.Models;
using DINBoard.ViewModels;
using DINBoard.Services;
using E = DINBoard.Services.SchematicLayoutEngine;

namespace DINBoard.Services.Pdf;

/// <summary>
/// Generuje schemat jednokreskowy (single-line diagram) do PDF
/// — port SkiaSharp renderera z SingleLineDiagramControl.
/// </summary>
public class PdfSingleLineDiagramService
{
    private readonly IModuleTypeService _moduleTypeService;

    public PdfSingleLineDiagramService(IModuleTypeService moduleTypeService)
    {
        _moduleTypeService = moduleTypeService ?? throw new ArgumentNullException(nameof(moduleTypeService));
    }

    // ═══ Kolory (PN-EN 60446 / IEC 60446) — wersja jasna do druku ═══
    static readonly SKColor CL1 = new(139, 69, 19);    // brązowy
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

    /// <summary>
    /// Renderuje obwody elektryczne schematu (tylko część wektorowa - symbole i kable) do PNG
    /// </summary>
    public static byte[]? RenderCircuitImage(SchematicLayout lay, int pageIndex, MainViewModel viewModel)
    {
        if (lay == null || lay.IsEmpty) return null;

        float pageW = (float)E.DrawW;
        
        // Zmniejszamy wysokość by nie rysować pustego terenu tabelki
        float drawTop = (float)E.DrawT;
        float drawBottom = (float)E.YWireEnd + 50; 
        float actualH = drawBottom - drawTop;

        float scale = 4.0f; // wysoka rozdzielczość do eksportu
        int imgW = Math.Max(1, (int)Math.Round(pageW * scale));
        int imgH = Math.Max(1, (int)Math.Round(actualH * scale));

        using var surface = SKSurface.Create(new SKImageInfo(imgW, imgH));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(scale);
        
        // Przesunięcie lokalne - rysujemy od 0
        canvas.Translate(0, -drawTop);

        DrawCircuitVectors(canvas, lay, pageIndex);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>
    /// Rysuje same wektory obwodów (urządzenia, kable) na podanym płótnie Skia.
    /// Używane zarówno do eksportu PNG (do PDF) jak i do podglądu UI w SkiaRenderControl.
    /// </summary>
    public static void DrawCircuitVectors(SKCanvas canvas, SchematicLayout lay, int pageIndex, float yOffset = 0, bool drawDinRailAxis = true)
    {
        var pi = lay.Pages.Count > pageIndex ? lay.Pages[pageIndex] : null;
        var pageDev = lay.Devices.Where(d => d.Page == pageIndex).ToList();
        
        if (pi != null)
        {
            float pseudoYOff = yOffset;
            DrawMainBus(canvas, pi, pseudoYOff);
            DrawPathNumbers(canvas, lay, pageDev, pseudoYOff);
            foreach (var d in pageDev) DrawDevice(canvas, d, pi, pseudoYOff, drawDinRailAxis);
            DrawNPE(canvas, pi, pseudoYOff);
            DrawCableLabels(canvas, lay, pageIndex, pseudoYOff);

            if (pageIndex < lay.TotalPages - 1) DrawCont(canvas, pi, pseudoYOff, pageIndex + 2, true);
            if (pageIndex > 0) DrawCont(canvas, pi, pseudoYOff, pageIndex, false);
        }
    }

    /// <summary>
    /// Komponuje stronę schematu jednokreskowego w dokumencie QuestPDF (Hybryda: QuestPDF + Skia image).
    /// </summary>
    public void ComposeSingleLineDiagram(IContainer container, MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(viewModel);

        var engine = new SchematicLayoutEngine(_moduleTypeService);
        var layout = engine.BuildLayout(viewModel.Symbols, viewModel.CurrentProject);
        
        if (layout == null || layout.IsEmpty)
        {
            container.AlignCenter().AlignMiddle()
                .Text("Brak obwodów do wyświetlenia na schemacie jednokreskowym")
                .FontSize(10).FontColor("#6B7280");
            return;
        }

        container.Column(col => 
        {
            for (int pg = 0; pg < layout.TotalPages; pg++)
            {
                if (pg > 0)
                {
                    col.Item().PageBreak();
                }
                
                // Renderujemy CAŁĄ stronę jako jeden obraz Skia
                var fullPageImg = RenderFullPage(layout, pg, viewModel);
                if (fullPageImg != null)
                {
                    col.Item().Image(fullPageImg);
                }
            }
        });
    }

    /// <summary>
    /// Renderuje pełną stronę schematu (szablon + obwody + tabelę + tabelkę rysunkową) jako obraz PNG.
    /// </summary>
    private static byte[] RenderFullPage(SchematicLayout lay, int pageIndex, MainViewModel viewModel)
    {
        float w = (float)E.PageW;
        float h = (float)E.PageH;
        float scale = 8.0f; // Zwiększona rozdzielczość dla idealnej ostrości PDF

        // Zamieniamy szerokość z wysokością, by otrzymać obraz w orientacji pionowej (Portrait)
        using var surface = SKSurface.Create(new SKImageInfo((int)(h * scale), (int)(w * scale)));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        // Obrót o 90 stopni w prawo
        canvas.Translate((float)(h * scale), 0);
        canvas.RotateDegrees(90);

        canvas.Scale(scale);

        // Przesunięcie płótna dla kolejnych stron - koordynaty symboli są bezwzględne
        float yOff = pageIndex * (h + (float)E.PageGap);
        canvas.Translate(0, -yOff);

        // 1. Szablon (ramka, siatka)
        DrawPageTemplate(canvas, yOff);

        // 2. Obwody (symbole, szyny, przewody)
        DrawCircuitVectors(canvas, lay, pageIndex, yOff);

        // 3. Tabela obwodów
        DrawSkiaTable(canvas, lay, pageIndex, yOff);

        // 4. Tabelka rysunkowa
        DrawSkiaTitleBlock(canvas, lay, pageIndex + 1, lay.TotalPages, yOff);

        // 5. Legenda symboli
        DrawLegend(canvas, lay, pageIndex, yOff);

        // 6. Etykiety kabli przy przewodach
        DrawCableLabels(canvas, lay, pageIndex, yOff);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private void DrawQuestTitleBlock(IContainer container, SchematicLayout layout, int pageNum, int totalPages)
    {
        var p = layout.Project;
        var meta = p?.Metadata;
        string drawingNum = meta?.ProjectNumber ?? "E-SCH-001";
        string title = GetProjectObjectName(p);
        string desc = p?.Description ?? "Schemat jednokreskowy";
        string date = (meta?.DateModified ?? DateTime.UtcNow).ToString("yyyy-MM-dd");

        // Rozmiar i orientacja tabelki podobna do tej obróconej w Skia (Wąska, Wysoka)
        container.Width(52).BorderLeft(1.2f).BorderTop(1.2f).BorderColor("#3C414B").Column(col => 
        {
            col.Item().Padding(4).Column(c => {
                c.Item().Text("Obiekt:").FontSize(5).FontColor("#6E737D");
                c.Item().Text(title).FontSize(8).Bold();
            });
            col.Item().BorderTop(0.5f).BorderColor("#3C414B").Padding(4).Column(c => {
                c.Item().Text("Opis:").FontSize(5).FontColor("#6E737D");
                c.Item().Text(desc).FontSize(6).FontColor("#5A5F69");
            });
            col.Item().BorderTop(0.5f).BorderColor("#3C414B").Padding(4).Column(c => {
                c.Item().Text("Nr rys.:").FontSize(5).FontColor("#6E737D");
                c.Item().Text(drawingNum).FontSize(8).Bold();
            });
            col.Item().BorderTop(0.5f).BorderColor("#3C414B").Padding(4).Column(c => {
                c.Item().Text("Data:").FontSize(5).FontColor("#6E737D");
                c.Item().Text(date).FontSize(6).FontColor("#5A5F69");
            });
            col.Item().BorderTop(0.5f).BorderColor("#3C414B").Padding(4).Column(c => {
                c.Item().Text("Arkusz:").FontSize(5).FontColor("#6E737D");
                c.Item().Text($"{pageNum} / {totalPages}").FontSize(8).Bold();
            });
            col.Item().BorderTop(0.5f).BorderColor("#3C414B").Padding(2).AlignRight().Text("PN-EN 60617").FontSize(4).FontColor("#6E737D");
        });
    }

    private string CellText(SchematicNode n, string field)
    {
        var sym = n.Symbol;
        switch (field)
        {
            case "Designation": return sym?.ReferenceDesignation ?? n.Designation ?? "";
            case "Protection": return sym?.ProtectionType ?? n.Protection ?? "";
            case "CircuitName": return sym?.CircuitName ?? n.CircuitName ?? "";
            case "Location": return sym?.Location ?? n.Location ?? "";
            case "CableDesig": return (sym?.Parameters != null && sym.Parameters.TryGetValue("CableDesig", out var v1)) ? v1 : (n.CableDesig ?? "");
            case "CableType": return (sym?.Parameters != null && sym.Parameters.TryGetValue("CableType", out var v2)) ? v2 : (n.CableType ?? "");
            case "CableSpec": return (sym?.Parameters != null && sym.Parameters.TryGetValue("CableSpec", out var v3)) ? v3 : (n.CableSpec ?? "");
            case "CableLength": return (sym?.Parameters != null && sym.Parameters.TryGetValue("CableLength", out var v4)) ? v4 : (n.CableLength ?? "");
            case "PowerInfo": return (sym?.Parameters != null && sym.Parameters.TryGetValue("PowerInfo", out var v5)) ? v5 : (n.PowerInfo ?? "");
            default: return "";
        }
    }

    // ═══ KONTYNUACJA MIĘDZY ARKUSZAMI ═══
    static void DrawCont(SKCanvas c, PageInfo pi, float yo, int tgt, bool right)
    {
        float by = Y(yo, E.YMainBus);
        using var pen = Stroke(CCont, 1.2f);
        if (right)
        {
            float x = (float)pi.BusX2 + 4;
            c.DrawLine(x, by, x + 16, by, pen);
            c.DrawLine(x + 12, by - 3, x + 16, by, pen);
            c.DrawLine(x + 12, by + 3, x + 16, by, pen);
            Txt(c, $"→ ark. {tgt}", x, by - 20, 8, CCont, true);
        }
        else
        {
            float x = (float)pi.BusX1 - 4;
            c.DrawLine(x - 16, by, x, by, pen);
            c.DrawLine(x - 12, by - 3, x - 16, by, pen);
            c.DrawLine(x - 12, by + 3, x - 16, by, pen);
            Txt(c, $"← z ark. {tgt}", x - 48, by - 20, 8, CCont, true);
        }
    }

    static void AddLeafNodes(List<SchematicNode> nodes, SchematicNode device)
    {
        if (ShouldReserveHeadSlot(device))
        {
            nodes.Add(CloneDisplayNode(device, GetHeadCellWidth(device)));

            foreach (var child in device.Children)
            {
                nodes.Add(child);
            }

            return;
        }

        if (device.Children.Count > 0)
        {
            foreach (var child in device.Children)
            {
                nodes.Add(child);
            }

            return;
        }

        nodes.Add(device);
    }

    static string GetProjectObjectName(Project? project)
    {
        string? metadataName = project?.Metadata?.Company;
        if (!string.IsNullOrWhiteSpace(metadataName))
        {
            return metadataName;
        }

        return string.IsNullOrWhiteSpace(project?.Name) ? "Rozdzielnica" : project!.Name;
    }

    static string GetContractorName(ProjectMetadata? metadata)
    {
        if (!string.IsNullOrWhiteSpace(metadata?.Contractor))
        {
            return metadata.Contractor!;
        }

        return "---";
    }

    private void DrawQuestTable(IContainer container, List<SchematicNode> devs)
    {
        var nodes = new List<SchematicNode>();
        foreach (var d in devs)
        {
            AddLeafNodes(nodes, d);
        }
        if (nodes.Count == 0) return;

        container.PaddingHorizontal(20).PaddingBottom(10).Table(table =>
        {
            // Kolumna nagłówków wierszy + kolumny urządzeń
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(60); // Nagłówki wierszy
                foreach(var n in nodes)
                {
                    columns.RelativeColumn((float)n.CellWidth);
                }
            });

            // Styling cell helpers
            static IContainer CellH(IContainer c) => c.BorderBottom(0.5f).BorderColor("#B4B9C3").PaddingVertical(3).AlignMiddle();
            static IContainer CellD(IContainer c) => c.BorderBottom(0.5f).BorderColor("#B4B9C3").BorderLeft(0.28f).BorderColor("#B4B9C3").PaddingVertical(3).AlignCenter().AlignMiddle();

            // Wiersz 1: Oznaczenie
            table.Cell().Element(CellH).Text("Oznaczenie").FontSize(7.5f).FontColor("#6E737D");
            foreach(var n in nodes) table.Cell().Element(CellD).Text(CellText(n, "Designation")).FontSize(11.5f).Bold().FontColor("#282832");

            // Wiersz 2: Zabezpieczenie (Zabezp.)
            table.Cell().Element(CellH).Text("Zabezp.").FontSize(7.5f).FontColor("#6E737D");
            foreach(var n in nodes) table.Cell().Element(CellD).Text(CellText(n, "Protection")).FontSize(11.5f).Bold().FontColor("#1E232D");

            // Wiersz 3: Obwód
            table.Cell().Element(CellH).Text("Obwód").FontSize(7.5f).FontColor("#6E737D");
            foreach(var n in nodes) table.Cell().Element(CellD).Text(string.IsNullOrEmpty(CellText(n, "CircuitName")) ? "—" : CellText(n, "CircuitName")).FontSize(11.5f).FontColor("#6E737D");

            // Wiersz 4: Lokalizacja
            table.Cell().Element(CellH).Text("Lokalizacja").FontSize(7.5f).FontColor("#6E737D");
            foreach (var n in nodes) table.Cell().Element(CellD).Text(string.IsNullOrEmpty(CellText(n, "Location")) ? "—" : CellText(n, "Location")).FontSize(11.5f).FontColor("#5A5F69");

            // Separator wyrazisty
            table.Cell().ColumnSpan((uint)(nodes.Count + 1)).BorderBottom(1.5f).BorderColor("#3C414B");

            // Wiersz 5: Kabel
            table.Cell().Element(CellH).Text("Kabel").FontSize(7.5f).FontColor("#6E737D");
            foreach(var n in nodes) 
            {
                string text = "";
                string color = "#282832";
                if(n.NodeType == SchematicNodeType.MainBreaker) { text = "FR"; color = "#783CDC"; }
                else if(n.NodeType == SchematicNodeType.SPD) { text = "SPD"; color = "#D28200"; }
                else if(n.NodeType == SchematicNodeType.PhaseIndicator) { text = "KF"; color = "#AA8214"; }
                else { text = CellText(n, "CableDesig"); }
                
                table.Cell().Element(CellD).Text(text).FontSize(11.5f).Bold().FontColor(color);
            }

            // Wiersz 6: Typ kabla
            table.Cell().Element(CellH).Text("Typ kabla").FontSize(7.5f).FontColor("#6E737D");
            foreach(var n in nodes) table.Cell().Element(CellD).Text(n.NodeType == SchematicNodeType.MainBreaker || n.NodeType == SchematicNodeType.MCB ? CellText(n, "CableType") : CellText(n, "Protection")).FontSize(11.5f).FontColor("#5A5F69");

            // Wiersz 7: Przekrój
            table.Cell().Element(CellH).Text("Przekrój").FontSize(7.5f).FontColor("#6E737D");
            foreach(var n in nodes) table.Cell().Element(CellD).Text(n.NodeType == SchematicNodeType.MainBreaker || n.NodeType == SchematicNodeType.MCB ? (n.NodeType == SchematicNodeType.MainBreaker ? CellText(n, "CableDesig") : CellText(n, "CableSpec")) : "").FontSize(11.5f).FontColor("#1E232D");

            // Wiersz 8: Długość
            table.Cell().Element(CellH).Text("Długość").FontSize(7.5f).FontColor("#6E737D");
            foreach(var n in nodes) table.Cell().Element(CellD).Text(n.NodeType == SchematicNodeType.MCB ? CellText(n, "CableLength") : "").FontSize(11.5f).FontColor("#5A5F69");

            // Wiersz 9: Moc
            table.Cell().Element(CellH).BorderBottom(0).PaddingVertical(3).AlignMiddle().Text("Moc").FontSize(7.5f).FontColor("#6E737D");
            foreach(var n in nodes) table.Cell().Element(c => c.BorderLeft(0.28f).BorderColor("#B4B9C3").PaddingVertical(3).AlignCenter().AlignMiddle()).Text(n.NodeType == SchematicNodeType.MCB ? CellText(n, "PowerInfo") : "").FontSize(11.5f).FontColor("#6E737D");
        });
    }

    // ═══ SZABLON STRONY ZE SKIA (Tło, Siatka, Ramka) ═══

    /// <summary>
    /// Rysuje szablon strony (białe tło, ramkę i siatkę referencyjną) bezpośrednio na podanym płótnie Skia.
    /// Używane zarówno w podglądzie UI (SkiaRenderControl) jak i przy eksporcie do PDF.
    /// </summary>
    public static void DrawPageTemplate(SKCanvas canvas, float yOffset = 0, bool drawGrid = true)
    {
        float fl = (float)E.FrameL;
        float ft = (float)E.FrameT + yOffset;
        float fw = (float)(E.PageW - E.FrameL - E.FrameR);
        float fh = (float)(E.PageH - E.FrameT - E.FrameB);

        // Białe tło strony
        using var bgPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
        canvas.DrawRect(0, yOffset, (float)E.PageW, (float)E.PageH, bgPaint);

        // Ramka główna
        using var framePen = Stroke(CFrame, 1.5f);
        canvas.DrawRect(fl, ft, fw, fh, framePen);
        
        // Ramka dla tytułu
        canvas.DrawRect((float)E.DrawR, (float)(E.PageH - E.FrameB - E.TitleH) + yOffset, (float)E.TitleW, (float)E.TitleH, framePen);

        // Siatka i markery
        using var gridPen = Stroke(CFrame, 0.5f);
        using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 10);
        using var txtPaint = new SKPaint { Color = CFrame, IsAntialias = true };

        // Kolumny (1..8)
        float colSpace = fw / E.GridCols;
        for (int i = 0; i < E.GridCols; i++)
        {
            float cx = fl + i * colSpace;
            if (i > 0)
            {
                canvas.DrawLine(cx, ft, cx, ft + 6, gridPen);
                canvas.DrawLine(cx, ft + fh, cx, ft + fh - 6, gridPen);
            }
            float textW = font.MeasureText((i + 1).ToString());
            canvas.DrawText((i + 1).ToString(), cx + colSpace / 2 - textW / 2, ft - 4, SKTextAlign.Left, font, txtPaint);
            canvas.DrawText((i + 1).ToString(), cx + colSpace / 2 - textW / 2, ft + fh + 12, SKTextAlign.Left, font, txtPaint);
        }

        // Rzędy (A..F)
        float rowSpace = fh / E.GridRows;
        for (int i = 0; i < E.GridRows; i++)
        {
            float cy = ft + i * rowSpace;
            if (i > 0)
            {
                canvas.DrawLine(fl, cy, fl + 6, cy, gridPen);
                canvas.DrawLine(fl + fw, cy, fl + fw - 6, cy, gridPen);
            }
            char letter = (char)('A' + i);
            canvas.DrawText(letter.ToString(), fl + fw + 6, cy + rowSpace / 2 + 4, SKTextAlign.Left, font, txtPaint);
        }
    }

    /// <summary>
    /// Rysuje tabelę obwodów (Oznaczenie, Zabezp., Obwód, etc.) bezpośrednio na kanwie Skia.
    /// Używane w podglądzie UI (SkiaRenderControl).
    /// </summary>
    public static void DrawSkiaTable(SKCanvas canvas, SchematicLayout lay, int pageIndex, float yOffset)
    {
        var devs = lay.Devices.Where(d => d.Page == pageIndex).ToList();
        var nodes = new List<SchematicNode>();
        foreach (var d in devs)
        {
            AddLeafNodes(nodes, d);
        }
        if (nodes.Count == 0) return;

        using var linePen = Stroke(CGrid, 0.5f);
        using var sepPen = Stroke(CFrame, 1.5f);

        // Nagłówki wierszy
        (double yRow, string label)[] headers = {
            (E.YRowDesig,    "Oznaczenie"),
            (E.YRowProt,     "Zabezp."),
            (E.YRowCircuit,  "Obwód"),
            (E.YRowLocation, "Lokalizacja"),
            (E.YRowCable,    "Kabel"),
            (E.YRowCableType,"Typ kabla"),
            (E.YRowCableSpec,"Przekrój"),
            (E.YRowCableLen, "Długość"),
            (E.YRowPower,    "Moc"),
        };
        foreach (var (yRow, label) in headers)
        {
            float ry = Y(yOffset, yRow);
            Txt(canvas, label, (float)E.DrawL + 4, ry + 2, (float)E.HeaderFontSize, CTxtLbl);
            canvas.DrawLine((float)E.DrawL, ry + (float)E.RowH, (float)E.DrawR, ry + (float)E.RowH, linePen);
        }

        // Separator (grubsza linia między Location a Cable)
        float sepY = Y(yOffset, E.YRowSep);
        canvas.DrawLine((float)E.DrawL, sepY, (float)E.DrawR, sepY, sepPen);

        // Kolumny tabeli — pionowe linie oddzielające komórki
        foreach (var n in nodes)
        {
            float cx = (float)(n.X + E.NW / 2);
            float cw = (float)n.CellWidth;
            float cellL = cx - cw / 2f;
            float cellR = cx + cw / 2f;

            // Pionowa linia oddzielająca
            canvas.DrawLine(cellL, Y(yOffset, E.YRowDesig), cellL, Y(yOffset, E.YTableEnd), linePen);

            // Wartości w komórkach
            string desig = n.Symbol?.ReferenceDesignation ?? n.Designation ?? "";
            string prot = n.Symbol?.ProtectionType ?? n.Protection ?? "";
            string circuit = n.Symbol?.CircuitName ?? n.CircuitName ?? "";
            string location = n.Symbol?.Location ?? n.Location ?? "";
            string cableDesig = n.CableDesig ?? "";
            string cableType = n.CableType ?? "";
            string cableSpec = n.CableSpec ?? "";
            string cableLen = n.CableLength ?? "";
            string power = n.PowerInfo ?? "";

            // Specjalne oznaczenia
            if (n.NodeType == SchematicNodeType.MainBreaker) { cableDesig = "FR"; }
            else if (n.NodeType == SchematicNodeType.SPD) { cableDesig = "SPD"; }
            else if (n.NodeType == SchematicNodeType.PhaseIndicator) { cableDesig = "KF"; }

            TblCell(canvas, desig, cellL, Y(yOffset, E.YRowDesig), cw, CTxtDes, true);
            TblCell(canvas, prot, cellL, Y(yOffset, E.YRowProt), cw, CTxt, true);
            TblCell(canvas, string.IsNullOrEmpty(circuit) ? "—" : circuit, cellL, Y(yOffset, E.YRowCircuit), cw, CTxtDim);
            TblCell(canvas, string.IsNullOrEmpty(location) ? "—" : location, cellL, Y(yOffset, E.YRowLocation), cw, CTxtDim);
            TblCell(canvas, cableDesig, cellL, Y(yOffset, E.YRowCable), cw, CTxtDes, true);
            TblCell(canvas, cableType, cellL, Y(yOffset, E.YRowCableType), cw, CTxtDim);
            TblCell(canvas, cableSpec, cellL, Y(yOffset, E.YRowCableSpec), cw, CTxt);
            TblCell(canvas, cableLen, cellL, Y(yOffset, E.YRowCableLen), cw, CTxtDim);
            TblCell(canvas, power, cellL, Y(yOffset, E.YRowPower), cw, CTxtDim);
        }
    }

    /// <summary>
    /// Rysuje tabelkę rysunkową (title block) bezpośrednio na kanwie Skia.
    /// Używane w podglądzie UI (SkiaRenderControl).
    /// </summary>
    public static void DrawSkiaTitleBlock(SKCanvas canvas, SchematicLayout lay, int pageNum, int totalPages, float yOffset, bool showPageNumbers = true)
    {
        // ... ramka tytułowa ...
        float tbW = (float)E.TitleW;
        float tbH = (float)E.TitleH;
        float tbX = (float)E.PageW - (float)E.FrameR - tbW;
        float tbY = (float)E.PageH - (float)E.FrameB - tbH + yOffset; // Apply yOffset here

        // Tło
        using var bg = new SKPaint { Color = CBoxBg, Style = SKPaintStyle.Fill };
        canvas.DrawRect(tbX, tbY, tbW, tbH, bg);

        // Ramka
        using var border = Stroke(CFrame, 0.8f);
        canvas.DrawRect(tbX, tbY, tbW, tbH, border);

        // Podział na sekcje pionowe (obrócone — tekst jest wąski)
        float rowH = tbH / 6f; // Zmieniamy podział z 5 na 6 komórek żeby upchnąć Inwestora/Wykonawcę
        for (int i = 1; i < 6; i++)
        {
            float ly = tbY + i * rowH;
            canvas.DrawLine(tbX, ly, tbX + tbW, ly, border);
        }

        var p = lay.Project;
        var meta = p?.Metadata;
        string drawNum = meta?.ProjectNumber ?? "E-SCH-001";
        string title = GetProjectObjectName(p);
        string contractor = GetContractorName(meta);
        string investor = meta?.Investor ?? "---";
        string address = meta?.Address ?? "---";
        string date = (meta?.DateModified ?? DateTime.UtcNow).ToString("yyyy-MM-dd");
        string sheet = $"{pageNum} / {totalPages}";
        string designer = meta?.Author ?? "---";

        // Sekcja 1: Tytuł, Obiekt i Adres
        Txt(canvas, title, tbX + 3, tbY + 2, 6, CTxtLbl, true);
        Txt(canvas, $"Inwestor: {investor}", tbX + 3, tbY + 12, 4, CTxtDim);
        Txt(canvas, $"Adres: {address}", tbX + 3, tbY + 20, 4, CTxtDim);

        // Sekcja 2: Twórca / Wykonawca
        float s2Y = tbY + rowH;
        Txt(canvas, "Wykonawca:", tbX + 3, s2Y + 2, 4, CTxtLbl);
        Txt(canvas, contractor, tbX + 3, s2Y + 10, 5, CTxt, true);
        Txt(canvas, $"Projektant: {designer}", tbX + 3, s2Y + 20, 4, CTxtDim);

        // Sekcja 3: Nr rysunku
        float s3Y = tbY + 2 * rowH;
        Txt(canvas, "Nr rys.:", tbX + 3, s3Y + 2, 5, CTxtLbl);
        Txt(canvas, drawNum, tbX + 3, s3Y + 12, 6, CTxt, true);

        // Sekcja 4: Data
        float s4Y = tbY + 3 * rowH;
        Txt(canvas, "Data:", tbX + 3, s4Y + 2, 5, CTxtLbl);
        Txt(canvas, date, tbX + 3, s4Y + 12, 6, CTxtDim);

        // Sekcja 5: Arkusz
        float s5Y = tbY + 4 * rowH;
        Txt(canvas, "Arkusz:", tbX + 3, s5Y + 2, 5, CTxtLbl);
        if (showPageNumbers)
        {
            Txt(canvas, sheet, tbX + 3, s5Y + 12, 7, CTxt, true);
        }

        // Sekcja 6: Norma
        float s6Y = tbY + 5 * rowH;
        Txt(canvas, "PN-EN 60617", tbX + 3, s6Y + 6, 5, CTxtLbl);
    }

    // ═══ LEGENDA SYMBOLI ═══
    static void DrawLegend(SKCanvas c, SchematicLayout lay, int pageIndex, float yo)
    {
        var devs = lay.Devices.Where(d => d.Page == pageIndex).ToList();
        var types = new HashSet<SchematicNodeType>();
        foreach (var d in devs)
        {
            types.Add(d.NodeType);
            if (d.Children.Count > 0)
                foreach (var ch in d.Children) types.Add(ch.NodeType);
        }

        var items = new List<(string sym, string desc, SKColor clr)>();
        if (types.Contains(SchematicNodeType.MainBreaker)) items.Add(("FR", "Wyłącznik główny", CFR));
        if (types.Contains(SchematicNodeType.RCD)) items.Add(("RCD", "Wyłącznik różnicowoprądowy", CRCD));
        if (types.Contains(SchematicNodeType.MCB)) items.Add(("MCB", "Wyłącznik nadprądowy", CWire));
        if (types.Contains(SchematicNodeType.SPD)) items.Add(("SPD", "Ogranicznik przepięć", CSPD));
        if (types.Contains(SchematicNodeType.PhaseIndicator)) items.Add(("KF", "Kontrolka fazy", CKF));

        if (items.Count == 0) return;

        // Pozycja: pod tabelką rysunkową, przy prawej krawędzi
        float legX = (float)E.DrawR;
        float legW = (float)E.TitleW;
        float tbBottom = (float)(E.PageH - E.FrameB - E.TitleH) + yo;
        float legY = tbBottom - (items.Count * 16 + 20); // nad tabelką rysunkową
        float rowHt = 16f;

        // Tło + ramka
        using var bg = new SKPaint { Color = CBoxBg, Style = SKPaintStyle.Fill };
        using var border = Stroke(CFrame, 0.5f);
        float legH = items.Count * rowHt + 18;
        c.DrawRect(legX, legY, legW, legH, bg);
        c.DrawRect(legX, legY, legW, legH, border);

        // Nagłówek
        Txt(c, "LEGENDA", legX + 3, legY + 2, 5.5f, CTxtLbl, true);

        // Elementy
        for (int i = 0; i < items.Count; i++)
        {
            var (sym, desc, clr) = items[i];
            float ry = legY + 14 + i * rowHt;
            using var dot = new SKPaint { Color = clr, Style = SKPaintStyle.Fill, IsAntialias = true };
            c.DrawCircle(legX + 8, ry + 5, 3, dot);
            Txt(c, sym, legX + 14, ry, 5.5f, CTxt, true);
            Txt(c, desc, legX + 14, ry + 7, 4, CTxtDim);
        }
    }

    // ═══ ETYKIETY KABLI PRZY PRZEWODACH ═══
    static void DrawCableLabels(SKCanvas c, SchematicLayout lay, int pageIndex, float yo)
    {
        var devs = lay.Devices.Where(d => d.Page == pageIndex).ToList();
        foreach (var d in devs)
        {
            if (d.Children.Count > 0)
            {
                foreach (var ch in d.Children)
                    DrawSingleCableLabel(c, ch, yo);
            }
            else if (d.NodeType == SchematicNodeType.MCB)
            {
                DrawSingleCableLabel(c, d, yo);
            }
        }
    }

    static void DrawSingleCableLabel(SKCanvas c, SchematicNode n, float yo)
    {
        string spec = n.CableSpec ?? "";
        string cableType = n.CableType ?? "";
        if (string.IsNullOrEmpty(spec) && string.IsNullOrEmpty(cableType)) return;

        string label = !string.IsNullOrEmpty(spec) ? spec : cableType;
        float cx = (float)n.X + NW / 2;
        float wireEnd = Y(yo, E.YWireEnd);
        float mcbBottom = (float)n.Y + NH + 8; // Odrobinę pod urządzeniem

        // Rysujemy estetyczny marker kabla: "YDYp 3x2.5" obok przewodu obwodu
        using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 8.5f);
        using var paint = new SKPaint { Color = CTxtDes, IsAntialias = true };
        
        // Pionowy tekst wzdłuż linii
        c.Save();
        c.Translate(cx + 8, mcbBottom + 10);
        c.RotateDegrees(-90);
        c.DrawText(label, 0, 0, SKTextAlign.Right, font, paint);
        c.Restore();
    }

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

    // ═══ NUMERY ŚCIEŻEK ═══
    static void DrawPathNumbers(SKCanvas c, SchematicLayout lay, List<SchematicNode> devs, float yo)
    {
        int n = 1;
        foreach (var d in lay.Devices)
        {
            if (devs.Contains(d)) break;
            n += GetVisualSlotCount(d);
        }
        foreach (var d in devs)
        {
            if (ShouldReserveHeadSlot(d))
            {
                DrawPL(c, (float)d.X + NW / 2, ref n, yo, -10f);
                foreach (var ch in d.Children) DrawPL(c, (float)ch.X + NW / 2, ref n, yo);
            }
            else if (d.Children.Count > 0)
                foreach (var ch in d.Children) DrawPL(c, (float)ch.X + NW / 2, ref n, yo);
            else DrawPL(c, (float)d.X + NW / 2, ref n, yo);
        }
    }

    static void DrawPL(SKCanvas c, float cx, ref int n, float yo, float labelOffsetX = 0)
    {
        DrawPathNumberLabel(c, n.ToString(), cx + labelOffsetX, Y(yo, E.YPathNums) - 2);
        using var gp = Stroke(CGrid, 0.4f);
        gp.PathEffect = SKPathEffect.CreateDash(new[] { 2f, 2f }, 0); // Kreskowana linia prowadząca ścieżkę
        c.DrawLine(cx, Y(yo, E.YPathNums + 8), cx, Y(yo, E.YLabelTop), gp);
        n++;
    }

    // ═══ SZYNA GŁÓWNA ═══
    static void DrawMainBus(SKCanvas c, PageInfo pi, float yo)
    {
        using var pen = Stroke(CBus, 3.5f);
        c.DrawLine((float)pi.BusX1, Y(yo, E.YMainBus), (float)pi.BusX2, Y(yo, E.YMainBus), pen);
    }

    // ═══ URZĄDZENIA ═══
    static void DrawDevice(SKCanvas c, SchematicNode n, PageInfo pi, float yo, bool drawDinRailAxis = true)
    {
        float cx = (float)n.X + NW / 2, by = Y(yo, E.YMainBus);
        switch (n.NodeType)
        {
            case SchematicNodeType.MainBreaker:
                if (n.Children.Count > 0)
                {
                    DrawGroupedMainBreaker(c, n, pi, yo, drawDinRailAxis);
                    break;
                }

                string supplyLabel = GetSupplyLabel(n.PhaseCount);
                DrawWireLine(c, cx, Y(yo, E.YSupply), (float)n.Y, CFR, 1.8f);
                PhaseMarks(c, cx, Y(yo, E.YSupply) + ((float)n.Y - Y(yo, E.YSupply)) / 2, n.PhaseCount, n.Phase, hasNeutral: true);
                Txt(c, supplyLabel, cx - 38, Y(yo, E.YSupply) + ((float)n.Y - Y(yo, E.YSupply)) / 2 - 2, 8, CTxtDim);
                SymBox(c, (float)n.X, (float)n.Y, CFR);
                SymFR(c, cx, (float)n.Y + NH / 2, CFR);
                Txt(c, n.Designation, cx + 12, (float)n.Y + 25, 9, CTxtDes, true);
                DrawWireLine(c, cx, (float)n.Y + NH, by, CWire, 1.2f);
                PhaseMarks(c, cx, (float)n.Y + NH + (by - ((float)n.Y + NH)) / 2, n.PhaseCount, n.Phase, hasNeutral: true);
                DrawDot(c, cx, by, CWire, 2.5f);
                break;

            case SchematicNodeType.PhaseIndicator:
                WireDot(c, cx, by, (float)n.Y);
                PhaseMarks(c, cx, by + ((float)n.Y - by) / 2, n.PhaseCount, n.Phase);
                SymBox(c, (float)n.X, (float)n.Y, CKF);
                SymKF(c, cx, (float)n.Y + NH / 2);
                Txt(c, n.Designation, cx + 12, (float)n.Y + 25, 8.5f, CTxtDes, true);
                TxtR(c, n.Protection, cx - 12, (float)n.Y + NH / 2 + 5, 7.5f, CTxtDim);
                break;

            case SchematicNodeType.SPD:
                WireDot(c, cx, by, (float)n.Y);
                PhaseMarks(c, cx, by + ((float)n.Y - by) / 2, n.PhaseCount, n.Phase, hasNeutral: true);
                SymBox(c, (float)n.X, (float)n.Y, CSPD);
                SymSPD(c, cx, (float)n.Y + NH / 2);
                Txt(c, n.Designation, cx + 12, (float)n.Y + 25, 8.5f, CTxtDes, true);
                SymGround(c, cx, (float)n.Y + NH + 3);
                DrawTextCenteredInBox(c, n.Protection, cx - 35, (float)n.Y + NH + 24, 70, 7, CTxtDim);
                break;

            case SchematicNodeType.RCD:
                DrawRCD(c, n, pi, yo, drawDinRailAxis);
                break;

            case SchematicNodeType.MCB:
                WireDot(c, cx, by, (float)n.Y);
                PhaseMarks(c, cx, by + ((float)n.Y - by) / 2, n.PhaseCount, n.Phase);
                DrawMCB(c, n, (float)n.Y, yo, drawDinRailAxis);
                break;
        }
    }

    static void DrawGroupedMainBreaker(SKCanvas c, SchematicNode breaker, PageInfo pi, float yo, bool drawDinRailAxis = true)
    {
        float cx = (float)breaker.X + NW / 2;
        string supplyLabel = GetSupplyLabel(breaker.PhaseCount);
        DrawWireLine(c, cx, Y(yo, E.YSupply), (float)breaker.Y, CFR, 1.8f);
        PhaseMarks(c, cx, Y(yo, E.YSupply) + ((float)breaker.Y - Y(yo, E.YSupply)) / 2, breaker.PhaseCount, breaker.Phase, hasNeutral: true);
        Txt(c, supplyLabel, cx - 38, Y(yo, E.YSupply) + ((float)breaker.Y - Y(yo, E.YSupply)) / 2 - 2, 8, CTxtDim);
        SymBox(c, (float)breaker.X, (float)breaker.Y, CFR);
        SymFR(c, cx, (float)breaker.Y + NH / 2, CFR);
        Txt(c, breaker.Designation, cx + 12, (float)breaker.Y + 25, 9, CTxtDes, true);
        TxtR(c, breaker.Protection, cx - 18, (float)breaker.Y + NH / 2 + 5, 7.5f, CTxtDim);

        if (breaker.Children.Count == 0) return;

        bool hasDistributionBlock = breaker.DistributionBlockSymbol != null;
        bool useMainBusAsDistribution = breaker.PhaseCount == 1;
        float by = Y(yo, E.YMainBus);
        float gY = useMainBusAsDistribution ? by : (hasDistributionBlock ? Y(yo, E.YGroupBus + 22) : Y(yo, E.YGroupBus));
        float f = (float)breaker.Children[0].X + NW / 2;
        float l = (float)breaker.Children[^1].X + NW / 2;
        if (useMainBusAsDistribution)
        {
            DrawWireLine(c, cx, (float)breaker.Y + NH, by, CWire, 1.2f);
            PhaseMarks(c, cx, (float)breaker.Y + NH + (by - ((float)breaker.Y + NH)) / 2, breaker.PhaseCount, breaker.Phase);
            DrawDot(c, cx, by, CWire, 2.5f);
            DrawDistributionBlockLabel(c, breaker, f - NW / 2 + 4f, by - 10f);
        }
        else if (hasDistributionBlock)
        {
            float blockW = 52f;
            float blockH = 18f;
            float blockTop = gY - blockH - 18f;
            float blockLeft = cx - blockW / 2f;

            DrawWireLine(c, cx, (float)breaker.Y + NH, blockTop, CWire, 1.2f);
            PhaseMarks(c, cx, (float)breaker.Y + NH + (blockTop - ((float)breaker.Y + NH)) / 2, breaker.PhaseCount, breaker.Phase);
            DrawDistributionBlock(c, breaker, blockLeft, blockTop, blockW, blockH);
            WireDot(c, cx, blockTop + blockH, gY);
        }
        else
        {
            WireDot(c, cx, (float)breaker.Y + NH, gY);
            PhaseMarks(c, cx, (float)breaker.Y + NH + (gY - ((float)breaker.Y + NH)) / 2, breaker.PhaseCount, breaker.Phase);
        }

        if (!useMainBusAsDistribution)
        {
            using var gBus = Stroke(CWire, 2.2f);
            c.DrawLine(f - 8, gY, l + 8, gY, gBus);
        }

        foreach (var ch in breaker.Children)
        {
            float chCx = (float)ch.X + NW / 2;
            WireDot(c, chCx, gY, (float)ch.Y);
            bool childHasN = ch.NodeType == SchematicNodeType.SPD;
            PhaseMarks(c, chCx, gY + ((float)ch.Y - gY) / 2, ch.PhaseCount, ch.Phase, hasNeutral: childHasN);
            switch (ch.NodeType)
            {
                case SchematicNodeType.MCB:
                    DrawMCB(c, ch, (float)ch.Y, yo, drawDinRailAxis);
                    break;
                case SchematicNodeType.SPD:
                    SymBox(c, (float)ch.X, (float)ch.Y, CSPD);
                    SymSPD(c, chCx, (float)ch.Y + NH / 2);
                    Txt(c, ch.Designation, chCx + 12, (float)ch.Y + 25, 8, CTxtDes, true);
                    SymGround(c, chCx, (float)ch.Y + NH + 3);
                    DrawTextCenteredInBox(c, ch.Protection, chCx - 35, (float)ch.Y + NH + 22, 70, 6.5f, CTxtDim);
                    break;
                case SchematicNodeType.PhaseIndicator:
                    SymBox(c, (float)ch.X, (float)ch.Y, CKF);
                    SymKF(c, chCx, (float)ch.Y + NH / 2);
                    Txt(c, ch.Designation, chCx + 12, (float)ch.Y + 25, 8, CTxtDes, true);
                    TxtR(c, ch.Protection, chCx - 12, (float)ch.Y + NH / 2 + 5, 7, CTxtDim);
                    break;
            }
        }
    }

    static void DrawDistributionBlock(SKCanvas c, SchematicNode node, float x, float y, float width, float height)
    {
        using var fill = new SKPaint { Color = CBoxBg, Style = SKPaintStyle.Fill, IsAntialias = true };
        using var stroke = Stroke(CBus, 1.2f);
        c.DrawRoundRect(new SKRoundRect(new SKRect(x, y, x + width, y + height), 4, 4), fill);
        c.DrawRoundRect(new SKRoundRect(new SKRect(x, y, x + width, y + height), 4, 4), stroke);

        float cx = x + width / 2f;
        DrawDot(c, cx, y, CWire, 2.2f);
        DrawDot(c, cx, y + height, CWire, 2.2f);

        string label = GetDistributionBlockLabel(node);
        DrawTextCentered(c, label, cx, y + height / 2f + 3, 7, CTxtDim, true);
    }

    static void DrawDistributionBlockLabel(SKCanvas c, SchematicNode node, float x, float y)
    {
        string label = GetDistributionBlockLabel(node);
        Txt(c, label, x, y - 7f, 7, CTxtDim, true);
    }

    static string GetSupplyLabel(int phaseCount) => phaseCount >= 3 ? "3~ 400V" : "1~ 230V";

    static int GetVisualSlotCount(SchematicNode node)
    {
        if (ShouldReserveHeadSlot(node))
        {
            return node.Children.Count + 1;
        }

        return node.Children.Count > 0 ? node.Children.Count : 1;
    }

    static bool ShouldReserveHeadSlot(SchematicNode node)
        => node.NodeType == SchematicNodeType.MainBreaker && node.Children.Count > 0;

    static double GetHeadCellWidth(SchematicNode node)
    {
        double childWidth = node.Children.Sum(child => child.CellWidth);
        double headWidth = node.CellWidth - childWidth;
        return headWidth > 0 ? headWidth : node.CellWidth;
    }

    static SchematicNode CloneDisplayNode(SchematicNode node, double cellWidth)
    {
        return new SchematicNode
        {
            NodeType = node.NodeType,
            Symbol = node.Symbol,
            DistributionBlockSymbol = node.DistributionBlockSymbol,
            Designation = node.Designation,
            Protection = node.Protection,
            CircuitName = node.CircuitName,
            CableDesig = node.CableDesig,
            CableType = node.CableType,
            CableSpec = node.CableSpec,
            CableLength = node.CableLength,
            PowerInfo = node.PowerInfo,
            Phase = node.Phase,
            PhaseCount = node.PhaseCount,
            Location = node.Location,
            X = node.X,
            Y = node.Y,
            Width = node.Width,
            Height = node.Height,
            Column = node.Column,
            Page = node.Page,
            CellWidth = cellWidth
        };
    }

    static string GetDistributionBlockLabel(SchematicNode node)
    {
        string? label = node.DistributionBlockSymbol?.Label;
        if (string.IsNullOrWhiteSpace(label) || label.Contains("blok", StringComparison.OrdinalIgnoreCase))
        {
            return "BIAS";
        }

        return label;
    }

    static void DrawRCD(SKCanvas c, SchematicNode rcd, PageInfo pi, float yo, bool drawDinRailAxis = true)
    {
        float cx = (float)rcd.X + NW / 2, by = Y(yo, E.YMainBus);
        WireDot(c, cx, by, (float)rcd.Y);
        PhaseMarks(c, cx, by + ((float)rcd.Y - by) / 2, rcd.PhaseCount, rcd.Phase, hasNeutral: true);
        SymBox(c, (float)rcd.X, (float)rcd.Y, CRCD);
        SymRCD(c, cx, (float)rcd.Y + NH / 2);
        Txt(c, rcd.Designation, cx + 12, (float)rcd.Y + 25, 9, CTxtDes, true);
        TxtR(c, rcd.Protection, cx - 22, (float)rcd.Y + NH / 2 + 5, 7.5f, CRCD);

        if (rcd.Children.Count == 0) return;

        float gY = Y(yo, E.YGroupBus);
        float f = (float)rcd.Children[0].X + NW / 2;
        float l = (float)rcd.Children[^1].X + NW / 2;
        WireDot(c, cx, (float)rcd.Y + NH, gY);
        PhaseMarks(c, cx, (float)rcd.Y + NH + (gY - ((float)rcd.Y + NH)) / 2, rcd.PhaseCount, rcd.Phase, hasNeutral: true);
        using var gBus = Stroke(CWire, 2.2f);
        c.DrawLine(f - 8, gY, l + 8, gY, gBus);

        foreach (var ch in rcd.Children)
        {
            float chCx = (float)ch.X + NW / 2;
            WireDot(c, chCx, gY, (float)ch.Y);
            bool childHasN = ch.NodeType == SchematicNodeType.SPD;
            PhaseMarks(c, chCx, gY + ((float)ch.Y - gY) / 2, ch.PhaseCount, ch.Phase, hasNeutral: childHasN);
            switch (ch.NodeType)
            {
                case SchematicNodeType.MCB:
                    DrawMCB(c, ch, (float)ch.Y, yo, drawDinRailAxis);
                    break;
                case SchematicNodeType.SPD:
                    SymBox(c, (float)ch.X, (float)ch.Y, CSPD);
                    SymSPD(c, chCx, (float)ch.Y + NH / 2);
                    Txt(c, ch.Designation, chCx + 12, (float)ch.Y + 25, 8, CTxtDes, true);
                    SymGround(c, chCx, (float)ch.Y + NH + 3);
                    DrawTextCenteredInBox(c, ch.Protection, chCx - 35, (float)ch.Y + NH + 22, 70, 6.5f, CTxtDim);
                    break;
                case SchematicNodeType.PhaseIndicator:
                    SymBox(c, (float)ch.X, (float)ch.Y, CKF);
                    SymKF(c, chCx, (float)ch.Y + NH / 2);
                    Txt(c, ch.Designation, chCx + 12, (float)ch.Y + 25, 8, CTxtDes, true);
                    TxtR(c, ch.Protection, chCx - 12, (float)ch.Y + NH / 2 + 5, 7, CTxtDim);
                    break;
            }
        }
    }

    // ═══ MCB ═══
    static void DrawMCB(SKCanvas c, SchematicNode n, float y, float yo, bool drawDinRailAxis = true)
    {
        var ph = PhClr(n.Phase);
        float cx = (float)n.X + NW / 2, cy = y + NH / 2;
        
        if (drawDinRailAxis)
        {
            using var dinAxisPen = Stroke(CGrid, 0.4f);
            dinAxisPen.PathEffect = SKPathEffect.CreateDash(new[] { 5f, 5f }, 0);
            c.DrawLine(cx - NW / 2 - 4, cy, cx + NW / 2 + 4, cy, dinAxisPen);
        }

        SymBox(c, (float)n.X, y, ph);

        SymMCB(c, cx, cy, ph);

        // PhaseBadge(c, n.Phase, cx - 20, y + 24); // Usunięto znaczki faz na prośbę użytkownika
        Txt(c, n.Designation, cx + 12, y + 25, 8.5f, CTxtDes, true);

        DrawWireLine(c, cx, y + NH, Y(yo, E.YWireEnd), CWire, 1.2f);
        PhaseMarks(c, cx, y + NH + (Y(yo, E.YWireEnd) - (y + NH)) / 2, n.PhaseCount, n.Phase);
    }

    // Usunięto DrawInfoTable, CellText, DrawNodeTblData z logiki Skia.
    // Od teraz formatowane są przez metody DrawQuestTable w QuestPDF.

    // ═══ N / PE ═══
    static void DrawNPE(SKCanvas c, PageInfo pi, float yo)
    {
        using var nPen = Stroke(CN, 1.4f);
        c.DrawLine((float)pi.BusX1, Y(yo, E.YN), (float)pi.BusX2, Y(yo, E.YN), nPen);
        Txt(c, "N", (float)pi.BusX1 - 16, Y(yo, E.YN) - 4, 9, CN, true);

        using var pePen = new SKPaint { Color = CPE, Style = SKPaintStyle.Stroke, StrokeWidth = 1.4f, PathEffect = SKPathEffect.CreateDash(new[] { 6f, 3f }, 0), IsAntialias = true };
        c.DrawLine((float)pi.BusX1, Y(yo, E.YPE), (float)pi.BusX2, Y(yo, E.YPE), pePen);
        Txt(c, "PE", (float)pi.BusX1 - 22, Y(yo, E.YPE) - 4, 9, CPE, true);
    }

    // ═══ SYMBOLE IEC (zoptymalizowane pod proporcje 300x350) ═══
    static float px(float val) => (val / 300f) * NW;
    static float py(float val) => (val / 350f) * NH;
    static void DrawP(SKCanvas c, SKColor clr, float w, params float[] l)
    {
        using var p = Stroke(clr, w);
        for(int i = 0; i < l.Length; i+=4)
            c.DrawLine(l[i], l[i+1], l[i+2], l[i+3], p);
    }
    
    static void SymFR(SKCanvas b, float cx, float cy, SKColor clr)
    {
        float x(float v) => cx - NW/2f + px(v);
        float y(float v) => cy - NH/2f + py(v);
        
        DrawP(b, clr, 0.6f, 
            x(150), y(0), x(150), y(120),
            x(144), y(119), x(156), y(119),
            x(150), y(180), x(125), y(125),
            x(150), y(180), x(150), y(350)
        );
        using var p = Stroke(clr, 0.6f); b.DrawCircle(x(150), y(126), px(6), p);
        using var f = new SKPaint { Color = clr, Style = SKPaintStyle.Fill, IsAntialias = true }; b.DrawCircle(x(150), y(180), px(3), f);
    }

    static void SymMCB(SKCanvas b, float cx, float cy, SKColor clr)
    {
        float x(float v) => cx - NW/2f + px(v);
        float y(float v) => cy - NH/2f + py(v);
        
        DrawP(b, clr, 0.6f, 
            x(150), y(0), x(150), y(120),
            x(144), y(120), x(156), y(120),
            x(150), y(180), x(125), y(125),
            x(144), y(102), x(156), y(114),
            x(144), y(114), x(156), y(102),
            x(150), y(180), x(150), y(350)
        );
        using var f = new SKPaint { Color = clr, Style = SKPaintStyle.Fill, IsAntialias = true }; b.DrawCircle(x(150), y(180), px(3), f);
    }

    static void SymRCD(SKCanvas b, float cx, float cy)
    {
        float x(float v) => cx - NW/2f + px(v);
        float y(float v) => cy - NH/2f + py(v);
        SKColor clr = CRCD;
        
        DrawP(b, clr, 0.6f, 
            x(150), y(0), x(150), y(120),
            x(150), y(180), x(125), y(120),
            x(150), y(180), x(150), y(350)
        );
        // Przekładnik
        using var p = Stroke(clr, 0.6f);
        b.DrawOval(x(150), y(230), px(25), py(12), p);
        
        using var f = new SKPaint { Color = clr, Style = SKPaintStyle.Fill, IsAntialias = true }; 
        b.DrawCircle(x(150), y(120), px(3), f);
        b.DrawCircle(x(150), y(180), px(3), f);
        
        // Dashed lines
        using var dash = new SKPaint { Color = clr, Style = SKPaintStyle.Stroke, StrokeWidth = 0.4f, StrokeCap = SKStrokeCap.Butt, StrokeJoin = SKStrokeJoin.Miter, PathEffect = SKPathEffect.CreateDash(new[] { 2f, 2f }, 0), IsAntialias = true };
        b.DrawLine(x(125), y(230), x(100), y(230), dash);
        b.DrawLine(x(100), y(230), x(100), y(150), dash);
        b.DrawLine(x(100), y(150), x(135), y(150), dash);

        Txt(b, "IΔ", x(185), y(235), 5f, clr, true);
    }

    static void SymSPD(SKCanvas b, float cx, float cy)
    {
        float x(float v) => cx - NW/2f + px(v);
        float y(float v) => cy - NH/2f + py(v);
        SKColor clr = CSPD;
        
        DrawP(b, clr, 0.6f, 
            x(150), y(0), x(150), y(130),
            x(150), y(130), x(150), y(155),
            x(143), y(148), x(150), y(155),
            x(150), y(155), x(157), y(148),
            x(150), y(190), x(150), y(165),
            x(143), y(172), x(150), y(165),
            x(150), y(165), x(157), y(172),
            x(150), y(190), x(150), y(250),
            x(125), y(250), x(175), y(250),
            x(135), y(260), x(165), y(260),
            x(145), y(270), x(155), y(270)
        );
        using var p = Stroke(clr, 0.6f);
        b.DrawRect(x(130), y(130), px(40), py(60), p);
        
        using var f = new SKPaint { Color = clr, Style = SKPaintStyle.Fill, IsAntialias = true }; 
        b.DrawCircle(x(150), y(60), px(3), f);
    }

    static void SymKF(SKCanvas b, float cx, float cy)
    {
        float x(float v) => cx - NW/2f + px(v);
        float y(float v) => cy - NH/2f + py(v);
        SKColor clr = CKF;
        
        DrawP(b, clr, 0.6f, 
            x(150), y(0), x(150), y(130),
            x(144), y(154), x(156), y(166),
            x(144), y(166), x(156), y(154),
            x(150), y(190), x(150), y(350)
        );
        using var p = Stroke(clr, 0.6f);
        b.DrawRect(x(130), y(130), px(40), py(60), p);
        b.DrawCircle(x(150), y(160), px(8), p);

        using var f = new SKPaint { Color = clr, Style = SKPaintStyle.Fill, IsAntialias = true }; 
        b.DrawCircle(x(150), y(60), px(3), f);
        b.DrawCircle(x(150), y(250), px(3), f);

        Txt(b, "N", x(165), y(350), 5f, clr, true);
    }

    static void SymGround(SKCanvas c, float cx, float y)
    {
        float g = y + 5;
        float w = NW * 0.3f;
        using var p1 = Stroke(CPE, 1f); c.DrawLine(cx, y, cx, g, p1);
        using var p12 = Stroke(CPE, 1.2f); c.DrawLine(cx - w/2, g, cx + w/2, g, p12);
        using var p8 = Stroke(CPE, 0.8f); c.DrawLine(cx - w/3, g + 3, cx + w/3, g + 3, p8);
        using var p5 = Stroke(CPE, 0.5f); c.DrawLine(cx - w/6, g + 6, cx + w/6, g + 6, p5);
    }

    static void PhaseMarks(SKCanvas c, float cx, float cy, int n, string? phaseText = null, bool hasNeutral = false)
    {
        // Użytkownik poprosił o usunięcie oznaczania przewodu neutralnego (kreska z kropką).
        // Traktujemy pole 'hasNeutral' obojętnie. Upraszczamy tylko do samych faz.
        int totalMarks = Math.Clamp(n, 1, 3);
        float h = 4, gap = 2.5f, off = -(totalMarks - 1) * gap / 2;
        using var pen = Stroke(CTxt, 1f);
        for (int i = 0; i < totalMarks; i++)
        {
            float d = off + i * gap;
            c.DrawLine(cx - h + d, cy + h, cx + h + d, cy - h, pen);
        }

        // Podpisz fazę (np. L1+L2) z boku kresek
        if (!string.IsNullOrEmpty(phaseText) && phaseText != "PENDING" && phaseText != "pending" && phaseText != "3P")
        {
            // Pomiń "L1+L2+L3" dla FR/SPD, jeśli ma dedykowany większy podpis, 
            // no ale dla MCB 2P/3P chcemy pokazywać zawsze
            // Podnosimy Y o 3 pt w górę (z cy + 2 na cy - 1), by uniknąć nachodzenia na szynę (np. "3φ" lub "L1+L2+L3")
            Txt(c, phaseText, cx + 8, cy - 1, 5.5f, CTxtDim);
        }
    }

    // ═══ HELPERS ═══
    static void SymBox(SKCanvas c, float x, float y, SKColor accent)
    {
        // Usunięte rysowanie obramowań wokół symboli
    }

    static void WireDot(SKCanvas c, float x, float y1, float y2)
    {
        DrawDot(c, x, y1, CWire, 2f);
        DrawWireLine(c, x, y1, y2, CWire, 1.2f);
    }

    static void DrawDot(SKCanvas c, float x, float y, SKColor color, float r)
    {
        using var fill = new SKPaint { Color = color, Style = SKPaintStyle.Fill, IsAntialias = true };
        c.DrawCircle(x, y, r, fill);
    }

    static void DrawWireLine(SKCanvas c, float x, float y1, float y2, SKColor color, float width)
    {
        using var pen = Stroke(color, width);
        c.DrawLine(x, y1, x, y2, pen);
    }

    static void PhaseBadge(SKCanvas c, string? phase, float x, float y)
    {
        if (string.IsNullOrEmpty(phase) || phase == "PENDING") return;
        var br = PhClr(phase);
        string t = phase switch { "L1" => "1", "L2" => "2", "L3" => "3", "L1+L2+L3" => "3φ", _ => "" };
        if (string.IsNullOrEmpty(t)) return;
        using var bg = new SKPaint { Color = br, Style = SKPaintStyle.Fill, IsAntialias = true };
        c.DrawRoundRect(new SKRoundRect(new SKRect(x, y, x + 12, y + 12), 2, 2), bg);
        using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 7);
        using var tp = new SKPaint { Color = CWhite, IsAntialias = true };
        float tw = font.MeasureText(t);
        c.DrawText(t, x + (12 - tw) / 2, y + 9, SKTextAlign.Left, font, tp);
    }

    // ═══ TEXT HELPERS ═══
    static SKPaint Stroke(SKColor color, float width) => new()
    {
        Color = color, Style = SKPaintStyle.Stroke, StrokeWidth = width, IsAntialias = true,
        StrokeCap = SKStrokeCap.Butt,
        StrokeJoin = SKStrokeJoin.Miter
    };

    static void Txt(SKCanvas c, string text, float x, float y, float size, SKColor color, bool bold = false)
    {
        if (string.IsNullOrEmpty(text)) return;
        
        using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", bold ? SKFontStyle.Bold : SKFontStyle.Normal), size);
        using var paint = new SKPaint { Color = color, IsAntialias = true };

        string[] lines = text.Split('\n');
        float lineGap = 1.5f;

        for (int i = 0; i < lines.Length; i++)
        {
            c.DrawText(lines[i], x, y + size + i * (size + lineGap), SKTextAlign.Left, font, paint);
        }
    }

    static void TxtR(SKCanvas c, string text, float xRight, float y, float size, SKColor color)
    {
        if (string.IsNullOrEmpty(text)) return;
        
        using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), size);
        using var paint = new SKPaint { Color = color, IsAntialias = true };

        string[] lines = text.Split('\n');
        float lineGap = 1.5f;
        float totalHeight = lines.Length * size + (lines.Length - 1) * lineGap;
        float startY = y - totalHeight / 2 + size; // Środkowanie w pionie

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.Length > 22) line = line[..21] + "…";
            float tw = font.MeasureText(line);
            c.DrawText(line, xRight - tw, startY + i * (size + lineGap), SKTextAlign.Left, font, paint);
        }
    }

    static void DrawTextCentered(SKCanvas c, string text, float cx, float y, float size, SKColor color, bool bold = false)
    {
        if (string.IsNullOrEmpty(text)) return;
        using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", bold ? SKFontStyle.Bold : SKFontStyle.Normal), size);
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        c.DrawText(text, cx, y + size, SKTextAlign.Center, font, paint);
    }

    static void DrawPathNumberLabel(SKCanvas c, string text, float cx, float y)
    {
        using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 9);
        float textWidth = font.MeasureText(text);
        float paddingX = 4f;
        float rectWidth = textWidth + paddingX * 2f;
        float rectHeight = 12f;
        float rectLeft = cx - rectWidth / 2f;
        float rectTop = y - 1f;

        using var fill = new SKPaint { Color = CWhite, Style = SKPaintStyle.Fill, IsAntialias = true };
        c.DrawRoundRect(new SKRoundRect(new SKRect(rectLeft, rectTop, rectLeft + rectWidth, rectTop + rectHeight), 2, 2), fill);

        DrawTextCentered(c, text, cx, y, 9, CTxtDes, true);
    }

    static void DrawTextCenteredInBox(SKCanvas c, string text, float x, float y, float w, float size, SKColor color)
    {
        if (string.IsNullOrEmpty(text)) return;
        
        using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), size);
        using var paint = new SKPaint { Color = color, IsAntialias = true };

        string[] lines = text.Split('\n');
        float lineGap = 1.5f;
        float totalHeight = lines.Length * size + (lines.Length - 1) * lineGap;
        float startY = y + (float)(E.RowH - totalHeight) / 2 + size - (size * 0.1f); // Drobna korekta baseline

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.Length > 22) line = line[..21] + "…";
            float tw = font.MeasureText(line);
            c.DrawText(line, x + (w - tw) / 2, startY + i * (size + lineGap), SKTextAlign.Left, font, paint);
        }
    }

    static void TblCell(SKCanvas c, string text, float x, float y, float w, SKColor color, bool bold = false)
    {
        if (string.IsNullOrEmpty(text)) return;
        
        float sz = (float)E.CellFontSize;
        using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", bold ? SKFontStyle.Bold : SKFontStyle.Normal), sz);
        using var paint = new SKPaint { Color = color, IsAntialias = true };

        string[] manualLines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var wrappedLines = new List<string>();
        
        float maxW = w - 4f; // 2px marginesu na stronę
        foreach (var line in manualLines)
        {
            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) continue;
            
            string currentLine = words[0];
            for (int i = 1; i < words.Length; i++)
            {
                string word = words[i];
                string testLine = currentLine + " " + word;
                if (font.MeasureText(testLine) <= maxW)
                {
                    currentLine = testLine;
                }
                else
                {
                    wrappedLines.Add(currentLine);
                    currentLine = word;
                }
            }
            wrappedLines.Add(currentLine);
        }

        float lineGap = 1.0f;
        float totalHeight = wrappedLines.Count * sz + (wrappedLines.Count - 1) * lineGap;
        
        // Wyśrodkowanie pionowe całego zablokowanego tekstu
        float startY = y + ((float)E.RowH - totalHeight) / 2 + sz - (sz * 0.1f);

        for (int i = 0; i < wrappedLines.Count; i++)
        {
            string lineToDraw = wrappedLines[i];
            float tw = font.MeasureText(lineToDraw);
            
            // Jesli nawet pojedyncze slowo bez spacji jest za dlugie by sie zmiescic - ucinamy z kropeczkami
            if (tw > w)
            {
                while (lineToDraw.Length > 2 && font.MeasureText(lineToDraw + "…") > maxW)
                    lineToDraw = lineToDraw[..^1];
                lineToDraw += "…";
                tw = font.MeasureText(lineToDraw);
            }

            c.DrawText(lineToDraw, x + (w - tw) / 2, startY + i * (sz + lineGap), SKTextAlign.Left, font, paint);
        }
    }

    static SKColor PhClr(string? p) => p switch
    {
        "L1" => CL1, "L2" => CL2, "L3" => CL3, "L1+L2+L3" => CL1, _ => CL1
    };
}
