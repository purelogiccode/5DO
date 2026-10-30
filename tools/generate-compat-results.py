#!/usr/bin/env python3
"""Turn a compat-sweep summary.csv into a readable Markdown results document."""

from __future__ import annotations

import argparse
import csv
from collections import Counter
from pathlib import Path


DEFAULT_RESULTS = "artifacts/compat-full/summary.csv"
DEFAULT_OUT = "docs/GameCompatibilityResults.md"

STATUS_ORDER = {
    "Not Loadable": 0,
    "Loadable/No video": 1,
    "Loadable/Rendered": 2,
}


def read_rows(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        return list(csv.DictReader(stream))


def escape(value: str | None) -> str:
    if value is None:
        return ""
    return str(value).replace("|", "\\|").replace("\n", " ").strip()


def relative_evidence(value: str | None) -> str:
    if not value:
        return ""
    path = Path(value)
    try:
        return str(path.resolve().relative_to(Path.cwd().resolve())).replace("\\", "/")
    except ValueError:
        return str(path)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--results", default=DEFAULT_RESULTS, help="Sweep summary.csv")
    parser.add_argument("--out", default=DEFAULT_OUT, help="Markdown output")
    args = parser.parse_args()

    results_path = Path(args.results)
    if not results_path.exists():
        parser.error(f"Results file not found: {results_path}")

    rows = read_rows(results_path)
    if not rows:
        parser.error(f"Results file is empty: {results_path}")

    counts = Counter(row.get("Status", "Unknown") for row in rows)
    rows.sort(
        key=lambda row: (
            STATUS_ORDER.get(row.get("Status", ""), 99),
            row.get("File", "").lower(),
        )
    )

    lines: list[str] = []
    lines.append("# Game Compatibility Results")
    lines.append("")
    lines.append(
        "Automated output of `tools/compat-sweep.ps1`. These are **loadability** "
        "results, not final compatibility grades: `Loadable/Rendered` still needs a "
        "visual and input review before it becomes `Playable` or `Good`."
    )
    lines.append("")
    lines.append("## Summary")
    lines.append("")
    lines.append(f"- Discs tested: **{len(rows)}**")
    for status in ("Loadable/Rendered", "Loadable/No video", "Not Loadable"):
        lines.append(f"- {status}: **{counts.get(status, 0)}**")
    other = {key: value for key, value in counts.items() if key not in STATUS_ORDER}
    for status, count in sorted(other.items()):
        lines.append(f"- {status}: **{count}**")
    lines.append("")
    lines.append(
        "Status definitions and triage guidance are in "
        "[`CompatibilitySweep.md`](CompatibilitySweep.md). The 4DO reference list is "
        "in [`GameCompatibilityList.md`](GameCompatibilityList.md)."
    )
    lines.append("")
    lines.append("## Results")
    lines.append("")
    lines.append(
        "| Status | Disc | Root | Frames | Elapsed (s) | PC | Frame hash | "
        "Non-black | Colors | Reason | Evidence |"
    )
    lines.append("|---|---|---|---:|---:|---|---|---:|---:|---|---|")
    for row in rows:
        evidence = relative_evidence(row.get("PngPath") or row.get("JsonPath"))
        lines.append(
            "| "
            + " | ".join(
                [
                    escape(row.get("Status")),
                    escape(row.get("File")),
                    escape(row.get("Root")),
                    escape(row.get("Frames")),
                    escape(row.get("ElapsedSeconds", "").replace(",", ".")),
                    escape(row.get("Pc")),
                    escape(row.get("FrameHash")),
                    escape(row.get("NonBlackPixels")),
                    escape(row.get("DistinctColors")),
                    escape(row.get("Reason")),
                    escape(evidence),
                ]
            )
            + " |"
        )

    lines.append("")
    lines.append("## Next step")
    lines.append("")
    lines.append(
        "For each `Loadable/Rendered` row, inspect the PNG and run a short input smoke "
        "test. Record the final `Unplayable`/`Playable`/`Good` status in "
        "[`GameCompatibilityList.md`](GameCompatibilityList.md). For "
        "`Loadable/No video`, rerun at 12,000 frames and inspect intermediate captures."
    )
    lines.append("")

    output = Path(args.out)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text("\n".join(lines), encoding="utf-8")
    print(f"Wrote {output} ({len(rows)} rows)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
