# Jak Wnosić Zmiany Do DINBoard

Dziękujemy za współtworzenie DINBoard.

To repozytorium zawiera zarówno zwykły kod aplikacji desktopowej, jak i
wrażliwą logikę inżynierską, więc jakość zmian jest ważniejsza niż samo tempo.

## Spis Treści

1. [Architektura Kodu](#architektura-kodu)
2. [Wytyczne Dla Pull Requestów](#wytyczne-dla-pull-requestów)
3. [Styl Kodu](#styl-kodu)
4. [Wymagania Dotyczące Testów](#wymagania-dotyczące-testów)
5. [Standardy Dokumentacji](#standardy-dokumentacji)
6. [Workflow Git](#workflow-git)
7. [Typowe Wzorce](#typowe-wzorce)

---

## Architektura Kodu

### Zasada Pojedynczej Odpowiedzialności

Każda klasa powinna mieć jedną wyraźną przyczynę do zmiany.

Jeśli zmiana dokłada drugi niezależny obowiązek, lepiej go wyodrębnić niż
rozszerzać istniejącą klasę.

### Pragmatyczne Limity Rozmiaru

- cel: `<= 300` linii na klasę
- strefa ostrzegawcza: `301-450` linii
- refaktor wymagany przed dokładaniem kolejnej odpowiedzialności: `451+` linii
- cel: `<= 50` linii na metodę tam, gdzie to praktyczne

Traktuj te liczby jako bezpieczniki, a nie jedyny miernik jakości.

Zanim rozbudujesz dużą klasę, zapytaj:
- czy ta klasa nadal ma jedną przyczynę do zmiany
- czy jej logikę da się testować bez zależności od UI albo plików
- czy nowa odpowiedzialność nie powinna trafić do osobnego `Service`
- czy nie zwiększam sprzężenia przez nowe `new` w `View` albo `ViewModel`

### Dobry Przykład

```csharp
// DOBRZE - skupiona odpowiedzialność
public partial class ProjectWorkspaceViewModel : ObservableObject
{
    public async Task SaveProjectAsync()
    {
        // Tylko lifecycle projektu i obowiązki workspace
    }

    public async Task OpenProjectAsync()
    {
        // Tylko lifecycle projektu i obowiązki workspace
    }
}
```

### Zły Przykład

```csharp
// ŹLE - jeden koordynator zbiera niepowiązane obowiązki
public partial class MainViewModel : ObservableObject
{
    public async Task OpenProjectAsync() { }
    public async Task ExportPdfAsync() { }
    public void RecalculatePhaseBalance() { }
    public void ApplyCircuitEditPreset() { }
    public void RebuildSchematicLayout() { }
}
```

### Architektura Warstwowa

```text
Models/              -> Stan domeny i projektu
|- SymbolItem.cs
|- Circuit.cs
`- Project.cs

ViewModels/          -> Stan UI, komendy, orkiestracja
|- MainViewModel.cs
|- ProjectWorkspaceViewModel.cs
|- PowerBalanceViewModel.cs
`- LayoutViewModel.cs

Views/               -> Tylko UI
|- MainWindow.axaml
|- Views/CircuitEditPanelView.axaml
`- Views/ModulesPaletteView.axaml

Services/            -> Logika współdzielona, domenowa, techniczna i infrastrukturalna
|- PhaseDistributionCalculator.cs
|- ProjectPersistenceService.cs
|- CircuitEditFieldDefinitionProvider.cs
`- CircuitEditValueApplier.cs

Helpers/             -> Narzędzia pomocnicze
|- LocalizationHelper.cs
`- SvgHelper.cs

Tests/               -> Pokrycie zachowania i regresji
|- Tests/PowerBalanceViewModelTests.cs
|- Tests/PhaseDistributionCalculatorTests.cs
`- Tests/ViewModels/ProjectWorkspaceViewModelTests.cs
```

### Najważniejsze Reguły Architektoniczne

- `Views` powinny pozostać wizualne.
- `ViewModels` powinny orkiestrwać stan UI i komendy.
- `Services` powinny trzymać logikę domenową, techniczną lub infrastrukturalną.
- Nie przenoś logiki biznesowej do code-behind.
- Nie zmieniaj po cichu wzorów elektrycznych, walidacji, persistence,
  undo/redo ani danych eksportowanych.

---

## Wytyczne Dla Pull Requestów

### Lista Kontrolna Przed Wysłaniem

Każdy nietrywialny PR powinien spełnić te warunki:

```text
[ ] Kod się buduje
[ ] Uruchomiono odpowiednie testy
[ ] Ryzykowna logika ma testy celowane albo testy charakteryzujące
[ ] Brak cichej zmiany zachowania w obszarach krytycznych
[ ] Wzrost klasy i metod jest uzasadniony
[ ] Brak martwego kodu i debugowych pozostałości
[ ] Dokumentacja została zaktualizowana, jeśli zmieniła się architektura lub workflow
```

### Format Tytułu PR

```text
[KOMPONENT] Krótki opis
```

Przykłady:
- `[ViewModel] Uproszczenie konstruktora MainViewModel`
- `[Services] Ekstrakcja definicji pól z CircuitEditPanelView`
- `[Tests] Dodanie pokrycia dla phase distribution planner`

Unikaj tytułów typu:
- `fix`
- `update`
- `refactoring`

### Szablon Opisu PR

```markdown
## Co
Krótki opis zmiany.

## Dlaczego
Problem, ryzyko albo motywacja.

## Jak
Najmniejsza bezpieczna zastosowana zmiana.

## Testowanie
Uruchomione testy i wykonane sprawdzenia ręczne.

## Uwagi
Na co recenzent powinien zwrócić szczególną uwagę.
```

### Oczekiwania Przy Review

- utrzymuj zmiany w formie nadającej się do review
- nie mieszaj dużego refaktoru z nową funkcją, jeśli nie jest to konieczne
- wypisz ręczne kroki smoke testu, jeśli zmiana może wpływać na UI
- jeśli zmiana dotyka krytycznego subsystemu, opisz obecne zachowanie i ryzyko

---

## Styl Kodu

### Konwencje Nazewnictwa

```csharp
// DOBRZE
var projectMetadata = projectService.GetProjectMetadata();
void RecalculatePhaseBalance(Project project, IReadOnlyList<SymbolItem> symbols);
private readonly RecentProjectsService _recentProjectsService;
private const double DefaultViewBoxWidth = 596;

// ŹLE
var x = GetData();
void Calculate(int a, string b, bool c);
private readonly RecentProjectsService _svc;
private const double VIEWBOX_W = 596;
```

### Unikaj Liczb Magicznych

```csharp
// ŹLE
var scale = Math.Min(width / 596, height / 842);

// DOBRZE
const double DefaultViewBoxWidth = 596;
const double DefaultViewBoxHeight = 842;
var scale = Math.Min(width / DefaultViewBoxWidth, height / DefaultViewBoxHeight);
```

### Preferuj Nazwy Domenowe

Używaj nazw oddających znaczenie w domenie:
- `phaseLoad`
- `lockedGroups`
- `workspaceState`
- `recentProjects`
- `fieldDefinitions`

Unikaj nazw typu:
- `data`
- `item2`
- `temp`
- `svc`

### Unikaj Magicznych Stringów

```csharp
// ŹLE
if (symbol.Type == "MCB" || symbol.Type == "RCD") { }

// DOBRZE
if (ModuleTypes.IsMcbOrRcd(symbol.Type)) { }

public static class ModuleTypes
{
    public const string MCB = "MCB";
    public const string RCD = "RCD";
}
```

### Unikaj Zakomentowanego Kodu

Usuń go.

Jeśli nie jesteś pewien, skorzystaj z historii gita zamiast zostawiać martwy kod
w pliku.

---

## Wymagania Dotyczące Testów

### Zasada Ogólna

Uruchamiaj najbardziej adekwatne testy dla subsystemu, którego dotyka zmiana.

Jeśli zmiana jest szeroka albo ryzykowna, uruchom pełny zestaw:

```powershell
dotnet test Tests\Avalonia.Tests.csproj --no-restore
```

### Krytyczne Zestawy Testów

Dla logiki elektrycznej:
- `Tests/PhaseDistributionCalculatorTests.cs`
- `Tests/ElectricalValidationTests.cs`
- `Tests/PowerBalanceViewModelTests.cs`

Dla schematu i canvas:
- `Tests/SchematicLayoutEngineTests.cs`
- `Tests/SchematicDragDropControllerTests.cs`
- `Tests/SchematicViewModelTests.cs`

Dla infrastruktury i bezpieczeństwa danych:
- `Tests/UndoRedoTests.cs`
- `Tests/ProjectRoundTripTests.cs`
- `Tests/PdfExportTests.cs`

### Minimum Dla Nowej Logiki

Nowa logika powinna dostać celowane testy.

Dla nowego `ViewModel` albo `Service` celuj co najmniej w pięć sensownych testów
pokrywających:
- inicjalizację
- ścieżkę poprawną
- niepoprawne albo brzegowe dane wejściowe
- jeden przypadek podatny na regresję
- jedną zmianę stanu albo scenariusz komendy

### Przykładowy Kształt Testów

```csharp
public class ProjectWorkspaceViewModelTests
{
    [Fact]
    public void Constructor_InitializesRecentProjects()
    {
    }

    [Fact]
    public async Task OpenProjectAsync_WithValidFile_LoadsProject()
    {
    }

    [Fact]
    public async Task SaveProjectAsync_WithoutCurrentProject_DoesNotThrow()
    {
    }

    [Fact]
    public async Task OpenRecentProjectAsync_WithMissingFile_ShowsExpectedHandling()
    {
    }

    [Fact]
    public void UpdateLicenseState_RefreshesHomeScreenFlags()
    {
    }
}
```

### Konwencja Nazewnictwa Testów

```text
[Metoda]_[Scenariusz]_[OczekiwanyWynik]
```

Przykłady:
- `Constructor_InitializesRecentProjects()`
- `CreateBalancePlan_WithLockedGroups_ExcludesLockedLoads()`
- `ApplyValue_WithRcdPreset_UpdatesExpectedFields()`

Unikaj nazw typu:
- `Test1`
- `Check`
- `Works`

---

## Standardy Dokumentacji

### Co Dokumentować

- publiczne API z nieoczywistym zachowaniem
- algorytmy wrażliwe domenowo
- ważne ograniczenia projektowe
- wymagania konfiguracyjne i workflow
- znane ograniczenia albo tymczasowe kompromisy bezpieczeństwa

### Przykład Dokumentacji

```csharp
/// <summary>
/// Tworzy plan bilansowania dla bieżącego zestawu symboli bez bezpośredniego
/// uruchamiania animacji UI i bez mutowania stanu widoku.
/// </summary>
/// <remarks>
/// Ta metoda jest częścią pipeline'u bilansowania elektrycznego i powinna
/// zachować obecny wynik elektryczny, jednocześnie oddzielając planowanie od
/// wykonania.
/// </remarks>
public BalancePlan CreateBalancePlan(
    IReadOnlyList<SymbolItem> symbols,
    Project project)
{
}
```

### Utrzymuj Dokumentację W Prawdzie

Jeśli zmieniasz:
- granice architektury
- workflow
- liczbę testów
- instrukcje setupu

to zaktualizuj powiązane pliki markdown w tej samej zmianie, jeśli to praktyczne.

---

## Workflow Git

### Format Komunikatu Commitu

```text
[KOMPONENT] Krótki opis
```

Przykłady:
- `[Services] Ekstrakcja CircuitEditValueApplier`
- `[ViewModel] Jawne wstrzyknięcie zależności workspace`
- `[Docs] Aktualizacja quick start i code quality guides`

### Rozmiar Commitu

- jeden commit powinien reprezentować jedną logiczną zmianę, tam gdzie to praktyczne
- nie mieszaj refaktoru z nową funkcją, jeśli nie jest to wymagane
- preferuj małe, czytelne sekwencje zamiast jednego wielkiego commitu

Dobra sekwencja:

```text
1. [Tests] Dodanie pokrycia dla planowania bilansowania faz
2. [Services] Ekstrakcja helpera wykonania bilansowania
3. [Services] Przepięcie kalkulatora na helper
4. [Docs] Aktualizacja notatek architektonicznych
```

Zła sekwencja:

```text
1. refactor everything and fix bugs
```

### Nazewnictwo Gałęzi

Przykłady:
- `feature/project-workspace-cleanup`
- `bugfix/null-phase-indicator`
- `refactor/phase-distribution-planner`
- `docs/update-quick-start`

Unikaj:
- `f/new`
- `changes`
- `test`

---

## Typowe Wzorce

### Wstrzykiwanie Zależności

```csharp
// DOBRZE
public class MainViewModel
{
    private readonly RecentProjectsService _recentProjectsService;

    public MainViewModel(RecentProjectsService recentProjectsService)
    {
        _recentProjectsService = recentProjectsService;
    }
}

// ŹLE
public class MainViewModel
{
    private readonly RecentProjectsService _recentProjectsService =
        new RecentProjectsService();
}
```

### Wyciągaj Logikę Z Widoków

```csharp
// DOBRZE
var fields = _fieldDefinitionProvider.GetDefinitions(symbol);
_valueApplier.Apply(symbol, values);

// ŹLE
// Widok sam decyduje o polach, parsuje presety i mutuje obiekt domenowy
// bezpośrednio w code-behind.
```

### Oddzielaj Planowanie Od Wykonania

```csharp
// DOBRZE
var plan = _phaseDistributionCalculator.CreateBalancePlan(symbols, project);
await _executionHelper.ApplyGreedyAssignmentsAsync(plan, delayMs);

// ŹLE
// Jedna duża metoda robi planowanie, animację zaznaczeń, opóźnienia i całą
// logikę domenową inline.
```

### Observable Properties W MVVM Toolkit

```csharp
public partial class PowerBalanceViewModel : ObservableObject
{
    [ObservableProperty]
    private double _phaseImbalancePercent;

    partial void OnPhaseImbalancePercentChanged(double oldValue, double newValue)
    {
        // Opcjonalna logika po zmianie
    }
}
```

### Bezpieczeństwo Null

Preferuj jawne sprawdzenia null i bezpieczne guard clause tam, gdzie kontenery
UI albo stan projektu mogą nie istnieć.

Nie zakładaj, że opcjonalne referencje do widoku, stan bieżącego projektu albo
ścieżki design-time zawsze są dostępne.

---

## Pytania

- Najpierw sprawdź aktualne wzorce w istniejącym kodzie.
- Przy większych zmianach użyj `AI_CONTEXT.md` i `ARCHITECTURE_MAP.md`.
- Jeśli masz wątpliwości, wybierz mniejszą i bezpieczniejszą zmianę.
