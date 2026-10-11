# Basira Bridge: language runs for mixed Arabic/English text.
# Copyright (C) 2026 dusk-futile
# This file is covered by the GNU General Public License, version 2.
# See the file COPYING for details.

"""Splits mixed Arabic/English text into language runs.

Pure Python with no NVDA imports, so it can be tested anywhere. It mirrors
Basira.Core.Text.ScriptSegmenter (C#) rule for rule:

- Arabic-script letters and marks are "ar"; every other letter is "en".
- Neutral characters (spaces, punctuation, symbols) between two runs of the same
  language stay in it; between different languages they join the preceding run,
  or the following one when the preceding run is a short acronym inside Arabic.
- Numbers follow the dominant language of their sentence by letter count (ties go
  to the default language; letters of URLs and e-mail addresses do not count).
  A number glued to a word (Win10, 2026م) follows that word.
- URLs and e-mail addresses stay whole in one "en" run.
- Text without letters is one run in the default language.
- Neighbouring runs of the same language are merged, and the run texts always
  concatenate back to the input.

The C# version reads UTF-16 code units, so characters outside the Basic
Multilingual Plane (emoji, mathematical letters) are neutral here too.
"""

import re
import unicodedata

AR = "ar"
EN = "en"
LANGUAGES = (AR, EN)

_NEUTRAL, _ARABIC, _LATIN, _DIGIT = 0, 1, 2, 3

_ARABIC_SCRIPT_RE = re.compile("[؀-ۿݐ-ݿࢠ-ࣿﭐ-﷿ﹰ-﻿]")
_LETTERS = frozenset(("Lu", "Ll", "Lt", "Lm", "Lo"))
_MARKS = frozenset(("Mn", "Mc", "Me"))
_ARABIC_STRONG = frozenset(("Lo", "Lm", "Mn", "Mc", "Me"))
_SPACE_CATEGORIES = frozenset(("Zs", "Zl", "Zp"))
_SPACE_CONTROLS = frozenset("\t\n\v\f\r\x85\xa0")
_LINE_BREAKS = frozenset("\n\r  \v\f\x85")
_SENTENCE_ENDS = frozenset(".!?؟…۔！？")
_NUMBER_SEPARATORS = frozenset(".,:/-٫٬")
_TOKEN_OPENERS = frozenset("([{<\"'«“")
_TOKEN_CLOSERS = frozenset(".,;:!?؟،؛)]}>\"'»”…")


def _category(c):
	# .NET sees a code point outside the BMP as two surrogates, which are neither letters nor digits.
	return "Cs" if ord(c) > 0xFFFF else unicodedata.category(c)


def is_arabic_script(c):
	"""True for characters in the Arabic, Arabic Supplement, Extended-A and Presentation Forms blocks."""
	o = ord(c)
	return (
		0x0600 <= o <= 0x06FF
		or 0x0750 <= o <= 0x077F
		or 0x08A0 <= o <= 0x08FF
		or 0xFB50 <= o <= 0xFDFF
		or 0xFE70 <= o <= 0xFEFF
	)


def _is_letter(c):
	return _category(c) in _LETTERS


def _is_letter_or_digit(c):
	cat = _category(c)
	return cat in _LETTERS or cat == "Nd"


def _is_space(c):
	return c in _SPACE_CONTROLS or _category(c) in _SPACE_CATEGORIES


def _dominant(arabic, latin, default_lang):
	if arabic > latin:
		return AR
	if latin > arabic:
		return EN
	return default_lang


def _check_lang(default_lang):
	if default_lang not in LANGUAGES:
		raise ValueError("default_lang must be 'ar' or 'en', not %r" % (default_lang,))


def _classify(text):
	cls = []
	prev = _NEUTRAL
	for c in text:
		cat = _category(c)
		if cat == "Nd":
			kind = _DIGIT
		elif is_arabic_script(c):
			kind = _ARABIC if cat in _ARABIC_STRONG else _NEUTRAL
		elif cat in _LETTERS:
			kind = _LATIN
		elif cls and cat in _MARKS and prev in (_ARABIC, _LATIN):
			kind = prev
		else:
			kind = _NEUTRAL
		cls.append(kind)
		prev = kind
	return cls


def _count_letters(text, cls, start, end, exclude=None):
	arabic = latin = 0
	for i in range(start, end):
		if (exclude is not None and exclude[i]) or not _is_letter(text[i]):
			continue
		if cls[i] == _ARABIC:
			arabic += 1
		elif cls[i] == _LATIN:
			latin += 1
	return arabic, latin


def _is_url_or_email(token):
	if any(is_arabic_script(c) for c in token):
		return False
	if "://" in token or token[:4].lower() == "www.":
		return True
	at = token.find("@")
	if at <= 0 or at != token.rfind("@") or not _is_letter_or_digit(token[at - 1]):
		return False
	domain = token[at + 1:]
	dot = domain.find(".")
	return 0 < dot < len(domain) - 1


def _mark_urls_and_emails(text):
	"""Marks URLs (scheme or www.) and e-mail addresses, minus surrounding brackets and trailing punctuation."""
	n = len(text)
	marks = [False] * n
	i = 0
	while i < n:
		if _is_space(text[i]):
			i += 1
			continue
		start = i
		while i < n and not _is_space(text[i]):
			i += 1
		s, e = start, i
		while s < e and text[s] in _TOKEN_OPENERS:
			s += 1
		while e > s and text[e - 1] in _TOKEN_CLOSERS:
			e -= 1
		if e - s >= 4 and _is_url_or_email(text[s:e]):
			for k in range(s, e):
				marks[k] = True
	return marks


