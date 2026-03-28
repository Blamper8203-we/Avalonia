# ARCHITECTURE_MAP.md

## Przegląd
DINBoard to desktopowa aplikacja inżynierska do projektowania rozdzielnic elektrycznych.

Główny przepływ odpowiedzialności:
Views -> ViewModels -> Services -> Models

Dodatkowo istnieją:
- Controls jako specjalistyczna warstwa UI edytora
- Dialogs jako lokalne przepływy konfiguracji i edycji
- subsystem eksportu i zapisu danych
- subsystem schematów i canvas
- subsystem logiki elektrycznej

---

## 1. Warstwa prezentacji

### Views
Widoki odpowiadają za:
- układ UI
- powiązania bindingów
- strukturę ekranów i paneli
- zachowanie wizualne

Kluczowe pliki:
- MainWindow.axaml
- Views/CircuitEditPanelView.axaml
- Views/CircuitListView.axaml
- Views/GroupedCircuitsPanel.axaml
- Views/HomeScreenView.axaml
- Views/ModulesPaletteView.axaml
- Views/PowerBalancePanel.axaml
- Views/PowerConfigPanel.axaml
- Views/ProjectPropertiesView.axaml

### ViewModels
ViewModele odpowiadają za:
- stan UI
- komendy
- logikę prezentacji
- koordynację usług
- sterowanie przepływem danych między UI a usługami

Kluczowe pliki:
- ViewModels/MainViewModel.cs
- ViewModels/ProjectWorkspaceViewModel.cs
- ViewModels/SchematicViewModel.cs
- ViewModels/PowerBalanceViewModel.cs
- ViewModels/ValidationViewModel.cs
- ViewModels/CircuitListViewModel.cs
- ViewModels/LayoutViewModel.cs
- ViewModels/GroupViewModel.cs
- ViewModels/ExportViewModel.cs
- ViewModels/ProjectThemeViewModel.cs
- ViewModels/SymbolManagerViewModel.cs

Aktualne uwagi:
- MainViewModel jest koordynatorem i sklada child ViewModel-e, ale nie powinien odzyskiwac odpowiedzialnosci juz wydzielonych do Workspace, Export, Layout, Validation i Schematic
- ProjectWorkspaceViewModel odpowiada za lifecycle projektu: zapis, odczyt, recent projects, metadane i stan licencji na ekranie startowym
- Views/CircuitEditPanelView.* powinny zostac cienkie: zestaw pol formularza definiuje Services/CircuitEditFieldDefinitionProvider.cs, a mapowanie wartosci na model trzyma Services/CircuitEditValueApplier.cs

---

## 2. Subsystem canvas / edytor schematów

To jeden z najbardziej wrażliwych i krytycznych subsystemów projektu.

### Controls
Pliki:
- Controls/SingleLineDiagramCanvas.axaml
- Controls/SingleLineDiagramCanvas.cs
- Controls/SkiaRenderControl.cs
- Controls/VirtualizingCanvasPanel.cs
- Controls/DinRailView.cs
- Controls/SymbolControl.axaml
- Controls/SymbolControl.axaml.cs
- Controls/RadialMenu.cs
- Controls/CircuitReferenceControl.axaml
- Controls/CircuitReferenceControl.axaml.cs

Odpowiedzialności:
- renderowanie elementów schematu
- kontrola widocznych obszarów
- interakcje użytkownika
- integracja z drag and drop
- wizualizacja elementów i relacji
- obsługa specjalistycznych kontrolek edytora

### Services wspierające edytor
Pliki:
- Services/SchematicCanvasController.cs
- Services/SchematicCanvasZoomService.cs
- Services/SchematicCellEditController.cs
- Services/SchematicClonePlacementHelper.cs
- Services/SchematicDinRailController.cs
- Services/SchematicDragDropController.cs
- Services/SchematicElementFactory.cs
- Services/SchematicLayoutAnimator.cs
- Services/SchematicLayoutEngine.cs
- Services/SchematicNodeBuilderService.cs
- Services/SchematicPaginationService.cs
- Services/SchematicSnapService.cs

Odpowiedzialności:
- sterowanie stanem canvas
- obsługa zoom
- obsługa komórek i edycji
- klonowanie i rozmieszczanie elementów
- logika DIN rail
- drag and drop
- tworzenie elementów schematu
- wyliczanie układu
- animacja układu
- budowanie relacji i węzłów
- paginacja
- snapping

