#!/usr/bin/env python3
"""Build a 5DO / 4DO / Phoenix compatibility comparison from local evidence.

Inputs:

* a disc inventory produced by ``tools/corpus-inventory.py`` (game ID + metadata);
* the saved 4DO compatibility wiki MHTML;
* the saved Phoenix (Arts-Union) compatibility MHTML;
* the 5DO sweep CSV (``artifacts/compat-full/summary-merged.csv``);
* the manual 5DO grades in ``docs/GameCompatibilityList.md``.

Outputs:

* ``docs/CompatibilityComparison.md`` - the committed 3-way matrix and summary;
* ``docs/data/unique-corpus.csv`` - one row per unique title, with duplicates
  collapsed and a canonical disc chosen for testing;
* ``docs/data/compatibility-comparison.json`` - machine-readable copy.

The script is deliberately conservative: a 5DO row that only says
``Loadable/Rendered`` is compared as *loadability*, not as gameplay parity.
Only a manual ``Good``/``Playable``/``Unplayable`` grade is compared directly
with the other emulators' playability scores.

Only the Python standard library is required.
"""

from __future__ import annotations

import argparse
import csv
import difflib
import email
import html as html_module
import io
import json
import re
import sys
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from email import policy
from pathlib import Path

DEFAULT_INVENTORY = "artifacts/corpus/inventory.json"
DEFAULT_SWEEP = "artifacts/compat-full/summary-merged.csv"
DEFAULT_REFERENCE = "docs/GameCompatibilityList.md"
DEFAULT_GRADES = "docs/data/playability-grades.csv"
DEFAULT_BEYOND = "docs/data/beyond-title-results.csv"
DEFAULT_FOURDO = "References/4DO-master/*Compatibility List*.mhtml"
DEFAULT_PHOENIX = "References/Phoenix/*Compatibility List*.mhtml"
DEFAULT_OUT = "docs/CompatibilityComparison.md"
DEFAULT_UNIQUE = "docs/data/unique-corpus.csv"
DEFAULT_JSON = "docs/data/compatibility-comparison.json"

DISC_EXTENSIONS = (".chd", ".iso", ".cue")

# Peer titles that are the same game under a different regional name.
# Keys and values are already normalised by ``normalize_title``.
TITLE_ALIASES = {
    "mad dog mccree 2 lost gold": "mad dog ii lost gold",
    "road and track presents need for speed": "need for speed",
    "road and track presents overdrivin": "overdrivin",
    "perfect world combat in a digital world": "immercenary combat in a digital world",
    "immercenary": "immercenary combat in a digital world",
    "john madden nfl football": "john madden football",
    "nfl madden football": "john madden football",
    "iron angel of the apocalypse tetsujin": "tetsujin",
    "iron angel of the apocalypse the return": "tetsujin returns",
    "great soccer kid": "soccer kid",
    "puzzle bobble": "bust a move",
    "super street fighter ii x grand master challenge": "super street fighter ii turbo",
    "world cup special": "striker world cup special",
    "totsugeki karakuri megadas": "totsugeki kikan megadasu",
    "uchuu seifutsu furopon kun": "uchuu seibutsu flopon kun",
    "furopon world": "flopon world",
    "flopon world": "uchuu seibutsu flopon kun",
    "shadow warriors": "space hulk vengeance of the blood angels",
    "blood angels space hulk": "space hulk vengeance of the blood angels",
    "tetsujin iron angel of the apocalypse": "tetsujin",
    "the need for speed": "need for speed",
}

# Local filenames that are the same game under a regional title.
# Applied after ``normalize_title`` so variants collapse into one test-set row.
LOCAL_TITLE_ALIASES = {
    "blood angels": "blood angels space hulk",
    "space hulk vengeance of blood angels": "blood angels space hulk",
    "tetsujin": "iron angel of apocalypse",
    "tetsujin iron angel of apocalypse": "iron angel of apocalypse",
    "out of this world": "another world",
    "great soccer kid": "soccer kid",
    "puzzle bobble": "bust a move",
    "super street fighter ii x grand master challenge": "super street fighter ii turbo",
    "john madden football": "john madden nfl football",
    "nfl madden football": "john madden nfl football",
    "perfect world combat in a digital world": "immercenary combat in a digital world",
    "immercenary": "immercenary combat in a digital world",
    "mad dog mccree 2 lost gold": "mad dog ii lost gold",
    "road and track presents need for speed": "need for speed",
    "the need for speed": "need for speed",
    "striker world cup special": "world cup special",
    "totsugeki karakuri megadas": "totsugeki kikan megadasu",
}

REGION_SCORE = {
    "usa": 30,
    "us": 30,
    "usa, europe": 30,
    "usa, korea": 28,
    "usa, canada": 28,
    "europe": 25,
    "eu": 25,
    "world": 25,
    "japan": 20,
    "jp": 20,
    "korea": 16,
    "kr": 16,
    "germany": 10,
    "de": 10,
    "france": 10,
    "fr": 10,
    "canada": 20,
    "ca": 20,
    "uk": 20,
    "gb": 20,
    "australia": 15,
    "au": 15,
    "japan, europe": 18,
    "europe, japan": 18,
}


def region_score(regions: str) -> int:
    tokens = [
        token
        for token in re.split(r"[,/&+\s-]+", (regions or "").lower())
        if token
    ]
    if not tokens:
        return 0
    score = max(REGION_SCORE.get(token, 0) for token in tokens)
    if len(set(tokens)) > 1:
        score += 2
    return score

