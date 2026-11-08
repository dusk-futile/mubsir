# Changelog

## 0.3.0 (unreleased)

**You choose each service.** Brain: Gemini or Claude Haiku. Hearing: Gemini or Fish Audio. Arabic voice: the
Windows voice or Fish Audio. Only the chosen service needs a key, and one Gemini key covers the brain and
the hearing.

**Hearing what happened.** Each step is said as it starts and "Done" (تم) is said when it has really
finished, so you don't need to see the checklist. A step that fails plays the error sound.

**The notch**
- The light around it lives: blue and moving with your voice while listening, warm and moving with
  Mubsir's voice while speaking, a slow spectrum while thinking, and amber turning green as a task's steps
  finish, with a pulse at each one.
- Thinking is a small orb of drifting colour, without words.
- Shorter words everywhere: "Step 2", and "Book open · Page 34 of 312" while a book is read aloud (now shown
  by the real app, not only the demo).
- The payment screens are gone.

**Setup** is dark, like the notch, and built from Windows' own controls.

## 0.2.0 (2026-11-08)

**Free, with your own keys.** No account, server or relay. Gemini is the free brain and hearing; Claude
Haiku is used instead when you add a Claude key.

**Speech**
- Arabic and English mixed in one sentence switch voice at the word, with a short natural pause.
- Numbers are read in the language of their label: "chapter 3" stays English, "صفحة 12" stays Arabic.
- Square roots, Greek letters, maths signs, arrows, section and paragraph marks, copyright and check
  marks are named instead of skipped.
- Every sentence has the same short pause after it, the same loudness, and no clicks.
- The next two sentences are prepared while one plays.
- New option, off by default: one voice for a whole answer that has Arabic.

**Removed**
- Hudhud, the animated companion, and the on-screen pointer. The notch is the only visual.
- Claude Sonnet. Claude always means Haiku.

**Repo**
- `scripts/package.ps1` builds the release zip, with a plain-text `START-HERE.txt` inside.
- Warnings are errors, and unused code is a warning.

## 0.1.0

First preview.
