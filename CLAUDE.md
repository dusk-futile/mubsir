# Mubsir — instructions for Claude Code sessions

Read this first, then `docs/HANDOFF.md` (where we are and what's next), then `docs/PLAN.md` (the full plan).
When building, follow `docs/EXECUTION.md` (order of work, tests, numbers to report in `docs/RESULTS.md`).

## What this is

Mubsir (مُبصِر) is a free Windows voice agent that can do anything a mouse and keyboard can do.
- **Blind mode**: no visuals, earcons + speech, works next to NVDA, reads books by page.
- **Normal mode**: a notch at the top, a pointer that flies to UI elements, a drawing board for teaching
  (Clicky-style), Caps Lock dictation (Saywork-style).
- Arabic (Egyptian and other dialects) and English, mixed mid-sentence.

## Owner decisions (don't re-ask)

- Completely free for users. The owner pays all AI and voice costs through their own server.
- Distributed as a zip. No API keys in the zip, no account, no sign-in; the device registers itself.
- Windows only (10 22H2 and 11), must stay light on 4 GB RAM PCs.
- Voice: Fish Audio hosted API is the lead choice (to be confirmed by a week-1 blind listening test vs
  ElevenLabs, Azure, Google). The app asks the server for a "conversation" or "reading" voice; the
  provider is a server setting. Windows voices are only the fallback. Fish open weights: research
  licence + GPU only, not used.
- Reading must handle any symbol (IPA, maths, Arabic tashkeel); see PLAN §6 "Reading anything".
- Full access including passwords and payments. Dangerous actions (level 3) need **Right Ctrl pressed
  four times**; passwords are typed locally from the Secrets Vault and never sent to the AI.
- Limits: free (local / cached) things have no cost limit, only a listening check-in the user can set or
  turn off ("read without stopping"). Paid things: 2-minute check-in while new audio is made, invisible
  daily premium budget, then the Windows voice continues; users never hear "limit reached" (PLAN §6).
- One user = one PC; no multi-device sync. The owner's own PC runs nothing for users (PLAN §9).
- Normal mode notch: prototype in HTML with `taste-skill`, owner approves, then native WPF (PLAN §6).
- Brain: Claude Haiku 5.5 (`claude-haiku-5-5`) by default, escalate to Sonnet 5.5 (`claude-sonnet-5-5`).
- Everything about the project (decisions, progress, next steps) is written into this repo so work can
  continue from any device. **Update `docs/HANDOFF.md` at the end of every session.**

## Code layout

- `src/Mubsir.Core` — cross-platform logic (text pipeline, policy, input state machine, agent loop,
  context builder). Builds on Linux; CI builds it.
- `src/Mubsir.App` — Windows host (keyboard hook, UIA, OCR, audio, speech, tools, Claude/Gemini clients).
  Windows only.
- `nvda-addon/` — NVDA Bridge add-on (Python, GPL-2.0).
- `scripts/` — `setup.ps1` installer script, `build-addon.py`.
- Planned (see PLAN §11): `src/Mubsir.Overlay` (WPF), `src/Mubsir.Setup` (Velopack), `cloud/proxy`
  (Cloudflare Worker), `tests/`.

## Build

```bash
dotnet build src/Mubsir.Core -c Release        # any OS
dotnet build Mubsir.slnx                        # Windows, full app
python scripts/build-addon.py
```

CI runs restore in locked mode (`RestoreLockedMode` when `CI=true`). If you change packages or the
SDK version, regenerate lock files with `dotnet restore Mubsir.slnx --force-evaluate` and commit them.

## Conventions

- C# / .NET 10, nullable on, implicit usings. Keep `Mubsir.Core` free of Windows APIs.
- Never put API keys in the repo or in the shipped app. Keys live only in the cloud proxy's secrets.
- Logs hold timings and error codes only, never what the user said, read or typed.
- Skills available in `.claude/skills/`: `agent-reach` (web/platform research),
  `reverse-engineer-anything` (REA; needs its MCP server and a Windows machine with the target app),
  `taste-skill` (UI design direction, for the notch prototype) and `find-skills` (find more skills).
