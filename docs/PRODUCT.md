# What Mubsir is

Mubsir (مُبصِر) is a free Windows app that lets a blind or low-vision person use a whole PC by voice, in Arabic, English or both in one sentence. It reads anything, does what they ask on the PC, and tells them what happened. It runs alongside the NVDA screen reader and doesn't replace it.

It's made only for people who can't see the screen or see it with difficulty. The first conversation adapts to which.

## How someone uses it

They hold **Right Ctrl**, say what they want and let go. Pressing **Ctrl** stops it at once. Nothing else has to be learned, though keys exist for those who want them.

## What it can do

**Read anything**
- Books and documents in PDF, Word and text, including long ones.
- Normal pages are read directly. Scanned pages go through OCR on the PC. Broken pages or diagrams go to the AI as a picture.
- Phonetic writing, maths and Greek are said by name. Tashkeel in the text goes to the voice as written.
- It keeps the exact place by what was actually heard, and the place survives renaming or moving the file.
- Next page, go to a page, where am I, notes, bookmarks, sleep timer, reading journal, and questions answered from the book with the page number.

**Find things**
- It searches the books on the PC by name or by subject in both languages, even when the file name is useless. With the person's OK, the AI writes a short description of each book once. Short files, and anything that looks like a bill, statement or ID, are skipped.
- Google and YouTube results are read as a short numbered list. They say "رقم واحد" and it opens.

**Work the PC like a person**
- Opens apps, sites and files. Clicks, types, scrolls, reads the screen, reads messages, manages files, reads what was copied, and waits for things to load.
- If a button can't be reached normally, it looks at the screen and clicks it.
- It can undo a file move, rename or copy.
- Settings can be changed by voice, including speed and the Arabic voice. "صرفت كام" says the month's spending.

**Teach**
- "علمني Excel" teaches one step at a time and checks each step by reading the screen. It can't click or type in this mode. A blind learner is taught keys and what they'll hear. For low vision, the pointer also moves to the right place.

## Voices

| | Voice | Cost |
|---|---|---|
| **Arabic** | Fish Audio. The Arabic narrator by default, and they can hear and pick from 6 by saying "غير الصوت". | Free while Fish offers `s2.1-pro-free`, which may end soon. After that about $0.50 for 10 pages on a paid account. Audio already heard is saved. |
| **English** | Ryan, a free voice that runs on the PC, at 90% of his normal speed. It downloads once in the background (115 MB). | Free |
| **Backup for both** | The Windows or NVDA voice. | Free |

Any of them can be turned faster or slower. If Fish fails or runs out of credit, or Ryan is missing or too slow for the PC, the Windows voice takes over and they're told why.

Fish reads a page one sentence at a time, only as it's reached, and the next sentence gets ready while the current one plays. Stopping stops the spending, apart from at most one sentence that was getting ready.

## Keys and cost

The app is free. There's no account or server of ours. Keys are encrypted for the Windows account:

- **Google Gemini**: the brain and hearing. Required, and free within Google's limits. Free Whisper was tested for hearing and got 39% to 72% of Arabic words wrong against Gemini's 7%, so it isn't used.
- **Fish Audio**: the Arabic voice. Optional, and free for now.
- **Claude** (Haiku 5.5): an optional paid brain, used instead of Gemini when its key is there.

Free requests may be used by Google and Fish to improve their products. If the free limits run out, Mubsir says so. It's not suited to anything private until paid keys are added, and the owner should decide that before daily use.

## First start

1. Setup talks to them. Enter opens the key page. They copy a key, and it's picked up, tested and they hear whether it works. Creating an account on those sites is the hard part, and many people will need help once.
2. They hear the privacy summary and press Enter.
3. A short conversation by voice or buttons: language, what they want most, subject, whether to describe books, speech speed, name. If NVDA is running it never asks whether they can see.
4. Mubsir tells them what to try first.

## Safety

- One key stops anything. Win+Shift+Esc stops everything.
- Deleting, paying, installing, restarting and signing out need a real key press. A spoken yes isn't enough, so a video playing can't agree.
- On bank and payment pages the AI turns off, and the person presses Pay themselves.
- Passwords, card numbers and codes are never sent to the AI or typed by it.
- Text on a page or in a document is never treated as an instruction. Scripts never run. Security and account settings are left to the person.
- Private mode keeps everything on the PC.

## When things go wrong

The person must always hear what happened and what to do. Silence counts as a bug. `docs/WINDOWS-TEST.md` lists every failure and what they should hear.

## What it needs

64-bit Windows 10 (22H2) or Windows 11, a microphone, and about 4 GB of RAM. Understanding speech needs internet. With no internet, books already readable keep going in a free voice, and keyboard commands still work.

The notch at the top of the screen and Hudhud the hoopoe let anyone watching see what's going on. The person never needs them, and they can be turned off.

## Not built yet

- Adding tashkeel to hard Arabic words, and saving pronunciation corrections. This waits on `scripts/voice-test.py`.
- Dragging with the mouse.
- Hearing speech with no internet.
- Parental controls, and a hash check on the installer.
