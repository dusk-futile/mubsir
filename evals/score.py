#!/usr/bin/env python3
"""Scores a Basira eval run: success rate, latency (p50/p90) and cost.

Usage:
  python3 evals/score.py RESULTS.jsonl [--tasks TASKS.json] [--by-tag] [--min-success 0.9] [--json]
  python3 evals/score.py --check-tasks TASKS.json

RESULTS.jsonl has one JSON object per run of a task:
  {"task_id": "scroll-down-egyptian",
   "success": true,                      optional; judged from "observed" and --tasks when missing
   "latency_ms": 1840,                   key release (or Enter) to the first audio
   "cost_usd": 0.00031,
   "observed": {"local_command": {"name": "ScrollDown", "args": {}},
                "tools": [{"name": "browse_search", "args": {"query": "..."}}],
                "answer": "spoken answer text",
                "confirmation_asked": false,
                "screen_content_sent": false},
   "error": "timeout"}                   optional; a run with an error counts as a failure

Exit codes: 0 scored, 1 success rate below --min-success, 2 bad input.
Standard library only, so it runs anywhere Python 3.9+ runs.
"""

import argparse
import json
import math
import re
import sys
import unicodedata

KINDS = ("local_command", "tools", "answer_contains")
TASK_KEYS = {"id", "utterance_ar", "utterance_en", "setup", "expected", "tags", "notes"}
SETUP_KEYS = {"description", "app", "url", "fixture", "focus"}
EXPECTED_KEYS = set(KINDS) | {"match", "forbidden_tools", "requires_confirmation", "screen_content_to_cloud"}
ID_RE = re.compile(r"^[a-z0-9][a-z0-9-]{1,63}$")
TAG_RE = re.compile(r"^[a-z0-9][a-z0-9:_-]*$")

_FOLD = str.maketrans({
	"أ": "ا", "إ": "ا", "آ": "ا", "ٱ": "ا",
	"ة": "ه", "ى": "ي", "ؤ": "و", "ئ": "ي",
	"ـ": None,  # tatweel
})


class InputError(Exception):
	pass


def fold(text):
	"""Lower-cases and folds Arabic letter variants and diacritics, so spelling differences do not fail a match."""
	text = unicodedata.normalize("NFKC", str(text)).casefold()
	text = "".join(c for c in text if not ("\u064b" <= c <= "\u065f" or c == "\u0670"))
	text = text.translate(_FOLD)
	return " ".join(text.split())


def percentile(values, p):
	"""Nearest-rank percentile (p in 0..100) of a non-empty list; None for an empty one."""
	if not values:
		return None
	ordered = sorted(values)
	rank = max(1, int(math.ceil(p / 100.0 * len(ordered))))
	return ordered[rank - 1]


# --- Task file validation ----------------------------------------------------------

def validate_task(task):
	"""Returns a list of problems with one task (empty when it is valid). Mirrors tasks.schema.json."""
	problems = []
	if not isinstance(task, dict):
		return ["task is not an object"]
	tid = task.get("id")
	label = tid if isinstance(tid, str) else "?"
	for key in ("id", "utterance_ar", "utterance_en", "setup", "expected", "tags"):
		if key not in task:
			problems.append("%s: missing %s" % (label, key))
	for key in set(task) - TASK_KEYS:
		problems.append("%s: unknown field %s" % (label, key))
	if "id" in task and not (isinstance(tid, str) and ID_RE.match(tid)):
		problems.append("%s: id must be lower-case letters, digits and dashes" % label)
	if not isinstance(task.get("utterance_ar", ""), str):
		problems.append("%s: utterance_ar must be a string" % label)
	if "utterance_en" in task and not (isinstance(task["utterance_en"], str) and task["utterance_en"].strip()):
		problems.append("%s: utterance_en must be a non-empty string" % label)

	setup = task.get("setup")
	if "setup" in task:
		if not isinstance(setup, dict) or not isinstance(setup.get("description"), str) or not setup["description"].strip():
			problems.append("%s: setup needs a description" % label)
		elif set(setup) - SETUP_KEYS:
			problems.append("%s: unknown setup fields %s" % (label, ", ".join(sorted(set(setup) - SETUP_KEYS))))

	tags = task.get("tags")
	if "tags" in task:
		if not isinstance(tags, list) or not all(isinstance(t, str) and TAG_RE.match(t) for t in tags):
			problems.append("%s: tags must be lower-case strings" % label)
		elif len(set(tags)) != len(tags):
			problems.append("%s: duplicate tags" % label)

	expected = task.get("expected")
	if "expected" in task:
		if not isinstance(expected, dict):
			problems.append("%s: expected must be an object" % label)
		else:
			problems.extend("%s: %s" % (label, p) for p in _validate_expected(expected))
	return problems


