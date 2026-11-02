#!/usr/bin/env python3
"""Build the JSON data used by the 5DO compatibility browser.

The committed Markdown tables remain the source of truth:

* ``docs/GameCompatibilityResults.md`` - per-disc automated sweep results.
* ``docs/GameCompatibilityList.md``    - 4DO reference titles and manual 5DO grades.

The script uses only the Python standard library so it can run in the docs CI
job without installing extra packages.  Run it from the repository root:

    python tools/generate-compat-site.py

Use ``--check`` to verify that the committed JSON is up to date.
"""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import re
import shutil
import subprocess
import sys
import unicodedata
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from pathlib import Path

STATUS_MAP = {
    "Loadable/Rendered": "rendered",
    "Loadable/No video": "no-video",
    "Not Loadable": "not-loadable",
}

STATUS_INFO = {
    "rendered": {
        "label": "Rendered",
        "short": "Boots & renders",
        "description": (
            "The automated boot test completed the requested frames and the final "
            "frame was non-black. This is not a playability grade."
        ),
    },
    "no-video": {
        "label": "No video",
        "short": "Boots, no video",
        "description": (
            "The automated boot test completed but the final frame was black or "
            "nearly blank. It needs visual review."
        ),
    },
    "not-loadable": {
        "label": "Not loadable",
        "short": "Not loadable",
        "description": (
            "The emulator failed, timed out, or did not complete the requested "
            "frames."
        ),
    },
}

STATUS_RANK = {"unknown": 0, "rendered": 1, "no-video": 2, "not-loadable": 3}
MANUAL_RANK = {
    "good": 4,
    "playable": 3,
    "unplayable": 2,
    "not loadable": 1,
    "no data": 0,
}

REGION_ORDER = [
    "USA", "Europe", "Japan", "Korea", "World", "Germany", "France",
    "Canada", "Brazil", "Australia", "Spain", "Italy", "Taiwan", "China",
    "Asia", "United Kingdom", "Netherlands", "Sweden", "Unknown",
]

REGION_ALIASES = {
    "usa": "USA", "us": "USA", "u.s.a.": "USA", "united states": "USA",
    "united states of america": "USA", "europe": "Europe", "eur": "Europe",
    "eu": "Europe", "japan": "Japan", "jpn": "Japan", "korea": "Korea",
    "kor": "Korea", "south korea": "Korea", "world": "World",
    "germany": "Germany", "ger": "Germany", "france": "France",
    "fra": "France", "canada": "Canada", "can": "Canada", "brazil": "Brazil",
    "bra": "Brazil", "australia": "Australia", "aus": "Australia",
    "spain": "Spain", "esp": "Spain", "italy": "Italy", "ita": "Italy",
    "taiwan": "Taiwan", "china": "China", "asia": "Asia",
    "uk": "United Kingdom", "united kingdom": "United Kingdom",
    "england": "United Kingdom", "netherlands": "Netherlands",
    "holland": "Netherlands", "sweden": "Sweden",
}

LANGUAGE_TOKENS = {
    "en", "ja", "jp", "fr", "de", "es", "it", "pt", "ru", "ko", "kr", "zh",
    "cn", "nl", "sv", "no", "nb", "da", "fi", "pl", "cs", "hu", "tr", "ar",
    "he", "hi", "th", "vi", "id", "ms", "uk", "ro", "el", "bg", "hr", "sk",
    "sl", "sr", "lt", "lv", "et", "ca", "gl", "eu", "ast",
}

TAG_TOKENS = {
    "alt", "unl", "beta", "demo", "sample", "promo", "prototype", "ntsc",
    "pal", "secam", "re", "rev", "revision", "ver", "version", "arcade",
    "cd", "cdrom", "rom", "disc", "disk", "side", "a", "b", "1", "2", "3",
    "4", "of", "and", "the",
}

CATEGORY_ORDER = [
    "Games", "Action", "Adventure", "Racing", "Sports", "Fighting",
    "Shooter", "Strategy", "Puzzle", "Simulation", "Multimedia",
    "Educational", "Demo", "Adult", "Arcade", "Miscellaneous", "Other",
]

