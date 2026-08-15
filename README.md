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
"Api": { "BaseUrl": "https://api.m455yn.dev/" }
```

The desktop client defaults to `https://api.m455yn.dev/`. Visual Studio profile **Local API** sets `SGS_Api__BaseUrl=https://localhost:7142/` (overrides `appsettings.local.json`). Do not put `localhost:7142` in `appsettings.local.json` unless you always want local API.

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

### Docker / Portainer (OVH + Nginx Proxy Manager)

Existing NPM (`npm` on external networks `frontend` / `backend`) proxies TLS.
`sgs-api` joins **`frontend`** only — NPM forwards to `http://sgs-api:8080`. Do not publish API ports on the host.

```bash
cp deploy/portainer.env.example .env   # SGS_SQL_CONNECTION (SQL Server, not NPM MariaDB)
docker compose up -d --build
```

Then in NPM UI (port 81) add a Proxy Host — copy-paste Advanced nginx from `deploy/nginx-proxy-manager.txt` (`client_max_body_size 2048m`).

Portainer stack file to paste: **`deploy/portainer-stack.yml`**. Image: `ghcr.io/m455yn/sgsclient-api:latest` (GitHub Actions on push to `master`, or `.\deploy\build-api-image.ps1 -Push`). If the package is private, add a GitHub registry in Portainer (`read:packages` PAT) and recreate with pull.

| Piece | Setting |
|-------|---------|
| Network | external `frontend` (already used by `npm`) |
| Packages | volume or bind `SGS_GAMES_VOLUME=/opt/sgs/games` |
| SQL | `SGS_SQL_CONNECTION` — SGS catalog (SQL Server, not `npm-db`) |
| ROFW | `SGS_ROFW_SQL_CONNECTION` — existing `SZYRROGI_AUTOBATTLER` catalog so `/api/rofw/*` keeps working |
| Reportingo | `SGS_REPORTINGO_SQL_CONNECTION` — existing `REPORTINGO` catalog so `/api/reportingo/report-bug` keeps working |
| Desktop client | default `Api:BaseUrl` = `https://api.m455yn.dev/` (override via VS profile Local or `appsettings.local.json`) |

Leave `SGS_AZUREAD_ENABLED=false` for the first upload test. Cloudflare orange-cloud caps large ZIPs — DNS-only for the API host.

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

- **WinUI · Production API** / **Avalonia · Production API** — client → `https://api.m455yn.dev/`
- **Api + WinUI · Local** / **Api + Avalonia · Local** — local Api + client → `https://localhost:7142/`
- **Api only**

Project launch profiles (dropdown next to Start when a single UI project is selected): **Unpackaged (Production API)** / **Unpackaged (Local API)** (WinUI), **Production API** / **Local API** (Avalonia).

Docker image for OVH (linux/amd64): set startup project `SGSClient.Api`, profile **Container (linux/amd64)** (needs Docker Desktop). To tag `sgsclient-api:latest` without debugging: right-click the API project → **Publish** → profile `docker-linux-amd64`, or `.\deploy\build-api-image.ps1`.

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
