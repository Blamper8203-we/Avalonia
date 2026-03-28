# DOMAIN_RULES.md

## Cel pliku
Ten plik opisuje zasady domenowe projektu DINBoard.
Agent ma traktować je jako ograniczenia bezpieczeństwa przy pracy nad logiką elektryczną, walidacją, bilansem mocy, schematem i eksportem.

## Charakter projektu
DINBoard to aplikacja inżynierska dla elektryków.
To nie jest projekt demonstracyjny.
Zmiany w logice domenowej mogą wpływać na realne decyzje projektowe użytkownika, dlatego muszą być wykonywane ostrożnie, jawnie i z uzasadnieniem.

## Główne obszary domenowe
- obwody
- rozdzielnice
- fazy L1, L2, L3
- bilans mocy / bilans obciążenia
- walidacja parametrów
- relacje schematu
- dokumentacja eksportowa

## Zasady ogólne
- Nie zmieniaj po cichu logiki domenowej.
- Nie zmieniaj wzorów ani założeń obliczeń bez wyraźnej potrzeby i wyjaśnienia.
- Nie zmieniaj wyników bilansowania tylko dlatego, że inny algorytm wydaje się „ładniejszy”.
- Nie zmieniaj progów walidacji bez wyraźnego uzasadnienia.
- Nie wprowadzaj zmian, które wpływają na raporty PDF bez sprawdzenia wpływu na dane wynikowe.

## Fazy
Projekt pracuje na fazach:
- L1
- L2
- L3

Zasady:
- bilansowanie faz jest krytyczne
- agent nie może po cichu zmieniać logiki przypisywania obciążeń do faz
- każda zmiana algorytmu rozkładu obciążeń musi być opisana przed implementacją
- należy zachowywać przewidywalność wyników dla użytkownika
- zmiana algorytmu nie może przypadkowo pogorszyć asymetrii bez wyraźnej akceptacji celu

## Bilans obciążeń
Bilans obciążeń i bilans faz są obszarem krytycznym.

Agent ma:
- zachowywać dotychczasowe działanie, jeśli użytkownik nie prosi o zmianę logiki
- wskazywać wpływ na wyniki końcowe
- wskazywać, czy zmiana wpływa na testy
- nie zmieniać sposobu sumowania, grupowania i prezentacji danych bez uzasadnienia

## Obwody
Obwody są podstawową jednostką logiczną projektu.

Przy zmianach dotyczących obwodów:
- nie wolno po cichu zmieniać semantyki pól
- nie wolno zmieniać sposobu serializacji bez potrzeby
- nie wolno psuć zgodności w zapisie i odczycie projektu
- nie wolno zmieniać logiki używanej przez walidację, bilans i eksport bez prześledzenia zależności

## Walidacja
Walidacja ma wspierać poprawność projektu elektrycznego.

Agent nie może:
- po cichu usuwać ostrzeżeń lub błędów walidacji
- łagodzić reguł tylko po to, by „mniej przeszkadzały”
- zmieniać logiki walidacji bez opisu skutków

Przy zmianach w walidacji agent ma zawsze opisać:
1. jaka reguła działa obecnie
2. co jest w niej problemem
3. jaki będzie nowy efekt działania
4. jakie testy to obejmują

## Schemat i relacje
Schemat nie jest tylko warstwą wizualną.
Część danych schematu wpływa na logikę, połączenia, układ i eksport.

Dlatego:
- nie wolno traktować zmian w schemacie jako czysto wizualnych bez sprawdzenia zależności
- zmiany węzłów, połączeń i budowania relacji muszą być analizowane jak zmiany domenowe
- nie wolno psuć spójności danych między edytorem, walidacją i eksportem

## Eksport PDF
Eksport PDF jest częścią wyniku inżynierskiego.
Nie wolno po cichu:
- zmieniać danych wejściowych raportów
- usuwać sekcji
- zmieniać kolejności informacji, jeśli ma znaczenie techniczne
- rozjeżdżać zgodności między UI a dokumentem

## Undo / Redo a domena
Zmiany domenowe muszą zachować poprawne cofanie i ponawianie.
Jeśli zmiana wpływa na:
- dodawanie obwodów
- usuwanie obwodów
- zmianę przypisań
- modyfikację schematu
- zmianę parametrów wpływających na obliczenia

to trzeba sprawdzić, czy undo/redo nadal odtwarza stan poprawnie.

## Zapis i odczyt projektu
Dane projektu muszą być trwałe i spójne.

Nie wolno:
- zmieniać kontraktów danych bez potrzeby
- psuć zgodności wstecznej bez wyraźnej decyzji
- zmieniać nazewnictwa lub struktury danych bez analizy wpływu na zapis/odczyt

## Zasada jawności zmian
Jeśli zadanie dotyczy logiki domenowej, agent ma najpierw opisać:
- co działa obecnie
- gdzie leży ryzyko
- co dokładnie ma się zmienić
- dlaczego ta zmiana jest bezpieczna

Dopiero potem może proponować kod.

## Zasada minimalnej ingerencji
W logice domenowej preferuj:
- małe poprawki
- poprawę nazw
- wyodrębnienie metod
- testy
- zabezpieczenia warunków brzegowych

Unikaj:
- szerokiego przepisywania algorytmów
- zmian wielu reguł naraz
- „ulepszania” logiki bez jasnego celu biznesowego lub inżynierskiego