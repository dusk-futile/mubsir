# Getting Mubsir into the Microsoft Store

CI builds an unsigned package, `Mubsir.msix` (artifact `mubsir-msix`), so the manifest in `packaging/AppxManifest.xml` is checked on every push. Nobody has installed it yet.

## What only the owner can do

1. Open a Partner Center developer account. Microsoft charges for this, so check the current price.
2. Reserve the name. Partner Center gives the real `Name` and `Publisher`; put them in `packaging/AppxManifest.xml`.
3. Publish `docs/PRIVACY.md` at a public URL, which the listing requires. The file's GitHub link works.
4. In the submission, justify `runFullTrust`: an assistive agent needs keyboard hooks, UI Automation and synthetic input.
5. Choose the age rating. Check the current AI-content and children's rules before submitting.

## Likely review questions (from the audit; unconfirmed until a real submission)

- A low-level keyboard hook can look like a keylogger. Explain that it only handles the talk key and logs no keys.
- Generative AI apps may need AI disclosure and a way to report harmful output. Neither exists as a feature yet.
- Without `uiAccess`, Mubsir can't act on windows running as administrator. It says so and stops.

## What the Store version can't do that `setup.ps1` does

| `setup.ps1` | Store version |
| --- | --- |
| Installs Arabic OCR and speech | Not possible. Mubsir says how to add Arabic speech if it's missing. |
| Installs NVDA | Not possible, and not needed unless the user wants NVDA. |
| Adds the Mubsir Bridge add-on to NVDA | Not built yet. It needs a button in settings that opens the add-on file so NVDA can install it. |
| Starts at sign-in with a scheduled task | Done by the manifest's startup task |
| Runs the supervisor that restarts it after a crash | Not used in the package. Needs testing. |
