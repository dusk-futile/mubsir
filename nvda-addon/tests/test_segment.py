# -*- coding: utf-8 -*-
"""Tests for segment.split_runs. The cases mirror tests/Basira.Core.Tests/Text/ScriptSegmenterTests.cs;
where C# marks a spelled acronym run ("en*"), the Python runs simply say "en" and merge with
neighbouring English runs."""

import random

import pytest

import segment
from segment import AR, EN


def describe(runs):
	return "|".join("%s:%s" % (lang, text) for text, lang in runs)


def assert_lossless(text, runs):
	assert "".join(t for t, _ in runs) == text
	assert all(t != "" for t, _ in runs)
	assert all(lang in (AR, EN) for _, lang in runs)
	for i in range(1, len(runs)):
		assert runs[i][1] != runs[i - 1][1], "runs %d and %d should have been merged: %s" % (i - 1, i, describe(runs))


def seg(text, default=AR):
	runs = segment.split_runs(text, default)
	assert_lossless(text, runs)
	return describe(runs)


# Mixed sentences from the plan and from the C# tests.


def test_arabic_sentence_with_acronym_product_and_number():
	assert seg("افتح ملف PDF على Windows 10 من فضلك") == "ar:افتح ملف |en:PDF|ar: على |en:Windows |ar:10 من فضلك"


@pytest.mark.parametrize("default", [AR, EN])
def test_english_sentence_with_arabic_name(default):
	assert seg("Meeting at 5 مع أحمد", default) == "en:Meeting at 5 |ar:مع أحمد"


def test_returns_list_of_tuples():
	assert segment.split_runs("Meeting at 5 مع أحمد", "ar") == [("Meeting at 5 ", "en"), ("مع أحمد", "ar")]


# Empty, blank and single-language text.


@pytest.mark.parametrize("default", [AR, EN])
def test_empty_text_has_no_runs(default):
	assert segment.split_runs("", default) == []


@pytest.mark.parametrize(
	"text, default, expected",
	[
		("   ", AR, "ar:   "),
		(" \t\n ", EN, "en: \t\n "),
	],
)
def test_whitespace_only_is_one_run_in_the_default_language(text, default, expected):
	assert seg(text, default) == expected


@pytest.mark.parametrize(
	"text, default, expected",
	[
		("مرحبا بك في بصيرة", EN, "ar:مرحبا بك في بصيرة"),
		("Hello and welcome", AR, "en:Hello and welcome"),
		("café naïve résumé", AR, "en:café naïve résumé"),
		("بِسْمِ اللَّهِ الرَّحْمَنِ الرَّحِيمِ", EN, "ar:بِسْمِ اللَّهِ الرَّحْمَنِ الرَّحِيمِ"),
	],
)
def test_single_language_text_is_one_run(text, default, expected):
	assert seg(text, default) == expected


# Digits.


@pytest.mark.parametrize(
	"text, expected",
	[
		("السعر 25 دولار", "ar:السعر 25 دولار"),
		("السعر ٢٥ دولار", "ar:السعر ٢٥ دولار"),
		("The price is 25 dollars", "en:The price is 25 dollars"),
		("The price is ٢٥ dollars", "en:The price is ٢٥ dollars"),
		("The price is ۲۵ dollars", "en:The price is ۲۵ dollars"),
	],
)
def test_digits_follow_the_dominant_language(text, expected):
	assert seg(text) == expected


@pytest.mark.parametrize(
	"text, default, expected",
	[
		("123 456", AR, "ar:123 456"),
		("123 456", EN, "en:123 456"),
		("٣٫١٤ + 2 = ?", EN, "en:٣٫١٤ + 2 = ?"),
		("!!! ... ???", AR, "ar:!!! ... ???"),
		("😀 👍🏽", EN, "en:😀 👍🏽"),
	],
)
def test_text_without_letters_uses_the_default_language(text, default, expected):
	assert seg(text, default) == expected


def test_letter_count_ties_go_to_the_default_language():
	# Two Latin letters and two Arabic letters: the digit follows the default language.
	assert seg("ok 5 لا", AR) == "en:ok |ar:5 لا"
	assert seg("ok 5 لا", EN) == "en:ok 5 |ar:لا"


def test_digits_follow_each_sentences_own_dominant_language():
	assert seg("Call me at 5. سأتصل بك الساعة 6.") == "en:Call me at 5. |ar:سأتصل بك الساعة 6."


def test_sentences_split_at_line_breaks():
	assert seg("Room 12\nالغرفة 13") == "en:Room 12\n|ar:الغرفة 13"


