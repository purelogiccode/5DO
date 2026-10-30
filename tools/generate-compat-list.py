#!/usr/bin/env python3
"""Generate docs/GameCompatibilityList.md from the saved 4DO compatibility wiki page.

The 4DO wiki page is an MHTML snapshot. This script extracts its two game tables,
matches the titles against the local CHD/ISO collection, and writes a Markdown
reference table with empty 5DO result columns for the compatibility sweep.

Only the Python standard library is required.
"""

from __future__ import annotations

import argparse
import difflib
import email
import glob
import os
import re
from dataclasses import dataclass, field
from email import policy
from pathlib import Path


DEFAULT_MHTML_GLOB = "References/4DO-master/*Compatibility List*.mhtml"
DEFAULT_ROOTS = (r"J:\Panasonic 3DO", r"G:\MAME\MAME Software List CHDs\3do")
DEFAULT_OUT = "docs/GameCompatibilityList.md"
DISC_EXTENSIONS = (".chd", ".iso", ".bin", ".cue")


@dataclass
class GameRecord:
    category: str
    name: str
    score: str
    version: str
    notes: str
    local_files: list[str] = field(default_factory=list)


def strip_tags(value: str) -> str:
    value = re.sub(r"<.*?>", "", value, flags=re.S)
    value = value.replace("&nbsp;", " ").replace("&amp;", "&")
    return " ".join(value.split())


def table_rows(table_html: str) -> list[list[str]]:
    rows: list[list[str]] = []
    for row_html in re.findall(r"<tr.*?</tr>", table_html, flags=re.S):
        cells = [
            strip_tags(cell)
            for cell in re.findall(r"<t[dh].*?</t[dh]>", row_html, flags=re.S)
        ]
        if cells:
            rows.append(cells)
    return rows


def load_mhtml(path: Path) -> str:
    message = email.message_from_file(
        path.open("r", encoding="utf-8", errors="replace"),
        policy=policy.default,
    )
    for part in message.walk():
        if part.get_content_type() == "text/html":
            return part.get_content()
    raise RuntimeError(f"No text/html part found in {path}")


def parse_records(html: str) -> list[GameRecord]:
    tables = re.findall(r"<table.*?</table>", html, flags=re.S)
    if len(tables) < 5:
        raise RuntimeError(f"Expected at least 5 tables, found {len(tables)}")

    records: list[GameRecord] = []
    for row in table_rows(tables[2])[1:]:
        if len(row) >= 4 and row[0]:
            records.append(
                GameRecord(
                    category="Main list",
                    name=row[0],
                    score=row[1],
                    version=row[2],
                    notes=row[3],
                )
            )

    for row in table_rows(tables[4])[1:]:
        if len(row) >= 5 and row[1]:
            records.append(
                GameRecord(
                    category=row[0] or "Uncategorised",
                    name=row[1],
                    score=row[2],
                    version=row[3],
                    notes=row[4],
                )
            )

    return records


def normalize_title(value: str) -> str:
    value = value.lower()
    value = re.sub(r"\([^)]*\)", " ", value)
    value = re.sub(r"\[[^\]]*\]", " ", value)
    value = re.sub(r"\bdisc\s+[a-z0-9]+\b", " ", value)
    value = re.sub(r"\brev\s+[a-z0-9]+\b", " ", value)
    value = re.sub(r"\bstarring\b.*$", " ", value)
    value = value.replace("&", " and ")
    value = re.sub(r"[^a-z0-9]+", " ", value)
    value = re.sub(r"\b(the|a|an)\b", " ", value)
    return " ".join(value.split())


def compact_title(value: str) -> str:
    return normalize_title(value).replace(" ", "")


def match_candidates(title: str) -> list[str]:
    """Return normalised candidates, most specific first."""
    base = normalize_title(title)
    candidates = [base]

    # "Need for Speed, The" -> "need for speed"
    if title.lower().endswith(", the"):
        candidates.append(normalize_title(title[:-5]))

    # "Out of this World / Another World" -> either title.
    if "/" in title:
        candidates.extend(normalize_title(part) for part in title.split("/"))

    # Drop a subtitle after " - " or ":" as a fallback.
    for separator in (" - ", ":"):
        if separator in title:
            candidates.append(normalize_title(title.split(separator, 1)[0]))

    # Remove duplicate/empty candidates while preserving order.
    result: list[str] = []
    for candidate in candidates:
        if candidate and candidate not in result:
            result.append(candidate)
    return result


def scan_discs(roots: list[str]) -> list[Path]:
    discs: list[Path] = []
    for root in roots:
        root_path = Path(root)
        if not root_path.exists():
            continue
        for directory, _, files in os.walk(root_path):
            for name in files:
                if name.lower().endswith(DISC_EXTENSIONS):
                    discs.append(Path(directory) / name)
    return sorted(discs, key=lambda path: (path.name.lower(), str(path).lower()))


