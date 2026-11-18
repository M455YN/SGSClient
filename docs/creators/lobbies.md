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
2. Gracze wchodzą przez zaproszenie, **Dołącz** na liście znajomych albo wpisując kod lobby (np. z Discorda). Po kodzie może dołączyć każdy, kto ma grę, także spoza znajomych hosta. Na pasku bocznym widzą **W lobby**.
3. Każdy gość klika **Gotowy**. Gość bez zainstalowanej gry nie może tego zrobić. Host może wyrzucić gracza (✕ przy nazwie).
4. Gdy wszyscy są gotowi, host klika **Start**. Klient każdego członka uruchamia grę z `SGS_LOBBY_CODE`, a host dostaje też `SGS_LOBBY_HOST=1`.
5. **Host:** gra startuje serwer i wysyła `PATCH /api/sdk/lobbies/{code}` z `{"address":"…"}`. Powtarzaj PATCH co kilka minut, żeby lobby nie wygasło.
6. **Gość:** gra wywołuje `GET /api/sdk/lobbies/{code}`, aż `address` nie będzie pusty, i łączy się pod ten adres.
7. **Po meczu wszyscy wracają do lobby** w tym samym składzie. Dzieje się to samo, gdy gra hosta się zamknie, więc gra nie musi nic robić. Gra, która chce wrócić do lobby bez zamykania się (np. po rundzie), wywołuje `POST /api/sdk/lobbies/{code}/finish` (tylko host).

Jeśli host wyjdzie z lobby, gospodarzem zostaje gracz, który dołączył najwcześniej. Lobby zamyka się dopiero, gdy wyjdą wszyscy.

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

## Łączność: LAN, otwarty port, relay

SGS pomaga graczom się **znaleźć**: zbiera skład, przekazuje kod i adres. Samo połączenie między grami zestawia gra. SGS nie ma własnego serwera pośredniczącego (relaya) i nie przepuszcza ruchu gry.

### Dlaczego zwykły IP nie działa przez internet

Domowy router (NAT) wpuszcza tylko odpowiedzi na połączenia wychodzące. Gość próbujący połączyć się z IP hosta jest blokowany przez router hosta. Dlatego `192.168.0.5:7777` działa w jednej sieci, a między dwoma domami zwykle nie.

### Co może zrobić gra

| Sposób | Co wpisać do `address` | Dla kogo |
|---|---|---|
| **LAN** | Lokalny IP i port hosta, np. `192.168.0.5:7777` | Gracze w jednej sieci. Działa od razu. |
| **VPN dla graczy** (Tailscale, ZeroTier, Radmin VPN) | IP hosta w sieci VPN | Znajomi, którzy zainstalują ten sam VPN. Gra nic nie zmienia. |
| **Otwarty port u hosta** (przekierowanie portu albo UPnP z gry) | Publiczny IP i port hosta | Host, który umie lub może otworzyć port. Działa nie w każdej sieci (np. sieci komórkowe, akademiki). |
| **Relay silnika lub usługi** | Kod dołączenia z usługi | Gry dla wszystkich przez internet. Zalecane. |
| **Własny relay twórcy** | Adres twojego serwera i np. id sesji | Twórca, który utrzymuje własny serwer. |

`address` to dowolny tekst (do 100 znaków, bez spacji), więc mieści każdą z tych form. SGS go nie sprawdza i nie interpretuje.

### Relay silnika (zalecane przez internet)

Relay to serwer pośredniczący: host i goście łączą się do niego wychodząco, co przechodzi przez każdy router, a on przekazuje ruch między nimi. Większość silników i platform ma go gotowego:

| Usługa | Silnik / platforma | Uwagi |
|---|---|---|
| **Unity Relay** (Unity Gaming Services) | Unity | Kod dołączenia pasuje wprost do `address`. Darmowy limit, potem płatne. |
| **Steam Networking Sockets** | Gry na Steamie | Gracze muszą mieć Steam. |
| **Epic Online Services P2P** | Dowolny silnik | Darmowe, wymaga konta deweloperskiego Epic. |
| **Photon** (PUN, Fusion, Realtime) | Unity i inne | Darmowy limit graczy, potem płatne. |

Przykład z Unity Relay:

```csharp
// Host
var alloc = await RelayService.Instance.CreateAllocationAsync(maxConnections: 3);
var joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
StartCoroutine(PublishAddress(joinCode));

// Gość: address z WaitForAddress to joinCode
var join = await RelayService.Instance.JoinAllocationAsync(address);
```

### Którą drogę wybrać

- **Gra turowa, karciana, planszowa:** wystarczy TCP i dowolny relay. Na testy ze znajomymi wystarczy LAN albo VPN.
- **Gra akcji:** potrzebujesz UDP (ENet, KCP, transport silnika) i relaya z UDP, np. Unity Relay, Steam, EOS.
- **Własny silnik bez relaya:** zacznij od LAN i VPN. Do gry przez internet użyj EOS P2P albo własnego serwera.

### Dlaczego SGS nie ma własnego relaya

Rozważaliśmy to i na razie odpuściliśmy. Gry na popularnych silnikach mają relay w silniku albo w usłudze, a relay SGS pomógłby tylko grom bez takiej opcji, kosztem utrzymania osobnego serwera, który przepuszcza cały ruch gier. Jeśli twoja gra go potrzebuje, daj znać. Gdy pojawi się realna potrzeba, wrócimy do tematu: najpierw relay TCP dla gier turowych, a UDP dopiero dla gier akcji.

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

- **SGS nie przechodzi przez NAT i nie ma własnego relaya.** Przekazuje tylko `address`, który mu wysłałeś. Zwykły IP zadziała w LAN, przez VPN albo u hosta z otwartym portem. Przez internet użyj relaya silnika, patrz [Łączność](#łączność-lan-otwarty-port-relay).
- Lobby są zapisywane w bazie, więc przeżywają restart API. Przy kilku instancjach API zdarzenie `LobbyUpdated` dociera tylko do klientów połączonych z tą samą instancją, dopóki SignalR nie dostanie wspólnego backplane'u (np. Redis).
- Kod lobby działa jak zaproszenie: każdy zalogowany, kto go zna i ma grę, może odczytać lobby i do niego dołączyć. Zaproszenia i lista lobby znajomych są tylko dla znajomych hosta. Host może wyrzucić niechcianego gracza.
