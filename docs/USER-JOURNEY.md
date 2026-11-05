# In a blind user's shoes

Written 2026-11-05 from the owner's scenario: a blind man turns on his PC, wants a book he can't name, in a place he doesn't remember, with no useful file name. This file walks through that, says what the code does **today**, and lists what to build. PLAN §6 and §7 hold the wider design; where this adds to them, this is newer.

Status marks: ✅ built, 🟡 partly, ❌ not built.

## The rules this scenario teaches

1. **Never silent.** Anything over 2 seconds gets a sound, then a progress line: "still looking, 200 files so far."
2. **Say what happened, after checking it happened.** "Renamed it to *Introducing Phonology.pdf*. It's still in Downloads." Never "done" before the check.
3. **One key always stops everything,** with no internet and no AI involved.
4. **Lists are numbered, and short.** Four at most, then "say more". He can't look back at a list, so numbers must keep working until a new list replaces them.
5. **Plain reading never goes through the AI.** It's faster, free and word for word.
6. **Never guess in silence.** "I'm not sure about this word" beats a wrong word said confidently.
7. **It knows him.** His name, his book, where he stopped, how he likes to work.

## The walk

| # | He says | He hears | What happens | Today | Tokens |
| --- | --- | --- | --- | --- | --- |
| 1 | (turns on the PC) | A soft sound, then "Good morning, Omar. Yesterday you stopped at page 34 of *Introducing Phonology*. Say continue, or tell me what you need." | Starts at sign-in, reads the reading journal locally | 🟡 starts, says ready; no journal | 0 |
| 2 | "Give me the book for phonology" | "Looking." (sound) "I found 5. One: *Introducing Phonology*. Two: an unnamed scan that starts with 'Sounds of English'. Three… Four… Say a number, or more." | Searches library cards: title, author, contents and first pages of every book on the PC | ❌ `find_files` matches file names only, in Desktop, Documents, Downloads and similar, 4 folders deep | 0 |
| 3 | "Read me the contents of each" | "Number one has 12 chapters: Phonetics, Phonemes… Stop me any time." Main chapters only; "details" reads the rest | Contents from PDF bookmarks, or detected headings. AI only if a book has neither, once, then saved | ❌ | ~0 |
| 4 | "Number 3" / "التالت" / "the Roach one" / "the last one" | "Opened *number three*, 312 pages, in its own language. Start at the beginning, or the contents?" | The app remembers the last list and resolves the choice itself | ❌ goes through the AI each time, and the system prompt discourages numbered lists | 0 |
| 5 | "Name it the exact title" | "Renamed from *scan_0042.pdf* to *Introducing Phonology.pdf*. It's in Downloads." | Title from the book's own first pages, renames, checks the new file exists, keeps undo | 🟡 rename and undo work; **renaming loses his place** (see gaps) | ~0 |
| 6 | "Go to the start" | "Chapter 1, Phonetics, page 1. Reading." | Printed page numbers, not PDF ones | 🟡 | 0 |
| 7 | (listens) | The page, in the book's language; symbols said by name | Page checked locally first (below). Next and previous pages prepared locally | 🟡 goes through the AI | 0 on clean pages |
| 8 | Taps Ctrl, "what's an allophone?", "continue" | Answer in his language, with the page it came from; then it resumes on the exact word | Local search finds 3–5 passages; only those go to Haiku | 🟡 sends up to 90,000 tokens | ~2,000 |
| 9 | Next day: "which page did I read yesterday?" | "Yesterday at 9 you read pages 30 to 34. You stopped at 'allophones are variants'. Shall I continue from that line?" | Answered from the encrypted journal on his PC | ❌ only the last place per file, no history | 0 |

## What I found in the code (real gaps)

1. **Reading goes through the AI.** `read_document` hands up to 6,000 tokens of text to the model, and the model repeats it. About $0.0036 per chunk on Haiku, it adds to every later turn, and only an instruction ("read it faithfully") stops it changing words. **Biggest fix for both cost and accuracy.** **Fixed on `feat/reading-direct`.**
2. **Renaming or moving a book loses his place.** `ReadingPositions` is keyed by the file's path. Step 5 of the scenario would break step 9. Key it by a fingerprint of the file. **Fixed on `feat/reading-direct`.**
3. **The place is saved before he hears it.** It's stored when text is handed to the model, so if he stops halfway it can jump ahead. Save the sentence he actually heard. **Fixed on `feat/reading-direct`.**
4. **Unnamed books can't be found.** File-name search only, in the usual folders.
5. **Questions can send up to 90,000 tokens** (`QuestionTokens`), about $0.009 each on Haiku, when 3–5 passages would do.
6. **Nothing checks for garbled text.** Old PDFs with broken fonts, like many phonology books, would be read as nonsense without warning.
7. **The system prompt works against this journey.** It discourages numbered lists and says not to offer extra help. Both need changing for this flow.
8. **Memory is a 4,000-character list of facts,** with no reading history. "What did I read yesterday" needs a real journal.