def fuzzy_compatible(candidate: str, key: str) -> bool:
    """Reject fuzzy matches that drop a sequel/edition marker."""
    markers = {"2", "3", "4", "ii", "iii", "iv", "alpha", "demo", "sampler", "magazine"}
    candidate_markers = set(candidate.split()) & markers
    key_markers = set(key.split()) & markers
    return candidate_markers == key_markers


def match_discs(records: list[GameRecord], discs: list[Path]) -> set[Path]:
    by_normalized: dict[str, list[Path]] = {}
    by_compact: dict[str, list[Path]] = {}
    for disc in discs:
        normalized = normalize_title(disc.stem)
        by_normalized.setdefault(normalized, []).append(disc)
        by_compact.setdefault(compact_title(disc.stem), []).append(disc)

    matched: set[Path] = set()
    for record in records:
        exact: list[Path] = []
        for candidate in match_candidates(record.name):
            exact.extend(by_normalized.get(candidate, []))
        if not exact:
            for candidate in match_candidates(record.name):
                exact.extend(by_compact.get(candidate.replace(" ", ""), []))
        if not exact:
            for candidate in match_candidates(record.name):
                if len(candidate) < 6:
                    continue
                for key, paths in by_normalized.items():
                    if candidate in key:
                        exact.extend(paths)
        if not exact:
            for candidate in match_candidates(record.name):
                close = difflib.get_close_matches(
                    candidate,
                    by_normalized.keys(),
                    n=3,
                    cutoff=0.85,
                )
                for key in close:
                    if fuzzy_compatible(candidate, key):
                        exact.extend(by_normalized[key])
        unique = sorted(set(exact), key=lambda path: (path.name.lower(), str(path).lower()))
        record.local_files = [path.name for path in unique]
        matched.update(unique)

    return matched


def status_for_score(score: str) -> str:
    score = score.strip()
    if not score:
        return "No data"
    try:
        value = int(score)
    except ValueError:
        return "No data"
    if value == 0:
        return "Not Loadable"
    if value < 50:
        return "Unplayable"
    if value < 80:
        return "Playable"
    return "Good"


def escape_cell(value: str) -> str:
    return value.replace("|", "\\|").replace("\n", " ").strip()


