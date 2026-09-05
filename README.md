# SGSClient

.NET 10 game store / library client and catalog API. Two desktop shells (Avalonia, WinUI 3) share ViewModels in `SGSClient.UI.Shared`. UI copy is Polish.

```
desktop  →  HTTPS  →  SGSClient.Api  →  SQL Server (catalog, users)
                ↘  SQLite (%LocalAppData%/SGSClient/library.db)
```

`SGSClient.Core` has **no** project references into Apps or Infrastructure (DIP). Shared UI is `net10.0` and must not reference Avalonia or WinUI.

## Solution

| Path | Role |
|------|------|
| `src/Apps/SGSClient.UI` | Avalonia (`net10.0`) — Windows / Linux / macOS |
| `src/Apps/SGSClient.UI.WinUI` | WinUI 3 (`net10.0-windows10.0.22621.0`), MSIX |
| `src/Apps/SGSClient.UI.Shared` | Shared ViewModels; URL-only media |
| `src/Apps/SGSClient.Api` | ASP.NET Core — JWT, catalog, uploads, admin |
| `src/Apps/SGSClient.Server` | Worker / backend |
| `src/Core/SGSClient.Core` | DTOs, install helpers, `sgs-manifest` + `SgsPackageRules` |
| `src/Infrastructure/...Client` | SQLite, MSAL, launch, pack/upload |
| `src/Infrastructure/...Server` | SQL Server EF |
| `schemas/` | Manifest JSON Schema + example |
| `tests/` | xUnit |

Entry: `SGSClient.slnx`.

Views and file pickers stay per shell. Shared VMs live in `SGSClient.UI.ViewModels`. Covers are resolved URLs (`InstallPathHelper.TryResolveDownloadUrl`).

## Package contract

Upload ZIP **must** contain `sgs-manifest.json` at the archive root. Schema: [`schemas/sgs-manifest.v1.json`](schemas/sgs-manifest.v1.json). Example: [`schemas/examples/demo-runner.sgs-manifest.json`](schemas/examples/demo-runner.sgs-manifest.json). Rules: `src/Core/SGSClient.Core/Packaging/`. Client preflight: `SgsPackageRules.ValidateManifest`. Upload: `POST /api/Games/uploads`.

**v1 limits:** ZIP ≤ 2 GB, uncompressed ≤ 4 GB, ≤ 20k entries, depth ≤ 12, no `..` / absolute paths, executable matches platform.

Two stored art files (do not merge on the server):

| Slot | Manifest / DTO | Used for |
|------|----------------|----------|
| Thumbnail | `media.cover` → `CoverUrl` | lists, library posters |
| Landscape | `media.header` → `HeaderUrl` | title page, hero |
| Square logo | `media.logo` → `LogoUrl` | sidebar tiles |

`ThumbnailUrl` / `LandscapeUrl` / `SquareLogoUrl` are client fallbacks.

Empty databases seed **Demo Runner** and **Sky Arena** with real ZIP packages under `Games:StoragePath` (claim → install → play works without publishing first).

## Data

| Store | Engine | Data |
|-------|--------|------|
| Server | SQL Server + EF Core | `games`, `game_builds`, `users` |
| Client | SQLite + EF Core | install registry, playtime |

Client DB: `%LocalAppData%/SGSClient/library.db`. API migrates and seeds on startup (`GET /api/games`).

Auth: MSAL public client + Azure AD B2C. API validates JWT and upserts `users` on `GET /api/me`. Desktop does not query SQL for identity. See `appsettings.example.json` (`AzureAd`, `Api:BaseUrl`).

## Build

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download). SQL Server for Api. Windows App SDK for WinUI.

```bash
dotnet build SGSClient.slnx
dotnet test SGSClient.slnx
dotnet run --project src/Apps/SGSClient.Api
dotnet run --project src/Apps/SGSClient.UI
```

Local API: launch profile **Local API** (`SGS_Api__BaseUrl=https://localhost:7142/`). Production profile uses `Api:BaseUrl` from config. Multi-startup: `SGSClient.slnLaunch`.

```bash
dotnet publish src/Apps/SGSClient.UI -c Release -r win-x64 --self-contained true
dotnet publish src/Apps/SGSClient.UI.WinUI -c Release -r win-x64 -p:PublishProfile=msix-x64 -p:Platform=x64
```

API image and compose files: `deploy/`.