PAREN_RE = re.compile(r"\(([^()]*)\)")
BRACKET_RE = re.compile(r"\[[^\[\]]*\]")
REVISION_RE = re.compile(
    r"(?i)(re|rev|revision|v|ver|version)[\s._-]*\d+([\s._-]*\d+)*"
)
REVISION_LETTER_RE = re.compile(r"(?i)(rev|revision)\s*[a-z]")
MEDIA_RE = re.compile(
    r"(?i)(arcade\s+cd-?rom|cd-?rom|cd|disc|disk|side)\s*[a-z0-9]*"
)
DISC_OF_RE = re.compile(r"(?i)\d+\s+of\s+\d+")
LAST_UPDATED_RE = re.compile(r"^\*\*Last updated:\*\*\s*(\d{4}-\d{2}-\d{2})", re.M)


@dataclass
class Reference:
    category: str
    game: str
    score: int | None
    status: str
    version: str
    notes: str
    discs: list[str] = field(default_factory=list)
    five_do_status: str = "No data"
    five_do_notes: str = ""


@dataclass
class Variant:
    disc: str
    root: str
    status: str
    bios: str | None
    bios2: str | None
    frames: int
    elapsed: float | None
    pc: int | str | None
    frame_hash: str
    non_black: int | None
    colors: int | None
    reason: str
    evidence: str
    regions: list[str]
    title: str
    sort_title: str
    category: str
    manual_grade: str | None
    manual_notes: str
    reference: dict | None
    screenshot: str | None = None

    def to_dict(self) -> dict:
        return {
            "disc": self.disc,
            "root": self.root,
            "status": self.status,
            "bios": self.bios,
            "bios2": self.bios2,
            "frames": self.frames,
            "elapsed": self.elapsed,
            "pc": self.pc,
            "frameHash": self.frame_hash,
            "nonBlack": self.non_black,
            "colors": self.colors,
            "reason": self.reason,
            "evidence": self.evidence,
            "regions": self.regions,
            "manualGrade": self.manual_grade,
            "manualNotes": self.manual_notes,
            "reference": self.reference,
            "screenshot": self.screenshot,
        }


def split_md_row(line: str) -> list[str] | None:
    """Split a Markdown table row, honouring escaped pipes."""
    text = line.strip()
    if not text.startswith("|"):
        return None
    text = text[1:]
    if text.endswith("|"):
        text = text[:-1]

    cells: list[str] = []
    buf: list[str] = []
    index = 0
    while index < len(text):
        char = text[index]
        if char == "\\" and index + 1 < len(text) and text[index + 1] == "|":
            buf.append("|")
            index += 2
            continue
        if char == "|":
            cells.append("".join(buf).strip())
            buf = []
            index += 1
            continue
        buf.append(char)
        index += 1
    cells.append("".join(buf).strip())
    return cells


def is_separator(cells: list[str]) -> bool:
    return bool(cells) and all(
        re.fullmatch(r":?-{3,}:?", cell.strip()) for cell in cells
    )


def parse_table(path: Path, columns: int, header: str) -> list[list[str]]:
    if not path.exists():
        raise SystemExit(f"error: file not found: {path}")
    rows: list[list[str]] = []
    for line in path.read_text(encoding="utf-8").splitlines():
        cells = split_md_row(line)
        if cells is None or len(cells) != columns or is_separator(cells):
            continue
        if cells[0] == header:
            continue
        rows.append(cells)
    return rows


def parse_int(value: str | None) -> int | None:
    if not value:
        return None
    cleaned = value.replace(",", "").replace(" ", "").strip()
    if not cleaned or not re.fullmatch(r"-?\d+", cleaned):
        return None
    return int(cleaned)


def parse_float(value: str | None) -> float | None:
    if not value:
        return None
    cleaned = value.replace(",", ".").replace(" ", "").strip()
    try:
        return float(cleaned)
    except ValueError:
        return None


def clean_dash(value: str | None) -> str:
    text = (value or "").strip()
    if text in {"", "-", "\u2014", "\u2013"}:
        return ""
    return text


def split_bios(value: str | None) -> tuple[str | None, str | None]:
    text = (value or "").strip()
    if not text:
        return None, None
    parts = [part.strip() for part in text.split(" + ") if part.strip()]
    if not parts:
        return None, None
    if len(parts) == 1:
        return parts[0], None
    return parts[0], " + ".join(parts[1:])


def normalize_region(token: str) -> str | None:
    text = token.strip().lower().rstrip(".")
    if not text:
        return None
    return REGION_ALIASES.get(text)


