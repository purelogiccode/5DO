#!/usr/bin/env python3
"""Build labelled contact sheets from a beyond-title sweep directory.

The classifier writes one directory per title with ``captures/`` and
``summary.json``. This tool tiles the final capture (or any requested frame)
with the title and observed status so a batch can be reviewed quickly.

Requires Pillow (already used by the documentation tooling).
"""

from __future__ import annotations

import argparse
import csv
import json
from pathlib import Path

from PIL import Image, ImageDraw

STATUS_ORDER = ("Playable", "Title-only", "No video", "Not loadable")


def load_records(root: Path) -> list[dict]:
    summary = root / "summary.csv"
    if summary.exists():
        with summary.open("r", encoding="utf-8-sig", newline="") as stream:
            return list(csv.DictReader(stream))
    records = []
    for path in sorted(root.glob("*/summary.json")):
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
        except json.JSONDecodeError:
            continue
        if data.get("record"):
            records.append(data["record"])
    return records


def capture_for(root: Path, record: dict, frame: int | None) -> Path | None:
    screenshot = record.get("screenshot") or ""
    if screenshot:
        base = Path(screenshot)
        if frame:
            candidate = base.parent / f"f{frame:06d}.png"
            return candidate if candidate.exists() else None
        if base.exists():
            return base
    directory = root / slug(record.get("title") or record.get("file") or "")
    if frame:
        candidate = directory / "captures" / f"f{frame:06d}.png"
        return candidate if candidate.exists() else None
    captures = sorted((directory / "captures").glob("*.png"))
    return captures[-1] if captures else None


def slug(value: str) -> str:
    import re

    value = re.sub(r"\[[^\]]*\]", " ", value or "")
    value = re.sub(r"\([^)]*\)", " ", value)
    value = re.sub(r"[^A-Za-z0-9]+", "-", value).strip("-").lower()
    return value[:80] or "title"


def build_sheet(entries: list[tuple[dict, Path]], output: Path, columns: int, scale: int) -> None:
    with Image.open(entries[0][1]) as first:
        width, height = first.size
    cell_w = width * scale
    cell_h = height * scale + 22
    columns = max(1, columns)
    rows = (len(entries) + columns - 1) // columns
    sheet = Image.new("RGB", (cell_w * columns, cell_h * rows), (24, 24, 24))
    draw = ImageDraw.Draw(sheet)
    for index, (record, path) in enumerate(entries):
        column = index % columns
        row = index // columns
        with Image.open(path) as image:
            image = image.convert("RGB").resize((cell_w, height * scale), Image.Resampling.NEAREST)
        sheet.paste(image, (column * cell_w, row * cell_h + 22))
        title = record.get("title") or record.get("file") or ""
        label = title if len(title) <= 34 else title[:31] + "..."
        status = record.get("status", "")
        review = " REVIEW" if record.get("review") == "yes" else ""
        draw.text((column * cell_w + 4, row * cell_h + 3), label, fill=(225, 225, 225))
        draw.text((column * cell_w + 4, row * cell_h + 11), f"{status}{review}", fill=(160, 200, 160))
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output)
    print(f"Wrote {output}: {len(entries)} titles, {columns} columns")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dir", required=True, help="Beyond-title output directory.")
    parser.add_argument("--status", default="", help="Only this status.")
    parser.add_argument("--frame", type=int, default=0, help="Capture frame, or 0 for the final capture.")
    parser.add_argument("--columns", type=int, default=4)
    parser.add_argument("--scale", type=int, default=1)
    parser.add_argument("--max", type=int, default=40, help="Maximum titles per sheet.")
    parser.add_argument("--out", default=None, help="Output prefix.")
    args = parser.parse_args()

    root = Path(args.dir)
    records = load_records(root)
    if args.status:
        records = [record for record in records if record.get("status") == args.status]
    entries = []
    for record in records:
        capture = capture_for(root, record, args.frame or None)
        if capture:
            entries.append((record, capture))
    if not entries:
        raise SystemExit(f"no captures under {root}")

    prefix = Path(args.out) if args.out else root / "contact"
    part = 1
    for start in range(0, len(entries), max(1, args.max)):
        chunk = entries[start : start + max(1, args.max)]
        status = args.status or "all"
        output = prefix.parent / f"{prefix.name}-{status.lower().replace(' ', '-')}-{part:02d}.png"
        build_sheet(chunk, output, args.columns, args.scale)
        part += 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