def _find_acronyms(text, cls, protected):
	"""Marks all-caps ASCII words of 2 to 5 letters (PDF, USB) outside URLs and e-mail addresses."""
	n = len(text)
	acronym = [False] * n
	i = 0
	while i < n:
		if cls[i] != _LATIN:
			i += 1
			continue
		start = i
		while i < n and cls[i] == _LATIN:
			i += 1
		if not 2 <= i - start <= 5 or protected[start]:
			continue
		if all("A" <= text[k] <= "Z" for k in range(start, i)):
			for k in range(start, i):
				acronym[k] = True
	return acronym


def _sentence_dominance(text, cls, exclude, default_lang):
	"""Dominant language per character, sentence by sentence; letterless sentences take the whole text's."""
	n = len(text)
	text_dominant = _dominant(*_count_letters(text, cls, 0, n, exclude), default_lang=default_lang)
	result = [default_lang] * n
	start = 0
	for i in range(n):
		end = (
			i == n - 1
			or text[i] in _LINE_BREAKS
			or (text[i] in _SENTENCE_ENDS and _is_space(text[i + 1]))
		)
		if not end:
			continue
		arabic, latin = _count_letters(text, cls, start, i + 1, exclude)
		lang = text_dominant if arabic + latin == 0 else _dominant(arabic, latin, default_lang)
		for k in range(start, i + 1):
			result[k] = lang
		start = i + 1
	return result


def _assign_numbers(text, cls, sentence_lang, lang, assigned):
	n = len(text)
	i = 0
	while i < n:
		if cls[i] != _DIGIT or assigned[i]:
			i += 1
			continue
		start = i
		i += 1
		while i < n:
			if cls[i] == _DIGIT and not assigned[i]:
				i += 1
			elif text[i] in _NUMBER_SEPARATORS and i + 1 < n and cls[i + 1] == _DIGIT and not assigned[i + 1]:
				i += 2
			else:
				break
		end = i
		if start > 0 and cls[start - 1] in (_ARABIC, _LATIN):
			value = lang[start - 1]
		elif end < n and cls[end] in (_ARABIC, _LATIN):
			value = lang[end]
		else:
			value = sentence_lang[start]
		for k in range(start, end):
			lang[k] = value
			assigned[k] = True


def _assign_neutrals(lang, assigned, spell):
	n = len(lang)
	i = 0
	while i < n:
		if assigned[i]:
			i += 1
			continue
		start = i
		while i < n and not assigned[i]:
			i += 1
		end = i
		if start == 0:
			value = lang[end]
		elif end == n:
			value = lang[start - 1]
		elif lang[start - 1] == lang[end] or spell[start - 1]:
			value = lang[end]
		else:
			value = lang[start - 1]
		for k in range(start, end):
			lang[k] = value


def split_runs(text, default_lang=AR):
	"""Splits text into a list of (run_text, "ar" or "en") whose texts concatenate back to text."""
	_check_lang(default_lang)
	if not text:
		return []
	n = len(text)
	cls = _classify(text)
	arabic, latin = _count_letters(text, cls, 0, n)
	if arabic + latin == 0:
		return [(text, default_lang)]

	protected = _mark_urls_and_emails(text)
	acronym = _find_acronyms(text, cls, protected)
	protected_or_acronym = [p or a for p, a in zip(protected, acronym)]
	sentence_lang = _sentence_dominance(text, cls, protected, default_lang)
	acronym_context = _sentence_dominance(text, cls, protected_or_acronym, default_lang)

	lang = [default_lang] * n
	assigned = [False] * n
	for i in range(n):
		if protected[i] or cls[i] == _LATIN:
			lang[i] = EN
			assigned[i] = True
		elif cls[i] == _ARABIC:
			lang[i] = AR
			assigned[i] = True
	spell = [acronym[i] and acronym_context[i] == AR for i in range(n)]

	_assign_numbers(text, cls, sentence_lang, lang, assigned)
	_assign_neutrals(lang, assigned, spell)

	runs = []
	start = 0
	for i in range(1, n + 1):
		if i == n or lang[i] != lang[start]:
			runs.append((text[start:i], lang[start]))
			start = i
	return runs


def dominant_language(text, default_lang=AR):
	"""The language with more letters in text, not counting URLs and e-mail addresses; ties give default_lang."""
	_check_lang(default_lang)
	cls = _classify(text)
	arabic, latin = _count_letters(text, cls, 0, len(text), _mark_urls_and_emails(text))
	return _dominant(arabic, latin, default_lang)


def has_both_scripts(text):
	"""True when text has an Arabic letter or mark and also a letter of another script."""
	if not _ARABIC_SCRIPT_RE.search(text):
		return False
	cls = _classify(text)
	return _ARABIC in cls and _LATIN in cls


def with_language_runs(sequence, default_lang, make_command, is_language_command):
	"""Returns a speech sequence with a language command before every run of its text items.

	The sequence comes back unchanged (same object) when it already holds a language command
	or when its text does not mix Arabic with another script. Non-text items keep their place.
	Text items without letters follow the dominant language of the whole sequence.
	"""
	items = sequence if isinstance(sequence, list) else list(sequence)
	texts = [item for item in items if isinstance(item, str)]
	if not texts or any(is_language_command(item) for item in items):
		return items
	joined = " ".join(texts)
	if not has_both_scripts(joined):
		return items
	sequence_lang = dominant_language(joined, default_lang)
	result = []
	for item in items:
		if not isinstance(item, str):
			result.append(item)
			continue
		for run_text, run_lang in split_runs(item, sequence_lang):
			result.append(make_command(run_lang))
			result.append(run_text)
	return result
