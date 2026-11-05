# Testing on the owner's Windows PC

Everything here was built on a Mac. Compiling and CI show it builds, but nobody has used it on a real PC. Run this from a new Claude Code session on Windows, with this repo on branch `feat/reading-direct`.

## Before starting

1. Install the .NET 10 SDK, Git, and Antigravity (`agy`), then sign in to `agy` once.
2. Have a Claude key and a Fish Audio key ready. Paste them only into Mubsir's setup window, never into a chat or a job prompt.

## Prompt for the new session

> Read AGENTS.md, docs/HANDOFF.md and docs/WINDOWS-TEST.md. You're the brain and Antigravity does the work, to save tokens. Use Antigravity jobs with Gemini 3.8 Flash to run and report each test below, and verify what they report before telling me. Never put my keys in a prompt.

## What to test, in order

Each test says what proves it. Anything not proved stays "not tested".

| # | Test | Proved by |
| --- | --- | --- |
| 1 | `dotnet test tests/Mubsir.Core.Tests -c Release` and `dotnet publish src/Mubsir.App -c Release -r win-x64 -o publish` | All pass |
| 2 | `publish\Mubsir.exe --book-check tests\fixtures\books results.txt` | `ALL PASSED`, with Arabic OCR and speech installed. In CI the last failure was the letter `قرأ` lost by the PDF; the fix for that hasn't run on Windows yet. |
| 3 | First start with no keys: the setup window opens and speaks, by keyboard only, with NVDA on and off | A person hears every step |
| 4 | Copy a Claude key: it's picked up, tested and saved. Try a wrong key too. Check a real Fish key really is 32 hex characters, which the clipboard pickup assumes. | The spoken result is right both times |
| 5 | The real calls: does Haiku 5.5 accept `output_config` effort low, and does `api.fish.audio/wallet/self/api-credit` test a key | No errors in `%LOCALAPPDATA%\Mubsir\logs` |
| 6 | **Fish hearing in Arabic, English and mixed sentences.** Its Arabic quality is unknown and this decides whether Gemini should stay the default. | Say 20 real sentences and count the right ones |
| 7 | Fish voice: Arabic, English, mixed. Pull the network mid-sentence. | Falls back to the Windows voice with no silence |
| 8 | First conversation by voice and by buttons, as each kind of person | Settings match the answers |
| 9 | Teaching: "علمني أعمل فولدر جديد" | The pointer and mouse go to the right place, nothing gets clicked |
| 10 | Ctrl alone stops speech and a running task, on release. Ctrl+C in another app doesn't. Right Ctrl and Win+Shift+Esc also stop it. | It stops at once |
| 11 | A delete: "yes" by voice must not agree, Enter must | Only Enter works |
| 12 | Read a real book for 20 minutes through NVDA, stop, resume tomorrow's place | The place is right |
| 13 | Sleep and wake the PC, then hold Right Ctrl | It listens at once |
| 14 | RAM and CPU on a 4 GB PC or VM: with and without Hudhud, asleep and active | Numbers go in `docs/RESULTS.md` |
| 15 | Install `Mubsir.msix` with a test signature | It starts, listens and restarts at sign-in |
