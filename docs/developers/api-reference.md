# Referencja API

Adresy są względne do bazy API (`Api:BaseUrl` w konfiguracji klienta, np. `https://api.example.com/`). Wszystkie zaczynają się od `/api/`.

**Uwaga:** lista pochodzi z atrybutów kontrolerów w `src/Apps/SGSClient.Api/Controllers`. Nie ma jeszcze wygenerowanego, pełnego dokumentu OpenAPI. Parametry, body i kody odpowiedzi sprawdzaj w kodzie kontrolera.

## Zasady

- **Logowanie.** Endpointy `me/*` i `studio/*` (oraz `lobbies`, `uploads`) wymagają tokenu gracza w nagłówku `Authorization: Bearer <token>`. Brak tokenu daje `401`.
- **Gra uruchomiona z klienta** używa mostu zamiast API: [Most lokalny](bridge.md). Endpointy `sdk/*` z tej strony działają też bezpośrednio, ale wymagają tokenu gry z `me/games/{id}/achievements/session`.
- **Limity** (paczki, media, twórczość) są w [Publikowaniu gier](../creators/how-to-publish.md) i [Twórczości](../users/creative-works.md).

## Konto i profil (`/api/me`)

| Metoda | Ścieżka | Cel |
|---|---|---|
| GET | `me` | Dane zalogowanego użytkownika |
| GET | `me/social` | Znajomi, obserwowani, zaproszenia |
| POST | `me/friends/{userId}` | Wyślij prośbę o znajomość |
| POST | `me/friends/{userId}/accept` | Zaakceptuj prośbę |
| DELETE | `me/friends/{userId}` | Usuń znajomego |
| PUT / DELETE | `me/follows/{userId}` | Obserwuj / przestań |
| GET | `me/chat/threads` | Lista rozmów |
| GET / POST | `me/chat/{peerUserId}` | Historia i wysyłanie wiadomości |
| GET | `me/lobbies/friends` | Otwarte lobby znajomych |
| GET | `me/achievements` | Osiągnięcia zalogowanego gracza |
| POST | `me/games/{gameId}/achievements/session` | Token gry (12 h) dla zewnętrznych launcherów |
| POST | `me/games/{gameId}/achievements/{key}/unlock` | Odblokuj osiągnięcie |
| POST | `me/games/{gameId}/achievements/{key}/progress` | Ustaw licznik (`{"value": 37}`) |
| GET | `notifications`, `notifications/unread-count` | Powiadomienia |
| POST | `notifications/{id}/read`, `notifications/read-all` | Oznacz jako przeczytane |
| PUT | `avatar`, DELETE `avatar` | Zdjęcie profilowe |
| PUT | `tag` | Ustaw tag gracza |
| GET | `tag-available` | Sprawdź wolny tag |

## Użytkownicy (`/api/users`)

| Metoda | Ścieżka | Cel |
|---|---|---|
| GET | `users/search` | Szukaj użytkowników |
| GET | `users/by-tag/{tag}` | Znajdź po tagu |
| GET | `users/{userId}` | Profil publiczny |
| POST / DELETE | `users/{userId}/comments`, `…/comments/{commentId}` | Wpisy na ścianie |
| PATCH | `users/{id}/role` | Zmiana roli (tylko administrator) |

## Sklep i biblioteka (`games`)

| Metoda | Ścieżka | Cel |
|---|---|---|
| GET | `games` | Katalog |
| GET | `{id}` | Strona gry |
| GET | `{id}/versions`, `{id}/file` | Wersje i pliki do pobrania |
| GET | `{id}/reviews` | Recenzje |
| PUT / DELETE | `{id}/reviews/me` | Twoja recenzja |
| GET | `{id}/comments`, POST `{id}/comments` | Komentarze |
| GET | `{id}/dlcs` | DLC gry |
| GET | `library`, POST / DELETE `library/{gameId}` | Biblioteka |
| GET | `wishlist`, POST / DELETE `wishlist/{gameId}` | Lista życzeń |
| GET | `playtime`, `playtime/{gameId}`, POST `playtime/sessions` | Czas gry |
| POST | `uploads` | Wysyłka paczki (ZIP z `sgs-manifest.json`) |
| GET | `packages/{symbol}/{version}/{fileName}` | Pliki paczki |
| GET | `media/{symbol}/{fileName}`, `avatars/{userId}/{fileName}` | Grafika |

