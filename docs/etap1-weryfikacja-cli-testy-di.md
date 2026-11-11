# Weryfikacja Etapu 1 — CLI, testy jednostkowe, Dependency Injection

Data: 2026-11-11 · Build: **0 błędów / 0 ostrzeżeń** · Testy: **60 z 60 zielonych** (było 54)

## 1. CLI — sprawdzone uruchomieniem (nie tylko kompilacją)

| Scenariusz | Wynik |
|---|---|
| `1` — ładowanie danych | `Metadata loaded: SampleLibrary 1.0.0.0`, 3 przestrzenie, 7 klas |
| `2` — drzewo | `Assembly: SampleLibrary`, wcięcia `\|   +--`, namespace → klasa → składowa |
| `3` — podsumowanie | Liczniki: Namespaces 3, Classes 7, Members 28, Loaded at; lista klas z liczbą składowych |
| `4` — szczegóły składowej | Lista 7 klas z numeracją, wybór klasy i składowej |
| `0` — wyjście | `Goodbye.`, kod wyjścia 0 |
| Błędna opcja (`9`) | `Invalid number. Enter a value between 1 and 7.` — bez wyjątku |
| Brak danych (2/3/4 przed `1`) | `No metadata loaded. Select option 1 first.` |
| Koniec strumienia wejścia (EOF) | Czyste wyjście, bez wyjątku |
| Zapis do logu | `Logs/application.log`: `Application started`, `Metadata analysis started`, `Sample data loaded: SampleLibrary (SampleLibrary.dll)`, `Application closed` |

Zgodność z założeniami: ten sam `IMetadataService` co WPF (rozwiązywany z DI), brak własnej logiki analizy,
prezentacja oddzielona do `ConsolePresentation`, `MetadataFormatter` renderuje drzewo i szczegóły.

## 2. Testy jednostkowe — 60 testów, wszystkie zielone

| Plik | Testy | Zakres |
|---|---|---|
| `Models/ModelTests.cs` | 7 | liczniki, sygnatury, puste kolekcje, równość wartościowa |
| `Services/MockMetadataProviderTests.cs` | 6 | nazwa/plik, struktura 3 ns / 7 typów, wymagane klasy, znacznik czasu z `ISystemClock`, świeże instancje, anulowanie |
| `Services/MetadataServiceTests.cs` | 7 | sukces, log start/koniec, błąd + rethrow, anulowanie, przekazanie tokenu, `RefreshAsync`, `Current` |
| `Services/MetadataFormatterTests.cs` | 11 | drzewo, wcięcia, puste dane, null, liczniki, szczegóły składowych, nieznana składowa, kultura niezmienna |
| `Logging/FileLoggerTests.cs` | 8 | zapis, znacznik czasu, dopisywanie, tworzenie katalogu, wyłączenie, brak wyjątku przy awarii, kolejność, ścieżka względna |
| `ViewModels/MainViewModelTests.cs` | 9 | stan początkowy, ładowanie, komunikat ze czasem, zaznaczanie bez przeładowania, składowe, czyszczenie, błąd + log, Refresh, blokada przy `IsBusy` |
| `ViewModels/TreeNodeViewModelTests.cs` | 6 | hierarchia, parametry metod, szczegóły klasy/parametru/zestawu, powiadomienia |
| `DependencyInjectionTests.cs` (nowe) | 6 | walidacja kontenera, singletony, `SettingsAwareLogger` + sink, zapis do pliku i sinka, resolver, odrzucenie trybu refleksji |

## 3. Dependency Injection — sprawdzone testami

- `AddMetadataAnalyzerCore`: `ISystemClock`, `IMetadataFormatter`, `MockMetadataProvider`, `MetadataProviderOptions`,
  `IMetadataProvider` → `MetadataProviderResolver`, `IMetadataService` → `MetadataService` — wszystkie singletony.
- `AddMetadataAnalyzerLogging`: `LoggingOptions`, `IAppLogger` → `FileLogger`, `FileLogger`.
- `AddMetadataAnalyzer(useInMemoryLog: true)`: dodatkowo `InMemoryLogSink` + `SettingsAwareLogger` jako `IAppLogger`.
- `ValidateOnBuild` + `ValidateScopes` — kontener buduje się bez błędów (test `ContainerValidatesOnBuild`).

## Uwagi (nie blokujące, do decyzji)

1. **Podwójna rejestracja `IAppLogger`** — `GetServices<IAppLogger>()` zwraca 2 wpisy (`FileLogger` i
   `SettingsAwareLogger`). Działa, bo `GetRequiredService` bierze ostatni, ale lepiej rejestrować `FileLogger`
   tylko jako typ konkretny (albo użyć `TryAddSingleton`).
2. **Brak testów `MetadataProviderResolver` w projekcie produkcyjnym** — dopisane dopiero teraz w testach DI.
3. **Testy CLI** — brak testów `MetadataConsoleApp`; CLI sprawdzone ręcznie (sekcja 1). Warto dodać test
   z wstrzykniętym `StringReader`/`StringWriter` — klasa już przyjmuje `TextReader`/`TextWriter`.
4. **`Application:UseMockData = false`** rzuca `NotSupportedException` — zgodne z dokumentacją (etap 2).
