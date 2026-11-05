<div align="center">

<img src="assets/brand/mubsir-icon.svg" width="112" alt="">

# Mubsir · مُبصِر

### Your PC, by voice.

**A professional AI agent that lives on your Windows PC.**<br>
Hold one key and say what you want, in Arabic, in English, or both in one sentence.<br>
Mubsir reads it, explains it and gets it done, and tells you exactly what it did.

[![Release](https://img.shields.io/github/v/release/dusk-futile/mubsir?include_prereleases&label=release)](https://github.com/dusk-futile/mubsir/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![Windows 10 | 11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6)
![Works with NVDA](https://img.shields.io/badge/works%20with-NVDA-4B2E83)

</div>

---

Most AI assistants live in a chat box and tell you what to click. **Mubsir does the clicking.** It sees what's on your screen, works your apps, reads your documents and answers out loud. You never have to look away from what you're doing. You don't have to look at all.

> ```
> "افتح جوجل وابحث عن مواعيد الصلاة"
> "الكتاب ده بيتكلم عن إيه؟"
> "open a new window with the same page"
> "مين بعتلي على واتساب؟"
> ```

## Why it's different

- **It acts, it doesn't just advise.** Opens apps and sites, searches, scrolls, fills in fields, plays YouTube, manages files.
- **It reads what's really there.** Web pages, Word, PDFs and scanned Arabic books, plus apps that other tools struggle with.
- **It speaks your language, properly.** Egyptian, Gulf, Levantine, Libyan, Maghrebi and MSA, with English mixed in mid-sentence. It switches voice as it goes, and handles math, symbols, emoji and Arabizi (`7abibi`).
- **It works with your eyes closed.** Everything it does is spoken, with no window you need to look at. It runs alongside the [NVDA](https://www.nvaccess.org/) screen reader.
- **You stay in control.** One key stops anything, instantly. Sending, paying and deleting need your OK. On bank pages the AI switches off, and you press Pay yourself.
- **Your screen stays yours.** Nothing leaves your PC until you ask. Passwords, card numbers and one-time codes never do.

## What's coming

<img src="assets/brand/readme-preview.jpg" alt="Preview: a black notch at the top of a Windows desktop shows a checklist, with finished steps ticked, while a paper-cut hoopoe stands in the bottom-right corner.">

*A preview of what's in development: a notch at the top of your screen that shows what Mubsir is doing, and Hudhud, the hoopoe who keeps you company in the corner.*

- **The notch and Hudhud**, with a live checklist and a pointer that flies to the exact button.
- **Your whole library by voice.** "Give me the book on phonology", even if the file has no name, and pick up tomorrow on the exact line you stopped at.
- **No keys and no accounts.** Download it and start talking.

## Getting started

**Requirements:** 64-bit Windows 10 (22H2) or Windows 11, a microphone, and two API keys: [Anthropic](https://console.anthropic.com/) for the assistant and [Google AI Studio](https://aistudio.google.com/apikey) for speech recognition. Typical use costs a few dollars a month.

1. Download the latest zip from [Releases](https://github.com/dusk-futile/mubsir/releases) and extract it.
2. Right-click `setup.ps1` → **Run with PowerShell**, as administrator. It installs NVDA if needed, adds the Mubsir Bridge add-on and Arabic speech, and starts Mubsir when you sign in.
3. Add your keys. They are stored encrypted for your Windows account only:

   ```powershell
   $mubsir = "$env:LOCALAPPDATA\Mubsir\current\Mubsir.exe"
   & $mubsir --set-key anthropic YOUR_ANTHROPIC_KEY
   & $mubsir --set-key gemini YOUR_GEMINI_KEY
   ```

4. Sign out and back in. You'll hear "Mubsir is ready."

Without keys, Mubsir still reads the screen and runs OCR locally.

## Using it

| Keys | What happens |
| --- | --- |
| Hold **Right Ctrl** | Talk. Release when you're done. |
| **Ctrl** | Stop speaking (same as NVDA). |
| Double-tap **Right Ctrl**, then **Space** | Read from here |
| … then **← / →** | Previous / next sentence |
| … then **↑ / ↓** | Previous / next paragraph |
| … then **.** | Repeat |
| … then **Enter** | Type a request instead of speaking |
| … then **F1** | Help |
| **Right Ctrl** or **Esc** during a task | Stop it |
| **Win + Shift + Esc** | Stop everything, from anywhere |

Things you can say:

```
اقرأ لي الشاشة
افتح جوجل وابحث عن مواعيد الصلاة
مين بعتلي على واتساب؟
الكتاب ده بيتكلم عن إيه؟
scroll down and read the next part
open a new window with the same page
```

## Privacy

- Mubsir only sends something to the cloud when you ask for it: your spoken request plus the name of the app you are in (to Google for transcription) and the text on screen that the request needs (to Anthropic).
- Passwords, card numbers and one-time codes are never sent. Bank and payment pages are handled locally.
- Say "private mode on" to keep everything on your PC.
- Logs contain timings and error codes only, never what you said or read.

## Building from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
git clone https://github.com/dusk-futile/mubsir.git
cd mubsir
dotnet build Mubsir.slnx
dotnet publish src/Mubsir.App -c Release -r win-x64 -o publish
python scripts/build-addon.py
```

The core library (`src/Mubsir.Core`) builds on any OS, and `dotnet test tests/Mubsir.Core.Tests` runs its tests. The app itself (`src/Mubsir.App`) runs on Windows only.

On Windows, `Mubsir.exe --overlay-demo` shows the normal-mode notch and Hudhud on their own. It listens to, sends and clicks nothing, and Esc quits.

## License

[MIT](LICENSE). The NVDA add-on in `nvda-addon/` is GPL-2.0, like NVDA itself, and NVDA's controller client is LGPL-2.1.
