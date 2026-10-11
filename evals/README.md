# Evals

The eval suite checks Basira end to end: a spoken or typed request, the screen it starts from, and what Basira did. It is re-run after every model, prompt or router change, and before each milestone. Each run is scored for **success**, **latency** (key release or Enter to the first audio) and **cost**.

## Files

| Path | In git | What it is |
|---|---|---|
| `tasks.schema.json` | yes | JSON Schema for a task list |
| `tasks.sample.json` | yes | 27 synthetic tasks covering M1 to M4 (reading, agent, messaging, documents, safety) in several dialects |
| `score.py` | yes | Scorer, Python 3.9+ standard library only |
| `tests/` | yes | Tests for the scorer (`python3 -m pytest evals/tests`) |
| `private/` | **never** | Real recordings, real transcripts, his own task list and run results |

`evals/private/` is listed in `.gitignore`, and CI (`scripts/check-no-audio.sh`) fails if any audio file or anything under `evals/private/` is ever committed.

## A task

```json
{
  "id": "google-search-egyptian",
  "utterance_ar": "افتح جوجل وابحث عن مواعيد صلاة الجمعة",
  "utterance_en": "Open Google and search for Friday prayer times",
  "setup": { "description": "Desktop with Chrome installed, no browser window open." },
  "expected": {
    "tools": [{ "name": "browse_search", "args": { "query": "صلاة الجمعة" } }],
    "requires_confirmation": false
  },
  "tags": ["m3", "agent", "dialect:egyptian", "input:voice"]
}
```

`expected` holds exactly one of:

- `local_command`: the router handles it without the AI (`Basira.Core.Router.LocalCommand` names, e.g. `ScrollDown`, `OpenApp`).
- `tools`: agent tool calls that must appear in order (`match`: `subsequence` by default, or `exact`). Tool names are `Basira.Core.Brain.ToolNames`.
- `answer_contains`: strings the spoken answer must contain.

and optionally the safety checks `forbidden_tools`, `requires_confirmation` and `screen_content_to_cloud` (false for bank pages, password fields and private mode).

String comparisons ignore case, Arabic diacritics and the usual letter variants (أ/إ/آ/ا, ة/ه, ى/ي), and an expected argument matches when it appears inside the observed value.

## Running a suite

1. Start the dev build on the Windows test PC (`scripts/dev/run.sh`).
2. For each task, create its `setup`, then play the recording (voice tasks) or send the typed request through the debug pipe (`scripts/dev/send.sh`).
3. Basira's dev build writes one line per run to a results file (JSON Lines):

```json
{"task_id": "google-search-egyptian", "latency_ms": 2140, "cost_usd": 0.00041,
 "observed": {"tools": [{"name": "browse_search", "args": {"query": "مواعيد صلاة الجمعة"}}], "confirmation_asked": false}}
```

   A line may carry `"success": true|false` instead of `observed` (a manual verdict), or `"error"` (counted as a failure).

4. Score it:

```bash
python3 evals/score.py evals/private/results-2026-10-20.jsonl --tasks evals/private/tasks.json --by-tag
python3 evals/score.py results.jsonl --tasks evals/tasks.sample.json --min-success 0.9   # exit 1 below 90%
python3 evals/score.py --check-tasks evals/private/tasks.json                           # validate a task list
```

The output shows the success rate, p50/p90 latency, total cost and cost per run, each failure with its reason, and (with `--by-tag`) the success rate per milestone, area and dialect. `--json` prints the same summary as JSON.

Milestone gates from the plan: M1 needs 90% or more of his commands understood and voice latency p50 at most 3 s, p90 at most 5 s; typed questions p50 at most 2.5 s.

## Recordings and consent

- Recordings of a real person are made only with that person's **spoken consent**, recorded at the start of the first session, and only for testing Basira.
- They stay on the family's own machines in `evals/private/`. They are never committed, never uploaded to a shared drive, and never used to train a model.
- They are sent to a speech-to-text provider only during an eval run, the same way his normal use of Basira sends them.
- He can ask for any recording to be deleted at any time. All recordings are deleted after v1.0.
- Transcripts and task lists made from his recordings are private too: they reveal his habits and contacts. Synthetic tasks such as `tasks.sample.json` may be committed; real ones may not.
- For charities (M6), each person gives their own consent, and no dialect is claimed as supported before it has been tested on recordings of speakers of that dialect.