def test_number_with_separators_stays_in_one_run():
	assert seg("السعر 1,250.75 جنيه") == "ar:السعر 1,250.75 جنيه"
	assert seg("Call 010-555-1234 now") == "en:Call 010-555-1234 now"


def test_number_glued_to_a_word_follows_that_word():
	assert seg("وصل Win10 و 2026م") == "ar:وصل |en:Win10 |ar:و 2026م"


# Punctuation and spaces.


@pytest.mark.parametrize(
	"text, expected",
	[
		("مرحبا, hello", "ar:مرحبا, |en:hello"),
		("hello, مرحبا", "en:hello, |ar:مرحبا"),
		("مرحبا! hello", "ar:مرحبا! |en:hello"),
		("كتب (Hello) ثم ذهب", "ar:كتب (|en:Hello) |ar:ثم ذهب"),
	],
)
def test_punctuation_between_languages_joins_the_preceding_run(text, expected):
	assert seg(text) == expected


@pytest.mark.parametrize(
	"text, expected",
	[
		("مرحبا - أهلا، كيف الحال؟", "ar:مرحبا - أهلا، كيف الحال؟"),
		("one, two; three!", "en:one, two; three!"),
	],
)
def test_punctuation_between_the_same_language_stays_in_the_run(text, expected):
	assert seg(text) == expected


@pytest.mark.parametrize(
	"text, expected",
	[
		("  (hello)", "en:  (hello)"),
		("« مرحبا »", "ar:« مرحبا »"),
	],
)
def test_leading_and_trailing_neutrals_join_the_only_run(text, expected):
	assert seg(text) == expected


# Acronyms and other Latin words.


@pytest.mark.parametrize(
	"text, expected",
	[
		("شغل USB الآن", "ar:شغل |en:USB|ar: الآن"),
		("منظمة WHO للصحة", "ar:منظمة |en:WHO|ar: للصحة"),
		("وصلت USB3 اليوم", "ar:وصلت |en:USB3 |ar:اليوم"),
	],
)
def test_short_capital_acronyms_in_arabic_hand_the_next_space_to_arabic(text, expected):
	assert seg(text) == expected


def test_acronyms_are_judged_against_the_rest_of_the_sentence():
	assert seg("USB و PDF") == "en:USB|ar: و |en:PDF"
	assert seg("شغل USB", EN) == "ar:شغل |en:USB"


@pytest.mark.parametrize(
	"text, expected",
	[
		("Open the PDF file", "en:Open the PDF file"),
		("I like USB و", "en:I like USB |ar:و"),
	],
)
def test_acronyms_in_english_are_ordinary_words(text, expected):
	assert seg(text) == expected


@pytest.mark.parametrize(
	"text, expected",
	[
		("افتح iPhone الآن", "ar:افتح |en:iPhone |ar:الآن"),
		("حرف A فقط", "ar:حرف |en:A |ar:فقط"),
		("اكتب NETFLIX الآن", "ar:اكتب |en:NETFLIX |ar:الآن"),
	],
)
def test_other_latin_words_join_the_following_space(text, expected):
	assert seg(text) == expected


# URLs and e-mail addresses.


def test_url_inside_arabic_stays_whole_in_one_english_run():
	url = "https://site.com/v2/item?id=42"
	text = "من فضلك افتح الرابط التالي في المتصفح الآن %s وشكرا جزيلا" % url
	runs = segment.split_runs(text, AR)
	assert_lossless(text, runs)
	assert len(runs) == 3
	assert any(lang == EN and t.strip() == url for t, lang in runs)


@pytest.mark.parametrize(
	"text, token",
	[
		("زر الموقع https://example.com/page?id=3 الآن", "https://example.com/page?id=3"),
		("افتح www.google.com من فضلك", "www.google.com"),
		("افتح WWW.Google.com من فضلك", "WWW.Google.com"),
		("راسلني على ahmed.ali99@example.com لو سمحت", "ahmed.ali99@example.com"),
		("افتح https://WHO.int/USB الآن", "https://WHO.int/USB"),
		("الرابط (https://a.example/x). شكرا", "https://a.example/x"),
	],
)
def test_urls_and_emails_are_not_split(text, token):
	runs = segment.split_runs(text, AR)
	assert_lossless(text, runs)
	containing = [(t, lang) for t, lang in runs if token in t]
	assert len(containing) == 1
	assert containing[0][1] == EN


