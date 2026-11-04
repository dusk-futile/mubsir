# Handoff — where the work stands

Update this file at the end of every session. Newest entry on top.

---

## 2026-11-04 (design session) — app icon done, Hudhud concepts ready to pick

- **Image generation works through the owner's own ChatGPT**, signed in inside the Claude desktop
  app's browser pane. No API key was needed and none is in the repo. (Codex CLI's image tool also
  works with the owner's ChatGPT login, but the owner rejected its quality.)
- **App icon chosen: the seeing eye.** The owner picked it from two rounds of six concepts: an amber eye on
  Night navy, iris = sound rings, lower lid sweeping into the tail of م. Hudhud stays the companion
  (owner: "eye icon + Hudhud"). Details and rules: `docs/BRAND.md`.
- Built on branch `brand/eye-icon-hudhud`:
  - `assets/brand/`: SVG masters (full; medium for 24–48 px; small for 16–20 px; tile-less mark),
    `png/` 16–1024 px, `mubsir.ico` (9 sizes, each from the right drawing), `brand-sheet.png` and
    `social-preview.png` with their HTML sources.
  - `scripts/build-icons.py` rebuilds the PNGs and the `.ico` from the SVGs (Pillow + Chrome/Edge).
  - App: `Mubsir.App.csproj` embeds the icon (`ApplicationIcon`). `TrayIcon` loads it at the tray
    size for the DPI (icon group 32512) and falls back to the system icon. The README shows the logo.
  - Checked on macOS with SDK 10.0.401: `dotnet build src/Mubsir.App -c Release` → 0 errors,
    0 warnings; `Mubsir.dll` holds RT_GROUP_ICON 32512 with 9 images; every `.ico` frame matches its PNG.
  - **Not checked on Windows yet:** the tray icon at 100 / 125 / 150 / 200 % scaling and the exe icon
    in Explorer. Resources are copied into `Mubsir.exe` only when building on Windows (the release
    workflow does).
- **Hudhud concepts**, all navy/amber with the icon's sound-wave eye: 6 characters and 3
  companion-card states in `prototypes/hudhud-companion/assets/concepts/`. **Owner chose concept 4,
  the layered paper-cut hoopoe** (`hudhud-4-paper-cut.jpg`).
- **Prototype rebuilt** (`prototypes/hudhud-companion/index.html`, republished to the owner's link,
  version 2). Owner asked for Apple-smooth motion and an iPhone-style notch:
  - **The notch** hangs from the top edge (concave fillets like a MacBook notch) and pours out into
    each state; its content cross-fades with a 10 px blur. It carries the app icon's eye, whose rings
    ripple while listening and speaking.
  - **Hudhud** is the paper-cut hoopoe redrawn as layered SVG (body, bib, wing, tail, legs, head,
    6 crest feathers, beak with a moving jaw, eye with pupil). It breathes, blinks (sometimes twice),
    glances up at the notch, opens its crest when listening, nods when thinking, looks down when
    reading, raises its crest for payments and hops when paid.
  - **Motion** is a small spring engine with Apple's parameters (response, damping). Every value
    starts from where it is on screen, so states interrupt each other without jumps. The motion tokens
    are listed on the page and are meant to be reused by the WPF overlay.
  - States from the first prototype are kept (idle, listening, thinking, doing, pointing, speaking,
    reading, payment with 4× Ctrl, paid). A link can open one state: `#listening`, `#payment`, etc.
  - Checked: no console errors, 60 fps in the desktop app's browser, all states laid out at 2× with
    reduced motion. Not checked on a 120 Hz screen or on Windows.
  - Owner feedback, same day (published as version 3):
    - No backend wording in the UI; the reading line now says what the user can say.
    - Hudhud is smaller (116 px) with no ground shadow, visible legs with toes, and a detailed wing (three
      flight feathers under a striped covert) that lifts and spreads.
    - New idle moves: turns its head round to scratch its back while its tail wiggles, wing stretch,
      glance, crest flick, and a hop with squash and stretch. It flaps when a payment is done.
    - The stage scales with CSS zoom so text stays sharp.
    - The page states that the notch is drawn by Mubsir, so it works on any PC.
- Skills used this session: `apple-design` and `improve-animations` (Emil Kowalski, installed for
  this Mac with `npx skills add emilkowalski/skills@<name> -g`), plus `find-skills`.
- **Owner to-do:** upload `assets/brand/social-preview.png` in GitHub → Settings → General →
  Social preview (GitHub has no API for it).

### Next session
1. Owner reviews the prototype link. Fix anything they flag in the look or motion.
2. Then the backend, following `docs/EXECUTION.md` (owner: "show me first the UI, then work on the
   backend").
3. Later, for the native overlay: port the notch and Hudhud to WPF with the same springs.

---

## 2026-11-04 (end of planning chat)

- Full summary of the planning conversation: `docs/CONVERSATION-SUMMARY.md`.
- **Owner's verdict on the Hudhud prototype: it looks bad.** Keep its behaviour (states, flow,
  bottom-right corner, 4× Ctrl payment), redo the look.

