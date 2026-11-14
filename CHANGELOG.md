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
- Thinking is Mubsir's signature: a stroke shaped like the letter م writes itself right to left with a
  haloed pen tip, then lifts away like a pen moving on. The ink stays Mubsir's amber into coral, with a warm
  light under it; warm sparks lift off the tip, and the faint outline of the letter breathes. A light sweeps
  across the word beside it.
- Shorter words everywhere: "Step 2", and "Book open · Page 34 of 312" while a book is read aloud (now shown
  by the real app, not only the demo).
- The payment screens are gone.

**Setup** is dark, like the notch, and built from Windows' own controls.

**Reading**
- Go to a part of the page: "go to the fourth line", "السطر الرابع", "the last sentence", "paragraph 3".
- "Read the heading" says the first line of the page. "Read the ending" reads its last sentences.
- Speed by percent: "20% faster", "أبطأ عشرة في المية".
- "Which book has the phrase ...": Mubsir searches the text of every book on the PC, behind, and says the
  book and page when it's done. Say the number to open it there.

**Memory**
- "What did we do yesterday?" and "when did I ask about the invoice?" are answered from the chats saved
  on the PC.
- New option, off by default: saved chats encrypted for your Windows account.

**Yours**
- Give it a name: "your name is Jarvis", "اسمك محمد", or in settings.
- The brain can be any service that speaks the OpenAI chat format, by its address: OpenRouter, a router
  such as OmniRoute, or a model on your PC.
- A new PC with little memory starts light: no notch, and the Windows voice for English.

**Cheaper**
- Only the newest screen snapshot is kept in a task. Older ones become one line.

**Repo**
- 41 fewer files: small source files merged into the code that uses them (31 fewer), and one PNG icon
  export instead of eleven. No behaviour change.
- Tests that run whole tasks through the agent loop and judge them only by what is heard.

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
