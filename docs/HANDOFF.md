# Handoff

Update this file at the end of every session. Newest on top.

# Latest session, 2026-11-08 (live notch and provider choice)

- **Follow-up:** finished steps are said in the past tense (`Core/Agent/StepWords.cs`: "Opening Settings" → "Opened Settings", "أفتح" → "فتحت"; names in quotes untouched; otherwise "Done"/"تم"), and the notch line is reworded the same way. Thinking is now the م signature (owner's pick, #10 of ten ChatGPT ideas shown in a gallery): written right to left, with a blurred glow copy of the stroke, ink colour drifting through the palette, 12 pooled stardust sparks, a radial-gradient halo on the tip, a breathing outline and shimmer text (`ThinkingPane`). Measured on a 6-core PC: 30–48 % of one core while thinking, about the same as the checklist. Setup shows the recommended free setup and an expander with brain models (Gemini 3.5 Flash-Lite / Flash via `GeminiBrainModel`, Claude Haiku 5.5), hearing and Arabic voice.

- **"Done" out loud:** `AgentLoop` calls `INarrator.StepFinished(ok)` after a narrated step runs; `Narrator` says "تم"/"Done." or plays the error sound, and the notch ticks or crosses out that line (`CompanionBridge.StepFinished`). Lines still running when the task ends are ticked. Tests: `StepDoneTests`.
- **Payment screens removed** (Confirm and Paid states, `ConfirmPane`, the demo's payment scene). Only the demo used them. The policy still keeps the AI off bank pages.
- **Notch:** the rim's four colours glide per state (`NotchWindow.RimColors`), swell and speed up with the voice, and turn from amber to green with a task's progress, pulsing at each finished step. Thinking is three drifting blurred lights (sky, lilac, pink) with no text. Labels removed from listening and speaking; Doing says "Step N"; Reading says "Book open" and "Page N of M". The listening wave is cool, the speaking wave warm. New colours `sky` and `pink` in `motion-tokens.json`.
- **Reading is wired:** `BookSession.Reading` gives the page while a book is read; `CompanionBridge.Book` shows it instead of captions.
- **Providers:** `Core/Setup/KeySetup.cs` has `BrainChoice`, `EarsChoice`, `KeysHeld` and `Providers` (choice first, the other as fallback; no choice = old behaviour). Settings `Brain` and `Ears` (null until chosen). `BrainRouter` and `EarsRouter` follow them. The setup window shows radio buttons for brain, hearing and Arabic voice, then only the needed key; it's dark (`ThemeMode.Dark`, Fluent) with notch colours. Tests: `ProvidersTests`.
- Checked on Windows 11 with `--setup-demo` and `--overlay-demo` screenshots. 550 Core tests pass. Not heard by ear.

# Earlier, 2026-11-08 (finishing pass)

- **Ponytail-style audit:** `.editorconfig` makes unused code a warning, and `Directory.Build.props` now treats warnings as errors (package advisories stay warnings). Fixed what it found: the setup window's cost box was never shown and its numbers were from the old Fish-for-everything setup, so it's replaced by one line on the Claude key row ("about $2 a month at 60 requests a day", from `CostGuide`); unused parameters in Fish and the host.
- **Pointer removed:** only the demo used it since teaching was removed. `PointerWindow`, the Pointing notch state, its springs and pane are gone.
- **Every tool connects:** all 43 tools in `ToolNames` have a catalog entry and an executor case.
- **Packaging:** `scripts/package.ps1` builds the release zip (works in Windows PowerShell 5.1 and pwsh 7, forward-slash zip paths) with `docs/START-HERE.txt`; `release.yml` calls it. Built and unzipped on Windows 11: `--book-check` all passed, notch demo 156–194 fps.
- **New:** `CONTRIBUTING.md`, `SECURITY.md`, `CHANGELOG.md` (0.2.0 unreleased). README leads with the download and has cost and PC sections.
- **Open:** private vulnerability reporting is off on GitHub, so `SECURITY.md`'s link needs it turned on. The only release is v0.1.0; README's install steps need a v0.2.0 release.

# Earlier, 2026-11-07 (sound)

- **One player per voice:** `Speech/SentencePlayer.cs` keeps one Windows media player open and is shared by Fish, Ryan and the Windows voice, replacing three copies of the play code.
- **Windows voice, mixed sentences:** `OneCoreSpeech` makes every language piece at once and joins them with `Core/Speech/SpeechAudio.Join`: each piece trimmed, brought to the same loudness, 50 ms between languages. Before, each piece played as its own clip with a fresh player. Falls back to piece by piece if Windows doesn't give plain WAV.
- **Ryan:** exact 40 ms / 140 ms silence around each sentence and even loudness.
- **Two sentences ahead** (`Core/Speech/SpeechAhead`) for Fish and Ryan.
- **New setting `OneVoicePerAnswer`, off by default:** an answer with any Arabic is said all in the Fish voice. In the setup window's options.
- `--book-check` passed on a Windows 11 PC. That PC has no Windows Arabic voice, so mixed Windows-voice sentences weren't heard in Arabic. 540 Core tests pass. Not judged by ear yet.

# Earlier, 2026-11-07

- **Hudhud removed** (owner's decision): the bird window, its art and rig, the panel it opened, its motion code, tests and setting. The notch and pointer are unchanged and still move on `motion-tokens.json`. `overlay-smoke.ps1` now checks the notch only.
- **Sonnet removed:** Claude is always Haiku 5.5 (`ClaudeBrain`). The brain picker in the setup window is now one line of text. `BrainModel` and `EscalationModel` settings are gone; old settings files still load.
- **Reading fixes, with tests:** numbers take the language of their label ("chapter 3" no longer switches to the Arabic voice for "3"); opening brackets and quotes go with the words they open; √, Greek letters, maths relations, arrows, §, ¶, ©, ™ and check marks are named in normal reading (`SymbolTables.NormalSymbols`); the Arabic comma is left alone.
- **Smoother local voice:** `Core/Speech/SpeechSilence.cs` trims Ryan's padding to 40 ms before and 140 ms after each sentence, with 6 ms fades, so sentences follow evenly and never click.
- **New tests:** `LanguageSwitchingTests`, `SymbolReadingTests`, `SentenceSplitterTests`, `SpeechCompositionTests`, `SpeechSilenceTests`. 529 pass on Windows with the folder test excluded.
- **Plan:** `docs/ROADMAP.md`. Next for sound: an optional one-voice-per-answer mode, one open audio output, two sentences prepared ahead.
- Not checked by ear on a real PC yet.

# Earlier, 2026-11-05 (eighth part)

- **Built, never run on Windows:** reminders (`Core/Reminders`, spoken from `MubsirHost`), `describe_photo`, `read_notifications` (`WindowsToolExecutor.Daily.cs`), Spotify and WhatsApp steps in `AppRecipes`. Notifications may need the Store build's identity. The owner doesn't want external or remote help features.

# Earlier, 2026-11-05 (seventh part)

- **Teaching removed:** Mubsir only does what's said. The overlay's pointer code stays for the demo, and nothing else uses it.
- **Asking his AI chat is new and has never run on Windows or against the real sites:** `Core/Chat/ChatRouting.cs` picks plain questions, and `WindowsToolExecutor.Chat.cs` types them into ChatGPT or Gemini and reads the reply. Reading the answer off a real page is the main risk.
- **"Run tests":** `scripts/run-tests.ps1`, explained in `AGENTS.md` and `docs/WINDOWS-TEST.md`. It has never run on a real PC.

# Earlier, 2026-11-05 (sixth part)

- **Start here on Windows:** `docs/WINDOWS-TEST.md` has numbered tests and the prompt to paste. The latest finished CI run passed, including Ryan on Windows.
- **Open questions for the owner:** free Gemini or a paid Claude brain, a paid Gemini key for privacy, and Fish's end date for its free model.

- **The owner has no keys yet and wants it free.** `Brain/GeminiBrain.cs` (with `Core/Agent/GeminiWire.cs`) is a new free brain, and `BrainRouter` uses Claude only if its key exists. A Gemini key alone is enough to start. `FishVoice` tries `s2.1-pro-free` first, then the paid model. Its pages give two end dates, the later being 2026-11-30.
- **Hearing:** the owner asked about Handy, a dictation app built on Whisper. Free Whisper got 39% to 72% of Arabic words wrong against 7% for Gemini (`docs/RESULTS.md`), so Whisper is not built in. Gemini stays for hearing until real speech says otherwise.
- **Gemini tested for real from the Mac** (numbers in `docs/RESULTS.md`): the format works, but Flash-Lite often claimed an action without calling a tool, so `GeminiBrain` now forces a tool for action requests and holds their text back. The free Fish model is still untested: no account. `GeminiBrain` itself has not run. Tests B12, B13, D13 and D17 cover the rest.
- **Privacy:** Google and Fish may use free requests to improve their products. The setup and consent text say so. The owner should decide on paid keys before daily use by their grandfather.
- The free use isn't counted in "صرفت كام". Gemini hearing still is, at paid rates, so that command can read slightly high.

# Earlier, 2026-11-05 (fifth part)

- **Only blind and low-vision users now.** The owner is building this for their grandfather and will add their own keys. Sighted, beginner, hands-free, learner and child options are gone from `UserKind`, the first conversation and the prompt. The notch, Hudhud and the rest of the UI are unchanged. Anyone can still ask to be taught.
- CI failed because restoring the whole solution changed Core's and Overlay's lock files. Only App's lock file should hold the Windows packages.

# Earlier, 2026-11-05 (fourth part)

- **Voices decided:** Fish for Arabic only, with the Arabic narrator as default and 6 to choose from (`VoiceCatalog`). English is Ryan high from Piper at 90% speed, free, on the PC (`Speech/LocalVoice.cs`, via sherpa-onnx 1.12.40). Version 2.0 of sherpa-onnx can't phonemize Piper, so don't upgrade it without checking. It downloads in the background, with a pinned hash, unpacked with Windows `tar`. Unloads after 3 idle minutes. If it's too slow on a PC, the Windows voice takes over and the person is told.
- **Licences:** the Ryan model's data is CC BY-NC-SA 4.0, so it can't be used if Mubsir ever becomes commercial. Norman (public domain data) is the swap. espeak-ng is GPL-3.0, so the release that includes it is effectively GPL-3.0. The source is public. Have someone qualified confirm before the Store.
- `--book-check` now downloads Ryan and makes a sentence, so CI proves the engine runs on Windows. Playback by ear isn't proved.
- **Docs:** `docs/PRODUCT.md` describes the app. `docs/WINDOWS-TEST.md` is the full test.
- **Next:** the owner gives a Windows session full access, and it runs `docs/WINDOWS-TEST.md`. Errors and troubleshooting matter most to them.

# Latest session, 2026-11-05 (third part)

- **The owner wants it to work the PC like a person, with blind users first.** New tools in `Core/Brain` and `Actions/WindowsToolExecutor.Hands.cs`: `click_on` (looks at the window, its label goes through the same policy as a button), `open_file` (installers need a key press, scripts and unknown types are refused), `create_folder`, `read_clipboard`, `wait`, `choose_voice`. Dragging isn't built.
- **Built:** numbered Google results (`Core/Web/SearchResults.cs`), book descriptions after consent (`Core/Library/BookDescriber.cs`, run by `LibraryScanner`), phonetic names (`Core/Text/IpaReader.cs`), Arabic books in Fish by default with their audio saved, one voice per language (`Core/Speech/VoiceCatalog.cs`), more page pictures a day (25 per book).
- **Fixed after the Windows check:** the lost letter (`OcrRepair` now searches the whole OCR page, because OCR breaks lines elsewhere; a neighbouring word must match). The check skips that sentence when CI failed to install Arabic OCR. A spoken warning covers Arabic OCR missing on a real PC. Read-ahead: `FishVoice.GetReady` fetches the next sentence once the current one starts. `choose_voice` number 0 is the free voice for books.
- **Next:** `docs/WINDOWS-TEST.md`, including new rows 16 to 22. None of this has run on Windows.

# Latest session, 2026-11-05 (second part)

- **Product changed:** a free assistive agent for everyone, on the user's own keys. The relay (`cloud/proxy`) is gone. `AGENTS.md` has the decisions.
- **Built, compiled, 240 Core tests pass on the Mac, never used on a real PC:** setup window (`Mubsir.Overlay/SetupWindow.cs`, `Host/SetupCoordinator.cs`), first conversation (`Core/Onboarding`), Fish voice with fallback (`Speech/FishVoice.cs`), Fish hearing with fallback (`Brain/FishEars.cs`), teaching mode enforced by the policy (`Core/Brain/TeachMode.cs`, tool `point_at`), saved chats (`Core/Sessions`), notch connected to the real app (`Host/CompanionBridge.cs`), Store package (`packaging/`, `scripts/pack-msix.ps1`, `docs/STORE.md`), `docs/PRIVACY.md`.
- **Antigravity audit (5 Gemini 3.8 Flash jobs):** confirmed findings fixed: Ctrl didn't stop tasks, "إيه" counted as yes, deletes and payments now need a real key press, file moves after reading a folder need a yes, a hung UIA could end screen reading for good, a stopped book could restart, huge files could crash it, "continue" opened old books, hook reinstall on wake. Wrong finding rejected: it said Haiku 5.5 doesn't exist.
- **Next:** run `docs/WINDOWS-TEST.md` on the owner's PC. Fish hearing in Arabic is the biggest unknown. The owner likes the notch and listening wave as they are: don't change the visuals unless asked. Blind users are the main audience.