## The best way to handle a book

He reads 5–20 pages a day, so **never prepare a whole book**. Prepare what he's about to hear.

**On every book, once, in the background (free):** a library card, with title, author, contents and first 2–3 pages. Scans use Windows' built-in OCR for those pages. Scan all drives at low priority, then watch for new files. Haiku names a book only when its own text gives no title, about $0.0005 each.

**On the open book:** the current, previous and next page, extracted locally. Each page gets a local check, and goes one of three ways:

| Result | How it's detected | What it does | Cost |
| --- | --- | --- | --- |
| **Clean** | Real words in known scripts, sensible reading order | Straight to the voice | No tokens |
| **Symbols** | Valid IPA, maths or Arabic marks | Symbol reader says them by name | No tokens |
| **Broken** | Private-use characters, non-words, or the text layer disagrees with Windows OCR | One image to Claude, **only for a page he has actually reached** | ~$0.0006, once |

Your instinct was right: a picture only for pages that need it. Also: remove repeated headers and page numbers, rejoin hyphenated words, read footnotes after the paragraph, and say "there's a figure here, want it described?" instead of spending tokens on every figure.

### So a bug can't screenshot every page

This is your worry, and it needs several independent locks, because any one of them can have a bug:

1. **Only one function can send a page image,** and it needs a recorded reason.
2. **Preparing next and previous pages can never call the AI.** It's local only, by design.
3. **A repaired page is never paid for twice,** on this PC or (through the shared library) on any other.
4. **Font map.** Broken fonts repeat, so after 1–3 repaired pages the rest of the book is fixed locally.
5. **A circuit breaker.** More than 3 images in a row, or 10 for one book in a day, stops the images, falls back to local OCR, tells him, and alerts the owner.
6. **The server enforces a cap per device,** so even a broken app can't run up the bill. The relay already has device limits; add one for images.
7. **Tests that fail the build:** a clean 100-page book must make **0** image calls, and a broken one must stay within the budget.

### Checking accuracy

- After a repair, compare the result with local OCR. Real-word parts should mostly agree. If not, one retry with Sonnet. If still not, he's told the page may have mistakes.
- **Answers must quote.** The model gives its page and a short quote, and the app checks that quote really is on that page before speaking. Failed check: "I couldn't confirm that in the book."
- "That sounded wrong" marks the sentence and re-checks the page.
- Measure on the 30-page test set in PLAN §6, and report results in `docs/RESULTS.md`.

## Memory: what's stored, and who sees it

Your idea is right: an encrypted folder on his PC (PLAN §7), and the AI sees it only when needed.

- **Reading journal:** book fingerprint, date and time, from page and sentence to page and sentence. Sent to the AI: nothing. The app answers "what did I read yesterday" itself.
- **Profile:** name, languages, how he likes things. A small block (about 100 tokens, cached), so it always feels like someone who knows him.
- **Past chats:** summaries, loaded only when he asks.

That's what makes it feel like a person: "You usually read five pages. That was five. Stop here, or keep going?" Those suggestions come from the journal, on the PC, for free.

## Suggestions that would make it easier

**Do first:**
- "Where am I?", any time: book, chapter, page, percent, minutes left in the chapter.
- "What can I say?", based on what's happening right now.
- "What did you just do?" and "undo that", from an action journal.
- Repeat, slower, faster, spell that word: all local and free.

**Then:**
- Voice bookmarks and notes: "mark this", "note: ask my professor about this". "Read me my notes."
- "Read for 10 minutes" and a sleep timer.
- A daily goal, if he wants one, and a gentle offer of tomorrow's page.
- "Quiz me on what I read yesterday", for students.
- Short or detailed speech, as a setting he can change by voice.

## Scenarios that must not leave him stuck

