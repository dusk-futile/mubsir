

import os

import globalPluginHandler
from logHandler import log

from . import segment
from .quiet import QuietFlag

try:
	import speech
	from speech import extensions as speechExtensions
	from speech.commands import LangChangeCommand
except ImportError:
	speech = speechExtensions = LangChangeCommand = None
	log.error("mubsirBridge: NVDA speech API not found; the add-on stays inactive", exc_info=True)

NVDA_LANGUAGES = {segment.AR: "ar", segment.EN: "en"}

def _quietFilePath():
	localAppData = os.environ.get("LOCALAPPDATA")
	return os.path.join(localAppData, "Mubsir", "nvda-quiet.json") if localAppData else None

def _isLangChange(item):
	return isinstance(item, LangChangeCommand)

def _makeLangChange(lang):
	return LangChangeCommand(NVDA_LANGUAGES[lang])

class GlobalPlugin(globalPluginHandler.GlobalPlugin):
	def __init__(self, *args, **kwargs):
		super(GlobalPlugin, self).__init__(*args, **kwargs)
		self._muteDepth = 0
		path = _quietFilePath()
		self._quiet = QuietFlag(path, on_error=self._logQuietProblem) if path else None
		self._speechFilter = None
		speechFilter = getattr(speechExtensions, "filter_speechSequence", None)
		if speechFilter is None or LangChangeCommand is None:
			log.warning("mubsirBridge: speech.extensions.filter_speechSequence is missing; the add-on is inactive")
			return
		try:
			speechFilter.register(self._filterSpeech)
			self._speechFilter = speechFilter
		except Exception:
			log.error("mubsirBridge: could not register the speech filter", exc_info=True)

	def terminate(self):
		try:
			if self._speechFilter is not None:
				self._speechFilter.unregister(self._filterSpeech)
		except Exception:
			log.error("mubsirBridge: could not unregister the speech filter", exc_info=True)
		self._speechFilter = None
		self._quiet = None
		super(GlobalPlugin, self).terminate()

	def _filterSpeech(self, speechSequence):
		try:
			if self._muteDepth > 0:
				return []
			return segment.with_language_runs(
				speechSequence,
				self._defaultLanguage(),
				_makeLangChange,
				_isLangChange,
			)
		except Exception:
			log.error("mubsirBridge: speech filter failed; speech passed through unchanged", exc_info=True)
			return speechSequence

	@staticmethod
	def _defaultLanguage():
		try:
			current = speech.getCurrentLanguage() or ""
		except Exception:
			current = ""
		return segment.AR if current.lower().startswith("ar") else segment.EN

	def _isQuiet(self):
		quiet = self._quiet
		return quiet is not None and self._speechFilter is not None and quiet.is_quiet()

	def _runEventChain(self, eventName, nextHandler):
		try:
			mute = self._isQuiet()
		except Exception:
			log.error("mubsirBridge: quiet check failed", exc_info=True)
			mute = False
		if mute:
			self._muteDepth += 1
		try:
			nextHandler()
		except Exception:
			log.error("mubsirBridge: a later %s handler failed" % eventName, exc_info=True)
		finally:
			if mute:
				self._muteDepth -= 1

	def event_gainFocus(self, obj, nextHandler):
		self._runEventChain("gainFocus", nextHandler)

	def event_focusEntered(self, obj, nextHandler):
		self._runEventChain("focusEntered", nextHandler)

	def event_foreground(self, obj, nextHandler):
		self._runEventChain("foreground", nextHandler)

	@staticmethod
	def _logQuietProblem(message):
		log.debugWarning("mubsirBridge: %s" % message)
