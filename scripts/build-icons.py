"""Build Mubsir's app icon files from the SVG masters in assets/brand/.

Writes assets/brand/mubsir.ico (16 to 256 px) and assets/brand/png/mubsir-512.png, which the Store
package is cut from. Small sizes use simpler drawings so the icon stays readable in the tray:
  16, 20 px      -> mubsir-icon-small.svg  (solid pupil, heavier lines)
  24 to 48 px    -> mubsir-icon-medium.svg (one sound ring)
  64 px and up   -> mubsir-icon.svg        (full detail)

Needs Pillow and Chrome or Edge (used headless to draw the SVGs).
Usage: python scripts/build-icons.py
"""

from __future__ import annotations

import io
import os
import shutil
import struct
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
BRAND = ROOT / "assets" / "brand"
RENDER = 1024

VARIANTS = {
    "small": BRAND / "mubsir-icon-small.svg",
    "medium": BRAND / "mubsir-icon-medium.svg",
    "full": BRAND / "mubsir-icon.svg",
}
ICO_SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]
PNG_SIZE = 512


def variant_for(size: int) -> str:
    if size <= 20:
        return "small"
    if size <= 48:
        return "medium"
    return "full"


def find_browser() -> str:
    candidates = [
        os.environ.get("CHROME", ""),
        "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
        "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge",
        r"C:\Program Files\Google\Chrome\Application\chrome.exe",
        r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
        shutil.which("google-chrome") or "",
        shutil.which("chromium") or "",
        shutil.which("msedge") or "",
    ]
    for path in candidates:
        if path and Path(path).exists():
            return path
    sys.exit("Chrome or Edge not found. Set the CHROME environment variable to its path.")


def render(browser: str, svg: Path, size: int, workdir: Path) -> Image.Image:
    page = workdir / f"{svg.stem}.html"
    page.write_text(
        f'<!doctype html><body style="margin:0;background:transparent">'
        f'<img src="{svg.as_uri()}" width="{size}" height="{size}" style="display:block"></body>',
        encoding="utf-8",
    )
    out = workdir / f"{svg.stem}.png"
    subprocess.run(
        [
            browser, "--headless=new", "--disable-gpu", "--hide-scrollbars",
            "--force-device-scale-factor=1", "--default-background-color=00000000",
            "--allow-file-access-from-files", f"--window-size={size},{size}",
            f"--screenshot={out}", page.as_uri(),
        ],
        check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
    )
    image = Image.open(out).convert("RGBA")
    if image.size != (size, size):
        sys.exit(f"{svg.name}: browser returned {image.size}, expected {size}x{size}")
    return image


def downscale(image: Image.Image, size: int) -> Image.Image:
    # Resize with premultiplied alpha so the transparent corners don't bleed dark fringes.
    method = Image.Resampling.BOX if size <= 48 else Image.Resampling.LANCZOS
    return image.convert("RGBa").resize((size, size), method).convert("RGBA")


def bmp_entry(image: Image.Image) -> bytes:
    w, h = image.size
    header = struct.pack("<IiiHHIIiiII", 40, w, h * 2, 1, 32, 0, 0, 0, 0, 0, 0)
    pixels = bytearray()
    mask = bytearray()
    row_bytes = ((w + 31) // 32) * 4
    data = image.load()
    for y in range(h - 1, -1, -1):
        bits = bytearray(row_bytes)
        for x in range(w):
            r, g, b, a = data[x, y]
            pixels += bytes((b, g, r, a))
            if a == 0:
                bits[x // 8] |= 0x80 >> (x % 8)
        mask += bits
    return header + bytes(pixels) + bytes(mask)


def png_entry(image: Image.Image) -> bytes:
    buffer = io.BytesIO()
    image.save(buffer, "PNG", optimize=True)
    return buffer.getvalue()


def write_ico(path: Path, images: list[Image.Image]) -> None:
    # BMP frames up to 64 px (read by every Windows tool), PNG frames above (standard for 128/256).
    blobs = [bmp_entry(im) if im.width <= 64 else png_entry(im) for im in images]
    offset = 6 + 16 * len(images)
    directory = struct.pack("<HHH", 0, 1, len(images))
    for im, blob in zip(images, blobs):
        side = 0 if im.width >= 256 else im.width
        directory += struct.pack("<BBBBHHII", side, side, 0, 0, 1, 32, len(blob), offset)
        offset += len(blob)
    path.write_bytes(directory + b"".join(blobs))


def main() -> None:
    browser = find_browser()
    (BRAND / "png").mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory() as tmp:
        masters = {name: render(browser, svg, RENDER, Path(tmp)) for name, svg in VARIANTS.items()}

    frames = {size: downscale(masters[variant_for(size)], size) for size in ICO_SIZES}
    downscale(masters["full"], PNG_SIZE).save(BRAND / "png" / f"mubsir-{PNG_SIZE}.png", optimize=True)
    print(f"png  {PNG_SIZE} px")

    write_ico(BRAND / "mubsir.ico", [frames[s] for s in ICO_SIZES])
    print(f"ico  {', '.join(map(str, ICO_SIZES))} px -> {(BRAND / 'mubsir.ico').relative_to(ROOT)}")


if __name__ == "__main__":
    main()
