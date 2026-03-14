using System;
using System.Collections.Generic;
using System.Linq;
using DINBoard.Models;

namespace DINBoard.Services;

/// <summary>
/// Schemat jednokreskowy wg PN-EN 60617, IEC 61082, PN-EN 81346.
/// Profesjonalny rozkład z ramką, tabliczką, oznaczeniami.
/// </summary>
public class SchematicLayoutEngine
{
    private readonly SchematicNodeBuilderService _nodeBuilder;
    private readonly SchematicPaginationService _paginationService;

    // ═══ A4 poziomo: 297×210mm ═══
    public const double PageW = 1122;
    public const double PageH = 794;

    // Ramka (PN-EN ISO 5457)
    public const double FrameL = 24;   // Maksymalizacja miejsca z lewej (było 76)
    public const double FrameT = 24;
    public const double FrameR = 24;
    public const double FrameB = 24;

    // Tabliczka rysunkowa — orientacja pionowa (obrócona, oparta o prawą ramkę)
    public const double TitleW = 52;
    public const double TitleH = 360;

    // ═══ OBSZAR RYSUNKOWY — uwydatnia dół ═══
    public const double DrawL = FrameL + 6;           // 82
    public const double DrawR = PageW - FrameR - TitleW - 6; // Odcięty pionowy pas w prawym logu
    public const double DrawT = FrameT + 6;            // 24
    public const double DrawB = PageH - FrameB - 6;    // Uwolniliśmy całą podłogę po przeniesieni Title! (750px)
    public const double DrawW = DrawR - DrawL;          
    public const double DrawH = DrawB - DrawT;          

    // Kolumny — odpowiednio przeskalowane dla lepszej widoczności
    public const double ColW = 142;
    public const double ColGap = 12;
    public const double ColStep = ColW + ColGap;        // 154
    public const double ColMarginL = 72;                // nagłówki tabeli po lewej
    public const double ColMarginR = 24;                // margines prawy

    // MaxColsPerPage = floor((996 - 72 - 24) / 154) = floor(900/154) = 5
    public const int MaxColsPerPage = 5;

    // ═══ Rzędy Y — WZGLĘDNE do DrawT (skondensowane pod duże symbole NH=90, Tabela opuszczona) ═══
    public const double YPathNums     = 10;
    public const double YSupply       = 10;
    public const double YFR           = 25;     // FR box: 25..25+NH
    public const double YFRInfo       = 120;
    public const double YMainBus      = 135;    // Szyna główna
    public const double YMainDev      = 150;    // RCD i in. box: 150..150+NH
    public const double YGroupBus     = 255;    // L1/L2/L3 grupowe
    public const double YMCB          = 315;    // MCB box 

    public const double YWireEnd      = 415;
    public const double YLabelTop     = 417;

    // N / PE (przeniesione nad tabelę)
    public const double YN            = 425;
    public const double YPE           = 440;

    // Tabela obwodów (koniec ściśle na bezpiecznych 725px wykorzystując nową wylaną przestrzeń nad dolną ramką)
    public const double RowH          = 25;   // Zredukowane odrobinę dla ratowania miejsca pod grube okienka z etykietami 
    public const double YRowDesig     = 490;
    public const double YRowProt      = 515;
    public const double YRowCircuit   = 540;
    public const double YRowLocation  = 565;
    public const double YRowSep       = 590;  // separator
    public const double YRowCable     = 600;
    public const double YRowCableType = 625;
    public const double YRowCableSpec = 650;
    public const double YRowCableLen  = 675;
    public const double YRowPower     = 700;
    public const double YTableEnd     = 725;

    public const double CellFontSize = 10.5;
    public const double HeaderFontSize = 8.5;

    // Aliasy
    public const double YDesig = YRowDesig, YProt = YRowProt, YCircuit = YRowCircuit;
    public const double YLocation = YRowLocation, YSep = YRowSep;
    public const double YCableDes = YRowCable, YCableType = YRowCableType;
    public const double YCableSpec = YRowCableSpec, YCableLen = YRowCableLen, YPower = YRowPower;

    // Siatka referencyjna
    public const int GridCols = 8;
    public const int GridRows = 6;

    // Moduły — większe
    public const double NW = 82, NH = 90;

    // Stronicowanie
    public const double PageGap = 40;

    public SchematicLayoutEngine(IModuleTypeService moduleTypeService)
    {
        ArgumentNullException.ThrowIfNull(moduleTypeService);
        _nodeBuilder = new SchematicNodeBuilderService(moduleTypeService);
        _paginationService = new SchematicPaginationService();
    }

    public SchematicLayout BuildLayout(IReadOnlyCollection<SymbolItem> symbols, Project? project)
    {
        var layout = new SchematicLayout
        {
            Project = project,
            TotalWidth = PageW,
            TotalHeight = PageH
        };

        if (symbols == null || symbols.Count == 0)
        {
            layout.IsEmpty = true;
            return layout;
        }

        var buildResult = _nodeBuilder.Build(symbols, project);
        if (buildResult.MainDevices.Count == 0 && buildResult.CircuitDevices.Count == 0)
        {
            layout.IsEmpty = true;
            return layout;
        }
        layout.FR = buildResult.Fr;
        _paginationService.AssignPagesAndPosition(layout, buildResult.MainDevices, buildResult.CircuitDevices, project);
        SyncReferenceDesignations(layout);

        return layout;
    }

    private static void SyncReferenceDesignations(SchematicLayout layout)
    {
        foreach (var device in layout.Devices)
        {
            if (device.Symbol != null)
            {
                device.Symbol.ReferenceDesignation = device.Designation;
            }

            foreach (var child in device.Children)
            {
                if (child.Symbol != null)
                {
                    child.Symbol.ReferenceDesignation = child.Designation;
                }
            }
        }
    }
}

public class PageInfo
{
    public int PageIndex { get; set; }
    public double OffsetX { get; set; }
    public double YOffset { get; set; }
    public int MinCol { get; set; }
    public double BusX1 { get; set; }
    public double BusX2 { get; set; }
}

public class SchematicLayout
{
    public Project? Project { get; set; }
    public bool IsEmpty { get; set; }
    public SymbolItem? FR { get; set; }
    public (double X, double Y) FRPosition { get; set; }
    public double MainBusY { get; set; }
    public double SchematicOffsetX { get; set; }
    public List<SchematicNode> Devices { get; set; } = new();
    public List<PageInfo> Pages { get; set; } = new();
    public int TotalColumns { get; set; }
    public int TotalPages { get; set; } = 1;
    public double TotalWidth { get; set; }
    public double TotalHeight { get; set; }
}
