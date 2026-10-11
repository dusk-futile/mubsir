# -*- coding: utf-8 -*-
"""Tests for the global plugin, with small fakes standing in for NVDA's modules."""

import importlib
import json
import sys
import time
import types

import pytest

NVDA_MODULES = ("globalPluginHandler", "logHandler", "speech", "speech.extensions", "speech.commands")


class FakeLog(object):
	def __init__(self):
		self.records = []

	def _record(self, level, msg, *args, **kwargs):
		self.records.append((level, msg % args if args else msg))

	def error(self, msg, *args, **kwargs):
		self._record("error", msg, *args)

	def warning(self, msg, *args, **kwargs):
		self._record("warning", msg, *args)

	def debugWarning(self, msg, *args, **kwargs):
		self._record("debugWarning", msg, *args)

	def levels(self):
		return [level for level, _ in self.records]


class FakeFilter(object):
	"""Like extensionPoints.Filter: handlers run in order, and a failing handler is skipped."""

	def __init__(self):
		self.handlers = []

	def register(self, handler):
		self.handlers.append(handler)

	def unregister(self, handler):
		self.handlers = [h for h in self.handlers if h != handler]

	def apply(self, value):
		for handler in self.handlers:
			try:
				value = handler(value)
			except Exception:
				pass
		return value


class LangChangeCommand(object):
	def __init__(self, lang):
		self.lang = lang

	def __eq__(self, other):
		return isinstance(other, LangChangeCommand) and other.lang == self.lang

	def __repr__(self):
		return "LangChangeCommand(%r)" % self.lang


class CallbackCommand(object):
	pass


class BaseGlobalPlugin(object):
	terminated = False

	def terminate(self):
		self.terminated = True


class Nvda(object):
	"""Installs fake NVDA modules and imports the add-on against them."""

	def __init__(self, monkeypatch, with_filter=True, language="ar_SA"):
		self.log = FakeLog()
		self.filter = FakeFilter()
		self.language = language

		gph = types.ModuleType("globalPluginHandler")
		gph.GlobalPlugin = BaseGlobalPlugin
		log_handler = types.ModuleType("logHandler")
		log_handler.log = self.log
		speech = types.ModuleType("speech")
		speech.__path__ = []
		speech.getCurrentLanguage = self._current_language
		extensions = types.ModuleType("speech.extensions")
		if with_filter:
			extensions.filter_speechSequence = self.filter
		commands = types.ModuleType("speech.commands")
		commands.LangChangeCommand = LangChangeCommand
		speech.extensions = extensions
		speech.commands = commands
		for name, module in zip(NVDA_MODULES, (gph, log_handler, speech, extensions, commands)):
			monkeypatch.setitem(sys.modules, name, module)
		for name in [n for n in sys.modules if n == "basiraBridge" or n.startswith("basiraBridge.")]:
			monkeypatch.delitem(sys.modules, name)
		self.addon = importlib.import_module("basiraBridge")

	def _current_language(self):
		if isinstance(self.language, Exception):
			raise self.language
		return self.language

	def speak(self, sequence):
		return self.filter.apply(sequence)


@pytest.fixture
def local_app_data(tmp_path, monkeypatch):
	monkeypatch.setenv("LOCALAPPDATA", str(tmp_path))
	return tmp_path


@pytest.fixture
def nvda(monkeypatch, local_app_data):
	return Nvda(monkeypatch)


def set_quiet(local_app_data, seconds):
	folder = local_app_data / "Basira"
	folder.mkdir(exist_ok=True)
	(folder / "nvda-quiet.json").write_text(json.dumps({"until": time.time() * 1000 + seconds * 1000}))


# Language runs.


def test_registers_the_filter_and_unregisters_on_terminate(nvda):
	plugin = nvda.addon.GlobalPlugin()
	assert len(nvda.filter.handlers) == 1
	plugin.terminate()
	assert nvda.filter.handlers == []
	assert plugin.terminated


def test_inserts_language_changes_around_runs(nvda):
	nvda.addon.GlobalPlugin()
	result = nvda.speak(["افتح ملف PDF على Windows 10 من فضلك"])
	assert result == [
		LangChangeCommand("ar"), "افتح ملف ",
		LangChangeCommand("en"), "PDF",
		LangChangeCommand("ar"), " على ",
		LangChangeCommand("en"), "Windows ",
		LangChangeCommand("ar"), "10 من فضلك",
	]


def test_english_sentence_with_arabic_name(nvda):
	nvda.addon.GlobalPlugin()
	assert nvda.speak(["Meeting at 5 مع أحمد"]) == [
		LangChangeCommand("en"), "Meeting at 5 ",
		LangChangeCommand("ar"), "مع أحمد",
	]


def test_single_language_sequences_pass_through_untouched(nvda):
	nvda.addon.GlobalPlugin()
	for sequence in (["Hello world"], ["مرحبا بك"], ["صفحة ٣ من ١٠"], [], [CallbackCommand()]):
		assert nvda.speak(sequence) is sequence


def test_sequences_with_language_changes_pass_through_untouched(nvda):
	nvda.addon.GlobalPlugin()
	sequence = [LangChangeCommand("ar"), "Windows مرحبا"]
	assert nvda.speak(sequence) is sequence


def test_each_text_item_gets_its_own_runs_and_commands_keep_their_place(nvda):
	nvda.addon.GlobalPlugin()
	callback = CallbackCommand()
	result = nvda.speak(["زر", callback, "OK"])
	assert result == [LangChangeCommand("ar"), "زر", callback, LangChangeCommand("en"), "OK"]


