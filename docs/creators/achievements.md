# Achievements / Osiągnięcia

SGS achievements are plain HTTP. Any engine that can send a `POST` request can unlock them — no SDK, no native plugin.

Osiągnięcia w SGS to zwykłe HTTP. Każdy silnik, który potrafi wysłać `POST`, może je odblokowywać — bez SDK i bez natywnych wtyczek.

## 1. Define them / Zdefiniuj osiągnięcia

Two ways, use whichever fits your workflow / Dwa sposoby, wybierz wygodniejszy:

- **Creator panel** (Admins: Admin panel, any game): pick the game → **Achievements** tab → **Add achievement** → **Save achievements**. / **Panel twórcy** (admini: Panel admina, dowolna gra): wybierz grę → zakładka **Osiągnięcia** → **Dodaj osiągnięcie** → **Zapisz osiągnięcia**.
- **Package manifest** (`achievements` array in `sgs-manifest.json`): published together with the build. / **Manifest paczki** (tablica `achievements` w `sgs-manifest.json`): publikowane razem z buildem.

| Field / Pole | Meaning / Znaczenie |
|---|---|
| `key` | Permanent id used by your game code, e.g. `first_boss`. Lowercase letters, digits, `_` and `-`, up to 64 characters. **Never rename it** — a new key is a new achievement. / Stały identyfikator w kodzie gry. Małe litery, cyfry, `_` i `-`, do 64 znaków. **Nie zmieniaj go** — nowy klucz to nowe osiągnięcie. |
| `title`, `description` | Shown to players (128 / 512 characters). |
| `translations` | `{ "en": { "title": "…", "description": "…" } }`, up to 10 language codes. The panel edits English; other codes are kept. Players on that language see the translation, everyone else the base text. / Do 10 kodów języków. Panel edytuje angielski, pozostałe kody są zachowywane. |
| `icon` (manifest) / Icon (panel) | An `http(s)` URL, an image path inside the ZIP (manifest), or an uploaded file (panel: **Upload icon**). png, jpg, webp, gif. / URL `http(s)`, ścieżka obrazu w ZIP (manifest) albo wgrany plik (panel: **Wgraj ikonę**). |
| `points` | 0–1000, cosmetic. |
| `target` | Absent = one-shot unlock. A number (2–1 000 000) = counter that unlocks when progress reaches it. / Brak = jednorazowe. Liczba = licznik, który odblokowuje się po osiągnięciu wartości. |
| `hidden` | Title, description and icon stay empty for players until unlocked. / Tytuł, opis i ikona są puste dla graczy do czasu odblokowania. |

Up to 500 achievements per game. Removing one only deactivates it: players keep what they earned, and adding the same key back restores it.

Do 500 osiągnięć na grę. Usunięcie tylko je dezaktywuje: gracze zachowują zdobyte, a dodanie tego samego klucza przywraca osiągnięcie.

### Manifest

```json
"achievements": [
  { "key": "first_run", "title": "Pierwszy bieg", "description": "Ukończ pierwszy poziom.",
    "icon": "media/first_run.png", "points": 10,
    "translations": { "en": { "title": "First run", "description": "Finish the first level." } } },
  { "key": "marathon", "title": "Maratończyk", "points": 50, "target": 100 }
]
```

Absent `achievements` leaves the game's achievements untouched. A present array — even an empty one — **replaces** them on publish, so the manifest wins over the panel. See [`schemas/examples/demo-runner.sgs-manifest.json`](../../schemas/examples/demo-runner.sgs-manifest.json).

Brak `achievements` zostawia osiągnięcia gry bez zmian. Obecna tablica — nawet pusta — **zastępuje** je przy publikacji, więc manifest wygrywa z panelem.

The store's **Achievements** feature flag follows the list automatically. **Test unlock** unlocks an achievement on your own account; **Reset my progress** clears your own progress for the game.

Flaga funkcji **Osiągnięcia** w sklepie sama podąża za listą. **Testowo odblokuj** odblokowuje osiągnięcie na Twoim koncie; **Wyzeruj mój postęp** czyści Twój postęp w tej grze.