## Starsze endpointy (`/api/rofw`, `/api/reportingo`)

Kontrolery `Legacy/RofwController.cs` i `Legacy/ReportingoController.cs` obsługują starsze funkcje (np. ranking, karta gracza, wyniki). Nie są opisane w tej wiki. Nowe integracje nie powinny ich używać.

## Panel twórcy i studio

| Metoda | Ścieżka | Cel |
|---|---|---|
| GET | `mine` | Twoje gry (rola Twórca i wyższa) |
| PATCH | `{id}` / `games/{id}` | Dane gry |
| PATCH | `{id}/versions/{versionId}/notes` | Opis zmian wersji |
| PUT / POST / DELETE | `{id}/media/cover`, `…/header`, `…/logo`, `…/intro`, `…/screenshots` | Media |
| GET / PUT | `games/{gameId}/studio/achievements` | Definicje osiągnięć |
| PUT | `games/{gameId}/studio/achievements/{key}/icon` | Ikona osiągnięcia |
| DELETE | `games/{gameId}/studio/achievements/progress` | Wyzeruj własny postęp |
| GET | `games/{gameId}/achievements` | Osiągnięcia gry dla gracza (`?lang=`) |

## Most i SDK (`/api/sdk`)

Te ścieżki są używane przez grę, a most przekazuje je dalej. Wywołanie wymaga nagłówka `X-SGS-Token`.

| Metoda | Ścieżka | Cel |
|---|---|---|
| GET | `sdk/me` | Gracz zalogowany w kliencie |
| GET | `sdk/achievements?lang=` | Definicje i postęp |
| POST | `sdk/achievements/{key}/unlock` | Odblokuj |
| POST | `sdk/achievements/{key}/progress` | Ustaw licznik |
| POST | `sdk/lobbies` | Otwórz lobby (most mapuje na `lobbies`) |
| GET / PATCH / DELETE | `sdk/lobbies/{code}` | Odczyt, aktualizacja, zamknięcie |
| POST | `sdk/lobbies/{code}/invite` | Zaproś znajomego |

## Lobby (`/api/lobbies`)

| Metoda | Ścieżka | Cel |
|---|---|---|
| POST | `lobbies` | Utwórz |
| GET | `lobbies/{code}` | Odczyt |
| PATCH | `lobbies/{code}` | Aktualizacja (tylko host); każde wywołanie podtrzymuje lobby |
| DELETE | `lobbies/{code}` | Zamknięcie (tylko host) |
| POST | `lobbies/{code}/invite` | Zaproszenie (tylko znajomi hosta) |

Szczegóły i kody odpowiedzi: [Lobby i zaproszenia](../creators/lobbies.md).

## Twórczość (`/api/art`, `/api/music`, `/api/stories`)

Każdy typ ma ten sam wzór:

| Metoda | Ścieżka | Cel |
|---|---|---|
| GET | `{type}` | Lista |
| GET | `{type}/mine` | Twoje prace |
| GET | `{type}/{id}` | Szczegóły |
| GET | `{type}/{id}/file` (muzyka, grafika) / `{id}/cover` | Pliki |
| POST | `{type}` | Dodaj (multipart) |
| PUT | `{type}/{id}` | Edytuj |
| DELETE | `{type}/{id}` | Usuń |
| GET / POST / DELETE | `{type}/{id}/comments`, `…/comments/{commentId}` | Komentarze |

Limity i formaty: [Twórczość](../users/creative-works.md).

## Kody odpowiedzi

| Kod | Znaczenie |
|---|---|
| 200 / 201 / 204 | Sukces |
| 400 | Błędne dane; body ma pole `errors` |
| 401 | Brak lub zły token |
| 403 | Brak uprawnień (np. nie posiadasz gry, nie jesteś hostem) |
| 404 | Nie znaleziono |
| 413 | Plik większy niż limit |
| 503 | Usługa niedostępna lub brak logowania przy odczycie |