def _validate_expected(expected):
	problems = []
	kinds = [k for k in KINDS if k in expected]
	if len(kinds) != 1:
		problems.append("expected needs exactly one of local_command, tools, answer_contains")
	for key in set(expected) - EXPECTED_KEYS:
		problems.append("unknown expected field %s" % key)
	command = expected.get("local_command")
	if "local_command" in expected and not (
		isinstance(command, dict) and isinstance(command.get("name"), str) and isinstance(command.get("args", {}), dict)
	):
		problems.append("local_command needs a name (and args as an object)")
	tools = expected.get("tools")
	if "tools" in expected and not (
		isinstance(tools, list) and tools and all(_is_tool(t) for t in tools)
	):
		problems.append("tools must be a non-empty list of {name, args}")
	answer = expected.get("answer_contains")
	if "answer_contains" in expected and not (
		isinstance(answer, list) and answer and all(isinstance(a, str) and a for a in answer)
	):
		problems.append("answer_contains must be a non-empty list of strings")
	if expected.get("match", "subsequence") not in ("subsequence", "exact"):
		problems.append("match must be subsequence or exact")
	forbidden = expected.get("forbidden_tools", [])
	if not (isinstance(forbidden, list) and all(isinstance(t, str) for t in forbidden)):
		problems.append("forbidden_tools must be a list of tool names")
	for key in ("requires_confirmation", "screen_content_to_cloud"):
		if key in expected and not isinstance(expected[key], bool):
			problems.append("%s must be true or false" % key)
	return problems


def _is_tool(tool):
	return (
		isinstance(tool, dict)
		and isinstance(tool.get("name"), str)
		and isinstance(tool.get("args", {}), dict)
		and not set(tool) - {"name", "args"}
	)


def load_tasks(path):
	try:
		with open(path, encoding="utf-8") as f:
			tasks = json.load(f)
	except (OSError, ValueError) as e:
		raise InputError("cannot read tasks %s: %s" % (path, e))
	if not isinstance(tasks, list):
		raise InputError("%s must hold a JSON array of tasks" % path)
	problems = []
	seen = set()
	for task in tasks:
		problems.extend(validate_task(task))
		tid = task.get("id") if isinstance(task, dict) else None
		if isinstance(tid, str):
			if tid in seen:
				problems.append("%s: duplicate id" % tid)
			seen.add(tid)
	if problems:
		raise InputError("invalid tasks in %s:\n  %s" % (path, "\n  ".join(problems)))
	return {task["id"]: task for task in tasks}


# --- Judging -----------------------------------------------------------------------

def args_match(expected, observed):
	"""True when every expected argument is present: strings by folded containment, others by equality."""
	observed = observed if isinstance(observed, dict) else {}
	for key, want in (expected or {}).items():
		if key not in observed:
			return False
		got = observed[key]
		if isinstance(want, str):
			if not isinstance(got, str) or fold(want) not in fold(got):
				return False
		elif isinstance(want, bool) or isinstance(got, bool):
			if want is not got:
				return False
		elif want != got:
			return False
	return True


def _tool_matches(want, got):
	return isinstance(got, dict) and got.get("name") == want["name"] and args_match(want.get("args"), got.get("args"))


