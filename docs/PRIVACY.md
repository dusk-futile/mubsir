# Mubsir privacy policy

Mubsir is free and has no account and no server of its own. Nothing is sent to the people who make Mubsir.

## What leaves your PC, and only when you ask for something

You choose these services by giving Mubsir your own keys. Each one has its own privacy policy.

| Goes to | What | Why |
| --- | --- | --- |
| Fish Audio, or Google Gemini if you added that key | Your recorded voice. With Gemini, also the name of the app you're in and of the focused item. | To turn speech into text |
| Google Gemini, or Anthropic (Claude) if you added that key, or the service whose address you typed as the brain | Your request, and the screen or document text it needs. Sometimes a picture of the screen or a page, if you allowed it. When you ask about an earlier day, the saved chat lines for that day or with those words. | To answer and to act |
| Fish Audio | The Arabic sentences Mubsir says to you, unless you use the free Windows voice. English speech is made on your PC | To make the voice |

Hearing a request is off until you've agreed on first start. Saying "private mode" stops everything being sent.

If you type the address of another service as the brain, everything in the second row goes to that address instead, under that service's own policy. Mubsir only accepts an https address, or one on your own PC.

Google says it uses free Gemini requests to improve its products, and Fish Audio says the same of its free voice. Paid Gemini requests aren't used that way. Add paid keys if that matters to you.

## What stays on your PC

- Your keys, encrypted for your Windows account.
- Passwords, card numbers and one-time codes. They're hidden before anything is sent. This depends on Windows reporting the field correctly, so don't rely on it for an unusual site.
- On bank and payment pages the AI switches off.
- Your saved chats, as files in `Documents\Mubsir\Chats`. Card numbers and codes are hidden first. You can turn this off in settings, or have them encrypted so only your Windows account can read them. They leave the PC only when you ask about an earlier day, as the table says.
- The text of your books. The first time you ask which book holds some words, each book's text is kept compressed in `%LOCALAPPDATA%\Mubsir\book-text`, so the next search is quick. It's never sent anywhere.
- Logs. They hold timings and error codes, never what you said, read or typed.

## Questions

Open an issue at https://github.com/dusk-futile/mubsir/issues.

## Books

If you agree during setup, the first page of each book on your PC is sent to Anthropic once, so Mubsir can describe it and find it by subject. Short files, and anything that looks like a bill, statement or ID, are skipped. Book audio you've heard is saved in `%LOCALAPPDATA%\Mubsir`, up to 500 MB, so hearing it again is free. Private mode saves none.
