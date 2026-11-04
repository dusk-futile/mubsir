# Mubsir 2.0 — Product and Build Plan

> One voice agent for Windows that can do anything your mouse and keyboard can do.
> **Blind mode** talks you through the PC with no visuals. **Normal mode** shows a small "notch",
> points at things on screen and draws on it to teach you.
>
> Status: plan approved by the owner on 2026-11-04. No code from this plan has been written yet.
> Next step: see [`docs/HANDOFF.md`](HANDOFF.md).

### Decisions locked (2026-11-04)

| Topic | Decision |
| --- | --- |
| Price | **Completely free** for users. The owner pays all AI and voice costs through one server |
| Distribution | Owner sends a **zip**. Unzip, run, it is already connected to the owner's server. No keys, no account, no sign-in |
| Platform | **Windows only** (10 22H2 and 11) |
| Voice | **Fish Audio (hosted API) is the lead choice**, confirmed by a blind listening test in week 1 against ElevenLabs, Azure and Google. Two voice jobs: conversation and reading. Windows voices only as an emergency fallback (§9) |
| Access | **Full access**, including passwords and payments, behind a deliberate confirmation gesture (§6) |
| Name | Mubsir · مُبصِر |
| Repository | Private; everything (plan, decisions, handoff) lives in the repo so work can continue from any device |

---

## 1. What we learned from the products you named

### Clicky (the "Clickify" you meant)

