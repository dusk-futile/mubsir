# Handoff — where the work stands

Update this file at the end of every session. Newest entry on top.

---

## 2026-11-05 — native notch and Hudhud started (PR #14)

The owner approved prototype version 5 (smaller bird, paper-layer shadows) and asked to start the real
thing. Try it on a Windows PC with `Mubsir.exe --overlay-demo`. It runs over the real desktop and listens
to, sends and clicks nothing. Ctrl stops the checklist and undoes it, or wakes Hudhud. Clicking Hudhud
opens the panel, and clicking the notch replays the story. Esc quits.

- **`src/Mubsir.Overlay`** (WPF built in code, on its own UI thread):
  - The notch morphs between states on the prototype's springs (`motion-tokens.json`): idle, peek,
    listening, thinking, doing, pointing, speaking, reading, payment, paid and undone. Voice wave and
    glowing rim are in.
  - The pointer flies out of the notch to a point on the screen, lands with two pulses and names it.
  - Hudhud is the same cut-out puppet as the prototype. `assets/hudhud/rig.json` and the PNGs are shared
    by both and embedded in the assembly.
  - Clicking Hudhud opens a small Mubsir panel (Talk, Read this screen, Settings). A click anywhere else
    closes it, and Hudhud stays awake while it is open.
  - It never takes focus. Its windows stay click-through until the cursor is on the notch or the bird,
    because soft shadows have visible pixels that would otherwise swallow clicks. An error inside the
    overlay can't take the app down: it carries on, and closes itself after three failures.
  - Respects Windows' "Show animations" setting. Draws only as much as it needs: every refresh while
    something really moves, 20 fps for resting breaths, 10 fps for a sleeping bird. Hides over full-screen
    apps while it has nothing to show, and comes back when it does.
  - Hudhud's paper shadows and light edge are baked into its pictures at startup (`PaperBaker` in Core),
    not drawn as live effects: without a GPU, live effects cost 85 % of a core while idle.
- **`Mubsir.Core/Motion`**: spring engine, tokens, rig, pose math and shadow baking, with 29 tests in
  `tests/Mubsir.Core.Tests` (the repo's first .NET unit tests).
- **CI smoke test** (`scripts/overlay-smoke.ps1`, on windows-2025): runs the real demo on a Windows
  desktop, saves screenshots as the `overlay-shots` artifact, tests Ctrl undo, the panel and a clean Esc
  exit, the notch's hover and click, and records memory, CPU and frame rate. Numbers are in
  `docs/RESULTS.md`. This is how to check the native visuals from a Mac. It can't confirm hiding over
  full-screen apps: that machine reports Windows' "quiet time" state instead, so check it on a real PC.
- The zip grows from 62.7 MB to 86.9 MB because the app now ships WPF.

### Next session (normal mode, PLAN phase 7)
1. Owner runs `--overlay-demo` on their Windows PC and says what to change.
2. Wire it to the real app: a normal or blind mode setting (blind mode must create no overlay windows),
   then `UtteranceFlow` (listening, transcript), `TaskRunner` (thinking, doing steps, stop and undo),
   `Narrator` (speaking, voice level) and `Confirmer` (the four Ctrl presses).
3. Still to build: the monitor with the mouse, dragging Hudhud to another corner, the teaching board,
   Arabic for the notch's own labels (the app's words already show right-to-left; "Listening", "Thinking"
   and the like are English for now), and Readex Pro as the font (static TTFs; it uses Segoe UI for now).
4. Measure on a real PC: frame rate, CPU and memory against the app without the overlay
   (`docs/EXECUTION.md`: 60 fps, 0 % idle, < 30 MB extra). CI has no GPU, so its numbers are a worst case.
5. Dependabot: #5 (setup-python) passes and can be merged. #13 (vitest 5) fails the relay tests, so check
   it against `@cloudflare/vitest-plugin` first.

---

## 2026-11-04 (night) — repo cleaned, all open work merged

- **Contributors cleaned.** The six early commits that named the AI assistant as their author were
  rewritten as dusk-futile's, their co-author and session lines removed, and `main` was force-pushed.
  The files are byte-for-byte the same. The v0.1.0 tag still points at the same first commit, and
  stars are not affected. A full backup of the repo from before the rewrite is on the owner's Mac:
  `~/Desktop/mubsir-before-cleanup-2026-11-04.bundle`.
  - The old session branch was deleted, and the tool credit lines were removed from PRs #6 and #8 and
    three old PR comments.
  - GitHub still shows the old commits inside closed PRs #6–#8 (it keeps those refs; only GitHub
    Support can purge them). The 6 forks keep their old copies.
- **Agent files have neutral names:** instructions in `AGENTS.md`, skills in `.agents/skills/`.
  Tools that only look for their own file name need to be pointed at `AGENTS.md`. On the owner's Mac a
  small untracked shim does this (listed in `.git/info/exclude`, so it never reaches the repo).
- **Merged into `main`** (squash, in this order): #10 (CI fix, SDK pinned), #9 (app icon, brand kit,
  notch + Hudhud prototype), #11 (Phase 1 relay, server side).
