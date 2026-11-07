# Changelog

## 0.2.0 (unreleased)

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
