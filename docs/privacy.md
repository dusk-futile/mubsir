# Privacy / الخصوصية

Basira reads the screen and listens when the talk key is held. Some of that has to go to cloud AI services to answer him. This page says exactly what goes where, what never leaves the PC, and how to delete everything. The retention terms below are as researched in October 2026; check the providers' current terms before each release.

## Who receives what

| Service | What it receives | When | Retention and use |
|---|---|---|---|
| **Anthropic** (Claude, the "brain") | The text needed for the request: his words, the focused window's name and visible text, a pruned list of on-screen controls. **Screenshots only when Basira announces it** ("looking at the screen"). | Questions and agent tasks | Deleted within 30 days. Content flagged by safety systems may be kept up to 2 years. Not used for training under the commercial API terms. |
| **Google Gemini** (paid tier, the "ears") | The talk audio, plus the app name and the focused control's name as hints | Only while the talk key is held | Not used for training on the paid tier. Logged for a limited period for abuse detection and legal reasons. |
| **Claude web search** (through Anthropic) | Search queries Basira writes for quick answers | When he asks something that needs the web | As for Anthropic; the search provider receives the query. |

There is no Basira server: requests go directly from the PC to these providers with the family's own API keys. Nothing is sent to the developers of Basira.

## What never leaves the PC

- **Passwords**: in a password field Basira only says "password field: type your password, then press Enter."
- **Card numbers, CVV, IBAN, PINs and one-time codes (OTPs)**: removed by a local redaction pass before any request; OTPs are read aloud locally only.
- **Bank, payment, wallet and government pages** (bank mode): no screenshots and no AI at all while one is open; Basira reads the amount and payee locally and he presses the final button himself.
- **File names** from the PC check: the report is built locally.
- **Logs content**: logs never contain text, transcripts, URLs or file names.
- **His memory and profile** (`profile.json`, `memory.md`): stored encrypted on the PC; only the parts needed for a request are included in it.

## Private mode

Saying «شغّل الوضع الخاص» ("private mode on") blocks **all** cloud calls. Reading with NVDA and Basira's local reading, OCR and layer keys keep working. Saying «أوقف الوضع الخاص» turns it off. Basira says which mode it is in when asked.

## Data on the PC

| What | Where | Notes |
|---|---|---|
| Settings, profile, memory | `%LOCALAPPDATA%\Basira\` | Profile and memory are encrypted with Windows DPAPI (his account only) |
| API keys | `%LOCALAPPDATA%\Basira\` | DPAPI-encrypted |
| Logs | `%LOCALAPPDATA%\Basira\logs\` | **Metadata only**: event type, tool name, latency, tokens, cost, error code. Kept **14 days**. A content-logging debug switch exists for troubleshooting and turns itself off after 24 hours. |
| NVDA quiet flag | `%LOCALAPPDATA%\Basira\nvda-quiet.json` | Only a timestamp |
| Speech history, last answer | Memory only | Gone when Basira exits |
| Diagnostics report | Desktop, only when he asks | Redacted, metadata only |

Test recordings of his voice (with his spoken consent) are kept only on the developer's machine in `evals/private/`, never in git, and are deleted after v1.

## Deleting everything

1. Exit Basira (or restart the PC with the "Basira" task disabled: `Disable-ScheduledTask -TaskName Basira`).
2. Delete the folder `%LOCALAPPDATA%\Basira` (type it in the File Explorer address bar).
3. Remove the task: `Unregister-ScheduledTask -TaskName Basira`.
4. Delete the API keys in the Anthropic and Google Cloud consoles so they can never be used again.
5. Optional: remove the Basira Bridge add-on in NVDA (NVDA+N, Tools, Add-on store, Installed add-ons).

Data already sent to the providers is deleted on their schedule above.

## الملخص المسموع (60 ثانية)

This is the text Basira speaks during setup before asking for his consent.

> بصيرة يساعدك على قراءة الشاشة واستخدام الجهاز بصوتك.
> عندما تضغط مفتاح التحدث، يُرسَل صوتك إلى خدمة Google لتحويله إلى نص، ولا يُستخدم لتدريب نماذجها.
> وعندما تسأل سؤالًا أو تطلب مهمة، يُرسَل نص طلبك وما يظهر في النافذة إلى خدمة Anthropic لتجيب، وتُحذف هذه البيانات خلال ثلاثين يومًا في العادة.
> لن أرسل صورة الشاشة إلا بعد أن أخبرك بذلك.
> كلمات المرور وأرقام البطاقات والرموز السرية لا تخرج من جهازك أبدًا.
> وفي صفحات البنوك والدفع لا أستخدم الذكاء الاصطناعي، وتضغط أنت زر الدفع بنفسك.
> السجلات على جهازك لا تحفظ كلامك ولا ما تقرؤه، وتُحذف بعد أربعة عشر يومًا.
> إذا أردت ألّا يخرج شيء من جهازك، قل: شغّل الوضع الخاص.
> هل توافق على ذلك؟ قل نعم، أو اضغط Enter. وللرفض قل لا، أو اضغط Escape.