- **Built and tested** on macOS with SDK 10.0.401:
  - `Mubsir.Core` and `Mubsir.App` (Release): 0 warnings, 0 errors.
  - NVDA add-on built.
  - Relay: type check clean, 37/37 tests.
  - GitHub CI green on all three PRs: Core build and add-on, Windows build, and relay tests.

---

## 2026-11-04 (evening) — Hudhud rebuilt from ChatGPT-drawn parts, notch pass

Owner feedback on version 3: the hand-drawn bird looked blocky and a bit uncanny, the white wing
stripes did not reach the edges, the voice wave looked old. Asked for ChatGPT to draw the layers, more
wing detail, a shake that throws off dust, sleep when idle, a minimal Thinking state, a real Doing
checklist, and Ctrl to stop and undo. Published as version 4 on the owner's link.

- **Hudhud is now a cutout puppet.** ChatGPT (the owner's account, in the desktop app's browser pane)
  drew two part sheets on a lime-green background. The parts were cut out with soft chroma-key edges
  into `assets/hudhud/` (16 PNGs; moved there from the prototype folder so the app shares them). `rig.json` gives each part's joint,
  place and resting angle; `build.py` bakes them into `index.html` as one layered SVG.
  - The crest feathers grow from the crown: they slide forward and fan up when raised and lie back
    when folded. The flight feathers tuck under the wing and spread when it lifts.
  - Owner follow-up (version 5): Hudhud is smaller (92 × 100 px on the 960 px desktop), and every
    part casts a soft shadow on the parts under it, so it reads as stacked paper like the chosen
    concept (`paper_shadow` in `rig.json`).
  - New motion: a full-body shake (wing, crest and tail follow; dust flies off). After 20 s with
    nothing to do, Hudhud sleeps: eyes closed, head tucked, crest flat, slow breath, floating z's. Ctrl
    or a click wakes it with a double blink and a shake. Clicking it when awake opens a small Mubsir
    panel (Talk, Read this screen, Settings).
- **Notch:**
  - The voice wave is three soft glowing ribbons on a canvas, sized to the screen's pixels. A glowing
    rim circles the notch while listening and speaking.
  - Thinking is one breathing light and the word "Thinking".
  - Doing is a real checklist: each step spins while it runs and is ticked only when it is finished.
    Ctrl (or the Stop pill) stops it, undoes the finished steps in reverse on screen, and then says
    "Stopped. Everything is back as it was." Ctrl also calls the pointer back and stops listening,
    speaking and reading.
- Checked:
  - No console errors and 60 fps in the desktop app's browser pane.
  - Scripted runs: Doing then Ctrl undoes in the right order and closes the window; sleep then Ctrl
    wakes it; click opens the panel and Esc closes it.
  - Every notch pane fits its size.
  - Not checked on Windows or on a 120 Hz screen.

### Next session
1. Owner reviews version 4 on the link; fix what they flag.
2. Then the items below (#10 → #9 and #11, relay deploy, Phase 1 app side).

---

## 2026-11-04 (design session) — app icon done, Hudhud concepts ready to pick

- **Image generation works through the owner's own ChatGPT**, signed in inside the coding
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

- **Also opened in this session:**
  - #10 fixes CI by pinning the SDK to 10.0.401. Merge it first; #9 and #11 need it for green checks.
  - #11 is Phase 1's relay, server side, in `cloud/proxy/`: register, Messages pass-through with
    metering, speech with an R2 cache, limits, budget alerts, and pause and block switches. It has
    37 tests and its numbers are in `docs/RESULTS.md`.

### Next session
1. Merge #10, then #9 and #11.
2. Owner: deploy the relay (steps in `cloud/proxy/README.md`; needs the Cloudflare login and the
   Anthropic and Fish keys as Wrangler secrets), or let a session do it with a Cloudflare API token.
3. App side of Phase 1 (on a Windows PC): register on first start and keep the token with the other
   DPAPI secrets; point `AnthropicClient` (`ClaudeBrain.cs`) at the relay with the token as its key;
   speak through `/v1/tts`; handle `quota_exceeded` / `budget_exhausted` / `service_paused` with
   the quiet fallback. Phase 1 is done when the zip runs with zero keys and no account.
4. Phase 0 spikes (listening test, STT bake-off) need the owner's keys and testers.
5. Look and motion: fix anything the owner flags on the prototype link.
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
- The build will run on the owner's Windows PC (a coding agent running there), which
  also solves the "real Windows PC" testing need in EXECUTION.md.
- Added `docs/EXECUTION.md`: build order, what runs where (real Windows PC needed for end-to-end),
  tests and the numbers to report in `docs/RESULTS.md`.
- Plan: notch design (states, motion, performance budget, HTML prototype → WPF), limits policy (free vs
  paid, no "limit reached" messages), full price list and per-user monthly costs (≈ $8 / $25 / $35–60).
- Added skills `taste-skill` and `find-skills` (MIT) to `.agents/skills/`.
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
- Added skills `agent-reach` and `reverse-engineer-anything` to `.agents/skills/` (MIT, sources in
  `.agents/skills/README.md`).
- Added this file and the agent instructions (now `AGENTS.md`) so work can continue from another device.
- Draft PR: https://github.com/dusk-futile/mubsir/pull/6.

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
ask a coding session to do it.

### Owner to-do (can't be done from a coding session)
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
