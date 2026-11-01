# Integracja gry z SGS

Ta strona opisuje, jak gra łączy się z SGS. Potrzebujesz tylko protokołu HTTP. Nie ma SDK do zainstalowania, a każdy silnik (Unity, Godot, Unreal, Ren'Py, własny) może wysyłać żądania.

## Trzy poziomy integracji

| Poziom | Co daje | Kiedy wybrać |
|---|---|---|
| **Paczka** (`sgs-manifest.json`) | Metadane gry, wersje, osiągnięcia | Zawsze: to punkt wyjścia, bez niego nie ma publikacji |
| **Most lokalny** (zmienne środowiskowe + HTTP na `127.0.0.1`) | Osiągnięcia i lobby, działają offline z kolejką | Gra uruchamiana z klienta SGS |
| **API sklepu** (`/api/...`) | Pełny dostęp, także bez klienta | Launcher lub serwer innego typu |

## Kroki

1. **Opisz grę w manifeście.** Pola i limity: [Publikowanie gier](../creators/how-to-publish.md). Osiągnięcia dodajesz w sekcji `achievements`: [Osiągnięcia](../creators/achievements.md).
2. **Odczytaj zmienne środowiskowe** przy starcie. Jeśli ich nie ma, gra działa normalnie, bez funkcji SGS.
3. **Wywołuj most** tam, gdzie zachodzi zdarzenie: odblokowanie, postęp, otwarcie lobby. Szczegóły w [Most lokalny](bridge.md).
4. **Obsłuż odpowiedzi.** Kody i pola są opisane w [Osiągnięciach](../creators/achievements.md) i [Lobby](../creators/lobbies.md).
5. **Przetestuj bez sieci.** Odłącz internet, odblokuj osiągnięcie, włącz sieć. Gra powinna nie wywalić się, a wpis trafić do serwera.

## Co dostaje gra z klienta

| Zmienna | Przykład | Znaczenie |
|---|---|---|
| `SGS_API_URL` | `http://127.0.0.1:51234` | Adres lokalnego mostu |
| `SGS_GAME_ID` | `12` | Id gry w SGS |
| `SGS_ACHIEVEMENTS_TOKEN` | `3f9a…` | Token na jedno uruchomienie, w nagłówku `X-SGS-Token` |
| `SGS_LOBBY_CODE` | `K7MQ2X` | Ustawiona, gdy gracz dołączył do lobby; połącz się z tym lobby przy starcie |

## Zasady

- **Gra działa na komputerze gracza.** Wszystko, co wysyła gra, może być sfałszowane. Osiągnięcia traktuj jako element kosmetyczny, nie jako zabezpieczenie nagród o wartości pieniężnej.
- **Nie zmieniaj kluczy osiągnięć** po publikacji. Klucz to tożsamość osiągnięcia.
- **Nie blokuj gry**, gdy most nie odpowiada. Wywołania rób asynchronicznie lub w tle.
- **Traktuj `202` jako sukces.** Oznacza przyjęcie do kolejki offline.

## Dokumentacja

- [Most lokalny](bridge.md): zmienne, nagłówki, kolejka offline, kody odpowiedzi
- [Referencja API](api-reference.md): wszystkie endpointy według obszaru
- [Osiągnięcia](../creators/achievements.md): definicje, endpointy, przykłady w Unity, Godot i Unreal
- [Lobby i zaproszenia](../creators/lobbies.md): hostowanie, dołączanie, zaproszenia
- [Schemat manifestu](https://github.com/M455YN/SGSClient/blob/master/schemas/sgs-manifest.v1.json)
