#!/usr/bin/env python3
"""Verify the beyond-title ledger covers the 378-game comparison set.

Checks:

* every game/homebrew canonical disc has exactly one ledger row;
* each row has a dated status, BIOS, disc hash, capture frame, and screenshot;
* the screenshot path exists;
* every peer-playable title is ``Playable`` (or has an explicit peer-invalid row);
* the comparison has no ``Behind`` or ``Regression`` rows.

Exit code 1 on any problem. Only the Python standard library is required.
"""

from __future__ import annotations

import argparse
import csv
import json
import sys
from collections import Counter
from pathlib import Path

DEFAULT_LEDGER = "docs/data/beyond-title-results.csv"
DEFAULT_COMPARISON = "docs/data/compatibility-comparison.json"
PLAYABLE = "Playable"


def effective(row: dict) -> str:
    return (row.get("manualStatus") or row.get("status") or "").strip()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ledger", default=DEFAULT_LEDGER)
    parser.add_argument("--comparison", default=DEFAULT_COMPARISON)
    args = parser.parse_args()

    ledger_path = Path(args.ledger)
    if not ledger_path.exists():
        print(f"missing ledger: {ledger_path}", file=sys.stderr)
        return 1
    with ledger_path.open("r", encoding="utf-8-sig", newline="") as stream:
        ledger_rows = list(csv.DictReader(stream))
    ledger = {(row.get("file") or "").casefold(): row for row in ledger_rows if row.get("file")}
    comparison = json.loads(Path(args.comparison).read_text(encoding="utf-8"))
    titles = [
        row
        for row in comparison.get("titles", [])
        if row.get("kind") in ("game", "homebrew")
    ]

    problems: list[str] = []
    counts = Counter(effective(row) for row in ledger.values())
    if len(ledger) != len(ledger_rows):
        problems.append(f"duplicate ledger rows: {len(ledger_rows) - len(ledger)}")
    if len(ledger) != len(titles):
        problems.append(f"ledger rows {len(ledger)} != test set {len(titles)}")

    missing = [title for title in titles if (title.get("canonical") or "").casefold() not in ledger]
    if missing:
        problems.append(f"{len(missing)} test-set titles missing from the ledger")

    for title in titles:
        file_key = (title.get("canonical") or "").casefold()
        row = ledger.get(file_key)
        if not row:
            continue
        status = effective(row)
        if not status:
            problems.append(f"{row.get('file')}: empty status")
        if not row.get("bios"):
            problems.append(f"{row.get('file')}: missing BIOS")
        if not row.get("discHash"):
            problems.append(f"{row.get('file')}: missing disc hash")
        if not row.get("date"):
            problems.append(f"{row.get('file')}: missing date")
        if not row.get("frame"):
            problems.append(f"{row.get('file')}: missing capture frame")
        screenshot = row.get("screenshot") or ""
        if not screenshot:
            problems.append(f"{row.get('file')}: missing screenshot path")
        elif not Path(screenshot).exists():
            problems.append(f"{row.get('file')}: screenshot missing on disk: {screenshot}")
        peer_playable = (
            title.get("fourDo") in ("Good", "Playable") and not title.get("fourDoInvalid")
        ) or (title.get("phoenix") == "Good" and not title.get("phoenixInvalid"))
        if peer_playable and status != PLAYABLE:
            problems.append(
                f"{row.get('file')}: peer-playable but ledger status is {status}"
            )

    summary = comparison.get("summary", {})
    if summary.get("behind"):
        problems.append(f"comparison has {summary['behind']} Behind rows")
    if summary.get("regressions"):
        problems.append(f"comparison has {summary['regressions']} Regression rows")

    print("ledger rows:", len(ledger))
    for status in ("Playable", "Title-only", "No video", "Not loadable"):
        print(f"  {status}: {counts.get(status, 0)}")
    print("test set:", len(titles))
    print("comparison playable:", summary.get("fiveDoPlayable"))
    print("peer union:", summary.get("peerUnion"))
    print("strict superset:", summary.get("strictSuperset"))
    if problems:
        print("verification failed:", file=sys.stderr)
        for problem in problems[:50]:
            print(f"  {problem}", file=sys.stderr)
        if len(problems) > 50:
            print(f"  ... {len(problems) - 50} more", file=sys.stderr)
        return 1
    print("verification passed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
