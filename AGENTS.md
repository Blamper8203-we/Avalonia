# AGENTS.md

## Projekt
DINBoard

## Stos technologiczny
- C#
- .NET 10
- Avalonia UI
- MVVM

## Czym jest projekt
DINBoard to desktopowa aplikacja inżynierska dla elektryków.
Służy do projektowania rozdzielnic elektrycznych, rozmieszczania modułów na szynach DIN, zarządzania obwodami, bilansowania obciążeń między fazami, walidacji parametrów instalacji oraz generowania dokumentacji.

## Cel pracy agenta
Agent ma pomagać w rozwijaniu i utrzymaniu istniejącej aplikacji produkcyjnej.
Najważniejsze są:
1. poprawność
2. stabilność
3. bezpieczeństwo zmian
4. utrzymanie architektury
5. czytelność kodu
6. wydajność

## Zachowanie obowiązkowe przed każdą większą zmianą
Zanim wprowadzisz zmiany w kodzie:
1. Przeczytaj `AI_CONTEXT.md`
2. Przeczytaj `ARCHITECTURE_MAP.md`
3. Ustal, którego subsystemu dotyczy zadanie
4. Oceń, czy zadanie dotyczy części krytycznej
5. Zastosuj najmniejszą bezpieczną zmianę
6. Nie zmieniaj zachowania aplikacji bez wyraźnej prośby

## Części krytyczne
Traktuj poniższe pliki i obszary jako wysokiego ryzyka i zmieniaj je bardzo ostrożnie:

### Logika elektryczna
- Services/PhaseDistributionCalculator.cs
- Services/ElectricalValidationService.cs
- Services/PowerBusbarGenerator.cs
- Services/BusbarPlacementService.cs
- Services/SchematicNodeBuilderService.cs

### Canvas / schemat / układ
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

### Infrastruktura edycji
- Services/UndoRedoService.cs
- Services/UndoableCommands.cs

### Zapis / odczyt / eksport
- Services/ProjectPersistenceService.cs
- Services/ProjectService.cs
- Services/PdfExportService.cs
- Services/Pdf/*

## Główne zasady pracy
- Zachowuj obecne działanie aplikacji, chyba że wyraźnie poproszono o zmianę zachowania.
- Preferuj małe, lokalne i bezpieczne zmiany.
- Nie przepisuj dużych działających fragmentów bez mocnego powodu.
- Szanuj istniejącą architekturę MVVM.
- Nie przenoś logiki biznesowej ani obliczeniowej do widoków.
- Nie zmieniaj po cichu wzorów elektrycznych, założeń obliczeń, logiki walidacji ani bilansowania faz.
- Zachowuj wydajność renderowania i zachowanie interakcji w kodzie canvas.
- Zachowuj poprawność undo/redo.
- Zachowuj zgodność zapisu i odczytu projektu, jeśli zadanie nie wymaga zmiany formatu.
- Zachowuj działanie eksportu PDF, jeśli zadanie nie dotyczy eksportu.
- Unikaj zbędnych zależności i niepotrzebnych abstrakcji.
- Nie wprowadzaj dużych refaktorów przy okazji małych poprawek.

## Zasady architektury
- Views: tylko UI, bindingi i zachowanie ściśle wizualne
- ViewModels: logika prezentacji, stan UI, komendy i orkiestracja
- Services: logika współdzielona, domenowa, techniczna i infrastrukturalna
- Models: stan projektu, encje domenowe i dane wejściowe/wyjściowe
- Controls: specjalistyczne elementy UI i edytora
- Dialogs: lokalne przepływy konfiguracji i edycji

## Zasady dla Avalonia UI
- Nie blokuj wątku UI.
- Uważaj na częstotliwość odświeżania canvas.
- Uważaj na koszty renderowania.
- Uważaj na pointer events, drag and drop i snapping.
- Nie psuj bindingów.
- Nie przenoś logiki domenowej do code-behind.

## Zasady dla canvas i schematów
Kod edytora schematów jest szczególnie wrażliwy.
Przy zmianach w tej części:
1. Opisz bieżącą odpowiedzialność kodu
2. Opisz możliwe skutki uboczne
3. Zaproponuj najmniejszą bezpieczną zmianę
4. Unikaj regresji wydajności
5. Unikaj regresji zachowania interakcji
6. Zachowuj spójność układu, drag and drop, snappingu i paginacji

## Zasady dla logiki elektrycznej
Ta aplikacja zawiera rzeczywistą logikę inżynierską.
Nie wolno po cichu zmieniać:
- bilansowania faz
- obciążeń obwodów
- walidacji parametrów instalacji
- logiki dotyczącej połączeń i relacji schematu
- danych używanych w podsumowaniach mocy
- danych trafiających do dokumentacji PDF

Jeżeli aktualne zachowanie wydaje się błędne:
1. najpierw opisz problem
2. wyjaśnij dlaczego to może być błąd
3. opisz wpływ zmiany
4. dopiero potem zaproponuj bezpieczną poprawkę

## Zasady dla undo/redo
Undo/Redo to infrastruktura krytyczna.
Każda zmiana operacji edycyjnych musi zachować:
- spójność cofania
- spójność ponawiania
- integralność stanu obiektów
- przewidywalność działań użytkownika

## Zasady wydajności
- Unikaj zbędnych alokacji.
- Unikaj kosztownego LINQ w gorących ścieżkach.
- Nie wykonuj ciężkiej logiki w render loop.
- Nie wykonuj ciężkiej logiki na wątku UI.
- Dbaj o wydajność dla większych projektów i większej liczby elementów schematu.

## Zasady refaktoryzacji
Gdy refaktoryzujesz:
- zachowaj identyczne działanie, jeśli nie proszono o zmianę logiki
- nie mieszaj dużego refaktoru z nową funkcją bez wyraźnej prośby
- zachowuj istniejące interfejsy publiczne, jeśli to możliwe
- zachowuj kompatybilność bindingów, serializacji i przepływu komend
- preferuj małe ekstrakcje metod, poprawę nazw i uproszczenie odpowiedzialności

## Preferowany format odpowiedzi dla zadań nietrywialnych
Dla zadań innych niż trywialne odpowiadaj w strukturze:
1. Problem
2. Przyczyna
3. Bezpieczna poprawka
4. Kod
5. Co przetestować

## Preferencja aktualizacji plików
Jeśli modyfikujesz plik, preferuj zwracanie pełnej zaktualizowanej zawartości pliku, chyba że zadanie wyraźnie wymaga minimalnego diffu.

## Zasady testów
Jeśli zadanie dotyczy logiki, wskaż jakie testy trzeba:
- uruchomić
- dodać
- poprawić

Zwracaj szczególną uwagę na:
- Tests/PhaseDistributionCalculatorTests.cs
- Tests/ElectricalValidationTests.cs
- Tests/SchematicLayoutEngineTests.cs
- Tests/SchematicDragDropControllerTests.cs
- Tests/SchematicViewModelTests.cs
- Tests/PowerBalanceViewModelTests.cs
- Tests/UndoRedoTests.cs
- Tests/PdfExportTests.cs
- Tests/ProjectRoundTripTests.cs

## Preferowany styl pracy
- techniczny
- praktyczny
- ostrożny
- konkretny
- bez ogólników
- z naciskiem na minimalną bezpieczną zmianę