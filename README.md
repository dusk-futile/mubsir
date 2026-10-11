# Basira (بصيرة)

An AI screen assistant for blind Windows users. It reads the screen, answers questions about it and carries out tasks on the PC by voice or with a few keys, in Arabic and English. It is built to run alongside the free NVDA screen reader on old, low-memory PCs.

مساعد ذكي للمكفوفين على ويندوز: يقرأ الشاشة، ويجيب عن الأسئلة، وينفّذ المهام بالصوت أو بلوحة المفاتيح، بالعربية والإنجليزية.

- Talk in any Arabic dialect or English by holding one key; replies are short spoken Modern Standard Arabic (or English).
- Switches between Arabic and English voices inside a sentence, for its own speech and (with the Basira Bridge add-on) for everything NVDA says.
- An agent that narrates each step and asks for one key press before anything irreversible; bank pages, passwords and one-time codes never go to the AI.
- A few dollars a month of cloud AI; idle memory under 80 MB on 4 GB PCs.

## Status

Early development (milestone M0: foundations and spikes). Not ready for general use.

## Layout

| Path | What |
|---|---|
| `src/Basira.Core` | Platform-independent logic (net10.0): text segmentation, speech planning, input state machine, router, policy, context, cost. Builds and tests on macOS and Linux. |
| `src/Basira.App` | The Windows app (net10.0-windows): UI Automation, OCR, audio, keyboard hook, NVDA client. |
| `tests/Basira.Core.Tests` | xUnit tests for Core. |
| `nvda-addon/basiraBridge` | Basira Bridge, the NVDA add-on (language switching, quiet mode), with pytest tests in `nvda-addon/tests`. |
| `scripts/` | PC inventory, father's PC setup, add-on build, audio check; `scripts/dev/` for the Mac to Windows dev loop. |
| `evals/` | Task schema, synthetic tasks and the scorer. |
| `docs/` | Documentation (below). |

## Build

Requirements: the .NET 10 SDK (see `global.json`), Python 3.9+ for the add-on and evals.

```bash
# Core, on macOS, Linux or Windows
dotnet test tests/Basira.Core.Tests/Basira.Core.Tests.csproj

# NVDA add-on and evals
python3 -m pytest nvda-addon/tests evals/tests
python3 scripts/build-addon.py           # dist/basiraBridge-<version>.nvda-addon
```

```powershell
# The app, on Windows
dotnet build src/Basira.App -c Debug
dotnet publish src/Basira.App -c Release -r win-x64 -o publish
```

From a Mac, `scripts/dev/run.sh` builds and starts the app on a Windows test PC over SSH (see `scripts/dev/README.md`). CI runs the Core tests, the add-on tests and package, an audio-file check, and a Windows Release build.

## Documentation

- [Architecture](docs/architecture.md)
- [Setting up the PC](docs/father-setup.md) (for the sighted helper)
- [Keys and voice commands](docs/keymap.md)
- [Privacy](docs/privacy.md)
- [Manual test checklist](docs/test-checklist.md)
- [M0 spikes](docs/spikes.md)
- [Evals](evals/README.md)

## License

The app is MIT licensed (see `LICENSE`). The Basira Bridge NVDA add-on in `nvda-addon/basiraBridge` is GPL-2.0, as NVDA add-ons run inside NVDA (see `nvda-addon/basiraBridge/COPYING`).