Ryzyko:
Zmiany tutaj mogą wpłynąć na:
- wydajność
- poprawność renderowania
- zachowanie myszy i przeciągania
- poprawność rozmieszczenia elementów
- stabilność edycji
- spójność zachowania całego edytora

---

## 3. Subsystem logiki elektrycznej

To najbardziej wrażliwy obszar domenowy.

Kluczowe pliki:
- Services/PhaseDistributionCalculator.cs
- Services/ElectricalValidationService.cs
- Services/PowerBusbarGenerator.cs
- Services/BusbarPlacementService.cs
- Services/SchematicNodeBuilderService.cs

Odpowiedzialności:
- bilansowanie obciążeń między fazami
- walidacja konfiguracji elektrycznej
- przygotowanie danych do układu i raportów
- budowanie logicznych relacji między elementami
- wspieranie zestawień i dokumentacji

Aktualne uwagi:
- Services/PhaseDistributionCalculator.cs pozostaje glownym serwisem domenowym bilansowania faz
- planowanie bilansowania jest wydzielone za CreateBalancePlan, ale wynik elektryczny ma pozostac zgodny z obecnym algorytmem
- Services/PhaseDistributionExecutionHelper.cs odpowiada za techniczne wykonanie i animowane zastosowanie zmian planu
- przygotowanie planu, refinement i wykonanie zmian maja pozostac rozdzielone zamiast wracac do jednego duzego bloku
- nie nalezy mieszac z powrotem animacji UI i logiki obliczeniowej w jednym bloku kodu

Ryzyko:
Zmiany tutaj mogą:
- zmienić poprawność obliczeń
- zmienić wyniki walidacji
- wpłynąć na raporty i zestawienia
- wpłynąć na działanie paneli bilansu i walidacji
- wpłynąć na dane eksportowane do PDF

Zasada:
Nie zmieniać po cichu.
Najpierw trzeba opisać:
- co działa teraz
- co jest problemem
- jaki będzie wpływ zmiany
- jaka jest najbezpieczniejsza poprawka

---

## 4. Persistence i lifecycle projektu

Kluczowe pliki:
- Services/ProjectPersistenceService.cs
- Services/ProjectService.cs
- Services/RecentProjectsService.cs

Odpowiedzialności:
- zapis projektu
- odczyt projektu
- trwałość danych
- zarządzanie bieżącym projektem
- lista ostatnich projektów
- metadane i przepływ pracy z projektem

Ryzyko:
Błędy w tej części mogą:
- uszkodzić zapis projektu
- zepsuć odczyt projektu
- złamać zgodność danych
- zepsuć workflow użytkownika

---

## 5. Subsystem eksportu

### Główne pliki
- Services/PdfExportService.cs
- Services/BomExportService.cs
- Services/ExcelImportService.cs
- Services/SymbolImportService.cs
- Services/SvgModuleImporter.cs
- Services/SvgProcessor.cs
- Services/SvgHelper.cs

### PDF
Pliki:
- Services/Pdf/PdfCircuitTableService.cs
- Services/Pdf/PdfCircuitWiringService.cs
- Services/Pdf/PdfConnectionService.cs
- Services/Pdf/PdfDinRailService.cs
- Services/Pdf/PdfPowerBalanceService.cs
- Services/Pdf/PdfSingleLineDiagramService.cs
- Services/Pdf/PdfStandardsService.cs
- Services/Pdf/PdfTitlePageService.cs
- Services/Pdf/PdfExportOptions.cs

Odpowiedzialności:
- generowanie dokumentacji PDF
- zestawienia obwodów
- dane okablowania
- połączenia
- układ DIN rail
- bilans mocy
- schemat jednokreskowy
- sekcje standardów
- strona tytułowa
- opcje eksportu

Ryzyko:
Zmiany tutaj mogą:
- zepsuć dokumentację
- zmienić dane inżynierskie w raporcie
- pogorszyć układ PDF
- uszkodzić zgodność eksportu

---

## 6. Infrastruktura edycji

Kluczowe pliki:
- Services/UndoRedoService.cs
- Services/UndoableCommands.cs

