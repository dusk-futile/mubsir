"""Bake the Hudhud cutout rig into index.html.

Reads assets/hudhud-rig/rig.json and the part PNGs next to it, builds the layered SVG (every part in its
own group, so the page can rotate it around its joint) and replaces what sits between the
`<!-- hudhud:start -->` and `<!-- hudhud:end -->` markers in index.html. The PNGs are embedded as data
URIs, so the page stays a single file that can be published as is.

Usage: python3 prototypes/hudhud-companion/build.py
"""

from __future__ import annotations

import base64
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
RIG = HERE / "assets" / "hudhud-rig"
PAGE = HERE / "index.html"
START, END = "<!-- hudhud:start -->", "<!-- hudhud:end -->"


def uri(name: str) -> str:
    return "data:image/png;base64," + base64.b64encode((RIG / f"{name}.png").read_bytes()).decode()


def size(name: str, export_scale: float) -> tuple[float, float]:
    data = (RIG / f"{name}.png").read_bytes()
    w = int.from_bytes(data[16:20], "big")
    h = int.from_bytes(data[20:24], "big")
    return w / export_scale, h / export_scale


def image(name: str, pivot: list[float], sx: float, sy: float, k: float, attrs: str = "") -> str:
    w, h = size(name, k)
    return (
        f'<image href="{uri(name)}" x="{-pivot[0] * sx:.1f}" y="{-pivot[1] * sy:.1f}" '
        f'width="{w * sx:.1f}" height="{h * sy:.1f}" preserveAspectRatio="none"{attrs}/>'
    )


def part(p: dict, k: float) -> str:
    sx = p.get("sx", p.get("s", 1.0))
    sy = p.get("sy", p.get("s", 1.0))
    x, y = p["at"]
    rot = p.get("rot", 0)
    return (
        f'<g transform="translate({x} {y})"><g id="{p["id"]}" data-rest="{rot}" transform="rotate({rot})">'
        f'{image(p["img"], p["pivot"], sx, sy, k)}</g></g>'
    )


def feather(f: dict, k: float, c: float = 0.3) -> str:
    """One crest feather. The page moves it between its folded and raised pose (c = 0..1) every frame;
    the transform written here is the resting pose, so the bird looks right before the script runs."""
    (ox, oy), (fx, fy) = f["at"], f["at_closed"]
    x, y = fx + (ox - fx) * c, fy + (oy - fy) * c
    ang = f["closed"] + (f["open"] - f["closed"]) * c
    ln = f["len_closed"] + (1 - f["len_closed"]) * c
    return (
        f'<g id="{f["id"]}" data-open="{f["open"]}" data-closed="{f["closed"]}" data-at="{ox} {oy}" '
        f'data-at-closed="{fx} {fy}" data-len-closed="{f["len_closed"]}" '
        f'transform="translate({x:.1f} {y:.1f}) rotate({ang:.1f}) scale(1 {ln:.3f})">'
        f'{image(f["img"], f["pivot"], f["sx"], f["sy"], k)}</g>'
    )


def build() -> str:
    rig = json.loads((RIG / "rig.json").read_text())
    k = rig["export_scale"]
    vx, vy, vw, vh = rig["viewBox"]
    mirror_x = 2 * vx + vw  # face left on screen: x -> mirror_x - x
    head = rig["head"]
    nx, ny = head["neck"]
    cx, cy = head["crest_pivot"]
    fx = head["face_center_x"]

    crest = "".join(feather(f, k) for f in head["crest_back_to_front"])
    faces = (
        f'<g transform="translate({nx} {ny})">'
        + image(head["open"]["img"], head["open"]["pivot"], 1, 1, k, ' id="hFaceOpen"')
        + image(head["closed"]["img"], head["closed"]["pivot"], 1, 1, k, ' id="hFaceClosed" opacity="0"')
        + "</g>"
    )
    head_svg = (
        f'<g transform="translate({nx} {ny})"><g id="hHead"><g transform="translate({-nx} {-ny})">'
        f'<g id="hFlip" data-cx="{fx}">'
        f'<g transform="translate({cx} {cy})"><g id="hCrest"><g transform="translate({-cx} {-cy})">{crest}</g></g></g>'
        f"{faces}</g></g></g></g>"
    )
    body = "".join(part(p, k) for p in rig["back_to_front"])
    return (
        f'<svg id="hudsvg" viewBox="{vx} {vy} {vw} {vh}" aria-hidden="true" focusable="false">'
        f'<g transform="translate({mirror_x} 0) scale(-1 1)">'
        f'<g id="hBody" data-feet="{rig["feet"][0]} {rig["feet"][1]}">{body}{head_svg}<g id="hDust"></g></g>'
        f"</g>"
        f'<g id="hZzz" font-family="Readex Pro, system-ui, sans-serif" font-weight="600" fill="#FFF4DE"></g>'
        f"</svg>"
    )


def main() -> None:
    page = PAGE.read_text(encoding="utf-8")
    a, b = page.index(START) + len(START), page.index(END)
    PAGE.write_text(page[:a] + build() + page[b:], encoding="utf-8")
    print(f"index.html: Hudhud rig baked in ({len(page) // 1024} KB before)")


if __name__ == "__main__":
    main()
