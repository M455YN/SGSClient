# Referencja API

Najważniejsze endpointy do integracji z grą. Adresy są względne do bazy API (`Api:BaseUrl`, np. `https://api.example.com/`) i zaczynają się od `/api/`.

Endpointy do zarządzania kontem, znajomymi, czatem i panelem administracyjnym nie są tu opisane. Nie są potrzebne do integracji gry.

## Sklep

| Metoda | Ścieżka | Cel |
|---|---|---|
| GET | `games` | Katalog gier |
| GET | `games/{id}` | Strona gry |
| GET | `games/{id}/versions` | Wersje gry |
| GET | `games/{id}/achievements` | Osiągnięcia gry (`?lang=pl`) |

## Integracja z grą (`/api/sdk`)

Gra uruchomiona z klienta SGS wywołuje te ścieżki przez [most lokalny](bridge.md). Most dodaje tożsamość gracza, a wywołanie wymaga nagłówka `X-SGS-Token` z `SGS_ACHIEVEMENTS_TOKEN`.

| Metoda | Ścieżka | Cel |
|---|---|---|
| GET | `sdk/me` | Zalogowany gracz (`id`, `displayName`, `playerTag`) |
| GET | `sdk/achievements?lang=` | Definicje osiągnięć, postęp gracza i wartości statystyk (`stats`) |
| POST | `sdk/achievements/{key}/unlock` | Odblokuj osiągnięcie |
| POST | `sdk/achievements/{key}/progress` | Ustaw licznik, np. `{"value": 37}` |
| POST | `sdk/stats/{key}` | Ustaw statystykę, np. `{"value": 12}`; odblokowuje powiązane osiągnięcia po osiągnięciu progu |
| POST | `sdk/lobbies` | Otwórz lobby, zwraca kod |
| GET / PATCH / DELETE | `sdk/lobbies/{code}` | Odczyt, aktualizacja i zamknięcie lobby (aktualizacja tylko przez hosta) |
| POST | `sdk/lobbies/{code}/invite` | Zaproś znajomego (tylko host) |
| POST | `sdk/lobbies/{code}/finish` | Koniec meczu, wszyscy wracają do lobby (tylko host). Klient robi to sam, gdy gra hosta się zamknie |

Szczegóły i przykłady: [Osiągnięcia i statystyki](../creators/achievements.md) i [Lobby i zaproszenia](../creators/lobbies.md).

## Lobby w kliencie (`/api/lobbies`)

Wywołuje klient SGS z tokenem logowania gracza. Gra używa tylko `sdk/lobbies/{code}`.

| Metoda | Ścieżka | Cel |
|---|---|---|
| POST | `lobbies/{code}/join` | Dołącz do lobby (każdy, kto ma grę i zna kod; `400` gdy pełne lub wystartowało) |
| POST | `lobbies/{code}/leave` | Opuść lobby (tylko członek). Gdy wychodzi host, gospodarzem zostaje najwcześniej dołączony gracz; ostatnie wyjście zamyka lobby |
| POST | `lobbies/{code}/ready` z `{"ready": true}` | Gość zgłasza gotowość (lub ją cofa) |
| POST | `lobbies/{code}/kick` z `{"userId": 3}` | Host wyrzuca gracza |
| POST | `lobbies/{code}/start` | Start (tylko host, `400` gdy nie wszyscy goście są gotowi); klienci członków uruchamiają grę |
| POST | `lobbies/{code}/finish` | Koniec meczu (tylko host): `started` wraca na `false`, gotowość się zeruje |
| GET | `lobbies/{code}/invitable` | Znajomi hosta z grą, jeszcze nie w lobby (`userId`, `displayName`, `isOnline`) |

`LobbyDto` ma dodatkowo `members` (lista `userId`, `displayName`, `isReady`, host pierwszy) i `started` (trwa mecz).

SignalR (`ChatHub`): `JoinLobbyGroup(code)` (tylko członkowie) i `LeaveLobbyGroup(code)`. Członkowie dostają zdarzenie `LobbyUpdated` z `LobbyDto`.

## Panel twórcy (`/api/games/{id}/studio`)

Wymagają logowania właściciela gry albo admina.

| Metoda | Ścieżka | Cel |
|---|---|---|
| GET | `games/{id}/studio/achievements` | Definicje osiągnięć (z `lockedIconUrl`, `statKey`, licznikiem odblokowań) |
| GET | `games/{id}/studio/stats` | Definicje statystyk |
| PUT | `games/{id}/studio/achievements` | Pełna lista `items`; opcjonalne `stats` zastępuje statystyki (brak = bez zmian) |
| PUT | `games/{id}/studio/achievements/{key}/icon[?locked=true]` | Wgranie ikony zdobytej (lub niezdobytej) |
| DELETE | `games/{id}/studio/achievements/progress` | Wyzerowanie własnych osiągnięć i statystyk w grze (testy) |

## Launcher bez klienta SGS

Inny launcher albo serwer może działać bez mostu. Wywołaj `POST /api/me/games/{gameId}/achievements/session` z tokenem logowania gracza. Dostaniesz krótkotrwały token gry (12 h) do nagłówka `X-SGS-Token`. Potem używaj tych samych ścieżek `sdk/*` na hoście API. Szczegóły: [Osiągnięcia](../creators/achievements.md#6-bez-klienta-sgs).

## Kody odpowiedzi

| Kod | Znaczenie |
|---|---|
| 200 / 204 | Sukces |
| 202 | Przyjęte do kolejki offline (tylko most) |
| 400 | Błędne dane |
| 401 | Brak lub błędny token |
| 403 | Brak uprawnień, np. gracz nie ma gry w bibliotece albo nie jest hostem lobby |
| 404 | Nie znaleziono (nieznany klucz osiągnięcia, statystyki lub kod lobby) |
| 503 | Usługa niedostępna albo brak logowania przy odczycie |
