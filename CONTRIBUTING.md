# Contributing to Mubsir

Thanks for helping. Mubsir is used by people who can't see the screen, so a change is only done when it
works by voice and keyboard alone.

## Build and test

You need Windows 10 or 11, the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (the exact
version is in `global.json`) and Python 3.

```powershell
dotnet build Mubsir.slnx -c Release
dotnet test tests/Mubsir.Core.Tests -c Release --filter "FullyQualifiedName!~A_folder_that_cant_be_written_never_breaks_anything"
```

The filtered-out test writes outside the repo on Windows and is never run locally.

`src/Mubsir.Core` builds and tests on any OS. Running the app needs Windows. To try things without keys:

- `Mubsir.exe --overlay-demo` shows the notch on its own.
- `Mubsir.exe --book-check tests/fixtures/books results.txt` reads the test books aloud end to end.
- `powershell -File scripts/package.ps1 -Version v0.0.0` builds the release zip into `out/`.

Warnings are errors, and unused code counts as a warning, so the build tells you when something is left
behind.

## Rules that matter here

- **Never commit keys or secrets.** Users' keys live only in `secrets.dat`, encrypted with DPAPI.
- **Logs hold timings and error codes only,** never what someone said, read or typed.
- **`Mubsir.Core` stays free of Windows APIs,** so its logic can be tested anywhere.
- **Plain reading never goes through the AI.** Text goes from the page to the voice.
- **Everything is spoken.** Anything that takes over two seconds makes a sound and says what it's doing.
- **The smallest change that fully works.** Delete before adding; no options or layers nobody asked for.
- New logic with a branch, a loop or a parser comes with a test.

## Pull requests

Keep each one to one change, say what you tested and how, and include the numbers if you measured
speed or memory. If it changes what users hear, describe what it sounds like now.
