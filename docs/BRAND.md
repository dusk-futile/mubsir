# Mubsir identity

Status (2026-11-04, design session): two parts, both chosen by the owner.

- **App icon and logo: the seeing eye.** An amber eye on deep navy. Its iris is sound waves (Mubsir
  listens and speaks), and its lower lid sweeps into the tail of the Arabic letter **م**, the first
  letter of مُبصِر. Done: files in `assets/brand/`, used by `Mubsir.exe`, the tray and the README.
- **No mascot.** The Hudhud companion was removed on 2026-11-07. The notch at the top of the screen is
  the one visual, in the same Night, Amber and Cream.

The name stays **Mubsir · مُبصِر** ("the one who sees"). Most important for blind users, the identity
is also **a sound** (below).

## App icon

| File (`assets/brand/`) | Use |
| --- | --- |
| `mubsir-icon.svg` | Master, full detail. 64 px and up: README, store, website, splash |
| `mubsir-icon-medium.svg` | 24–48 px: one sound ring, slightly heavier lines |
| `mubsir-icon-small.svg` | 16–20 px (tray at 100–125 % scaling): solid pupil, heavy lines |
| `mubsir-mark.svg` | The mark without its tile, for dark surfaces such as the companion panel |
| `mubsir.ico` | Windows icon, 16–256 px, each size from the right drawing. `Mubsir.App.csproj` embeds it |
| `png/mubsir-<size>.png` | 16–1024 px exports |

After editing an SVG, run `python scripts/build-icons.py` (needs Pillow and Chrome or Edge) to
rebuild the PNGs and the `.ico`. Never edit the PNGs or the `.ico` by hand.

| Colour | Hex | Use |
| --- | --- | --- |
| Night | `#080D3F` | Icon tile, dark surfaces |
| Amber | `#F3C343` | The mark, highlights. 11.1 : 1 on Night |
| Cream | `#FFF4DE` | Text on Night. 16.9 : 1 |
| Crest | `#E2703A` | Warnings and "Stopped" in the notch. 5.8 : 1 on Night |

Rules: keep the mark amber on Night; don't stretch, rotate or outline it; no text inside the tile;
use the small drawing at 16–20 px. On a dark taskbar the tile blends in and the amber eye carries the
icon, which is intended. Next to the written name the logo is decorative (`alt=""`); alone it is
"Mubsir".

## Directions explored on 2026-11-04

A lantern (Fanoos), a hoopoe (Hudhud) and an abstract echo (Sada) were explored. Hudhud was built as an
animated companion and removed on 2026-11-07: it gave a blind user nothing, and the notch already shows
anyone watching what Mubsir is doing.

## Sound identity (all directions)

For blind users the sounds *are* the brand. One family of short, warm sounds, recognisable in half a
second, never harsh:

| Moment | Sound |
| --- | --- |
| Start-up / "Mubsir is ready" | Signature 3-note motif (≈ 0.8 s) |
| Listening | Soft rising two notes |
| Got it / thinking | One short low note |
| Done | Gentle two-note resolve |
| Check-in "continue?" | The listening sound, softer |
| Secure entry (passwords) | A distinct "lock" sound, used nowhere else |
| Level 3 confirm (payments) | A distinct two-tone question, used nowhere else |
| Error / offline | Low muted note, never a buzzer |

All sounds are original, made for Mubsir, under 1 second, mixed so they never cover NVDA or the voice.
The current code's `EarconTones` is where they plug in.

## Voice persona

Warm, calm, short sentences, never chatty during tasks. Speaks the user's dialect back to them. Says
"I" as Mubsir. Never pretends to be human.

## Next step

Done: logo and app icon at all sizes, the one-page brand sheet (`assets/brand/brand-sheet.png`) and the
GitHub social preview (`assets/brand/social-preview.png`).

The notch is native WPF in `src/Mubsir.Overlay`, moving on the springs in `motion-tokens.json`.

Still to do: the sound set, and Readex Pro in the native overlay (it uses Segoe UI for now).
