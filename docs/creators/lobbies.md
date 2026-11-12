# Lobby i zaproszenia

Lobby pozwala hostowi udostępnić adres znajomym przez krótki kod. SGS pomaga tylko **znaleźć** hosta. Połączenie nawiązuje sama gra (ENet, WebSocket albo cokolwiek używasz).

Tak jak osiągnięcia, lobby to zwykły HTTP na lokalnym moście, bez SDK. Gra uruchomiona z klienta SGS dostaje te same `SGS_API_URL` i `SGS_ACHIEVEMENTS_TOKEN` (token w nagłówku `X-SGS-Token`), a do tego:

| Zmienna | Znaczenie |
|---|---|
| `SGS_LOBBY_CODE` | Ustawiona, gdy gracz dołączył przez zaproszenie albo przycisk **Dołącz** u znajomego. Połącz się z tym lobby przy starcie. |

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

- **Brak przechodzenia przez NAT.** SGS przekazuje adres, który mu wysłałeś. Znajomi w tej samej sieci lokalnej (LAN) albo host z otwartym portem połączą się bez problemu. Między domowymi routerami potrzebujesz UPnP lub własnego relaya. Relay w `SGSClient.Server` nie jest jeszcze zbudowany.
- Lobby żyją w pamięci API. Restart API je kasuje, a kilka instancji API ich nie współdzieli.
- Każdy zalogowany, kto zna kod, może odczytać lobby, bo kod jest sekretem. Zaproszenie i widok na liście mają tylko znajomi hosta.