Clicky is an open-source (MIT) "AI buddy that lives next to your cursor", first on macOS
([farzaa/clicky](https://github.com/farzaa/clicky)), with a community Windows port
([Bitshank-2338/clicky-windows](https://github.com/Bitshank-2338/clicky-windows)). We read its source and
architecture notes directly.

| Piece | How Clicky does it | What we take |
| --- | --- | --- |
| Trigger | Hold Ctrl+Option (Mac) / Ctrl+Win (Windows port) to talk | Same idea, our own key scheme (§4) |
| Ears | AssemblyAI streaming STT over a websocket, token fetched from a proxy | Streaming STT through our own proxy |
| Brain | Claude with a screenshot + transcript, streamed | Claude, but we send the **accessibility tree first** and a screenshot only when needed |
| Voice | ElevenLabs `eleven_flash_v2_5` via the proxy | ElevenLabs for conversation, cheaper voice for long reading |
| Pointing | Claude ends its reply with `[POINT:x,y:label:screenN]`; a transparent overlay flies a blue cursor there on a bezier arc | Same overlay idea, but we point at **UI Automation element IDs** so the target is exact, with pixel coordinates as a fallback |
| Teaching | Windows port draws arrows, circles, underlines and labels on a "whiteboard" overlay | A small drawing language the model writes, rendered by our overlay (§6) |
| Keys | All API keys live in a Cloudflare Worker; nothing secret ships in the app | **Same.** Required for non-technical users (§9) |
| Limits | Points and explains; it does **not** click or type for you | We act, with a safety gate |

### Saywork

Saywork is a Windows app on the Microsoft Store. We could only read its store listing
(the Store page itself is blocked from this cloud session), so this is the vendor's own description,
not verified behaviour:

- **Hold Ctrl and talk**; the text is typed wherever your cursor is (dictation into any app).
- Cleans up speech: removes fillers and false starts, fixes punctuation.
- **English, Egyptian Arabic, or both mixed**, each kept in its own script.
- **Hold Caps Lock and describe a task**; it opens your apps and Chrome and does each step while you watch.
- **Stops and shows you what is about to happen** before anything other people would see or that costs money.
- Meeting notes with speaker separation, summary of decisions and to-dos.
- Dictation audio goes through Saywork's servers.

What we take: Ctrl-to-dictate into any text field, Egyptian/English code-switching, a separate
"do a task" trigger, and approval before public or paid actions. We build our own implementation;
we do not copy Saywork's code, name or branding.

### What none of them do (our edge)

1. A real **blind mode**: works with NVDA, never needs sight, reads long books by page, remembers where you stopped.
2. **Full PC control** grounded in Windows UI Automation, not only screenshots.
3. **Encrypted long-term memory** of whole conversations you can resume ("continue what we did yesterday at 6 PM").
4. **Arabic-first**: Egyptian, Gulf, Levantine, Maghrebi and MSA, mixed with English mid-sentence.

5. **Reads anything**: any book in two languages, IPA and other special symbols, broken PDF fonts (§6).

The existing Mubsir code (29k lines of C#) already has #1, #2 and #4 in part (§11).

| | Clicky | Saywork | **Mubsir** |
| --- | --- | --- | --- |
| Points at / explains the screen | ✅ | — | ✅ (normal mode) |
| Actually clicks and types for you | ❌ | ✅ (Caps Lock tasks) | ✅ full PC control, safety gate |
| Dictation into any app | ❌ | ✅ | ✅ (Caps Lock) |
| Blind mode (no sight needed, NVDA) | ❌ | ❌ (as far as we know) | ✅ |
| Reads books by page, bookmarks, timed reading | ❌ | ❌ (as far as we know) | ✅ |
| Phonetic symbols, maths, broken PDFs | ❌ | ❌ | ✅ |
| Memory across days ("yesterday at 6 PM") | ❌ | ❌ (as far as we know) | ✅ encrypted vault |
| Passwords and payments for blind users | ❌ | asks before paid actions | ✅ local Secrets Vault + 4× Right Ctrl |
| Egyptian Arabic + English | ❌ | ✅ | ✅ |

---

## 2. The product in one picture

```
 Ctrl  ──▶  🔔 "listening" earcon ──▶  you talk  ──▶  Ctrl / silence  ──▶  🔔 "thinking"
                                                                            │
   ┌──────────────────────── Mubsir (Windows tray app, ~60 MB RAM idle) ────┴──────────────┐
   │ Ears: streaming STT ─▶ Router ─▶ Brain (Claude) ─▶ Tools (UIA, files, apps, system)   │
   │                          │            │                                              │
   │                   local commands   Memory vault (encrypted)                          │
   │                   (stop, repeat,     Profile (blind/sighted, job, language)          │
   │                    next page)                                                        │
   │ Mouth: streaming TTS (Fish Audio / Windows voices)   Eyes: notch + pointer + board   │
   └───────────────────────────────────────────────────────────────────────────────────────┘
                                   │ HTTPS
                     Mubsir Cloud proxy (keys, accounts, usage limits)
                     ├── Anthropic (Claude Haiku 5.5 / Sonnet 5.5)
                     ├── STT provider (streaming, Egyptian Arabic)
                     └── Fish Audio TTS (switchable from the server)
```

---

## 3. Two modes

| | **Blind mode** | **Normal mode** |
| --- | --- | --- |
| Visuals | None. No notch, no overlay, no pop-ups that steal focus | Notch at top centre: listening / thinking / speaking / doing, plus a live caption |
| Feedback | Earcons (short tones) + speech, coordinated with NVDA so they never talk over each other | Same sounds (optional) + animation |
| Pointing | Speaks location instead ("the Save button, bottom right") | Blue pointer flies to the element and pulses |
| Teaching | Explains in words, reads maths aloud properly | Draws on screen: arrows, highlights, handwritten-style working for maths, underlined grammar |
| Reading | Book reader: pages, chapters, sentences, timed reading, bookmarks | Same, with the current line highlighted on screen |
| Confirmations | Spoken: "I'm about to send this email to Ahmed. Press Enter." Passwords and payments: **press Right Ctrl four times** (§6) | Notch shows the plan with Approve / Cancel; same 4× Right Ctrl for passwords and payments |
| Default action style | Does it, then tells you what happened | Can do it, or **teach** you to do it yourself (Clicky style) |

Both modes share one engine. The mode only changes the "face" and a few defaults. You can switch
any time by saying "switch to normal mode".

---

## 4. Talking to it: keys and conversation flow

The goal is a "ChatGPT Live" feel inside a push-to-talk tool: fast, interruptible, resumable.

### Keys (default, all remappable)

| Key | Action |
| --- | --- |
| **Tap Right Ctrl** | Start listening (earcon). Talk. It stops on its own when you pause (voice activity detection), or tap again to send now |
| **Hold Right Ctrl** | Push-to-talk: listens while held, sends on release |
| **Tap Right Ctrl while it speaks** | **Interrupt.** It stops mid-word and listens to you |
| **Left Ctrl** while it speaks | Pause speech only (same as NVDA). Say "continue" or tap Right Ctrl + say nothing to resume where it stopped |
| **Hold Caps Lock** | **Dictate**: whatever you say is typed into the focused field, cleaned up (Saywork style) |
| **Win + Shift + Esc** | Emergency stop everything (already built) |

Why Right Ctrl and not "any Ctrl": plain Ctrl is NVDA's stop key and is used in every shortcut
(Ctrl+C, Ctrl+S). Taking it over would break the PC for blind users. Right Ctrl alone is almost
never used, and the current code already handles it.

### Conversation behaviour

- **Continuous session.** After it answers, it keeps listening for ~6 seconds for a follow-up
  without a key press ("open it", "the third one"). Then it goes quiet. Configurable, off in shared rooms.
- **Barge-in.** Any key tap or (optionally) your voice while it talks stops the audio within ~150 ms.
- **Resume.** Interrupted reading or answers are saved with an exact position, so "continue" picks up
  at the word it stopped on.
- **Short-term context.** "The third one", "that file", "maximise it" resolve against the last list it
  read and the window in front. The current Mubsir `TaskContexts` and `ElementRegistry` already
  track this; we extend them to keep the last 20 referenced items.
- **Latency targets**: earcon < 100 ms after key, first spoken word < 1.2 s after you stop talking
  for simple commands, < 2.5 s for tasks that need the model to look at the screen.

How we hit those numbers:
1. STT streams while you talk, so the transcript is ready when you stop.
2. A **local router** handles ~40 commands with no cloud call at all (stop, repeat, next page,
   louder, what time is it, read from here). Already partly built (`CommandGrammar`, `LocalCommandRunner`).
3. The brain streams; we start speaking at the **first sentence**, not the end of the reply.
4. Prompt caching on the system prompt + tool list (stable prefix) cuts both cost and time-to-first-token.
5. TTS streams audio chunks over a websocket and plays as they arrive.

### Why not a speech-to-speech model (OpenAI Realtime / Gemini Live)?

They sound great but: the agent needs many tool calls against Windows, exact control over what is
said (confirmations, reading a book word-for-word), Arabic dialect handling we can tune, and cost
control. A **streaming cascade** (STT → Claude → TTS) gives us all of that at a fraction of the
cost, and with the steps above the delay is close to live. We keep the audio layer behind an
interface so a speech-to-speech provider can be tried later for pure chat.

---

## 5. Which AI models, and when

Prices are Anthropic first-party rates per million tokens, checked 2026-11-04.

| Model | ID | Input / Output | Use in Mubsir |
| --- | --- | --- | --- |
| Claude Haiku 5.5 | `claude-haiku-5-5` | $0.10 / $0.50 (prompts ≤ 100K) | **Default brain.** Commands, chat, reading help, summaries, choosing tools from the accessibility tree |
| Claude Sonnet 5.5 | `claude-sonnet-5-5` | $2 / $10 | **Escalation.** Long multi-step tasks, apps with no accessibility info (screenshot + computer-use toolset), hard teaching, drawing plans |
| Claude Opus 5.5 | `claude-opus-5-5` | $4 / $20 | Optional "think hard" mode the user asks for. Not used automatically |

Your instinct about Haiku 5.5 is right: it is cheap enough to run every turn. A typical turn
(~8K cached input, ~300 output tokens) costs about a tenth of a cent. The router escalates to Sonnet
when Haiku reports it is stuck, when the task needs more than ~6 tool steps, or when the target
app exposes no UI Automation tree.

Things to verify in the first spike (week 1), not assume:
- Whether Haiku 5.5 accepts the computer-use toolset. Sonnet 5.5 and Opus 5.5 use
  `computer_toolset_20260801`. If Haiku does not, Haiku drives UIA tools and Sonnet does pixel work.
- Haiku 5.5 thinking defaults to on at `medium` effort; for speed we set effort `low` on chat turns.
- Refusal handling: Haiku 5.5 has no server-side fallback, so the app retries on Sonnet itself.

**Rule of thumb: the accessibility tree is the primary "eyes", screenshots are the backup.**
UIA text is ~20x cheaper than an image, exact (real button names, real bounds) and works for
blind users without vision guesses. The current code already snapshots UIA (`UiaSnapshotter`,
`ContextBuilder`) and does OCR for scanned PDFs (`OcrService`).

---

## 6. What it can do (capability map)

Each row is a tool group the model can call. ✅ = exists in the current code, 🔧 = extend, 🆕 = new.

| Area | Examples you gave | Tools | State |
| --- | --- | --- | --- |
| Screen reading | "What's on this page?" "Read the titles" | `read_screen`, `describe_screen`, `ocr_window` | ✅ |
| UI control | Click, type, scroll, focus, menus, maximise | `invoke`, `set_text`, `type_text`, `press_keys`, `scroll`, `focus`, 🆕 `window` (max/min/move/close) | ✅🔧 |
| Raw mouse | "Drag this here", apps with no UIA | 🆕 `mouse` (move, click, drag, coordinates from UIA or computer-use) | 🆕 |
| Apps & web | "Open settings", "open the browser and search" | `open_app`, `open_url`, `browse_search`, `open_settings`, `switch_to` | ✅ |
| Files | "Open the book about tigers", "which one is newest?" | `find_files`, `list_folder`, `move/copy/rename/recycle` | ✅ |
| Books & documents | Table of contents, "book number three", go to page 1400, read 2 lines, read for 30 s | `read_document` (PDF/DOCX/EPUB/TXT), 🔧 `toc`, 🔧 `goto_page`, 🔧 `read_for(seconds|lines|sentences)`, bookmarks (`ReadingPositions`) | ✅🔧 |
| Media | "Play my favourite song on Spotify" | `media` keys ✅, 🆕 Spotify: open `spotify:` URI + UIA play; Spotify Web API when the user links an account (playback control needs Premium) | 🔧 |
| System | Storage, battery, Wi-Fi, volume | `system_status`, `set_volume` ✅, 🆕 `disk_report` (largest folders, cleanup suggestions) | 🔧 |
| Updates | "Update my PC first, then turn it off" | `windows_update_status` ✅, 🆕 `install_updates` (Windows Update agent + `winget upgrade --all`), 🆕 `power` (shutdown / restart / sleep / "after updates finish") | 🔧 |
| Startup | "Open these apps when I turn on the PC" | 🆕 `startup_apps` (add/remove/list in the user's Startup folder or Task Scheduler) | 🆕 |
| Dictation | Type what I say into WhatsApp | 🆕 Caps Lock dictation with clean-up and code-switching | 🆕 |
| Teaching / drawing | Maths and English explanations | 🆕 `board` drawing language (below) | 🆕 |
| Memory | "Save chat", "what did we do yesterday?" | `remember` ✅, 🆕 vault (§7) | 🔧 |
| Passwords & payments | "Log into Gmail", "pay the internet bill" | 🆕 `save_secret`, `fill_secret`, `fill_card` (typed locally, never sent to the AI; level 3 gate) | 🆕 |
| Web research | "Find me the best price for…" | Claude server tools `web_search` / `web_fetch` | 🆕 |

### The teaching board (normal mode)

The model never draws pixels. It writes a short list of drawing commands, and the overlay renders them:

```json
{"board": [
  {"op": "point",     "target": "uia:42", "label": "Save"},
  {"op": "highlight", "rect": [120, 300, 480, 340], "color": "yellow"},
  {"op": "arrow",     "from": [600, 500], "to": "uia:57"},
  {"op": "math",      "at": [700, 200], "latex": "x = \\frac{-b \\pm \\sqrt{b^2-4ac}}{2a}"},
  {"op": "write",     "at": [700, 260], "text": "Step 2: divide both sides by 2"},
  {"op": "underline", "target": "text:'have been'", "color": "green"}
]}
```

Steps appear one at a time, synced with the spoken explanation. Rendering: a transparent,
click-through, top-most WPF window per monitor, maths via WPF-Math (LaTeX). Esc clears the board.
In blind mode the same plan is turned into speech ("step 2: divide both sides by two"), and maths
goes through the existing `SymbolVerbalizer`.

### The notch (normal mode): look, motion and how it is built

Blind mode has no visuals at all. Normal mode has one small, beautiful thing at the top centre of the
screen: a dark **pill** that grows into a card when it has something to show, like a phone's "dynamic
island", plus Clicky's flying pointer and the teaching board.

| State | What the user sees | Motion |
| --- | --- | --- |
| Idle | Nothing, or a tiny 6 px dot (setting) | Fades out 1 s after the last activity |
| Listening | Pill (≈ 200 × 36 px) with a live waveform from the microphone and the words appearing as they are recognised | Pill springs open from the dot; bars follow the mic level at 60 fps |
| Thinking | A soft shimmer moving across the pill, one-line plan ("Opening Settings…") | Shimmer loop 1.2 s; text cross-fades |
| Speaking | Waveform following Mubsir's own voice + live caption, word highlighted as it is spoken | Bars follow TTS output level |
| Doing | Card with the steps ticking off: "✓ Opened Settings · Clicking Update…" | Each step slides in; tick draws itself |
| Confirm | Card with the exact action and two buttons: Approve / Cancel (and the key hints) | Card springs to full height; buttons fade in last |
| Reading | Book title, page, a thin progress ring; the current line is highlighted on the page itself | Ring fills smoothly; highlight glides line to line |
| Teaching | Pill shrinks; the pointer flies to targets and the board draws step by step | Pointer flies on a curved (bezier) path, pulses, shows a label |
| Offline / error | Pill turns amber with one short sentence | Gentle shake (off when "reduce motion" is on) |

Motion rules:
- Everything moves with **springs** (no straight-line timing): open ≈ 300 ms, close ≈ 220 ms, no bounce
  that overshoots more than a few pixels. One thing moves at a time.
- Respects Windows "Show animations" / reduce-motion: then only fades.
- **Never steals focus or clicks**: the window is click-through except its buttons, top-most, on the
  monitor with the mouse, DPI-aware, dark by default with a light theme.
- Performance budget (for 4 GB PCs): GPU-composited, draws only while something moves, 0 % CPU when
  idle, < 30 MB extra memory, the window is closed after 10 s of idle.

How it is built:
1. **Prototype first in HTML/CSS/JS**, using the `taste-skill` (design direction, no "AI-made" look) and
   real-browser screenshots. All states above, on one page, published as a private link the owner can
   open on a phone and click through.
2. The owner picks a direction. Its colours, sizes, durations and spring values are written once into
   `src/Mubsir.Overlay/motion-tokens.json`.
3. **Native build in WPF** (.NET 10) reading those tokens: a layered transparent window
   (`WS_EX_NOACTIVATE | WS_EX_TRANSPARENT`, buttons hit-testable), animations on the GPU. No web
   engine inside the app, because a browser engine would add 100 MB+ of memory on a 4 GB PC.
4. Tests: a scripted run through every state, recorded to GIF, with frame-rate, CPU and memory numbers.

References to study (ideas only, our own code): Clicky's `OverlayWindow` (MIT), Ripple (MIT),
Dynamic-Island-for-Windows by devcode90 (MIT). DynamicWin is CC-BY-SA, so look but don't copy code.

### Reading anything: two languages and strange symbols

**Rule: the user's language and the book's language are separate.** You talk in Egyptian Arabic, Mubsir
answers in Egyptian Arabic, and the book is read in its own language with a native voice.

Example, start to finish:

```
You (Arabic):  "افتحلي كتاب الفونولوجي واقرالي أول صفحة من الفصل التالت"
 1. Ears      Streaming STT → Arabic text (+ language tag "ar-EG")
 2. Brain     Haiku 5.5: intent = open book + go to chapter 3 + read 1 page
 3. Tools     find_files("phonology") → read_document → toc → goto(chapter 3)
 4. Mubsir    (Arabic voice) "فتحت كتاب Introducing Phonology، الفصل التالت. هبدأ أقرا."
 5. Reader    page text → segmenter → per-run voice:
                English prose        → English reading voice
                Arabic words         → Arabic voice (mid-sentence switch, already built)
                IPA /ˈfəʊnɪm/        → symbol reader (below)
 6. You       tap Right Ctrl → stops on the exact word; "يعني إيه schwa؟" → answer in Arabic,
              "كمّل" → resumes at the same word in English
```

The AI is **not** in the loop for plain reading: text goes straight from the page to the voice. That
keeps reading fast, cheap and word-for-word faithful. Haiku is only asked when you ask a question,
or when a page needs fixing (below).

**Strange symbols.** Phonology, linguistics, maths, chemistry and dictionaries use characters that every
TTS voice mangles or skips. Mubsir handles them itself, before the voice sees the text:

1. **Detect** special spans: IPA (Unicode blocks IPA Extensions U+0250–02AF, Spacing Modifier Letters,
   combining diacritics, text between `/…/` or `[…]`), stress and length marks `ˈ ˌ ː`, tone letters,
   maths, Greek letters, arrows, chemical formulas. Extends the existing `SymbolVerbalizer` /
   `SymbolTables`, which already handle maths, emoji and symbols.
2. **Full IPA table** (~170 symbols) with the official name, an English example word and an Arabic
   name. For example: ə = "schwa, the a in *about*"; ɐ = "turned a"; ʃ = "esh, the sh in *ship*";
   θ = "theta, the th in *think*"; ð = "eth, the th in *this*"; ŋ = "eng, the ng in *sing*";
   ʔ = "glottal stop, like the hamza ء".
3. **Three reading styles**, switched by voice ("read symbols by name" / "say the sounds"):
   - **Name** (default for study): "slash, primary stress, f, schwa, long u, n, slash".
   - **Sound**: the voice pronounces the transcription (needs a voice that reads IPA; see §9).
   - **Both**: name, then the sound.
4. **Broken PDFs.** Many older linguistics PDFs draw IPA with legacy fonts, so the extracted text is
   garbage ("fYnim"). Mubsir checks each page: if letters are unknown, private-use (U+E000–F8FF) or
   don't form words, it sends **that page image** to Claude vision (Haiku first, Sonnet if unsure), which
   returns clean Unicode text with the IPA. The fixed page is cached so it is paid for once.
   **Font map trick:** a legacy font maps the same wrong code to the same symbol on every page. After
   vision fixes the first 1–3 broken pages, Mubsir learns that font's code → real symbol map and fixes
   the rest of the book **locally, for free, and exactly** (no image guessing). Vision is used again only
   for codes the map has not seen yet.
5. **Never skip silently.** If a symbol is still unknown, Mubsir says "unknown symbol" and offers to
   spell its Unicode name, so the reader always knows something was there.

Done when: a 30-page test set (phonology textbook, Arabic grammar book with tashkeel, maths worksheet,
dictionary page, scanned PDF with legacy IPA font) is read with no skipped or wrong symbols.

### Book preparation: process once, read many times

Goal: a book on the PC should cost almost nothing to read again, with the same word-for-word accuracy,
on a 4 GB RAM PC.

**What actually costs money when reading** (so we cut the right thing):

| Cost | When it happens | How preparation removes it |
| --- | --- | --- |
| Claude tokens | Only for broken pages (vision fix), missing table of contents, and questions | Fix each page **once**, cache it; answer questions from a local index, not the whole book |
| Voice characters | Every time text is spoken | Speak each sentence **once**, keep the audio on disk |
| Time | Opening a 1,500-page PDF, OCR | Done in the background, stored as a small prepared file |

**Two levels of preparation** (so 400 books on a weak PC cost almost nothing):

*Level 1 — library card, for every book on the PC (background, free, once per file):*
title and author from the file's metadata, the first 2–3 pages, and the table of contents (PDF bookmarks
or detected headings). Scanned books go through Windows' built-in OCR for those pages only. If a file has
no usable title ("scan_0042.pdf"), Haiku names it **from that extracted text** (text, not images):
≈ $0.0005 per book, ≈ $0.20 for 400 books, once. Cards are shared through the server, so a book that any
user has already catalogued is identified instantly. See "Finding a book" below.

*Level 2 — page window, only for the book that is open:*

1. **Fingerprint** the file (SHA-256) and also each page's text, so the same page is recognised even in
   a re-saved or different copy of the book.
2. **Prepare only the current page, the previous page and the next page**: extract text (PDF text layer,
   or Windows OCR for scanned pages; free, local), then **check it locally** (unknown or private-use
   characters, non-words, text layer that disagrees with OCR). Only a page that fails goes to the server
   for a Claude vision fix (Haiku; Sonnet if unsure). When the reader moves to page 35, page 36 is
   prepared; page 33 is kept. Nothing else in the book is touched.
3. **Structure**: table of contents, page numbers as printed in the book ("page 1400" means the printed
   number), sentences and paragraphs, language runs (English / Arabic), symbol spans (IPA, maths).
   Haiku is asked once only if the book has no usable TOC; the answer is stored in the library.
4. **Local book file** (`.mvb`) in the vault folder: compressed text of the pages prepared so far +
   structure + a small search index (SQLite FTS5) that grows as pages are read.
5. **Before preparing anything, ask the server's library.** If the page (by its text fingerprint) was
   already prepared or voiced by anyone, download it instead of paying again.
6. **Audio only for what is actually read.** Voice is made sentence by sentence, at most **~30 seconds
   ahead** of the reader (a few sentences), and **never past the end of what was asked**: "read pages
   46 to 80" never voices page 81; "read the first quarter of the page" stops there. Worst-case waste
   when the user stops: ~30 seconds of audio (≈ $0.01). Audio is saved on the PC as Opus (~10 MB per
   hour of speech, disk cap 1 GB, oldest evicted first) and in the server library. Re-reading is free.
7. **Reading amounts the user can ask for** (all understood in Arabic and English): a range of pages
   ("46 to 80"), a part of a page ("first quarter", "the last paragraph"), N lines / sentences /
   paragraphs, a section or chapter from the TOC, or a time ("read for 30 seconds", "for 10 minutes").
   The reader stops exactly there and says where it stopped.
8. **Speed changes are free**: audio is made once at normal speed and sped up or slowed down on the PC
   (pitch-preserving time-stretch), so "read faster" never re-buys the audio.
9. **One pronunciation list per book**: names and terms are fixed once ("Chomsky", "allophone") and
   reused for every page, so the book sounds consistent.

**Limits: free things vs paid things (decided 2026-11-04).** Users never hear "you reached your limit".

*Free things* (run only on the user's PC, or audio already saved): screen reading with the Windows voice,
replaying cached or library audio, local commands, OCR, file and app control. **No cost limit.** The only
pause is the **listening check-in**: continuous reading pauses after the user's chosen time (default
2 minutes, choose 1–30 or "never") with a soft sound: "Shall I continue? One: continue. Two: stop.
Three: read without stopping." If the user says "read the whole book without stopping" and the audio
is free, it reads to the end with no check-ins.

*Paid things* (new voice audio, AI, speech-to-text). Recommended combination, all invisible:

1. **Check-in every 2 minutes** while new (paid) audio is being made; 15 s of silence stops it and saves
   the place. "Read pages 46 to 80" is allowed; it still checks in every 10 minutes. A forgotten reader
   can never cost more than a few minutes of audio.
2. **Invisible daily premium budget per device** (e.g. 60 minutes of new audio). After it, reading
   **continues without stopping** in the on-device Windows natural voice. The owner chooses whether
   the switch is silent or marked by one short soft tone (no words). Premium returns the next day.
3. **Cached audio is always premium and free**, so popular books stay in the good voice forever.
4. **AI and speech-to-text** are so cheap (§9 price list) that their daily soft cap (e.g. 500 requests)
   is only an abuse guard; normal users never get near it.
5. **Global monthly budget** for the owner: at 80 % an alert to the owner; at 100 % everyone's new
   audio uses the Windows voice until the month ends; AI keeps working within a separate hard ceiling.

**Questions about the book with few tokens:** "what does chapter 3 say about allophones?" searches the
local index and sends Haiku only the 3–5 best-matching passages (a few thousand tokens), not the
chapter. Chapter summaries are written once on first request and cached. When several questions in a
row are about the same chapter, that chapter goes into a cached prompt prefix (prompt caching), which
makes follow-up questions much cheaper.

**4 GB RAM rules for preparation:** one page in memory at a time, background thread at low priority,
paused while the user is talking or reading aloud, about 150 MB peak, stops on battery saver.

**Shared library on the owner's server (decided 2026-11-04: the owner has the rights to the books).**
What is shared and what is personal:

| Shared by everyone (server library) | Personal to each user |
| --- | --- |
| Library cards (title, author, TOC, first pages) | Which books they have, reading position, bookmarks |
| Prepared page text, vision fixes, font maps | Reading history, questions asked, saved chats |
| Voice audio per sentence | Settings (voice speed, check-in time, mode) |

- **Audio is stored by the exact sentence text + voice**, not by file. If two different copies or
  editions of a book contain the same sentence, it is voiced once. If even one letter differs, it is a
  different key, so shared audio is always word-for-word right.
- **The server makes everything itself.** A PC sends text (or, for a broken page, its image); the
  server calls Fish Audio or Claude and stores the result. PCs only download. So one faulty or
  tampered PC can never put wrong audio or text into everyone's library.
- **Corrections improve it for everyone.** "That sounded wrong" / "الكلمة دي غلط" marks the sentence;
  the server re-checks that page with Sonnet and replaces the shared copy if needed.
- **Personal data stays on the user's PC only** (encrypted vault). One user = one PC; there is no
  multi-device sync (owner's decision, 2026-11-04).

The first person to read a page pays for it once; **everyone after that downloads it** for the cost of
storage. Popular books can be fully voiced on the server once, in advance. Storage: a 600-page book is
roughly 40–50 hours of speech ≈ 0.5 GB of Opus audio; on Cloudflare R2 that is about a cent a month,
with no download fees.

Done when: a 600-page English phonology PDF is prepared in the background on a 4 GB PC without
slowing it down, any page opens in under 1 second, and reading the same page twice costs 0 tokens and
0 voice characters.

### Finding a book among hundreds (even untitled ones)

1. "Open the book about the history of phonetics" → local search over all library cards on the PC
   (title, author, TOC, first pages). Free.
2. If more than one fits, Mubsir always answers with a **numbered list, at most 4 at a time**: "I found
   6. One: *Introducing Phonology*, by …. Two: an untitled scan that starts with 'Chapter 1: Sounds of
   English'. Three: … Four: … Say a number, or 'more'." Numbers are spoken in the user's language.
3. The user can say the number, or part of a title or author ("the Roach one").
4. Not sure which one? "Read me the first page of number two" reads it, then asks again.
5. Once chosen: the book opens full screen and Mubsir offers numbered starting points:
   "One: table of contents. Two: chapter 1, *Phonetics*, page 1. Three: where you stopped last time,
   page 34. Four: the first page." Then it goes there, says the chapter, page and title, and how
   the section is divided ("this chapter has 4 sections: …"), and starts reading.

### Example session and what each step costs

```
PC starts           → Mubsir starts at sign-in, "Mubsir is ready"                        local, $0
"Hey Mubsir" / Right Ctrl → listening earcon                                              local, $0
"Find the book whose contents have chapters on allophones and stress"
                    → local search over the TOCs of every prepared book on the PC        local, $0
                    → Haiku ranks the matches and phrases the answer                     ~$0.001
"I found 4. One: Introducing Phonology. Two: … Which one?"   "Number one."
                    → opens it, maximises the window                                     local, $0
"Read me the table of contents"   → from the prepared file, read aloud                   voice only
"Go to the start of chapter 1"    → "Chapter 1, Phonetics, page 1. It has 4 sections: …" ~$0.001
"Read"  → straight from page to voice (no AI), audio cached / downloaded                 voice only
Right Ctrl → stops on the word.   "Read that again" → replays cached audio                $0
"Continue" → resumes at the stopped word                                                 voice only
"يعني إيه allophone؟" → local index finds passages → Haiku answers in Arabic             ~$0.002
```

Wake word: the default trigger is the Right Ctrl key. An optional "Hey Mubsir" / "يا مبصر" wake word
runs a small **on-device** keyword spotter (no audio leaves the PC until it hears the name; low CPU).
Off by default on shared PCs.

### Cost estimate: one person reads 5 books of 600 pages

**Cost follows what is read, not the book's size.** Price per page (≈ 2,200 characters) with Fish
Audio ≈ **$0.03**; with ElevenLabs Flash ≈ $0.09. Examples for a first reader (0 for later readers of the
same pages):

| What the user reads | Fish Audio | ElevenLabs Flash |
| --- | --- | --- |
| First quarter of one page | ≈ $0.01 | ≈ $0.02 |
| One page | ≈ $0.03 | ≈ $0.09 |
| Pages 46 to 80 (35 pages) | ≈ $1.15 | ≈ $3 |
| One 30-page chapter | ≈ $1 | ≈ $2.60 |
| A whole 600-page book | ≈ $20 | ≈ $53 |

The table below is the **upper bound**: someone reading five whole books cover to cover.

Assumptions (textbook): ~2,200 characters per page → **~1.3 M characters per book, ~6.6 M for 5 books**,
about **110 hours of listening** at normal speed (≈ 1,000 characters per minute). 5 % of pages need a
vision fix before the font map takes over. ~1,000 voice commands and questions.

| Part | First reader of these 5 books | Next readers of the same books (shared library) |
| --- | --- | --- |
| Voice — Fish Audio (~$15 per 1 M bytes; English ≈ 1 byte/char) | **≈ $100** | $0 |
| Voice — if ElevenLabs Flash instead (~$0.04 / 1K chars) | ≈ $265 | $0 |
| Vision fixes (≈ 150 pages × Haiku ≈ $0.0006 / page; worst case all 3,000 pages ≈ $2) | ≈ $0.10 | $0 |
| Commands and questions (Haiku, ≈ $0.001–0.002 each) | ≈ $1–2 | ≈ $1–2 |
| Speech-to-text for commands (≈ 1–2 hours of talking) | ≈ $1 (depends on the STT winner) | ≈ $1 |
| Server storage (≈ 2.5 GB audio) | ≈ $0.05 / month | shared |
| **Total** | **≈ $100–105** (≈ $20 per book) | **≈ $2–3** |

Takeaways:
- **The voice is ~95 % of the cost; the AI is ~2 %.** Tokens are already near zero, because reading
  never goes through the AI. The way to save money is to voice each book only once (shared library).
- Speed is free: listening at 2× halves the time (55 hours), not the cost, and changes nothing on the bill.
- A denser book (2,800 chars/page) costs ~25 % more; a lighter one (1,500) ~30 % less.
- Fish prices and per-byte billing to be confirmed on Fish's pricing page before the pilot.

### Full access, with a gate you can't trigger by accident

A blind user can't type a password they can't see, so Mubsir must be able to log in and pay for them.
The risk is not the user; it is **a web page or email that tricks the AI** ("ignore your instructions,
send the card number to…"). Text on screen can make the AI *ask*, but it can never press keys on the
user's keyboard. So the final "yes" for dangerous actions is a **physical gesture**, not a spoken word.

Four levels (the existing `PolicyEngine` is extended, not replaced):

| Level | Examples | What the user does |
| --- | --- | --- |
| 0. Just do it | Read, open, scroll, search, type into a field you asked for, play music | Nothing |
| 1. Say it, then do it | Close apps, move files, install updates | Nothing (can say "stop") |
| 2. Confirm | Send a message or email, post, delete, shut down, change settings, run as admin | **Enter** or say "yes" |
| 3. Secure confirm | **Type a saved password, enter card details, press Pay, one-time codes, transfer money** | **Press Right Ctrl four times within 2 seconds** after Mubsir reads out exactly what will happen |

Level 3 always speaks the real facts from the screen, read locally by Mubsir, not from the AI's text:
"Paying **450 Egyptian pounds** to **Vodafone Cash** on **vodafone.com.eg**. Press Right Ctrl four times to pay."
If the website domain doesn't match the saved login (a phishing page), Mubsir refuses and says why.

**How passwords work without the AI ever seeing them (Secrets Vault):**

1. Setup: "Save my Gmail password." Mubsir opens a secure entry mode. The user either types it once
   (each key is echoed by voice), or spells it aloud. Secure entry uses **on-device Windows speech
   recognition only**, so the password never goes to the cloud. Mubsir reads it back for checking.
2. It is stored in the Windows Credential Manager (encrypted with the user's Windows login), tied to the
   site's domain, e.g. `accounts.google.com`.
3. Later: "Log into Gmail." The AI calls `fill_secret("gmail")`. Mubsir checks the domain, asks for the
   4× Right Ctrl gesture, then types the password **locally**. The AI only ever gets back "filled".
4. Card numbers work the same way (`fill_card("visa")`), with the CVV asked for each time unless the
   user chooses to save it.
5. Screen text sent to the AI is still redacted (`Redactor`, `SensitiveDetector` already exist), so
   passwords and card numbers visible on screen never leave the PC.

The old rule "bank pages switch the AI off" becomes: **bank pages switch to level 3 for every action.**

Every action is logged locally (what was done, never what was said or typed) so "what did you just do?"
and "undo" work.

---

## 7. Memory: saving and resuming conversations

What you asked for: talk for four hours, say **"save chat"**, turn the PC off, and tomorrow say
"remember our chat yesterday at 6 PM Egypt time" and continue.

### Design

```
%LOCALAPPDATA%\Mubsir\Vault\
  index.mv            ← encrypted list: id, start/end time (with time zone), title, topics, files touched
  sessions\
    2026-11-03T18-02+0200_7f3a.mv   ← one encrypted file per saved conversation
  profile.mv          ← who you are (mode, name, job, languages, preferences)
  reading.mv          ← bookmarks: file, page, sentence for every book
```

Each `.mv` file is: JSON → **Brotli compression** → **AES-256-GCM encryption**, with the key protected
by Windows DPAPI for the current user (the code already has `Dpapi.cs`). A four-hour chat summary is a
few KB on disk.

What is saved per conversation:
1. A **structured summary** written by Haiku: goal, what we did, decisions, open questions, next steps.
2. **Key facts** learned about you (goes to the profile, with your permission).
3. **Artifacts**: files opened, pages read up to, apps used, so "continue" can reopen them.
4. The last ~20 turns verbatim, so the tone and the exact last question carry over.

When it saves:
- When you say "save chat" / "احفظ المحادثة".
- **Automatically** when you go quiet for 30 min, when Windows shuts down or you sign out
  (the code already watches sessions in `SessionWatcher`), and before an update reboot.
  Auto-save can be turned off; then only "save chat" saves.

How you get it back:
- "What did we talk about yesterday at 6?" → the index is searched by time (in your time zone,
  Africa/Cairo by default) and topic, the summary is loaded into the conversation, and Mubsir says
  "Yesterday at 6 PM we read chapter 3 of *The Tigers* up to page 212. Shall I continue?"
- "Forget yesterday's chat" deletes it.
- Nothing from the vault is sent to the cloud unless that conversation is being resumed, and then only its summary.

Honest limit: DPAPI encryption stops other Windows users and anyone who copies the files from reading
them. It does not stop malware already running as you. That is the same protection browsers use for
saved passwords.

---

## 8. First run: install and onboarding

### Install (one zip, no keys, no PowerShell, no account)

1. The owner sends **`Mubsir.zip`** (WhatsApp, Drive, a download link). Inside: `Mubsir Setup.exe` and
   a short audio file "how to install" for anyone who needs a sighted helper.
2. Running `Mubsir Setup.exe` **speaks from the first second** (it plays its own audio, in Arabic and
   English) and needs only Enter to continue. No admin prompt for the normal install.
3. It installs per-user with **Velopack** (C#-native, delta auto-updates, silent background updates,
   rollback if an update fails). Updates come from the owner's server, so the zip only has to be sent once.
4. On first start it **registers the device** with the owner's server automatically (§9). The zip contains
   **no API keys**: only the server address.
5. If NVDA is not present and the user is blind, it offers to install NVDA and the Mubsir Bridge add-on
   (existing `setup.ps1` logic moves into the installer).
6. Mubsir registers itself to start at sign-in (Task Scheduler, already done by `setup.ps1`) and
   starts immediately.

Code signing: an unsigned `.exe` downloaded from the internet triggers the Windows SmartScreen
"Windows protected your PC" box, which a screen reader user can get stuck on. Plan for a signing
certificate (Azure Trusted Signing, about $10/month) before the pilot. Until then, the audio guide in
the zip explains "More info → Run anyway".

### Onboarding conversation (spoken, ~2 minutes)

```
Mubsir: Hello, I'm Mubsir. Can you see the screen, or would you like me to work fully by voice?
You:    I'm blind.
Mubsir: Got it, blind mode is on. Nothing will pop up on screen. What should I call you?
You:    Karim.
Mubsir: Nice to meet you, Karim. What do you do, and what would you like help with most?
You:    I'm an English teacher and I can't read printed text. I want help reading books.
Mubsir: Understood. I'll read documents and books for you, remember where you stop, and help you
        prepare lessons. Which language should I speak by default: Egyptian Arabic, English, or both?
You:    Both.
Mubsir: Now let's try it. Tap the right Control key once, then ask me anything.
```

Saved to `profile.mv`: mode, name, job, goals, languages/dialect, reading speed, voice, how chatty to be.
The profile goes into the system prompt (`ProfileBlock` already exists), so "read the book" means
"read aloud, slowly, remember the page" for this user, and "explain this" means teaching level
pitched to an English teacher.

No account, no sign-in: the device registration is invisible to the user.

### Full-access setup (once, spoken, right after onboarding)

Mubsir walks the user through each step by voice, checks it worked, and says what it is for:

1. **Microphone allowed for desktop apps** (Windows Privacy settings). Mubsir opens the page and checks.
2. **Start at sign-in** (Task Scheduler entry, already done by `setup.ps1`).
3. **One administrator approval** to install a tiny **Mubsir Helper** service. Windows shows its
   "Do you want to allow…" box once; NVDA reads it. After that, the helper does an allow-listed set of
   admin jobs (install updates, restart / shut down, change system settings) **without new pop-ups**,
   and only when the main app asks with a valid level 2–3 confirmation (§6). The AI can't call it
   directly.
4. **NVDA and the Mubsir Bridge add-on** for blind users.
5. **Library scan**: "Which folders have your books?" (default: Documents, Downloads, Desktop).
6. **Test**: "Tap Right Ctrl and say: what time is it?"

Everything a user says during onboarding (mode, job, needs) is saved to their profile on their PC.

---

## 9. Cloud and cost (free for users, paid by the owner)

### The server

The zip must never contain an API key: anyone could unzip it, copy the key and spend the owner's money.
All keys live on the owner's server, like Clicky does:

```
Mubsir.exe ──HTTPS──▶ Mubsir Cloud (Cloudflare Workers, owner's account)
                       /register    → first start: device gets its own random token
                       /chat        → Anthropic Messages API (streaming)
                       /stt-token   → short-lived token for the STT websocket
                       /tts         → TTS provider chosen by server config (Fish Audio first) + audio cache
                       /library     → shared book library: cards, prepared pages, audio (R2)
                       /update      → Velopack update feed
                       Storage: device tokens + usage counters only (Cloudflare D1 / KV)
                       Secrets: ANTHROPIC_API_KEY, ELEVENLABS_API_KEY, STT key (Wrangler secrets)
```

The server never stores a user's recordings, transcripts, questions or screen text. It stores only the
shared book library (§6) and counts tokens and characters per device.

### Where things run: balancing the owner's PC, the users' PCs and the cloud

The owner's own PC is not strong and can't be on 24/7 (power cuts, home internet, changing IP address,
and it would expose a home machine to the internet). So **the owner's PC runs nothing for users**.
The work is split three ways:

| Where | What it does | Load |
| --- | --- | --- |
| **Each user's PC** | Everything that is free to do locally: keyboard, screen reading (UIA), Windows OCR, PDF text extraction, page checks, font maps, library cards, search index, audio cache and playback, speed changes, memory vault, Secrets Vault, settings | Light: < 80 MB idle, ~150 MB while preparing a page; storage capped (~1 GB audio cache) |
| **Cloudflare (the "server")**, on the owner's account | A thin relay: holds the API keys, checks the device token and daily caps, forwards requests to Claude / Fish / STT and streams the answers back, stores the shared library in R2 | Tiny: no AI runs here, it only passes data. Free tier ≈ 100,000 requests a day; paid plan ≈ $5 a month for millions. R2 storage ≈ $0.015 per GB-month, 10 GB free, no download fees (check current limits) |
| **Anthropic, Fish Audio, STT provider** | The heavy work: AI thinking, image reading, voice generation | Paid per use (§9 costs) |
| **The owner's PC** | Only for the owner: deploy updates, set caps and voices, look at the usage and cost dashboard | Nothing while users use Mubsir; it can be switched off |

Why this is "equal": each user's PC carries its own storage and its own local work, so adding users
adds no load to the owner's PC and almost none to the relay. The only thing that grows with users is
the per-use bill to Anthropic and Fish, which the caps control.

Rough relay traffic: 10 users reading 2 hours a day ≈ 20,000 requests a day, inside the free tier.

### Protecting the owner's money (required because it is free)

| Protection | Why |
| --- | --- |
| One token per device, created on first start | A leaked token can be switched off without breaking everyone |
| **Invisible daily caps per device** (e.g. 60 minutes of new premium audio, 500 AI requests), set from the server | One heavy user or a script can't run up the bill |
| **Global monthly budget** with an alert at 50 % / 80 % and a hard stop at 100 % | No surprise bills |
| Over-cap behaviour: reading continues in the Windows natural voice, silently or with one soft tone; no "limit reached" message | The user is never stopped or lectured |
| Invite codes (optional switch) | If the zip spreads beyond the people it was sent to |
| Kill switch per device and for everyone | Abuse or a bug |
| Also set spend limits in the Anthropic and voice provider consoles | Second line of defence |

### Voices: Fish Audio first, chosen by ear

History: first decision was "ElevenLabs everywhere" (natural sound). After comparing prices the owner
chose **Fish Audio** as the lead: similar or better quality in blind tests (mostly English), about
**$15 per million bytes** of text vs $40–80 per million characters for ElevenLabs.

| Job | Provider (lead → backup) | Notes |
| --- | --- | --- |
| Conversation (short answers, confirmations) | Fish Audio streaming → ElevenLabs Flash v2.5 | Measure time-to-first-audio from Cairo; target < 300 ms |
| Reading books and documents | Fish Audio → Azure Neural (has Egyptian voices Salma / Shakir, ~$15 per 1M) | Price matters most here; audio cache on |
| Phonetic (IPA) "say the sound" snippets | Whichever voice passes the IPA test (§6 "Reading anything"); ElevenLabs v3 reads IPA in `/…/` (docs: 80–90 % consistent); Azure supports SSML `<phoneme alphabet="ipa">` | Short snippets only, so price doesn't matter |
| Speech-to-text | Bake-off in week 1: current Gemini path, ElevenLabs Scribe realtime, Deepgram, Soniox on 50 real Egyptian/English recordings | Egyptian code-switching accuracy decides |

Rules:
- **Week-1 blind listening test** with ~10 blind testers: same Egyptian Arabic and English passages,
  normal and 2× speed, providers hidden. Fish (S2 Pro, Drama 3), ElevenLabs (Flash, v3), Azure (Salma,
  Shakir), Google Chirp 3 HD. Winner per job and per language.
- The app never names a provider: it asks the server for the "conversation" or "reading" voice.
  Switching provider is a server setting, not a new zip.
- Fish bills per UTF-8 byte; Arabic letters are 2 bytes, so Arabic costs about **double** per character
  (≈ $30 per million characters). Verify on Fish's billing page.
- **Fish open weights are not for us yet.** S2 Pro weights are under the Fish Audio Research License
  (research / non-commercial; anything else needs a licence from business@fish.audio), and need a GPU
  with roughly 7–20 GB of VRAM. They can never run on users' 4 GB PCs. Self-hosting on the owner's GPU
  server is a later cost option, only after a written licence from Fish Audio.

### Price list: every item (estimates, November 2026; confirm on each vendor's page)

| Item | Unit price | What one action costs |
| --- | --- | --- |
| Claude Haiku 5.5 (main brain) | $0.10 / 1 M input tokens, $0.50 / 1 M output | A command ≈ $0.001; a question about a book ≈ $0.002; reading one page image ≈ $0.0006; naming an untitled book ≈ $0.0005 |
| Claude Sonnet 5.5 (hard tasks only) | $2 / 1 M input, $10 / 1 M output | A hard multi-step task ≈ $0.01–0.03; a vision re-check of a page ≈ $0.01 |
| Claude Batch API (non-urgent jobs) | 50 % off | Library cards for 400 books ≈ $0.10 instead of $0.20 |
| Fish Audio voice | ≈ $15 / 1 M bytes | English ≈ $0.015 per minute of speech, ≈ $0.03 per page; Arabic ≈ double |
| ElevenLabs Flash (backup voice) | ≈ $0.04 / 1,000 characters | ≈ $0.04 per minute; ≈ $0.09 per page |
| Speech-to-text (user's voice) | Soniox real-time ≈ $0.12 / hour; ElevenLabs Scribe v2 RT ≈ $0.39 / hour; Deepgram ≈ $0.35–0.75 / hour (sources disagree) | A 5-second command ≈ $0.0002–0.001 |
| Windows voices, Windows OCR, local work | $0 | $0 |
| Cloudflare Workers (relay) | Free ≈ 100 k requests / day; paid ≈ $5 / month | ≈ $0–5 / month in total |
| Cloudflare R2 (shared library) | ≈ $0.015 / GB-month, 10 GB free, no download fees | A 600-page book's audio (≈ 0.5 GB) ≈ $0.008 / month |
| Code-signing certificate | ≈ $10 / month | Fixed |
| Domain for the server (optional) | ≈ $10–15 / year | Fixed |

**Per user per month** (first reader of every page; the shared library lowers the reading part):

| User | Daily use | Brain | STT | Replies (voice) | Reading (voice) | Total / month |
| --- | --- | --- | --- | --- | --- | --- |
| Light | 20 commands, 5 pages | $0.60 | $0.30 | $2.25 | $4.50 | **≈ $8** |
| Typical | 50 commands, 20 pages | $1.50 | $0.90 | $4.50 | $18 | **≈ $25** |
| Heavy reader | 50 commands, 60 pages | $1.50 | $0.90 | $4.50 | $54 | **≈ $60** |

With the 60-minute daily premium-audio cap (§6 Limits), a heavy reader costs at most **≈ $35 / month**;
reading beyond it continues in the free Windows voice, or in premium wherever the audio is already cached.

**Owner's total**: pilot of 10 users (5 light, 4 typical, 1 heavy) ≈ **$200 / month** + ≈ $15 fixed.
100 users with the same mix ≈ $2,000 / month before library savings; when many users read the same
books, the reading column falls towards $0 for everyone after the first reader.

### What it will cost (estimates; verify on each provider's pricing page)

Reported API list prices (October 2026): **Flash v2.5 ≈ $0.04 per 1,000 characters**,
**Multilingual v2 / v3 ≈ $0.08 per 1,000 characters**. 1,000 characters ≈ 1 minute of speech.

| User type | Speech per day | ElevenLabs / month | Fish Audio / month (EN–AR) | Claude (Haiku-first) | Total with Fish |
| --- | --- | --- | --- | --- | --- |
| Light (questions, commands) | 10 min | ≈ $12 – $24 | ≈ $5 – $9 | < $1 | **≈ $5 – $10** |
| Heavy reader (1 h of books daily) | 70 min | ≈ $85 – $170 | ≈ $32 – $64 | ≈ $1–2 | **≈ $35 – $65** |

Every provider is cheap **per sentence**; **reading books is the expensive part**. With Fish, 10 pilot users
cost roughly $50 – $400 a month depending on how much they read. Ways to bring it down, in order:

1. Audio cache (same page never billed twice; shared across users for the same public book file).
2. The cheapest voice that wins the listening test for each job (Azure is ~$15 per 1M for reading).
3. A daily premium-audio cap, with Windows natural voices after it.
4. Apply to voice providers' programmes for non-profit / accessibility projects and ask Anthropic about
   startup or non-profit credits. A free tool for blind Arabic speakers is a strong case. (Not verified
   what is on offer today; check before the pilot.)

## 10. Staying light on old PCs

Target: 4 GB RAM Windows 10 PCs (the current README target).

**What a user's PC needs** (all AI and voices run in the cloud through the owner's server):

| | Minimum |
| --- | --- |
| Windows | 10 (22H2) or 11, 64-bit |
| Memory | 4 GB RAM |
| Graphics card | None needed |
| Disk | ~300 MB for Mubsir, plus the audio cache (capped, e.g. 1 GB) |
| Audio | Microphone + speakers or headphones (headphones recommended in shared rooms) |
| Internet | ~1 Mbps; voice streaming needs a stable connection. Offline: screen reading, OCR and Windows voices still work |
| Screen reader | NVDA for blind users (installer offers it) |

- Native .NET 10 app, no Electron, no browser engine. Idle: < 80 MB RAM, ~0% CPU.
- Overlay windows are created only in normal mode and only while showing something.
- Audio capture opens only after the key press.
- No local AI models by default. Optional "offline mode" can add a small local STT later.
- Logs rotate and stay small (already built: `LogRetention`).

---

## 11. What we keep from the current Mubsir, and what changes

The current code is a solid base for blind mode: keyboard hook state machine, NVDA bridge and quiet
mode, UIA snapshotting and actions, OCR, document reader with reading positions, Arabic/English text
pipeline (dialects, Arabizi, symbols, emoji), a policy/safety engine, an agent loop with 29 tools,
cost ledger, encrypted secret storage.

| Keep as is | Rework | New |
| --- | --- | --- |
| `Mubsir.Core` text pipeline, input state machine, context builder | Brain: add model router (Haiku → Sonnet), prompt caching, streaming-first speech | Cloud proxy, device tokens, usage caps |
| UIA reader/actions, OCR, document reader | Ears: provider interface + streaming STT bake-off winner | Normal mode: notch, pointer, teaching board (WPF) |
| NVDA bridge add-on | Keys: tap/hold Right Ctrl, Caps Lock dictation, resume | Memory vault (§7), onboarding conversation |
| Speech service (Windows voices, now the fallback) | Speech: streaming cloud TTS (provider set by server) + audio cache + IPA reader | Installer (zip + Velopack), auto-update |
| `Redactor`, `SensitiveDetector` | Policy engine: 4 levels, 4× Right Ctrl gesture; `MemoryStore` → vault | Secrets Vault; tools: window, mouse, power, install_updates, startup_apps, disk_report, Spotify, web search |

Repository layout after the change:

```
src/
  Mubsir.Core/        (cross-platform logic, unit-tested on Linux CI)
  Mubsir.App/         (Windows host: hooks, UIA, audio, speech, tools)
  Mubsir.Overlay/     🆕 WPF: notch, pointer, board
  Mubsir.Setup/       🆕 Velopack installer + speaking bootstrapper
cloud/
  proxy/              🆕 Cloudflare Worker (TypeScript)
nvda-addon/
tests/                🆕 Core unit tests + voice-latency harness
docs/          PLAN.md, HANDOFF.md
CLAUDE.md     (instructions every Claude Code session reads first)
```

---

## 12. Build roadmap

Each phase ends with something a blind tester can use. Weeks assume one full-time developer working with Claude Code.

| Phase | Weeks | Deliverable | Done when |
| --- | --- | --- | --- |
| **0. Spikes** | 1 | STT bake-off (Egyptian/English), blind listening test (Fish / ElevenLabs / Azure / Google) incl. IPA test, Haiku 5.5 tool-use + computer-use check, latency baseline on a 4 GB PC | Vendor choices written down with numbers |
| **1. Cloud proxy** | 1–2 | Worker with `/register`, `/chat`, `/stt-token`, `/tts` (+ cache), `/update`, per-device caps, budget alerts, kill switch | App from a zip runs with **zero keys and no account** |
| **2. Live conversation** | 2–4 | Tap/hold Right Ctrl, streaming STT → Haiku → streaming TTS, barge-in, resume, 6 s follow-up window, local router | First word < 1.2 s on simple commands, interrupt < 150 ms |
| **3. Model router + tools** | 4–6 | Haiku→Sonnet escalation, new tools (window, mouse, power, updates, startup apps, disk, Spotify, web search), 4-level safety gate, Secrets Vault with 4× Right Ctrl | 50-task test list passes ≥ 90 % (open settings, find book, read TOC, go to page 1400, update then shut down…) |
| **4. Reading** | 5–7 | TOC, go to page, read N lines / N seconds, bookmarks per book, scanned Arabic PDF OCR, **book preparation** (prepared file, audio cache, local search index) | A 1,500-page PDF opens at page 1,400 in < 3 s; re-reading a page costs 0 tokens and 0 voice characters |
| **5. Memory vault** | 6–7 | Save chat, auto-save on shutdown, resume by date/topic, forget | "Yesterday at 6 PM" resumes correctly after a reboot |
| **6. Onboarding + installer** | 7–9 | `Mubsir.zip` with speaking installer, Velopack updates, onboarding conversation, profile | A blind tester installs from the zip and finishes onboarding **alone** |
| **7. Normal mode** | 8–11 | HTML prototype of every notch state (taste-skill) → owner picks → `motion-tokens.json` → WPF notch, pointer, teaching board, maths rendering, Caps Lock dictation | Owner approves the prototype; WPF notch 60 fps while moving, 0 % CPU idle, < 30 MB; sighted tester learns a new app with pointer guidance |
| **8. Pilot** | 11–13 | 10 blind + 5 sighted users in Egypt, weekly fixes, cost per user measured | Testers keep using it for 2 weeks |

---

## 13. How we will use the skills you asked for

Both are now installed in this repository under `.claude/skills/`, so every Claude Code session on
this repo can use them:

- **Agent Reach** (`.claude/skills/agent-reach`): read-only research across GitHub, YouTube
  transcripts, Reddit, X and more. Use it in phase 0 to collect user reviews and demo videos of
  Saywork, Clicky and similar tools, and to watch for new competitors.
  The `agent-reach` CLI installs on first use (`pipx install https://github.com/Panniantong/agent-reach/archive/main.zip`).
- **REA – Reverse Engineer Anything** (`.claude/skills/reverse-engineer-anything`): analyses shipped
  apps (native, .NET, Electron) to explain how a feature works. It needs its MCP server
  (`npx rea-agents setup`) and the app installed **on a Windows machine**, so it cannot run on Saywork
  from this cloud session. Use it on your own PC to study *how* Saywork achieves low latency or
  dictation into any app. Check Saywork's licence terms first; many forbid decompiling. We study
  behaviour and ideas, and write our own code. Clicky is MIT and open source, so we read it directly.

---

## 14. Decisions

Decided on 2026-11-04: see "Decisions locked" at the top.

Still open:

1. **Monthly budget** the owner is willing to spend during the pilot. This sets the per-device daily caps (§9).
2. **Pilot users:** can the owner find ~10 blind testers (schools, associations) for phase 8?
3. **Code signing certificate** before the pilot (about $10/month) — recommended.
4. **Distribution link:** the repo is private, so GitHub Releases can't be downloaded by testers. The zip
   will be hosted on the owner's Cloudflare account (R2) next to the server, or shared by Drive/WhatsApp.

## Sources

- Clicky: [farzaa/clicky](https://github.com/farzaa/clicky) (source and `CLAUDE.md` read directly), [clicky-windows](https://github.com/Bitshank-2338/clicky-windows)
- Saywork: Microsoft Store listing `9pnl0gh167b5` (summary via search; page not reachable from this session)
- Agent Reach: [Panniantong/Agent-Reach](https://github.com/Panniantong/Agent-Reach) (MIT)
- REA: [morluto/rea](https://github.com/morluto/rea) (MIT)
- ElevenLabs models: [elevenlabs.io/docs/overview/models](https://elevenlabs.io/docs/overview/models)
- ElevenLabs API prices (third-party summaries, Oct 2026): [cartesia.ai](https://www.cartesia.ai/learn/elevenlabs-pricing), [texttolab.com](https://texttolab.com/blog/elevenlabs-pricing), [micdrop.dev](https://micdrop.dev/blog/elevenlabs-api-pricing-voice-agent)
- Claude model IDs and prices: Anthropic API reference, as of 2026-10-06
