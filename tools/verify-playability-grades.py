#!/usr/bin/env python3
"""Verify the Tier C playability-grade ledger.

Checks:

* every row has a known ``Good``/``Playable``/``Unplayable`` status;
* the ledger has no duplicate disc names;
* every ``Good`` row has a reason and every ``tierc-*`` ``Good`` row cites
  an ``Evidence:`` path;
* every ``Playable`` row states why it remains playable;
* an optional baseline fails the run when a title drops from ``Good``.

Only the Python standard library is required.
"""

from __future__ import annotations

import argparse
import csv
import json
import sys
from collections import Counter
from pathlib import Path

DEFAULT_LEDGER = "docs/data/playability-grades.csv"
DEFAULT_BASELINE = "docs/data/playability-grades-baseline.json"
STATUSES = ("Good", "Playable", "Unplayable")


def load_ledger(path: Path) -> list[dict]:
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        return list(csv.DictReader(stream))


def build_baseline(rows: list[dict], ledger: Path) -> dict:
    counts = Counter((row.get("status") or "").strip() for row in rows)
    return {
        "schemaVersion": 1,
        "ledger": str(ledger).replace("\\", "/"),
        "counts": {status: counts.get(status, 0) for status in STATUSES},
        "titles": {
            (row.get("file") or "").strip(): (row.get("status") or "").strip()
            for row in rows
            if (row.get("file") or "").strip()
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ledger", default=DEFAULT_LEDGER)
    parser.add_argument("--baseline", default=DEFAULT_BASELINE)
    parser.add_argument(
        "--write",
        action="store_true",
        help="write the current statuses to --baseline instead of checking",
    )
    args = parser.parse_args()

    ledger_path = Path(args.ledger)
    if not ledger_path.exists():
        print(f"missing ledger: {ledger_path}", file=sys.stderr)
        return 1

    rows = load_ledger(ledger_path)
    if not rows:
        print(f"empty ledger: {ledger_path}", file=sys.stderr)
        return 1

    baseline_path = Path(args.baseline)
    if args.write:
        baseline_path.write_text(
            json.dumps(build_baseline(rows, ledger_path), indent=2) + "\n",
            encoding="utf-8",
        )
        print(f"wrote baseline: {baseline_path}")
        return 0

    problems: list[str] = []
    seen: set[str] = set()
    counts = Counter()
    for line, row in enumerate(rows, start=2):
        file_name = (row.get("file") or "").strip()
        status = (row.get("status") or "").strip()
        notes = (row.get("notes") or "").strip()
        batch = (row.get("batch") or "").strip()
        if not file_name:
            problems.append(f"line {line}: empty file")
            continue
        key = file_name.casefold()
        if key in seen:
            problems.append(f"line {line}: duplicate row for {file_name}")
        seen.add(key)
        if status not in STATUSES:
            problems.append(f"{file_name}: unknown status '{status}'")
            continue
        counts[status] += 1
        if not notes:
            problems.append(f"{file_name}: empty notes")
        if status == "Good" and batch.startswith("tierc-") and "Evidence:" not in notes:
            problems.append(f"{file_name}: tierc Good row has no Evidence: path")
        if status == "Playable" and not notes:
            problems.append(f"{file_name}: Playable row has no reason")

    if baseline_path.exists():
        baseline = json.loads(baseline_path.read_text(encoding="utf-8"))
        previous = baseline.get("titles", {})
        previous_counts = baseline.get("counts", {})
        current = {
            (row.get("file") or "").strip(): (row.get("status") or "").strip()
            for row in rows
        }
        for status in STATUSES:
            if counts.get(status, 0) != previous_counts.get(status, 0):
                problems.append(
                    f"{status} count changed: baseline {previous_counts.get(status, 0)}, "
                    f"now {counts.get(status, 0)}; update the baseline with a dated batch"
                )
        for file_name, old_status in previous.items():
            if old_status != "Good":
                continue
            new_status = current.get(file_name)
            if new_status is None:
                problems.append(f"baseline title missing from ledger: {file_name}")
            elif new_status != "Good":
                problems.append(
                    f"quality regression: {file_name} was Good, now {new_status}"
                )
    else:
        problems.append(f"missing baseline: {baseline_path} (run with --write)")

    print("ledger rows:", len(rows))
    for status in STATUSES:
        print(f"  {status}: {counts.get(status, 0)}")
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
