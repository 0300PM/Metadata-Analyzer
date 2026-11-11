# Metadata Analyzer — Etap 1

Szkielet aplikacji do analizy metadanych bibliotek .NET z dwoma niezależnymi interfejsami
(WPF i CLI), korzystającymi z tej samej logiki i tych samych modeli danych.

- **.NET 10** (LTS), C#, WPF/XAML, wzorzec MVVM (CommunityToolkit.Mvvm)
- **Core** nie zna WPF, CLI ani implementacji logowania — zależy wyłącznie od abstrakcji
- Testy: xUnit + Moq — **54 testy, wszystkie pozytywne**
- Build: **0 błędów, 0 ostrzeżeń** (`TreatWarningsAsErrors` + analizatory .NET, bez `#pragma` i bez wyciszeń)

📄 Pełna dokumentacja techniczna: **[DOKUMENTACJA.md](DOKUMENTACJA.md)** (wersja PDF: `docs/MetadataAnalyzer-Etap1-Dokumentacja.pdf`)

## Struktura rozwiązania

```
MetadataAnalyzer/
├── MetadataAnalyzer.slnx              # solution (.NET 10, format slnx)
├── Directory.Build.props              # wspólne ustawienia kompilacji i analizatorów
├── README.md
├── DOKUMENTACJA.md
├── docs/
│   ├── MetadataAnalyzer-Etap1-Dokumentacja.pdf  # dokumentacja techniczna (PDF)
│   ├── wpf-preview.png                # zrzut działającej aplikacji WPF
│   ├── ui/                            # zrzuty ekranów aplikacji
│   └── cli-smoke.txt                  # zapis sesji CLI (smoke test)
│
├── MetadataAnalyzer.Core/             # modele, interfejsy, logika (bez UI)
│   ├── Models/                        # Assembly → Namespace → Class → members → Parameter
│   ├── Interfaces/                    # IMetadataProvider, IMetadataService, IMetadataFormatter, IAppLogger, ISystemClock
│   └── Services/                      # MockMetadataProvider, MetadataService, MetadataFormatter, DI
│
├── MetadataAnalyzer.Infrastructure/   # FileLogger, konfiguracja, kompozycja DI
│   ├── Configuration/ApplicationOptions.cs
│   ├── Logging/                       # FileLogger, LoggingOptions
│   └── AppConfiguration.cs
│
├── MetadataAnalyzer.WPF/              # Views, ViewModels, Converters, Themes
├── MetadataAnalyzer.CLI/              # menu tekstowe, prezentacja drzewa i szczegółów
└── MetadataAnalyzer.Tests/            # Models, Services, Logging, ViewModels
```

Zależności: `WPF` i `CLI` → `Core`; `Infrastructure` → `Core`; `Tests` → `Core` + `Infrastructure` + `WPF`.
`Core` **nie** odwołuje się do WPF, CLI ani Infrastructure.

## Uruchomienie

```bash
dotnet build MetadataAnalyzer.slnx
dotnet run --project MetadataAnalyzer.WPF
dotnet run --project MetadataAnalyzer.CLI
dotnet test  MetadataAnalyzer.Tests
```

Wymagany .NET SDK 10.0.x. Na tej maszynie SDK leży w `C:/Users/anysz/.dotnet` (nie ma go w `PATH`).

## Co działa (mapowanie wymagań etapu)

| Wymaganie | Realizacja |
|---|---|
| 1. Interfejs WPF | `Views/MainWindow.xaml` + `Views/DetailsView.xaml`, ciemny motyw w `Themes/` |
| 2. Interfejs CLI | `MetadataConsoleApp` — menu 1–4 / 0, obsługa błędnych opcji |
| 3. Dane symulowane | `MockMetadataProvider` → `SampleLibrary.dll`, 3 przestrzenie nazw, 4 klasy |
| 4. Drzewo w WPF | `TreeNodeViewModel` (Assembly → Namespace → Class → Field/Property/Constructor/Method → Parameter) |
| 5. Drzewo w konsoli | `MetadataFormatter.FormatTree` — wcięcia `|   +--` |
| 6. Szczegóły wybranego elementu | `DetailsViewModel` — wybór w drzewie aktualizuje panel bez przeładowania okna |
| 7. Logowanie do pliku | `FileLogger` → `Logs/application.log` (INFO/WARN/ERROR, append, odporne na błędy zapisu) |
| 8. Testy jednostkowe | 54 testy: modele, provider, serwis, formatter, logger, ViewModele |

## Konfiguracja (`appsettings.json`, kopiowany do katalogu wyjściowego)

```json
{
  "Application": { "Name": "Metadata Analyzer", "Version": "1.0.0", "UseMockData": true },
  "Logging": { "Enabled": true, "Provider": "File", "FilePath": "Logs/application.log" }
}
```

Kompozycja: `services.AddMetadataAnalyzer(configuration, logToConsole: false)` rejestruje
`IMetadataProvider`, `IMetadataService`, `IMetadataFormatter`, `IAppLogger` i `ISystemClock`.
Wymiana dostawcy danych (etap 2: `System.Reflection`) to jedna rejestracja + jedna klasa.

## Przykład logu

```
[2026-11-10 20:27:49] INFO  Application started
[2026-11-10 20:27:49] INFO  Metadata analysis started
[2026-11-10 20:27:49] INFO  Sample data loaded: SampleLibrary (SampleLibrary.dll)
[2026-11-10 20:27:49] WARN  Invalid selection: '7'
[2026-11-10 20:27:49] INFO  Application closed
```



