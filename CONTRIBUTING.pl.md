# Jak Przyczynić Się Do DINBoard

Dziękujemy zainteresowania udziałem w projekcie DINBoard! Dokument ten zawiera wytyczne i najlepsze praktyki dla współtwórców projektu.

## Spis Treści

1. [Architektura Kodu](#architektura-kodu)
2. [Wytyczne Pull Request](#wytyczne-pull-request)
3. [Styl Kodu](#styl-kodu)
4. [Wymagania Testów](#wymagania-testów)
5. [Standard Dokumentacji](#standard-dokumentacji)
6. [Workflow Git](#workflow-git)
7. [Typowe Wzorce](#typowe-wzorce)

---

## Architektura Kodu

### Zasada Pojedynczej Odpowiedzialności (SRP)

Każda klasa powinna mieć **jedną przyczynę do zmiany**. Jeśli piszesz więcej niż jedną odpowiedzialność, rozważ podzielenie.

**Limity Wielkości Klasy:**
- **Maksimum 300 linii** na klasę
- **Maksimum 50 linii** na metodę
- Jeśli klasa przekracza te limity → **natychmiast refaktoruj**

**Praktyczna interpretacja (sygnalizacja):**
- 🟢 **0-300 linii**: zdrowo
- 🟡 **301-450 linii**: strefa ostrzegawcza, dodaj plan podziału w PR
- 🔴 **451+ linii**: wymagany podział/refaktor przed dokładaniem odpowiedzialności

Jeśli 2+ odpowiedzi poniżej to "Nie", rozbij klasę przed merge:
- Czy klasa nadal ma jedną przyczynę do zmiany?
- Czy logikę da się testować unit testami bez zależności UI/plików?
- Czy nowa logika nie powinna trafić do dedykowanego Service?
- Czy DI jest zachowane (bez zbędnych `new` w View/ViewModel)?

**Dobry Przykład:**
```csharp
// ✅ DOBRZE - Skoncentrowana odpowiedzialność
public partial class WireDrawingViewModel : ObservableObject
{
    // Tylko logika rysowania przewodów
    public void AddDrawingPoint(Point point) { }
    public void StartDrawing() { }
    public void FinishDrawing() { }
}
```

**Zły Przykład:**
```csharp
// ❌ ŹLE - Za wiele odpowiedzialności
public partial class MainViewModel : ObservableObject
{
    // Zawiera: rysowanie, bilans mocy, walidacja, export, undo/redo...
    public void DrawWire() { }
    public void CalculatePowerBalance() { }
    public void ValidateCircuit() { }
    public void ExportPDF() { }
    // ... 1000+ linii
}
```

### Architektura Warstwowa

```
Models/              → Dane (SymbolItem, WireConnection, Circuit)
├─ SymbolItem.cs
├─ WireConnection.cs
└─ Project.cs

ViewModels/          → Logika Aplikacji (Coordinator + Specjaliści)
├─ MainViewModel.cs         (Koordynator - zarządza innymi VM)
├─ WireDrawingViewModel.cs  (Logika rysowania)
├─ PowerBalanceViewModel.cs (Obliczenia mocy)
└─ ProjectThemeViewModel.cs (Ustawienia UI)

Views/               → Warstwa UI (XAML + Code-behind)
├─ MainWindow.xaml
├─ MainWindow.xaml.cs
└─ ModulesPaletteView.xaml

Services/            → Logika Biznesowa (Jeden Interface = Jedna Odpowiedzialność)
├─ ISymbolImporter.cs       (Import symboli)
├─ ISymbolValidator.cs      (Walidacja obwodów)
├─ IPdfExporter.cs          (Export do PDF)
└─ SymbolImportService.cs   (Implementacja)

Helpers/             → Narzędzia i Stałe
├─ SvgHelper.cs     (Operacje SVG)
├─ PathHelper.cs    (Operacje na ścieżkach)
└─ Constants.cs

Tests/               → Testy Jednostkowe (Lustrzana struktura)
├─ WireDrawingViewModelTests.cs
├─ PowerBalanceViewModelTests.cs
└─ SvgHelperTests.cs
```

---

## Wytyczne Pull Request

### Checklist Przed Wysłaniem

Każdy PR musi przejść WSZYSTKIE sprawdzenia przed połączeniem:

```
☐ Kod się kompiluje bez błędów
☐ Brak nowych warningów (CS0*)
☐ Wielkość klasy < 300 linii
☐ Wielkość metody < 50 linii
☐ Brak duplikacji kodu (zasada DRY)
☐ Wszystkie publiczne metody mają dokumentację XML
☐ Napisane testy jednostkowe dla nowych funkcji
☐ Pokrycie testami > 70%
☐ Komunikaty commitów są jasne i opisowe
☐ Brak kodu debug'owania (Console.WriteLine, TODO dla hacków)
```

### Format Tytułu PR

```
[KOMPONENT] Krótki opis

Przykłady:
✅ [ViewModel] Wyodrębnianie WireDrawingViewModel z MainViewModel
✅ [Services] Ekstrakcja narzędzi SVG do SvgHelper
✅ [Tests] Dodanie testów jednostkowych dla WireDrawingViewModel
❌ "fix"
❌ "refactoring"
❌ "update"
```

### Szablon Opisu PR

```markdown
## Co robi ten PR?
Krótkie podsumowanie zmian.

## Dlaczego?
Motywacja i kontekst.

## Jak?
Podejście techniczne i kluczowe zmiany.

## Testowanie
Jak sprawdzić, że zmiany działają poprawnie.

## Powiązane Problemy
Zamyka #123

## Zrzuty Ekranu (jeśli dotyczy)
Zrzuty Przed/Po.

## Checklist
- [ ] Przechodzi wszystkie sprawdzenia
- [ ] Dodane testy
- [ ] Zaktualizowana dokumentacja
```

---

## Styl Kodu

### Konwencje Nazewnictwa

```csharp
// ✅ DOBRZE - Jasne, opisowe nazwy
var symbolData = symbolService.GetSymbolData();
void CalculatePhaseBalance(int voltage, string phase, bool includeDefaults);
private List<string> availableThemes;
private const double DefaultViewBoxWidth = 596;

// ❌ ŹLE - Niejednoznaczne, kryptyczne
var x = GetData();
void Calculate(int a, string b, bool c);
private List<string> lst;
private const double VIEWBOX_W = 596; // Co to jest?
```

### Stałe - Nigdy Nie Hardkoduj Liczb Magicznych

```csharp
// ❌ ŹLE
var scale = Math.Min(w / 596, h / 842); // Co to są 596 i 842?

// ✅ DOBRZE
const double DefaultViewBoxWidth = 596;  // Szerokość viewBox bloku
const double DefaultViewBoxHeight = 842; // Wysokość viewBox bloku
var scale = Math.Min(w / DefaultViewBoxWidth, h / DefaultViewBoxHeight);
```

### Dokumentacja XML

Każda publiczna klasa, metoda i właściwość musi mieć dokumentację XML:

```csharp
/// <summary>
/// Oblicza bilans faz i rozkład mocy na L1, L2, L3.
/// </summary>
/// <remarks>
/// WAŻNE: Używa napięcia z CurrentProject.PowerConfig.
/// Jeśli napięcie wynosi 0, prądy są ustawiane na 0.
/// </remarks>
/// <param name="symbols">Kolekcja symboli do analizy</param>
/// <param name="project">Projekt zawierający konfigurację zasilania</param>
/// <exception cref="ArgumentNullException">Jeśli symbols jest null</exception>
public void RecalculatePhaseBalance(ObservableCollection<SymbolItem> symbols, Project? project)
{
}
```

### Unikaj Magicznych Stringów

```csharp
// ❌ ŹLE
if (symbol.Type == "MCB" || symbol.Type == "RCD") { }
if (theme == "Dark (Anthracite)") { }

// ✅ DOBRZE
if (ModuleTypes.IsMcbOrRcd(symbol.Type)) { }
if (theme == AvailableThemes.DarkAnthracite) { }

// Lub użyj stałych:
public static class ModuleTypes
{
    public const string MCB = "MCB";
    public const string RCD = "RCD";
}
```

### Unikaj Skomentowanego Kodu

```csharp
// ❌ ŹLE
// public void OldMethod() { }
// var x = 5; // może później?
// if (someCondition) { }

// ✅ DOBRZE
// Użyj historii git aby znaleźć stary kod
// Otwórz issue jeśli masz wątpliwości
// Usuń go całkowicie
```

---

## Wymagania Testów

### Minimum Testów Jednostkowych

Każdy nowy ViewModel lub Service musi mieć co najmniej **5 testów jednostkowych**:

```csharp
public class WireDrawingViewModelTests
{
    [Fact]
    public void Constructor_InitializesWithEmptyCollections()
    {
        // Arrange
        // Act
        // Assert
    }

    [Fact]
    public void StartDrawing_SetsDrawingState()
    {
        // Arrange
        // Act
        // Assert
    }

    [Fact]
    public void AddDrawingPoint_AddsPointToCollection()
    {
        // Arrange
        // Act
        // Assert
    }

    [Fact]
    public void CanFinishDrawing_RequiresAtLeastTwoPoints()
    {
        // Arrange
        // Act
        // Assert
    }

    [Fact]
    public void CancelDrawing_ClearsPointsAndState()
    {
        // Arrange
        // Act
        // Assert
    }
}
```

### Konwencja Nazewnictwa Testów

```csharp
// Format: [NazwaMetody]_[Scenariusz]_[OczekiwanyWynik]

✅ Constructor_InitializesWithEmptyCollections()
✅ AddDrawingPoint_WithValidPoint_AddsToCollection()
✅ CalculateBalance_WithAsymmetricalLoad_ReturnsImbalancePercent()
✅ GetColor_ForL1Phase_ReturnsCorrectHexCode()

❌ Test1()
❌ TestMethod()
❌ Check()
```

### Cel Pokrycia Testami

- **Minimum 70%** pokrycia dla nowego kodu
- **Testy jednostkowe** dla wszystkich publicznych metod
- **Testy integracyjne** dla Services

---

## Standard Dokumentacji

### Co Dokumentować

✅ **Dokumentuj:**
- Publiczne klasy i metody
- Złożone algorytmy lub logikę biznesową
- Nie oczywiste decyzje projektowe
- Wymagania konfiguracji
- Znane ograniczenia lub obejścia

❌ **Nie dokumentuj:**
- Oczywiste właściwości getter/setter
- Samowyjaśniające się nazwy metod
- Proste implementacje pętli
- Cechy języka

### Przykład Dokumentacji

```csharp
/// <summary>
/// Wykrywa nakładające się segmenty przewodów i stosuje offset 
/// aby równoległe przewody się nie nakładały.
/// Grupuje przewody z nakładającymi się segmentami i przypisuje symetryczne offsety.
/// </summary>
/// <remarks>
/// Algorytm:
/// 1. Zbierz wszystkie segmenty z każdego przewodu
/// 2. Znajdź przewody z nakładającymi się segmentami (na podstawie odległości)
/// 3. Pogrupuj nakładające się przewody w zestawy
/// 4. Przypisz symetryczne offsety: -N/2, -N/2+1, ..., 0, ..., N/2-1, N/2
///
/// Wydajność: O(n²) gdzie n = liczba przewodów
/// </remarks>
public void RecalculateParallelOffsets()
{
}
```

---

## Workflow Git

### Format Komunikatu Commitu

```
[KOMPONENT] Krótki opis

Dłuższe wyjaśnienie dlaczego ta zmiana była niezbędna.
Uwzględnij istotne szczegóły techniczne.

Powiązane issues: #123, #456
```

**Przykłady:**

```
✅ [ViewModel] Wyodrębnianie WireDrawingViewModel z MainViewModel

Oddziela logikę rysowania przewodów do własnego ViewModel
zgodnie z SRP. Poprawia testowalność i pozwala na ponowne
użycie w innych projektach.

Powiązane issues: #45

✅ [Tests] Dodanie 8 testów dla WireDrawingViewModel

Testy obejmują: inicjalizację, stan rysowania, dodawanie
punktów, snap'owanie, i anulowanie.

Naprawia: #89

❌ "fix bug"
❌ "update code"
❌ "refactoring changes"
```

### Wielkość Commitu

- **Jeden commit = Jedna logiczna zmiana**
- Trzymaj commity atomowe i poddawane recenzji
- Nigdy nie mieszaj refaktoryzacji ze zmianami funkcji

**Dobra sekwencja commitów:**
```
1. [Services] Ekstrakcja SvgHelper z GetDimensionsFromViewBox()
2. [Services] Aktualizacja SymbolImportService aby używał SvgHelper
3. [Services] Usunięcie duplikatów GetDimensionsFromViewBox()
4. [Tests] Dodanie testów dla SvgHelper
```

**Źle:**
```
1. "refactoring everything, fix bugs, add features, update tests"
```

### Nazewnictwo Gałęzi

```
feature/[opis]         → feature/wire-drawing-viewmodel
bugfix/[opis]          → bugfix/null-reference-snap-service
refactor/[opis]        → refactor/split-mainviewmodel
docs/[opis]            → docs/add-contributing-guide

✅ feature/split-mainviewmodel
❌ f/split
❌ new-feature
```

---

## Typowe Wzorce

### Wstrzykiwanie Zależności (Dependency Injection)

```csharp
// ✅ DOBRZE - Luźne sprzężenie
public class MainViewModel
{
    private readonly ISymbolService _symbolService;
    
    public MainViewModel(ISymbolService symbolService)
    {
        _symbolService = symbolService;
    }
}

// W App.cs
services.AddScoped<ISymbolService, SymbolService>();
services.AddScoped<MainViewModel>();

// ❌ ŹLE - Ciasne sprzężenie
public class MainViewModel
{
    private var _service = new SymbolService(); // Trudne do testowania
}
```

### Brak Duplikacji Kodu (DRY)

```csharp
// ❌ ŹLE - Powielone w 3 miejscach
// W SymbolImportService:
var (w, h) = GetDimensionsFromViewBox(svg);

// W SchematicController:
var (w, h) = GetDimensionsFromViewBox(svg);

// W SvgModuleImporter:
var (w, h) = GetDimensionsFromViewBox(svg);

// ✅ DOBRZE - Scentralizowane w SvgHelper
public static class SvgHelper
{
    public static (double Width, double Height) GetDimensionsFromViewBox(string svgContent)
    {
    }
}

// Używane wszędzie:
var (w, h) = SvgHelper.GetDimensionsFromViewBox(svg);
```

### Wzorzec ObservableProperty

```csharp
// ✅ DOBRZE - Używając MVVM Toolkit
public partial class PowerBalanceViewModel : ObservableObject
{
    [ObservableProperty]
    private double _l1PowerW;
    
    [ObservableProperty]
    private double _phaseImbalancePercent;
    
    // Częściowa metoda dla logiki zmiany właściwości
    partial void OnL1PowerWChanged(double oldValue, double newValue)
    {
        RecalculatePhaseBalance();
    }
}

// ❌ ŹLE - Manualna implementacja właściwości
public class PowerBalanceViewModel
{
    private double _l1PowerW;
    public double L1PowerW
    {
        get => _l1PowerW;
        set
        {
            if (_l1PowerW != value)
            {
                _l1PowerW = value;
                OnPropertyChanged(nameof(L1PowerW));
                RecalculatePhaseBalance();
            }
        }
    }
}
```

### Bezpieczeństwo Null'a

```csharp
// ✅ DOBRZE - Sprawdzenia pamiętające null
if (_groupOverlaysContainer != null)
{
    _groupOverlayController = new GroupOverlayController(
        ViewModel,
        _groupOverlaysContainer,
        () => ((App)Application.Current!).Services.GetRequiredService<IDialogService>(),
        () => _groupOverlayController?.RegenerateOverlays());
}

// ❌ ŹLE - Założenie że jest not-null
var controller = new GroupOverlayController(
    ViewModel,
    _groupOverlaysContainer,  // Może być null!
    ...
);
```

---

## Pytania?

- Sprawdź istniejący kod dla wzorców
- Pytaj w dyskusjach Pull Request
- Odwołaj się do tego przewodnika w razie wątpliwości

**Powodzenia! 🚀**
