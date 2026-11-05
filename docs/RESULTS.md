# Results

Measured numbers for each phase (`docs/archive/EXECUTION.md` → "Tests and the numbers to report").
Every row: date, commit, sample size, value, target, pass or fail.

## Free hearing against Gemini, in Arabic

Measured 2026-11-05 on an Intel i3-8100, 2 threads. 4 Fish voice samples with known text, in formal Arabic, Egyptian and Saudi. They're clear computer voices, so real speech will be harder for all of them. Words wrong after ignoring tashkeel and hamza.

| Engine | Words wrong | Speed | RAM | Runs on the PC |
| --- | --- | --- | --- | --- |
| Whisper tiny | 72% | 7x | 414 MB | Yes |
| Whisper base | 61% | 3.6x | 541 MB | Yes |
| Whisper small | 39% | 1.1x | 1,077 MB | Yes, but only just keeps up |
| Gemini 3.5 Flash-Lite | 7% | about 3 s per sample | none | No |

Handy is a dictation app built on Whisper and Parakeet, so it isn't a better engine than these. Fish's hearing hasn't been measured.

## Gemini brain against the real service

Measured 2026-11-05 from macOS with a free-tier key and the app's real `GeminiWire` and `SystemPrompt`. `GeminiBrain` itself and the Windows tools weren't run.

| Test | Sample | Value | Result |
| --- | --- | --- | --- |
| All tools accepted, thought signatures sent back, multi-step tasks | 4 tasks | no format errors | Pass |
| "افتح يوتيوب" gets a tool call first time on `gemini-3.5-flash-lite` | 12 | 5 | Fail, it said it acted without acting |
| The same after forcing a tool (`AsksForAction`) | 7 misses | 7 fixed | Pass |
| Vision finds a known spot | 1 each | Flash-Lite 755,257, Flash 759,265, real about 760,267 | Pass |
| Page text saying to delete files | 3 | no dangerous call | Pass |
| Describing a book | 1 | valid JSON | Pass |
| Arabic hearing, Piper Kareem's voice, not a real speaker | 2 | words wrong, 7 to 12 s | Weak test, check with real speech |
| Availability | about 60 requests | 503 busy several times on Flash-Lite and Flash, 429 on Flash, `gemini-3.8-flash` hung 90 s | Retries matter |
| `gemini-2.5-flash-lite` | 1 | 404, closed to new users | Not usable |

Fish's free model wasn't tested: there's no Fish account signed in.

## Full PC control, numbered results, book descriptions, IPA, voices

Measured 2026-11-05 on macOS, SDK 10.0.401. Nothing here has run on Windows. `docs/WINDOWS-TEST.md` rows 16 to 22 prove it.

| Test | Sample | Value | Target | Result |
| --- | --- | --- | --- | --- |
| Core unit tests, all | 440 tests | 440 pass | all pass | Pass |
| Release build of the solution | Core, Overlay, App, tests | 0 errors, 0 warnings | 0 | Pass |
| Voice sample links work with no key | 14 voices | 14 return 200 | all | Pass |

Local voices, one 10 to 15 second passage each, on an Intel i3-8100 (4 cores, 3.6 GHz), macOS, sherpa-onnx. Above 1x it can read live. RAM is the whole Python process, so the app would differ.

| Voice | On disk | Threads | Speed | Peak RAM | Result |
| --- | --- | --- | --- | --- | --- |
| Kokoro multi-lang v1.0, int8 | 182 MB | 2 | 0.5 to 0.6x | 418 MB | Too slow to read live |
| Kokoro multi-lang v1.0, int8 | 182 MB | 4 | 0.3x | 333 MB | Too slow to read live |
| Piper Ryan, English, high, the app's voice, at 90% speed | 133 MB | 2 | 2.2x | 326 MB | Fast enough here, check on a weak PC |
| Piper Amy, English, medium | 79 MB | 2 | 9.2x | 294 MB | Fast enough |
| Piper Ryan, English, medium | 78 MB | 2 | 12.1x | 232 MB | Fast enough |
| Piper Kareem, Arabic, medium | 78 MB | 2 | 7.9x | 315 MB | Fast enough |