EDITIONS = (
    "demo",
    "beta",
    "proto",
    "prototype",
    "alpha",
    "sample",
    "sampler",
    "preview",
    "promo",
    "kiosk",
)
NON_GAME_WORDS = ("magazine", "sampler", "storage manager", "game guru", "gamepack")


@dataclass
class Reference:
    source: str
    name: str
    category: str = ""
    score: int | None = None
    status: str = ""
    version: str = ""
    notes: str = ""
    percent: int | None = None

    @property
    def key(self) -> str:
        return normalize_title(self.name)


@dataclass
class Disc:
    path: str
    file: str
    root: str
    game_id: str
    name: str
    regions: str
    revision: str
    disc_number: int | None
    track_count: int | None
    audio_cd: bool
    known: bool
    error: str
    size: int


@dataclass
class UniqueTitle:
    base: str
    display: str
    discs: list[Disc] = field(default_factory=list)
    canonical: Disc | None = None
    kind: str = "game"

    @property
    def game_ids(self) -> set[str]:
        return {disc.game_id for disc in self.discs if disc.game_id}

    @property
    def variants(self) -> int:
        return max(1, len(self.game_ids))


def strip_tags(value: str) -> str:
    value = re.sub(r"<.*?>", "", value, flags=re.S)
    value = html_module.unescape(value)
    return " ".join(value.split())


def load_mhtml(path: Path) -> str:
    message = email.message_from_file(
        path.open("r", encoding="utf-8", errors="replace"),
        policy=policy.default,
    )
    for part in message.walk():
        if part.get_content_type() == "text/html":
            return part.get_content()
    raise RuntimeError(f"No text/html part found in {path}")


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


def parse_fourdo(path: Path) -> list[Reference]:
    tables = re.findall(r"<table.*?</table>", load_mhtml(path), flags=re.S)
    if len(tables) < 5:
        raise RuntimeError(f"Expected at least 5 tables in {path}, found {len(tables)}")

    records: list[Reference] = []
    for row in table_rows(tables[2])[1:]:
        if len(row) >= 4 and row[0]:
            records.append(
                Reference(
                    source="4DO",
                    category="Main list",
                    name=row[0],
                    score=parse_int(row[1]),
                    version=row[2],
                    notes=row[3],
                    status=status_for_score(parse_int(row[1])),
                )
            )
    for row in table_rows(tables[4])[1:]:
        if len(row) >= 5 and row[1]:
            records.append(
                Reference(
                    source="4DO",
                    category=row[0] or "Uncategorised",
                    name=row[1],
                    score=parse_int(row[2]),
                    version=row[3],
                    notes=row[4],
                    status=status_for_score(parse_int(row[2])),
                )
            )
    return records


def parse_phoenix(path: Path) -> list[Reference]:
    tables = re.findall(r"<table.*?</table>", load_mhtml(path), flags=re.S)
    if not tables:
        raise RuntimeError(f"No table found in {path}")

    records: list[Reference] = []
    for row in table_rows(max(tables, key=len))[1:]:
        if len(row) < 2 or not row[0]:
            continue
        percent = parse_percent(row[1])
        records.append(
            Reference(
                source="Phoenix",
                category="",
                name=row[0],
                percent=percent,
                status=status_for_percent(percent),
                notes=row[2] if len(row) > 2 else "",
            )
        )
    return records


def parse_int(value: str) -> int | None:
    match = re.search(r"-?\d+", value or "")
    return int(match.group()) if match else None


def parse_percent(value: str) -> int | None:
    match = re.search(r"(\d+)\s*%", value or "")
    return int(match.group(1)) if match else None


def status_for_score(score: int | None) -> str:
    if score is None:
        return "No data"
    if score == 0:
        return "Not Loadable"
    if score < 50:
        return "Unplayable"
    if score < 80:
        return "Playable"
    return "Good"


def status_for_percent(percent: int | None) -> str:
    if percent is None:
        return "No data"
    if percent == 0:
        return "Not Loadable"
    if percent < 50:
        return "Unplayable"
    if percent < 80:
        return "Playable"
    return "Good"


def normalize_title(value: str) -> str:
    value = html_module.unescape(value or "").lower()
    value = re.sub(r"\.(chd|iso|cue|bin)$", " ", value)
    value = re.sub(r"<[^>]+>", " ", value)
    value = re.sub(r"\([^)]*\)", " ", value)
    value = re.sub(r"\[[^\]]*\]", " ", value)
    value = value.replace("&", " and ")
    value = re.sub(r"\b(disc|disk)\s+[a-z0-9]+\b", " ", value)
    value = re.sub(r"\brev(?:ision)?\s+[a-z0-9]+\b", " ", value)
    value = re.sub(r"\bv\d+(?:\.\d+)*\b", " ", value)
    value = re.sub(r"\s+[a-z]{1,4}\d+[a-z0-9.]*(?:\s+\d+)*\s*$", " ", value)
    value = re.sub(r"[^a-z0-9]+", " ", value)
    value = re.sub(r"\b(the|a|an)\b", " ", value)
    return " ".join(value.split())


def compact(value: str) -> str:
    return value.replace(" ", "")


def title_key(value: str) -> str:
    base = normalize_title(value)
    return LOCAL_TITLE_ALIASES.get(base, base)


