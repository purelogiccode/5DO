#!/usr/bin/env python3
"""Render the committed beyond-title ledger as a Markdown evidence page.

Inputs:

* ``docs/data/beyond-title-results.csv`` - one row per tested canonical disc;
* ``docs/data/compatibility-comparison.json`` - peer claims and playable state.

Output: ``docs/BeyondTitleEvidence.md``. The page is intentionally evidence-heavy:
every row carries BIOS, disc hash, capture frame, and screenshot path. Use
``--check`` in docs CI to fail when the page is stale.

Only the Python standard library is required.
"""

from __future__ import annotations

import argparse
import csv
import json
from collections import Counter
from pathlib import Path

DEFAULT_LEDGER = "docs/data/beyond-title-results.csv"
DEFAULT_COMPARISON = "docs/data/compatibility-comparison.json"
DEFAULT_OUT = "docs/BeyondTitleEvidence.md"

STATUS_ORDER = ("Playable", "Title-only", "No video", "Not loadable")


def escape(value: str) -> str:
    return (value or "").replace("|", "\\|").replace("\n", " ").strip()


def short_hash(value: str) -> str:
    return (value or "")[:12]


def load_ledger(path: Path) -> list[dict]:
    if not path.exists():
        return []
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        return [row for row in csv.DictReader(stream) if (row.get("file") or "").strip()]


def effective_status(row: dict) -> str:
    return (row.get("manualStatus") or row.get("status") or "").strip()


def triage_bucket(row: dict) -> str:
    if row.get("triageBucket"):
        return row["triageBucket"]
    status = effective_status(row)
    reason = ((row.get("failureReason") or "") + " " + (row.get("notes") or "")).lower()
    if status == "Playable":
        return ""
    if status == "No video":
        return "core/video"
    if status == "Not loadable":
        if "timeout" in reason:
            return "harness/script gap"
        return "core/CD boot"
    if "input" in reason or "title" in reason or "script" in reason or "attract" in reason:
        return "harness/script gap"
    return "per-title game fix"


def reproduction_for(row: dict) -> str:
    if row.get("reproduction"):
        return row["reproduction"]
    disc = row.get("path") or row.get("file") or ""
    bios = row.get("bios") or ""
    command = f'dotnet src/FiveDo.Cli/bin/Release/net11.0/FiveDo.Cli.dll run "{disc}"'
    command += f' --bios "{bios}"' if bios else " --bios-auto bios"
    command += f' --frames {row.get("frameCount") or 12000} --audio null --json'
    script = row.get("inputScript") or ""
    if script:
        command += f' --input-script "{script}"'
    return command