def extract_regions(disc: str) -> list[str]:
    regions: list[str] = []
    for group in PAREN_RE.findall(disc):
        for token in re.split(r"[,/&]", group):
            region = normalize_region(token)
            if region and region not in regions:
                regions.append(region)
    return regions


def is_language(token: str) -> bool:
    return token.strip().lower().rstrip(".") in LANGUAGE_TOKENS


def is_tag(token: str) -> bool:
    return token.strip().lower().rstrip(".") in TAG_TOKENS


def is_metadata_group(group: str) -> bool:
    tokens = [token.strip() for token in re.split(r"[,/&]", group) if token.strip()]
    if not tokens:
        return True
    if any(normalize_region(token) for token in tokens):
        return True
    if all(is_language(token) or is_tag(token) for token in tokens):
        return True
    stripped = group.strip()
    if REVISION_RE.fullmatch(stripped):
        return True
    if REVISION_LETTER_RE.fullmatch(stripped):
        return True
    if MEDIA_RE.fullmatch(stripped):
        return True
    if DISC_OF_RE.fullmatch(stripped):
        return True
    if re.fullmatch(r"(?i)(ntsc|pal|secam)(\s+version)?", stripped):
        return True
    return False


def clean_title(disc: str) -> str:
    title = re.sub(r"\.chd$", "", disc, flags=re.I)
    title = BRACKET_RE.sub("", title)

    def replace(match: re.Match[str]) -> str:
        return "" if is_metadata_group(match.group(1)) else match.group(0)

    title = PAREN_RE.sub(replace, title)
    title = re.sub(r"\s+", " ", title).strip(" -_,:;")
    return title or re.sub(r"\.chd$", "", disc, flags=re.I).strip()


def make_sort_title(title: str) -> str:
    text = unicodedata.normalize("NFKD", title)
    text = "".join(char for char in text if not unicodedata.combining(char))
    text = text.casefold()
    text = re.sub(r"&", " and ", text)
    text = re.sub(r"^(the|a|an)\s+", "", text)
    text = re.sub(r",\s*(the|a|an)$", "", text)
    text = re.sub(r"[^a-z0-9]+", " ", text)
    text = re.sub(r"\s+", " ", text).strip()
    return text or title.casefold()


def letter_for(sort_title: str) -> str:
    if sort_title and sort_title[0].isascii() and sort_title[0].isalpha():
        return sort_title[0].upper()
    return "#"


def slugify(value: str) -> str:
    text = unicodedata.normalize("NFKD", value)
    text = text.encode("ascii", "ignore").decode("ascii")
    text = re.sub(r"[^a-z0-9]+", "-", text.lower()).strip("-")
    return text


def screenshot_name(disc: str) -> str:
    base = re.sub(r"\.chd$", "", disc, flags=re.I)
    slug = slugify(base) or "disc"
    digest = hashlib.sha1(disc.casefold().encode("utf-8")).hexdigest()[:8]
    return f"{slug}-{digest}.png"


def region_sort_key(region: str) -> tuple[int, str]:
    try:
        return REGION_ORDER.index(region), ""
    except ValueError:
        return len(REGION_ORDER), region


def category_sort_key(category: str) -> tuple[int, str]:
    try:
        return CATEGORY_ORDER.index(category), ""
    except ValueError:
        return len(CATEGORY_ORDER), category


def manual_rank(value: str | None) -> int:
    if not value:
        return -1
    return MANUAL_RANK.get(value.strip().lower(), 0)


def reference_rank(reference: Reference) -> tuple[int, int]:
    score = reference.score if reference.score is not None else -1
    return manual_rank(reference.five_do_status), score


def choose_reference(references: list[Reference]) -> Reference | None:
    if not references:
        return None
    return max(references, key=reference_rank)


def reference_to_dict(reference: Reference | None) -> dict | None:
    if reference is None:
        return None
    return {
        "category": reference.category,
        "score": reference.score,
        "status": reference.status,
        "version": reference.version,
        "notes": reference.notes,
        "fiveDoStatus": reference.five_do_status,
        "fiveDoNotes": reference.five_do_notes,
    }


def display_path(path: Path, repo_root: Path) -> str:
    try:
        return path.resolve().relative_to(repo_root.resolve()).as_posix()
    except ValueError:
        return path.name


def read_last_updated(path: Path) -> str | None:
    if not path.exists():
        return None
    match = LAST_UPDATED_RE.search(path.read_text(encoding="utf-8"))
    return match.group(1) if match else None


