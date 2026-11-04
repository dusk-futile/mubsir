# Results

Measured numbers for each phase (`docs/EXECUTION.md` → "Tests and the numbers to report").
Every row: date, commit, sample size, value, target, pass or fail.

## Phase 7, started: native notch and Hudhud (`src/Mubsir.Overlay`)

Measured 2026-11-05 on branch `feat/native-notch`. Unit tests and sizes on macOS with SDK 10.0.401.

| Test | Sample | Value | Target | Result |
| --- | --- | --- | --- | --- |
| Core unit tests (springs, tokens, rig, pose math, shadow baking) | 29 tests | 29 pass | all pass | Pass |
| Release build of the solution | Core, Overlay, App, tests | 0 warnings, 0 errors | 0 | Pass |
| Size of the Windows zip, before and after WPF | 1 publish each, zipped at level 9 | 62.7 MB → 86.9 MB | no target yet | — |

Smoke test on a Windows desktop (`scripts/overlay-smoke.ps1`, windows-2025, commit `43ab4b4`, 1024 x 768,
no real GPU, Windows animations turned on for the run). The CPU does the drawing here, so CPU and frame rate
are a worst case. They still need measuring on a real PC.

| Test | Sample | Value | Target | Result |
| --- | --- | --- | --- | --- |
| Whole demo story, Ctrl undo, panel opens and closes, panel button takes a click, clean exit, no overlay errors | 2 runs | all pass | all pass | Pass |
| Hudhud and the full-screen app | 1 full-screen window | Hudhud hidden while it runs: False; back afterwards: True | hidden, then back | Not confirmed: the test window may not count as full-screen on this machine |
| CPU, awake and idle | 5 s | 10 % of one core | 0 % idle (PLAN), on a real PC | Not met here |
| CPU, asleep | 5 s | 3.4 % of one core | 0 % idle (PLAN), on a real PC | Not met here |
| CPU, awake and idle, before baking the shadows and slowing resting frames | 5 s | 85.3 % of one core | — | — |
| Memory after the demo story | 1 run | 122.3 MB working set, 73.1 MB private | < 30 MB extra (PLAN), against the app without the overlay | Not measured yet |
| Memory, asleep | 1 run | 118.7 MB working set, 64.8 MB private | as above | Not measured yet |
| Frames a second, every 5 s through the first run | 1 run | 46, 53, 50, 53, 48, 47, 27, 29, 27, 45, 10, 9, 21, 47, 28, 30 | 60 while moving (PLAN), on a real PC | Not met here |

The frame rates cover the story, then a resting bird (20 a second by design) and a sleeping one (10 by design).

Not measured yet (needs a real Windows PC): frame rate and CPU with a GPU, and memory against the app without
the overlay. `Mubsir.exe --overlay-demo` prints its frame rate to a console.

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
