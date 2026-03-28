# AI_CONTEXT.md

## Nazwa projektu
DINBoard

## Typ projektu
Desktopowa aplikacja inżynierska dla elektryków.

## Technologie
- C#
- .NET 10
- Avalonia UI
- MVVM

## Główne zadania aplikacji
DINBoard służy do:
- projektowania rozdzielnic elektrycznych
- rozmieszczania modułów na szynach DIN
- zarządzania obwodami
- grupowania modułów
- tworzenia i edycji schematów
- bilansowania obciążeń między fazami
- walidacji konfiguracji elektrycznej
- eksportu dokumentacji do PDF
- importu danych, w tym z Excela
- zapisu i odczytu projektów

## Ogólny podział projektu
Repozytorium jest podzielone na:
- Models
- Services
- ViewModels
- Views
- Controls
- Dialogs
- Tests
- Assets
- Converters
- Helpers
- Resources
- Styles

## Główna architektura
Aplikacja bazuje na MVVM.
Typowy przepływ odpowiedzialności wygląda tak:

Views -> ViewModels -> Services -> Models

Dodatkowo istnieje rozbudowany subsystem canvas / schematów, który łączy:
- Controls
- Services związane z układem, drag and drop, snappingiem i renderowaniem
- modele schematu

## Aktualne punkty architektury
- MainViewModel jest glownym koordynatorem UI, ale lifecycle projektu jest wydzielony do ViewModels/ProjectWorkspaceViewModel.cs
- Views/CircuitEditPanelView.* renderuja formularz z definicji z Services/CircuitEditFieldDefinitionProvider.cs
- mapowanie wartosci formularza na SymbolItem trzyma Services/CircuitEditValueApplier.cs
- Services/PhaseDistributionCalculator.cs pozostaje glownym serwisem domenowym bilansowania faz
- Services/PhaseDistributionExecutionHelper.cs odpowiada za techniczne wykonanie i animowane zastosowanie zmian planu, bez mieszania tego z obliczeniami elektrycznymi
- planowanie bilansowania jest wydzielone za CreateBalancePlan, a wynik elektryczny ma pozostac zgodny z obecnym algorytmem

## Dokumenty startowe
Najwazniejsze dokumenty orientacyjne w repo:
- START_HERE.md
- QUICK_START.md
- CODE_QUALITY.md
- PREVENTING_CODE_MESS.md

## Najważniejsze obszary funkcjonalne
### 1. Edytor schematów
Obejmuje:
- rysowanie i renderowanie elementów
- układ modułów
- drag and drop
- snapping
- paginację
- zarządzanie elementami na schemacie
- relacje między elementami

### 2. Logika elektryczna
Obejmuje:
- bilansowanie faz
- walidację parametrów
- budowanie relacji połączeń
- logikę używaną do zestawień i raportów

### 3. Zarządzanie projektem
Obejmuje:
- zapis projektu
- odczyt projektu
- metadane projektu
- ostatnie projekty
- trwałość danych

### 4. Eksport i import
Obejmuje:
- eksport PDF
- eksport BOM
- import z Excela
- import symboli i modułów SVG

### 5. Infrastruktura edycji
Obejmuje:
- undo/redo
- komendy edycyjne
- obsługę zmian na schemacie

## Kluczowe pliki i subsystemy
### Logika elektryczna
- Services/PhaseDistributionCalculator.cs
- Services/ElectricalValidationService.cs
- Services/PowerBusbarGenerator.cs
- Services/BusbarPlacementService.cs
- Services/SchematicNodeBuilderService.cs

### Edytor schematów / canvas
- Controls/SingleLineDiagramCanvas.axaml
- Controls/SingleLineDiagramCanvas.cs
- Controls/SkiaRenderControl.cs
- Controls/VirtualizingCanvasPanel.cs
- Services/SchematicCanvasController.cs
- Services/SchematicDragDropController.cs
- Services/SchematicDinRailController.cs
- Services/SchematicLayoutEngine.cs
- Services/SchematicLayoutAnimator.cs
- Services/SchematicSnapService.cs
- Services/SchematicPaginationService.cs
- Services/SchematicClonePlacementHelper.cs
- Services/SchematicElementFactory.cs

### Infrastruktura projektu i danych
- Services/ProjectPersistenceService.cs
- Services/ProjectService.cs
- Services/RecentProjectsService.cs
- ViewModels/ProjectWorkspaceViewModel.cs

### Undo / redo
- Services/UndoRedoService.cs
- Services/UndoableCommands.cs

### Eksport PDF
- Services/PdfExportService.cs
- Services/Pdf/*

## Obszary wysokiego ryzyka
Za szczególnie ryzykowne uznawaj:
- obliczenia elektryczne
- logikę walidacji
- renderowanie canvas
- drag and drop
- snapping
- silnik layoutu
- paginację schematu
- undo/redo
- zapis i odczyt projektu
- generowanie PDF

## Czego agent ma pilnować
Podczas pracy nad projektem agent powinien zawsze:
1. zachowywać obecne działanie, jeśli nie poproszono o jego zmianę
2. preferować małe i bezpieczne zmiany
3. szanować granice MVVM
4. nie przenosić logiki biznesowej do widoków
5. nie robić dużych refaktorów bez potrzeby
6. zachowywać wydajność renderowania i interakcji
7. zachowywać poprawność logiki elektrycznej
8. zachowywać spójność undo/redo
9. zachowywać zgodność zapisu i odczytu projektów
10. wskazywać co trzeba przetestować po zmianie

## Definicja bezpiecznej zmiany
Bezpieczna zmiana to taka, która:
- ma jasny zakres
- nie zmienia po cichu logiki elektrycznej
- nie pogarsza responsywności UI
- nie psuje bindingów
- nie psuje zapisu i odczytu projektu
- nie psuje undo/redo
- nie wprowadza zbędnego chaosu architektonicznego

## Preferowany styl zmian
Preferowane:
- małe refaktory
- ekstrakcja metod
- poprawa nazewnictwa
- porządkowanie odpowiedzialności
- lokalne poprawki wydajności
- ukierunkowane testy

Niepreferowane:
- szerokie przepisywanie działającego kodu
- zmiany wielu subsystemów naraz bez potrzeby
- dokładanie nowych frameworków
- zmiany architektoniczne bez wyraźnego celu
- mieszanie refaktoru z nową funkcją w jednym kroku

## Oczekiwany styl odpowiedzi agenta
Przy propozycji zmian w kodzie używaj struktury:
- Problem
- Przyczyna
- Bezpieczna poprawka
- Kod
- Co przetestować

## Ważna uwaga
DINBoard nie jest projektem demonstracyjnym.
To aplikacja inżynierska z logiką wrażliwą domenowo.
Każda zmiana dotycząca bilansowania, walidacji, układu schematu, eksportu, zapisu projektu albo infrastruktury edycyjnej musi być wykonywana ostrożnie i z jasnym uzasadnieniem.