def source_date(repo_root: Path, results_path: Path, override: str | None) -> str:
    if override:
        return override
    explicit = read_last_updated(results_path)
    if explicit:
        return explicit
    try:
        relative = results_path.resolve().relative_to(repo_root.resolve())
    except ValueError:
        relative = results_path
    try:
        output = subprocess.check_output(
            ["git", "-C", str(repo_root), "log", "-1", "--format=%cs", "--", str(relative)],
            text=True,
            stderr=subprocess.DEVNULL,
        ).strip()
        if re.fullmatch(r"\d{4}-\d{2}-\d{2}", output):
            return output
    except (OSError, subprocess.CalledProcessError):
        pass
    return dt.date.fromtimestamp(results_path.stat().st_mtime).isoformat()


def import_or_find_screenshot(
    variant: Variant,
    repo_root: Path,
    screenshots_dir: Path,
    url_prefix: str,
    copy: bool,
) -> tuple[str, str]:
    """Return (state, path) where state is existing/imported/missing."""
    name = screenshot_name(variant.disc)
    destination = screenshots_dir / name
    if destination.exists():
        variant.screenshot = f"{url_prefix}/{name}"
        return "existing", name

    source = Path(variant.evidence)
    if not source.is_absolute():
        source = repo_root / source
    if source.suffix.lower() != ".png":
        candidate = source.with_name("frame.png")
        if candidate.exists():
            source = candidate
    if not source.exists():
        candidate = source.with_name("frame.png")
        if candidate.exists():
            source = candidate

    if source.exists() and source.is_file():
        if copy:
            screenshots_dir.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, destination)
            variant.screenshot = f"{url_prefix}/{name}"
            return "imported", name
        return "missing", name

    return "missing", name


