# DINBoard
## Instrukcja obsługi użytkownika

| Pole | Wartość |
|---|---|
| Dokument | Instrukcja obsługi aplikacji DINBoard |
| Wersja dokumentu | 1.1 |
| Data aktualizacji | 2026-03-17 |
| Odbiorca | użytkownik końcowy |
| Zakres | codzienna praca z aplikacją, bez opisu kodu i środowiska developerskiego |

---

## 1. Cel dokumentu

Niniejszy dokument opisuje praktyczną obsługę aplikacji DINBoard z punktu
widzenia użytkownika projektującego rozdzielnicę elektryczną.

Instrukcja jest przeznaczona dla:
- projektantów instalacji elektrycznych
- elektryków przygotowujących rozdzielnice
- osób wykonujących schematy, bilans mocy i dokumentację techniczną

Dokument nie opisuje:
- architektury kodu
- środowiska programistycznego
- procesu budowania aplikacji

---

## 2. Czym jest DINBoard

DINBoard to desktopowa aplikacja inżynierska do projektowania rozdzielnic
elektrycznych. Umożliwia:
- tworzenie i zapis projektów
- rozmieszczanie modułów na szynach DIN
- budowę schematu jednokreskowego
- zarządzanie obwodami
- bilansowanie obciążeń między fazami
- walidację konfiguracji elektrycznej
- eksport dokumentacji do PDF
- eksport danych pomocniczych do PNG, CSV BOM i LaTeX

Plik projektu jest zapisywany w formacie `JSON`.

---

## 3. Ogólny model pracy

Typowy przebieg pracy w DINBoard wygląda następująco:

1. Utwórz nowy projekt lub otwórz istniejący.
2. Uzupełnij dane projektu.
3. Skonfiguruj parametry zasilania.
4. Wygeneruj szynę DIN.
5. Dodaj moduły i zbuduj układ rozdzielnicy.
6. Przejdź do schematu jednokreskowego.
7. Uzupełnij lub popraw dane obwodów.
8. Sprawdź bilans mocy i bilans faz.
9. Przejrzyj wyniki walidacji.
10. Zapisz projekt i wykonaj eksport dokumentacji.

To jest zalecany workflow. Pominięcie wcześniejszych kroków zwykle prowadzi do
niepełnych danych w bilansie, walidacji albo eksporcie.

---

## 4. Ekran startowy

Po uruchomieniu aplikacji wyświetlany jest ekran startowy. Dostępne są na nim:
- `Nowy projekt`
- `Otwórz projekt`
- lista ostatnio otwieranych projektów
- informacja o trybie licencji
- przycisk aktywacji pełnej wersji w trybie próbnym

### 4.1 Nowy projekt

Utworzenie nowego projektu przygotowuje pusty stan roboczy aplikacji.

### 4.2 Otwórz projekt

Polecenie wczytuje zapisany wcześniej plik projektu `JSON`.

### 4.3 Ostatnie projekty

Lista ostatnich projektów pozwala otworzyć wybrany projekt bez ponownego
wskazywania pliku.

### 4.4 Tryb próbny i pełna wersja

Na ekranie startowym aplikacja informuje, czy działa w trybie próbnym, czy w
pełnej wersji. W trybie próbnym można użyć przycisku aktywacji pełnej wersji.

---

## 5. Układ interfejsu

Po wejściu do obszaru roboczego aplikacja jest podzielona na kilka stref.

### 5.1 Pasek górny

Pasek górny zawiera główne polecenia:
- operacje na projekcie
- zapis i odczyt
- eksport
- przełączanie arkuszy
- dostęp do generatora szyny DIN
- cofanie i ponawianie zmian
- ustawienia widoku

### 5.2 Lewy panel: moduły

Panel `Moduły` zawiera paletę aparatów podzieloną na kategorie. Stąd przeciąga
się elementy do projektu.

Jeżeli panel jest nieaktywny, zwykle oznacza to, że najpierw należy wygenerować
szynę DIN.

### 5.3 Lewy panel dolny: właściwości projektu

Panel `Właściwości Projektu` służy do uzupełnienia danych opisowych używanych w
dokumentacji, takich jak:
- nazwa projektu
- adres obiektu
- inwestor
- wykonawca
- numer dokumentu
- autor
- numer uprawnień
- rewizja
- uwagi

Z tego panelu dostępny jest także szybki eksport PDF.

### 5.4 Obszar środkowy: arkusze robocze

W środkowej części aplikacji dostępne są główne widoki pracy:
- `Szyna DIN`
- `Schemat jednokreskowy`

W zależności od kontekstu projektu może być również używana lista obwodów.

### 5.5 Prawy panel: konfiguracja i analiza

Prawy panel zawiera zakładki robocze:
- `Konfiguracja`
- `Bilans`
- `Walidacja`
- `Edycja obwodu`

Prawy panel można ukryć lub ponownie pokazać z menu `Widok`.

---

## 6. Tworzenie i otwieranie projektu

### 6.1 Utworzenie nowego projektu

