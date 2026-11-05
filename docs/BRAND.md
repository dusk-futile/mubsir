# Mubsir identity

Status (2026-11-04, design session): two parts, both chosen by the owner.

- **App icon and logo: the seeing eye.** An amber eye on deep navy. Its iris is sound waves (Mubsir
  listens and speaks), and its lower lid sweeps into the tail of the Arabic letter **م**, the first
  letter of مُبصِر. Done: files in `assets/brand/`, used by `Mubsir.exe`, the tray and the README.
- **Companion: Hudhud (the hoopoe)**, bottom-right in normal mode, next to the notch at the top.
  The owner chose concept 4, the **layered paper-cut** hoopoe
  (`prototypes/hudhud-companion/assets/concepts/hudhud-4-paper-cut.jpg`), now an animated cutout
  puppet: ChatGPT drew each part (body, head with open and closed eyes, wing, three flight feathers,
  six crest feathers, tail, legs), and `assets/hudhud/rig.json` says
  where each part's joint is. The hand-drawn SVG version looked blocky to the owner and was replaced.

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
| Crest | `#E2703A` | Hudhud's crest only. 5.8 : 1 on Night |

Rules: keep the mark amber on Night; don't stretch, rotate or outline it; no text inside the tile;
use the small drawing at 16–20 px. On a dark taskbar the tile blends in and the amber eye carries the
icon, which is intended. Next to the written name the logo is decorative (`alt=""`); alone it is
"Mubsir".

## Directions explored on 2026-11-04 (first planning chat)

The owner chose Direction 2, Hudhud, as the companion. The others are kept for reference. Hudhud's
first palette (sand, crest orange, ink) is replaced by the icon's Night, Amber and Cream; the crest
keeps its orange.

### 1. Fanoos — the little lantern (recommended)

A small, round Ramadan lantern (فانوس) with a warm light inside. The light is Mubsir's "eye": it glows
when listening, flickers softly when thinking, pulses with the voice when speaking.

- **Why it fits:** "the one who sees" = the one who brings light. Instantly Egyptian and warm, not a
  copy of any app's mascot, and it works at every size, from an app icon to a 6 px dot in the notch.
- **Notch:** the idle dot is a tiny amber light; the pill's waveform is lantern-coloured light.
- **Palette:** deep night blue `#0E1730`, lantern amber `#F5B544`, soft cream `#FFF4DE`.
- **Character name:** Fanoos (the lantern), while the product stays Mubsir.

### 2. Hudhud — the hoopoe messenger ✅ chosen

A friendly hoopoe (هدهد) with its crest. In the Qur'an the hoopoe is the bird that saw what others
had not and brought back the news, a natural fit for an assistant that sees the screen and tells you.

- **Why it fits:** clever, curious, a messenger; strong recognition across the Arab world.
- **Notch:** the crest opens like a fan when listening; feathers ripple when speaking.
- **Palette:** sand `#E9C79A`, crest orange `#E2703A`, ink `#1C1A17`.
- **Risk:** a bird mascot must be drawn clearly differently from well-known app owls and birds.

### 3. Sada — the echo (sound-first, abstract)

No animal: a soft ring of sound waves forming an eye (صدى = echo). Clean and premium.

- **Why it fits:** the product is voice; the logo *is* sound. Best for a serious, tool-like feel.
- **Notch:** concentric rings that breathe; the speaking waveform is the logo itself.
- **Palette:** graphite `#15171C`, signal teal `#3DD6C3`, white.

### 4. Wattwat — the bat that sees with sound

A cute bat: bats find their way in the dark by sound, just like blind users with Mubsir.

- **Why it fits:** the strongest story ("see with sound").
- **Risk:** bats are not loved by everyone in the region. Test it before choosing.

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

Done since: the Hudhud concept (4, paper-cut), its cutout parts and rig, and the companion and notch
states in this style (`prototypes/hudhud-companion/`).

The owner approved the prototype's look on 2026-11-04 (version 5), and the native overlay that reuses
the rig and the motion tokens is under way in `src/Mubsir.Overlay`.

Still to do: the sound set, and Readex Pro in the native overlay (it uses Segoe UI for now).