def sequel_marker(value: str) -> tuple:
    return tuple(sorted(set(value.split()) & {"2", "3", "4", "ii", "iii", "iv", "return"}))


def fuzzy_compatible(candidate: str, key: str) -> bool:
    return sequel_marker(candidate) == sequel_marker(key)


def reference_keys(reference: Reference) -> list[str]:
    raw = reference.name
    candidates = [normalize_title(raw)]

    if raw.lower().endswith(", the"):
        candidates.append(normalize_title(raw[:-5]))
    if "/" in raw:
        candidates.extend(normalize_title(part) for part in raw.split("/"))
    for separator in (" - ", ":"):
        if separator in raw:
            candidates.append(normalize_title(raw.split(separator, 1)[0]))
    for candidate in list(candidates):
        if candidate in TITLE_ALIASES:
            candidates.append(TITLE_ALIASES[candidate])
        if candidate in LOCAL_TITLE_ALIASES:
            candidates.append(LOCAL_TITLE_ALIASES[candidate])

    result: list[str] = []
    for candidate in candidates:
        if candidate and candidate not in result:
            result.append(candidate)
    return result


def disc_kind(filename: str, audio_cd: bool) -> str:
    lowered = filename.lower()
    if audio_cd:
        return "audio"
    if re.search(r"\[h[^\]]*\]|\(hack\)|\bhack\b", lowered):
        return "hack"
    if re.search(r"\[tr[^\]]*\]|\(translation\)|\btranslated\b", lowered):
        return "translation"
    if any(re.search(rf"\b{re.escape(word)}\b", lowered) for word in NON_GAME_WORDS):
        return "media"
    if re.search(r"\b(demo|beta|proto|prototype|alpha|sample|sampler|preview|promo|kiosk)\b", lowered):
        return "demo"
    if re.search(r"\bunl\b", lowered):
        return "homebrew"
    return "game"


def clean_display_name(filename: str) -> str:
    """Turn a dump filename into a human-readable title."""
    value = Path(filename).stem
    value = re.sub(r"\[[^\]]*\]", " ", value)
    value = re.sub(r"\([^)]*\)", " ", value)
    value = re.sub(r"\bv\d+(?:\.\d+)*\b", " ", value, flags=re.I)
    value = re.sub(r"\s+[A-Za-z]{1,4}\d+[A-Za-z0-9.]*(?:\s+\d+)*\s*$", " ", value)
    value = re.sub(r"\s+", " ", value)
    value = value.strip(" -_")
    return value or Path(filename).stem


def disc_penalty(filename: str) -> int:
    lowered = filename.lower()
    penalty = 0
    if re.search(r"\[h[^\]]*\]|\(hack\)|\bhack\b", lowered):
        penalty += 60
    if re.search(r"\[tr[^\]]*\]|\(translation\)|\btranslated\b", lowered):
        penalty += 50
    if re.search(r"\[[abf][^\]]*\]", lowered):
        penalty += 20
    if re.search(r"\b(beta|proto|prototype|alpha)\b", lowered):
        penalty += 40
    if re.search(r"\b(demo|sample|preview|promo|kiosk)\b", lowered):
        penalty += 30
    return penalty


def disc_score(disc: Disc) -> int:
    score = 100 - disc_penalty(disc.file)
    score += region_score(disc.regions)
    if disc.game_id:
        score += 20
    if "[!]" in disc.file:
        score += 12
    if disc.disc_number in (None, 1):
        score += 5
    if disc.revision:
        score += 2
    return score


def load_inventory(path: Path) -> list[Disc]:
    records = json.loads(path.read_text(encoding="utf-8"))
    discs: list[Disc] = []
    for record in records:
        discs.append(
            Disc(
                path=record.get("path", ""),
                file=record.get("file", ""),
                root=record.get("root", ""),
                game_id=record.get("gameId", "") or "",
                name=record.get("name", "") or "",
                regions=record.get("regions", "") or "",
                revision=record.get("revision", "") or "",
                disc_number=record.get("discNumber"),
                track_count=record.get("trackCount"),
                audio_cd=bool(record.get("audioCd")),
                known=bool(record.get("known")),
                error=record.get("error", "") or "",
                size=int(record.get("bytes") or 0),
            )
        )
    return discs


def build_unique(discs: list[Disc]) -> list[UniqueTitle]:
    """Collapse identical game IDs, then collapse variants of one title."""
    by_id: dict[str, list[Disc]] = defaultdict(list)
    no_id: list[Disc] = []
    for disc in discs:
        if disc.game_id:
            by_id[disc.game_id].append(disc)
        else:
            no_id.append(disc)

    # One representative per distinct disc content.
    content_groups: list[list[Disc]] = [sorted(group, key=disc_score, reverse=True) for group in by_id.values()]
    content_groups.extend([disc] for disc in no_id)

    titles: dict[str, UniqueTitle] = {}
    for group in content_groups:
        base = title_key(group[0].file)
        group_key = compact(base)
        title = titles.setdefault(group_key, UniqueTitle(base=base, display=clean_display_name(group[0].file)))
        title.discs.extend(group)

    for title in titles.values():
        title.discs.sort(key=disc_score, reverse=True)
        title.canonical = title.discs[0]
        title.display = clean_display_name(title.canonical.file)
        title.kind = disc_kind(title.canonical.file, title.canonical.audio_cd)
    return sorted(titles.values(), key=lambda item: item.base)


