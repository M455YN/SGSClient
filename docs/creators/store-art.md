# Store art / Grafika sklepowa

Three separate images per game. Thumbnail, landscape header, and square logo are **different files** — do not upload the same frame to every slot.

Trzy osobne obrazy na grę. Miniaturka, okładka pozioma i kwadratowe logo to **różne pliki** — nie wgrywaj tego samego kadru do wszystkich slotów.

The client **does not validate pixels**, only format and file size (max 80 MB). Sizes below are recommendations so art does not crop badly on tiles and hero.

Klient **nie waliduje pikseli**, tylko format i rozmiar pliku (max 80 MB). Poniższe wymiary to rekomendacja, żeby grafika nie rozjeżdżała się na kafelkach i hero.

## Formats / Formaty

| Asset | Manifest | Allowed / Dozwolone |
|-------|----------|---------------------|
| Thumbnail / Miniaturka | `media.cover` | `.png`, `.jpg`, `.jpeg`, `.webp`, `.gif` |
| Landscape header / Okładka pozioma | `media.header` | same / to samo |
| Square logo / Logo kwadratowe | `media.logo` | same / to samo |
| Intro | `media.intro` | `.mp4`, `.webm` |
| Screenshots / Zrzuty | `media.screenshots` | images, max 12 / obrazy, max 12 |

Prefer **PNG** (or WebP) over heavy JPEG compression.

Preferuj **PNG** (lub WebP) bez mocnej kompresji JPG.

## Dimensions / Wymiary

| Asset | Source (recommended) / Źródło (zalecane) | Minimum | Aspect / Proporcje | Where / Gdzie |
|-------|------------------------------------------|---------|---------------------|---------------|
| **Thumbnail / Miniaturka** | **600 × 900** | 300 × 450 | 2:3 portrait / pion | library, store lists, 168×248 posters |
| **Landscape / Okładka pozioma** | **920 × 430** | 460 × 215 | ~2.14:1 (Steam header) | title page, hero, featured |
| **Logo** | **512 × 512** | 256 × 256 | 1:1 square / kwadrat | sidebar installed list, small square tiles |
| **Screen** | **1920 × 1080** | 1280 × 720 | 16:9 | title-page gallery / galeria na stronie gry |

2× landscape: **1840 × 860**. 2× thumbnail: **1200 × 1800**. 2× logo: **1024 × 1024**.

### Thumbnail / Miniaturka

Portrait capsule (Steam library-style). Keep a safe margin — tiles crop with `UniformToFill`.

Pionowa kapsuła. Zostaw bezpieczny margines — kafelek przycina obraz.

Do not paint the game title into the image; the caption sits under the tile.

Nie wstawiaj tytułu gry w grafikę: podpis jest pod kafelkiem.

A square (e.g. 512 × 512) works but crops hard on a 168×248 poster — use the dedicated logo slot for squares.

Kwadrat zadziała, ale na plakacie 168×248 dostanie mocne kadrowanie — do kwadratów użyj slotu logo.

### Landscape header / Okładka pozioma

Wide Steam-style header (460 × 215 and multiples). Keep title and key art toward the center / left — the bottom is covered by a gradient and buttons.

Szeroki header. Tytuł i kluczowe elementy bliżej środka / lewej — dół zasłania gradient i przyciski.

Without a header the client shows the thumbnail (looks wrong on hero). Without a thumbnail, the header is used on lists (also poor). Ship both.

Bez headera klient pokazuje miniaturkę (źle na hero). Bez miniaturki — header na listach. Wrzuć oba.

### Game logo / Logo gry

Transparent PNG preferred, about **512 × 512**, no extra padding. Used on small square tiles (sidebar). It does not replace thumbnail or header — without a logo the client falls back to the thumbnail.

Preferuj PNG z przezroczystością, ok. **512 × 512**, bez zbędnego paddingu. Trafia na małe kwadratowe kafelki (sidebar). Nie zastępuje miniaturki ani headera — bez logo klient używa miniaturki.

## File layout / Gdzie wrzucić pliki

```text
media/cover.png      ← thumbnail / miniaturka
media/header.png     ← landscape / okładka pozioma
media/logo.png       ← square logo / logo kwadratowe
media/intro.mp4      ← optional / opcjonalnie
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

Or after publish: **Creator panel** / **Admin** → **Media** → separate **Thumbnail**, **Landscape header**, and **Square logo** slots. Empty slots do not show a fallback in the media editor — you see what is missing.

Albo po publikacji: **Panel twórcy** / **Admin** → **Multimedia** → sloty „Miniaturka”, „Okładka pozioma” i „Logo (kwadratowe)”. Puste sloty w edytorze nie pokazują fallbacku.
