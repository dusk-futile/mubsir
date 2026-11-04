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
| Voice | **ElevenLabs everywhere** (conversation and reading). Windows voices only as an emergency fallback |
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

The existing Mubsir code (29k lines of C#) already has #1, #2 and #4 in part (§11).

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
   │ Mouth: streaming TTS (ElevenLabs / Windows voices)   Eyes: notch + pointer + board   │
   └───────────────────────────────────────────────────────────────────────────────────────┘
                                   │ HTTPS
                     Mubsir Cloud proxy (keys, accounts, usage limits)
                     ├── Anthropic (Claude Haiku 5.5 / Sonnet 5.5)
                     ├── STT provider (streaming, Egyptian Arabic)
                     └── ElevenLabs TTS
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
                       /tts         → ElevenLabs streaming (+ audio cache)
                       /update      → Velopack update feed
                       Storage: device tokens + usage counters only (Cloudflare D1 / KV)
                       Secrets: ANTHROPIC_API_KEY, ELEVENLABS_API_KEY, STT key (Wrangler secrets)
```

The server never stores audio, transcripts or screen text. It counts tokens and characters per device.

### Protecting the owner's money (required because it is free)

| Protection | Why |
| --- | --- |
| One token per device, created on first start | A leaked token can be switched off without breaking everyone |
| **Daily caps per device** (e.g. 300 AI turns, 90 minutes of ElevenLabs audio), set from the server | One heavy user or a script can't run up the bill |
| **Global monthly budget** with an alert at 50 % / 80 % and a hard stop at 100 % | No surprise bills |
| Over-cap behaviour: keep working with Windows voices, say "premium voice is back tomorrow" | The user is never left without speech |
| Invite codes (optional switch) | If the zip spreads beyond the people it was sent to |
| Kill switch per device and for everyone | Abuse or a bug |
| Also set spend limits in the Anthropic and ElevenLabs consoles | Second line of defence |

### Voices: ElevenLabs everywhere

Decision: ElevenLabs for everything, because it sounds natural and Windows voices sounded bad to the
owner. Note: the voices that sounded bad were most likely the old SAPI voices. Windows 11 also has
"natural" neural voices that are much better; they are the fallback when the daily cap is reached or
the internet is down, not the main voice.

| Job | ElevenLabs model | Why |
| --- | --- | --- |
| Conversation (short answers) | **Flash v2.5**, streaming websocket | Lowest delay (~75 ms model time) and half the price |
| Arabic, especially Egyptian | **Multilingual v2 / v3**, test in week 1 | Early reports say Flash sounds weak in Arabic; pick per language after listening tests |
| Reading books and documents | Same voices, with an **audio cache** | A page already read is replayed from disk, never paid twice |
| Speech-to-text | Bake-off in week 1: current Gemini path, ElevenLabs Scribe realtime, Deepgram, Soniox on 50 real Egyptian/English recordings | Egyptian code-switching accuracy decides |

### What it will cost (estimate, verify on elevenlabs.io/pricing)

Reported API list prices (October 2026): **Flash v2.5 ≈ $0.04 per 1,000 characters**,
**Multilingual v2 / v3 ≈ $0.08 per 1,000 characters**. 1,000 characters ≈ 1 minute of speech.

| User type | Speech per day | ElevenLabs / month | Claude (Haiku-first) / month | Total / month |
| --- | --- | --- | --- | --- |
| Light (questions, commands) | 10 min | ≈ $12 (Flash) – $24 (Multilingual) | < $1 | **≈ $13 – $25** |
| Heavy reader (1 h of books daily) | 70 min | ≈ $85 – $170 | ≈ $1–2 | **≈ $85 – $170** |

ElevenLabs is cheap **per sentence**, but **reading books is the expensive part**. For 10 pilot users
that is roughly $150 – $1,000 a month depending on how much they read. Ways to bring it down, in order:

1. Audio cache (same page never billed twice; shared across users for the same public book file).
2. Flash v2.5 wherever the listening test says it is good enough, Multilingual only for Arabic if needed.
3. A daily premium-audio cap, with Windows natural voices after it.
4. Apply to ElevenLabs' programmes for non-profit / accessibility projects and ask Anthropic about
   startup or non-profit credits. A free tool for blind Arabic speakers is a strong case. (Not verified
   what is on offer today; check before the pilot.)

## 10. Staying light on old PCs

Target: 4 GB RAM Windows 10 PCs (the current README target).

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
| Speech service (Windows voices, now the fallback) | Speech: streaming ElevenLabs path + audio cache | Installer (zip + Velopack), auto-update |
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
| **0. Spikes** | 1 | STT bake-off (Egyptian/English), ElevenLabs Arabic voice test, Haiku 5.5 tool-use + computer-use check, latency baseline on a 4 GB PC | Vendor choices written down with numbers |
| **1. Cloud proxy** | 1–2 | Worker with `/register`, `/chat`, `/stt-token`, `/tts` (+ cache), `/update`, per-device caps, budget alerts, kill switch | App from a zip runs with **zero keys and no account** |
| **2. Live conversation** | 2–4 | Tap/hold Right Ctrl, streaming STT → Haiku → streaming TTS, barge-in, resume, 6 s follow-up window, local router | First word < 1.2 s on simple commands, interrupt < 150 ms |
| **3. Model router + tools** | 4–6 | Haiku→Sonnet escalation, new tools (window, mouse, power, updates, startup apps, disk, Spotify, web search), 4-level safety gate, Secrets Vault with 4× Right Ctrl | 50-task test list passes ≥ 90 % (open settings, find book, read TOC, go to page 1400, update then shut down…) |
| **4. Reading** | 5–7 | TOC, go to page, read N lines / N seconds, bookmarks per book, scanned Arabic PDF OCR | A 1,500-page PDF opens at page 1,400 in < 3 s |
| **5. Memory vault** | 6–7 | Save chat, auto-save on shutdown, resume by date/topic, forget | "Yesterday at 6 PM" resumes correctly after a reboot |
| **6. Onboarding + installer** | 7–9 | `Mubsir.zip` with speaking installer, Velopack updates, onboarding conversation, profile | A blind tester installs from the zip and finishes onboarding **alone** |
| **7. Normal mode** | 8–11 | Notch, pointer, teaching board, maths rendering, Caps Lock dictation | Sighted tester learns a new app with pointer guidance |
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