def read_sweep(path: Path) -> dict[str, dict[str, str]]:
    if not path.exists():
        return {}
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        return {
            row["Path"].casefold(): row
            for row in csv.DictReader(stream)
            if row.get("Path")
        }


def read_manual_grades(path: Path) -> dict[str, tuple[str, str]]:
    if not path.exists():
        return {}
    grades: dict[str, tuple[str, str]] = {}
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        line = line.strip()
        if not line.startswith("|"):
            continue
        cells = [cell.strip() for cell in line.strip("|").split("|")]
        if len(cells) < 9:
            continue
        local, status, notes = cells[6], cells[7], cells[8]
        if status in ("", "No data"):
            continue
        for name in local.split("<br>"):
            name = name.replace("\\|", "|").strip()
            if name.lower().endswith(DISC_EXTENSIONS) and not name.startswith("+"):
                grades[name.casefold()] = (status, notes)
    return grades


def read_grade_csv(path: Path) -> dict[str, tuple[str, str]]:
    """Read the Tier B playability ledger (file,status,notes,batch)."""
    if not path.exists():
        return {}
    grades: dict[str, tuple[str, str]] = {}
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        for row in csv.DictReader(stream):
            name = (row.get("file") or "").strip()
            status = (row.get("status") or "").strip()
            if not name or not status:
                continue
            grades[name.casefold()] = (status, (row.get("notes") or "").strip())
    return grades


def read_beyond(path: Path) -> dict[str, tuple[str, str]]:
    """Read the committed beyond-title evidence ledger.

    The runner writes one row per canonical disc with the observed status,
    BIOS, disc hash, capture frame, screenshot, and failure reason. A missing
    ledger is allowed while the corpus pass is still being built. A non-empty
    ``manualStatus`` is an explicit human adjudication and wins over the
    automated status.
    """
    if not path.exists():
        return {}
    results: dict[str, tuple[str, str]] = {}
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        for row in csv.DictReader(stream):
            name = (row.get("file") or "").strip()
            status = (row.get("manualStatus") or row.get("status") or "").strip()
            if not name or not status:
                continue
            note = (row.get("notes") or row.get("failureReason") or "").strip()
            results[name.casefold()] = (status, note)
    return results


def read_peer_invalid(path: Path) -> tuple[set[str], set[str], dict[str, str]]:
    """Read explicit peer-claim invalidation records from the ledger."""
    fourdo: set[str] = set()
    phoenix: set[str] = set()
    evidence: dict[str, str] = {}
    if not path.exists():
        return fourdo, phoenix, evidence
    positive = {"yes", "true", "1", "y"}
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        for row in csv.DictReader(stream):
            name = (row.get("file") or "").strip().casefold()
            if not name:
                continue
            if (row.get("peerInvalid4do") or "").strip().lower() in positive:
                fourdo.add(name)
            if (row.get("peerInvalidPhoenix") or "").strip().lower() in positive:
                phoenix.add(name)
            if row.get("peerEvidence"):
                evidence[name] = row["peerEvidence"]
    return fourdo, phoenix, evidence


# A title is playable when it reaches a distinct screen beyond the title. The
# five-level quality scale is kept as information only; it must not turn a
# reached beyond-title screen into a losing row against a peer's ``Good``.
FIVE_DO_PLAYABLE = {"Good", "Playable", "Playable (beyond-title)"}
FOUR_DO_PLAYABLE = {"Good", "Playable"}
PHOENIX_PLAYABLE = {"Good"}
BEYOND_FAILED = {"Title-only", "Unplayable", "No video", "Not loadable"}


def is_playable(label: str, source: str) -> bool:
    if source == "4DO":
        return label in FOUR_DO_PLAYABLE
    if source == "Phoenix":
        return label in PHOENIX_PLAYABLE
    return label in FIVE_DO_PLAYABLE


def observed_status(
    title: UniqueTitle,
    sweep: dict[str, dict[str, str]],
    manual: dict[str, tuple[str, str]],
    beyond: dict[str, tuple[str, str]] | None = None,
) -> tuple[str, str, str]:
    """Return (label, detail, evidence state).

    Precedence is beyond-title evidence, then a manual grade, then the
    automated loadability sweep. A ``Rendered``/untested title is *unverified*,
    not a failure: it becomes ``Behind`` only when a beyond-title pass fails.
    """
    ordered = [title.canonical] + [disc for disc in title.discs if disc is not title.canonical]
    for disc in ordered:
        if disc is None:
            continue
        result = (beyond or {}).get(disc.file.casefold())
        if result:
            status, notes = result
            return status, notes, "beyond-title"

    for disc in ordered:
        if disc is None:
            continue
        grade = manual.get(disc.file.casefold())
        if grade:
            return grade[0], grade[1], "reviewed"

    statuses = []
    for disc in ordered:
        if disc is None:
            continue
        row = sweep.get(disc.path.casefold())
        if row:
            statuses.append(row)
    if not statuses:
        return "Untested", "", "none"
    if any(row.get("Status") == "Loadable/Rendered" for row in statuses):
        return "Rendered", "automated loadability only", "loadability"
    if any(row.get("Status") == "Loadable/No video" for row in statuses):
        return "No video", "automated loadability only", "loadability"
    return "Not loadable", statuses[0].get("Reason", ""), "loadability"


