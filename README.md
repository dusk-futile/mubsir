<div align="center">

# Mubsir · مُبصِر

**An AI screen assistant for blind and low-vision Windows users.**<br>
Hold a key, ask in Arabic or English, and Mubsir reads, explains and does things on your PC.

[![Release](https://img.shields.io/github/v/release/dusk-futile/mubsir?include_prereleases&label=release)](https://github.com/dusk-futile/mubsir/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![Windows 10 | 11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6)
![Works with NVDA](https://img.shields.io/badge/works%20with-NVDA-4B2E83)

</div>

---

Mubsir runs in the background next to the [NVDA](https://www.nvaccess.org/) screen reader. It has no window to look at; you talk to it or use a couple of keys. It is built for people who switch between Arabic and English all day, and for older PCs with 4 GB of RAM.

## What it does

- **Reads anything on screen.** Web pages, Word, PDFs (including scanned Arabic books, through OCR), and apps that NVDA struggles with.
- **Answers questions about it.** "What is this page about?", "Who sent me this email?", "What's in this picture?"
- **Does things for you.** Opens apps and sites, searches Google, scrolls, fills in fields, plays YouTube, manages files.
- **Speaks Arabic and English properly.** Switches voice mid-sentence, reads math and symbols, emoji and Arabizi (`7abibi`).
- **Understands dialects.** Egyptian, Gulf, Levantine, Libyan, Maghrebi and MSA.
- **Asks before anything you can't undo.** Sending, paying and deleting need one Enter press. On bank pages the AI switches off and you press Pay yourself.

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

The core library (`src/Mubsir.Core`) builds on any OS. The app itself (`src/Mubsir.App`) runs on Windows only.

## License

[MIT](LICENSE). The NVDA add-on in `nvda-addon/` is GPL-2.0, like NVDA itself, and NVDA's controller client is LGPL-2.1.
