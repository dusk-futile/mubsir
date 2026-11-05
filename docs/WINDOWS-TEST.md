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
| 5 | The real calls: does Haiku 5.5 accept `output_config` effort low, does `api.fish.audio/wallet/self/api-credit` test a key, and does a task of 25 or more steps still work after the conversation is trimmed (`ConversationTrimmer`), including with Haiku's thinking blocks | No errors in `%LOCALAPPDATA%\Mubsir\logs` |
| 6 | **Fish hearing in Arabic, English and mixed sentences.** Its Arabic quality is unknown and this decides whether Gemini should stay the default. | Say 20 real sentences and count the right ones |
| 7 | Fish voice: Arabic, English, mixed. Pull the network mid-sentence. | Falls back to the Windows voice with no silence |
| 8 | First conversation by voice and by buttons, as each kind of person | Settings match the answers |
| 9 | Teaching: "علمني أعمل فولدر جديد" | The pointer and mouse go to the right place, nothing gets clicked |
| 10 | Ctrl alone stops speech and a running task, on release. Ctrl+C in another app doesn't. Right Ctrl and Win+Shift+Esc also stop it. | It stops at once |
| 11 | A delete: "yes" by voice must not agree, Enter must | Only Enter works |
| 12 | Read a real book for 20 minutes through NVDA, stop, resume tomorrow's place. Do it once in the Fish voice with Mubsir Bridge installed: are the gaps between sentences acceptable, and does the spending command match Fish's own usage page? | The place is right, and the cost matches |
| 13 | Sleep and wake the PC, then hold Right Ctrl | It listens at once |
| 14 | RAM and CPU on a 4 GB PC or VM: with and without Hudhud, asleep and active | Numbers go in `docs/RESULTS.md` |
| 16 | Say "دور على مواعيد الصلاة" in Chrome and Edge | Up to 5 real results read with numbers, no ads; "رقم واحد" opens it |
| 17 | `click_on` in an app with no accessible buttons, such as Paint. Say "اضغط على الدايرة الزرقا" | The right place gets clicked. A button that says Pay asks for a key press first |
| 18 | "افتح الملف" on a PDF and on an installer | The PDF opens, the installer asks for Enter, a .bat is refused |
| 19 | "غير الصوت" with a Fish key, then pick a number | Each sample plays with its number, Ctrl stops them, the choice stays after a restart |
| 20 | Read a page in the Fish voice twice | The second time costs nothing in the spending command |
| 21 | A book with /ʃɪp/ in it | Said sound by sound |
| 22 | Say yes to describing books, then wait for the next scan and search by subject | Bank statements and short files aren't described |
| 15 | Install `Mubsir.msix` with a test signature | It starts, listens and restarts at sign-in |
