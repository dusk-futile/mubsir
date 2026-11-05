# Prototypes

Design sketches that can be opened in any browser. They are for direction only; the real app is
native Windows (WPF), and its motion tokens come from the approved prototype.

| Prototype | What it shows | How to view |
| --- | --- | --- |
| `hudhud-companion/` | Normal mode: the notch at the top and Hudhud (paper-cut hoopoe) in the bottom-right corner, on springs, with idle, listening, thinking, doing (Ctrl stops and undoes), pointing, speaking, reading, payment (4× Ctrl) and sleeping states. Clicking Hudhud opens a small Mubsir panel. `#state` in the link opens one state | Open `hudhud-companion/index.html` in a browser, or the owner's private published link (see `docs/HANDOFF.md`) |

Concept images for the Hudhud redesign (2026-11-04) are in `hudhud-companion/assets/concepts/`.

Hudhud is a cutout puppet. Its parts are PNGs drawn by ChatGPT in `assets/hudhud/` at the repo root (the Windows overlay uses the same files),
and `rig.json` there gives each part's joint, place and resting angle. After changing a part or the rig,
run `python3 prototypes/hudhud-companion/build.py`: it bakes the parts into `index.html` between the
`hudhud:start` and `hudhud:end` markers, so the page stays a single file.

`index.html` files here are page bodies without `<html>`/`<head>` tags (the publishing tool adds
them); browsers open them fine as they are.