def write_markdown(
    output: Path,
    records: list[GameRecord],
    discs: list[Path],
    matched: set[Path],
    roots: list[str],
) -> None:
    records.sort(key=lambda record: (record.category.lower(), record.name.lower()))
    unmatched = [disc for disc in discs if disc not in matched]

    lines: list[str] = []
    lines.append("# Game Compatibility List")
    lines.append("")
    lines.append(
        "This is the working compatibility matrix for the full local 3DO collection. "
        "The 4DO scores and notes are transcribed from the saved 4DO wiki page; the "
        "5DO columns are filled in by the collection sweep."
    )
    lines.append("")
    lines.append("## Sources")
    lines.append("")
    lines.append(
        "- 4DO compatibility wiki snapshot: "
        "`References/4DO-master/4DO Compatibility List ⚈ 3DO emulator Wiki ».mhtml` "
        "(saved 2026-10-25). `References/` is local-only and ignored by git; this "
        "Markdown file is the committed reference copy."
    )
    lines.append(
        "- Local collection roots: "
        + ", ".join(f"`{root}`" for root in roots)
        + "."
    )
    lines.append(
        "- 5DO sweep tool: `tools/compat-sweep.ps1`; results are written under "
        "`artifacts/compat/` and can be merged into the 5DO columns below. The latest "
        "automated pass is [`GameCompatibilityResults.md`](GameCompatibilityResults.md)."
    )
    lines.append("")
    lines.append("## 4DO status scale")
    lines.append("")
    lines.append("| Status | 4DO score | Meaning |")
    lines.append("|---|---:|---|")
    lines.append("| No data | (none) | No assessment recorded. |")
    lines.append("| Not Loadable | 0 | The game cannot load or does not start. |")
    lines.append("| Unplayable | 1-49 | Starts, but freezes or has severe graphical/functional problems. |")
    lines.append("| Playable | 50-79 | Playable, but has significant problems or cannot be completed. |")
    lines.append("| Good | 80-100 | Completes and runs without significant problems. |")
    lines.append("")
    lines.append("## Automated sweep labels")
    lines.append("")
    lines.append(
        "The automated loadability pass uses three intermediate labels before a human "
        "assigns the final five-level status:"
    )
    lines.append("")
    lines.append("| Automated label | Meaning | Usual final status |")
    lines.append("|---|---|---|")
    lines.append(
        "| Loadable/Rendered | Completed the frame count and the final frame is "
        "non-black. | Playable or Good after visual/input review |"
    )
    lines.append(
        "| Loadable/No video | Completed the frame count but the final frame is "
        "black/blank. | Unplayable or Playable after rerun/inspection |"
    )
    lines.append(
        "| Not Loadable | CLI error, timeout, invalid JSON, or short frame count. | "
        "Not Loadable unless the failure is a harness issue |"
    )
    lines.append("")
    lines.append("## Summary")
    lines.append("")
    lines.append(f"- 4DO reference titles: **{len(records)}**")
    lines.append(f"- Local disc images found: **{len(discs)}**")
    for root in roots:
        root_count = sum(
            1 for disc in discs
            if str(disc).lower().startswith(str(Path(root)).lower())
        )
        lines.append(f"  - `{root}`: **{root_count}**")
    lines.append(f"- 4DO titles with a local match: **{sum(1 for r in records if r.local_files)}**")
    lines.append(f"- Local images without a 4DO reference entry: **{len(unmatched)}**")
    lines.append("")
    lines.append("## 4DO reference and 5DO results")
    lines.append("")
    lines.append(
        "`Local disc(s)` is a filename match, not proof that the image is the same "
        "revision. `5DO status` starts as `No data`; use the same scale as 4DO."
    )
    lines.append("")
    lines.append(
        "| Category | Game | 4DO score | 4DO status | 4DO version | 4DO notes | Local disc(s) | 5DO status | 5DO notes |"
    )
    lines.append("|---|---|---:|---|---|---|---|---|---|")
    for record in records:
        local = "<br>".join(escape_cell(name) for name in record.local_files[:3])
        if len(record.local_files) > 3:
            local += f"<br>+{len(record.local_files) - 3} more"
        lines.append(
            "| "
            + " | ".join(
                [
                    escape_cell(record.category),
                    escape_cell(record.name),
                    escape_cell(record.score) or "—",
                    status_for_score(record.score),
                    escape_cell(record.version) or "—",
                    escape_cell(record.notes) or "—",
                    local or "—",
                    "No data",
                    "Not tested yet",
                ]
            )
            + " |"
        )

    lines.append("")
    lines.append("## Local images without a 4DO reference entry")
    lines.append("")
    if not unmatched:
        lines.append("None.")
    else:
        lines.append(
            "These images still belong in the loadability sweep. Add a 4DO/5DO row "
            "when the title is identified."
        )
        lines.append("")
        lines.append("| Disc file | Root | 5DO status | 5DO notes |")
        lines.append("|---|---|---|---|")
        for disc in unmatched:
            root = next((root for root in roots if str(disc).lower().startswith(str(Path(root)).lower())), "")
            lines.append(
                "| "
                + " | ".join(
                    [
                        escape_cell(disc.name),
                        escape_cell(root) or "—",
                        "No data",
                        "Not tested yet",
                    ]
                )
                + " |"
            )

    lines.append("")
    lines.append("## Sweep workflow")
    lines.append("")
    lines.append("1. Build the CLI: `dotnet build src/FiveDo.Cli -c Release`.")
    lines.append(
        "2. Run the collection sweep: "
        "`pwsh -File tools/compat-sweep.ps1 -Frames 3000`."
    )
    lines.append(
        "3. Inspect `artifacts/compat/summary.csv` and the per-game JSON/PNG evidence."
    )
    lines.append(
        "4. Update the `5DO status` and `5DO notes` columns for every tested title."
    )
    lines.append(
        "5. Re-run a title with `--high-res` and record the result separately when the "
        "high-resolution CEL path is being evaluated."
    )
    lines.append("")

    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text("\n".join(lines), encoding="utf-8")
    print(
        f"Wrote {output} ({len(records)} 4DO titles, {len(discs)} local discs, "
        f"{len(unmatched)} unmatched local discs)"
    )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--mhtml",
        default=None,
        help="Path to the saved 4DO compatibility MHTML file.",
    )
    parser.add_argument(
        "--root",
        action="append",
        default=None,
        help="Collection root. Repeat for multiple roots.",
    )
    parser.add_argument("--out", default=DEFAULT_OUT, help="Markdown output path.")
    args = parser.parse_args()

    mhtml = Path(args.mhtml) if args.mhtml else None
    if mhtml is None:
        matches = sorted(glob.glob(DEFAULT_MHTML_GLOB))
        if not matches:
            parser.error(f"No MHTML file matched {DEFAULT_MHTML_GLOB}")
        mhtml = Path(matches[0])

    roots = args.root or list(DEFAULT_ROOTS)
    records = parse_records(load_mhtml(mhtml))
    discs = scan_discs(roots)
    matched = match_discs(records, discs)
    write_markdown(Path(args.out), records, discs, matched, roots)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