def tools_match(expected_tools, observed_tools, mode="subsequence"):
	observed_tools = observed_tools if isinstance(observed_tools, list) else []
	if mode == "exact":
		return len(expected_tools) == len(observed_tools) and all(
			_tool_matches(w, g) for w, g in zip(expected_tools, observed_tools)
		)
	position = 0
	for got in observed_tools:
		if position < len(expected_tools) and _tool_matches(expected_tools[position], got):
			position += 1
	return position == len(expected_tools)


def judge(task, observed):
	"""Returns (success, reason) for one run of a task against what Basira did."""
	if not isinstance(observed, dict):
		return False, "no observed outcome"
	expected = task["expected"]
	tools = observed.get("tools") if isinstance(observed.get("tools"), list) else []
	called = {t.get("name") for t in tools if isinstance(t, dict)}

	forbidden = called.intersection(expected.get("forbidden_tools", []))
	if forbidden:
		return False, "called forbidden tool(s): %s" % ", ".join(sorted(forbidden))
	if "requires_confirmation" in expected and bool(observed.get("confirmation_asked")) != expected["requires_confirmation"]:
		return False, "confirmation was %sasked" % ("not " if expected["requires_confirmation"] else "")
	if expected.get("screen_content_to_cloud") is False and observed.get("screen_content_sent"):
		return False, "screen content was sent to the cloud"

	if "local_command" in expected:
		want = expected["local_command"]
		got = observed.get("local_command")
		if not isinstance(got, dict) or str(got.get("name", "")).casefold() != want["name"].casefold():
			return False, "expected local command %s" % want["name"]
		if not args_match(want.get("args"), got.get("args")):
			return False, "local command arguments differ"
		return True, "ok"
	if "tools" in expected:
		mode = expected.get("match", "subsequence")
		if not tools_match(expected["tools"], tools, mode):
			names = " > ".join(t["name"] for t in expected["tools"])
			return False, "expected tools (%s): %s" % (mode, names)
		return True, "ok"
	answer = fold(observed.get("answer") or "")
	missing = [a for a in expected["answer_contains"] if fold(a) not in answer]
	if missing:
		return False, "answer is missing: %s" % ", ".join(missing)
	return True, "ok"


# --- Scoring -----------------------------------------------------------------------

def read_results(path):
	"""Returns (runs, problems). Malformed lines are skipped and reported."""
	runs = []
	problems = []
	try:
		f = sys.stdin if path == "-" else open(path, encoding="utf-8")
	except OSError as e:
		raise InputError("cannot read results %s: %s" % (path, e))
	with f:
		for number, line in enumerate(f, 1):
			line = line.strip()
			if not line or line.startswith("//"):
				continue
			try:
				run = json.loads(line)
			except ValueError as e:
				problems.append("line %d: not JSON (%s)" % (number, e))
				continue
			if not isinstance(run, dict) or not isinstance(run.get("task_id"), str):
				problems.append("line %d: needs a task_id" % number)
				continue
			runs.append(run)
	return runs, problems


def _number(value):
	if isinstance(value, bool) or not isinstance(value, (int, float)) or value != value or value < 0:
		return None
	return float(value)


def score(runs, tasks=None):
	"""Builds the summary dict for a list of runs."""
	judged = []
	unjudged = []
	unknown_tasks = set()
	for run in runs:
		task = tasks.get(run["task_id"]) if tasks else None
		if tasks is not None and task is None:
			unknown_tasks.add(run["task_id"])
		if run.get("error"):
			success, reason = False, "error: %s" % run["error"]
		elif isinstance(run.get("success"), bool):
			success, reason = run["success"], "reported"
		elif task is not None and "observed" in run:
			success, reason = judge(task, run["observed"])
		else:
			unjudged.append(run["task_id"])
			continue
		judged.append((run, success, reason))

	latencies = [v for v in (_number(r.get("latency_ms")) for r in runs) if v is not None]
	costs = [v for v in (_number(r.get("cost_usd")) for r in runs) if v is not None]
	passed = sum(1 for _, ok, _ in judged if ok)
	summary = {
		"runs": len(runs),
		"judged": len(judged),
		"passed": passed,
		"success_rate": (passed / float(len(judged))) if judged else None,
		"latency_ms": {"p50": percentile(latencies, 50), "p90": percentile(latencies, 90), "count": len(latencies)},
		"cost_usd": {
			"total": round(sum(costs), 6),
			"per_run": round(sum(costs) / len(costs), 6) if costs else None,
		},
		"unjudged": sorted(set(unjudged)),
		"unknown_tasks": sorted(unknown_tasks),
		"failures": [
			{"task_id": run["task_id"], "reason": reason} for run, ok, reason in judged if not ok
		],
	}
	if tasks:
		by_tag = {}
		for run, ok, _ in judged:
			for tag in tasks.get(run["task_id"], {}).get("tags", []):
				entry = by_tag.setdefault(tag, [0, 0])
				entry[0] += 1 if ok else 0
				entry[1] += 1
		summary["by_tag"] = {tag: {"passed": p, "judged": n} for tag, (p, n) in sorted(by_tag.items())}
	return summary


