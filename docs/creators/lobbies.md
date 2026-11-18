# Lobby i zaproszenia

Lobby pozwala hostowi udostępnić adres znajomym przez krótki kod. SGS pomaga tylko **znaleźć** hosta. Połączenie nawiązuje sama gra (ENet, WebSocket albo cokolwiek używasz).

Tak jak osiągnięcia, lobby to zwykły HTTP na lokalnym moście, bez SDK. Gra uruchomiona z klienta SGS dostaje te same `SGS_API_URL` i `SGS_ACHIEVEMENTS_TOKEN` (token w nagłówku `X-SGS-Token`), a do tego:

| Zmienna | Znaczenie |
|---|---|
| `SGS_LOBBY_CODE` | Ustawiona, gdy gracz dołączył przez zaproszenie albo przycisk **Dołącz** u znajomego. Połącz się z tym lobby przy starcie. |
| `SGS_LOBBY_HOST` | `1` u hosta lobby utworzonego w kliencie (patrz niżej). U gości brak. |

`SGS_API_URL` i token klient ustawia od nowa przy każdym uruchomieniu. Zawsze czytaj je ze zmiennych środowiskowych, nigdy nie wpisuj ich na sztywno.

Lobby można prowadzić na dwa sposoby: [w kliencie SGS](#lobby-w-kliencie-sgs) (prostsze, gra nie potrzebuje własnego UI) albo [z gry](#wywołania) (gra sama otwiera lobby i zaprasza).

## Lobby w kliencie SGS

Działający przykład: [samples/lobby-demo](../../samples/lobby-demo/README.md), mini gra w kości z gotowym manifestem. Kodu lobby nigdzie nie wpisujesz, bo klient przekazuje go grze w `SGS_LOBBY_CODE`.

Gra deklaruje lobby w manifeście. Bez flagi `lobby` nic się nie zmienia.

```json
"features": ["multiplayer", "lobby"],
"lobbyMaxPlayers": 4
```

| Pole | Zasada |
|---|---|
| `lobby` w `features` | Na stronie gry w bibliotece pojawia się **Graj ze znajomymi** |
| `lobbyMaxPlayers` | `1`–`16`, wymagane przy `lobby` |

Przebieg:

1. Host klika **Graj ze znajomymi**. Klient tworzy lobby i pokazuje listę graczy oraz zaproszenia znajomych, którzy mają grę.
2. Znajomi wchodzą przez zaproszenie albo **Dołącz** na liście znajomych. Na pasku bocznym widzą **W lobby**.
3. Host klika **Start**. Klient każdego członka uruchamia grę z `SGS_LOBBY_CODE`, a host dostaje też `SGS_LOBBY_HOST=1`.
4. **Host:** gra startuje serwer i wysyła `PATCH /api/sdk/lobbies/{code}` z `{"address":"…"}`. Powtarzaj PATCH co kilka minut, żeby lobby nie wygasło.
5. **Gość:** gra wywołuje `GET /api/sdk/lobbies/{code}`, aż `address` nie będzie pusty, i łączy się pod ten adres.
6. Po meczu lobby się zamyka. Nie ma powrotu do lobby.

Gra nie wywołuje `POST`, `invite` ani nie potrzebuje własnego UI lobby.

### Przykład (Unity)

```csharp
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class SgsLobby : MonoBehaviour
{
    string api = Environment.GetEnvironmentVariable("SGS_API_URL");
    string token = Environment.GetEnvironmentVariable("SGS_ACHIEVEMENTS_TOKEN");
    string code = Environment.GetEnvironmentVariable("SGS_LOBBY_CODE");
    bool isHost = Environment.GetEnvironmentVariable("SGS_LOBBY_HOST") == "1";

    [Serializable] class Lobby { public string address; }

    // Host: po starcie serwera, potem co kilka minut.
    public IEnumerator PublishAddress(string address)
    {
        var body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Lobby { address = address }));
        using var req = new UnityWebRequest($"{api}/api/sdk/lobbies/{code}", "PATCH");
        req.uploadHandler = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("X-SGS-Token", token);
        yield return req.SendWebRequest();
    }

    // Gość: czekaj na adres hosta.
    public IEnumerator WaitForAddress(Action<string> connect)
    {
        while (true)
        {
            using var req = UnityWebRequest.Get($"{api}/api/sdk/lobbies/{code}");
            req.SetRequestHeader("X-SGS-Token", token);
            yield return req.SendWebRequest();
            var address = req.result == UnityWebRequest.Result.Success
                ? JsonUtility.FromJson<Lobby>(req.downloadHandler.text).address : null;
            if (!string.IsNullOrEmpty(address)) { connect(address); yield break; }
            yield return new WaitForSeconds(2);
        }
    }
}
```

### Przez internet (relay silnika)

`address` to dowolny tekst (do 100 znaków, bez spacji). Zamiast IP możesz wpisać kod relaya z silnika (Unity Relay, Steam Networking, EOS, Photon). Wtedy gracze połączą się przez internet. Przykład z Unity Relay:

```csharp
// Host
var alloc = await RelayService.Instance.CreateAllocationAsync(maxConnections: 3);
var joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
StartCoroutine(PublishAddress(joinCode));

// Gość: address z WaitForAddress to joinCode
var join = await RelayService.Instance.JoinAllocationAsync(address);
```

## Wywołania

| Wywołanie | Cel |
|---|---|
| `GET /api/sdk/me` | `{ "id", "displayName", "playerTag" }` zalogowanego gracza, np. do menu. `503`, gdy gracz nie jest zalogowany. |
| `POST /api/sdk/lobbies` z `{"address":"192.168.0.5:7777","maxPlayers":4,"players":1}` | Otwiera lobby i zwraca `{ "code": "K7MQ2X", … }`. Gra to ta uruchomiona. Host ma jedno lobby na grę, a nowe zastępuje stare. |
| `GET /api/sdk/lobbies/{code}` | Adres hosta, liczba graczy i miejsc, żeby się połączyć. |
| `PATCH /api/sdk/lobbies/{code}` z `{"players":2}`, `{"isOpen":false}` albo `{"address":"…"}` | Tylko host. **Każde wywołanie podtrzymuje lobby.** Wysyłaj je co kilka minut i przy zmianie liczby graczy. |
| `DELETE /api/sdk/lobbies/{code}` | Zamyka lobby (tylko host). |
| `POST /api/sdk/lobbies/{code}/invite` z `{"friendUserId": 3}` | Zaprasza znajomego (tylko host, tylko znajomych). Zaproszony dostaje powiadomienie, a kliknięcie uruchamia grę z `SGS_LOBBY_CODE`. |

Znajomi widzą też otwarte lobby z wolnym miejscem przy znajomym na liście **Znajomi** i mogą dołączyć stamtąd.

| Kod | Znaczenie |
|---|---|
| `200` / `204` | OK |
| `400` | Błędny adres (bez spacji), liczba graczy lub miejsc |
| `401` | Brak lub błędny token |
| `403` | Gracz nie ma gry, nie jest hostem albo zapraszany nie jest znajomym |
| `404` | Nieznany lub wygasły kod |
| `503` | Brak logowania lub sieci. W przeciwieństwie do osiągnięć lobby działa na żywo, bez kolejki offline. |

Kody mają 6 znaków (`A–Z`, `2–9`, bez `0 O 1 I`) i nie rozróżniają wielkości liter. Lobby wygasa 10 minut po ostatnim dotknięciu przez hosta.

## Przykład

```bash
curl -X POST "$SGS_API_URL/api/sdk/lobbies" \
     -H "X-SGS-Token: $SGS_ACHIEVEMENTS_TOKEN" -H "Content-Type: application/json" \
     -d '{"address":"192.168.0.5:7777","maxPlayers":4,"players":1}'
```

## Ograniczenia

- **SGS nie przechodzi przez NAT.** Przekazuje tylko `address`, który mu wysłałeś. Zwykły IP zadziała w LAN albo u hosta z otwartym portem. Przez internet wpisz do `address` kod relaya silnika (Unity Relay, Steam Networking, EOS, Photon), patrz [wyżej](#przez-internet-relay-silnika).
- Lobby żyją w pamięci API. Restart API je kasuje, a kilka instancji API ich nie współdzieli.
- Każdy zalogowany, kto zna kod, może odczytać lobby, bo kod jest sekretem. Zaproszenie i widok na liście mają tylko znajomi hosta.
