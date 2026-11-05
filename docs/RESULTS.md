# Results

Measured numbers for each phase (`docs/EXECUTION.md` → "Tests and the numbers to report").
Every row: date, commit, sample size, value, target, pass or fail.

## Softer notch and the reader's features

Measured 2026-11-05 on branch `feat/reading-direct`, macOS, SDK 10.0.401. Windows results are in the CI run for the same commit.

| Test | Sample | Value | Target | Result |
| --- | --- | --- | --- | --- |
| Core unit tests, all | 168 tests | 168 pass | all pass | Pass |
| Mixed Arabic and English commands and searches | 20 sentences | 20 understood | all | Pass |
| Book search when the AI fails, returns nothing or is off | 3 runs | plain search still answers | still answers | Pass |
| Notch opening to listening, overshoot | spring model | 9.6 px before, 0.8 px after | under 2.5 px | Pass |
| Notch opening to listening, time to within 2 px | spring model | 0.28 s before, 0.34 s after | under 0.36 s | Pass |
| Page images for a clean book | 100 pages | 0 | 0 | Pass |
| Page images for a broken book | 50 pages | 3 | 3 or fewer in a row | Pass |
| Page images, broken and good pages mixed | 60 pages | 10 | 10 or fewer a day | Pass |
| Text sent for a question | 400-page book | under 8,000 characters | about 2,000 tokens | Pass |
| Windows overlay smoke test, before the features | run 42002603609 | passed; asleep 8.1 % of one core, against 2.5–3.4 % before | no target on this machine | Check on the next run |

Not measured yet: `--book-check` on Windows, using any of this by ear, and how long a real library scan takes.

## Reading fixes: straight to the voice, bookmarks by file, place saved when heard

Measured 2026-11-05 on branch `feat/reading-direct`, macOS, SDK 10.0.401. Speech is a fake voice on the real `SpeechHistory` and splitter.

| Test | Sample | Value | Target | Result |
| --- | --- | --- | --- | --- |
| Core unit tests, all | 62 tests | 62 pass | all pass | Pass |
| AI calls after a book starts reading | 1 run with a fake brain | 0 | 0 | Pass |
| Place saved before any sentence is heard | 1 run | never | never | Pass |
| Bookmark after a rename, a move, an edit, and from an old file | 1 run each | kept in all 4 | kept | Pass |
| Release build of the solution | Core, Overlay, App, tests | 0 warnings, 0 errors | 0 | Pass |

Not measured yet (needs a Windows PC): reading a real book through NVDA and OneCore, and stopping it with Ctrl.

## Phase 7, started: native notch and Hudhud (`src/Mubsir.Overlay`)

Measured 2026-11-05 on branch `feat/native-notch`. Unit tests and sizes on macOS with SDK 10.0.401.

| Test | Sample | Value | Target | Result |
| --- | --- | --- | --- | --- |
| Core unit tests (springs, tokens, rig, pose math, shadow baking) | 29 tests | 29 pass | all pass | Pass |
| Release build of the solution | Core, Overlay, App, tests | 0 warnings, 0 errors | 0 | Pass |
| Size of the Windows zip, before and after WPF | 1 publish each, zipped at level 9 | 62.7 MB → 86.9 MB | no target yet | — |

Smoke test on a Windows desktop (`scripts/overlay-smoke.ps1`, windows-2025, commits `43ab4b4` and `a2fd950`,
1024 x 768, no real GPU, Windows animations turned on for the run). The CPU does the drawing here, so CPU and
frame rate are a worst case. They still need measuring on a real PC. Ranges are the two runs.

| Test | Sample | Value | Target | Result |
| --- | --- | --- | --- | --- |
| Whole demo story, Ctrl undo, panel opens and closes, panel button takes a click, notch peeks on hover and listens on a click, clean exit, no overlay errors | 2 runs | all pass | all pass | Pass |
| Hudhud and the full-screen app | 1 full-screen window | not hidden; Windows reported state 6 ("quiet time", a new user's first hour) instead of a full-screen state | hidden, then back | Not confirmed: this machine can't trigger it. Check on a real PC |
| CPU, awake and idle | 5 s, 2 runs | 10–20 % of one core (higher if Hudhud does an idle move during the sample) | 0 % idle (PLAN), on a real PC | Not met here |
| CPU, asleep | 5 s, 2 runs | 2.5–3.4 % of one core | 0 % idle (PLAN), on a real PC | Not met here |
| CPU, awake and idle, before baking the shadows and slowing resting frames | 5 s | 85.3 % of one core | — | — |
| Memory after the demo story | 2 runs | 119–122 MB working set, 65–73 MB private | < 30 MB extra (PLAN), against the app without the overlay | Not measured yet |
| Memory, asleep | 2 runs | 119–124 MB working set, 65–70 MB private | as above | Not measured yet |
| Frames a second, every 5 s through the first run | 1 run | 44, 53, 50, 51, 47, 45, 37, 33, 26, 41, 10, 9, 23, 35, 31, 34 | 60 while moving (PLAN), on a real PC | Not met here |

The frame rates cover the story, a resting bird (20 a second by design), a sleeping one (10 by design), and
then the panel and the replay.

Not measured yet (needs a real Windows PC): frame rate and CPU with a GPU, memory against the app without the
overlay, and hiding over a real full-screen app. `Mubsir.exe --overlay-demo` prints its frame rate to a console.

## Phase 1: relay, server side (`cloud/proxy`)

Measured 2026-11-04 on branch `feat/cloud-relay`, macOS, Node 22.23, vitest 4.1.12 running the tests
inside the Workers runtime (Miniflare, `@cloudflare/vitest-plugin` 1.5.1). Anthropic, Fish Audio and the
alert webhook are mocked; no keys and no network.

| Test | Sample | Value | Target | Result |
| --- | --- | --- | --- | --- |
| Relay test suite | 37 tests in 4 files | 37 pass | all pass | Pass |
| Streaming pass-through is byte-identical (Arabic text in the stream) | 5 chunk sizes (1, 3, 7, 64, 100,000 bytes) | identical in 5 / 5 | identical | Pass |
| Usage read from the stream matches the stream | same 5 runs | input 120, cache read 800, output 42 in 5 / 5 | exact | Pass |
| Cost maths matches Anthropic's price table | 8 cases incl. Haiku 5.5's two rate cards, 5-min / 1-hour cache writes, Opus 5.5 cache reads | exact (integer maths, no float drift) | exact | Pass |
| Daily limits stop before calling the provider | requests and speech bytes | 0 provider calls after the limit | 0 | Pass |
| Budget alerts | 3 requests crossing 50 % and 100 % | 1 alert per threshold, then 402 | 1 each | Pass |
| Worker bundle (`wrangler deploy --dry-run`) | 1 build | 23.7 KB | < 3 MB (free plan) | Pass |
| Type check (`tsc --noEmit`, strict) | src + tests | 0 errors | 0 | Pass |

Not measured yet (needs the deployed relay and the owner's keys): time the relay adds to the first
audio and the first token from Cairo, and real cost per action against PLAN §9.