Odpowiedzialności:
- historia operacji
- cofanie
- ponawianie
- spójność zmian edytora

Ryzyko:
Błędy tutaj niszczą zaufanie do aplikacji, bo użytkownik przestaje mieć pewność, co zostanie cofnięte i w jakim stanie pozostanie projekt.

---

## 7. Models

Kluczowe pliki:
- Models/Project.cs
- Models/Circuit.cs
- Models/CircuitReference.cs
- Models/SchematicNode.cs
- Models/SymbolItem.cs
- Models/ExcelImportRow.cs
- Models/GroupFrameInfo.cs
- Models/LicenseInfo.cs

Odpowiedzialności:
- stan projektu
- dane obwodów
- relacje referencyjne
- stan i dane schematu
- symbole i zasoby
- dane importu
- informacje grupowania
- informacje licencyjne

---

## 8. Dialogs

Kluczowe pliki:
- Dialogs/BusbarGeneratorDialog.axaml
- Dialogs/CircuitConfigDialog.axaml
- Dialogs/CircuitEditDialog.axaml
- Dialogs/DinRailDialog.axaml
- Dialogs/ExcelImportDialog.axaml
- Dialogs/GroupModulesDialog.axaml
- Dialogs/ImportModulesDialog.axaml
- Dialogs/ModuleParametersDialog.axaml
- Dialogs/PdfExportDialog.axaml
- Dialogs/ProjectMetadataDialog.axaml

Odpowiedzialności:
- lokalne przepływy konfiguracji
- edycja parametrów
- import
- eksport
- metadane projektu
- ustawienia specyficzne dla modułów i rozdzielnicy

Ryzyko:
Najczęściej średnie, ale może rosnąć, jeśli dialog bezpośrednio wpływa na dane domenowe lub eksport.

---

## 9. Converters / Helpers / Resources / Styles

### Converters
- BoolToBrushConverter.cs
- BoolToRotateConverter.cs
- ColorToBrushConverter.cs
- InverseBoolConverter.cs

### Helpers
- Helpers/LocalizationHelper.cs

### Resources
- Resources/Strings.axaml

### Styles
- Styles/RadialMenu.axaml

Odpowiedzialności:
- wsparcie UI
- konwersje bindingów
- lokalizacja
- zasoby i style

Ryzyko:
Zwykle niższe niż w logice domenowej i canvas, ale błędy mogą psuć UX, wygląd albo powiązania bindingów.

---

## 10. Testy

Ważne testy wskazujące krytyczne strefy:
- Tests/PhaseDistributionCalculatorTests.cs
- Tests/ElectricalValidationTests.cs
- Tests/SchematicLayoutEngineTests.cs
- Tests/SchematicDragDropControllerTests.cs
- Tests/SchematicViewModelTests.cs
- Tests/PowerBalanceViewModelTests.cs
- Tests/UndoRedoTests.cs
- Tests/PdfExportTests.cs
- Tests/ProjectRoundTripTests.cs
- Tests/ValidationViewModelTests.cs
- Tests/LayoutViewModelTests.cs
- Tests/MainViewModelTests.cs

Interpretacja:
Jeśli zmiana dotyka jednego z tych obszarów, należy:
- przejrzeć istniejące testy
- uruchomić je
- rozszerzyć je, jeśli logika się zmienia

---

## 11. Podsumowanie krytyczności

### Najwyższa krytyczność
- obliczenia elektryczne
- walidacja
- silnik layoutu
- canvas rendering
- drag and drop
- snapping
- undo/redo
- zapis i odczyt projektu

### Średnia krytyczność
- eksport PDF
- import
- dialogi wpływające na dane domenowe
- import symboli i modułów SVG

### Niższa krytyczność
- czysto wizualny polish UI
- konwertery
- zasoby
- style
- niewielkie poprawki tekstów i lokalizacji

---

## 12. Zasady pracy agenta w tym repo
Przy każdej zmianie:
1. najpierw ustal subsystem
2. oceń poziom ryzyka
3. wybierz najmniejszą bezpieczną zmianę
4. zachowaj dotychczasowe działanie, jeśli nie proszono o zmianę logiki
5. wskaż co trzeba przetestować
6. unikaj szerokich refaktorów obejmujących wiele subsystemów naraz