### Next session: redesign Hudhud with image generation
The owner has a ChatGPT subscription and will give the new chat access to it (e.g. an OpenAI API key
as an environment secret, or a connected tool). Plan:
1. Generate 6–10 concepts of the **Hudhud character** (original hoopoe, not like any known app mascot):
   cute but premium, readable at 32–80 px, crest that can open/close, works on dark glass.
   Also 3–4 concepts of the **bottom-right companion card** (listening, doing, payment confirm).
2. Show them to the owner; iterate on the favourite (pose sheet: idle, listening/crest open,
   speaking, thinking, happy/done, alert).
3. Turn the chosen character into clean vector art (SVG layers: body, wing, head, crest feathers,
   beak, eye) so it can be animated; never ship raw generated bitmaps as the animated mascot.
4. Rebuild `prototypes/hudhud-companion/index.html` with it, use `taste-skill`, republish the owner's
   link (https://claude.ai/artifact/UpBpC7m8hzQ3VCryB9LvyC) and get approval.
5. Keys never go in the repo; generated images that are kept go in `prototypes/hudhud-companion/assets/`.

---

## 2026-11-04 (later) — Book preparation added to the plan

- PLAN §6 "Book preparation": extract and fix each book once locally, cache voice audio just ahead of
  the reader, free speed changes, local search index so questions send only a few passages to Haiku,
  4 GB RAM limits. Roadmap phase 4 includes it.
- Owner confirmed they have the rights to the books: shared library on the owner's server (prepared
  text + voice audio) so each book is voiced once for all users. Plan §6 has an example session with
  per-step costs and a 5-book cost estimate (≈ $100 first reader with Fish, ≈ $2–3 after).
- Reading check-in: continuous reading pauses after 2 minutes ("Shall I continue?"), stops after 15 s
  of silence. Pay only for what is read: voice at most ~30 s ahead and never past the requested range
  (pages 46–80, first quarter of a page, N lines, a section, a time). Plan §6 has per-page costs.
- Library and page window: every book gets a free "library card" (metadata, first pages, TOC; Haiku names
  untitled files from text, ≈ $0.20 per 400 books). The open book prepares only previous / current / next
  page. Numbered choices (max 4), numbered starting points. Shared library stores audio by exact sentence
  text + voice; the server generates everything itself; personal data stays on the user's PC.
- **Hudhud chosen** as the identity; normal-mode companion lives **bottom-right**. Working prototype in
  `prototypes/hudhud-companion/index.html`, also published for the owner at
  https://claude.ai/artifact/UpBpC7m8hzQ3VCryB9LvyC (private link; republish from the file to update).
- Code signing: try free routes first: Microsoft Store (MSIX, Microsoft signs) or SignPath Foundation
  (free for open-source). PLAN §8.
- Usage is irregular (some users read 20 minutes one day, then nothing for a week), so real cost per
  user is usually below the planning tiers; idle users cost $0.
- Owner allowed the build to restructure or replace old code, keeping the best parts; the repo and its
  stars must never be deleted.
- Owner decisions (latest): repo stays **public** (private would erase 71 stars); commits authored only
  as dusk-futile, no AI attribution lines; **no daily switch to Windows voices** (premium always, only an
  abuse guard + monthly budget alerts); password dictation with spelled read-back and corrections;
  full mouse/keyboard parity (windows, websites, settings, text). Brand options in `docs/BRAND.md`
  (recommended: Fanoos lantern) — owner to choose.
- The build will run on the owner's Windows PC (Claude Desktop or `claude remote-control` there), which
  also solves the "real Windows PC" testing need in EXECUTION.md.
- Added `docs/EXECUTION.md`: build order, what runs where (real Windows PC needed for end-to-end),
  tests and the numbers to report in `docs/RESULTS.md`.
- Plan: notch design (states, motion, performance budget, HTML prototype → WPF), limits policy (free vs
  paid, no "limit reached" messages), full price list and per-user monthly costs (≈ $8 / $25 / $35–60).
- Added skills `taste-skill` and `find-skills` (MIT) to `.claude/skills/`.
- Owner decisions: **no multi-device sync** (one user = one PC). The owner's own PC runs nothing for
  users: users' PCs do the local work and storage, a Cloudflare relay holds keys and forwards to
  Claude / Fish (PLAN §9 "Where things run"). Full-access setup with a one-time admin approval for a
  small Mubsir Helper service (PLAN §8).
- Font map trick: learn a legacy IPA font's code map from 1–3 vision-fixed pages, fix the rest locally.
- Optional on-device wake word "Hey Mubsir" / "يا مبصر" (Right Ctrl stays the default).
- Repo stays **public** to keep its 71 stars (making it private would erase them).

---

## 2026-11-04 — Plan approved, nothing built yet

### Done
- Voice decision changed: **Fish Audio** is the lead (hosted API, cheaper, strong in blind tests),
  confirmed by a week-1 listening test. Fish open weights rejected for now (research licence, needs a GPU).
