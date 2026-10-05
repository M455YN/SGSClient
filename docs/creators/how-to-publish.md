# How to publish a game / Jak opublikować grę

You need a **creator** role (or higher). In the client open **Creator panel** — that is where you ship the first version and later builds. The separate **Publish** nav item is hidden.

Potrzebujesz konta z rolą **twórca** (lub wyższą). W kliencie wejdź w **Panel twórcy** — stamtąd publikujesz pierwszą wersję i kolejne buildy. Osobna pozycja „Publikuj” w menu jest ukryta.

## 1. Game folder / Przygotuj folder gry

Put the game files and the manifest in the **folder root** (the same layout is zipped):

Na dysku złóż katalog z plikami gry i manifestem w **korzeniu** folderu (ten sam układ trafia do ZIP):

```text
my-game/
  sgs-manifest.json
  bin/MyGame.exe
  data/...
  media/cover.png
  media/header.png
```

Media filenames can differ as long as manifest paths match. Paths must be relative, use `/`, no `..`.

Nazwy plików mediów mogą być inne, byle ścieżki w manifeście się zgadzały. Ścieżki tylko względne, ze slashem `/`, bez `..`.

## 2. Fill in `sgs-manifest.json` / Wypełnij manifest

Required: `schemaVersion` (currently `1`), `game.title`, `game.symbol`, `build.version`, `build.platform`, `build.executable`.

Wymagane: `schemaVersion` (obecnie `1`), `game.title`, `game.symbol`, `build.version`, `build.platform`, `build.executable`.

```json
{
  "schemaVersion": 1,
  "game": {
    "title": "My Game",
    "symbol": "my-game",
    "kind": "game",
    "description": "Short store-page description.",
    "genre": "indie",
    "engine": "unity",
    "studioName": "My Studio",
    "websiteUrl": "https://example.com",
    "discordUrl": "https://discord.gg/example",
    "supportUrl": "https://example.com/support",
    "releaseStatus": "released",
    "releaseDate": "2024-06-15",
    "ageRating": "pegi12",
    "features": ["singleplayer", "controller"],
    "languages": ["Polski", "English"],
    "tags": ["roguelike", "pixel art"],
    "media": {
      "cover": "media/cover.png",
      "header": "media/header.png",
      "logo": "media/logo.png",
      "intro": "media/intro.mp4",
      "screenshots": [
        "media/screen-01.png",
        "media/screen-02.png"
      ]
    }
  },
  "build": {
    "version": "1.0.0",
    "platform": "windows",
    "architecture": "x64",
    "executable": "bin/MyGame.exe",
    "isDefault": true
  }
}
```

| Field / Pole | Rule / Zasada |
|--------------|----------------|
| `symbol` | Stable kebab-case slug: `a-z`, `0-9`, hyphens; 2–64 chars. Do not change after first publish. / Stały slug; nie zmieniaj po pierwszej publikacji. |
| `kind` | `game` or `dlc`. DLC needs `parentSymbol` of the base game. / `game` albo `dlc`. DLC wymaga `parentSymbol`. |
| `version` | SemVer, e.g. `1.0.0` or `1.0.0-beta.1` |
| `platform` | `windows`, `linux`, `macos` |
| `architecture` | `x64`, `x86`, `arm64` |
| `executable` | Entry file inside the ZIP; on Windows usually `.exe` / Plik startowy w ZIP; na Windowsie zwykle `.exe` |
| `genre` | `action`, `adventure`, `rpg`, `strategy`, `simulation`, `sports`, `racing`, `puzzle`, `horror`, `indie`, `platformer`, `shooter`, `fps`, `tps`, `fighting`, `stealth`, `survival`, `sandbox`, `open-world`, `roguelike`, `roguelite`, `metroidvania`, `visual-novel`, `card`, `turn-based`, `mmo`, `moba`, `battle-royale`, `tower-defense`, `idle`, `casual`, `party`, `rhythm`, `educational`, `narrative`, `walking-sim`, `vr`, `other` |
| `engine` | `unity`, `unreal`, `godot`, `cryengine`, `gamemaker`, `construct`, `rpgmaker`, `renpy`, `defold`, `monogame`, `fna`, `stride`, `bevy`, `libgdx`, `love2d`, `phaser`, `cocos`, `solar2d`, `clickteam-fusion`, `source`, `source2`, `idtech`, `frostbite`, `creation-engine`, `red-engine`, `anvil`, `snowdrop`, `o3de`, `raylib`, `pygame`, `sdl`, `html5`, `roblox`, `custom`, `other` |
| `studioName` / `websiteUrl` / `discordUrl` / `supportUrl` | Optional store links (URLs must be `http://` or `https://`) |
| `releaseStatus` | `announced`, `earlyAccess`, `released`, `demo` |
| `releaseDate` | ISO date, e.g. `2024-06-15` |
| `ageRating` | `unrated`, `pegi3`, `pegi7`, `pegi12`, `pegi16`, `pegi18` |
| `features` | `singleplayer`, `multiplayer`, `coop`, `controller`, `cloud`, `achievements`, `modding`, `vr` |
| `languages` / `tags` | Arrays of strings (same as Creator panel CSV fields) |

Media are optional at upload — you can add them later in Creator panel → **Media**. Sizes: [Store art](store-art.md).

Media są opcjonalne przy uploadzie — możesz je potem dodać w Panelu twórcy (zakładka **Multimedia**). Wymiary: [Grafika sklepowa](store-art.md).

Full sample manifest: [`schemas/examples/demo-runner.sgs-manifest.json`](../../schemas/examples/demo-runner.sgs-manifest.json).

Ready-made folder + ZIP template (placeholder exe + labeled store art): [`schemas/examples/sample-game/`](../../schemas/examples/sample-game/) / [`sample-game-package.zip`](../../schemas/examples/sample-game-package.zip).

Gotowy folder + ZIP szablon (atrapa exe + opisane grafiki): [`schemas/examples/sample-game/`](../../schemas/examples/sample-game/) / [`sample-game-package.zip`](../../schemas/examples/sample-game-package.zip).

## 3. Upload from the client / Wyślij z klienta

1. Sign in under **Profile**. / Zaloguj się w **Profilu**.
2. Open **Creator panel** → publish a new game / new version. / Otwórz **Panel twórcy** → nowa gra / nowa wersja.
3. Pick the folder that contains `sgs-manifest.json`. / Wskaż folder z manifestem.
4. Check title, symbol, media, and exe path. / Sprawdź tytuł, symbol, media i ścieżkę exe.
5. Pack and upload. / Przygotuj paczkę i wyślij.

The client builds a ZIP and posts to `POST /api/Games/uploads`. The server validates the manifest again.

Klient pakuje ZIP i wysyła na `POST /api/Games/uploads`. Serwer jeszcze raz sprawdza manifest.

A new game starts as a **draft** (hidden from the store). In Creator panel overview turn on **In store** when the page and art are ready.

Nowa gra startuje jako **szkic** (niewidoczna w sklepie). W przeglądzie Panelu twórcy włącz **W sklepie**, gdy strona i grafika są gotowe.

## Package limits (v1) / Limity paczki (v1)

| Limit | Value / Wartość |
|-------|-----------------|
| ZIP | 2 GB |
| Uncompressed / Rozpakowane | 4 GB |
| Archive entries / Pliki | 20 000 |
| Path depth / Głębokość | 12 |
| Single media file / Jeden plik media | 80 MB |
| Screenshots / Zrzuty | 12 |

The server rejects absolute paths, `..`, and an exe that does not match the platform.

Serwer odrzuca ścieżki absolutne, `..` oraz exe niezgodne z platformą.
