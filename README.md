# SGSClient

Cross-platform game client skeleton (.NET 10).

## Layout

```
src/Apps/SGSClient.UI              Avalonia — primary desktop (Windows / Linux / macOS)
src/Apps/SGSClient.UI.WinUI        WinUI 3 — optional Microsoft Store (MSIX)
src/Apps/SGSClient.Api             ASP.NET Core gateway API
src/Apps/SGSClient.Server          Worker / backend service
src/Core/SGSClient.Core            Shared DTOs, models, interfaces
src/Infrastructure/...Client       Client infra (SQLite library, process launch)
src/Infrastructure/...Server       Server infra (SQL Server / EF Core)
tests/                             xUnit smoke tests
```

`SGSClient.Core` has no project references into the rest of the solution (DIP).

## Data

| Store | Engine | Purpose |
|-------|--------|---------|
| Server | SQL Server + EF Core | Catalog (`games` / `game_builds`), `users` |
| Client | SQLite + EF Core | Local install registry only |

Client DB file (default): `%LocalAppData%/SGSClient/library.db`

### Accounts (Azure AD B2C)

Same model as the old SGSClient desktop app (MSAL public client + B2C), but:

- Desktop acquires tokens via MSAL (cache: `%LocalAppData%/SGSClient/msal_cache.bin3`)
- API validates JWT and upserts `users` on `GET /api/me`
- Client does **not** talk to SQL for identity

Config (Api + UI `appsettings.json` / `appsettings.example.json`):

```json
"AzureAd": {
  "Enabled": true,
  "ClientId": "<B2C app registration client id>",
  "TenantName": "<tenant>",
  "SigninPolicy": "B2C_1_SGSClient_SignIn",
  "PasswordResetPolicy": "B2C_1_SGSClient_PassRESET",
  "Scopes": [ "openid", "offline_access" ]
}
```

UI also needs:

```json
"Api": { "BaseUrl": "https://localhost:7142/" }
```

B2C app registration: public client / native, redirect `http://localhost`. Set `AzureAd:Enabled` to `true` on both Api and UI.

### SQL Server

EF provider: **Microsoft.EntityFrameworkCore.SqlServer**.

Connection string example:

```
Data Source=db.m455yn.dev;Initial Catalog=<db>;User ID=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=True
```

On Api startup: migrate + seed demo games (`GET /api/games`).

## Game package manifest (`sgs-manifest.json`)

Every uploaded build ZIP must contain **`sgs-manifest.json` at the archive root**.

| Artifact | Path |
|----------|------|
| JSON Schema | `schemas/sgs-manifest.v1.json` |
| Example | `schemas/examples/demo-runner.sgs-manifest.json` |
| C# model + rules | `src/Core/SGSClient.Core/Packaging/` |

**ZIP layout**

```text
game.zip
  sgs-manifest.json
  bin/DemoRunner.exe
  data/...
```

**Server limits (v1):** max ZIP 2 GB, max uncompressed 4 GB, max 20k entries, depth ≤ 12, no `..` / absolute paths, executable must match platform (e.g. `.exe` on Windows).

Client may preflight with `SgsPackageRules.ValidateManifest`; server re-validates before accepting storage.

### Publish from SGSClient

Nav item **Publikuj**: pick game folder (with `sgs-manifest.json`) → prepare ZIP → upload to `POST /api/Games/uploads`.

### Docker (Api + game files)

```bash
docker compose up -d --build
```

- Volume `sgs_games` → `/data/sgs/games`
- Set `ConnectionStrings__SgsDatabase` in `docker-compose.yml`
- With B2C enabled, login is required before upload

## Prerequisites

- .NET 10 SDK
- SQL Server (for Api)
- Windows App SDK / VS workload for `SGSClient.UI.WinUI` (Windows only)

## Build

```bash
dotnet build SGSClient.slnx
dotnet test SGSClient.slnx
```

Run (Visual Studio): open `SGSClient.slnx`, then in the toolbar startup dropdown choose a profile from `SGSClient.slnLaunch`:

- **Api + Avalonia UI**
- **Api + WinUI**
- **Api only**

Or Solution → **Configure Startup Projects…** → Multiple startup projects.

CLI:

```bash
dotnet run --project src/Apps/SGSClient.Api
dotnet run --project src/Apps/SGSClient.UI
```

## Publish — Avalonia (standalone)

```bash
dotnet publish src/Apps/SGSClient.UI -c Release -r win-x64 --self-contained true
dotnet publish src/Apps/SGSClient.UI -c Release -r linux-x64 --self-contained true
dotnet publish src/Apps/SGSClient.UI -c Release -r osx-arm64 --self-contained true
```

## Publish — WinUI (Microsoft Store / MSIX)

Windows only. Package from Visual Studio (**Project → Package and Publish**) or:

```bash
dotnet publish src/Apps/SGSClient.UI.WinUI -c Release -r win-x64 -p:PublishProfile=msix-x64 -p:Platform=x64
```