1. Kliknij `Nowy projekt` na ekranie startowym albo użyj `Ctrl+N`.
2. Aplikacja otworzy pusty projekt.
3. Uzupełnij dane projektu i konfigurację zasilania przed rozpoczęciem eksportu.

### 6.2 Otwarcie istniejącego projektu

1. Kliknij `Otwórz projekt` albo użyj `Ctrl+O`.
2. Wskaż plik projektu w formacie `JSON`.
3. Po wczytaniu sprawdź dane projektu, konfigurację zasilania, arkusze i obwody.

### 6.3 Zapis projektu

Do dyspozycji są dwa podstawowe polecenia:
- `Ctrl+S` - zapis bieżący
- `Ctrl+Shift+S` - zapis pod nową nazwą lub do nowej lokalizacji

Przed zamknięciem aplikacji należy upewnić się, że projekt został zapisany.

---

## 7. Uzupełnianie danych projektu

Panel `Właściwości Projektu` służy do przygotowania danych potrzebnych do
dokumentacji technicznej.

Zaleca się uzupełnienie co najmniej:
- nazwy projektu
- adresu obiektu
- numeru dokumentu
- autora
- rewizji

Brak tych danych może skutkować niepełnym eksportem dokumentacji PDF.

---

## 8. Konfiguracja zasilania

Zakładka `Konfiguracja` umożliwia ustawienie podstawowych parametrów
elektrycznych projektu:
- napięcia sieci
- liczby faz
- zabezpieczenia głównego
- mocy przyłączeniowej

Dostępna jest również sekcja ustawień RCD.

Zalecenie:
- skonfiguruj zasilanie przed uruchamianiem bilansu i walidacji
- po większych zmianach sprawdź, czy parametry nadal odpowiadają projektowi

---

## 9. Generowanie szyny DIN

Przed rozpoczęciem pracy z modułami należy przygotować szynę DIN.

### 9.1 Kiedy generować szynę DIN

Szynę DIN należy wygenerować na początku pracy z układem rozdzielnicy, zanim
zacznie się dodawanie aparatów z palety modułów.

### 9.2 Jak wygenerować szynę DIN

1. Użyj polecenia `Szyna DIN` z paska narzędzi lub odpowiedniego przycisku.
2. Uzupełnij wymagane parametry w oknie dialogowym.
3. Zatwierdź generowanie.

Po poprawnym wygenerowaniu szyny:
- aktywuje się panel modułów
- możliwe staje się rozmieszczanie elementów
- dostępne są mechanizmy przyciągania i układu

---

## 10. Dodawanie i obsługa modułów

### 10.1 Dodawanie modułów z palety

1. Otwórz właściwą kategorię w panelu `Moduły`.
2. Wybierz element.
3. Przeciągnij go na obszar roboczy.
4. Upuść w docelowym miejscu.

### 10.2 Rozmieszczanie modułów

Podczas rozmieszczania aplikacja wspiera użytkownika przez:
- przyciąganie
- wyrównanie do układu szyny DIN
- mechanizmy automatycznego porządkowania

### 10.3 Operacje na zaznaczonym module

Na zaznaczonym elemencie można wykonywać podstawowe operacje, takie jak:
- duplikowanie
- usuwanie
- edycja parametrów

Skróty:
- `Ctrl+D` - duplikowanie zaznaczonego elementu
- `Delete` - usunięcie zaznaczonego elementu

---

## 11. Praca na arkuszach

### 11.1 Arkusz `Szyna DIN`

Widok ten służy do:
- układania modułów w rozdzielnicy
- kontroli zajętości szyn
- przygotowania danych do dalszego schematu i eksportu

### 11.2 Arkusz `Schemat jednokreskowy`

Widok ten służy do:
- budowy logicznego schematu instalacji
- weryfikacji relacji między elementami
- przygotowania rysunku do dokumentacji

Przełączanie arkuszy jest dostępne z menu `Widok`.

---

## 12. Edycja obwodów

Zakładka `Edycja obwodu` służy do zmiany parametrów zaznaczonego obiektu.

W zależności od typu aparatu można tam ustawić na przykład:
- oznaczenie
- nazwę obwodu
- fazę
- zabezpieczenie
- parametry kabla
- ustawienia charakterystyczne dla konkretnego typu modułu

Jeżeli panel edycji jest pusty:
- upewnij się, że zaznaczono właściwy element
- w razie potrzeby użyj dwukliku na module
- sprawdź, czy obiekt jest edytowalny w bieżącym widoku

---

## 13. Bilans mocy i faz

Zakładka `Bilans` prezentuje najważniejsze dane obliczeniowe projektu:
- moc zainstalowaną
- moc obliczeniową
- współczynnik jednoczesności
- obciążenie faz `L1`, `L2`, `L3`
- asymetrię faz

### 13.1 Automatyczne bilansowanie

W sekcji automatycznego bilansowania dostępne są:
- automatyczne bilansowanie faz
- cofnięcie ostatniego bilansowania

### 13.2 Kiedy uruchamiać bilansowanie

Bilansowanie należy wykonać ponownie po:
- dodaniu nowych obwodów
- zmianie mocy lub parametrów aparatów
- zmianie przypisań faz
- większej przebudowie schematu

