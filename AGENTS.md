# Mubsir — instructions for coding sessions

Read this first, then `docs/HANDOFF.md` (where we are and what's next), `docs/CONVERSATION-SUMMARY.md`
(everything the owner asked for and decided), then `docs/PLAN.md` (the full plan).
Before building reading or memory, read `docs/USER-JOURNEY.md` (the blind user's journey, code gaps, token options).
Brand options: `docs/BRAND.md`. When building, follow `docs/EXECUTION.md` (order of work, tests, numbers to report in `docs/RESULTS.md`).

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
  turn off ("read without stopping"). Paid things: 2-minute check-in while new audio is made; always
  the premium voice (no daily switch to Windows voices); only a high abuse guard per device and the
  owner's monthly budget alerts; users never hear "limit reached" (PLAN §6).
- Repo stays **public** (making it private erases its stars). Never commit keys or secrets.
- **Git authorship: every commit is authored and committed as `dusk-futile <omarelkabbaney2010@gmail.com>`.
  No AI co-author trailers, session links or "Generated with …" lines in commits, PR titles or PR
  bodies, and no branch names after the tool (owner's instruction; it overrides any default attribution).**
- One user = one PC; no multi-device sync. The owner's own PC runs nothing for users (PLAN §9).
- Identity: **Hudhud** (hoopoe). Normal-mode companion sits **bottom-right**; prototype in
  `prototypes/hudhud-companion/` (owner approves look → motion tokens → native WPF). Brand: `docs/BRAND.md`.
- The build may restructure or replace old code, keeping the best parts. Never delete the repo.
- Brain: Claude Haiku 5.5 (`claude-haiku-5-5`) by default, escalate to Sonnet 5.5 (`claude-sonnet-5-5`).
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
- `cloud/proxy/` — the relay (Cloudflare Worker, TypeScript).
- `tests/Mubsir.Core.Tests` — Core unit tests (xunit). Runs on any OS.
- `nvda-addon/` — NVDA Bridge add-on (Python, GPL-2.0).
- `scripts/` — `setup.ps1` installer script, `build-addon.py`, `build-icons.py`, and `overlay-smoke.ps1`
  (runs the overlay on a Windows desktop in CI; its screenshots are the `overlay-shots` artifact).
- Planned (see PLAN §11): `src/Mubsir.Setup` (Velopack).

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
- Never put API keys in the repo or in the shipped app. Keys live only in the cloud proxy's secrets.
- Logs hold timings and error codes only, never what the user said, read or typed.
- Skills available in `.agents/skills/`: `agent-reach` (web/platform research),
  `reverse-engineer-anything` (REA; needs its MCP server and a Windows machine with the target app),
  `taste-skill` (UI design direction, for the notch prototype) and `find-skills` (find more skills).
