using System.Collections.Generic;
using System.Linq;

namespace DINBoard.Services;

/// <summary>
/// Wspólne metody pomocnicze używane w wielu częściach aplikacji.
/// </summary>
public static class CommonHelpers
{
    /// <summary>
    /// Próbuje wyciągnąć dodatnią liczbę z tekstu (np. "Grupa 3" → 3).
    /// Używane do sortowania grup wg numerów.
    /// </summary>
    public static int? TryExtractPositiveNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var digits = new string(text.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var number) && number > 0 ? number : null;
    }

    // ──── Obciążalność prądowa przewodów Cu wg PN-HD 60364-5-52 ────

    /// <summary>
    /// Obciążalność — metoda D1 (kabel bezpośrednio w ziemi).
    /// Wg PN-HD 60364-5-52, Tab. B.52.4, wielożyłowe Cu.
    /// Używana dla WLZ (wewnętrzna linia zasilająca) w gruncie.
    /// </summary>
    public static readonly Dictionary<double, double> CableAmpacityMethodD1 = new()
    {
        { 1.5, 22.0 },
        { 2.5, 29.0 },
        { 4.0, 37.0 },
        { 6.0, 46.0 },
        { 10.0, 61.0 },
        { 16.0, 79.0 },
        { 25.0, 101.0 },
        { 35.0, 122.0 },
        { 50.0, 144.0 },
        { 70.0, 178.0 },
        { 95.0, 211.0 },
        { 120.0, 240.0 }
    };

    /// <summary>
    /// Obciążalność z korekcją — metoda B2 (w rurce w ścianie izolowanej).
    /// Wartości obniżone współczynnikiem korekcyjnym, używane do walidacji kabli.
    /// </summary>
    public static readonly Dictionary<double, double> CableAmpacityMethodB2 = new()
    {
        { 1.5, 15.5 },
        { 2.5, 21.0 },
        { 4.0, 28.0 },
        { 6.0, 36.0 },
        { 10.0, 50.0 },
        { 16.0, 66.0 },
        { 25.0, 84.0 },
        { 35.0, 104.0 },
        { 50.0, 125.0 },
        { 70.0, 160.0 },
        { 95.0, 194.0 },
        { 120.0, 225.0 }
    };

    /// <summary>
    /// Obciazalnosc - metoda C (ulozenie bezposrednie, bez oslon).
    /// Wartosci referencyjne dla przewodow Cu/PVC 70C, obwod pojedynczy.
    /// </summary>
    public static readonly Dictionary<double, double> CableAmpacityMethodC = new()
    {
        { 1.5, 19.5 },
        { 2.5, 27.0 },
        { 4.0, 36.0 },
        { 6.0, 46.0 },
        { 10.0, 63.0 },
        { 16.0, 85.0 },
        { 25.0, 112.0 },
        { 35.0, 138.0 },
        { 50.0, 168.0 },
        { 70.0, 213.0 },
        { 95.0, 258.0 },
        { 120.0, 299.0 }
    };

    /// <summary>
    /// Interpoluje obciążalność prądową dla podanego przekroju i wybranej tabeli.
    /// </summary>
    public static double GetCableCapacity(double crossSectionMm2, Dictionary<double, double> table)
    {
        if (table.TryGetValue(crossSectionMm2, out var capacity))
            return capacity;

        // Interpolacja dla niestandardowych przekrojów
        var lowerKey = table.Keys.Where(k => k < crossSectionMm2).DefaultIfEmpty(1.5).Max();
        var upperKey = table.Keys.Where(k => k > crossSectionMm2).DefaultIfEmpty(120.0).Min();

        if (lowerKey == upperKey) return table[lowerKey];

        var lowerVal = table[lowerKey];
        var upperVal = table[upperKey];
        var ratio = (crossSectionMm2 - lowerKey) / (upperKey - lowerKey);

        return lowerVal + (upperVal - lowerVal) * ratio;
    }
}
