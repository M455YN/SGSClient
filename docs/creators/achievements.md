# Osiągnięcia

Osiągnięcia w SGS to zwykłe HTTP. Każdy silnik, który potrafi wysłać `POST`, może je odblokowywać. Nie potrzebujesz SDK ani natywnej wtyczki.

## 1. Zdefiniuj osiągnięcia

Masz dwa sposoby. Wybierz ten, który pasuje do Twojego workflow:

- **Panel twórcy** (admini: Panel admina, dowolna gra): wybierz grę → zakładka **Osiągnięcia** → **Dodaj osiągnięcie** → **Zapisz osiągnięcia**.
- **Manifest paczki** (tablica `achievements` w `sgs-manifest.json`): publikowana razem z buildem.

| Pole | Znaczenie |
|---|---|
| `key` | Stały identyfikator używany w kodzie gry, np. `first_boss`. Małe litery, cyfry, `_` i `-`, do 64 znaków. **Nie zmieniaj go.** Nowy klucz to nowe osiągnięcie. |
| `title`, `description` | Widoczne dla graczy (do 128 i 512 znaków). |
| `translations` | `{ "en": { "title": "…", "description": "…" } }`, do 10 kodów języków. Panel edytuje wersję angielską, pozostałe kody są zachowywane. Gracze z danym językiem widzą tłumaczenie, pozostali tekst podstawowy. |
| `icon` (manifest) / ikona (panel) | Adres `http(s)`, ścieżka obrazu w ZIP (manifest) albo wgrany plik (panel: **Wgraj ikonę**). Formaty: png, jpg, webp, gif. |
| `points` | 0–1000, wartość kosmetyczna. |
| `target` | Brak = odblokowanie jednorazowe. Liczba (2–1 000 000) = licznik, który odblokowuje osiągnięcie po osiągnięciu wartości. |
| `hidden` | Tytuł, opis i ikona są puste dla graczy do momentu odblokowania. |

Na grę przypada do 500 osiągnięć. Usunięcie osiągnięcia tylko je dezaktywuje: gracze zachowują zdobyte, a ponowne dodanie tego samego klucza je przywraca.

### Manifest

```json
"achievements": [
  { "key": "first_run", "title": "Pierwszy bieg", "description": "Ukończ pierwszy poziom.",
    "icon": "media/first_run.png", "points": 10,
    "translations": { "en": { "title": "First run", "description": "Finish the first level." } } },
  { "key": "marathon", "title": "Maratończyk", "points": 50, "target": 100 }
]
```

