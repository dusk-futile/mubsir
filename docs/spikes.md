# M0 spikes

Each spike answers one question with a pass/fail result on the Windows test PC (and, where it says so, on the weak profile: msconfig 4096 MB, 2 CPUs, HDD if possible). Fill in the results table at the end. A failed spike is not a problem: it chooses the fallback.

## (a) Native AOT (2 days at most)

Publish a test exe with `PublishAot=true` that does each of these. Pass = all six work in the AOT exe, without trimming warnings that matter.

| # | Check | Pass when |
|---|---|---|
| a1 | Source-generated COM for UIA: focused element, `CacheRequest` + `FindAll`, `TextPattern` ranges | Same results as the JIT build on Notepad and Chrome |
| a2 | Unpackaged WinRT `Windows.Media.Ocr` (ar-SA and en) | Recognizes a test image in both languages |
| a3 | `Windows.Data.Pdf` page render | Renders page 1 of a scanned PDF to a bitmap |
| a4 | `Windows.Media.SpeechSynthesis` (OneCore fallback voice) | Speaks Arabic and English |
| a5 | Anthropic C# SDK streaming | First token arrives; tool-call JSON parses |
| a6 | Size and cold start | Exe size noted; cold start under 1 s on the weak profile |

Fail on any: ship **ReadyToRun, untrimmed** (the current default in `Basira.App.csproj`).

## (b) NVDA speech from Basira

| # | Check | Pass when |
|---|---|---|
| b1 | `nvdaController_speakSsml` with a language tag (`<lang xml:lang="ar">` or `<voice xml:lang=…>`): which one switches the OneCore voice | Arabic and English voices switch inside one call; note the working tag |
| b2 | `<mark name="…"/>` callbacks | Callbacks arrive in order and close to when the words are spoken (note the lag) |
| b3 | Ctrl while Basira speaks through NVDA | Speech stops at once; Basira's queue also empties |
| b4 | Injected keys (SendInput) | NVDA does not treat Basira's injected keys as user input that cancels Basira's speech (or the workaround is noted) |
| b5 | Synchronous mode | `speakSsml` with the synchronous flag blocks until done or cancelled |

## (c) Basira Bridge add-on

| # | Check | Pass when |
|---|---|---|
| c1 | `speech.extensions.filter_speechSequence` exists in NVDA 2026.2 and the add-on's filter runs (verified in source; confirm at runtime in the NVDA log: no "add-on is inactive" warning) | Mixed text such as "افتح ملف PDF على Windows 10" is spoken with both voices by NVDA itself |
| c2 | `LangChangeCommand("ar"/"en")` with automatic language switching picks the OneCore Arabic / English voice | Correct voice per run; no voice loading delay above ~100 ms |
| c3 | Sequences that already contain language changes (e.g. web pages with `lang` attributes) | Left unchanged |
| c4 | `speech.getCurrentLanguage()` | Returns the synth's language; Arabic default is used for digits in ties |
| c5 | Quiet mode: write `{"until": <now+5000>}` to `%LOCALAPPDATA%\Basira\nvda-quiet.json`, then Alt+Tab | No focus announcement for 5 s; after that announcements return. Focus still moves (NVDA+Tab reads the new focus; browse mode works). Source check: `eventHandler.executeEvent` calls `doPreGainFocus` (which calls `api.setFocusObject`) before the global plugin chain, and `speech.speak` calls `filter_speechSequence.apply` synchronously, so dropping speech inside the chain is safe |
| c6 | Quiet mode does not swallow other speech | Basira's own speech and NVDA speech outside focus events (typing echo, say-all) still speak during quiet |
| c7 | Install through `nvda_slave.exe addons_installAddonPackage <file>` (used by `setup-father-pc.ps1`; this is the action behind the `.nvda-addon` file association) | NVDA shows its install dialog; after restart the add-on is listed as running |
| c8 | Add-on disabled on the sign-in screen | `systemConfig` has no add-ons; sign-in speech works |

## (d) Keyboard hook

