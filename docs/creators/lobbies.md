# Lobbies and invites / Lobby i zaproszenia

A lobby lets a host share an address with friends by a short code. SGS only helps friends **find** the host; the game connects to the host itself (ENet, WebSocket, whatever you use).

Lobby pozwala hostowi udostępnić adres znajomym przez krótki kod. SGS pomaga tylko **znaleźć** hosta; połączenie nawiązuje sama gra (ENet, WebSocket, cokolwiek używasz).

Like achievements, it is plain HTTP on the local bridge: no SDK. A game started from the SGS client gets the same `SGS_API_URL` and `SGS_ACHIEVEMENTS_TOKEN` (send the token in `X-SGS-Token`), plus:

Tak jak osiągnięcia, to zwykły HTTP na lokalnym moście, bez SDK. Gra uruchomiona z klienta SGS dostaje te same `SGS_API_URL` i `SGS_ACHIEVEMENTS_TOKEN` (token w nagłówku `X-SGS-Token`), a do tego:

| Variable / Zmienna | Meaning / Znaczenie |
|---|---|
| `SGS_LOBBY_CODE` | Set when the player joined through an invite or a friend's **Join** button: connect to that lobby on start. / Ustawiona, gdy gracz dołączył przez zaproszenie lub przycisk **Dołącz** u znajomego: połącz się z tym lobby przy starcie. |

## Calls / Wywołania

| Call | Purpose / Cel |
|---|---|
| `GET /api/sdk/me` | `{ "id", "displayName", "playerTag" }` of the signed-in player, for your menu. `503` when signed out. / Dane zalogowanego gracza do menu. |
| `POST /api/sdk/lobbies` with `{"address":"192.168.0.5:7777","maxPlayers":4,"players":1}` | Open a lobby, get `{ "code": "K7MQ2X", … }`. The game is the one that was launched. A host has one lobby per game: a new one replaces the old one. / Otwiera lobby i zwraca kod. Gra to ta uruchomiona; host ma jedno lobby na grę. |
| `GET /api/sdk/lobbies/{code}` | The host's `address`, players and slots, to connect. / Adres hosta, liczba graczy, żeby się połączyć. |
| `PATCH /api/sdk/lobbies/{code}` with `{"players":2}`, `{"isOpen":false}` or `{"address":"…"}` | Host only. **Every call keeps the lobby alive**: send one at least every few minutes, e.g. when a player joins or leaves. / Tylko host. **Każde wywołanie podtrzymuje lobby**: wysyłaj co kilka minut i przy zmianie liczby graczy. |
| `DELETE /api/sdk/lobbies/{code}` | Close it (host only). / Zamknij (tylko host). |
| `POST /api/sdk/lobbies/{code}/invite` with `{"friendUserId": 3}` | Invite a friend (host only, friends only): they get a notification and an open client shows it at once; clicking it starts the game with `SGS_LOBBY_CODE`. / Zaproszenie znajomego: dostaje powiadomienie, a kliknięcie uruchamia grę z `SGS_LOBBY_CODE`. |

Friends also see an open lobby with a free slot next to the friend in **Friends** and can join from there.

Znajomi widzą też otwarte lobby z wolnym miejscem przy znajomym na liście **Znajomi** i mogą dołączyć stamtąd.

| Status | Meaning / Znaczenie |
|---|---|
| `200` / `204` | OK |
| `400` | Invalid address (no spaces), players or slots / Zły adres, liczba graczy lub miejsc |
| `401` | Token missing or wrong / brak lub zły token |
| `403` | The player does not own the game, is not the host, or the invitee is not a friend / gracz nie ma gry, nie jest hostem albo zapraszany nie jest znajomym |
| `404` | Unknown or expired code / nieznany lub wygasły kod |
| `503` | Signed out or offline. Unlike achievements, lobbies are live: there is no offline queue. / Brak logowania lub sieci; lobby nie mają kolejki offline. |

Codes are 6 characters from `A–Z` and `2–9` (no `0 O 1 I`), case-insensitive. A lobby expires 10 minutes after the host last touched it.

Kody mają 6 znaków (`A–Z`, `2–9`, bez `0 O 1 I`), wielkość liter bez znaczenia. Lobby wygasa 10 minut po ostatnim dotknięciu przez hosta.

## Example / Przykład

```bash
curl -X POST "$SGS_API_URL/api/sdk/lobbies" \
     -H "X-SGS-Token: $SGS_ACHIEVEMENTS_TOKEN" -H "Content-Type: application/json" \
     -d '{"address":"192.168.0.5:7777","maxPlayers":4,"players":1}'
```

## Limits / Ograniczenia

- **No NAT traversal.** SGS gives the address you send it. Friends on the same network (LAN) or a host with a forwarded port connect fine; across home routers you need UPnP or your own relay. A relay in `SGSClient.Server` is not built yet. / **Brak przechodzenia przez NAT.** Znajomi w jednej sieci albo host z otwartym portem połączą się; między domowymi sieciami potrzeba UPnP lub własnego relaya.
- Lobbies live in the API's memory: an API restart drops them, and several API instances do not share them. / Lobby żyją w pamięci API: restart je kasuje, a kilka instancji API ich nie współdzieli.
- Anyone signed in who knows a code can read the lobby (the code is the secret). Only friends of the host get an invite or see it in their list. / Każdy zalogowany, kto zna kod, może odczytać lobby. Zaproszenie i widok na liście mają tylko znajomi hosta.