def compare(label: str, evidence: str, peer_status: str, peer_source: str) -> str:
    """Compare on the binary playable rule.

    ``Behind`` is reserved for a title that was actually tested beyond the
    title and failed. An untested/``Rendered`` title is ``Unverified``. A peer
    playable title that 5DO cannot even render is a ``Regression``.
    """
    if not peer_status or peer_status == "No data":
        return "—"
    peer = is_playable(peer_status, peer_source)
    five = is_playable(label, "5DO")
    if five and peer:
        return "Parity"
    if five and not peer:
        return "5DO ahead"
    if not five and not peer:
        return "—"
    # 5DO is not playable while the peer is. Classify the evidence.
    if label in ("No video", "Not loadable") and evidence != "beyond-title":
        return "Regression"
    if evidence in ("beyond-title", "reviewed"):
        return "Behind"
    return "Unverified"


def match_references(
    references: list[Reference],
    titles: list[UniqueTitle],
) -> dict[str, list[Reference]]:
    by_key: dict[str, list[UniqueTitle]] = defaultdict(list)
    by_compact: dict[str, list[UniqueTitle]] = defaultdict(list)
    for title in titles:
        keys = {title.base}
        if title.canonical:
            keys.add(title_key(title.canonical.file))
        for key in keys:
            by_key[key].append(title)
            by_compact[compact(key)].append(title)

    matched: dict[str, list[Reference]] = defaultdict(list)
    for reference in references:
        candidates = reference_keys(reference)
        found: list[UniqueTitle] = []

        # Most specific candidate wins; do not union a full title with its
        # short subtitle fallback.
        for candidate in candidates:
            found = by_key.get(candidate, [])
            if found:
                break
        if not found:
            for candidate in candidates:
                found = by_compact.get(compact(candidate), [])
                if found:
                    break
        if not found:
            for candidate in candidates:
                if len(candidate) < 6:
                    continue
                close = [
                    title
                    for title in titles
                    if candidate in title.base and fuzzy_compatible(candidate, title.base)
                ]
                if close:
                    found = close
                    break
        if not found:
            for candidate in candidates:
                close = difflib.get_close_matches(
                    candidate, [title.base for title in titles], n=2, cutoff=0.9
                )
                found = [
                    title
                    for title in titles
                    if title.base in close and fuzzy_compatible(candidate, title.base)
                ]
                if found:
                    break

        for title in found:
            matched[title.base].append(reference)
    return matched


def best_reference(references: list[Reference]) -> Reference | None:
    order = {"Good": 3, "Playable": 2, "Unplayable": 1, "Not Loadable": 0, "No data": -1}
    if not references:
        return None
    return max(references, key=lambda item: order.get(item.status, -1))


def escape_cell(value: str) -> str:
    return (value or "").replace("|", "\\|").replace("\n", " ").strip()