| What goes wrong | What he should hear |
| --- | --- |
| It didn't catch him | "I didn't catch that." Never a wrong action |
| Mic muted or missing | A distinct sound: "I can't hear you. Your mic seems off." |
| No internet | "No internet. I can still read books on this PC, with the Windows voice." Reading continues |
| Internet drops mid-page | Switches voice, says so once, keeps his place |
| Number out of range | "There are only 5. Which one?" |
| Two files with the same name | Says where each is, and the difference: pages, date |
| Password-protected or DRM book | "This book is locked. Do you have the password?" Typed locally |
| Book moved or deleted | Finds it by fingerprint. If it's gone: "It's no longer on this PC." The bookmark stays |
| Poor scan | "This page is hard to read; some words may be wrong." |
| A pop-up steals focus | Reads it first, and asks. Never clicks past it |
| PC wakes from sleep | Sound, then his place, unchanged |
| Headphones unplugged during reading | Pauses and says so, so a private book doesn't play out loud |
| Someone else at the PC | Private mode by voice, and nothing personal said at start-up on a shared PC |
| The AI is slow | Sound after 2 seconds, "still working" after 5, Ctrl always stops |

## Reducing tokens, with better accuracy

Ranked by what they give. Voice is still about 95 % of the running cost (PLAN §6), so the shared library matters most for money, and these matter most for tokens and accuracy.

| # | Option | Saves | Accuracy |
| --- | --- | --- | --- |
| 1 | Reading bypasses the AI | ~$0.0036 per chunk → $0 | Much better: word for word |
| 2 | Answer from 3–5 local passages, with checked quotes | Up to ~90,000 → ~2,000 tokens a question | Better: verified |
| 3 | The app handles next, repeat, stop, numbers, "where am I" and journal questions | Most everyday turns → $0 | Better, and instant |
| 4 | Local library cards for finding books | No AI to search | Better |
| 5 | Three-way page check, font map, one repair per page | Almost all images | Better |
| 6 | Accessibility tree first, screenshot only if needed (✅ built) | ~1,400+ tokens each | Better: exact buttons |
| 7 | Drop old screen text and tool results from the history | Less on every later turn | Same |
| 8 | Keep the cached prefix stable (🟡 caching is on) | Up to 90 % of that part | Same |
| 9 | Haiku first, Sonnet only on a failed check | Avoids 20× prices | Same or better |
| 10 | Shared library of repaired pages and audio | Later readers pay ~0 | Same |

## What Clicky does, and what to take

I read the open-source Clicky (`farzaa/clicky`, MIT) and the community Windows port (`Bitshank-2338/clicky-windows`, MIT). **HeyClicky, the current product, is closed source** ("new stuff" is private, per its README), so this covers only the public code.

| They do | Compared with us | Decision |
| --- | --- | --- |
| Screenshot of every screen on every question, Sonnet by default, no prompt caching in the API code | We read the accessibility tree first and start on Haiku | **Keep ours.** Far cheaper and more exact |
| They point and explain; they don't act | We act, with a safety gate | **Keep ours** |
| Prompt written "for the ear": short sentences, spell out small numbers, avoid symbols | Ours is similar | **Take** the extra details |
| Keep only the last 10 exchanges, as text; old screenshots aren't re-sent | We should confirm the same | **Take** (option 7) |
| Pointing in a separate small call, with the screenshot matched to the screen's shape | We use element IDs first | **Take** the shape matching for our screenshot fallback |
| Connect to the server early, so the first request is faster | Not yet | **Take**: small and easy |
| Windows port: pointing tries UI Automation, then local OCR, then AI | We skip the middle | **Take** local OCR before AI |
| Windows port: next, repeat and stop handled with no AI call | Partly | **Take** (option 3) |
| Windows port: blanks out sensitive windows before any capture | We block bank pages and private mode | **Take**: one rule for every capture |
| Windows port: separate memory per app | One thread | **Consider** |
| Windows port: journal and spaced repetition, "quiz me" | No | **Take** for students, later |
| Windows port: wake word, local speech recognition | Planned | Later |

**Where we already beat them:** we act on the PC, Arabic and English mixed, full use with no screen at all, reading any book with symbols, memory across days, and the four-press payment gate.

## Suggested build order

1. **Reading fixes (small, and they fix real bugs):** reading skips the AI, bookmarks by fingerprint, save only what was heard. **Built on `feat/reading-direct`; not yet heard on a real PC.**
2. **Numbered lists and local commands.**
3. **Page check and image locks,** with the tests.
4. **Library cards,** so unnamed books can be found.
5. **Reading journal and start-up greeting.**
6. **Answers from passages, with checked quotes.**
