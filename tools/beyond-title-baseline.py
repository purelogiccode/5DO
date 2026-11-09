#!/usr/bin/env python3
"""Maintain and gate the committed beyond-title baseline.

The nightly/weekly full-corpus pass writes the evidence ledger. This tool turns
that ledger into a committed baseline and fails a later run when a title that
was ``Playable`` regresses to any failing state. Improvements and newly tested
titles are allowed. A justified, dated exception can temporarily suppress a
known regression:

```json
{
  "exceptions": [
    {
      "file": "Example (USA).chd",
      "reason": "Tracked in COMPAT-10; BIOS regression under bisect.",
      "expires": "2027-01-31"
    }
  ]
}
```

Only the Python standard library is required.
"""

from __future__ import annotations

import argparse
import csv
import json
import sys
from collections import Counter
from datetime import date
from pathlib import Path

DEFAULT_LEDGER = "docs/data/beyond-title-results.csv"
DEFAULT_BASELINE = "docs/data/beyond-title-baseline.json"
DEFAULT_EXCEPTIONS = "docs/data/beyond-title-exceptions.json"
PLAYABLE = "Playable"


def effective_status(row: dict) -> str:
    return (row.get("manualStatus") or row.get("status") or "").strip()


def read_ledger(path: Path) -> dict[str, dict]:
    rows: dict[str, dict] = {}
    if not path.exists():
        return rows
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        for row in csv.DictReader(stream):
            name = (row.get("file") or "").strip()
            if name:
                rows[name.casefold()] = row
    return rows


def read_exceptions(path: Path) -> dict[str, dict]:
    if not path.exists():
        return {}
    data = json.loads(path.read_text(encoding="utf-8"))
    return {
        (item.get("file") or "").casefold(): item
        for item in data.get("exceptions", [])
        if item.get("file")
    }


def build_baseline(ledger: dict[str, dict]) -> dict:
    rows = {}
    for name, row in sorted(ledger.items()):
        status = effective_status(row)
        if not status:
            continue
        rows[name] = {
            "file": row.get("file", ""),
            "title": row.get("title", ""),
            "status": status,
            "date": row.get("date", ""),
            "screenshot": row.get("screenshot", ""),
            "discHash": row.get("discHash", ""),
            "bios": row.get("bios", ""),
        }
    counts = Counter(item["status"] for item in rows.values())
    return {
        "schemaVersion": 1,
        "updated": date.today().isoformat(),
        "counts": dict(sorted(counts.items())),
        "rows": rows,
    }


def check(
    baseline: dict,
    ledger: dict[str, dict],
    exceptions: dict[str, dict],
) -> list[str]:
    problems: list[str] = []
    today = date.today().isoformat()
    for name, previous in baseline.get("rows", {}).items():
        if previous.get("status") != PLAYABLE:
            continue
        current = ledger.get(name)
        status = effective_status(current) if current else "Missing"
        if status == PLAYABLE:
            continue
        exception = exceptions.get(name)
        if exception:
            expires = exception.get("expires", "")
            if not expires or expires >= today:
                continue
            problems.append(
                f"{previous.get('file')}: exception expired on {expires} "
                f"(current {status})"
            )
            continue
        problems.append(
            f"{previous.get('file')}: Playable -> {status} "
            f"(was {previous.get('date', 'undated')})"
        )
    return problems


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ledger", default=DEFAULT_LEDGER)
    parser.add_argument("--baseline", default=DEFAULT_BASELINE)
    parser.add_argument("--exceptions", default=DEFAULT_EXCEPTIONS)
    parser.add_argument("--update", action="store_true", help="Write the baseline from the ledger.")
    parser.add_argument("--check", action="store_true", help="Fail on a Playable regression.")
    parser.add_argument(
        "--require-count",
        type=int,
        default=0,
        help="Fail unless the ledger contains at least this many rows.",
    )
    args = parser.parse_args()

    ledger = read_ledger(Path(args.ledger))
    if args.require_count and len(ledger) < args.require_count:
        print(
            f"ledger coverage {len(ledger)} < required {args.require_count}",
            file=sys.stderr,
        )
        return 1

    if args.update:
        baseline = build_baseline(ledger)
        output = Path(args.baseline)
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(baseline, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        counts = baseline["counts"]
        print(
            f"Wrote {output}: {len(baseline['rows'])} rows, "
            + ", ".join(f"{key} {value}" for key, value in counts.items())
        )
        return 0

    if args.check:
        baseline_path = Path(args.baseline)
        if not baseline_path.exists():
            print(f"missing baseline: {baseline_path}", file=sys.stderr)
            return 1
        baseline = json.loads(baseline_path.read_text(encoding="utf-8"))
        exceptions = read_exceptions(Path(args.exceptions))
        problems = check(baseline, ledger, exceptions)
        if problems:
            print("beyond-title regression gate failed:", file=sys.stderr)
            for problem in problems:
                print(f"  {problem}", file=sys.stderr)
            return 1
        print(
            f"beyond-title regression gate passed: {len(ledger)} ledger rows, "
            f"{len(baseline.get('rows', {}))} baseline rows"
        )
        return 0

    parser.print_help()
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
