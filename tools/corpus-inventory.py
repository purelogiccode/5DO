#!/usr/bin/env python3
"""Inventory a local 3DO disc collection and fingerprint every image.

The compatibility matrix needs a stable dedupe key. Filenames in a mixed
Redump/No-Intro collection describe the same disc in different ways, so this
tool asks the 5DO CLI for the disc's ``gameId`` (the MD5 of the first two
2048-byte sectors) and records the database metadata alongside it.

The output is a JSON inventory plus a CSV with one row per image. Both are
written under ``artifacts/`` by default and are safe to regenerate.

Only the Python standard library is required.
"""

from __future__ import annotations

import argparse
import csv
import json
import os
import subprocess
import sys
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

DEFAULT_ROOTS = (r"J:\Panasonic 3DO", r"G:\MAME\MAME Software List CHDs\3do")
DEFAULT_OUT = "artifacts/corpus/inventory.json"
DEFAULT_CLI = "src/FiveDo.Cli/bin/Release/net10.0/FiveDo.Cli.dll"
DISC_EXTENSIONS = (".chd", ".iso", ".cue")

CSV_FIELDS = (
    "root",
    "file",
    "path",
    "bytes",
    "gameId",
    "name",
    "regions",
    "revision",
    "discNumber",
    "discTotal",
    "trackCount",
    "audioCd",
    "sectorCount",
    "volumeBlockCount",
    "volumeLabel",
    "known",
    "error",
)


def scan_discs(roots: list[str]) -> list[Path]:
    discs: list[Path] = []
    for root in roots:
        root_path = Path(root)
        if not root_path.exists():
            print(f"skipping missing root: {root}", file=sys.stderr)
            continue
        for directory, _, files in os.walk(root_path):
            for name in files:
                if name.lower().endswith(DISC_EXTENSIONS):
                    discs.append(Path(directory) / name)
    return sorted(discs, key=lambda path: (path.name.lower(), str(path).lower()))


def probe(cli: list[str], path: Path) -> dict[str, object]:
    record: dict[str, object] = {
        "path": str(path),
        "file": path.name,
        "bytes": path.stat().st_size if path.exists() else 0,
        "gameId": "",
        "name": "",
        "regions": "",
        "revision": "",
        "discNumber": None,
        "discTotal": None,
        "trackCount": None,
        "audioCd": None,
        "sectorCount": None,
        "volumeBlockCount": None,
        "volumeLabel": "",
        "known": None,
        "error": "",
    }
    try:
        completed = subprocess.run(
            [*cli, "info", str(path), "--json"],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=120,
            check=False,
        )
    except (OSError, subprocess.TimeoutExpired) as error:
        record["error"] = str(error)
        return record

    if completed.returncode != 0:
        record["error"] = (completed.stderr or completed.stdout).strip()[:400]
        return record

    try:
        data = json.loads(completed.stdout)
    except json.JSONDecodeError as error:
        record["error"] = f"invalid JSON: {error}"
        return record

    for key in (
        "gameId",
        "name",
        "regions",
        "revision",
        "discNumber",
        "discTotal",
        "trackCount",
        "audioCd",
        "sectorCount",
        "volumeBlockCount",
        "volumeLabel",
        "known",
    ):
        if key in data:
            record[key] = data[key]
    return record


def root_for(path: str, roots: list[str]) -> str:
    lowered = path.lower()
    for root in roots:
        if lowered.startswith(str(Path(root)).lower()):
            return root
    return ""


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--root",
        action="append",
        default=None,
        help="Collection root. Repeat for multiple roots.",
    )
    parser.add_argument("--out", default=DEFAULT_OUT, help="JSON inventory output.")
    parser.add_argument(
        "--cli",
        default=DEFAULT_CLI,
        help="Path to FiveDo.Cli.dll or a published 5do executable.",
    )
    parser.add_argument("--jobs", type=int, default=6, help="Concurrent CLI processes.")
    parser.add_argument(
        "--refresh",
        action="store_true",
        help="Ignore cached results and probe every disc again.",
    )
    args = parser.parse_args()

    roots = args.root or list(DEFAULT_ROOTS)
    output = Path(args.out)
    output.parent.mkdir(parents=True, exist_ok=True)

    cached: dict[str, dict[str, object]] = {}
    if output.exists() and not args.refresh:
        try:
            cached = {
                record["path"]: record
                for record in json.loads(output.read_text(encoding="utf-8"))
                if record.get("path")
            }
        except (json.JSONDecodeError, OSError):
            cached = {}

    discs = scan_discs(roots)
    cli = [args.cli] if args.cli.lower().endswith(".exe") else ["dotnet", args.cli]
    if not Path(args.cli).exists():
        parser.error(f"CLI not found: {args.cli}")

    pending = [disc for disc in discs if args.refresh or str(disc) not in cached]
    print(f"{len(discs)} images, {len(pending)} to probe", file=sys.stderr)

    def work(disc: Path) -> dict[str, object]:
        record = probe(cli, disc)
        record["root"] = root_for(str(disc), roots)
        return record

    with ThreadPoolExecutor(max_workers=max(1, args.jobs)) as pool:
        for index, record in enumerate(pool.map(work, pending), start=1):
            cached[str(record["path"])] = record
            if index % 25 == 0 or index == len(pending):
                print(f"  probed {index}/{len(pending)}", file=sys.stderr)

    records = [cached[str(disc)] for disc in discs]
    records.sort(key=lambda record: (str(record.get("file", "")).lower(), str(record.get("path", "")).lower()))
    output.write_text(json.dumps(records, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    csv_path = output.with_suffix(".csv")
    with csv_path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=CSV_FIELDS, extrasaction="ignore")
        writer.writeheader()
        for record in records:
            writer.writerow(record)

    errors = sum(1 for record in records if record.get("error"))
    unique = len({record.get("gameId") or record.get("path") for record in records})
    print(
        f"Wrote {output} and {csv_path}: {len(records)} images, "
        f"{unique} distinct game IDs, {errors} errors"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
