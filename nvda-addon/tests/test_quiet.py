"""Tests for quiet.QuietFlag."""

import json
import os

import pytest

import quiet
from quiet import QuietFlag


class Clocks(object):
	"""A monotonic clock in seconds and a wall clock in Unix ms that tests move by hand."""

	def __init__(self, wall_ms=1_800_000_000_000.0):
		self.mono = 100.0
		self.wall_ms = wall_ms

	def monotonic(self):
		return self.mono

	def now_ms(self):
		return self.wall_ms

	def advance(self, seconds):
		self.mono += seconds
		self.wall_ms += seconds * 1000.0


@pytest.fixture
def clocks():
	return Clocks()


@pytest.fixture
def errors():
	return []


def make_flag(path, clocks, errors):
	return QuietFlag(str(path), monotonic=clocks.monotonic, now_ms=clocks.now_ms, on_error=errors.append)


def write_flag(path, content, modified_ms):
	path.write_bytes(content if isinstance(content, bytes) else content.encode("utf-8"))
	os.utime(str(path), (modified_ms / 1000.0, modified_ms / 1000.0))


def test_missing_file_is_not_quiet_and_not_an_error(tmp_path, clocks, errors):
	flag = make_flag(tmp_path / "nvda-quiet.json", clocks, errors)
	assert flag.is_quiet() is False
	assert errors == []


def test_missing_folder_is_not_quiet(tmp_path, clocks, errors):
	flag = make_flag(tmp_path / "Basira" / "nvda-quiet.json", clocks, errors)
	assert flag.is_quiet() is False
	assert errors == []


def test_quiet_until_the_given_time(tmp_path, clocks, errors):
	path = tmp_path / "nvda-quiet.json"
	write_flag(path, json.dumps({"until": clocks.wall_ms + 2000}), clocks.wall_ms)
	flag = make_flag(path, clocks, errors)
	assert flag.is_quiet() is True
	clocks.advance(1.9)
	assert flag.is_quiet() is True
	clocks.advance(0.2)
	assert flag.is_quiet() is False
	assert errors == []


def test_past_time_is_not_quiet(tmp_path, clocks, errors):
	path = tmp_path / "nvda-quiet.json"
	write_flag(path, json.dumps({"until": clocks.wall_ms - 1}), clocks.wall_ms)
	assert make_flag(path, clocks, errors).is_quiet() is False


def test_integer_and_float_until_both_work(tmp_path, clocks, errors):
	path = tmp_path / "nvda-quiet.json"
	write_flag(path, '{"until": %d}' % int(clocks.wall_ms + 5000), clocks.wall_ms)
	assert make_flag(path, clocks, errors).is_quiet() is True
	write_flag(path, '{"until": %.3f}' % (clocks.wall_ms + 5000.5), clocks.wall_ms)
	assert make_flag(path, clocks, errors).is_quiet() is True


def test_file_is_read_at_most_every_poll_interval(tmp_path, clocks, errors, monkeypatch):
	path = tmp_path / "nvda-quiet.json"
	reads = []
	real_read = QuietFlag._read

	def counting_read(self):
		reads.append(1)
		return real_read(self)

	monkeypatch.setattr(QuietFlag, "_read", counting_read)
	flag = make_flag(path, clocks, errors)
	assert flag.is_quiet() is False
	write_flag(path, json.dumps({"until": clocks.wall_ms + 10000}), clocks.wall_ms)
	clocks.mono += 0.1
	assert flag.is_quiet() is False  # The cached "not quiet" is still used.
	clocks.mono += 0.1
	assert flag.is_quiet() is False
	assert len(reads) == 1
	clocks.mono += quiet.POLL_SECONDS
	assert flag.is_quiet() is True
	assert len(reads) == 2


def test_cached_until_still_expires_between_reads(tmp_path, clocks, errors):
	path = tmp_path / "nvda-quiet.json"
	write_flag(path, json.dumps({"until": clocks.wall_ms + 100}), clocks.wall_ms)
	flag = make_flag(path, clocks, errors)
	assert flag.is_quiet() is True
	clocks.wall_ms += 150  # Wall time passes without a new read.
	assert flag.is_quiet() is False


