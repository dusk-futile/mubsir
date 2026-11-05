# Handoff

Update this file at the end of every session. Newest on top. Older entries are in `docs/archive/HANDOFF-OLD.md`.

# Latest session, 2026-11-05 (sixth part)

- **The owner has no keys yet and wants it free.** `Brain/GeminiBrain.cs` (with `Core/Agent/GeminiWire.cs`) is a new free brain, and `BrainRouter` uses Claude only if its key exists. A Gemini key alone is enough to start. `FishVoice` tries `s2.1-pro-free` first, then the paid model. Its pages give two end dates, the later being 2026-11-30.
- **Never run against the real services:** the Gemini brain and the free Fish model. No key was available. The tool format, thought signatures and limits are the risks. Tests B12, B13, D13 and D17 cover them.
- **Privacy:** Google and Fish may use free requests to improve their products. The setup and consent text say so. The owner should decide on paid keys before daily use by their grandfather.
- The free use isn't counted in "صرفت كام". Gemini hearing still is, at paid rates, so that command can read slightly high.

# Earlier, 2026-11-05 (fifth part)

- **Only blind and low-vision users now.** The owner is building this for their grandfather and will add their own keys. Sighted, beginner, hands-free, learner and child options are gone from `UserKind`, the first conversation and the prompt. The notch, Hudhud and the rest of the UI are unchanged. Anyone can still ask to be taught.
- CI failed because restoring the whole solution changed Core's and Overlay's lock files. Only App's lock file should hold the Windows packages.

# Earlier, 2026-11-05 (fourth part)

- **Voices decided:** Fish for Arabic only, with the Arabic narrator as default and 6 to choose from (`VoiceCatalog`). English is Ryan high from Piper at 90% speed, free, on the PC (`Speech/LocalVoice.cs`, via sherpa-onnx 1.12.40). Version 2.0 of sherpa-onnx can't phonemize Piper, so don't upgrade it without checking. It downloads in the background, with a pinned hash, unpacked with Windows `tar`. Unloads after 3 idle minutes. If it's too slow on a PC, the Windows voice takes over and the person is told.
- **Licences:** the Ryan model's data is CC BY-NC-SA 4.0, so it can't be used if Mubsir ever becomes commercial. Norman (public domain data) is the swap. espeak-ng is GPL-3.0, so the release that includes it is effectively GPL-3.0. The source is public. Have someone qualified confirm before the Store.
- `--book-check` now downloads Ryan and makes a sentence, so CI proves the engine runs on Windows. Playback by ear isn't proved.
- **Docs:** `docs/PRODUCT.md` describes the app. `docs/WINDOWS-TEST.md` is the full test. Old plans moved to `docs/archive/`.
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

