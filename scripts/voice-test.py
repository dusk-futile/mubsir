#!/usr/bin/env python3
"""Reads the same hard Arabic passage in several Fish voices, with and without tashkeel, so they can be compared by ear.

Asks for your Fish Audio key and sends it only to api.fish.audio. It's never saved or printed.
Writes to ~/Desktop/mubsir-voice-test. Open index.html there.
"""
import getpass
import html
import json
import pathlib
import urllib.error
import urllib.request

PRICE_PER_BYTE = 15 / 1_000_000
PLAIN = "استنتج الباحثون أن المستشرقين لم يستوعبوا خصائص الاشتقاق في اللغة العربية، فالكلمة الواحدة قد تتفرع إلى عشرات الصيغ. وكتب العالم كتبا كثيرة عن الأصوات المهموسة والمجهورة."
MARKED = "اِسْتَنْتَجَ الباحِثُونَ أَنَّ المُسْتَشْرِقِينَ لَمْ يَسْتَوْعِبُوا خَصائِصَ الاِشْتِقاقِ في اللُّغَةِ العَرَبِيَّةِ، فَالكَلِمَةُ الواحِدَةُ قَدْ تَتَفَرَّعُ إِلى عَشَراتِ الصِّيَغِ. وَكَتَبَ العالِمُ كُتُبًا كَثِيرَةً عَنِ الأَصْواتِ المَهْمُوسَةِ وَالمَجْهُورَةِ."
VOICES = [
    ("فاطمة", "0b14f34a13a94ed88fe6113193749bb0"),
    ("حنان", "05bf47f4aad948e1a7167a56e1d21c22"),
    ("أسماء", "14f1000b77d547eeb5f03b474dd29e0f"),
    ("شاب سعودي", "10c5c2a37a284a81bb0cf3c53955d795"),
    ("راوي عربي", "c3e5d81d807f4cbc9a0c2872a4dea9ea"),
    ("حسام", "183721675c2045499e8de847f4488b32"),
    ("عمر", "a381d0da904d402d82d457788d1b90fe"),
    ("آدم", "6a2c72ffa4454df8909f8887e29cf668"),
    ("مذيع أخبار", "899f9f87713b4e3cbf95ad409b6296b7"),
    ("مصري", "ccbfe8a2690e4ca99e91a059c38f30dc"),
    ("معلّق", "82273447f36e4e5cbb0de4735dd40fab"),
]


def speak(key, voice, text):
    body = json.dumps({"text": text, "format": "mp3", "latency": "normal", "reference_id": voice}).encode()
    request = urllib.request.Request(
        "https://api.fish.audio/v1/tts",
        body,
        {"Authorization": "Bearer " + key, "Content-Type": "application/json", "model": "s2.1-pro"},
    )
    with urllib.request.urlopen(request, timeout=90) as response:
        return response.read()


def main():
    bytes_each = len(PLAIN.encode()) + len(MARKED.encode())
    cost = len(VOICES) * bytes_each * PRICE_PER_BYTE
    print(f"{len(VOICES)} voices, about ${cost:.2f} from your Fish credit.")
    key = getpass.getpass("Fish Audio key (hidden): ").strip()
    out = pathlib.Path.home() / "Desktop" / "mubsir-voice-test"
    out.mkdir(exist_ok=True)
    rows = []
    for number, (name, voice) in enumerate(VOICES, 1):
        cells = []
        for label, text in (("plain", PLAIN), ("tashkeel", MARKED)):
            file = f"{number}-{label}.mp3"
            try:
                (out / file).write_bytes(speak(key, voice, text))
            except (urllib.error.URLError, TimeoutError) as error:
                print(f"{name} {label} failed: {getattr(error, 'code', 'network')}")
                if getattr(error, "code", 0) in (401, 402):
                    return
                cells.append("<td>failed</td>")
                continue
            print(f"{name} {label} done")
            cells.append(f'<td><audio controls preload="none" src="{file}"></audio></td>')
        rows.append(f"<tr><th>{number}. {html.escape(name)}</th>{''.join(cells)}</tr>")
    (out / "index.html").write_text(
        '<!doctype html><meta charset="utf-8"><title>Mubsir voice test</title>'
        f'<p dir="rtl">{html.escape(PLAIN)}</p><table><tr><th>Voice</th><th>Plain</th><th>Tashkeel</th></tr>{"".join(rows)}</table>',
        encoding="utf-8",
    )
    print("Open", out / "index.html")


if __name__ == "__main__":
    main()