def format_summary(summary, by_tag=False):
	def ms(value):
		return "-" if value is None else "%d ms" % round(value)

	rate = summary["success_rate"]
	lines = [
		"Runs:          %d (%d judged)" % (summary["runs"], summary["judged"]),
		"Success rate:  %s" % ("-" if rate is None else "%.1f%% (%d/%d)" % (rate * 100, summary["passed"], summary["judged"])),
		"Latency:       p50 %s, p90 %s (%d runs)" % (
			ms(summary["latency_ms"]["p50"]), ms(summary["latency_ms"]["p90"]), summary["latency_ms"]["count"]),
		"Cost:          $%.4f total, %s per run" % (
			summary["cost_usd"]["total"],
			"-" if summary["cost_usd"]["per_run"] is None else "$%.5f" % summary["cost_usd"]["per_run"]),
	]
	if summary["failures"]:
		lines.append("Failures:")
		lines.extend("  %s: %s" % (f["task_id"], f["reason"]) for f in summary["failures"])
	if summary["unjudged"]:
		lines.append("Not judged (no success field and no matching task): %s" % ", ".join(summary["unjudged"]))
	if summary["unknown_tasks"]:
		lines.append("Unknown task ids: %s" % ", ".join(summary["unknown_tasks"]))
	if by_tag and summary.get("by_tag"):
		lines.append("By tag:")
		lines.extend(
			"  %-24s %d/%d" % (tag, entry["passed"], entry["judged"]) for tag, entry in summary["by_tag"].items()
		)
	return "\n".join(lines)


def main(argv=None):
	parser = argparse.ArgumentParser(description="Score a Basira eval run.")
	parser.add_argument("results", nargs="?", help="results JSONL file, or - for stdin")
	parser.add_argument("--tasks", help="task list (JSON) used to judge runs without a success field")
	parser.add_argument("--check-tasks", metavar="TASKS", help="only validate a task list")
	parser.add_argument("--by-tag", action="store_true", help="also print the success rate per tag")
	parser.add_argument("--min-success", type=float, help="exit 1 when the success rate is below this (0..1)")
	parser.add_argument("--json", action="store_true", help="print the summary as JSON")
	args = parser.parse_args(argv)

	try:
		if args.check_tasks:
			tasks = load_tasks(args.check_tasks)
			print("OK: %d tasks in %s" % (len(tasks), args.check_tasks))
			return 0
		if not args.results:
			parser.print_usage(sys.stderr)
			return 2
		tasks = load_tasks(args.tasks) if args.tasks else None
		runs, problems = read_results(args.results)
	except InputError as e:
		print("ERROR: %s" % e, file=sys.stderr)
		return 2

	for problem in problems:
		print("warning: %s" % problem, file=sys.stderr)
	summary = score(runs, tasks)
	if args.json:
		print(json.dumps(summary, ensure_ascii=False, indent=2))
	else:
		print(format_summary(summary, by_tag=args.by_tag))
	if args.min_success is not None and (summary["success_rate"] or 0.0) < args.min_success:
		return 1
	return 0


if __name__ == "__main__":
	sys.exit(main())
