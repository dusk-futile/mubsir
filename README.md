<div align="center">

<img src="assets/brand/mubsir-icon.svg" width="120" alt="">

# Mubsir · مُبصِر

### Your whole PC, by voice.

**A free voice agent for blind and low-vision people on Windows.**<br>
Hold one key and say what you want, in Arabic, in English, or both in one sentence.<br>
Mubsir reads it, finds it or does it.

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![Windows 10 | 11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6)
![Works with NVDA](https://img.shields.io/badge/works%20with-NVDA-4B2E83)
![Arabic + English](https://img.shields.io/badge/speaks-Arabic%20%2B%20English-E2552B)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)

</div>

---

Most AI assistants live in a chat box and tell you what to click. **Mubsir does the clicking.** It reads your screen, works your apps, reads your documents and answers out loud. It runs next to the NVDA screen reader and never needs you to look.

Mubsir started as something one person needed: a grandfather who can't see the screen any more and speaks Arabic first. It's free and open source, and you bring your own keys.

```
افتح جوجل وابحث عن مواعيد الصلاة
هات the book on النحو واقرأ من آخر مكان
مين بعتلي على واتساب؟
open a new window with the same page
```

## What it does

| | |
| --- | --- |
| **Works the whole PC** | Opens apps, sites and files, reads you numbered results, clicks what it can see, fills in fields, plays YouTube, manages files, and installs a program when you ask and press a key. |
| **Reads what's really there** | Web pages, Word, PDFs and scanned Arabic books, picking up where you stopped. |
| **Reads any symbol** | Phonetic writing such as /ʃɪp/ is said sound by sound, and maths and Greek are named. |
| **Speaks your Arabic** | Egyptian, Gulf, Levantine, Libyan, Maghrebi and MSA, with English mixed in mid-sentence. |
| **Asks your own AI chat** | Signed in to ChatGPT or Gemini in your browser? Plain questions go there, cost nothing, and the answer is read aloud. |
| **Keeps you in control** | One key stops anything, instantly. Sending, paying and deleting need your OK. On bank pages the AI switches off, and you press Pay yourself. |
| **Stays light** | Everything on screen has a job and can be turned off. It's built for 4 GB PCs. |

On first start it asks a few questions, and each answer changes how it behaves. If you use a screen reader, it already knows and skips that question.

## Install

1. Download the latest zip from [Releases](https://github.com/dusk-futile/mubsir/releases) and extract it to a folder you keep.
2. Right-click `setup.ps1` → **Run with PowerShell** as administrator. It installs NVDA if needed, the Mubsir add-on for NVDA, Arabic speech and text recognition, and starts Mubsir when you sign in.
3. Mubsir's setup window talks you through adding one free Google Gemini key.

`START-HERE.txt` in the zip has the same steps as plain text, easy to read with a screen reader.

## Your keys

There are no keys in this repo and no account to make. Copy a key on the website and Mubsir picks it up, tests it and stores it, encrypted for your Windows account. Nothing goes in a file.

| Key | What it's for | |
| --- | --- | --- |
| [Google Gemini](https://aistudio.google.com/apikey) | The brain and hearing you | Required and free, within Google's limits. Google may use free requests to improve its products. |
| [Fish Audio](https://fish.audio/app/api-keys) | A natural Arabic voice | Optional, and free while Fish offers its free model. Without it, the free Windows voice speaks Arabic. |
| [Claude](https://console.anthropic.com/settings/keys) | A paid brain | Optional. If you add it, Claude Haiku is used instead of Gemini. |

English speech is free and private: Ryan, a natural voice that runs on your PC, downloads once in the background.

## Build it yourself

You need 64-bit Windows 10 (22H2) or 11, a microphone, the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), Git and Python.

```powershell
git clone https://github.com/dusk-futile/mubsir.git
cd mubsir
dotnet publish src/Mubsir.App -c Release -r win-x64 -o publish
python scripts/build-addon.py
.\publish\Mubsir.exe
```

Mubsir opens its setup window and talks you through the rest. To add Arabic speech and the NVDA add-on, and start Mubsir when you sign in, run this as administrator:

```powershell
.\scripts\setup.ps1 -MubsirExe (Resolve-Path .\publish\Mubsir.exe) -BridgeAddon (Resolve-Path .\dist\mubsirBridge-0.1.0.nvda-addon)
```

### Hand it to your AI agent

On the Windows PC, open your coding agent in an empty folder and paste this:

> Clone https://github.com/dusk-futile/mubsir.git, read AGENTS.md, then install what's missing, build Mubsir and start it. Run `pwsh scripts/run-tests.ps1` as administrator for the full checks. Stop and ask me when the app needs my API keys. Never write keys into a file or a commit.

## Using it

| Keys | What happens |
| --- | --- |
| Hold **Right Ctrl** | Talk. Release when you're done. |
| **Ctrl** | Stop speaking (same as NVDA). |
| Double-tap **Right Ctrl**, then **Space** | Read from here |
| … then **← / →** | Previous / next sentence |
| … then **↑ / ↓** | Previous / next paragraph |
| … then **Page Up / Page Down** | Previous / next page of a book, with no internet needed |
| … then **.** | Repeat |
| … then **Enter** | Type a request instead of speaking |
| … then **F1** | Help |
| **Right Ctrl** or **Esc** during a task | Stop it |
| **Win + Shift + Esc** | Stop everything, from anywhere |

Settings are in the tray icon's menu. To see the interface on its own, run `Mubsir.exe --overlay-demo`. It listens to, sends and clicks nothing, and Esc quits.

## What it costs

| | |
| --- | --- |
| Gemini brain and hearing | Free, within Google's limits |
| English voice (Ryan, on your PC) | Free |
| Arabic voice | Free with the Windows voice; Fish Audio's natural voice costs about $0.50 for 10 book pages, and pages already heard are kept |
| Claude Haiku brain, if you add it | About $2 a month at 60 requests a day |

## What your PC needs

64-bit Windows 10 (22H2) or 11, 4 GB of RAM (8 GB is smoother), a microphone, the internet, and about 300 MB of disk with the English voice.

## Privacy

- Mubsir only sends something when you ask for it, and only to the services you gave keys for.
- Passwords, card numbers and one-time codes are never sent. Bank and payment pages are handled locally.
- Say "private mode on" to keep everything on your PC.
- It never uses the camera.
- Logs contain timings and error codes only, never what you said or read.
- Chats are plain files in `Documents\Mubsir\Chats`, one a day. Card numbers and codes are hidden first. You can turn this off, and private mode saves nothing.

## For developers

```
src/Mubsir.Core      the brain, reading and onboarding, builds on any OS
src/Mubsir.App       the Windows app
src/Mubsir.Overlay   the notch and the setup window
nvda-addon/          the NVDA bridge
tests/               dotnet test tests/Mubsir.Core.Tests
scripts/package.ps1  builds the release zip
```

Start with [CONTRIBUTING.md](CONTRIBUTING.md). Coding agents start at [AGENTS.md](AGENTS.md). Where it's going: [docs/ROADMAP.md](docs/ROADMAP.md). Security issues: [SECURITY.md](SECURITY.md).

## License

[MIT](LICENSE). The NVDA add-on in `nvda-addon/` is GPL-2.0, like NVDA itself, and NVDA's controller client is LGPL-2.1. The English voice uses [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) (Apache-2.0) with espeak-ng (GPL-3.0). Its voice model, Ryan from [Piper](https://github.com/rhasspy/piper), is trained on [RyanSpeech](https://www.kaggle.com/datasets/roholazandie/ryanspeech), CC BY-NC-SA 4.0, so it can't be used commercially.