def build_data(args: argparse.Namespace) -> tuple[dict, dict[str, int]]:
    repo_root = Path(args.repo_root).resolve()
    results_path = Path(args.results)
    reference_path = Path(args.reference)
    screenshots_dir = Path(args.screenshots_dir)
    if not screenshots_dir.is_absolute():
        screenshots_dir = repo_root / screenshots_dir

    result_rows = parse_table(results_path, 12, "Status")
    reference_rows = parse_table(reference_path, 9, "Category")
    if not result_rows:
        raise SystemExit(f"error: no result rows found in {results_path}")

    references: list[Reference] = []
    for row in reference_rows:
        category = row[0].strip()
        if category == "Main list":
            category = "Games"
        discs = [
            disc.strip()
            for disc in re.split(r"<br\s*/?>", row[6], flags=re.I)
            if disc.strip() and disc.strip() not in {"-", "\u2014", "\u2013"}
        ]
        references.append(
            Reference(
                category=category,
                game=row[1].strip(),
                score=parse_int(row[2]),
                status=clean_dash(row[3]) or "No data",
                version=clean_dash(row[4]),
                notes=clean_dash(row[5]),
                discs=discs,
                five_do_status=clean_dash(row[7]) or "No data",
                five_do_notes=clean_dash(row[8]),
            )
        )

    disc_map: dict[str, list[Reference]] = defaultdict(list)
    for reference in references:
        for disc in reference.discs:
            disc_map[disc.casefold()].append(reference)

    variants: list[Variant] = []
    for row in result_rows:
        status_raw = row[0].strip()
        status = STATUS_MAP.get(status_raw) or slugify(status_raw) or "unknown"
        disc = row[1].strip()
        bios, bios2 = split_bios(row[3])
        references_for_disc = disc_map.get(disc.casefold(), [])
        reference = choose_reference(references_for_disc)
        title = reference.game if reference else clean_title(disc)
        sort_title = make_sort_title(title)
        category = reference.category if reference else "Other"
        manual_grade = None
        manual_notes = ""
        if reference and reference.five_do_status.strip().lower() != "no data":
            manual_grade = reference.five_do_status.strip()
            manual_notes = reference.five_do_notes

        variant = Variant(
            disc=disc,
            root=row[2].strip(),
            status=status,
            bios=bios,
            bios2=bios2,
            frames=parse_int(row[4]) or 0,
            elapsed=parse_float(row[5]),
            pc=parse_int(row[6]) if parse_int(row[6]) is not None else clean_dash(row[6]),
            frame_hash=row[7].strip(),
            non_black=parse_int(row[8]),
            colors=parse_int(row[9]),
            reason=row[10].strip(),
            evidence=row[11].strip().replace("\\", "/"),
            regions=extract_regions(disc) or ["Unknown"],
            title=title,
            sort_title=sort_title,
            category=category,
            manual_grade=manual_grade,
            manual_notes=manual_notes,
            reference=reference_to_dict(reference),
        )
        variants.append(variant)

    grouped: dict[str, list[Variant]] = defaultdict(list)
    for variant in variants:
        key = variant.sort_title or variant.title.casefold()
        grouped[key].append(variant)

    games: list[dict] = []
    used_ids: set[str] = set()
    for _key, group_variants in grouped.items():
        group_variants.sort(key=lambda item: item.disc.casefold())

        with_grade = [item for item in group_variants if item.manual_grade]
        with_reference = [item for item in group_variants if item.reference]
        display_variant = (
            max(with_grade, key=lambda item: manual_rank(item.manual_grade))
            if with_grade
            else (with_reference[0] if with_reference else group_variants[0])
        )
        display_title = display_variant.title.strip() or clean_title(display_variant.disc)
        sort_title = make_sort_title(display_title)

        best_manual = (
            max(with_grade, key=lambda item: manual_rank(item.manual_grade))
            if with_grade
            else None
        )
        best_reference = (
            best_manual
            if best_manual is not None
            else (
                max(
                    with_reference,
                    key=lambda item: (
                        item.reference.get("score")
                        if item.reference and item.reference.get("score") is not None
                        else -1
                    ),
                )
                if with_reference
                else None
            )
        )

        category_counts = Counter(
            item.category
            for item in group_variants
            if item.category and item.category != "Other"
        )
        if category_counts:
            category = sorted(
                category_counts.items(),
                key=lambda pair: (-pair[1], category_sort_key(pair[0])),
            )[0][0]
        else:
            category = "Other"

        regions: list[str] = []
        for item in group_variants:
            for region in item.regions:
                if region not in regions:
                    regions.append(region)
        regions.sort(key=region_sort_key)

        status_counts = Counter(item.status for item in group_variants)
        worst_status = max(status_counts, key=lambda value: STATUS_RANK.get(value, 0))

        base_id = slugify(display_title)
        if not base_id:
            base_id = "game-" + hashlib.sha1(
                display_title.casefold().encode("utf-8")
            ).hexdigest()[:8]
        game_id = base_id
        suffix = 2
        while game_id in used_ids:
            game_id = f"{base_id}-{suffix}"
            suffix += 1
        used_ids.add(game_id)

        games.append(
            {
                "id": game_id,
                "title": display_title,
                "sortTitle": sort_title,
                "letter": letter_for(sort_title),
                "category": category,
                "regions": regions or ["Unknown"],
                "status": worst_status,
                "statusCounts": dict(status_counts),
                "manualGrade": best_manual.manual_grade if best_manual else None,
                "manualNotes": best_manual.manual_notes if best_manual else "",
                "reference": best_reference.reference if best_reference else None,
                "testDepth": max(item.frames for item in group_variants),
                "hasBiosOverride": any(item.bios or item.bios2 for item in group_variants),
                "extended": any(item.frames > 3000 for item in group_variants),
                "screenshot": None,
                "variants": [item.to_dict() for item in group_variants],
            }
        )

    games.sort(key=lambda game: (game["sortTitle"], game["title"].casefold()))

    screenshot_stats: Counter[str] = Counter()
    for variant in variants:
        state, _ = import_or_find_screenshot(
            variant,
            repo_root=repo_root,
            screenshots_dir=screenshots_dir,
            url_prefix=args.screenshot_url_prefix,
            copy=args.import_screenshots,
        )
        screenshot_stats[state] += 1

    variant_lookup = {item.disc.casefold(): item for item in variants}
    for game in games:
        for variant_dict in game["variants"]:
            source = variant_lookup.get(variant_dict["disc"].casefold())
            if source is not None:
                variant_dict["screenshot"] = source.screenshot
        game["screenshot"] = next(
            (
                variant["screenshot"]
                for variant in game["variants"]
                if variant["screenshot"] and variant["status"] == "rendered"
            ),
            None,
        )

    totals = {
        "discs": len(variants),
        "games": len(games),
        "rendered": sum(1 for item in variants if item.status == "rendered"),
        "noVideo": sum(1 for item in variants if item.status == "no-video"),
        "notLoadable": sum(1 for item in variants if item.status == "not-loadable"),
        "manualGrades": sum(1 for game in games if game["manualGrade"]),
        "biosOverrides": sum(1 for item in variants if item.bios or item.bios2),
        "extendedRuns": sum(1 for item in variants if item.frames > 3000),
        "screenshots": screenshot_stats["existing"] + screenshot_stats["imported"],
        "screenshotsMissing": screenshot_stats["missing"],
    }

    data = {
        "schema": 1,
        "sourceUpdated": source_date(repo_root, results_path, args.source_updated),
        "source": {
            "results": display_path(results_path, repo_root),
            "reference": display_path(reference_path, repo_root),
        },
        "totals": totals,
        "statuses": STATUS_INFO,
        "games": games,
    }
    return data, {
        "resultRows": len(result_rows),
        "referenceRows": len(reference_rows),
        "variants": len(variants),
        "games": len(games),
        "screenshotExisting": screenshot_stats["existing"],
        "screenshotImported": screenshot_stats["imported"],
        "screenshotMissing": screenshot_stats["missing"],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--repo-root",
        default=".",
        help="Repository root used to resolve evidence paths (default: .)",
    )
    parser.add_argument(
        "--results",
        default="docs/GameCompatibilityResults.md",
        help="Curated sweep results Markdown",
    )
    parser.add_argument(
        "--reference",
        default="docs/GameCompatibilityList.md",
        help="4DO reference and manual-grade Markdown",
    )
    parser.add_argument(
        "--out",
        default="docs/data/compatibility.json",
        help="Output JSON path",
    )
    parser.add_argument(
        "--screenshots-dir",
        default="docs/assets/compat",
        help="Directory containing published screenshots",
    )
    parser.add_argument(
        "--screenshot-url-prefix",
        default="assets/compat",
        help="URL prefix written into the JSON for screenshots",
    )
    parser.add_argument(
        "--import-screenshots",
        action="store_true",
        help="Copy evidence PNGs from the sweep artifacts into the screenshots directory",
    )
    parser.add_argument(
        "--check",
        action="store_true",
        help="Fail if the output file is missing or out of date",
    )
    parser.add_argument(
        "--source-updated",
        help="Override the results date (YYYY-MM-DD)",
    )
    parser.add_argument(
        "--quiet",
        action="store_true",
        help="Only print warnings and errors",
    )
    args = parser.parse_args()

    repo_root = Path(args.repo_root).resolve()
    results_path = Path(args.results)
    reference_path = Path(args.reference)
    if not results_path.is_absolute():
        results_path = repo_root / results_path
    if not reference_path.is_absolute():
        reference_path = repo_root / reference_path
    args.results = str(results_path)
    args.reference = str(reference_path)

    data, stats = build_data(args)
    text = json.dumps(data, indent=2, ensure_ascii=False) + "\n"

    out_path = Path(args.out)
    if not out_path.is_absolute():
        out_path = repo_root / out_path

    if args.check:
        if not out_path.exists():
            print(f"error: {out_path} is missing; run tools/generate-compat-site.py")
            return 1
        current = out_path.read_text(encoding="utf-8")
        if current != text:
            print(
                f"error: {out_path} is out of date; run tools/generate-compat-site.py"
            )
            return 1
        if not args.quiet:
            print(
                "Compatibility data is up to date "
                f"({stats['games']} games, {stats['variants']} discs)."
            )
        return 0

    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(text, encoding="utf-8", newline="\n")
    if not args.quiet:
        totals = data["totals"]
        print(
            f"Parsed {stats['resultRows']} result rows and "
            f"{stats['referenceRows']} reference rows."
        )
        print(
            f"Generated {totals['games']} games from {totals['discs']} discs "
            f"({totals['rendered']} rendered, {totals['noVideo']} no video, "
            f"{totals['notLoadable']} not loadable)."
        )
        print(
            f"Manual grades: {totals['manualGrades']} games; "
            f"BIOS overrides: {totals['biosOverrides']} discs."
        )
        print(
            f"Screenshots: {stats['screenshotExisting']} existing, "
            f"{stats['screenshotImported']} imported, "
            f"{stats['screenshotMissing']} missing."
        )
        print(f"Wrote {out_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