Sherpa-onnx 2.0 could not phonemize Kokoro v0.19 or Piper here, so Piper ran on 1.12.40. Piper needs espeak-ng, which is GPL-3.0. Ryan is now in the app as `LocalVoice`. The Windows run of `--book-check` proves the engine works there, and nobody has heard it there yet.

## Own-keys app, setup, teaching and audit fixes

Measured 2026-11-05 on macOS, SDK 10.0.401, on branch `feat/reading-direct`. Nothing below has run on Windows or by ear. `docs/WINDOWS-TEST.md` lists what proves each part.

| Test | Sample | Value | Target | Result |
| --- | --- | --- | --- | --- |
| Core unit tests, all | 404 tests | 404 pass | all pass | Pass |
| Teaching blocks action tools | 7 tools | all blocked by the policy | all | Pass |
| A first conversation with answers it can't understand | 30 tries | finishes on safe defaults | can't get stuck | Pass |
| Release build of the solution | Core, Overlay, App, tests | 0 errors | 0 | Pass |

Windows, CI run 42031100660, commit `126d812`, windows-2025 (no real user):

| Test | Value | Result |
| --- | --- | --- |
| Book check | 11 of 12 checks pass. Arabic letters, English scans, resume, search and one end-of-book line all right. Lost letter `قرأ` not filled; fix pushed after this run. | 1 fail |
| Arabic OCR misread | `ورفي` for `ورقي`, 1 letter of 2 phrases | Allowed by the check, not fixed |
| Setup window | Opens in all 6 demo stages, in English and right-to-left Arabic | Pass, by picture |
| Store package | `Mubsir.msix` built, 88 MB, never installed | Built |
| Overlay at rest | 121 MB working set, 5 % of one core asleep, 13.1 % awake | Above the 0 % target, on a machine with no real GPU |

## First real book check on Windows, and its fixes

Run 42005451328, commit `0836a33`, windows-2025, `--book-check`. Arabic OCR was installed; the Arabic voice was not. This run read the fixtures through the real code on Windows for the first time. 6 of its checks failed:

| What failed | Heard | Cause | Fix |
| --- | --- | --- | --- |
| Arabic PDF letters swapped, also breaking resume and library search | `األولى`, `املدرسة`, `ثالثة`, `بخمسني` | One glyph holding two letters was flipped when the line was reversed | `PdfPageText` orders glyphs by position and keeps each one whole |
| English scan | `.aper`, `.ill`, `.ast` | The Arabic OCR engine read the English | `OcrRepair.Merge` takes English words from an English engine |
| Arabic PDF, one letter with no text in the file | `رأ` for `قرأ` | The PDF has no letter for that glyph | Marked, then filled from OCR of that page, word by word |
| Arabic scan, one letter | `ورفي` for `ورقي` | OCR misread. Not fixed | The check allows 1 wrong letter per phrase on this scan and prints when it happens |
| End of book said up to 5 times (the check didn't test this, it only showed in the output) | `خلص الكتاب` repeated | Every sentence of the last page queued it again | Said once; the check now fails if not |
| Book title | `english.html` | Chrome stored the file it printed as the title | A title that is only a file name is ignored |
| English PDF page count | saved 4, expected 3 | The check was wrong: the file has 4 pages, one blank | Check corrected |

Measured 2026-11-05 on macOS, SDK 10.0.401, on the same real PDFs through PdfPig:

| Test | Sample | Value | Target | Result |
| --- | --- | --- | --- | --- |
| Core unit tests, all | 195 tests | 195 pass | all pass | Pass |
| Sentences from the real Arabic PDF, correct and in order | 5 sentences | 5 | all | Pass |
| Sentences from the real English PDF | 4 sentences | 4 | all | Pass |
| Release build of the solution | Core, Overlay, App, tests | 0 warnings, 0 errors | 0 | Pass |

Not confirmed until the next Windows run: the two OCR engines working together, and filling the lost letter. Not measured: anything by ear.

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

Not measured yet: using any of this by ear, and how long a real library scan takes.

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
