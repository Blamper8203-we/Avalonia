using System;
using System.Text;
using System.Globalization;

namespace DINBoard.Services;

/// <summary>
/// Generator proceduralny szyny DIN.
/// Zamiast skalować gotowy obrazek (co powoduje zniekształcenia),
/// rysuje on szynę od zera, zachowując stałe wymiary uchwytów i otworów.
/// </summary>
public class DinRailGeneratorProcedural
{
    // Stałe wymiary wyciągnięte z Twojego pliku SVG (dla zachowania spójności wizualnej)
    private const double UnitPerModule = 250.0; // Zwiększono dla zapasu - 24 moduły z marginesem
    private const double RailHeight = 1642.0;
    private const double PaddingX = 55.0; // Margines boczny
    private const double RowSpacing = 50.0; // Odstęp między rzędami (jeśli rows > 1)

    // Stałe geometryczne elementów
    private const double VerticalGuideWidth = 177.167;
    private const double VerticalGuideHeight = 1631.102;
    private const double VerticalGuideY = 5.449;

    private const double RailBodyY = 614.306;
    private const double RailBodyHeight = 413.387;

    private const double LipHeight = 75.0;
    private const double LipTopY = 614.75;
    private const double LipBottomY = 952.25;

    // Stałe dla otworów (Holes)
    private const double HoleSpacing = 738.19; // Odległość między środkami otworów
    // private const double FirstHoleOffset = 833.715; // <--- USUNIĘTE: Nie używamy już sztywnego offsetu
    private const string HolePathData = "m 0,0 l -403.562,0 c -10.866,0 -19.675,-8.808 -19.675,-19.674 l 0,-73.15 c 0,-10.867 8.809,-19.675 19.675,-19.675 l 403.562,0 c 10.866,0 19.675,8.808 19.675,19.675 l 0,73.15 c 0,10.866 -8.809,19.674 -19.675,19.674 z";
    private const double HoleVisualWidth = 403.562; // Szerokość graficzna otworu (z patha powyżej)

    // Stałe dla śrub (Mounts)
    private const string LeftScrewPath = "M181.452,822.032c0,-20.697 -16.803,-37.5 -37.5,-37.5c-20.697,0 -37.5,16.803 -37.5,37.5c0,20.696 16.803,37.499 37.5,37.499c20.697,0 37.5,-16.803 37.5,-37.499Z";