def unique_csv_text(titles: list[UniqueTitle]) -> str:
    fields = (
        "title",
        "kind",
        "canonical_file",
        "regions",
        "gameId",
        "variants",
        "images",
        "duplicates",
    )
    stream = io.StringIO()
    writer = csv.DictWriter(stream, fieldnames=fields, lineterminator="\n")
    writer.writeheader()
    for title in titles:
        canonical = title.canonical
        if canonical is None:
            continue
        writer.writerow(
            {
                "title": title.display or title.base,
                "kind": title.kind,
                "canonical_file": canonical.file,
                "regions": canonical.regions,
                "gameId": canonical.game_id,
                "variants": title.variants,
                "images": len(title.discs),
                "duplicates": max(0, len(title.discs) - title.variants),
            }
        )
    return stream.getvalue()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inventory", default=DEFAULT_INVENTORY)
    parser.add_argument(
        "--sweep",
        action="append",
        default=None,
        help="Sweep summary CSV. Repeat to merge several runs.",
    )
    parser.add_argument("--reference", default=DEFAULT_REFERENCE)
    parser.add_argument(
        "--grades",
        default=DEFAULT_GRADES,
        help="Optional Tier B manual-grade ledger (CSV).",
    )
    parser.add_argument(
        "--beyond",
        default=DEFAULT_BEYOND,
        help="Optional beyond-title evidence ledger (CSV).",
    )
    parser.add_argument("--fourdo", default=None)
    parser.add_argument("--phoenix", default=None)
    parser.add_argument("--out", default=DEFAULT_OUT)
    parser.add_argument(
        "--check",
        action="store_true",
        help="Fail if the committed outputs are stale instead of rewriting them.",
    )
    parser.add_argument("--unique-out", default=DEFAULT_UNIQUE)
    parser.add_argument(
        "--testset-out",
        default="artifacts/corpus/unique-testset.txt",
        help="Plain path list for the canonical unique game test set.",
    )
    parser.add_argument("--json-out", default=DEFAULT_JSON)
    args = parser.parse_args()

    import glob

    fourdo_path = Path(args.fourdo) if args.fourdo else Path(sorted(glob.glob(DEFAULT_FOURDO))[0])
    phoenix_path = Path(args.phoenix) if args.phoenix else Path(sorted(glob.glob(DEFAULT_PHOENIX))[0])

    discs = load_inventory(Path(args.inventory))
    titles = build_unique(discs)
    sweep: dict[str, dict[str, str]] = {}
    for sweep_path in args.sweep or [DEFAULT_SWEEP]:
        sweep.update(read_sweep(Path(sweep_path)))
    manual = read_manual_grades(Path(args.reference))
    manual.update(read_grade_csv(Path(args.grades)))
    beyond = read_beyond(Path(args.beyond))
    peer_invalid4do, peer_invalid_phoenix, peer_evidence = read_peer_invalid(Path(args.beyond))

    fourdo = parse_fourdo(fourdo_path)
    phoenix = parse_phoenix(phoenix_path)
    fourdo_match = match_references(fourdo, titles)
    phoenix_match = match_references(phoenix, titles)

    rows = []
    verdicts_4do = Counter()
    verdicts_phoenix = Counter()
    tested = Counter()
    for title in titles:
        canonical = title.canonical
        if canonical is None:
            continue
        label, detail, evidence = observed_status(title, sweep, manual, beyond)
        tested[evidence] += 1
        four = best_reference(fourdo_match.get(title.base, []))
        phx = best_reference(phoenix_match.get(title.base, []))
        file_key = canonical.file.casefold()
        four_invalid = file_key in peer_invalid4do
        phoenix_invalid = file_key in peer_invalid_phoenix
        vs4 = "Peer invalid" if four_invalid else compare(label, evidence, four.status if four else "", "4DO")
        vsp = "Peer invalid" if phoenix_invalid else compare(label, evidence, phx.status if phx else "", "Phoenix")
        if four and not four_invalid:
            verdicts_4do[vs4] += 1
        if phx and not phoenix_invalid:
            verdicts_phoenix[vsp] += 1
        rows.append(
            {
                "title": title.display or title.base,
                "base": title.base,
                "kind": title.kind,
                "canonical": canonical.file,
                "canonicalPath": canonical.path,
                "regions": canonical.regions,
                "images": len(title.discs),
                "variants": title.variants,
                "fiveDo": label,
                "fiveDoDetail": detail,
                "fiveDoEvidence": evidence,
                "playable": is_playable(label, "5DO"),
                "fourDo": four.status if four else "—",
                "fourDoScore": four.score if four else None,
                "fourDoNotes": four.notes if four else "",
                "fourDoInvalid": four_invalid,
                "phoenix": phx.status if phx else "—",
                "phoenixPercent": phx.percent if phx else None,
                "phoenixNotes": phx.notes if phx else "",
                "phoenixInvalid": phoenix_invalid,
                "peerEvidence": peer_evidence.get(file_key, ""),
                "vs4do": vs4,
                "vsPhoenix": vsp,
            }
        )

    rows.sort(key=lambda row: (row["kind"], normalize_title(row["title"])))

    # Console-friendly summary counts.
    fourdo_covered = sum(1 for row in rows if row["fourDo"] != "—")
    phoenix_covered = sum(1 for row in rows if row["phoenix"] != "—")
    both_covered = sum(1 for row in rows if row["fourDo"] != "—" and row["phoenix"] != "—")

    lines: list[str] = []
    lines.append("# 5DO, 4DO, and Phoenix Compatibility Comparison")
    lines.append("")
    lines.append(
        "This page compares the three emulators on the **same local collection**. "
        "The 4DO and Phoenix columns are transcribed from their saved compatibility "
        "lists; the 5DO column comes from the merged sweep summaries under "
        "`artifacts/compat-full` and `artifacts/compat-unique`, the manual grades in "
        "[`GameCompatibilityList.md`](GameCompatibilityList.md), and the committed "
        "beyond-title ledger in `docs/data/beyond-title-results.csv`. The per-title "
        "captures and failure reasons are in "
        "[`BeyondTitleEvidence.md`](BeyondTitleEvidence.md)."
    )
    lines.append("")
    lines.append("## Method")
    lines.append("")
    lines.append(
        "- **Unique titles only.** Identical discs are collapsed by the 5DO `gameId` "
        "(MD5 of the first two 2048-byte sectors); regional revisions of one title are "
        "grouped and one canonical disc is chosen for testing. The full mapping is in "
        "`docs/data/unique-corpus.csv`."
    )
    lines.append(
        "- **Playable is binary.** 5DO is playable when it reaches a distinct screen "
        "beyond the title and a `Good`, `Playable`, or automated beyond-title `Playable` "
        "evidence row exists. The quality grade remains visible but never turns a "
        "reached screen into a losing row."
    )
    lines.append(
        "- **Peer playable is binary.** 4DO `Good`/`Playable` and Phoenix `Good` count "
        "as peer-playable. Phoenix publishes a 100% list, so an unlisted title means "
        "*no data*, not a failure."
    )
    lines.append(
        "- **Unverified is not a failure.** A `Rendered` or untested title is excluded "
        "from the playable count and marked `Unverified`; it becomes `Behind` only "
        "after a beyond-title pass fails. `Regression` is reserved for a title that "
        "does not load or renders no video while a peer can play it."
    )
    lines.append("")
    lines.append("## Summary")
    lines.append("")
    primary = [title for title in titles if title.kind in ("game", "homebrew")]
    secondary = [title for title in titles if title.kind not in ("game", "homebrew")]
    primary_rows = [row for row in rows if row["kind"] in ("game", "homebrew")]

    def row_four_playable(row: dict) -> bool:
        return not row.get("fourDoInvalid") and is_playable(row["fourDo"], "4DO")

    def row_phoenix_playable(row: dict) -> bool:
        return not row.get("phoenixInvalid") and is_playable(row["phoenix"], "Phoenix")

    five_playable = sum(1 for row in primary_rows if row["playable"])
    four_playable = sum(1 for row in primary_rows if row_four_playable(row))
    phoenix_playable = sum(1 for row in primary_rows if row_phoenix_playable(row))
    peer_union = sum(
        1 for row in primary_rows if row_four_playable(row) or row_phoenix_playable(row)
    )
    unverified = sum(
        1
        for row in primary_rows
        if not row["playable"]
        and "Unverified" in (row["vs4do"], row["vsPhoenix"])
    )
    regressions = [
        row
        for row in primary_rows
        if "Regression" in (row["vs4do"], row["vsPhoenix"])
    ]
    behind = [
        row for row in primary_rows if "Behind" in (row["vs4do"], row["vsPhoenix"])
    ]
    failed_beyond = sum(
        1
        for row in primary_rows
        if row["fiveDoEvidence"] == "beyond-title" and row["fiveDo"] in BEYOND_FAILED
    )
    strict_superset = all(
        row["playable"]
        for row in primary_rows
        if row_four_playable(row) or row_phoenix_playable(row)
    )
    target = 254

    lines.append(f"- Local images inventoried: **{len(discs)}**")
    lines.append(f"- Unique titles: **{len(titles)}**")
    lines.append(f"  - Playable test set (game/homebrew): **{len(primary)}**")
    lines.append(f"  - Secondary media (demo/media/hack/translation/audio): **{len(secondary)}**")
    lines.append(f"- Titles matched to 4DO: **{fourdo_covered}**")
    lines.append(f"- Titles matched to Phoenix: **{phoenix_covered}**")
    lines.append(f"- Titles matched to both: **{both_covered}**")
    lines.append("")
    lines.append("### Playable-set target")
    lines.append("")
    lines.append("| Measure | Titles |")
    lines.append("|---|---:|")
    lines.append(f"| 5DO playable | **{five_playable}** |")
    lines.append(f"| 4DO playable | {four_playable} |")
    lines.append(f"| Phoenix playable | {phoenix_playable} |")
    lines.append(f"| Peer union | {peer_union} |")
    lines.append(f"| 5DO unverified (test set) | {unverified} |")
    lines.append(f"| 5DO failed beyond-title (test set) | {failed_beyond} |")
    lines.append(f"| 5DO `Behind` rows | {len(behind)} |")
    lines.append(f"| 5DO `Regression` rows | {len(regressions)} |")
    lines.append(f"| Strict superset | {'yes' if strict_superset else 'no'} |")
    lines.append(f"| Numeric target (≥{target}) | {'yes' if five_playable >= target else 'no'} |")
    lines.append("")
    lines.append(
        "The target is measured on the 378 game/homebrew titles. The 36 secondary "
        "demo/media/hack/translation/audio titles are loadability-only and do not enter "
        "the playable count; their `Unverified` rows in the table below are expected."
    )
    lines.append("")
    lines.append("| 5DO evidence | Titles |")
    lines.append("|---|---:|")
    for key, label in (
        ("beyond-title", "Beyond-title pass"),
        ("reviewed", "Manual gameplay grade"),
        ("loadability", "Automated loadability"),
        ("none", "Untested"),
    ):
        lines.append(f"| {label} | {tested.get(key, 0)} |")
    lines.append("")
    lines.append("### 5DO vs 4DO")
    lines.append("")
    lines.append("| Verdict | Titles |")
    lines.append("|---|---:|")
    for key in ("5DO ahead", "Parity", "Unverified", "Behind", "Regression", "Peer invalid", "—"):
        if verdicts_4do.get(key):
            lines.append(f"| {key} | {verdicts_4do[key]} |")
    lines.append("")
    lines.append("### 5DO vs Phoenix")
    lines.append("")
    lines.append("| Verdict | Titles |")
    lines.append("|---|---:|")
    for key in ("5DO ahead", "Parity", "Unverified", "Behind", "Regression", "Peer invalid", "—"):
        if verdicts_phoenix.get(key):
            lines.append(f"| {key} | {verdicts_phoenix[key]} |")
    lines.append("")
    lines.append(
        "`Behind` means a tested title failed to reach beyond-title while its peer can "
        "play it. `Unverified` means the title has not been tested beyond the title yet. "
        "`Regression` is the hard failure: a peer reports the title as playable, but 5DO "
        "produced no usable frame."
    )
    lines.append("")
    lines.append("## Unique titles")
    lines.append("")
    lines.append(
        "`5DO` is the observed state: `Rendered`/`No video`/`Not loadable` are automated, "
        "`Good`/`Playable`/`Unplayable` are manual grades, and beyond-title results come "
        "from `docs/data/beyond-title-results.csv`. `Images` is the number of local "
        "files collapsed into this title; `Variants` counts distinct game IDs."
    )
    lines.append("")
    lines.append(
        "| Title | Kind | Canonical disc | Images | Variants | 5DO | 4DO | vs 4DO | Phoenix | vs Phoenix |"
    )
    lines.append("|---|---|---|---:|---:|---|---|---|---|---|")
    for row in rows:
        four = row["fourDo"]
        if row["fourDoScore"] is not None:
            four = f"{four} ({row['fourDoScore']})"
        phx = row["phoenix"]
        if row["phoenixPercent"] is not None:
            phx = f"{phx} ({row['phoenixPercent']}%)"
        lines.append(
            "| "
            + " | ".join(
                [
                    escape_cell(row["title"]),
                    escape_cell(row["kind"]),
                    escape_cell(row["canonical"]),
                    str(row["images"]),
                    str(row["variants"]),
                    escape_cell(row["fiveDo"]),
                    escape_cell(four),
                    escape_cell(row["vs4do"]),
                    escape_cell(phx),
                    escape_cell(row["vsPhoenix"]),
                ]
            )
            + " |"
        )
    lines.append("")
    lines.append("## Regressions to triage")
    lines.append("")
    regression_rows = [row for row in rows if "Regression" in (row["vs4do"], row["vsPhoenix"])]
    if not regression_rows:
        lines.append("None.")
    else:
        lines.append(
            "These titles load on a peer but produced `No video` or `Not loadable` in 5DO. "
            "They are the first gameplay blockers to investigate."
        )
        lines.append("")
        lines.append("| Title | 5DO | 4DO | Phoenix |")
        lines.append("|---|---|---|---|")
        for row in regression_rows:
            lines.append(
                "| "
                + " | ".join(
                    [
                        escape_cell(row["title"]),
                        escape_cell(row["fiveDo"]),
                        escape_cell(row["fourDo"]),
                        escape_cell(row["phoenix"]),
                    ]
                )
                + " |"
            )
    lines.append("")
    lines.append("## Titles where 5DO is ahead")
    lines.append("")
    wins = [row for row in rows if "5DO ahead" in (row["vs4do"], row["vsPhoenix"])]
    if not wins:
        lines.append("None.")
    else:
        lines.append(
            "The peer reports `Unplayable`/`Not Loadable`, but 5DO is playable under the "
            "beyond-title rule. Each row carries a manual or automated beyond-title "
            "evidence record."
        )
        lines.append("")
        lines.append("| Title | 5DO | 4DO | Phoenix |")
        lines.append("|---|---|---|---|")
        for row in wins:
            lines.append(
                "| "
                + " | ".join(
                    [
                        escape_cell(row["title"]),
                        escape_cell(row["fiveDo"]),
                        escape_cell(row["fourDo"]),
                        escape_cell(row["phoenix"]),
                    ]
                )
                + " |"
            )
    lines.append("")
    lines.append("## Peer-claim invalidations")
    lines.append("")
    invalidated = [row for row in rows if row.get("fourDoInvalid") or row.get("phoenixInvalid")]
    if not invalidated:
        lines.append("None.")
    else:
        lines.append(
            "A peer claim is removed only with a beyond-title capture and an explicit "
            "`peerInvalid`/`peerEvidence` record. The peer's raw status remains visible in "
            "the table above."
        )
        lines.append("")
        lines.append("| Title | Peer | Raw status | Evidence |")
        lines.append("|---|---|---|---|")
        for row in invalidated:
            peers = []
            if row.get("fourDoInvalid"):
                peers.append("4DO")
            if row.get("phoenixInvalid"):
                peers.append("Phoenix")
            raw = []
            if row.get("fourDoInvalid"):
                raw.append(f"4DO {row.get('fourDo')}")
            if row.get("phoenixInvalid"):
                raw.append(f"Phoenix {row.get('phoenix')}")
            lines.append(
                "| "
                + " | ".join(
                    [
                        escape_cell(row["title"]),
                        escape_cell(", ".join(peers)),
                        escape_cell("; ".join(raw)),
                        escape_cell(row.get("peerEvidence", "")),
                    ]
                )
                + " |"
            )
    lines.append("")

    output = Path(args.out)
    testset = [
        title.canonical.path
        for title in titles
        if title.canonical and title.kind in ("game", "homebrew")
    ]
    testset_path = Path(args.testset_out)
    json_text = (
        json.dumps(
            {
                "summary": {
                    "images": len(discs),
                    "uniqueTitles": len(titles),
                    "testSet": len(primary),
                    "fiveDoPlayable": five_playable,
                    "fourDoPlayable": four_playable,
                    "phoenixPlayable": phoenix_playable,
                    "peerUnion": peer_union,
                    "unverified": unverified,
                    "failedBeyondTitle": failed_beyond,
                    "behind": len(behind),
                    "regressions": len(regressions),
                    "strictSuperset": strict_superset,
                    "target": target,
                    "targetMet": five_playable >= target,
                    "fourdoMatched": fourdo_covered,
                    "phoenixMatched": phoenix_covered,
                    "bothMatched": both_covered,
                    "fiveDo": dict(tested),
                    "vs4do": dict(verdicts_4do),
                    "vsPhoenix": dict(verdicts_phoenix),
                },
                "titles": rows,
            },
            indent=2,
            ensure_ascii=False,
        )
        + "\n"
    )
    contents = {
        output: "\n".join(lines),
        Path(args.unique_out): unique_csv_text(titles),
        testset_path: "\n".join(testset) + "\n",
        Path(args.json_out): json_text,
    }

    if args.check:
        stale = [
            str(path)
            for path, content in contents.items()
            if not path.exists() or path.read_text(encoding="utf-8") != content
        ]
        if stale:
            print("stale comparison outputs: " + ", ".join(stale), file=sys.stderr)
            return 1
        print("comparison outputs are current")
        return 0

    for path, content in contents.items():
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")

    print(
        f"Wrote {output}, {args.unique_out}, {args.json_out}, {testset_path}: "
        f"{len(discs)} images -> {len(titles)} unique titles ({len(testset)} game test set); "
        f"4DO matched {fourdo_covered}, Phoenix matched {phoenix_covered}, both {both_covered}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
