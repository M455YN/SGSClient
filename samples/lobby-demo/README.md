# Lobby Demo

Najmniejsza gra, która korzysta z lobby w kliencie SGS. To wyścig w rzucie kością: każdy gracz wciska Enter, a kto pierwszy dobije do 30, wygrywa. Cała integracja z SGS mieści się w [src/Program.cs](src/Program.cs) i zajmuje kilkadziesiąt linii.

## Jak gra używa lobby

Kodu lobby **nigdzie nie wpisujesz**. Gdy host w kliencie kliknie **Start**, klient uruchamia grę u każdego członka lobby i sam ustawia zmienne środowiskowe:

| Zmienna | Co gra z nią robi |
|---|---|
| `SGS_API_URL`, `SGS_ACHIEVEMENTS_TOKEN` | Adres lokalnego mostka i token. Ustawiane przy każdym uruchomieniu. |
| `SGS_LOBBY_CODE` | Kod lobby, w którym jest gracz. |
| `SGS_LOBBY_HOST=1` | Tylko u hosta. |

1. **Host** uruchamia serwer TCP i wysyła swój adres: `PATCH /api/sdk/lobbies/{code}` z `{"address":"192.168.0.5:53265"}`. Potem co 2 minuty wysyła pusty `PATCH`, żeby lobby nie wygasło.
2. **Gość** odpytuje `GET /api/sdk/lobbies/{code}`, aż pole `address` przestanie być puste, i łączy się z tym adresem.
3. Dalej gra działa już bez SGS. Host liczy wyniki i rozsyła je do wszystkich graczy.
4. Gdy host zamknie grę (`q`), klient SGS sam przywraca wszystkich do lobby na kolejną rundę. Gra nie musi wywoływać `finish`.

Nazwa gracza pochodzi z `GET /api/sdk/me`.

## Budowanie

```bash
dotnet publish samples/lobby-demo/src -c Release -o samples/lobby-demo/bin
```

Folder `samples/lobby-demo` (z `sgs-manifest.json` i `bin/LobbyDemo.exe`) można teraz wysłać w **Panelu twórcy** jak każdą inną grę. Manifest ma już `"features": ["multiplayer", "lobby"]` i `"lobbyMaxPlayers": 4`.

## Test bez klienta SGS

Dwa okna na jednym komputerze:

```bash
samples/lobby-demo/bin/LobbyDemo.exe --host
```

```bash
samples/lobby-demo/bin/LobbyDemo.exe --join 192.168.0.5:53265
```

W drugiej komendzie podaj adres, który wypisał host.

## Ograniczenia

- Host podaje swój adres w sieci lokalnej, więc gra działa w obrębie jednej sieci albo przez VPN. Gra przeznaczona do grania przez internet wpisuje do `address` kod relaya (patrz [docs/creators/lobbies.md](../../docs/creators/lobbies.md)).
- To przykład, nie wzór architektury sieciowej: tekstowy protokół, bez obsługi rozłączeń i ponownego dołączania.