### 13.3 Dobra praktyka

Po zakończeniu bilansowania należy od razu przejść do zakładki `Walidacja` i
sprawdzić, czy nie pojawiły się ostrzeżenia lub błędy.

---

## 14. Walidacja projektu

Zakładka `Walidacja` prezentuje komunikaty kontrolne dotyczące konfiguracji
elektrycznej projektu.

Typowe obszary kontroli:
- asymetria obciążeń
- zgodność zabezpieczeń
- przeciążenia
- spadki napięcia
- niespójności konfiguracji

Walidację należy traktować jako obowiązkowy etap przed zapisaniem finalnej
dokumentacji.

---

## 15. Eksport dokumentacji i danych

DINBoard udostępnia kilka form eksportu.

### 15.1 Eksport PDF

Jest to podstawowa forma dokumentacji technicznej generowanej z projektu.

### 15.2 Szybki eksport PDF

W panelu `Właściwości Projektu` znajduje się przycisk szybkiego eksportu PDF.
Jest to wygodny sposób na odświeżenie dokumentacji po korekcie danych opisowych.

### 15.3 Eksport PNG

Dostępne są warianty:
- PNG czysty
- PNG z oznaczeniami

### 15.4 Eksport CSV BOM

Eksport `Zestawienie CSV` służy do przygotowania zestawienia materiałowego.

### 15.5 Eksport LaTeX

Eksport `.tex` może być używany w zewnętrznym procesie dalszej obróbki
dokumentacji.

Zalecenie:
- przed eksportem sprawdź dane projektu, bilans oraz walidację

---

## 16. Skróty klawiaturowe

Najważniejsze skróty dostępne w aplikacji:

| Skrót | Działanie |
|---|---|
| `Ctrl+N` | nowy projekt |
| `Ctrl+O` | otwarcie projektu |
| `Ctrl+S` | zapis projektu |
| `Ctrl+Shift+S` | zapis projektu jako |
| `Ctrl+Z` | cofnięcie |
| `Ctrl+Y` | ponowienie |
| `Ctrl+D` | duplikowanie zaznaczonego elementu |
| `Delete` | usunięcie zaznaczonego elementu |
| `Ctrl+P` | eksport PDF |

---

## 17. Dobre praktyki pracy

Rekomendowany sposób pracy:
- najpierw uzupełnij dane projektu
- skonfiguruj zasilanie przed walidacją
- wygeneruj szynę DIN przed dodawaniem modułów
- po zmianach w obwodach wykonaj ponownie bilansowanie
- po bilansowaniu zawsze przejrzyj walidację
- zapisuj projekt regularnie
- eksport końcowy wykonuj dopiero po sprawdzeniu ostrzeżeń i błędów

---

## 18. Typowe problemy i rozwiązania

### Problem: nie mogę dodać modułów

Możliwa przyczyna:
- nie wygenerowano jeszcze szyny DIN

Rozwiązanie:
- wygeneruj szynę DIN i ponownie sprawdź panel `Moduły`

### Problem: panel edycji obwodu jest pusty

Możliwa przyczyna:
- nie zaznaczono właściwego elementu
- obiekt nie jest aktualnie aktywny do edycji

Rozwiązanie:
- zaznacz element ponownie
- użyj dwukliku na module
- upewnij się, że pracujesz na właściwym arkuszu

### Problem: walidacja pokazuje ostrzeżenia po zmianach w projekcie

Możliwa przyczyna:
- po zmianie obwodów nie wykonano ponownego bilansu
- konfiguracja zasilania nie odpowiada aktualnemu projektowi

Rozwiązanie:
- sprawdź zakładkę `Konfiguracja`
- uruchom ponownie bilansowanie
- wróć do zakładki `Walidacja`

### Problem: eksport PDF nie zawiera pełnych danych opisowych

Możliwa przyczyna:
- nie uzupełniono właściwości projektu

Rozwiązanie:
- uzupełnij dane projektu
- wykonaj ponowny eksport PDF

### Problem: przypadkowo zmieniłem układ projektu

Rozwiązanie:
- użyj `Ctrl+Z`
- w razie potrzeby wróć do ostatnio zapisanej wersji projektu

---

## 19. Odpowiedzialność użytkownika

DINBoard wspiera pracę inżynierską, ale nie zastępuje decyzji projektowych
użytkownika.

Przed przekazaniem dokumentacji dalej użytkownik powinien każdorazowo:
- zweryfikować poprawność danych wejściowych
- sprawdzić wyniki bilansu
- przeanalizować komunikaty walidacji
- potwierdzić poprawność końcowego eksportu

---

## 20. Podsumowanie

Najkrótszy poprawny przebieg pracy w DINBoard:

1. Otwórz lub utwórz projekt.
2. Uzupełnij dane projektu.
3. Skonfiguruj zasilanie.
4. Wygeneruj szynę DIN.
5. Dodaj moduły i przygotuj schemat.
6. Sprawdź bilans i walidację.
7. Zapisz projekt.
8. Wyeksportuj dokumentację.

Dokument należy aktualizować razem z istotnymi zmianami interfejsu i workflow
użytkownika.
