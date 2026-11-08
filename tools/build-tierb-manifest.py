#!/usr/bin/env python3
"""Generate a Tier B playability manifest from the comparison data.

Tier B is the set of titles where a peer emulator claims playability and 5DO
only has automated loadability evidence. This tool selects the next batch,
resolves each canonical filename to the local image, and writes a manifest for
``tools/playability-sweep.py``.

Only the Python standard library is required.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

DEFAULT_COMPARISON = "docs/data/compatibility-comparison.json"
DEFAULT_INVENTORY = "artifacts/corpus/inventory.json"
DEFAULT_OUT = "benchmarks/diff/tierb-panel.json"
DEFAULT_EXCLUDE = "benchmarks/diff/tierb-behind.json"
PEER_PLAYABLE = {"Good", "Playable"}


def load_excluded(path: Path) -> set[str]:
    if not path.exists():
        return set()
    data = json.loads(path.read_text(encoding="utf-8"))
    return {title["name"] for title in data.get("titles", []) if title.get("name")}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--comparison", default=DEFAULT_COMPARISON)
    parser.add_argument("--inventory", default=DEFAULT_INVENTORY)
    parser.add_argument("--out", default=DEFAULT_OUT)
    parser.add_argument("--limit", type=int, default=32)
    parser.add_argument("--exclude", default=DEFAULT_EXCLUDE)
    parser.add_argument(
        "--include-reviewed",
        action="store_true",
        help="Include titles that already have a manual grade.",
    )
    args = parser.parse_args()

    comparison = json.loads(Path(args.comparison).read_text(encoding="utf-8"))
    inventory = json.loads(Path(args.inventory).read_text(encoding="utf-8"))
    excluded = load_excluded(Path(args.exclude))

    by_file = {record["file"]: record for record in inventory if record.get("path")}
    candidates = []
    for title in comparison.get("titles", []):
        if title.get("kind") not in ("game", "homebrew"):
            continue
        if title.get("title") in excluded:
            continue
        if not args.include_reviewed and title.get("fiveDoEvidence") != "loadability":
            continue
        if title.get("fourDo") not in PEER_PLAYABLE:
            continue
        if title.get("phoenix") != "Good":
            continue
        record = by_file.get(title.get("canonical"))
        if record is None:
            continue
        candidates.append(
            {
                "id": title["title"].lower().replace("'", "").replace(" ", "-"),
                "name": title["title"],
                "disc": record["path"],
                "bios": None,
                "biosAuto": "bios",
                "inputDevice": None,
                "note": f"5DO {title['fiveDo']}; 4DO {title['fourDo']}; Phoenix {title['phoenix']}",
            }
        )

    candidates.sort(key=lambda item: item["name"].lower())
    selected = candidates[: max(0, args.limit)]
    manifest = {
        "schemaVersion": 1,
        "name": f"Tier B playability panel ({len(selected)} titles)",
        "defaults": {
            "biosAuto": "bios",
            "frames": 36000,
            "captureFrames": [600, 3600, 18000, 36000],
            "inputScript": "benchmarks/diff/scripts/tierb-10min.txt",
            "audio": "null",
            "prefetch": True
        },
        "titles": selected,
    }
    Path(args.out).write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(
        f"Wrote {args.out}: {len(selected)} of {len(candidates)} remaining candidates"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
