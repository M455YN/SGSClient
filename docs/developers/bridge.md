# Most lokalny

Gra uruchomiona z klienta SGS nie łączy się z API bezpośrednio. Łączy się z **mostem lokalnym**, który działa w kliencie na pętli zwrotnej (`127.0.0.1`). Most dodaje do wywołań tożsamość gracza, kolejkuje je, gdy brak sieci, i odtwarza po powrocie.

## Dlaczego most

- **Brak wygasających tokenów.** Most podpisuje wywołania logowaniem gracza, więc długa sesja nie traci uprawnień.
- **Tryb offline.** Odblokowania, postęp i zapisy statystyk (`api/sdk/stats/{key}`; z kolejnych zapisów jednej statystyki zostaje ostatni) trafiają do pliku kolejki (`%LocalAppData%/SGSClient/achievement-queue.json`) i są wysyłane co minutę albo po powrocie sieci. Kolejka przeżywa restart klienta.
- **Ta sama ścieżka co w API.** Endpointy mostu używają tych samych adresów co `api/sdk/…`, więc kod pisany pod most działa też bez klienta, pod warunkiem innego tokenu (patrz niżej).

## Nagłówki i CORS

Każde wywołanie wysyła nagłówek:

```http
X-SGS-Token: <wartość SGS_ACHIEVEMENTS_TOKEN>
```

Most odpowiada na zapytania `OPTIONS` (CORS preflight), więc działają buildy HTML5 i WebGL.

## Odpowiedzi

Most przekazuje bez zmian odpowiedzi API `200`, `400`, `403` i `404`. Dodatkowo zwraca:

| Kod | Znaczenie | Co zrobić |
|---|---|---|
| `200` | Przyjęte | Pokaż własny komunikat, jeśli `justUnlocked` to `true` |
| `202` | Przyjęte do kolejki, bo gracz jest offline; pole `"queued": true` | Traktuj jak sukces |
| `400` | Zły kształt danych, np. `progress` na osiągnięciu jednorazowym albo statystyka łamiąca zakres, `maxChange` lub `incrementOnly` | Napraw wywołanie; nie ponawiaj |
| `401` | Brak lub zły token | Sprawdź, czy wysyłasz `X-SGS-Token` z `SGS_ACHIEVEMENTS_TOKEN` |
| `403` | Gracz nie ma gry w bibliotece | Nie ponawiaj |
| `404` | Nieznany lub nieaktywny klucz | Sprawdź klucz w manifeście |
| `503` | Brak logowania lub sieci przy odczycie (`GET`) | Pokaż stan offline |

Zapisy (`POST`) przy braku sieci zwracają `202`. Odczyty (`GET`) nie są kolejkowane, więc przy braku logowania dostajesz `503`.

## Jeśli zmiennych nie ma

Gra uruchomiona poza klientem (np. z edytora) nie dostaje zmiennych. Pomiń wywołania SGS i kontynuuj normalnie.

## Zastrzeżenia

- Kolejka odtwarza wywołania, gdy klient działa i gracz jest zalogowany. Wywołania konta, które nie jest aktualnie zalogowane, czekają.
- Sesje tokenów żyją tak długo, jak proces klienta.
- Most nie przechodzi przez NAT ani nie łączy graczy ze sobą. Do tego służy [lobby](../creators/lobbies.md).
