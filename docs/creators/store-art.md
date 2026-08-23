# Store art / Grafika sklepowa

Two separate images per game. Thumbnail and landscape header are **different files** — do not upload the same frame to both slots.

Dwa osobne obrazy na grę. Miniaturka i okładka pozioma to **różne pliki** — nie wgrywaj tego samego kadru do obu slotów.

The client **does not validate pixels**, only format and file size (max 80 MB). Sizes below are recommendations so art does not crop badly on tiles and hero.

Klient **nie waliduje pikseli**, tylko format i rozmiar pliku (max 80 MB). Poniższe wymiary to rekomendacja, żeby grafika nie rozjeżdżała się na kafelkach i hero.

## Formats / Formaty

| Asset | Manifest | Allowed / Dozwolone |
|-------|----------|---------------------|
| Thumbnail / Miniaturka | `media.cover` | `.png`, `.jpg`, `.jpeg`, `.webp`, `.gif` |
| Landscape header / Okładka pozioma | `media.header` | same / to samo |
| Intro | `media.intro` | `.mp4`, `.webm` |
| Screenshots / Zrzuty | `media.screenshots` | images, max 12 / obrazy, max 12 |

Prefer **PNG** (or WebP) over heavy JPEG compression.

Preferuj **PNG** (lub WebP) bez mocnej kompresji JPG.

## Dimensions / Wymiary

| Asset | Source (recommended) / Źródło (zalecane) | Minimum | Aspect / Proporcje | Where / Gdzie |
|-------|------------------------------------------|---------|---------------------|---------------|
| **Thumbnail / Miniaturka** | **600 × 900** | 300 × 450 | 2:3 portrait / pion | library, sidebar, 168×248 posters |
| **Landscape / Okładka pozioma** | **920 × 430** | 460 × 215 | ~2.14:1 (Steam header) | title page, hero, featured, store grid |
| **Logo** | **640 × 360** | 320 × 180 | wide, no background / szerokie, bez tła | *slot not shipped yet / slot jeszcze nie istnieje* |
| **Screen** | **1920 × 1080** | 1280 × 720 | 16:9 | title-page gallery / galeria na stronie gry |

2× landscape: **1840 × 860**. 2× thumbnail: **1200 × 1800**.

### Thumbnail / Miniaturka

Portrait capsule (Steam library-style). Keep a safe margin — tiles crop with `UniformToFill`.

Pionowa kapsuła. Zostaw bezpieczny margines — kafelek przycina obraz.

Do not paint the game title into the image; the caption sits under the tile.

Nie wstawiaj tytułu gry w grafikę: podpis jest pod kafelkiem.

A square (e.g. 512 × 512) works but crops hard on a 168×248 poster.

Kwadrat zadziała, ale na plakacie 168×248 dostanie mocne kadrowanie.

### Landscape header / Okładka pozioma

Wide Steam-style header (460 × 215 and multiples). Keep title and key art toward the center / left — the bottom is covered by a gradient and buttons.

Szeroki header. Tytuł i kluczowe elementy bliżej środka / lewej — dół zasłania gradient i przyciski.

Without a header the client shows the thumbnail (looks wrong on hero). Without a thumbnail, the header is used on lists (also poor). Ship both.

Bez headera klient pokazuje miniaturkę (źle na hero). Bez miniaturki — header na listach. Wrzuć oba.

### Game logo / Logo gry

There is **no** `media.logo` field yet. Planned: transparent PNG, about **640 × 360**, no extra padding. It does not replace thumbnail or header.

Osobnego pola `media.logo` **jeszcze nie ma**. Plan: PNG z przezroczystością, ok. **640 × 360**. Nie zastępuje miniaturki ani headera.

Do not add `logo` to the manifest until the slot exists — the server rejects unknown `media` keys.

Do tego czasu nie dodawaj `logo` do manifestu — serwer odrzuci nieznane pola w `media`.

## File layout / Gdzie wrzucić pliki

```text
media/cover.png      ← thumbnail / miniaturka
media/header.png     ← landscape / okładka pozioma
media/intro.mp4      ← optional / opcjonalnie
media/screen-01.png
```

```json
"media": {
  "cover": "media/cover.png",
  "header": "media/header.png",
  "screenshots": ["media/screen-01.png"]
}
```

Or after publish: **Creator panel** / **Admin** → **Media** → separate **Thumbnail** and **Landscape header** slots. Empty slots do not show a fallback — you see what is missing.

Albo po publikacji: **Panel twórcy** / **Admin** → **Multimedia** → sloty „Miniaturka” i „Okładka pozioma”. Puste sloty nie pokazują fallbacku.
