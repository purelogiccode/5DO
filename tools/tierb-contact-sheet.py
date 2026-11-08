#!/usr/bin/env python3
"""Build a contact sheet from a Tier B playability run.

A 10-minute pass produces four captures per title; reviewing them one by one is
slow. This tool tiles the final frame (or any requested frame) into a labelled
grid so a batch can be triaged in one image.

Requires Pillow (already used by the documentation tooling).
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw


def title_for(directory: Path) -> str:
    result = directory / "result.json"
    if result.exists():
        try:
            data = json.loads(result.read_text(encoding="utf-8"))
            if data.get("name"):
                return str(data["name"])
        except json.JSONDecodeError:
            pass
    return directory.name


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dir", required=True, help="Playability output directory.")
    parser.add_argument("--frame", type=int, default=36000)
    parser.add_argument("--columns", type=int, default=4)
    parser.add_argument("--scale", type=int, default=1, help="Integer upscale.")
    parser.add_argument("--out", default=None)
    args = parser.parse_args()

    root = Path(args.dir)
    entries = []
    for directory in sorted(path for path in root.iterdir() if path.is_dir()):
        capture = directory / f"f{args.frame:06d}.png"
        if capture.exists():
            entries.append((title_for(directory), capture))
    if not entries:
        raise SystemExit(f"no f{args.frame:06d}.png files under {root}")

    with Image.open(entries[0][1]) as first:
        width, height = first.size
    scale = max(1, args.scale)
    cell_w = width * scale
    cell_h = height * scale + 18
    columns = max(1, args.columns)
    rows = (len(entries) + columns - 1) // columns
    sheet = Image.new("RGB", (cell_w * columns, cell_h * rows), (24, 24, 24))
    draw = ImageDraw.Draw(sheet)

    for index, (name, path) in enumerate(entries):
        column = index % columns
        row = index // columns
        with Image.open(path) as image:
            image = image.convert("RGB").resize((cell_w, height * scale), Image.Resampling.NEAREST)
        sheet.paste(image, (column * cell_w, row * cell_h + 18))
        label = name if len(name) <= 34 else name[:31] + "..."
        draw.text((column * cell_w + 4, row * cell_h + 4), label, fill=(220, 220, 220))

    output = Path(args.out) if args.out else root / f"contact-{args.frame}.png"
    sheet.save(output)
    print(f"Wrote {output}: {len(entries)} titles, {columns} columns")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