def render(ledger: list[dict], comparison: dict) -> str:
    by_file = {(row.get("file") or "").casefold(): row for row in ledger}
    rows = [
        row
        for row in comparison.get("titles", [])
        if row.get("kind") in ("game", "homebrew")
    ]
    status_counts = Counter(effective_status(row) for row in ledger)
    review = [row for row in ledger if row.get("review") == "yes"]

    def beyond_for(title: dict) -> dict | None:
        return by_file.get((title.get("canonical") or "").casefold())

    def peer_playable(title: dict) -> bool:
        four = title.get("fourDo") in ("Good", "Playable") and not title.get("fourDoInvalid")
        phx = title.get("phoenix") == "Good" and not title.get("phoenixInvalid")
        return four or phx

    lines = [
        "# Beyond-title playability evidence",
        "",
        "This page is generated from `docs/data/beyond-title-results.csv` by "
        "`python tools/beyond-title-report.py`. It records the dated beyond-title pass "
        "for the 378-game test set: capture frame, BIOS, disc hash, screenshot, and "
        "failure reason for every row.",
        "",
        "## Summary",
        "",
        "| Measure | Titles |",
        "|---|---:|",
        f"| Ledger rows | {len(ledger)} |",
    ]
    for status in STATUS_ORDER:
        lines.append(f"| {status} | {status_counts.get(status, 0)} |")
    lines.append(f"| Review flagged | {len(review)} |")
    covered = sum(1 for title in rows if beyond_for(title))
    lines.append(f"| 378-game set covered | {covered}/{len(rows)} |")
    playable = sum(
        1
        for title in rows
        if effective_status(beyond_for(title) or {}) == "Playable"
    )
    lines.append(f"| 5DO playable (ledger only) | {playable} |")
    lines.append("")

    bios_counts = Counter(
        (row.get("bios") or row.get("biosSelection") or "unknown") for row in ledger
    )
    device_counts = Counter()
    for row in ledger:
        devices = row.get("inputDevice") or ""
        found = [
            name
            for name in ("mouse", "lightgun", "arcade-lightgun", "flightstick", "orbatak-trackball")
            if name in devices
        ]
        device_counts[", ".join(found) if found else "joypad/default"] += 1
    lines.append("## BIOS and input coverage")
    lines.append("")
    lines.append("| BIOS | Rows |")
    lines.append("|---|---:|")
    for name, count in bios_counts.most_common():
        lines.append(f"| {escape(name)} | {count} |")
    lines.append("")
    lines.append("| Input device | Rows |")
    lines.append("|---|---:|")
    for name, count in device_counts.most_common():
        lines.append(f"| {escape(name)} | {count} |")
    lines.append("")

    peer_gap = [
        title
        for title in rows
        if peer_playable(title) and effective_status(beyond_for(title) or {}) != "Playable"
    ]
    lines.append("## Peer-gap rows")
    lines.append("")
    if not peer_gap:
        lines.append("None: every peer-playable title has a beyond-title `Playable` row.")
    else:
        lines.append(
            "These titles are peer-playable but do not yet have a beyond-title `Playable` "
            "row. Each row links to the latest capture and failure reason."
        )
        lines.append("")
        lines.append("| Title | 4DO | Phoenix | 5DO | Reason | Screenshot |")
        lines.append("|---|---|---|---|---|---|")
        for title in peer_gap:
            result = beyond_for(title) or {}
            lines.append(
                "| "
                + " | ".join(
                    [
                        escape(title.get("title", "")),
                        escape(title.get("fourDo", "—")),
                        escape(title.get("phoenix", "—")),
                        escape(effective_status(result) or "Untested"),
                        escape(result.get("failureReason", "")),
                        escape(result.get("screenshot", "")),
                    ]
                )
                + " |"
            )
    lines.append("")

    failures = [row for row in ledger if effective_status(row) != "Playable"]
    lines.append("## Failures and triage")
    lines.append("")
    if not failures:
        lines.append("None.")
        lines.append("")
    else:
        lines.append(
            "Every non-playable title has one triage bucket, a reproduction command, the "
            "last capture, and the failing reason. The buckets are BIOS/region, "
            "disc/multi-disc, input device, core/CD boot, per-title game fix, and "
            "harness/script gap; a row can be re-bucketed with an explicit `triageBucket` "
            "column."
        )
        lines.append("")
        lines.append("| Title | Status | Bucket | PC / frame hash | Reason | Screenshot | Reproduction |")
        lines.append("|---|---|---|---|---|---|---|")
        for row in sorted(failures, key=lambda item: (item.get("title") or "").casefold()):
            pc = row.get("failurePc") or ""
            frame_hash = row.get("failureHash") or ""
            pc_hash = f"{pc} / {frame_hash}".strip(" /")
            lines.append(
                "| "
                + " | ".join(
                    [
                        escape(row.get("title", "")),
                        escape(effective_status(row)),
                        escape(triage_bucket(row)),
                        escape(pc_hash),
                        escape(row.get("notes") or row.get("failureReason", "")),
                        escape(row.get("screenshot", "")),
                        "`" + escape(reproduction_for(row)) + "`",
                    ]
                )
                + " |"
            )
        lines.append("")

    for status in STATUS_ORDER:
        selected = [row for row in ledger if effective_status(row) == status]
        lines.append(f"## {status} ({len(selected)})")
        lines.append("")
        if not selected:
            lines.append("None.")
            lines.append("")
            continue
        lines.append("| Title | Canonical disc | BIOS | Disc hash | Frame | Review | Screenshot |")
        lines.append("|---|---|---|---|---:|---|---|")
        for row in sorted(selected, key=lambda item: (item.get("title") or "").casefold()):
            lines.append(
                "| "
                + " | ".join(
                    [
                        escape(row.get("title", "")),
                        escape(row.get("file", "")),
                        escape(row.get("bios", "") or row.get("biosSelection", "")),
                        short_hash(row.get("discHash", "")),
                        escape(row.get("frame", "")),
                        escape(row.get("review", "")),
                        escape(row.get("screenshot", "")),
                    ]
                )
                + " |"
            )
        lines.append("")
        notes = [row for row in selected if (row.get("failureReason") or row.get("notes"))]
        if status != "Playable" and notes:
            lines.append("| Title | Reason |")
            lines.append("|---|---|")
            for row in sorted(notes, key=lambda item: (item.get("title") or "").casefold()):
                lines.append(
                    f"| {escape(row.get('title', ''))} | "
                    f"{escape(row.get('failureReason') or row.get('notes', ''))} |"
                )
            lines.append("")

    lines.append("## Review queue")
    lines.append("")
    if not review:
        lines.append("None.")
    else:
        lines.append("| Title | Status | Signals |")
        lines.append("|---|---|---|")
        for row in sorted(review, key=lambda item: (item.get("title") or "").casefold()):
            lines.append(
                f"| {escape(row.get('title', ''))} | {escape(effective_status(row))} | "
                f"{escape(row.get('signals', ''))} |"
            )
    lines.append("")
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ledger", default=DEFAULT_LEDGER)
    parser.add_argument("--comparison", default=DEFAULT_COMPARISON)
    parser.add_argument("--out", default=DEFAULT_OUT)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()

    ledger = load_ledger(Path(args.ledger))
    comparison = json.loads(Path(args.comparison).read_text(encoding="utf-8"))
    text = render(ledger, comparison)
    output = Path(args.out)
    if args.check:
        if not output.exists() or output.read_text(encoding="utf-8") != text:
            print(f"stale evidence page: {output}", file=__import__("sys").stderr)
            return 1
        print("beyond-title evidence page is current")
        return 0
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(text, encoding="utf-8")
    counts = Counter(effective_status(row) for row in ledger)
    print(
        f"Wrote {output}: {len(ledger)} rows, "
        + ", ".join(f"{status} {counts.get(status, 0)}" for status in STATUS_ORDER)
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
