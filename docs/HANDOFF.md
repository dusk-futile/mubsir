# Handoff — where the work stands

Update this file at the end of every session. Newest entry on top.

---

## 2026-11-04 — Plan approved, nothing built yet

### Done
- Researched Clicky (open source, read its code) and Saywork (store listing only). Findings in `docs/PLAN.md` §1.
- Wrote `docs/PLAN.md` (Mubsir 2.0 plan). The owner approved it, then locked these decisions:
  free for users, zip distribution with no keys, Windows only, ElevenLabs everywhere, full access with
  a 4× Right Ctrl gesture for passwords and payments, name stays Mubsir.
- Added skills `agent-reach` and `reverse-engineer-anything` to `.claude/skills/` (MIT, sources in
  `.claude/skills/README.md`). `.gitignore` now tracks `.claude/skills/` only.
- Added this file and `CLAUDE.md` so work can continue from another device.
- Draft PR: https://github.com/dusk-futile/mubsir/pull/6 (branch `claude/jolly-fermi-i04fxf`).

### Known problem: CI is red (not caused by the plan)
`Core build and add-on` fails on `main` too: `NU1004 Microsoft.NET.ILLink.Tasks 10.0.12 → 10.0.14`.
The runner's SDK (10.0.403) is newer than the one the lock files were made with. Fix needs a machine
with the .NET 10 SDK (the cloud session couldn't download it):
1. `dotnet restore Mubsir.slnx --force-evaluate`, commit `src/*/packages.lock.json`.
2. In `global.json` set `"version": "10.0.403", "rollForward": "latestPatch"`.

### Owner to-do (can't be done from a Claude session)
- [ ] Make the repo private: GitHub → repo **Settings** → **General** → bottom "Danger Zone" →
      **Change repository visibility** → Private.
- [ ] Create accounts and keys: Anthropic Console, ElevenLabs, Cloudflare (Workers + R2). Set a monthly
      spend limit in each console. Keys go into the Worker as secrets later, **never** into the repo.
- [ ] Decide the pilot's monthly budget (sets per-device daily caps; see PLAN §9 cost table).
- [ ] Optional before the pilot: code-signing certificate (Azure Trusted Signing).

### Next session: start Phase 0 (PLAN §12)
1. Fix CI (above) if a .NET SDK is available.
2. Spike: ElevenLabs Arabic/Egyptian listening test (Flash v2.5 vs Multilingual v2 vs v3).
3. Spike: STT bake-off on Egyptian/English recordings.
4. Spike: Haiku 5.5 tool use against the existing 29 tools; check computer-use support.
5. Then Phase 1: scaffold `cloud/proxy` (Cloudflare Worker: `/register`, `/chat`, `/tts`, `/stt-token`,
   per-device caps, budget alerts, kill switch).
