# Setting up the PC / تجهيز الجهاز

For the sighted helper in the family. Week 1 is about NVDA only: Basira comes later, and the PC should already talk and be comfortable to use before then. Each step says what to check before moving on.

هذا الدليل لمن يساعد في التجهيز من الأسرة. في الأسبوع الأول نثبّت قارئ الشاشة NVDA فقط، ونتدرّب عليه قبل أن يصل بصيرة.

## What you need

- The PC, signed in to **his** Windows account, connected to the internet and to power.
- An administrator password for the PC (Windows will ask for it).
- A microphone or headset (the laptop's built-in microphone is fine to start).
- This repository on a USB stick or downloaded as a ZIP from GitHub (`scripts/` folder).
- About one hour.

## Week 1

### 1. Take an inventory (5 minutes)

Open **Windows PowerShell** (Start menu, type `powershell`, Enter). Then:

```powershell
cd D:\basira          # wherever the repository is
powershell -ExecutionPolicy Bypass -File .\scripts\inventory.ps1 -Label father-pc
```

It prints the CPU, memory, disk (SSD or HDD), Windows version, microphones, NVDA, Arabic speech and OCR features, and Windows 10 extended security (ESU) status, and saves everything to `inventory.json`. It asks one question: **does the keyboard have a Ctrl key to the right of the space bar?** Answer Y or N (Basira's talk key is Right Ctrl by default; without it we choose another key later).

Send `inventory.json` to Omar. It contains no personal files, only hardware and settings. Running it again as administrator also shows which Arabic features are installed.

### 2. Run the setup script (20–40 minutes)

Right-click the Start button, choose **Windows PowerShell (Admin)** or **Terminal (Admin)**, and run:

```powershell
cd D:\basira
powershell -ExecutionPolicy Bypass -File .\scripts\setup-father-pc.ps1 -KeyboardLayout laptop
```

Use `-KeyboardLayout desktop` if the keyboard has a number pad, or leave it out to let the script decide (laptop when the PC has a battery). To see what it would do without changing anything, add `-DryRun`.

The script:

1. checks the PC is 64-bit Windows 10 22H2 (build 19045) or newer, and stops with an explanation if not;
2. downloads NVDA 2026.2, checks that the file is exactly the official one (SHA-256 fingerprint and the "NV Access" digital signature), and installs it so it also talks on the sign-in screen and starts after sign-in;
3. sets NVDA to Arabic, Windows OneCore voices, automatic Arabic/English voice switching, no welcome dialog, no statistics question and **no automatic NVDA updates** (we update NVDA only after testing the Basira add-on with the new version);
4. copies these settings to the sign-in screen (without add-ons);
5. adds the Windows Arabic language, Arabic voice and Arabic text recognition (OCR) features; this can take 10–20 minutes and needs the internet;
6. creates a "Basira" task that starts Basira at sign-in. It stays **disabled** until Basira is installed.

At the end it prints a **summary**: what was done, what was skipped, and what you still need to do by hand. Exit code 0 means everything worked; 1 means a step failed (read the red lines and run the script again: it is safe to repeat); 2 means this PC or session cannot be set up.

After it finishes, **restart the PC once.** NVDA should speak Arabic on the sign-in screen and start talking a few seconds after sign-in.

### 3. Check the microphone privacy setting

Windows can silently block the microphone. Open **Settings > Privacy > Microphone** (Windows 11: **Privacy & security > Microphone**) and make sure that:

- "Allow apps to access your microphone" is **On**;
- "Allow desktop apps to access your microphone" is **On** (Basira is a desktop app).

Also check that the laptop has no microphone mute key turned on (often F4 or a key with a crossed-out microphone).

### 4. Practise NVDA together (every day this week, 15 minutes)

Start with these, in this order. The full list is in [keymap.md](keymap.md).

| Practise | Keys |
|---|---|
| Stop speech | Ctrl |
| Move between items | Tab, Shift+Tab |
| Switch windows / close a window | Alt+Tab, Alt+F4 |
| Open a program | Windows key, type the name, Enter |
| Switch keyboard language | Alt+Shift |
| Web pages: browse / type mode | NVDA+Space (NVDA = Insert) |
| Hear what a key does | NVDA+1, then press keys; NVDA+1 again to stop |
| Restart NVDA if it goes silent | Ctrl+Alt+N (check that this works on his PC) |

A good first goal: open Chrome or Edge, go to a news site, and jump between headings with H.

## Later: API keys

Basira works without keys in local mode (reading, OCR, layer keys). For voice requests and answers it needs two keys, which Omar creates and gives you:

- an **Anthropic** key (the "brain"), from a workspace with a monthly spend limit;
- a **Google Gemini** key (speech to text), from a project with a budget alert.

When Basira is installed, add them in PowerShell **as his user** (not as admin):

```powershell
basira --set-key anthropic
basira --set-key gemini
```

Each command asks for the key without showing it and stores it encrypted for his Windows account only. (This command is planned; the exact form is confirmed when the app ships.) Never write keys in a document, an email or a chat message; if a key leaks, Omar deletes it in the provider's console and makes a new one.

## Windows 10 extended security updates (ESU)

Windows 10 stopped getting free security updates in October 2025. For home PCs, Microsoft offers one more year of security updates (to **2027-10-12**) through ESU enrollment.

1. Sign in to Windows with (or link) a Microsoft account if asked; the PC must be fully updated first.
2. Open **Settings > Update & Security > Windows Update**.
3. Choose **Enroll now** under "Windows 10 support is ending" and follow the steps (free options include syncing settings with Windows Backup, or Microsoft Rewards points).
4. Run `inventory.ps1` again; it shows the ESU status when Windows exposes it ("unknown" is normal on many PCs: then check that Windows Update lists ESU updates).

If the PC can run Windows 11, upgrading is the longer-term option; NVDA and Basira support both.

## If something goes wrong

| Problem | What to do |
|---|---|
| NVDA is silent | Press Ctrl+Alt+N. If nothing happens, open NVDA from the Start menu. Check the volume and the speakers. |
| NVDA speaks English instead of Arabic | NVDA+N, Preferences, Settings, General, Language: Arabic; then restart NVDA. Or run the setup script again. |
| Arabic text is read in an English voice | Run the setup script again (Arabic voice feature), then restart the PC. |
| The script says the SHA-256 or signature is wrong | **Do not run that file.** Delete it and run the script again; if it repeats, tell Omar. |
| The script stops at "Add Windows capabilities" | Check the internet and run it again. On a work PC, the IT administrator may have to allow it. |
| Basira does not hear him | Microphone privacy setting (step 3), mute key, and the default microphone in Sound settings. |
| Basira behaves strangely | He can say «احفظ تقريرًا لعائلتي» ("save a report for my family"): a report without personal content is saved on the Desktop. Send it to Omar. |
| Anything else | Restart the PC. NVDA starts by itself; Basira restarts itself after a crash. Then tell Omar what happened and when. |

## ملخص بالعربية

1. شغّل `inventory.ps1` وأرسل ملف `inventory.json`.
2. شغّل `setup-father-pc.ps1` بصلاحيات المسؤول، ثم أعد تشغيل الجهاز.
3. تأكّد أن الميكروفون مسموح به في إعدادات الخصوصية.
4. تدرّبا كل يوم على أساسيات NVDA: Ctrl لإيقاف الكلام، وTab، وAlt+Tab، وAlt+F4، ومفتاح Windows.
5. سجّل الجهاز في تحديثات الأمان الممتدة (ESU) من Windows Update.
6. مفاتيح الذكاء الاصطناعي تُضاف لاحقًا، ولا تُكتب في أي رسالة أو ملف.
