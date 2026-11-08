# Changelog

## 0.3.0 (unreleased)

**You choose each service.** Setup shows the recommended free setup (one Gemini key), and "Choose other
services and models" opens the full choice: brain (Gemini 3.5 Flash-Lite, Gemini 3.5 Flash or Claude Haiku
5.5), hearing (Gemini or Fish Audio) and Arabic voice (Windows or Fish Audio), with the recommended ones
marked. Only the chosen services need keys.

**Hearing what happened.** Each step is said as it starts ("Opening Settings") and again when it has really
finished ("Opened Settings", "فتحت الإعدادات"), so you don't need to see the checklist. A step that fails
plays the error sound.

**The notch**
- The light around it lives: blue and moving with your voice while listening, warm and moving with
  Mubsir's voice while speaking, a slow spectrum while thinking, and amber turning green as a task's steps
  finish, with a pulse at each one.
- Thinking is a crisp sparkle with colour flowing through it and a light sweeping across the word.
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
