# Roadmap

Where Mubsir is going, and what's already done. Decisions live in `AGENTS.md`; this file is the plan.

## The goal

A clean, trustworthy, open-source Windows voice agent for blind and low-vision people. Unzip, paste your
own keys in the spoken setup, hold Right Ctrl and talk. It does everything a blind person needs on a PC,
reads every character of any book word for word (English, Arabic with tashkeel, IPA, maths), and sounds
like a person reading: the right voice for each language, an even breath between sentences, nothing
skipped. Everyday use costs almost nothing. The only visual is a very smooth notch.

## Rules

1. **One Claude model: Haiku 5.5.** No Sonnet, no escalation. When Haiku struggles, give it better input
   (a fresh screen snapshot, a cropped screenshot, one short question to the user), not a pricier model.
2. **The AI is never in the reading path.** Text goes from the page to the voice.
3. **Pay only for what is hard, and only once.** A broken page is fixed once and cached; audio already
   heard is kept.
4. **A local option is offered only when it is as good as the cloud one** and the PC can run it.

## Done

- [x] Free, bring-your-own-key app: no account, server or relay (2026-11-05)
- [x] Gemini free brain and hearing; Claude Haiku 5.5 when its key is there
- [x] Books read without the AI, with symbol, IPA and tashkeel reading, page checks and OCR repair
- [x] Fish Audio for Arabic, Ryan (Piper, on the PC) for English, Windows voices as the fallback, audio cache
- [x] Hudhud removed; the notch is the only visual (2026-11-07)
- [x] The demo-only pointer removed, and the stale cost box replaced by one line on the Claude key (2026-11-08)
- [x] `scripts/package.ps1` builds the release zip with `START-HERE.txt`; the release workflow uses it (2026-11-08)
- [x] Sonnet removed; Claude is Haiku only (2026-11-07)
- [x] Numbers follow the language of their label ("chapter 3" stays English, "صفحة 12" stays Arabic)
- [x] Opening brackets and quotes go with the words they open, so the voice switches at the word
- [x] √, Greek letters, ≤ ≥ ≠ ±, ∪ ∩ ∀ ∈, arrows, §, ¶, ©, ™ and check marks are named in ordinary text
- [x] Even, short silence between local-voice sentences, with faded edges so there is no click
- [x] Tests for language switching, symbol and IPA reading, sentence splitting, SSML and silence

## Next

### Sound
- [x] One voice per answer as an option (off by default): when an answer has Arabic, the Fish voice says
      all of it, so the speaker doesn't change mid-answer. Costs Fish for the English sentences
- [x] One audio player per voice, kept open, instead of a new one per sentence (`SentencePlayer`)
- [x] The Windows voice joins a mixed sentence's Arabic and English pieces into one clip with a word-sized
      gap, instead of playing each piece separately
- [x] Two sentences prepared ahead instead of one
- [x] Even loudness for Ryan and the Windows voice, piece by piece
- [ ] Even loudness for Fish (its audio is MP3, so it needs decoding first)
- [ ] Listening test on a real PC with the owner's grandfather: gaps, switching, speed

### Cheaper per task
- [ ] Keep only the latest screen snapshot in the conversation; older ones become one line
- [ ] Send only what changed on screen after an action, with stable element ids
- [ ] Replay a task that worked before ("open WhatsApp and read the last message") with checks, and ask
      the AI only if a check fails
- [ ] A test that fails if the cached part of the prompt changes
- [ ] More local commands that need no AI at all

### Clean repo
- [x] Warnings as errors and `.editorconfig`, with unused code as a warning
- [x] Fewer files: Win32 imports in one file per area, small types beside the code that uses them, one
      PNG export instead of eleven (2026-11-14)
- [ ] `dotnet format` check in CI
- [ ] Tests for the policy engine, redaction, router and context builder
- [ ] A fake brain so CI can run whole tasks without spending tokens
- [ ] A "blind tester": a second AI that is given only what Mubsir says, never the screen, and has to
      finish real tasks. Any silence or dead end is a failure
- [x] `CONTRIBUTING.md`, `SECURITY.md`, `CHANGELOG.md`
- [ ] Issue templates
- [ ] A short demo video with captions and sound for the README

### Make it personal
- [ ] A name for the assistant, chosen in setup
- [ ] A custom address for the brain (URL, key, model), so OpenRouter, OmniRoute or a model on the PC can
      be used. No router is bundled: OmniRoute alone needs Node and about 1 GB of memory
- [ ] A PC check in setup that picks lighter settings on a weak PC (medium Ryan or the Windows voice, a
      still notch)
- [ ] Optional downloads chosen in setup

### Wake name
- [ ] Optional and off by default: say the assistant's name instead of holding Right Ctrl, with the
      keyword spotting in sherpa-onnx, which already ships. Hold Right Ctrl always works, and dangerous
      actions still need a real key press. Ships only if it stays under about 40 MB of RAM and 3 % of one
      core, and an Arabic name proves reliable (the models are English and Chinese)

### Reading and memory
- [ ] Move by word, line, sentence, paragraph or heading: "go to the fourth line", "read the heading",
      "read the ending"
- [ ] Speed by percent: "20 % faster"
- [ ] Find the book that holds an exact phrase, from a local index of each book's text
- [ ] Encrypted chats as an option, off by default (encrypted chats can't be opened in Notepad)
- [ ] "What did we do yesterday?" answered on the PC from the saved chats and the reading journal

### Release
- [ ] Run `docs/WINDOWS-TEST.md` on the owner's PC
- [ ] Signed zip and the Store package (`docs/STORE.md`)

## How it works, and why

- **One agent, not several.** Each extra agent multiplies cost and delay. Long tasks get a spoken list of
  steps instead.
- **In front for apps, behind for the rest.** Anything that presses or types happens on the real screen,
  where NVDA can follow it. Files, book search, reminders and library scans run behind and say when
  they're done.
- **Cheapest way first.** A direct Windows call, then the accessibility tree, then a picture of the screen
  only when the first two fail.
- **No live voice API for now.** It bills the whole conversation again on every turn.

## Local options

| Job | Cloud | Local | As good? |
| --- | --- | --- | --- |
| Hearing | Gemini | Whisper | No for Arabic: free Whisper got 39–72 % of Arabic words wrong against 7 % for Gemini (`docs/RESULTS.md`). English only, later |
| English voice | — | Ryan (Piper) | Already the default, free |
| Arabic voice | Fish Audio | Windows voice | No. Offered as the free fallback |
| OCR | — | Windows OCR | Already local |
| Brain | Gemini / Haiku | none | No local model matches them on a normal PC |

## What it costs

Estimates, with your own keys.

| Use | Cost |
| --- | --- |
| Gemini free tier | $0 within Google's limits |
| One task on Claude Haiku 5.5 | about $0.001 |
| 100 Haiku tasks a day | about $3 a month |
| Reading in English (Ryan) | $0 |
| Reading in Arabic (Fish) | about $0.50 for 10 pages, once; heard pages are kept |

## PC

| | Minimum | Recommended |
| --- | --- | --- |
| Windows | 10 22H2 or 11, 64-bit | 11 |
| RAM | 4 GB | 8 GB |
| CPU | 2 cores | 4 cores (Ryan speaks faster) |
| Disk | about 300 MB with the English voice | |
| Other | Microphone, internet | Headset, NVDA |
