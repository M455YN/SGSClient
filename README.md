# SGSClient

Klient sklepu i biblioteki gier oraz API katalogu, oparte na .NET 10. Dwie powłoki desktopowe (Avalonia, WinUI 3) współdzielą ViewModele w `SGSClient.UI.Shared`. Interfejs jest po polsku.

```
desktop  →  HTTPS  →  SGSClient.Api  →  SQL Server (katalog, użytkownicy)
                ↘  SQLite (%LocalAppData%/SGSClient/library.db)
```

`SGSClient.Core` **nie** ma referencji projektowych do Apps ani Infrastructure (DIP). Wspólny UI jest w `net10.0` i nie może odwoływać się do Avalonia ani WinUI.

## Solucja

| Ścieżka | Rola |
|---------|------|
| `src/Apps/SGSClient.UI` | Avalonia (`net10.0`): Windows, Linux, macOS |
| `src/Apps/SGSClient.UI.WinUI` | WinUI 3 (`net10.0-windows10.0.22621.0`), MSIX |
| `src/Apps/SGSClient.UI.Shared` | Wspólne ViewModele; multimedia tylko przez URL |
| `src/Apps/SGSClient.Api` | ASP.NET Core: JWT, katalog, wysyłki, administracja |
| `src/Apps/SGSClient.Server` | Worker i backend |
| `src/Core/SGSClient.Core` | DTO, helpery instalacji, `sgs-manifest` i `SgsPackageRules` |
| `src/Infrastructure/...Client` | SQLite, MSAL, uruchamianie, pakowanie i wysyłka |
| `src/Infrastructure/...Server` | SQL Server EF |
| `schemas/` | Schemat JSON manifestu i przykład |
| `tests/` | xUnit |

Punkt wejścia: `SGSClient.slnx`.

Widoki i wybieracze plików zostają w każdej powłoce osobno. Wspólne ViewModele są w `SGSClient.UI.ViewModels`. Okładki to rozwiązane adresy URL (`InstallPathHelper.TryResolveDownloadUrl`).

## Kontrakt paczki

ZIP do wysyłki **musi** zawierać `sgs-manifest.json` w korzeniu archiwum. Schemat: [`schemas/sgs-manifest.v1.json`](schemas/sgs-manifest.v1.json). Przykład: [`schemas/examples/demo-runner.sgs-manifest.json`](schemas/examples/demo-runner.sgs-manifest.json). Reguły: `src/Core/SGSClient.Core/Packaging/`. Sprawdzenie po stronie klienta: `SgsPackageRules.ValidateManifest`. Wysyłka: `POST /api/Games/uploads`.

**Limity v1:** ZIP ≤ 2 GB, rozpakowany ≤ 4 GB, ≤ 20 tys. wpisów, głębokość ≤ 12, bez `..` i ścieżek bezwzględnych, plik wykonywalny zgodny z platformą.

Dwa zapisywane pliki grafik (nie łączyć ich na serwerze):

| Slot | Manifest / DTO | Użycie |
|------|----------------|--------|
| Miniaturka | `media.cover` → `CoverUrl` | listy, plakaty w bibliotece |
| Okładka pozioma | `media.header` → `HeaderUrl` | strona tytułowa, hero |
| Logo kwadratowe | `media.logo` → `LogoUrl` | kafelki w pasku bocznym |

`ThumbnailUrl`, `LandscapeUrl` i `SquareLogoUrl` to zapasowe pola klienta.

Puste bazy zasiewają **Demo Runner** i **Sky Arena** prawdziwymi paczkami ZIP w `Games:StoragePath`. Dzięki temu cykl „odbierz → zainstaluj → graj” działa bez wcześniejszej publikacji.

## Dane

| Magazyn | Silnik | Dane |
|---------|--------|------|
| Serwer | SQL Server + EF Core | `games`, `game_builds`, `users` |
| Klient | SQLite + EF Core | rejestr instalacji, czas gry |

Baza klienta: `%LocalAppData%/SGSClient/library.db`. API migruje i zasiewa dane przy starcie (`GET /api/games`).

