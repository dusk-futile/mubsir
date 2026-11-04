# Execution playbook

Written 2026-11-04 for the session that will **build** Mubsir 2.0. The owner plans on one device and
will start the build from another, with Claude at a high effort setting. Read `CLAUDE.md`,
`docs/HANDOFF.md` and `docs/PLAN.md` first. This file says **in what order to build, how to test, and
which numbers to report**.

## Ground rules for the build session

- Follow `docs/PLAN.md`. Owner decisions in `CLAUDE.md` are final; don't re-ask them.
- Work in phases (PLAN §12). Each phase: branch → code → tests → numbers in `docs/RESULTS.md` →
  update `docs/HANDOFF.md` → PR. Don't start a phase before the previous one's "done when" passes,
  unless the owner says so.
- **Never commit keys.** Test keys come from environment secrets (`ANTHROPIC_API_KEY`,
  `FISH_API_KEY`, `ELEVENLABS_API_KEY`, `STT_API_KEY`, `CLOUDFLARE_API_TOKEN`).
- **Spending money in tests:** every test that calls a paid API prints its expected cost first. Keep
  each phase's test spend under **$5** unless the owner raises it.
- Report numbers, not adjectives: every claim in a PR or in `RESULTS.md` has a measured value, the
  sample size, and how it was measured.

## What can run where (important)

| Work | Cloud Claude session (Linux) | GitHub Actions `windows-2025` | Owner's / tester's real Windows PC |
| --- | --- | --- | --- |
| `Mubsir.Core` code + unit tests | ✅ | ✅ | ✅ |
| Cloudflare relay (`cloud/proxy`) code, tests, deploy | ✅ (needs Cloudflare token) | — | — |
| STT bake-off, voice tests, IPA tests, cost measurements (API calls) | ✅ (needs keys + network allow-list) | ✅ | ✅ |
| Notch HTML prototype + screenshots (taste-skill, Playwright) | ✅ | — | — |
| Build the Windows app, WPF notch | — | ✅ build + unit tests | ✅ |
| Real end-to-end: microphone, NVDA, UI Automation on real apps, latency, RAM on a 4 GB PC | — | ❌ (no mic/NVDA) | ✅ **only here** |

So the final checks of each Windows phase need a real Windows PC: either the owner runs Claude Code on
a Windows PC, or a tester runs the test script and sends back `results.json`. The cloud session should
also allow these hosts in its network settings: `builds.dotnet.microsoft.com` (to fix CI),
`api.anthropic.com`, the Fish Audio API host, the chosen STT host, `api.cloudflare.com`.

## Order of work

0. **Fix CI** (HANDOFF "Known problem"): regenerate lock files with the official .NET SDK 10.0.403, pin
   it in `global.json`. CI green before anything else.
1. **Phase 0 spikes** (PLAN §12) → write the decisions with numbers into `RESULTS.md`.
2. **Phase 1 relay** → deploy to the owner's Cloudflare account (owner provides the token).
3. Phases 2 → 8 in order. The notch HTML prototype (phase 7, step 1) can be done early, in parallel,
   because it needs no Windows: publish it so the owner can look at it on a phone.

## Tests and the numbers to report

Put every result in `docs/RESULTS.md` as a table with: date, version (commit), sample size, value,
target, pass/fail.

### Voice and language

| Test | How | Report | Target |
| --- | --- | --- | --- |
| STT accuracy | 50 real Egyptian Arabic / English / mixed recordings (owner or testers record them; transcripts written by a person) × each STT candidate | Word error rate (WER) and character error rate per language and per vendor; time to final transcript | Egyptian WER as low as possible; pick the best per $ |
| TTS quality | Blind listening test, ~10 testers, same passages, providers hidden, normal and 2× speed | Mean opinion score 1–5 per voice / language / speed; preference % | Choose winner per job |
| TTS speed | 100 requests per provider from a Cairo connection | Time to first audio p50 / p95 | < 300 ms p50 for conversation |
| IPA / symbols | 30-page symbol test set (PLAN §6 "Reading anything") with a hand-made answer key | % of symbols read correctly by name; % pronounced correctly per voice | 100 % by name; report sound mode as measured |
| Broken PDF fix | Pages with legacy IPA fonts; ground truth typed by hand | Character error rate after vision fix and after font map; % of pages needing vision | CER < 0.5 %; vision on < 5 % of pages |

### Agent and PC control

| Test | How | Report | Target |
| --- | --- | --- | --- |
| Task success | 50-task list (open settings, find a book by its contents, read TOC, go to page 1400, update then shut down, play a song, save a password, …) run 3 times each | Success %, average steps, cost per task, time per task, Haiku vs Sonnet share | ≥ 90 % success |
| Safety gate | 20 prompt-injection pages / emails that try to trigger sends, payments, password fills | % blocked; any level-3 action without the 4× Right Ctrl gesture | 100 % blocked, 0 bypasses |
| Book finding | 400-book folder incl. 50 untitled scans | % found in the top 4 choices; cost for library cards | ≥ 95 % top-4 |

### Speed, weight, cost

| Test | How | Report | Target |
| --- | --- | --- | --- |
| Conversation latency | 100 commands on a real PC | Key → earcon; end of speech → first audio (p50 / p95); interrupt time | < 100 ms; < 1.2 s p50; < 150 ms |
| Book open | 1,500-page PDF | Time to open at page 1,400; RAM peak while preparing | < 3 s; < 150 MB |
| Weight on a 4 GB PC | 1 hour of mixed use | Idle RAM / CPU, peak RAM, disk used | < 80 MB idle, ~0 % CPU |
| Notch | Scripted run through all states | Frame rate while animating, CPU idle, extra RAM, GIF | 60 fps, 0 % idle, < 30 MB |
| Real cost | Every test run | Measured $ per action vs PLAN §9 price list | Within ±30 % of the estimate, else update the plan |

## Reporting to the owner

At the end of each phase, give the owner (in plain language, short):
1. What now works, in one or two sentences.
2. The key numbers from `RESULTS.md` (pass / fail against targets).
3. What it cost to test, and the updated monthly cost estimate.
4. What is next and anything the owner must do (keys, testers, approvals).

## Quality bar ("flawless")

The owner's goal is code that does not break. Concretely, every PR must meet all of these:

- **Builds with zero warnings** (`TreatWarningsAsErrors` on for new projects), nullable on, .NET analyzers
  on at the "recommended" level.
- **Tests first for logic**: every `Mubsir.Core` change ships with unit tests (xUnit). Parsers, the
  policy engine, the symbol reader, the page checks and the cost guards get table-driven tests with
  real samples, including Arabic, mixed text and broken input. Coverage of `Mubsir.Core` ≥ 85 %.
- **Never crash the user's PC session**: every background task has a timeout and a catch that logs an
  error code and recovers; the existing `Supervisor` restarts the app; the emergency stop always works.
- **Review before merge**: run the `code-review` and `security-review` skills on each PR, and fix every
  real finding. Safety-gate code (passwords, payments, admin helper) gets a second, dedicated review.
- **Measured, not assumed**: performance and accuracy claims come with numbers in `RESULTS.md`.
- **If something is wrong, find the root cause and fix it**, then add a test that would have caught it.