Brak tablicy `achievements` zostawia osiągnięcia gry bez zmian. Obecna tablica, nawet pusta, **zastępuje** je przy publikacji, więc manifest wygrywa z Panelem twórcy. Pełny przykład: [`demo-runner.sgs-manifest.json`](https://github.com/M455YN/SGSClient/blob/master/schemas/examples/demo-runner.sgs-manifest.json).

Flaga funkcji **Osiągnięcia** w sklepie włącza się automatycznie, gdy lista nie jest pusta. **Testowo odblokuj** odblokowuje osiągnięcie na Twoim koncie. **Wyzeruj mój postęp** czyści Twój postęp w tej grze.

Gracze widzą listę na stronie gry (zakładka **Osiągnięcia**) i podsumowanie w profilu. Podsumowanie jest widoczne dla innych, o ile gracz tego nie ukrył.

## 2. Co dostaje gra

Gdy gracz uruchamia grę z klienta SGS, proces dostaje trzy zmienne środowiskowe:

| Zmienna | Przykład | Znaczenie |
|---|---|---|
| `SGS_API_URL` | `http://127.0.0.1:51234` | Adres **lokalnego mostu SGS** (tylko pętla zwrotna) |
| `SGS_GAME_ID` | `12` | Id Twojej gry |
| `SGS_ACHIEVEMENTS_TOKEN` | `3f9a…` | Losowy token na jedno uruchomienie, akceptowany tylko przez most |

Most podpisuje wywołania logowaniem gracza, więc token nie wygasa w długiej sesji. Gdy gracz jest **offline**, most **kolejkuje wywołania** i odtwarza je później. Kolejka przeżywa restart klienta. Jeśli zmiennych nie ma (gra uruchomiona poza SGS), pomiń osiągnięcia. Gra ma działać dalej.

Token wysyłaj w nagłówku `X-SGS-Token`.

## 3. Endpointy

Wszystkie wywołania są względne do `SGS_API_URL`. Most odpowiada na zapytania CORS preflight, więc działają też buildy HTML5 i WebGL.

| Wywołanie | Cel |
|---|---|
| `POST /api/sdk/achievements/{key}/unlock` | Odblokowanie osiągnięcia jednorazowego (lub wypełnienie licznika). Można powtarzać bezpiecznie. |
| `POST /api/sdk/achievements/{key}/progress` z `{"value": 37}` | Ustawienie licznika na wartość bezwzględną. Nigdy nie spada i jest ograniczony do celu. Osiągnięcie celu je odblokowuje. |
| `GET /api/sdk/achievements?lang=pl` | Definicje oraz postęp gracza, czas odblokowania i rzadkość (wymaga połączenia). |

Oba wywołania `POST` zwracają:

```json
{ "key": "kills", "progress": 40, "target": 100, "unlocked": false, "justUnlocked": false }
```

`justUnlocked` ma wartość `true` tylko dla wywołania, które faktycznie odblokowało osiągnięcie. Wtedy pokaż własny komunikat. Gracz dostaje też powiadomienie w kliencie SGS.

Gdy gracz jest offline, most odpowiada `202` w tym samym kształcie z polem `"queued": true`. Traktuj to jak sukces. Odblokowanie dotrze do serwera później, a powiadomienie przyjdzie wtedy.

| Kod | Znaczenie |
|---|---|
| `200` | OK |
| `202` | Przyjęte do kolejki offline |
| `400` | Błędne dane, np. `progress` na osiągnięciu jednorazowym |
| `401` | Brak lub błędny token |
| `403` | Gracz nie ma gry w bibliotece |
| `404` | Nieznany lub nieaktywny klucz |
| `503` | `GET` bez sieci lub bez logowania |

Serwer przycina klucze i zamienia je na małe litery, więc `First_Boss` i `first_boss` to to samo osiągnięcie.

## 4. Przykłady

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

Każdy inny silnik działa tak samo: odczytaj dwie zmienne, ustaw nagłówek i wyślij `POST`.

## 5. Bez klienta SGS

Inny launcher albo serwer może wołać API bezpośrednio. Wywołaj `POST /api/me/games/{id}/achievements/session` z tokenem logowania gracza. Dostaniesz krótkotrwały token gry (12 h). Potem używasz ścieżek `api/sdk/achievements` na hoście API z nagłówkiem `X-SGS-Token`. W tym trybie nie ma kolejki offline.

Host API potrzebuje `Achievements:SigningKey` (zmienna `Achievements__SigningKey`, 32 lub więcej losowych znaków), żeby podpisywać te tokeny. Bez niego serwer generuje losowy klucz przy każdym starcie, więc tokeny przestają działać po restarcie i przy kilku instancjach API. Klient SGS tego klucza nie potrzebuje.

## Ograniczenia

- Kod gry działa na komputerze gracza, więc osiągnięć nie da się zabezpieczyć przed ingerencją. Traktuj je jako element kosmetyczny.
- Kolejka jest odtwarzana, gdy klient działa i gracz jest zalogowany. Wywołania innego konta czekają, aż to konto się zaloguje.
- Gracz może ukryć swoje osiągnięcia przed innymi w **Profil → Dostosuj profil → Prywatność**. Ukryta lista jest pusta dla wszystkich poza właścicielem. Rzadkość per gra pozostaje zagregowana i anonimowa.
