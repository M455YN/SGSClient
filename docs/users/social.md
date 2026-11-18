# Znajomi, czat i profil

Wszystkie funkcje społeczne wymagają zalogowania.

## Profil

W **Profilu** ustawiasz:

- **Nazwę wyświetlaną** i **Zdjęcie profilowe**
- **Tag gracza**: unikalny identyfikator, po którym inni Cię znajdą (`@tag`). Wybierasz go raz (**Zapisz tag**).
- **Biografię** i **Lokalizację**
- **Ścianę**: wpisy od znajomych (**Napisz coś na swojej ścianie…**)
- **Osiągnięcia** i **Ostatnio używane gry**, czyli co grasz i co odblokowałeś

Profil pokazuje też listy **Znajomi**, **Obserwowani** i **Obserwujący**.

## Prywatność

W **Profil → Dostosuj profil → Prywatność** możesz ukryć swoje osiągnięcia przed innymi (**Ukryj moje osiągnięcia przed innymi graczami**). Ukryta lista jest pusta dla wszystkich poza Tobą. Zagregowana rzadkość osiągnięć pozostaje anonimowa.

## Znajomi

W **Znajomi** wyślesz zaproszenie po tagu, a osoba z zaproszeniem zobaczy je w zakładce **Do Ciebie** i może je **Akceptować**. Od tej chwili widzicie się na liście, możecie pisać i zapraszać się do gier.

## Czat

Wiadomości piszesz z listy **Znajomi** (zakładka **Czat**). Rozmowy są jednoosobowe, a lista pokazuje wszystkie Twoje rozmowy.

## Powiadomienia

Klient wysyła powiadomienia o trzech rzeczach: prośbie o znajomość, zaproszeniu do lobby i odblokowanym osiągnięciu. Liczba nieprzeczytanych jest widoczna w klienta, a powiadomienia możesz oznaczyć jako przeczytane.

## Zaproszenia do gry (lobby)

Jeśli twórca gry obsługuje lobby, znajomy może zaprosić Cię do wspólnej sesji. Zaproszenie przychodzi jako powiadomienie, a kliknięcie uruchamia grę i łączy Cię z hostem. Szczegóły od strony twórcy: [Lobby i zaproszenia](../creators/lobbies.md).

Otwarte lobby z wolnym miejscem widzisz też przy znajomym na liście **Znajomi**.

SGS tylko zbiera graczy, a łączy ich sama gra. Jeśli gra nie używa relaya, połączenie z hostem działa w tej samej sieci, przez wspólny VPN (np. Tailscale, ZeroTier, Radmin VPN) albo gdy host ma otwarty port. Gry z relayem (np. na Unity Relay czy Steamie) działają przez internet bez tego.

## Graj ze znajomymi

Jeśli gra to obsługuje, na jej stronie w bibliotece jest przycisk **Graj ze znajomymi**.

1. Kliknij go. Klient otworzy lobby z listą graczy.
2. Zaproś znajomych, którzy mają grę. Dołączą przez zaproszenie albo **Dołącz** na liście znajomych. Możesz też podać komuś kod lobby: wpisuje go w polu **Kod lobby** w zakładce **Znajomi**. W czasie oczekiwania pasek boczny pokazuje **W lobby**.
3. Każdy gość klika **Gotowy**. Host może wyrzucić gracza przyciskiem ✕ przy jego nazwie.
4. Gdy wszyscy są gotowi, host klika **Start**. Gra uruchamia się u każdego i łączy graczy sama.

Po meczu wszyscy wracają do lobby w tym samym składzie, więc kolejną rundę zaczyna się bez ponownego zapraszania. Jeśli host wyjdzie, gospodarzem zostaje kolejny gracz.