Uwierzytelnianie: publiczny klient MSAL + Azure AD B2C. API waliduje JWT i zapisuje `users` przy `GET /api/me`. Aplikacja desktopowa nie pyta SQL o tożsamość. Przykładowa konfiguracja: `appsettings.example.json` (`AzureAd`, `Api:BaseUrl`), w `src/Apps/SGSClient.Api/`.

## Budowanie

Wymagany jest [.NET 10 SDK](https://dotnet.microsoft.com/download). API potrzebuje SQL Server, a WinUI potrzebuje Windows App SDK.

```bash
dotnet build SGSClient.slnx
dotnet test SGSClient.slnx
dotnet run --project src/Apps/SGSClient.Api
dotnet run --project src/Apps/SGSClient.UI
```

Lokalne API: profil uruchomienia **Local API** (`SGS_Api__BaseUrl=https://localhost:7142/`). Profil produkcyjny używa `Api:BaseUrl` z konfiguracji. Uruchamianie wielu projektów: `SGSClient.slnLaunch`.

```bash
dotnet publish src/Apps/SGSClient.UI -c Release -r win-x64 --self-contained true
dotnet publish src/Apps/SGSClient.UI.WinUI -c Release -r win-x64 -p:PublishProfile=msix-x64 -p:Platform=x64
```

Obraz API i pliki compose: `deploy/`.

### Microsoft Store

Do Store idzie klient WinUI. Paczkę do Partner Center buduje jedna komenda (wymaga Visual Studio z MSBuild i Windows App SDK):

```powershell
.\deploy\build-msixbundle.ps1                                # x64, arm64
.\deploy\build-msixbundle.ps1 -Platforms "x64|arm64|x86"
```

Wynik trafia do `artifacts/msix/`: jeden plik `*.msixbundle`, który wgrywasz w Partner Center (**Pakiety**). Store podpisuje paczkę sam, więc skrypt niczego nie podpisuje. Skrypt czyści wyjścia `Release` projektu WinUI (stare manifesty z VS psują bundla). Wyjść `Debug` nie rusza, więc działający klient może zostać otwarty.

Paczka Store (warunek `UapAppxPackageBuildMode=StoreUpload` w `.csproj`) zawiera .NET, bo Store go nie doinstaluje, oraz natywne biblioteki tylko dla swojej platformy. Windows App SDK jest zależnością frameworkową (`Microsoft.WindowsAppRuntime.1.8`), którą Store instaluje sam. Windows ML (`onnxruntime`, `DirectML`) jest wyłączony z paczki, bo klient go nie używa. Buildy Debug i paczka zip bez MSIX zostają bez zmian.

WACK (Windows App Certification Kit) wymaga konsoli jako administrator i podpisanej paczki, którą da się zainstalować. Najprościej uruchomić go z Visual Studio: **Spakuj i opublikuj → Utwórz pakiety aplikacji → Microsoft Store** i na końcu kreatora **Uruchom zestaw certyfikacji**. Partner Center i tak przeprowadza te same testy przy zgłoszeniu.

Przed wysyłką:

- **Tożsamość** (`Identity Name`, `Publisher`, `PublisherDisplayName` w `src/Apps/SGSClient.UI.WinUI/Package.appxmanifest`) musi zgadzać się z Partner Center, **Zarządzanie produktem → Tożsamość produktu**.
- **Wersja** w `Package.appxmanifest` ma format `x.y.z.0` i każde zgłoszenie musi ją podnieść.
- **`appsettings.local.json`** w projekcie WinUI musi istnieć: z niego paczka bierze ustawienia logowania B2C. Skrypt przerywa budowanie, jeśli go brakuje.
- **`runFullTrust`**: przy zgłoszeniu Partner Center pyta o uzasadnienie. Przykład: „SGSClient to launcher gier w Windows App SDK (aplikacja desktopowa). Pełne zaufanie jest potrzebne, żeby instalować gry w folderach wybranych przez użytkownika i uruchamiać je jako osobne procesy.”

## Dokumentacja

Wiki dla graczy, twórców i deweloperów gier: [`docs/README.md`](docs/README.md).