    public string Generate(int rows, int modulesPerRow)
    {
        if (rows < 1 || modulesPerRow < 1)
            return GenerateErrorSvg("Invalid dimensions");

        // 1. Oblicz całkowitą szerokość samej szyny
        double railWidth = modulesPerRow * UnitPerModule;

        // 2. Oblicz wymiary ViewBox
        double totalWidth = railWidth + (PaddingX * 2) + 10.0;
        double totalHeight = (rows * RailHeight) + ((rows - 1) * RowSpacing);

        var sb = new StringBuilder();

        sb.Append($"<svg width=\"100%\" height=\"100%\" viewBox=\"0 0 {F(totalWidth)} {F(totalHeight)}\" version=\"1.1\" xmlns=\"http://www.w3.org/2000/svg\">");
        sb.Append("<defs><style>.rail-stroke { fill:#fff; stroke:#1e1e1c; stroke-width:5.55px; }</style></defs>");

        // --- Generowanie jednolitych pionowych prowadnic ---

        // Obliczamy wysokość prowadnicy tak, aby obejmowała wszystkie rzędy
        double guideTotalHeight = totalHeight - (VerticalGuideY * 2);

        // Lewa prowadnica
        sb.Append($"<rect x=\"{F(PaddingX)}\" y=\"{F(VerticalGuideY)}\" width=\"{F(VerticalGuideWidth)}\" height=\"{F(guideTotalHeight)}\" class=\"rail-stroke\" />");

        // Prawa prowadnica
        double rightGuideX = PaddingX + railWidth - VerticalGuideWidth;
        sb.Append($"<rect x=\"{F(rightGuideX)}\" y=\"{F(VerticalGuideY)}\" width=\"{F(VerticalGuideWidth)}\" height=\"{F(guideTotalHeight)}\" class=\"rail-stroke\" />");

        // --- Generowanie poziomych elementów (szyny, otwory, śruby) ---
        for (int row = 0; row < rows; row++)
        {
            double currentY = row * (RailHeight + RowSpacing);

            sb.Append($"<g transform=\"translate(0, {F(currentY)})\">");
            GenerateHorizontalElements(sb, railWidth);
            sb.Append("</g>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    /// <summary>
    /// Generuje tylko poziome elementy szyny (korpus, otwory, śruby).
    /// </summary>
    private void GenerateHorizontalElements(StringBuilder sb, double width)
    {
        // --- 1. DIN TH (Główny korpus poziomy) ---
        sb.Append($"<rect x=\"{F(PaddingX)}\" y=\"{F(RailBodyY)}\" width=\"{F(width)}\" height=\"{F(RailBodyHeight)}\" class=\"rail-stroke\" />");

        // --- 2. Wypusty (Górna i dolna krawędź) ---
        double lipX = 19.332;
        double lipWidth = width + 72.0;

        sb.Append($"<rect x=\"{F(lipX)}\" y=\"{F(LipTopY)}\" width=\"{F(lipWidth)}\" height=\"{F(75)}\" class=\"rail-stroke\" />");
        sb.Append($"<rect x=\"{F(lipX)}\" y=\"{F(LipBottomY)}\" width=\"{F(lipWidth)}\" height=\"{F(75)}\" class=\"rail-stroke\" />");

        // --- 3. Otwory (Holes) - WYCENTROWANE I BEZPIECZNE ---

        // Definiujemy bezpieczny margines od krawędzi (zapas na śruby mocujące).
        // Śruba lewa kończy się ok x=220, więc 280 to bezpieczna wartość.
        const double safeMargin = 280.0;

        // Ile miejsca mamy na otwory?
        double availableWidthForHoles = width - (2 * safeMargin);

        if (availableWidthForHoles > HoleVisualWidth)
        {
            // Obliczamy ile otworów się zmieści w dostępnym miejscu
            // Wzór: (SzerokośćDostepna - SzerokośćSamegoOtworu) / Odstęp
            int holeCount = (int)((availableWidthForHoles - HoleVisualWidth) / HoleSpacing) + 1;

            if (holeCount > 0)
            {
                // Obliczamy całkowitą szerokość, jaką zajmie grupa otworów
                double totalHolesGroupWidth = ((holeCount - 1) * HoleSpacing) + HoleVisualWidth;

                // Obliczamy punkt startowy (wizualnie lewa krawędź pierwszego otworu), żeby wycentrować całość
                double centerX = width / 2.0;
                double visualStartX = centerX - (totalHolesGroupWidth / 2.0);

                // Path otworu rysuje się "w lewo" (od 0 do -403), więc punkt wstawienia (insert point) 
                // musi być przesunięty o szerokość otworu w prawo względem jego wizualnego początku.
                double insertionStartX = visualStartX + HoleVisualWidth;

                // Generujemy pętlę
                for (int i = 0; i < holeCount; i++)
                {
                    // Punkt wstawienia konkretnego otworu
                    double xPos = insertionStartX + (i * HoleSpacing);

                    // Dodajemy padding zewnętrzny (55.0) do transformacji, bo wszystko jest w grupie
                    // przesuniętej o 0, ale szyna zaczyna się wizualnie od PaddingX.
                    // UWAGA: W tej metodzie 'width' to szerokość samej szyny.
                    // Wszystkie elementy rysujemy względem PaddingX.
                    // Poprawka: xPos obliczyliśmy względem 0..width, więc musimy dodać PaddingX.

                    sb.Append($"<path d=\"{HolePathData}\" transform=\"translate({F(PaddingX + xPos)}, 877.25)\" class=\"rail-stroke\" />");
                }
            }
        }

        // --- 4. Śruby mocujące (powtarzają się w każdym rzędzie) ---
        sb.Append($"<path d=\"{LeftScrewPath}\" class=\"rail-stroke\" />");

        double rightScrewTransX = width - 177.0;
        sb.Append($"<g transform=\"translate({F(rightScrewTransX)}, 0)\">");
        sb.Append($"<path d=\"{LeftScrewPath}\" class=\"rail-stroke\" />");
        sb.Append("</g>");
    }

    private string GenerateErrorSvg(string message)
    {
        return $"<svg xmlns='http://www.w3.org/2000/svg' width='200' height='50'><text x='10' y='30' fill='red'>{message}</text></svg>";
    }

    private string F(double val)
    {
        return val.ToString("0.###", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Zwraca wymiary szyny DIN dla UI
    /// </summary>
    public (double Width, double Height) GetDimensions(int rows, int modulesPerRow)
    {
        double railWidth = modulesPerRow * UnitPerModule;
        double totalWidth = railWidth + (PaddingX * 2) + 10.0;
        double totalHeight = (rows * RailHeight) + ((rows - 1) * RowSpacing);
        return (totalWidth, totalHeight);
    }

    /// <summary>
    /// Zwraca środki Y dla każdego rzędu szyny (w lokalnym układzie współrzędnych SVG)
    /// </summary>
    public System.Collections.Generic.List<double> GetRowCenters(int rows)
    {
        var centers = new System.Collections.Generic.List<double>();
        for (int r = 0; r < rows; r++)
        {
            double currentY = r * (RailHeight + RowSpacing);
            // Środek wiersza to początek Y + połowa wysokości sekcji
            double centerY = currentY + (RailHeight / 2.0);
            centers.Add(centerY);
        }
        return centers;
    }
}