Players see the list on the game page (**Achievements** tab) and a per-game summary on their profile (visible to others unless the player hides it).

Gracze widzą listę na stronie gry (zakładka **Osiągnięcia**) i podsumowanie per gra w profilu.

## 2. What your game receives / Co dostaje gra

When the player starts the game from the SGS client, the process gets three environment variables:

Gdy gracz uruchamia grę z klienta SGS, proces dostaje trzy zmienne środowiskowe:

| Variable | Example | Meaning |
|---|---|---|
| `SGS_API_URL` | `http://127.0.0.1:51234` | Address of the **local SGS bridge** (loopback only) / adres **lokalnego mostu SGS** (tylko pętla zwrotna) |
| `SGS_GAME_ID` | `12` | Id of your game / id Twojej gry |
| `SGS_ACHIEVEMENTS_TOKEN` | `3f9a…` | Random per-launch token that only the bridge accepts / losowy token na uruchomienie, akceptowany tylko przez most |

The bridge signs your calls with the player's own sign-in, so there is no token to expire during a long session, and it **queues calls while the player is offline** and replays them later (it survives a client restart). If the variables are missing (game started outside SGS) just skip achievements — the game must keep working.

Most podpisuje wywołania logowaniem samego gracza, więc token nie wygasa w długiej sesji, a gdy gracz jest **offline, kolejkuje wywołania** i odtwarza je później (przeżywa restart klienta). Jeśli zmiennych nie ma (gra uruchomiona poza SGS), po prostu pomiń osiągnięcia — gra ma dalej działać.

Send the token in the `X-SGS-Token` header.

## 3. Endpoints / Endpointy

All calls are relative to `SGS_API_URL`. The bridge answers CORS pre-flights, so HTML5 / WebGL builds work too.

| Call | Purpose / Cel |
|---|---|
| `POST /api/sdk/achievements/{key}/unlock` | Unlock a one-shot achievement (or fill a counter). Safe to repeat. |
| `POST /api/sdk/achievements/{key}/progress` with `{"value": 37}` | Set a counter to an absolute value. It never goes down and is capped at the target; reaching the target unlocks it. |
| `GET /api/sdk/achievements?lang=en` | Definitions plus this player's progress, unlock time and rarity (needs the player to be online). |

Both `POST` calls answer with:

```json
{ "key": "kills", "progress": 40, "target": 100, "unlocked": false, "justUnlocked": false }
```

`justUnlocked` is `true` only for the call that actually unlocked it — show your own toast then. The player also gets a notification in the SGS client.

When the player is offline the bridge answers `202` with the same shape plus `"queued": true` — treat it as success; the unlock reaches the server later (and the notification arrives then).

Gdy gracz jest offline, most odpowiada `202` w tym samym kształcie plus `"queued": true` — traktuj to jako sukces; odblokowanie dotrze do serwera później (wtedy też przyjdzie powiadomienie).

| Status | Meaning / Znaczenie |
|---|---|
| `200` | OK |
| `202` | Accepted into the offline queue / przyjęte do kolejki offline |
| `400` | Invalid input, e.g. `progress` on a one-shot achievement / np. `progress` na osiągnięciu jednorazowym |
| `401` | Token missing or wrong / brak lub błędny token |
| `403` | The player does not have the game in their library / gracz nie ma gry w bibliotece |
| `404` | Unknown or deactivated key / nieznany lub nieaktywny klucz |
| `503` | `GET` while offline or signed out / `GET` bez sieci lub bez logowania |

Keys are trimmed and lowercased by the server, so `First_Boss` and `first_boss` are the same achievement.

## 4. Examples / Przykłady

### curl

```bash
curl -X POST "$SGS_API_URL/api/sdk/achievements/first_boss/unlock" \
     -H "X-SGS-Token: $SGS_ACHIEVEMENTS_TOKEN"

curl -X POST "$SGS_API_URL/api/sdk/achievements/kills/progress" \
     -H "X-SGS-Token: $SGS_ACHIEVEMENTS_TOKEN" \
     -H "Content-Type: application/json" -d '{"value": 40}'
```