| # | Check | Pass when |
|---|---|---|
| d1 | Single Ctrl tap | Stops speech; passes through; arms nothing |
| d2 | Right Ctrl double-tap within 400 ms | Layer tone; layer keys work; other keys exit and pass through |
| d3 | Right Ctrl hold | Recording starts at key-down (pre-roll present); release sends |
| d4 | Slow Ctrl+C (hold Ctrl 1 s, then C) | Copies; recording dropped silently |
| d5 | Restart NVDA (NVDA+Q, Ctrl+Alt+N), then use the layer in Chrome browse mode | Basira's hook is re-installed ahead of NVDA's within 30 s or on the PID change; layer works |
| d6 | Admin window in the foreground | Hook gets no keys there (expected); Basira announces the handoff once |
| d7 | `RegisterHotKey` stop combination over an admin window and with the hook removed | Stop still works; record the chosen combination in keymap.md |
| d8 | Injected keys (`LLKHF_INJECTED`) | Ignored by the state machine |

## (e) Time to first token

From his location and connection, 20 runs each, short prompts typical of Basira:

| # | Check | Pass when |
|---|---|---|
| e1 | Haiku (`claude-haiku-5-5`), streaming, cached prefix | p50 ≤ 1.2 s, p90 ≤ 2 s to first token |
| e2 | Gemini (`gemini-3.5-flash-lite`), 3 s audio clip, transcript + local_command | p50 ≤ 1.2 s, p90 ≤ 2 s to full response |
| e3 | End to end (key release to first audio, typed and voice) | Typed p50 ≤ 2.5 s; voice p50 ≤ 3 s, p90 ≤ 5 s |

## (f) UIA snapshot time

| # | Check | Pass when |
|---|---|---|
| f1 | Pruned tree of a long Arabic Wikipedia page in Chrome, weak profile | ≤ 300 elements within 1.5 s; Basira says when it stopped early |
| f2 | `TextPattern` visible text of the same page | ≤ 500 ms for the visible range |
| f3 | Focus and window name (fast thread) during f1 | ≤ 50 ms |

## Other things to confirm on Windows

These were implemented from documentation or source reading and need a real run:

- NVDA `nvda.ini` keys written by the setup script (`[general] language, showWelcomeDialogAtStartup`, `[speech] synth=oneCore, autoLanguageSwitching`, `[keyboard] keyboardLayout`, `[virtualBuffers] autoSayAllOnPageLoad`, `[update] autoCheck, startupNotification, allowUsageStats, askedAllowUsageStats`): checked against `source/config/configSpec.py` in NVDA 2026.2; confirm NVDA keeps them after a restart.
- `<NVDA install dir>\systemConfig\nvda.ini` is the sign-in screen config.
- The installer creates the Ctrl+Alt+N desktop shortcut.
- The dev debug pipe `\\.\pipe\basira-dev` is reachable from an SSH session (a different logon session) when the dev build runs on the desktop.
- Windows capability names `Language.Basic~~~ar-SA~0.0.1.0`, `Language.TextToSpeech~~~ar-SA~0.0.1.0`, `Language.OCR~~~ar-SA~0.0.1.0` install on Windows 10 22H2 and Windows 11.
- The UAC prompt's button shortcuts in his Windows display language.

## Results

| Spike | Date | PC / profile | NVDA | Result (pass/fail) | Numbers | Decision / notes |
|---|---|---|---|---|---|---|
| a1–a6 AOT | | | | | | |
| b1 SSML language tag | | | | | | |
| b2 marks | | | | | | |
| b3 Ctrl | | | | | | |
| b4 injected keys | | | | | | |
| b5 synchronous | | | | | | |
| c1–c4 add-on language runs | | | | | | |
| c5–c6 quiet mode | | | | | | |
| c7–c8 add-on install, sign-in | | | | | | |
| d1–d8 keyboard hook | | | | | | |
| e1 Haiku TTFT | | | | | | |
| e2 Gemini latency | | | | | | |
| e3 end to end | | | | | | |
| f1–f3 UIA timing | | | | | | |
| Other confirmations | | | | | | |