- Plan §6 now has "Reading anything": Arabic speaker + English book, IPA/phonetic symbols, broken PDF fonts.
- Plan §1 now has a Clicky / Saywork / Mubsir comparison table; §10 lists what a user's PC needs.
- Researched Clicky (open source, read its code) and Saywork (store listing only). Findings in `docs/PLAN.md` §1.
- Wrote `docs/PLAN.md` (Mubsir 2.0 plan). The owner approved it, then locked these decisions:
  free for users, zip distribution with no keys, Windows only, ElevenLabs everywhere (later changed to Fish Audio, see above), full access with
  a 4× Right Ctrl gesture for passwords and payments, name stays Mubsir.
- Added skills `agent-reach` and `reverse-engineer-anything` to `.claude/skills/` (MIT, sources in
  `.claude/skills/README.md`). `.gitignore` now tracks `.claude/skills/` only.
- Added this file and `CLAUDE.md` so work can continue from another device.
- Draft PR: https://github.com/dusk-futile/mubsir/pull/6 (branch `claude/jolly-fermi-i04fxf`).

### Fixed (2026-11-04, design session): CI was red
Fixed by pinning `global.json` to exactly SDK 10.0.401 with `rollForward: disable`, so CI installs the SDK
the lock files were made with. A locked restore (`CI=true`) of Core and App passes with that SDK and leaves
the lock files unchanged. To move to a newer SDK later: install it, run
`dotnet restore Mubsir.slnx --force-evaluate`, and commit the lock files together with the new version in
`global.json`. The original notes follow.

`Core build and add-on` fails in locked restore: `NU1004 Microsoft.NET.ILLink.Tasks 10.0.12 → 10.0.14`.
The runner's .NET SDK 10.0.403 adds the implicit ILLink reference (from `IsTrimmable` /
`IsAotCompatible` in `Mubsir.Core.csproj`) at 10.0.14, but the lock file says 10.0.12.

A hand edit to 10.0.14 with the nuget.org hash was tried and reverted: CI then failed with `NU1403`
(hash mismatch), because the runner takes the package from its own SDK, whose bytes differ from
nuget.org's. The lock file must be regenerated by the same official SDK. The cloud session could not
download it (Microsoft download hosts are blocked; Ubuntu's 10.0.114 SDK is a different build).

Fix, on a machine with the official .NET SDK 10.0.403 (Windows is fine):
1. `dotnet restore Mubsir.slnx --force-evaluate`, commit `src/*/packages.lock.json`.
2. To stop it recurring on every SDK patch, pin the SDK in `global.json`
   (`"version": "10.0.403", "rollForward": "disable"`) and let Dependabot bump it.
Alternatively, allow `builds.dotnet.microsoft.com` in the cloud environment's network settings and
ask Claude to do it.

### Pending: remove Claude from the contributors list
The owner approved rewriting history so every commit is authored by dusk-futile (no Claude author, no
Claude trailer lines). The rewrite was prepared, but force-pushing `main` was blocked by the cloud
session's safety rules. To finish it (stars are not affected; the 6 forks keep the old copy), on a PC
with git, in a fresh clone of the repo:

```bash
git clone https://github.com/dusk-futile/mubsir.git && cd mubsir
git filter-branch -f --env-filter 'if [ "$GIT_AUTHOR_EMAIL" = "noreply@anthropic.com" ]; then export GIT_AUTHOR_NAME="dusk-futile" GIT_AUTHOR_EMAIL="omarelkabbaney2010@gmail.com"; fi; if [ "$GIT_COMMITTER_EMAIL" = "noreply@anthropic.com" ]; then export GIT_COMMITTER_NAME="dusk-futile" GIT_COMMITTER_EMAIL="omarelkabbaney2010@gmail.com"; fi' --msg-filter 'grep -v -E "^(Co-Authored-By: Claude|Claude-Session: )"' -- --all
git push --force origin main
```

Or allow force-push for that session (a Bash permission rule) and ask Claude to do it.

### Owner to-do (can't be done from a Claude session)
- [x] ~~Make the repo private~~ — **cancelled**: GitHub permanently erases stars and watchers when a
      public repo is made private. The repo stays public; never commit keys.
- [ ] Create accounts and keys: Anthropic Console, ElevenLabs, Cloudflare (Workers + R2). Set a monthly
      spend limit in each console. Keys go into the Worker as secrets later, **never** into the repo.
- [ ] Decide the pilot's monthly budget (sets per-device daily caps; see PLAN §9 cost table).
- [ ] Optional before the pilot: code-signing certificate (Azure Trusted Signing).

### Next session
- "Show me the UI" → open `prototypes/hudhud-companion/index.html` (or the link above) and walk through
  the states; the ▶ Play button runs the whole flow.
- "Start working" → follow `docs/EXECUTION.md` from step 0.

### Phase 0 details (PLAN §12)
1. Fix CI (above) if a .NET SDK is available.
2. Spike: ElevenLabs Arabic/Egyptian listening test (Flash v2.5 vs Multilingual v2 vs v3).
3. Spike: STT bake-off on Egyptian/English recordings.
4. Spike: Haiku 5.5 tool use against the existing 29 tools; check computer-use support.
5. Then Phase 1: scaffold `cloud/proxy` (Cloudflare Worker: `/register`, `/chat`, `/tts`, `/stt-token`,
   per-device caps, budget alerts, kill switch).
