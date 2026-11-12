# Grafika sklepowa

Każda gra ma trzy osobne obrazy: miniaturkę, okładkę poziomą i kwadratowe logo. To **różne pliki**. Nie wgrywaj tego samego kadru do wszystkich slotów.

Klient **nie sprawdza pikseli**, tylko format i rozmiar pliku (maksymalnie 80 MB). Poniższe wymiary są zalecane, żeby grafika nie przycinała się źle na kafelkach i w sekcji hero.

## Formaty

| Zasób | Manifest | Dozwolone formaty |
|-------|----------|-------------------|
| Miniaturka | `media.cover` | `.png`, `.jpg`, `.jpeg`, `.webp`, `.gif` |
| Okładka pozioma | `media.header` | j.w. |
| Logo kwadratowe | `media.logo` | j.w. |
| Intro | `media.intro` | `.mp4`, `.webm` |
| Zrzuty ekranu | `media.screenshots` | obrazy, maksymalnie 12 |

Preferuj **PNG** (lub WebP). Unikaj mocnej kompresji JPG.

## Wymiary

| Zasób | Zalecane źródło | Minimum | Proporcje | Gdzie się pojawia |
|-------|-----------------|---------|-----------|-------------------|
| **Miniaturka** | **600 × 900** | 300 × 450 | 2:3, pion | biblioteka, listy w sklepie, plakaty 168×248 |
| **Okładka pozioma** | **920 × 430** | 460 × 215 | ok. 2,14:1 (nagłówek w stylu Steam) | strona gry, hero, wyróżnione |
| **Logo** | **512 × 512** | 256 × 256 | 1:1, kwadrat | lista zainstalowanych w pasku bocznym, małe kwadratowe kafelki |
| **Zrzut ekranu** | **1920 × 1080** | 1280 × 720 | 16:9 | galeria na stronie gry |

Wersje 2×: okładka pozioma **1840 × 860**, miniaturka **1200 × 1800**, logo **1024 × 1024**.

### Miniaturka

Pionowa kapsuła w stylu biblioteki Steam. Zostaw bezpieczny margines, bo kafelek przycina obraz (`UniformToFill`).

Nie wpisuj tytułu gry w grafikę. Podpis jest pod kafelkiem.

Kwadrat (np. 512 × 512) też zadziała, ale na plakacie 168×248 mocno się przytnie. Do kwadratów użyj slotu logo.

### Okładka pozioma

Szeroki nagłówek w stylu Steam (460 × 215 i wielokrotności). Trzymaj tytuł i kluczowe elementy bliżej środka lub lewej strony. Dół zasłania gradient i przyciski.

Bez nagłówka klient pokazuje miniaturkę, co wygląda źle w sekcji hero. Bez miniaturki nagłówek trafia na listy, co też słabo wygląda. Wgraj oba.

### Logo gry

Najlepiej przezroczysty PNG, ok. **512 × 512**, bez zbędnego marginesu. Używany jest na małych kwadratowych kafelkach w pasku bocznym. Nie zastępuje miniaturki ani nagłówka. Bez logo klient używa miniaturki.

## Układ plików

```text
media/cover.png      ← miniaturka
media/header.png     ← okładka pozioma
media/logo.png       ← logo kwadratowe
media/intro.mp4      ← opcjonalnie
media/screen-01.png
```

```json
"media": {
  "cover": "media/cover.png",
  "header": "media/header.png",
  "logo": "media/logo.png",
  "screenshots": ["media/screen-01.png"]
}
```

Możesz też dodać grafiki po publikacji: **Panel twórcy** (lub **Admin**) → **Multimedia** → osobne sloty **Miniaturka**, **Okładka pozioma** i **Logo (kwadratowe)**. Puste sloty w edytorze nie pokazują zastępczej grafiki, więc od razu widać, czego brakuje.
