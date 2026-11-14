

import json
import os
import time

POLL_SECONDS = 0.25
"""The file is read at most this often; calls in between reuse the last value."""

MAX_QUIET_MS = 60000
"""A quiet window never lasts longer than this after the file was last written,
so a stale or wrong file cannot keep NVDA quiet."""

MAX_FILE_BYTES = 4096

class QuietFlag(object):

	def __init__(self, path, monotonic=time.monotonic, now_ms=None, poll_seconds=POLL_SECONDS, on_error=None):
		self.path = path
		self._monotonic = monotonic
		self._now_ms = now_ms or (lambda: time.time() * 1000.0)
		self._poll_seconds = poll_seconds
		self._on_error = on_error
		self._next_read_at = None
		self._until_ms = 0.0
		self._last_error = None

	def is_quiet(self):
		try:
			now = self._monotonic()
			if self._next_read_at is None or now >= self._next_read_at:
				self._next_read_at = now + self._poll_seconds
				self._until_ms = self._read()
			return self._now_ms() < self._until_ms
		except Exception as e:
			self._report("quiet flag check failed: %r" % (e,))
			return False

	def _read(self):
		try:
			with open(self.path, "rb") as f:
				raw = f.read(MAX_FILE_BYTES + 1)
				modified_ms = os.fstat(f.fileno()).st_mtime * 1000.0
		except (FileNotFoundError, NotADirectoryError):
			return 0.0
		except OSError as e:
			self._report("cannot read %s: %s" % (self.path, e))
			return 0.0
		if len(raw) > MAX_FILE_BYTES:
			self._report("%s is larger than %d bytes" % (self.path, MAX_FILE_BYTES))
			return 0.0
		try:
			until = json.loads(raw.decode("utf-8-sig"))["until"]
			if isinstance(until, bool) or not isinstance(until, (int, float)):
				raise ValueError("'until' is not a number")
		except (ValueError, KeyError, TypeError, IndexError) as e:
			self._report("ignoring %s: %s" % (self.path, e))
			return 0.0
		until = float(until)
		if until != until:
			return 0.0
		return min(until, modified_ms + MAX_QUIET_MS)

	def _report(self, message):
		if self._on_error is None or message == self._last_error:
			return
		self._last_error = message
		try:
			self._on_error(message)
		except Exception:
			pass
