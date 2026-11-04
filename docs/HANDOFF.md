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

### CI: lock file fix pushed
`Core build and add-on` was red on `main` too: `NU1004 Microsoft.NET.ILLink.Tasks 10.0.12 → 10.0.14`.
The runner's SDK (10.0.403) adds the implicit ILLink reference at 10.0.14. The .NET SDK could not be
downloaded in the cloud session, so `src/Mubsir.Core/packages.lock.json` was updated by hand to
10.0.14 with the hash computed from the nuget.org package. Check the PR's CI result.

This will happen again whenever the runner gets a newer SDK patch. Real fix, on a machine with the
.NET 10 SDK: `dotnet restore Mubsir.slnx --force-evaluate` and commit the lock files. To stop it
recurring, pin the SDK in `global.json` (e.g. `"version": "10.0.403", "rollForward": "disable"`) and
let Dependabot bump it.

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
