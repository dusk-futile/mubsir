# Mubsir — instructions for coding sessions

Read this first, then `docs/PRODUCT.md` (what the app is), `docs/HANDOFF.md` (where we are and what's next) and `docs/WINDOWS-TEST.md` (the full test). `docs/BRAND.md` has the brand. Older plans are in `docs/archive/`. Where they disagree with "Owner decisions" below, the decisions win.

## What this is

Mubsir (مُبصِر) is a free voice agent for blind and low-vision people on Windows (decided 2026-11-05: no other audience). It can do anything a mouse and keyboard can do.
- The notch shows for everyone, so anyone watching the screen understands what's happening, and all of it works by voice and Right Ctrl alone.
- First start is a short branching conversation (`Mubsir.Core/Onboarding`). Each answer changes settings and behaviour. The only kinds of user are blind and low vision.
- Teaching mode never clicks: a blind learner is taught the keys, and for low vision the pointer goes to the place. The policy enforces this.
- Arabic (Egyptian and other dialects) and English, mixed mid-sentence.

## Owner decisions (don't re-ask)

- **Decided 2026-11-05, replacing the owner-paid server:** the app is completely free and each user pastes their own keys. The owner is building it for their grandfather and will set up their own keys for him. Decided 2026-11-05: it should run free: Gemini is the default brain and hearing, Fish's free model is tried first for Arabic, and Claude is an optional paid brain used when its key is there. Claude Haiku 5.5 is the recommended brain. Fish Audio is for hearing and the Arabic voice. Gemini is an optional alternative for hearing. The setup window suggests costs. There is no account, server or relay.
- Reliability first. Everything heavy is in the back end, and the interface is clean, simple and light for weak PCs. Everything on screen must have a purpose, including Hudhud.
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
- Identity: **Hudhud** (hoopoe). Normal-mode companion sits **bottom-right**; prototype in
  `prototypes/hudhud-companion/` (owner approves look → motion tokens → native WPF). Brand: `docs/BRAND.md`.
- The build may restructure or replace old code, keeping the best parts. Never delete the repo.
- Brain: Gemini on its free tier unless a Claude key is added. Claude means Haiku 5.5 (`claude-haiku-5-5`) by default, escalating to Sonnet 5.5 (`claude-sonnet-5-5`).
- Everything about the project (decisions, progress, next steps) is written into this repo so work can
  continue from any device. **Update `docs/HANDOFF.md` at the end of every session.**

## Code layout

- `src/Mubsir.Core` — cross-platform logic (text pipeline, policy, input state machine, agent loop,
  context builder). Builds on Linux; CI builds it.
- `src/Mubsir.App` — Windows host (keyboard hook, UIA, OCR, audio, speech, tools, Claude/Gemini clients).
  Windows only.
- `src/Mubsir.Overlay` — normal mode's visuals: the notch and Hudhud, WPF built in code on its own UI
  thread. Sizes and springs are in `motion-tokens.json`, taken from the approved prototype. Try it with
  `Mubsir.exe --overlay-demo`. Spring, token and pose logic live in `Mubsir.Core/Motion`.
- `assets/hudhud/` — Hudhud's cut-out parts and `rig.json`, shared by the overlay and the prototype.
- `tests/Mubsir.Core.Tests` — Core unit tests (xunit). Runs on any OS.
- `nvda-addon/` — NVDA Bridge add-on (Python, GPL-2.0).
- `scripts/` — `setup.ps1` installer script, `pack-msix.ps1` (Store package), `build-addon.py`, `build-icons.py`, and
  `overlay-smoke.ps1` and `setup-shots.ps1` (run on a Windows desktop in CI; their screenshots are the `overlay-shots` artifact).
- `docs/archive/` — old plans, kept for history.
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
- Skills available in `.agents/skills/`: `agent-reach` (web/platform research),
  `reverse-engineer-anything` (REA; needs its MCP server and a Windows machine with the target app),
  `taste-skill` (UI design direction, for the notch prototype) and `find-skills` (find more skills).
