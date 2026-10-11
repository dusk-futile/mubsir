# Basira Bridge (NVDA add-on)

Companion add-on for Basira. Requires NVDA 2026.1 or later (tested on 2026.2). License: GPL-2.0, see `COPYING`.

## What it does

1. **Arabic/English voice switching inside mixed text.** NVDA only changes voice for text that is tagged with a language. This add-on tags it: when a speech sequence mixes Arabic with Latin letters and carries no language tags yet, it is split into runs (`globalPlugins/basiraBridge/segment.py`, the same rules as Basira's own segmenter) and a language change is inserted before each run. NVDA's automatic language switching then picks the installed voice, for example Hoda or Naayf for Arabic and an English OneCore voice for English.
   - Needs **Automatic language switching** on (NVDA settings, Speech). Basira's setup script turns it on.
   - Sequences that already have language tags (web pages that declare a language, Basira's own SSML) are left alone.
2. **Quiet mode.** While Basira carries out a step (for example opening a window), NVDA would announce every focus change on top of Basira's narration. Basira writes `%LOCALAPPDATA%\Basira\nvda-quiet.json` containing `{"until": <Unix time in ms>}`. Until then, speech produced by focus, focus-entered and foreground events is dropped. Focus tracking, browse mode, braille and vision are not touched; only the announcement is silenced.
   - The file is read at most every 250 ms.
   - A quiet window never lasts more than 60 seconds after the file was last written, so a stale file cannot silence NVDA.
   - Basira should write the file atomically (write a temporary file, then replace) and retry if the replace hits a sharing violation.

## Install

Build the package with `python3 scripts/build-addon.py` (from the repository root). It produces `dist/basiraBridge-<version>.nvda-addon`. Copy it to the PC and open it (Enter in File Explorer) while NVDA is running; NVDA asks for confirmation and then restarts.

## Development

- Tests: `python3 -m pytest nvda-addon/tests` (no NVDA needed; NVDA modules are faked).
- `segment.py` and `quiet.py` must not import NVDA modules.
- Keep the code compatible with Python 3.9 (tests) and 3.13 (NVDA 2026).
