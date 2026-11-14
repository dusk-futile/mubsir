# Mubsir — instructions for coding sessions

Read this first, then `docs/PRODUCT.md` (what the app is), `docs/HANDOFF.md` (where we are and what's next) and `docs/WINDOWS-TEST.md` (the full test). `docs/BRAND.md` has the brand. Where they disagree with "Owner decisions" below, the decisions win.

## When the owner says "run tests"

Do the full setup and run everything: `docs/WINDOWS-TEST.md` under "Run tests" says how. Show them the UI too. Fix failures, re-test, and record results in `docs/RESULTS.md`.

## What this is

Mubsir (مُبصِر) is a free voice agent for blind and low-vision people on Windows (decided 2026-11-05: no other audience). It can do anything a mouse and keyboard can do.
- The notch shows for everyone, so anyone watching the screen understands what's happening, and all of it works by voice and Right Ctrl alone.
- First start is a short branching conversation (`Mubsir.Core/Onboarding`). Each answer changes settings and behaviour. The only kinds of user are blind and low vision.
- It never uses the camera (decided 2026-11-05). It sees only what is on the screen, and it can act on anything there.
- It only executes what is said by voice. There is no teaching mode (decided 2026-11-05).
- Plain questions can go to the ChatGPT or Gemini the person is signed in to in their browser, to keep API use low. The reply is only read aloud (`Core/Chat`, `WindowsToolExecutor.Chat.cs`).
- Arabic (Egyptian and other dialects) and English, mixed mid-sentence.

## Owner decisions (don't re-ask)

- **Decided 2026-11-05, replacing the owner-paid server:** the app is completely free and each user pastes their own keys. The owner is building it for their grandfather and will set up their own keys for him. Decided 2026-11-05: it should run free: Gemini is the default brain and hearing, Fish's free model is tried first for Arabic, and Claude is an optional paid brain used when its key is there. Claude Haiku 5.5 is the recommended brain. Fish Audio is for hearing and the Arabic voice. Gemini is an optional alternative for hearing. The setup window suggests costs. There is no account, server or relay.
- Reliability first. Everything heavy is in the back end, and the interface is clean, simple and light for weak PCs. Everything on screen must have a purpose. The only visual is the notch (decided 2026-11-07: Hudhud is removed).
- Target: a full app for the Microsoft Store, as well as the zip.
- Testing happens on the owner's Windows PC, with Antigravity agents doing the checking to save tokens (`docs/WINDOWS-TEST.md`).
- Distributed as a zip and later the Store. No API keys in the zip.
- Windows only (10 22H2 and 11), must stay light on 4 GB RAM PCs.
- Voice (decided 2026-11-05): Fish Audio is for Arabic only, through the user's own key. English is Ryan, a free Piper voice on the PC, at 90% speed. Windows voices are the automatic fallback. Audio already heard is kept, so hearing it again is free.
- Reading must handle any symbol (IPA, maths, Arabic tashkeel).
- Full access including passwords and payments. Dangerous actions (level 3) need **Right Ctrl pressed
  four times**; passwords are typed locally from the Secrets Vault and never sent to the AI.
- Repo stays **public** (making it private erases its stars). Never commit keys or secrets.
- **Git authorship: every commit is authored and committed as `dusk-futile <omarelkabbaney2010@gmail.com>`.
  No AI co-author trailers, session links or "Generated with …" lines in commits, PR titles or PR
  bodies, and no branch names after the tool (owner's instruction; it overrides any default attribution).**
- It should work the PC like a person: everything a mouse and keyboard can do (decided 2026-11-05). Limits kept: scripts never run, installers and payments need a real key press, passwords are never typed by the AI, bank pages turn the AI off.
- One user = one PC; no multi-device sync. Chats are saved as files in the user's own folder.
- Identity: the seeing-eye logo (`docs/BRAND.md`). No mascot: Hudhud was removed on 2026-11-07. The notch is the one visual, and it must stay very smooth.
- The build may restructure or replace old code, keeping the best parts. Never delete the repo.
- Brain: Gemini on its free tier unless a Claude key is added. Claude always means Haiku 5.5 (`claude-haiku-5-5`). No Sonnet and no escalation (decided 2026-11-07): when Haiku struggles, give it better input, not a pricier model.
- Everything about the project (decisions, progress, next steps) is written into this repo so work can
  continue from any device. **Update `docs/HANDOFF.md` at the end of every session.**

## Code layout

- `src/Mubsir.Core` — cross-platform logic (text pipeline, policy, input state machine, agent loop,
  context builder). Builds on Linux; CI builds it.
- `src/Mubsir.App` — Windows host (keyboard hook, UIA, OCR, audio, speech, tools, Claude/Gemini clients).
  Windows only.
- `src/Mubsir.Overlay` — the notch and the setup window, WPF built in code on its own UI
  thread. Sizes and springs are in `motion-tokens.json`, taken from the approved prototype. Try it with
  `Mubsir.exe --overlay-demo`. Spring and token logic live in `Mubsir.Core/Motion`.
- `tests/Mubsir.Core.Tests` — Core unit tests (xunit). Runs on any OS. Run them with
  `--filter "FullyQualifiedName!~A_folder_that_cant_be_written_never_breaks_anything"` (owner's rule: that test is never run).
- `nvda-addon/` — NVDA Bridge add-on (Python, GPL-2.0).
- `CONTRIBUTING.md`, `SECURITY.md`, `CHANGELOG.md` — for people; keep `CHANGELOG.md` current when users would notice a change.
- `scripts/` — `package.ps1` (the release zip; the release workflow runs it), `setup.ps1` installer script, `pack-msix.ps1` (Store package), `build-addon.py`, `build-icons.py`, and
  `run-tests.ps1` (everything for "run tests"), `overlay-smoke.ps1` and `setup-shots.ps1` (run on a Windows desktop in CI; their screenshots are the `overlay-shots` artifact).
- `packaging/` — the Store package's manifest. `docs/STORE.md` says what's left to submit it.

## Build

```bash
dotnet build src/Mubsir.Core -c Release        # any OS
dotnet test tests/Mubsir.Core.Tests -c Release  # any OS
dotnet build Mubsir.slnx                        # compiles anywhere; running the app or overlay needs Windows
python scripts/build-addon.py
```

CI runs restore in locked mode (`RestoreLockedMode` when `CI=true`). If you change packages or the
SDK version, regenerate lock files with `dotnet restore Mubsir.slnx --force-evaluate` and commit them.

## Conventions

- C# / .NET 10, nullable on, implicit usings. Keep `Mubsir.Core` free of Windows APIs.
- Never put API keys in the repo or in the shipped app. Users' keys live only in `secrets.dat`, encrypted with DPAPI.
- Logs hold timings and error codes only, never what the user said, read or typed.