def test_letterless_items_follow_the_dominant_language_of_the_sequence(nvda):
	nvda.addon.GlobalPlugin()
	result = nvda.speak(["Volume", "50%", "مرحبا"])
	assert result == [
		LangChangeCommand("en"), "Volume",
		LangChangeCommand("en"), "50%",
		LangChangeCommand("ar"), "مرحبا",
	]


@pytest.mark.parametrize("language, expected", [("ar_SA", "ar"), ("ar", "ar"), ("en_US", "en"), ("", "en"), (None, "en")])
def test_ties_go_to_nvdas_current_language(monkeypatch, local_app_data, language, expected):
	nvda = Nvda(monkeypatch, language=language)
	nvda.addon.GlobalPlugin()
	result = nvda.speak(["زر", "OK", "5"])
	assert result[-2:] == [LangChangeCommand(expected), "5"]


def test_language_lookup_failure_falls_back_to_english(monkeypatch, local_app_data):
	nvda = Nvda(monkeypatch, language=RuntimeError("no synth"))
	nvda.addon.GlobalPlugin()
	assert nvda.speak(["زر", "OK", "5"])[-2:] == [LangChangeCommand("en"), "5"]


def test_filter_errors_are_logged_and_speech_passes_through(nvda, monkeypatch):
	nvda.addon.GlobalPlugin()

	def broken(*args, **kwargs):
		raise RuntimeError("boom")

	monkeypatch.setattr(nvda.addon.segment, "with_language_runs", broken)
	sequence = ["hello مرحبا"]
	assert nvda.speak(sequence) is sequence
	assert "error" in nvda.log.levels()


def test_tuple_sequences_are_accepted(nvda):
	nvda.addon.GlobalPlugin()
	assert nvda.speak(("hi", "يا")) == [LangChangeCommand("en"), "hi", LangChangeCommand("ar"), "يا"]


def test_missing_extension_point_leaves_the_addon_inactive(monkeypatch, local_app_data):
	nvda = Nvda(monkeypatch, with_filter=False)
	plugin = nvda.addon.GlobalPlugin()
	assert "warning" in nvda.log.levels()
	calls = []
	plugin.event_gainFocus(object(), lambda: calls.append(1))
	assert calls == [1]
	plugin.terminate()


# Quiet mode.


def test_focus_speech_is_dropped_while_quiet(nvda, local_app_data):
	plugin = nvda.addon.GlobalPlugin()
	set_quiet(local_app_data, 30)
	spoken = []
	plugin.event_gainFocus(object(), lambda: spoken.append(nvda.speak(["Search, edit"])))
	assert spoken == [[]]
	# Speech outside the event chain (Basira's own narration) is not affected.
	assert nvda.speak(["Opening Google"]) == ["Opening Google"]


@pytest.mark.parametrize("event", ["event_gainFocus", "event_focusEntered", "event_foreground"])
def test_all_focus_events_are_quieted(nvda, local_app_data, event):
	plugin = nvda.addon.GlobalPlugin()
	set_quiet(local_app_data, 30)
	spoken = []
	getattr(plugin, event)(object(), lambda: spoken.append(nvda.speak(["dialog"])))
	assert spoken == [[]]


def test_focus_speech_is_kept_when_not_quiet(nvda, local_app_data):
	plugin = nvda.addon.GlobalPlugin()
	spoken = []
	plugin.event_gainFocus(object(), lambda: spoken.append(nvda.speak(["Search, edit"])))
	assert spoken == [["Search, edit"]]
	set_quiet(local_app_data, -1)
	plugin._quiet._next_read_at = None
	plugin.event_gainFocus(object(), lambda: spoken.append(nvda.speak(["OK, button"])))
	assert spoken[-1] == ["OK, button"]


def test_next_handler_always_runs_and_its_errors_do_not_escape(nvda, local_app_data):
	plugin = nvda.addon.GlobalPlugin()
	set_quiet(local_app_data, 30)

	def failing():
		raise RuntimeError("later handler")

	plugin.event_gainFocus(object(), failing)
	assert "error" in nvda.log.levels()
	assert plugin._muteDepth == 0
	assert nvda.speak(["after"]) == ["after"]


def test_nested_chains_restore_the_mute_depth(nvda, local_app_data):
	plugin = nvda.addon.GlobalPlugin()
	set_quiet(local_app_data, 30)
	depths = []

	def inner():
		depths.append(plugin._muteDepth)

	plugin.event_gainFocus(object(), lambda: plugin.event_focusEntered(object(), inner))
	assert depths == [2]
	assert plugin._muteDepth == 0


def test_no_local_app_data_means_no_quiet_mode(monkeypatch, tmp_path):
	monkeypatch.delenv("LOCALAPPDATA", raising=False)
	nvda = Nvda(monkeypatch)
	plugin = nvda.addon.GlobalPlugin()
	spoken = []
	plugin.event_gainFocus(object(), lambda: spoken.append(nvda.speak(["OK"])))
	assert spoken == [["OK"]]


def test_bad_quiet_file_is_logged_as_a_debug_warning(nvda, local_app_data):
	plugin = nvda.addon.GlobalPlugin()
	folder = local_app_data / "Basira"
	folder.mkdir()
	(folder / "nvda-quiet.json").write_text("{not json")
	spoken = []
	plugin.event_gainFocus(object(), lambda: spoken.append(nvda.speak(["OK"])))
	assert spoken == [["OK"]]
	assert "debugWarning" in nvda.log.levels()


def test_quiet_mode_after_terminate_does_nothing(nvda, local_app_data):
	plugin = nvda.addon.GlobalPlugin()
	set_quiet(local_app_data, 30)
	plugin.terminate()
	calls = []
	plugin.event_gainFocus(object(), lambda: calls.append(1))
	assert calls == [1]
	assert plugin._muteDepth == 0