### Unity (C#)

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public static class Sgs
{
    public static IEnumerator Unlock(string key)
    {
        var url = System.Environment.GetEnvironmentVariable("SGS_API_URL");
        var token = System.Environment.GetEnvironmentVariable("SGS_ACHIEVEMENTS_TOKEN");
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(token)) yield break;

        using var request = new UnityWebRequest($"{url}/api/sdk/achievements/{key}/unlock", "POST");
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("X-SGS-Token", token);
        yield return request.SendWebRequest();
    }
}
// StartCoroutine(Sgs.Unlock("first_boss"));
```

### Godot 4 (GDScript)

```gdscript
func sgs_unlock(key: String) -> void:
    var url := OS.get_environment("SGS_API_URL")
    var token := OS.get_environment("SGS_ACHIEVEMENTS_TOKEN")
    if url.is_empty() or token.is_empty():
        return
    var http := HTTPRequest.new()
    add_child(http)
    http.request_completed.connect(func(_r, _c, _h, _b): http.queue_free())
    http.request(url + "/api/sdk/achievements/" + key + "/unlock",
        ["X-SGS-Token: " + token], HTTPClient.METHOD_POST)
```

### Unreal (C++)

```cpp
#include "HttpModule.h"
#include "Interfaces/IHttpRequest.h"
#include "HAL/PlatformMisc.h"

void SgsUnlock(const FString& Key)
{
    const FString Url = FPlatformMisc::GetEnvironmentVariable(TEXT("SGS_API_URL"));
    const FString Token = FPlatformMisc::GetEnvironmentVariable(TEXT("SGS_ACHIEVEMENTS_TOKEN"));
    if (Url.IsEmpty() || Token.IsEmpty()) return;

    auto Request = FHttpModule::Get().CreateRequest();
    Request->SetURL(Url + TEXT("/api/sdk/achievements/") + Key + TEXT("/unlock"));
    Request->SetVerb(TEXT("POST"));
    Request->SetHeader(TEXT("X-SGS-Token"), Token);
    Request->ProcessRequest();
}
```

Any other engine works the same way: read the two variables, send the header, `POST`.

Każdy inny silnik działa tak samo: odczytaj dwie zmienne, ustaw nagłówek, wyślij `POST`.

## 5. Without the SGS client / Bez klienta SGS

Another launcher (or a server) can call the API directly. Ask `POST /api/me/games/{id}/achievements/session` with the player's sign-in token for a short-lived game token (12 h), then use the same `api/sdk/achievements` paths on the API host with `X-SGS-Token`. There is no offline queue in that mode.

Inny launcher (lub serwer) może wołać API bezpośrednio: `POST /api/me/games/{id}/achievements/session` z tokenem logowania gracza zwraca krótkotrwały token gry (12 h), z którym używasz tych samych ścieżek `api/sdk/achievements` na hoście API. W tym trybie nie ma kolejki offline.

The API host needs `Achievements:SigningKey` (env `Achievements__SigningKey`, 32+ random characters) to sign those tokens. Without it a random key is used per start, so tokens die on every restart (and with several API instances). The SGS client does not need it.

Host API potrzebuje `Achievements:SigningKey` (zmienna `Achievements__SigningKey`, 32+ losowych znaków) do podpisywania tych tokenów. Klient SGS jej nie potrzebuje.

## Limits / Ograniczenia

- Game code runs on the player's machine, so achievements cannot be made tamper-proof. Treat them as cosmetic.
- The queue replays when the client is running and the player is signed in; calls for another account stay queued until that account signs in.
- Players can hide their achievements from others in **Profile → Customize profile → Privacy**; hidden lists are empty for everyone but the owner. Per-game rarity stays aggregated and anonymous.

- Kod gry działa na komputerze gracza, więc osiągnięć nie da się zabezpieczyć przed ingerencją. Traktuj je jako element kosmetyczny.
- Kolejka jest odtwarzana, gdy klient działa i gracz jest zalogowany; wywołania innego konta czekają, aż to konto się zaloguje.
- Gracz może ukryć swoje osiągnięcia przed innymi w **Profil → Dostosuj profil → Prywatność**; ukryta lista jest pusta dla wszystkich poza właścicielem. Rzadkość per gra pozostaje zagregowana i anonimowa.
