# Struktura Serwisu PDF

W ramach refaktoryzacji, logika generowania PDF została podzielona na mniejsze, wyspecjalizowane serwisy. Poniżej znajduje się opis odpowiedzialności poszczególnych plików.

## Główny Koordynator (w folderze nadrzędnym `Services/`)
*   **`PdfExportService.cs`** - **NIE USUWAĆ!** Jest to główny punkt wejścia. Aplikacja wywołuje ten serwis, aby rozpocząć eksport. Koordynuje on pracę pozostałych serwisów i składa dokument w całość.

## Serwisy Składowe (w folderze `Services/Pdf/`)
*   **`PdfDinRailService.cs`** - Odpowiada za wizualizację szyny DIN, rysowanie modułów, grup oraz generowanie nagłówków i stopek stron.
*   **`PdfCircuitWiringService.cs`** - Generuje schemat połączeń elektrycznych (Sheet 2) wraz z odsyłaczami i liniami fazowymi.
*   **`PdfCircuitTableService.cs`** - Generuje tabelę z listą obwodów i ich parametrami.
*   **`PdfPowerBalanceService.cs`** - Oblicza i prezentuje bilans mocy zainstalowanej oraz rozkład na fazy.
*   **`PdfTitlePageService.cs`** - Tworzy stronę tytułową dokumentacji.
*   **`PdfStandardsService.cs`** - Generuje sekcję zgodności z normami PN-HD.
*   **`PdfExportOptions.cs`** - Klasa zawierająca opcje konfiguracji eksportu (np. widoczność grup, klamr).