def test_quiet_window_is_capped_after_the_last_write(tmp_path, clocks, errors):
	path = tmp_path / "nvda-quiet.json"
	written = clocks.wall_ms
	write_flag(path, json.dumps({"until": written + 3_600_000}), written)
	flag = make_flag(path, clocks, errors)
	assert flag.is_quiet() is True
	clocks.advance(quiet.MAX_QUIET_MS / 1000.0 - 1)
	assert flag.is_quiet() is True
	clocks.advance(2)
	assert flag.is_quiet() is False


def test_stale_file_from_long_ago_is_ignored(tmp_path, clocks, errors):
	path = tmp_path / "nvda-quiet.json"
	write_flag(path, json.dumps({"until": clocks.wall_ms + 10_000}), clocks.wall_ms - 3_600_000)
	assert make_flag(path, clocks, errors).is_quiet() is False


def test_utf8_bom_is_accepted(tmp_path, clocks, errors):
	path = tmp_path / "nvda-quiet.json"
	write_flag(path, b"\xef\xbb\xbf" + json.dumps({"until": clocks.wall_ms + 1000}).encode("utf-8"), clocks.wall_ms)
	assert make_flag(path, clocks, errors).is_quiet() is True
	assert errors == []


@pytest.mark.parametrize(
	"content",
	[
		"",
		"{",
		'{"until": ',
		"[]",
		"null",
		"42",
		'{"until": "soon"}',
		'{"until": true}',
		'{"until": null}',
		'{"untill": 99999999999999}',
		'{"until": NaN}',
		b"\xff\xfe{}",
	],
)
def test_bad_content_is_not_quiet(tmp_path, clocks, errors, content):
	path = tmp_path / "nvda-quiet.json"
	write_flag(path, content, clocks.wall_ms)
	assert make_flag(path, clocks, errors).is_quiet() is False


def test_bad_content_is_reported_once(tmp_path, clocks, errors):
	path = tmp_path / "nvda-quiet.json"
	write_flag(path, "{oops", clocks.wall_ms)
	flag = make_flag(path, clocks, errors)
	for _ in range(5):
		assert flag.is_quiet() is False
		clocks.advance(1)
	assert len(errors) == 1
	assert "nvda-quiet.json" in errors[0]


def test_oversized_file_is_ignored(tmp_path, clocks, errors):
	path = tmp_path / "nvda-quiet.json"
	padding = " " * (quiet.MAX_FILE_BYTES + 10)
	write_flag(path, '{"until": %d}%s' % (int(clocks.wall_ms + 5000), padding), clocks.wall_ms)
	assert make_flag(path, clocks, errors).is_quiet() is False
	assert len(errors) == 1


def test_unreadable_path_is_reported_not_raised(tmp_path, clocks, errors):
	path = tmp_path / "nvda-quiet.json"
	path.mkdir()
	assert make_flag(path, clocks, errors).is_quiet() is False
	assert len(errors) == 1


def test_recovers_when_the_file_becomes_valid(tmp_path, clocks, errors):
	path = tmp_path / "nvda-quiet.json"
	write_flag(path, "{", clocks.wall_ms)
	flag = make_flag(path, clocks, errors)
	assert flag.is_quiet() is False
	write_flag(path, json.dumps({"until": clocks.wall_ms + 5000}), clocks.wall_ms)
	clocks.advance(quiet.POLL_SECONDS)
	assert flag.is_quiet() is True


def test_broken_clock_or_error_callback_never_raises(tmp_path):
	def broken_clock():
		raise RuntimeError("clock")

	def broken_callback(message):
		raise RuntimeError("callback")

	flag = QuietFlag(str(tmp_path / "x.json"), monotonic=broken_clock, on_error=broken_callback)
	assert flag.is_quiet() is False


def test_default_clocks_work(tmp_path):
	path = tmp_path / "nvda-quiet.json"
	import time

	path.write_text(json.dumps({"until": time.time() * 1000 + 30_000}))
	assert QuietFlag(str(path)).is_quiet() is True
