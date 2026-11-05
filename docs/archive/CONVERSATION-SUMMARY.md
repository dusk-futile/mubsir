# Planning conversation — full summary (2026-11-04)

This is everything the owner (dusk-futile) asked for and decided in the first planning chat, so a new
chat can continue without losing anything. Details live in `docs/PLAN.md`; the current state and next
steps are in `docs/HANDOFF.md`.

## The goal, in the owner's words (summarised)

A Windows tool, combining what **Clicky** (screen tutor that points and explains) and **Saywork**
(hold Ctrl to dictate, Egyptian Arabic + English, voice tasks) do, and doing more than both:

- An **agent with full access** to the PC: anything a mouse and keyboard can do, the voice can do.
  Open apps and pages, change settings, move/resize/maximise windows, browse Google, run updates,
  check storage, play a song on Spotify, shut down after updating, choose startup apps.
- Built for **blind people** and for people who don't understand computers, and also for sighted users.
- **Press a key, hear a sound, talk**, get an answer; press again to stop; continue later. Conversation
  should feel like ChatGPT voice: fast and natural.
- **Two modes:** blind mode (pure voice and sounds, nothing on screen) and normal mode (an animated
  companion on screen that shows listening / thinking / doing, points at things and draws to teach,
  e.g. maths or English).
- **Reads anything, fluently**, in Arabic (incl. Egyptian) and English, mixed: books by page ("go to page
  1400"), tables of contents, "read the first two lines", "read for 30 seconds", re-read, continue.
  Must handle **phonetic (IPA) symbols** in phonology books, maths and other strange symbols.
- **Memory:** "save chat" summarises a long conversation into an encrypted, tiny local file, so
  tomorrow "continue what we did yesterday at 6 PM Egypt time" works.
- **Onboarding:** after install it asks "blind or sighted?", the user's job and needs ("I'm an English
  teacher and can't read printed text"), and adapts.
- **Light** on weak PCs (4 GB RAM).
- Code must be **flawless**: best possible, tested, with real accuracy numbers.

## Decisions (final unless the owner changes them)

| Topic | Decision |
| --- | --- |
| Platform | Windows only (10 22H2, 11) |
| Price | Completely free for users; the owner pays all AI/voice costs |
| Distribution | Owner sends a zip / link; it connects to the owner's server automatically. No keys, no account. Microsoft Store (free signing) is the preferred route if it works |
| Brain | Claude Haiku 5.5 by default, Sonnet 5.5 for hard tasks |
| Voice | Fish Audio (hosted API) is the lead, confirmed by a blind listening test vs ElevenLabs, Azure, Google. Always the good voice (no daily switch to Windows voices; the owner finds Windows voices bad) |
| Access | Full access including passwords and payments. Payments and passwords need **Right Ctrl pressed 4 times** after Mubsir reads the exact service, site and amount. Passwords can be said and spelled ("124, dusk, D-U-S-K, capital D"); Mubsir reads them back and accepts corrections; recognised on the PC only |
| Books | Owner has rights to the books → shared library on the owner's server (prepared text + voice audio), so each sentence is voiced once for everyone. Only previous/current/next page is prepared. Not everyone reads whole books: pay only for what is read |
| Limits | Free things: no limit, only a listening check-in the user can set or turn off ("read without stopping"). Paid things: "continue?" every 2 minutes while new audio is made; a high abuse guard; budget alerts to the owner. Users never hear "limit reached" |
| Devices | One user = one PC. No multi-device sync |
| Hosting | The owner's own PC runs nothing for users. Users' PCs do local work and storage; a Cloudflare relay holds the keys |
| Identity | Name **Mubsir · مُبصِر**. Character: **Hudhud (hoopoe)**. Companion in the **bottom-right corner** (like ChatGPT's floating button) in normal mode |
| Repository | Stays **public** (making it private erases its 71 stars). Never commit keys. Every commit authored by **dusk-futile** only, no AI attribution lines. Old code may be restructured or replaced, keeping the best parts |
| Pricing estimates | Accepted: ≈ $8 / $25 / $60 per user per month (light / typical / heavy reader); pilot of 10 ≈ $200 / month. Usage is irregular, so real averages are lower |

## Design feedback

- **The first Hudhud prototype (`prototypes/hudhud-companion/`) looks bad** — owner's verdict. Keep its
  behaviour (states, flow, bottom-right placement, 4× Ctrl payment) but redo the visual design.
- Next design round: the owner has a **ChatGPT subscription** and will give the next chat access to it,
  so it can use **image generation** to explore and iterate on the Hudhud character and the companion
  UI, then rebuild the prototype from the chosen images. See `docs/HANDOFF.md` → "Next session".

## Skills added to the repo (`.agents/skills/`)

`agent-reach` (research), `reverse-engineer-anything` (REA), `taste-skill` (UI taste),
`find-skills` (find more skills). All MIT.

## Still open

1. Done 2026-11-04: history rewritten so every commit is authored by dusk-futile (contributors list
   cleaned).
2. Done 2026-11-04: the SDK is pinned to 10.0.401 (#10) and CI is green on `main`.
3. Owner to create accounts: Anthropic, Fish Audio, Cloudflare; set spend limits; never put keys in the repo.
4. Pilot testers (~10 blind, 5 sighted) and ~50 real Egyptian/English voice recordings for the STT test.
5. Run the build on a real Windows PC (a coding agent running on that PC), because the
   end-to-end tests need Windows, a microphone and NVDA.