@pytest.mark.parametrize(
	"text, expected",
	[
		# Protected addresses do not count, so the five Arabic letters win.
		("abcdef@x.io مرحبا", AR),
		("ftp://example.org مرحبا", AR),
		("(www.example.org) مرحبا", AR),
		# These only look like addresses: their letters count and English wins.
		("abcdef@x مرحبا", EN),
		("@example.com مرحبا", EN),
		("abc@@example.com مرحبا", EN),
		("abcdef@example. مرحبا", EN),
		("ftp:/example.org مرحبا", EN),
		# A token with Arabic letters is never an address: 13 Latin letters against 10 Arabic.
		("موقعي://www.example.org مرحبا", EN),
	],
)
def test_only_real_urls_and_emails_are_left_out_of_the_letter_count(text, expected):
	assert segment.dominant_language(text, AR) == expected
	assert_lossless(text, segment.split_runs(text, AR))


def test_url_letters_do_not_decide_the_language_of_numbers():
	assert (
		seg("السعر 5 في https://shop.example.com/products/item ")
		== "ar:السعر 5 في |en:https://shop.example.com/products/item "
	)


def test_tashkeel_stays_with_its_arabic_letters():
	assert seg("بِسْمِ اللَّهِ PDF") == "ar:بِسْمِ اللَّهِ |en:PDF"


# Invariants.


@pytest.mark.parametrize(
	"text",
	[
		"مرحبا 😂 hello 👍🏽",
		"a\r\nب\r\nc",
		"سعر الـ iPhone هو $999.99 (تقريبا)!",
		"‏مرحبا‎ hello‏",
		"é́ بّ x",
		"١٢٣ abc ۴۵۶ أبج 789",
		"ﻣﺮﺣﺒﺎ ﻻ hello",
		"𝐀𝐁𝐂 عربي 🇪🇬 text",
		"tab\there\tوهنا",
		"﻿BOM ثم نص",
		"x",
		"ب",
		"5",
		"م5",
		"5م",
		"a5",
	],
)
@pytest.mark.parametrize("default", [AR, EN])
def test_never_loses_or_reorders_characters(text, default):
	assert_lossless(text, segment.split_runs(text, default))


def test_long_mixed_paragraph_is_lossless():
	text = (
		'قال أحمد: "I\'ll send the PDF at 5pm". ثم أرسل الملف عبر WhatsApp إلى 3 أشخاص، وكتب: Thanks! 🙏\n'
		"Visit https://basira.app/help?lang=ar للمساعدة. السعر 9.99$ فقط، أي 50% خصم."
	)
	assert_lossless(text, segment.split_runs(text, AR))
	assert_lossless(text, segment.split_runs(text, EN))


def test_random_text_is_always_lossless_and_merged():
	alphabet = "abcXYZ PDF ابتثجح ًٌٍَ 0123٠١٢۴ .,!?؟،()-:/@\n\t😀𝐀é́"
	rng = random.Random(20261010)
	for _ in range(2000):
		text = "".join(rng.choice(alphabet) for _ in range(rng.randint(0, 40)))
		for default in (AR, EN):
			assert_lossless(text, segment.split_runs(text, default))


def test_characters_outside_the_bmp_are_neutral_like_in_dotnet():
	# Mathematical bold letters are letters in Python but surrogate pairs in .NET; both treat them as neutral.
	assert seg("𝐀𝐁𝐂 عربي") == "ar:𝐀𝐁𝐂 عربي"


def test_rejects_unknown_default_language():
	with pytest.raises(ValueError):
		segment.split_runs("hello", "fr")
	with pytest.raises(ValueError):
		segment.dominant_language("hello", "Arabic")


# Dominant language and script detection.


@pytest.mark.parametrize(
	"text, default, expected",
	[
		("مرحبا بالعالم hello", EN, AR),
		("hello world مرحبا", AR, EN),
		("ab جد", EN, EN),
		("ab جد", AR, AR),
		("12345", AR, AR),
		("راسلني على ahmed@example.com", EN, AR),
		("", EN, EN),
	],
)
def test_dominant_language_counts_letters(text, default, expected):
	assert segment.dominant_language(text, default) == expected


@pytest.mark.parametrize(
	"text, expected",
	[
		("hello مرحبا", True),
		("Windows ١٠", False),
		("مرحبا ١٠", False),
		("hello", False),
		("مرحبا، كيف الحال؟", False),
		("", False),
		("café ب", True),
		("صفحة 3 من 10", False),
		("، ؟ hello", False),
	],
)
def test_has_both_scripts(text, expected):
	assert segment.has_both_scripts(text) is expected


@pytest.mark.parametrize("char, expected", [("؀", True), ("ۿ", True), ("ݐ", True), ("ࢠ", True), ("ﭐ", True), ("﻿", True), ("a", False), ("א", False), ("܀", False)])
def test_is_arabic_script_ranges(char, expected):
	assert segment.is_arabic_script(char) is expected
