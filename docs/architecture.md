# Architecture

Basira is one Windows program with a hidden window and no visual interface. It runs on top of the NVDA screen reader: NVDA keeps doing what it does well (typing echo, Tab and menu navigation, the sign-in screen, permission prompts), and Basira adds voice requests, reading help, and an agent that carries out tasks while saying what it does.

```
 Keys / Mic ─► Input (LL hook re-installed every 30 s and on NVDA restart; ignores injected keys;
               always-on RegisterHotKey stop)
                 │
                 ▼
          Router ── local, no AI: read / rewind / repeat / scroll / open app / OCR ──► Reader / Actions
                 │ needs AI (context built in parallel while the talk key is held)
                 ▼
  Context Builder: app, window, focus, pruned UIA tree with numeric ids (≤300 elements, ≤1.5 s),
                   visible text, speech-history ids, [screenshot: announced, off in bank mode]
                 │  local redaction pass (cards, IBAN, OTP-like codes, phone/IDs) before anything leaves
                 ▼
  Brain (Haiku 5.5, streaming, tools) ──► Policy (Core): tiers 1/2/3 by *target*, untrusted-content flag,
                 │                         sensitive mode; confirmations built from UIA ground truth
                 ▼
  Actions: focus re-check → UIA patterns → SendInput (Unicode) → read back to verify → narrate the step
                 │
                 ▼
  Speech Out: sentence splitter → segmenter (ar/en) → symbol/emoji verbalizer
              → NVDA speakSsml (+<mark>) | OneCore ; SpeechHistory kept in memory only
  NVDA + Basira Bridge add-on: language switching for all NVDA speech; quiet mode during agent steps
```

## The parts

| Part | Where | Notes |
|---|---|---|
| Input | `src/Basira.App/Input`, state machine in `src/Basira.Core/Input` | Low-level keyboard hook on its own thread, keys bound by scan code; talk key hold, double-tap command layer, stop and pause during agent tasks. |
| Router | `src/Basira.Core/Router` | Recognizes simple commands in Arabic dialects and English (scroll, read, stop, open app) so they never wait for the AI. The speech-to-text call also returns a `local_command` guess. |
| Context | `src/Basira.Core/Context` | Builds the request from UI Automation facts with numeric ids; caps size; redacts card numbers, IBANs, codes and IDs before anything leaves the PC. |
| Brain | `src/Basira.Core/Brain`, client in `src/Basira.App/Brain` | Claude Haiku through the official SDK, streaming, prompt caching, strict tool schemas. Model IDs come from config behind `IBrain` / `IEars`. |
| Policy | `src/Basira.Core/Policy` | Tier 1 (do and narrate), Tier 2 (one-key confirmation built from UIA facts), Tier 3 (never). Untrusted-content flag, bank mode. |
| Speech | `src/Basira.Core/Speech`, `src/Basira.Core/Text` | Sentence splitter, Arabic/English segmenter, symbol and emoji names, then NVDA `speakSsml` with marks (OneCore fallback). |
| Basira Bridge | `nvda-addon/basiraBridge` | NVDA add-on (GPL-2.0): splits *all* NVDA speech into Arabic and English runs, and quiets focus announcements while an agent step runs. |
| Setup and dev | `scripts/` | PC inventory, father's PC setup (NVDA, config, Arabic features, logon task), Mac to Windows dev loop. |
| Evals | `evals/` | Task schema, synthetic tasks, scorer; real recordings stay in `evals/private/` (git-ignored). |

## Two processes, two licenses

- **Basira.exe** (MIT): the app. It talks to NVDA only through NVDA's public controller client (`nvdaController_speakSsml`, `nvdaController_cancelSpeech`, mark callbacks) and through one small file.
- **Basira Bridge** (GPL-2.0): runs inside NVDA. Basira writes `%LOCALAPPDATA%\Basira\nvda-quiet.json` (`{"until": <unix ms>}`) while an agent step runs; the add-on reads it at most every 250 ms and drops speech produced by focus events until then. A quiet window never outlasts the file's write time by more than 60 s.

## Repository layout

```
basira/
  src/Basira.Core/         net10.0, platform-independent; built and unit-tested on macOS and Linux
  src/Basira.App/          net10.0-windows; UIA, OCR, audio, hooks, NVDA client, scheduled-task --watch mode
  tests/Basira.Core.Tests/ xUnit tests for Core
  nvda-addon/basiraBridge/ the NVDA add-on; nvda-addon/tests/ (pytest, no NVDA needed)
  scripts/                 inventory.ps1, setup-father-pc.ps1, build-addon.py, check-no-audio.sh
  scripts/dev/             setup-dev-pc.ps1, run.sh, logs.sh, send.sh (Mac to Windows test PC)
  evals/                   tasks.schema.json, tasks.sample.json, score.py; evals/private/ is git-ignored
  docs/                    this file, father-setup, keymap, privacy, test-checklist, spikes
  .github/                 CI (Core tests, add-on build, Windows build), Dependabot
```

## Build and test

| Where | Command |
|---|---|
| Mac or Linux | `dotnet test tests/Basira.Core.Tests/Basira.Core.Tests.csproj` |
| Mac or Linux | `python3 -m pytest nvda-addon/tests evals/tests` and `python3 scripts/build-addon.py` |
| Windows | `dotnet build src/Basira.App -c Debug` (or `scripts/dev/run.sh` from the Mac) |
| CI | `.github/workflows/ci.yml`: Core tests (locked restore), audio check, add-on tests and package, then a Windows Release build and win-x64 publish |

## Design limits

- Idle RAM under 80 MB; hotkey to first local audio under 300 ms on a 4 GB, 2-core PC.
- Voice request to first audio: p50 at most 3 s, p90 at most 5 s. Typed: p50 at most 2.5 s.
- UIA snapshot capped at about 300 elements or 1.5 s; Basira says when it read only part of a window.
- Logs hold metadata only and are kept 14 days.
